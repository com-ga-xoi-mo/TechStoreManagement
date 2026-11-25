using System.Net;
using System.Text.Json;
using TechStore.Client.Models.Loyalty;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;

namespace TechStore.Client.Repositories.Loyalty;

public sealed class LoyaltyRepository(IApiClient apiClient) : ILoyaltyRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LoyaltySummary?> GetSummaryAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("A customer id is required.", nameof(customerId));
        JsonElement json;
        try
        {
            json = await apiClient.GetAsync<JsonElement>($"api/customers/{customerId:D}/loyalty-summary", cancellationToken);
        }
        catch (ApiException exception) when (exception.Kind == ApiErrorKind.Http && exception.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }

        var dto = Deserialize(json);
        if (dto.CustomerId != customerId
            || dto.Points < 0
            || string.IsNullOrWhiteSpace(dto.Tier)
            || dto.TierDiscountPercent is < 0 or > 100
            || dto.PointsToNextTier < 0)
            throw InvalidResponse("The loyalty summary contains invalid values.");
        return new(dto.CustomerId, dto.Points, dto.Tier, dto.TierDiscountPercent, dto.NextTier, dto.PointsToNextTier);
    }

    private static LoyaltySummaryDto Deserialize(JsonElement json)
    {
        try
        {
            return json.Deserialize<LoyaltySummaryDto>(JsonOptions) ?? throw InvalidResponse("The loyalty summary response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("The loyalty summary response did not match the API contract.", exception);
        }
    }

    private static ApiException InvalidResponse(string message, Exception? inner = null) =>
        new(ApiErrorKind.InvalidResponse, message, innerException: inner);
}
