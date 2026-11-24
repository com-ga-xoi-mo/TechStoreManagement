using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;
using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Services;

public sealed class RefundLoyaltyService(ILoyaltyPointRepository journal, TimeProvider time) : IRefundLoyaltyService
{
    public async Task StageSaleAsync(Guid orderId, Guid customerId, decimal netAmount, CancellationToken ct)
    {
        var points = PointsFor(netAmount);
        if (points == 0) return;
        var customer = await journal.LockCustomerAsync(customerId, ct);
        var entries = await journal.OrderEntriesAsync(orderId, customerId, ct);
        if (!entries.Any(p => p.IsSaleAccrual))
            Stage(customer, orderId, null, points);
    }

    public async Task StageReversalAsync(Guid refundId, Guid orderId, Guid customerId, decimal remainingNetAmount, CancellationToken ct)
    {
        var customer = await journal.LockCustomerAsync(customerId, ct);
        var entries = await journal.OrderEntriesAsync(orderId, customerId, ct);
        if (!entries.Any(p => p.IsSaleAccrual) || entries.Any(p => p.RefundCaseId == refundId)) return;

        var retainedPoints = PointsFor(remainingNetAmount);
        var outstandingPoints = entries.Sum(p => p.PointsChange);
        if (outstandingPoints > retainedPoints)
            Stage(customer, orderId, refundId, retainedPoints - outstandingPoints);
    }

    private static int PointsFor(decimal netAmount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(netAmount);
        return checked((int)decimal.Floor(netAmount / 100_000m));
    }

    private void Stage(Customer customer, Guid orderId, Guid? refundId, int delta)
    {
        var balance = checked(customer.LoyaltyPoints + delta);
        if (balance < 0) throw new InvalidOperationException("The loyalty journal does not cover the refund.");
        var now = time.GetUtcNow().UtcDateTime;
        customer.LoyaltyPoints = balance;
        customer.Tier = balance switch
        {
            >= 5_000 => CustomerTier.Platinum,
            >= 1_500 => CustomerTier.Gold,
            >= 500 => CustomerTier.Silver,
            _ => CustomerTier.Standard
        };
        customer.UpdatedAt = now;
        journal.Add(new CustomerLoyaltyPoint
        {
            CustomerId = customer.Id, OrderId = orderId, RefundCaseId = refundId, IsSaleAccrual = refundId == null,
            PointsChange = delta, BalanceAfter = balance, CreatedAt = now,
            Reason = refundId == null ? "Tích điểm mua hàng" : "Đảo điểm hoàn hàng"
        });
    }
}
