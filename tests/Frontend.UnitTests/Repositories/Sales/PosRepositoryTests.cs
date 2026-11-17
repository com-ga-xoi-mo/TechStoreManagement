using TechStore.Client.Repositories.Orders;
using TechStore.Client.Repositories.Sales;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Repositories.Sales;

public sealed class PosRepositoryTests
{
    private const string ShiftId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    private const string OpenShiftJson = """
        {"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","cashierUserId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","cashierName":"Thu ngan","status":"Open","openingBalance":1000000,
         "cashSalesTotal":500000,"expectedClosingBalance":1500000,"closingBalance":null,"discrepancy":null,"closingNote":null,
         "closedByUserId":null,"openedAt":"2026-11-17T01:00:00Z","closedAt":null}
        """;

    private const string OrderJsonText = """
        {"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","orderCode":"ORD-202611-0001","status":"Completed","paymentMethod":"Cash",
         "customerId":null,"customerName":null,"customerPhone":null,"cashierUserId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
         "subtotal":10000000,"discountAmount":0,"taxAmount":0,"totalAmount":10000000,
         "posSessionId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","cashTendered":10500000,"changeGiven":500000,"notes":null,
         "createdAt":"2026-11-17T02:00:00Z","paidAt":"2026-11-17T02:00:00Z","completedAt":"2026-11-17T02:00:00Z",
         "items":[{"id":"dddddddd-dddd-dddd-dddd-dddddddddddd","productId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","variantId":null,
         "serialImeiId":"ffffffff-ffff-ffff-ffff-ffffffffffff","imei":"358912345678902","serialNumber":null,"productName":"iPhone",
         "variantName":null,"sku":"IP15","unitPrice":10000000,"quantity":1,"discountAmount":0,"totalPrice":10000000}]}
        """;

    private const string ScanPage = """
        {"items":[
          {"id":"11111111-1111-1111-1111-111111111111","productId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","variantId":null,"imei":"358912345678900","serialNumber":null,"unitCost":9000000},
          {"id":"22222222-2222-2222-2222-222222222222","productId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","variantId":"99999999-9999-9999-9999-999999999999","imei":"358912345678902","serialNumber":"SN-2","unitCost":9000000}
        ],"page":1,"pageSize":10,"totalCount":2,"totalPages":1}
        """;

