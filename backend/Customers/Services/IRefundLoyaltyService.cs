namespace TechStore.Api.Customers.Services;

// The Customers module owns the points formula, tier and idempotent journal entry.
// Stage the reversal on the caller's UnitOfWork using the original sale and remaining net merchandise.
public interface IRefundLoyaltyService
{
    Task StageSaleAsync(Guid orderId, Guid customerId, decimal netAmount, CancellationToken ct);
    Task StageReversalAsync(Guid refundId, Guid orderId, Guid customerId, decimal remainingNetAmount, CancellationToken ct);
}
