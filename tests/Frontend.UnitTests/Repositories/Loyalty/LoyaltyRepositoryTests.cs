using TechStore.Client.Repositories.Loyalty;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Client.Services.Api;

namespace TechStore.Client.UnitTests.Repositories.Loyalty;

public sealed class LoyaltyRepositoryTests
{
    private static readonly Guid CustomerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task GetSummaryAsync_MapsSilverCustomer()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Summary(700, "Silver", 2, "\"Gold\"", "800"));
        var repository = new LoyaltyRepository(api);

        // Act
        var summary = await repository.GetSummaryAsync(CustomerId);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(CustomerId, summary.CustomerId);
        Assert.Equal(700, summary.Points);
        Assert.Equal("Silver", summary.Tier);
        Assert.Equal(2, summary.TierDiscountPercent);
        Assert.Equal("Gold", summary.NextTier);
        Assert.Equal(800, summary.PointsToNextTier);
        Assert.Equal(("GET", $"api/customers/{CustomerId:D}/loyalty-summary"), (api.Single.Method, api.Single.Path));
    }

    [Fact]
    public async Task GetSummaryAsync_TopTierHasNoNextTier()
    {
        // Arrange
        var repository = new LoyaltyRepository(new RecordingApiClient((_, _, _) => Summary(7000, "Platinum", 10, "null", "null")));

        // Act
        var summary = await repository.GetSummaryAsync(CustomerId);

        // Assert
        Assert.NotNull(summary);
        Assert.Null(summary.NextTier);
        Assert.Null(summary.PointsToNextTier);
    }

    [Fact]
    public async Task GetSummaryAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var repository = new LoyaltyRepository(new RecordingApiClient((_, _, _) => throw RecordingApiClient.Http(404)));

        // Act and Assert
        Assert.Null(await repository.GetSummaryAsync(CustomerId));
    }

    [Fact]
    public async Task GetSummaryAsync_OtherHttpFailures_Propagate()
    {
        // Arrange
        var repository = new LoyaltyRepository(new RecordingApiClient((_, _, _) => throw RecordingApiClient.Http(500)));

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetSummaryAsync(CustomerId));

        // Assert
        Assert.Equal(500, error.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task GetSummaryAsync_RejectsMalformedBody(string json)
    {
        // Arrange
        var repository = new LoyaltyRepository(new RecordingApiClient((_, _, _) => json));

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetSummaryAsync(CustomerId));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetSummaryAsync_RejectsAnotherCustomerOrNegativePoints()
    {
        // Arrange
        var other = new LoyaltyRepository(new RecordingApiClient((_, _, _) =>
            Summary(10, "Standard", 0, "\"Silver\"", "490", Guid.NewGuid())));
        var negative = new LoyaltyRepository(new RecordingApiClient((_, _, _) =>
            Summary(-5, "Standard", 0, "\"Silver\"", "505")));

        // Act
        var otherError = await Assert.ThrowsAsync<ApiException>(() => other.GetSummaryAsync(CustomerId));
        var negativeError = await Assert.ThrowsAsync<ApiException>(() => negative.GetSummaryAsync(CustomerId));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, otherError.Kind);
        Assert.Equal(ApiErrorKind.InvalidResponse, negativeError.Kind);
    }

    [Fact]
    public async Task GetSummaryAsync_RejectsEmptyId()
    {
        // Arrange
        var repository = new LoyaltyRepository(new RecordingApiClient((_, _, _) => "{}"));

        // Act and Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetSummaryAsync(Guid.Empty));
    }

    private static string Summary(int points, string tier, int discount, string next, string missing, Guid? id = null) =>
        "{\"customerId\":\"" + (id ?? CustomerId) + "\",\"points\":" + points + ",\"tier\":\"" + tier +
        "\",\"tierDiscountPercent\":" + discount + ",\"nextTier\":" + next + ",\"pointsToNextTier\":" + missing + "}";
}