    [Fact]
    public async Task GetCurrentShiftAsync_WithAnOpenShift_MapsItIncludingExpectedCash()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => OpenShiftJson);
        var repository = new PosRepository(api);

        // Act
        var shift = await repository.GetCurrentShiftAsync();

        // Assert
        Assert.Equal("api/pos/shifts/current", api.Single.Path);
        Assert.NotNull(shift);
        Assert.True(shift.IsOpen);
        Assert.Equal(1_000_000m, shift.OpeningBalance);
        Assert.Equal(1_500_000m, shift.ExpectedCash);
    }

    [Fact]
    public async Task GetCurrentShiftAsync_WhenTheServerAnswersNotFound_ReturnsNull()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => throw RecordingApiClient.Http(404));

        // Act
        var shift = await new PosRepository(api).GetCurrentShiftAsync();

        // Assert
        Assert.Null(shift);
    }

    [Fact]
    public async Task GetCurrentShiftAsync_WhenTheServerFails_LetsTheErrorThrough()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => throw RecordingApiClient.Http(500));

        // Act and Assert
        await Assert.ThrowsAsync<ApiException>(() => new PosRepository(api).GetCurrentShiftAsync());
    }

    [Fact]
    public async Task OpenShiftAsync_PostsTheOpeningBalance()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => OpenShiftJson);

        // Act
        await new PosRepository(api).OpenShiftAsync(1_000_000m);

        // Assert
        Assert.Equal("POST", api.Single.Method);
        Assert.Equal("api/pos/shifts", api.Single.Path);
        Assert.Equal(1_000_000m, Assert.IsType<OpenShiftRequest>(api.Single.Body).OpeningBalance);
    }

    [Fact]
    public async Task CloseShiftAsync_PostsTheCountedCashAndATrimmedNote()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => OpenShiftJson.Replace("\"Open\"", "\"Closed\""));

        // Act
        var shift = await new PosRepository(api).CloseShiftAsync(Guid.Parse(ShiftId), 1_400_000m, "  thieu tien  ");

        // Assert
        Assert.Equal($"api/pos/shifts/{ShiftId}/close", api.Single.Path);
        var body = Assert.IsType<CloseShiftRequest>(api.Single.Body);
        Assert.Equal(1_400_000m, body.ClosingBalance);
        Assert.Equal("thieu tien", body.Note);
        Assert.Equal(PosSessionStatus.Closed, shift.Status);
    }

    [Fact]
    public async Task CloseShiftAsync_WithBlankNote_SendsNoNote()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => OpenShiftJson);

        // Act
        await new PosRepository(api).CloseShiftAsync(Guid.Parse(ShiftId), 1m, "   ");

        // Assert
        Assert.Null(Assert.IsType<CloseShiftRequest>(api.Single.Body).Note);
    }

    [Fact]
    public async Task CloseShiftAsync_WithEmptyId_IsRejectedBeforeAnyRequest()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => OpenShiftJson);

        // Act and Assert
        await Assert.ThrowsAsync<ArgumentException>(() => new PosRepository(api).CloseShiftAsync(Guid.Empty, 1m, null));
        Assert.Empty(api.Calls);
    }

    [Fact]
    public async Task GetCurrentShiftAsync_WithAnUnknownStatus_IsAnInvalidResponse()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => OpenShiftJson.Replace("\"Open\"", "\"Paused\""));

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => new PosRepository(api).GetCurrentShiftAsync());

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task CheckoutAsync_PostsTheRequestAndMapsTheCashFieldsOfTheOrder()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => OrderJsonText);
        var request = new PosCheckoutRequest { CheckoutKey = Guid.NewGuid(), CashTendered = 10_500_000m };

        // Act
        var order = await new PosRepository(api).CheckoutAsync(request);

        // Assert
        Assert.Equal("api/pos/checkout", api.Single.Path);
        Assert.Same(request, api.Single.Body);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(Guid.Parse(ShiftId), order.PosSessionId);
        Assert.Equal(10_500_000m, order.CashTendered);
        Assert.Equal(500_000m, order.ChangeGiven);
    }

    [Fact]
    public async Task CheckoutAsync_WithAnOrderThatLacksTheNewFields_StillParses()
    {
        // Arrange
        var json = OrderJsonText
            .Replace("\"posSessionId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"cashTendered\":10500000,\"changeGiven\":500000,", string.Empty);
        var api = new RecordingApiClient((_, _, _) => json);

        // Act
        var order = await new PosRepository(api).CheckoutAsync(new PosCheckoutRequest());

        // Assert
        Assert.Null(order.CashTendered);
        Assert.Null(order.PosSessionId);
    }

    [Fact]
    public async Task CheckoutAsync_WithNegativeChange_IsAnInvalidResponse()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => OrderJsonText.Replace("\"changeGiven\":500000", "\"changeGiven\":-1"));

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => new PosRepository(api).CheckoutAsync(new PosCheckoutRequest()));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task FindInStockByCodeAsync_ReturnsTheExactMatchWithItsProductAndVariant()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => ScanPage);

        // Act
        var scanned = await new OrderDeviceRepository(api).FindInStockByCodeAsync(" 358912345678902 ");

        // Assert
        Assert.Equal("api/inventory/serials?status=InStock&search=358912345678902&page=1&pageSize=10", api.Single.Path);
        Assert.NotNull(scanned);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), scanned.Device.Id);
        Assert.Equal(Guid.Parse("99999999-9999-9999-9999-999999999999"), scanned.VariantId);
        Assert.Equal(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), scanned.ProductId);
    }

    [Fact]
    public async Task FindInStockByCodeAsync_MatchesSerialNumberIgnoringCase()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => ScanPage);

        // Act
        var scanned = await new OrderDeviceRepository(api).FindInStockByCodeAsync("sn-2");

        // Assert
        Assert.Equal("SN-2", scanned!.Device.SerialNumber);
    }

    [Fact]
    public async Task FindInStockByCodeAsync_WhenOnlyPartialMatchesExist_ReturnsNull()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => ScanPage);

        // Act
        var scanned = await new OrderDeviceRepository(api).FindInStockByCodeAsync("3589123456789");

        // Assert
        Assert.Null(scanned);
    }

    [Fact]
    public async Task FindInStockByCodeAsync_WithBlankCode_IsRejectedBeforeAnyRequest()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => ScanPage);

        // Act and Assert
        await Assert.ThrowsAsync<ArgumentException>(() => new OrderDeviceRepository(api).FindInStockByCodeAsync("  "));
        Assert.Empty(api.Calls);
    }
}
