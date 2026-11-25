using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.Api.Customers.Services;

public enum LoyaltyOutcome
{
    /// <summary>A ledger row was staged and the customer's points and tier changed.</summary>
    Applied,
    /// <summary>The same order or return was already applied; nothing changed.</summary>
    AlreadyApplied,
    /// <summary>No points to add or take (below one point, or the order never earned any).</summary>
    NoPoints,
    /// <summary>The order cannot earn or lose points (no customer, not completed, unknown customer).</summary>
    NotEligible
}

public sealed record LoyaltyChange(LoyaltyOutcome Outcome, int PointsChange, int BalanceAfter, CustomerTier Tier, string? Reason = null);

/// <summary>
/// Points and VIP tier (owner: Vy). <see cref="AwardForOrderAsync"/> and <see cref="ReverseForReturnAsync"/> only stage
/// changes in the caller's unit of work: the caller opens the transaction, passes the tracked <see cref="Order"/> it is
/// completing or refunding, and saves once. They lock the customer row, so a transaction must be open.
/// </summary>
public interface ILoyaltyService
{
    /// <summary>
    /// Earns points for a <c>Completed</c> order with a customer: <c>floor((Subtotal - DiscountAmount) / 100,000)</c>.
    /// Idempotent per order (key <c>earn:{orderId}</c>).
    /// </summary>
    Task<LoyaltyChange> AwardForOrderAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalculates the order's points on the goods value the customer still keeps and deducts the difference, never below
    /// zero balance. Idempotent per return (key <c>reverse:{returnId}</c>).
    /// </summary>
    /// <param name="remainingGoodsAmount">Goods value after discounts and before VAT that stays with the customer after this return.</param>
    Task<LoyaltyChange> ReverseForReturnAsync(Order order, Guid returnId, decimal remainingGoodsAmount, CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<LoyaltySummaryDto>> GetSummaryAsync(Guid customerId, RoleType role, CancellationToken cancellationToken = default);
}
