using System.Text.RegularExpressions;
using TechStore.Client.Models.Vouchers;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Client.Repositories.Vouchers;
using TechStore.Client.Services.Api;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Repositories.Vouchers;

public sealed class VoucherRepositoryTests
{
    private static readonly Guid VoucherId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task SearchAsync_MapsRowsAndEncodesFilters()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Page(Voucher(), page: 2, pageSize: 10, total: 11, pages: 2));
        var repository = new VoucherRepository(api);

        // Act
        var page = await repository.SearchAsync(new VoucherQuery { Query = "sale 10%", IsActive = true, Page = 2, PageSize = 10 });

        // Assert
        var voucher = Assert.Single(page.Items);
        Assert.Equal(VoucherId, voucher.Id);
        Assert.Equal("SALE10", voucher.Code);
        Assert.Equal("Percentage", voucher.DiscountType);
        Assert.Equal(150000m, voucher.MaxDiscountAmount);
        Assert.Equal(1, voucher.ReservedCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(11, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        var call = api.Single;
        Assert.Equal("GET", call.Method);
        Assert.StartsWith("api/vouchers?", call.Path);
        Assert.Contains("q=sale%2010%25", call.Path);
        Assert.Contains("isActive=true", call.Path);
        Assert.Contains("page=2", call.Path);
        Assert.Contains("pageSize=10", call.Path);
    }

    [Fact]
    public async Task SearchAsync_OmitsEmptyFilters_AndClampsPaging()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Page(Voucher(), page: 1, pageSize: 100, total: 1, pages: 1));
        var repository = new VoucherRepository(api);

        // Act
        await repository.SearchAsync(new VoucherQuery { Query = "  ", IsActive = null, Page = 0, PageSize = 500 });

        // Assert
        var path = api.Single.Path;
        Assert.DoesNotContain("q=", path);
        Assert.DoesNotContain("isActive", path);
        Assert.Contains("page=1", path);
        Assert.Contains("pageSize=100", path);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"items\":[],\"page\":0,\"pageSize\":20,\"totalCount\":0,\"totalPages\":0}")]
    [InlineData("{\"items\":[],\"page\":1,\"pageSize\":20,\"totalCount\":5,\"totalPages\":9}")]
    [InlineData("{\"items\":[],\"page\":1,\"pageSize\":20,\"totalCount\":-1,\"totalPages\":0}")]
    public async Task SearchAsync_RejectsMalformedPage(string json)
    {
        // Arrange
        var repository = new VoucherRepository(new RecordingApiClient((_, _, _) => json));

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new VoucherQuery()));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Theory]
    [InlineData("id", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("code", "\" \"")]
    [InlineData("discountType", "\"Mystery\"")]
    [InlineData("usageLimit", "0")]
    [InlineData("usedCount", "-1")]
    [InlineData("discountValue", "0")]
    public async Task SearchAsync_RejectsInvalidVoucher(string field, string badValue)
    {
        // Arrange
        var broken = Regex.Replace(Voucher(), $"\"{field}\":[^,}}]+", $"\"{field}\":{badValue}");
        var repository = new VoucherRepository(new RecordingApiClient((_, _, _) =>
            Page(broken, page: 1, pageSize: 20, total: 1, pages: 1)));

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new VoucherQuery()));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task SearchAsync_PropagatesHttpFailure()
    {
        // Arrange
        var repository = new VoucherRepository(new RecordingApiClient((_, _, _) => throw RecordingApiClient.Http(403)));

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new VoucherQuery()));

        // Assert
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_PostsRequestAndMapsCreatedVoucher()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Voucher());
        var repository = new VoucherRepository(api);
        var request = new CreateVoucherRequest { Code = "sale10", DiscountType = "Percentage", DiscountValue = 10m, UsageLimit = 50 };

        // Act
        var created = await repository.CreateAsync(request);

        // Assert
        Assert.Equal("SALE10", created.Code);
        Assert.Equal(("POST", "api/vouchers"), (api.Single.Method, api.Single.Path));
        Assert.Same(request, api.Single.Body);
    }

    [Fact]
    public async Task UpdateAsync_PutsRequestToTheVoucher_AndRejectsAnotherId()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Voucher());
        var repository = new VoucherRepository(api);
        var request = new UpdateVoucherRequest { DiscountValue = 12m };

        // Act
        var updated = await repository.UpdateAsync(VoucherId, request);
        var mismatch = await Assert.ThrowsAsync<ApiException>(() => repository.UpdateAsync(Guid.NewGuid(), request));

        // Assert
        Assert.Equal(VoucherId, updated.Id);
        Assert.Equal(("PUT", $"api/vouchers/{VoucherId:D}"), (api.Calls[0].Method, api.Calls[0].Path));
        Assert.Equal(ApiErrorKind.InvalidResponse, mismatch.Kind);
    }

    [Theory]
    [InlineData(true, "activate")]
    [InlineData(false, "deactivate")]
    public async Task SetActiveAsync_PatchesTheMatchingEndpoint(bool isActive, string action)
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Voucher(isActive));
        var repository = new VoucherRepository(api);

        // Act
        var voucher = await repository.SetActiveAsync(VoucherId, isActive);

        // Assert
        Assert.Equal(isActive, voucher.IsActive);
        Assert.Equal(("PATCH", $"api/vouchers/{VoucherId:D}/{action}"), (api.Single.Method, api.Single.Path));
    }

    [Fact]
    public async Task Calls_RejectEmptyIds()
    {
        // Arrange
        var repository = new VoucherRepository(new RecordingApiClient((_, _, _) => Voucher()));

        // Act and Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateAsync(Guid.Empty, new UpdateVoucherRequest()));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SetActiveAsync(Guid.Empty, true));
    }

    private static string Voucher(bool isActive = true) =>
        "{\"id\":\"" + VoucherId + "\",\"code\":\"SALE10\",\"description\":\"Giảm giá thử\",\"discountType\":\"Percentage\"," +
        "\"discountValue\":10,\"minOrderAmount\":2000000,\"maxDiscountAmount\":150000,\"usageLimit\":50,\"usedCount\":3," +
        "\"reservedCount\":1,\"expiresAt\":\"2027-01-01T00:00:00Z\",\"isActive\":" + isActive.ToString().ToLowerInvariant() +
        ",\"createdAt\":\"2026-11-01T00:00:00Z\"}";

    private static string Page(string voucher, int page, int pageSize, int total, int pages) =>
        "{\"items\":[" + voucher + "],\"page\":" + page + ",\"pageSize\":" + pageSize +
        ",\"totalCount\":" + total + ",\"totalPages\":" + pages + "}";
}
