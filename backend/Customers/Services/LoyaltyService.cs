using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Services;

public class LoyaltyService(
    ILoyaltyRepository loyaltyRepository,
    ICustomerRepository customerRepository) : ILoyaltyService
{
    public async Task<LoyaltyChange> AwardForOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!order.CustomerId.HasValue)
            return NotEligible("The order has no customer.");
        if (order.Status != OrderStatus.Completed)
            return NotEligible("Points are earned only when the order is completed.");

        // Lock first: two calls for one customer (or one order) then run one after the other.
        var customer = await loyaltyRepository.GetForUpdateAsync(order.CustomerId.Value, cancellationToken);
        if (customer == null)
            return NotEligible("Customer was not found.");

        var key = $"earn:{order.Id}";
        var existing = await loyaltyRepository.FindByReferenceKeyAsync(key, cancellationToken);
        if (existing != null)
            return new LoyaltyChange(LoyaltyOutcome.AlreadyApplied, existing.PointsChange, existing.BalanceAfter, customer.Tier);

        var points = LoyaltyRules.PointsFor(order.Subtotal - order.DiscountAmount);
        if (points == 0)
            return new LoyaltyChange(LoyaltyOutcome.NoPoints, 0, customer.LoyaltyPoints, customer.Tier);

        return Apply(customer, order, points, key, $"Earned {points} points for order {order.OrderCode}");
    }

    public async Task<LoyaltyChange> ReverseForReturnAsync(Order order, Guid returnId, decimal remainingGoodsAmount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!order.CustomerId.HasValue)
            return NotEligible("The order has no customer.");

        var customer = await loyaltyRepository.GetForUpdateAsync(order.CustomerId.Value, cancellationToken);
        if (customer == null)
            return NotEligible("Customer was not found.");

        var key = $"reverse:{returnId}";
        var existing = await loyaltyRepository.FindByReferenceKeyAsync(key, cancellationToken);
        if (existing != null)
            return new LoyaltyChange(LoyaltyOutcome.AlreadyApplied, existing.PointsChange, existing.BalanceAfter, customer.Tier);

        // Points the order still holds, against what the goods the customer keeps would earn.
        var held = await loyaltyRepository.SumPointsForOrderAsync(order.Id, cancellationToken);
        var shouldKeep = LoyaltyRules.PointsFor(remainingGoodsAmount);
        var deduction = Math.Min(held - shouldKeep, customer.LoyaltyPoints);
        if (deduction <= 0)
            return new LoyaltyChange(LoyaltyOutcome.NoPoints, 0, customer.LoyaltyPoints, customer.Tier);

        return Apply(customer, order, -deduction, key, $"Reversed {deduction} points for return {returnId} of order {order.OrderCode}");
    }

    public async Task<CustomerOperationResult<LoyaltySummaryDto>> GetSummaryAsync(Guid customerId, RoleType role, CancellationToken cancellationToken = default)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        // Same visibility as the profile: SalesStaff serves active customers only.
        if (customer == null || (!customer.IsActive && role is not (RoleType.Admin or RoleType.Manager)))
            return CustomerOperationResult<LoyaltySummaryDto>.NotFound();

        var next = LoyaltyRules.NextTier(customer.Tier);
        return CustomerOperationResult<LoyaltySummaryDto>.Success(new LoyaltySummaryDto
        {
            CustomerId = customer.Id,
            Points = customer.LoyaltyPoints,
            Tier = customer.Tier.ToString(),
            TierDiscountPercent = LoyaltyRules.DiscountPercentFor(customer.Tier),
            NextTier = next?.Tier.ToString(),
            PointsToNextTier = next.HasValue ? Math.Max(0, next.Value.RequiredPoints - customer.LoyaltyPoints) : null
        });
    }

    // Stages one ledger row and moves the customer's balance and tier; the caller saves.
    private LoyaltyChange Apply(Customer customer, Order order, int pointsChange, string key, string reason)
    {
        customer.LoyaltyPoints += pointsChange;
        customer.Tier = LoyaltyRules.TierFor(customer.LoyaltyPoints);
        loyaltyRepository.Add(new CustomerLoyaltyPoint
        {
            CustomerId = customer.Id,
            OrderId = order.Id,
            PointsChange = pointsChange,
            BalanceAfter = customer.LoyaltyPoints,
            Reason = reason,
            ReferenceKey = key
        });
        return new LoyaltyChange(LoyaltyOutcome.Applied, pointsChange, customer.LoyaltyPoints, customer.Tier);
    }

    private static LoyaltyChange NotEligible(string reason) =>
        new(LoyaltyOutcome.NotEligible, 0, 0, CustomerTier.Standard, reason);
}
