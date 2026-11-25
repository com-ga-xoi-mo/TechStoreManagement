using TechStore.Client.Models.Loyalty;

namespace TechStore.Client.Repositories.Loyalty;

public interface ILoyaltyRepository
{
    /// <summary>Points, tier and next tier of a customer, or null when the customer is not found.</summary>
    Task<LoyaltySummary?> GetSummaryAsync(Guid customerId, CancellationToken cancellationToken = default);
}
