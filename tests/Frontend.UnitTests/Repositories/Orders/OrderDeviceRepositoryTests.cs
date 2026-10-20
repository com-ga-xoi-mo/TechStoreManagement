using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Api;

namespace TechStore.Client.UnitTests.Repositories.Orders;

public sealed class OrderDeviceRepositoryTests
{
    private static readonly Guid ProductId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid VariantId = Guid.Parse("66666666-7777-8888-9999-000000000000");

    private const string DeviceA = """{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","productId":"11111111-2222-3333-4444-555555555555","productName":"iPhone","imei":"358912345678902","serialNumber":null,"status":"InStock","unitCost":25000000,"purchaseOrderId":null,"warrantyMonths":12}""";
    private const string DeviceB = """{"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","imei":null,"serialNumber":"SN-0002","unitCost":null}""";

    private static string Page(string items, int total) =>
        $$"""{"items":[{{items}}],"page":1,"pageSize":100,"totalCount":{{total}},"totalPages":1}""";

    [Fact]
    public async Task GetInStockAsync_AsksForInStockDevicesOfTheVariant()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Page(DeviceA, 1));
        var repository = new OrderDeviceRepository(api);

        // Act
        await repository.GetInStockAsync(ProductId, VariantId);

        // Assert
        Assert.Equal(
            "api/inventory/serials?status=InStock&productId=11111111-2222-3333-4444-555555555555&variantId=66666666-7777-8888-9999-000000000000&page=1&pageSize=100",
            api.Single.Path);
    }

    [Fact]
    public async Task GetInStockAsync_OmitsTheVariantWhenThereIsNone()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Page(DeviceA, 1));
        var repository = new OrderDeviceRepository(api);

        // Act
        await repository.GetInStockAsync(ProductId, null);

        // Assert
        Assert.DoesNotContain("variantId", api.Single.Path);
    }

    [Fact]
    public async Task GetInStockAsync_KeepsOnlyIdImeiAndSerialAndNeverTheCost()
    {
        // Arrange
        var repository = new OrderDeviceRepository(new RecordingApiClient((_, _, _) => Page($"{DeviceA},{DeviceB}", 2)));

        // Act
        var page = await repository.GetInStockAsync(ProductId, VariantId);

        // Assert
        Assert.Equal(2, page.Items.Count);
        Assert.False(page.IsTruncated);
        Assert.Equal("358912345678902", page.Items[0].Imei);
        Assert.Null(page.Items[0].SerialNumber);
        Assert.Null(page.Items[1].Imei);
        Assert.Equal("SN-0002", page.Items[1].SerialNumber);
        var members = typeof(Client.Models.Orders.AvailableDevice).GetProperties().Select(p => p.Name);
        Assert.DoesNotContain(members, name => name.Contains("Cost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetInStockAsync_FlagsAListThatTheApiCappedAtOnePage()
    {
        // Arrange
        var repository = new OrderDeviceRepository(new RecordingApiClient((_, _, _) => Page(DeviceA, 150)));

        // Act
        var page = await repository.GetInStockAsync(ProductId, null);

        // Assert
        Assert.Single(page.Items);
        Assert.Equal(150, page.TotalCount);
        Assert.True(page.IsTruncated);
    }

    [Fact]
    public async Task GetInStockAsync_AcceptsAnEmptyList()
    {
        // Arrange
        var repository = new OrderDeviceRepository(new RecordingApiClient((_, _, _) => Page(string.Empty, 0)));

        // Act
        var page = await repository.GetInStockAsync(ProductId, null);

        // Assert
        Assert.Empty(page.Items);
        Assert.False(page.IsTruncated);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"items":[{"imei":"1","serialNumber":null}],"totalCount":1}""")]
    [InlineData("""{"items":[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","serialNumber":null}],"totalCount":1}""")]
    [InlineData("""{"items":[{"id":"00000000-0000-0000-0000-000000000000","imei":"1","serialNumber":null}],"totalCount":1}""")]
    [InlineData("""{"items":[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","imei":"1","serialNumber":null}],"totalCount":0}""")]
    public async Task GetInStockAsync_RejectsAMalformedPage(string json)
    {
        // Arrange
        var repository = new OrderDeviceRepository(new RecordingApiClient((_, _, _) => json));

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetInStockAsync(ProductId, null));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetInStockAsync_RejectsEmptyIdsBeforeSending()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Page(string.Empty, 0));
        var repository = new OrderDeviceRepository(api);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetInStockAsync(Guid.Empty, null));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetInStockAsync(ProductId, Guid.Empty));
        Assert.Empty(api.Calls);
    }
}
