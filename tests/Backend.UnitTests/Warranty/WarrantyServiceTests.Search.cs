using TechStore.Api.Warranty.Entities;

namespace TechStore.UnitTests.Warranty;

public sealed partial class WarrantyServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAndSearch_ReplacementCost_FollowsIncludeCost(bool includeCost)
    {
        var fixture = new Fixture();
        var claim = fixture.AddClaim(WarrantyCaseStatus.Replaced);
        claim.ReplacementUnitCost = 1234.50m;
        claim.ContactPhone = "0901234567";
        fixture.AddClaim(WarrantyCaseStatus.Repairing);

        var detail = (await fixture.Service.GetAsync(claim.Id, includeCost, default)).Data!;
        Assert.Equal(includeCost ? (decimal?)1234.50m : null, detail.ReplacementUnitCost);
        var page = (await fixture.Service.SearchAsync("Replaced", "0901", 1, 20, includeCost, default)).Data!;
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(detail.ReplacementUnitCost, Assert.Single(page.Items).ReplacementUnitCost);
    }

    [Fact]
    public async Task Search_InvalidPageAndStatus_ReturnsFieldErrors()
    {
        var result = await new Fixture().Service.SearchAsync("0", null, 0, 101, false, default);
        Assert.Null(result.Data);
        Assert.Equal(new[] { "page", "pageSize", "status" }, result.Errors!.Keys);
    }
}
