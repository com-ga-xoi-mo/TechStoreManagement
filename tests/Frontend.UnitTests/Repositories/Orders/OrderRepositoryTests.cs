using TechStore.Client.Models.Orders;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Api;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Repositories.Orders;

public sealed class OrderRepositoryTests
{
    private static readonly Guid OrderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CustomerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private const string SummaryWalkIn = """{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","orderCode":"ORD-202610-0001","status":"Pending","paymentMethod":"Cash","customerId":null,"customerName":null,"totalAmount":30000000,"itemCount":1,"createdAt":"2026-10-20T08:00:00Z","paidAt":null,"completedAt":null}""";
    private const string SummaryWithCustomer = """{"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","orderCode":"ORD-202610-0002","status":"Completed","paymentMethod":"VietQr","customerId":"cccccccc-cccc-cccc-cccc-cccccccccccc","customerName":"Nguyễn Văn A","totalAmount":1234567.89,"itemCount":2,"createdAt":"2026-10-20T09:00:00Z","paidAt":"2026-10-20T09:05:00Z","completedAt":"2026-10-20T09:10:00Z","unknownFutureField":true}""";

    private static string Page(string items, int total = 2, int totalPages = 1, int pageSize = 20, int page = 1) =>
        $$"""{"items":[{{items}}],"page":{{page}},"pageSize":{{pageSize}},"totalCount":{{total}},"totalPages":{{totalPages}},"added":"ignored"}""";

    private static string Detail(string status = "Pending", string payment = "Cash", string id = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", string unitPrice = "30000000", string total = "30000000", string quantity = "1") =>
        $$"""{"id":"{{id}}","orderCode":"ORD-202610-0001","status":"{{status}}","paymentMethod":"{{payment}}","customerId":null,"customerName":null,"customerPhone":null,"cashierUserId":"11111111-1111-1111-1111-111111111111","subtotal":{{total}},"discountAmount":0,"taxAmount":0,"totalAmount":{{total}},"notes":null,"createdAt":"2026-10-20T08:00:00Z","paidAt":null,"completedAt":null,"items":[{"id":"dddddddd-dddd-dddd-dddd-dddddddddddd","productId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","variantId":"ffffffff-ffff-ffff-ffff-ffffffffffff","serialImeiId":"99999999-9999-9999-9999-999999999999","imei":"358912345678902","serialNumber":null,"productName":"iPhone 15 Pro Max","variantName":"256GB","sku":"IP15PM-256-NT","unitPrice":{{unitPrice}},"quantity":{{quantity}},"discountAmount":0,"totalPrice":{{total}}}]}""";

    private static OrderRepository Repository(Func<string, string, object?, string> respond, out RecordingApiClient api)
    {
        api = new RecordingApiClient(respond);
        return new OrderRepository(api);
    }

    // ---------- SearchAsync ----------

    [Fact]
    public async Task SearchAsync_MapsPagedSummariesIncludingWalkInAndCustomerOrders()
    {
        // Arrange
        var repository = Repository((_, _, _) => Page($"{SummaryWalkIn},{SummaryWithCustomer}"), out var api);

        // Act
        var page = await repository.SearchAsync(new OrderQuery());

        // Assert
        Assert.Equal("api/orders?sort=createdAt%3Adesc&page=1&pageSize=20", api.Single.Path);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        var walkIn = page.Items[0];
        Assert.Equal("ORD-202610-0001", walkIn.OrderCode);
        Assert.Equal(OrderStatus.Pending, walkIn.Status);
        Assert.Equal(PaymentMethod.Cash, walkIn.PaymentMethod);
        Assert.Null(walkIn.CustomerId);
        Assert.Null(walkIn.CustomerName);
        Assert.Null(walkIn.PaidAt);
        var withCustomer = page.Items[1];
        Assert.Equal(OrderStatus.Completed, withCustomer.Status);
        Assert.Equal(PaymentMethod.VietQr, withCustomer.PaymentMethod);
        Assert.Equal(CustomerId, withCustomer.CustomerId);
        Assert.Equal("Nguyễn Văn A", withCustomer.CustomerName);
        Assert.Equal(1234567.89m, withCustomer.TotalAmount);
        Assert.Equal(2, withCustomer.ItemCount);
        Assert.NotNull(withCustomer.CompletedAt);
    }

    [Fact]
    public async Task SearchAsync_AcceptsAnEmptyPage()
    {
        // Arrange
        var repository = Repository((_, _, _) => Page(string.Empty, total: 0, totalPages: 0), out _);

        // Act
        var page = await repository.SearchAsync(new OrderQuery());

        // Assert
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public async Task SearchAsync_AcceptsEveryKnownStatus()
    {
        // Arrange
        foreach (var status in Enum.GetNames<OrderStatus>())
        {
            var json = SummaryWalkIn.Replace("\"Pending\"", $"\"{status}\"");
            var repository = Repository((_, _, _) => Page(json, total: 1), out _);

            // Act
            var page = await repository.SearchAsync(new OrderQuery());

            // Assert
            Assert.Equal(Enum.Parse<OrderStatus>(status), page.Items.Single().Status);
        }
    }

    [Theory]
    [InlineData("Shipped")]
    [InlineData("1")]
    [InlineData("")]
    public async Task SearchAsync_RejectsAStatusThatIsNotAKnownName(string status)
    {
        // Arrange
        var json = SummaryWalkIn.Replace("\"Pending\"", $"\"{status}\"");
        var repository = Repository((_, _, _) => Page(json, total: 1), out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new OrderQuery()));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task SearchAsync_RejectsAnUnknownPaymentMethod()
    {
        // Arrange
        var json = SummaryWalkIn.Replace("\"Cash\"", "\"Bitcoin\"");
        var repository = Repository((_, _, _) => Page(json, total: 1), out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new OrderQuery()));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("""{"items":null,"page":1,"pageSize":20,"totalCount":0,"totalPages":0}""")]
    [InlineData("""{"items":[],"page":1,"pageSize":20,"totalPages":0}""")]
    [InlineData("""{"items":[],"page":0,"pageSize":20,"totalCount":0,"totalPages":0}""")]
    [InlineData("""{"items":[],"page":1,"pageSize":200,"totalCount":0,"totalPages":0}""")]
    [InlineData("""{"items":[],"page":1,"pageSize":20,"totalCount":41,"totalPages":2}""")]
    [InlineData("""{"items":[],"page":1,"pageSize":20,"totalCount":0,"totalPages":1}""")]
    public async Task SearchAsync_RejectsAnEnvelopeOutsideTheContract(string json)
    {
        // Arrange
        var repository = Repository((_, _, _) => json, out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new OrderQuery()));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Theory]
    [InlineData("\"orderCode\":\"ORD-202610-0001\",", "")]
    [InlineData("\"totalAmount\":30000000", "\"totalAmount\":-1")]
    [InlineData("\"itemCount\":1", "\"itemCount\":-1")]
    [InlineData("\"id\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"", "\"id\":\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("\"createdAt\":\"2026-10-20T08:00:00Z\"", "\"createdAt\":\"yesterday\"")]
    [InlineData("\"customerId\":null", "\"customerId\":\"not-a-guid\"")]
    public async Task SearchAsync_RejectsAnInvalidSummary(string find, string replace)
    {
        // Arrange
        var json = SummaryWalkIn.Replace(find, replace);
        var repository = Repository((_, _, _) => Page(json, total: 1), out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new OrderQuery()));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task SearchAsync_LetsHttpFailuresReachTheCaller()
    {
        // Arrange
        var repository = Repository((_, _, _) => throw RecordingApiClient.Http(500), out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new OrderQuery()));

        // Assert
        Assert.Equal(500, error.StatusCode);
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsync_MapsTheOrderWithItsLines()
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(), out var api);

        // Act
        var order = await repository.GetByIdAsync(OrderId);

        // Assert
        Assert.Equal($"api/orders/{OrderId:D}", api.Single.Path);
        Assert.NotNull(order);
        Assert.Equal("ORD-202610-0001", order.OrderCode);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(30_000_000m, order.TotalAmount);
        var line = Assert.Single(order.Lines);
        Assert.Equal("iPhone 15 Pro Max", line.ProductName);
        Assert.Equal("358912345678902", line.Imei);
        Assert.Null(line.SerialNumber);
        Assert.Equal(1, line.Quantity);
    }

    [Fact]
    public async Task GetByIdAsync_KeepsDecimalPrecision()
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(unitPrice: "1234567.89", total: "1234567.89"), out _);

        // Act
        var order = await repository.GetByIdAsync(OrderId);

        // Assert
        Assert.Equal(1234567.89m, order!.Lines.Single().UnitPrice);
        Assert.Equal(1234567.89m, order.TotalAmount);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenTheOrderDoesNotExist()
    {
        // Arrange
        var repository = Repository((_, _, _) => throw RecordingApiClient.Http(404), out _);

        // Act
        var order = await repository.GetByIdAsync(OrderId);

        // Assert
        Assert.Null(order);
    }

    [Fact]
    public async Task GetByIdAsync_LetsOtherHttpFailuresReachTheCaller()
    {
        // Arrange
        var repository = Repository((_, _, _) => throw RecordingApiClient.Http(500), out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetByIdAsync(OrderId));

        // Assert
        Assert.Equal(500, error.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_RejectsAnOrderWithAnotherId()
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetByIdAsync(OrderId));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Theory]
    [InlineData("Shipped")]
    [InlineData("7")]
    public async Task GetByIdAsync_RejectsAnUnknownStatus(string status)
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(status: status), out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetByIdAsync(OrderId));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    public async Task GetByIdAsync_RejectsALineWithoutAPositiveQuantity(string quantity)
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(quantity: quantity), out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetByIdAsync(OrderId));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetByIdAsync_RejectsANegativeTotal()
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(total: "-1"), out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.GetByIdAsync(OrderId));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetByIdAsync_RejectsAnEmptyId()
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(), out var api);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetByIdAsync(Guid.Empty));
        Assert.Empty(api.Calls);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_PostsTheRequestAndReturnsTheCreatedOrder()
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(), out var api);
        var request = new CreateOrderRequest
        {
            PaymentMethod = "Cash",
            Items = [new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }]
        };

        // Act
        var order = await repository.CreateAsync(request);

        // Assert
        var call = api.Single;
        Assert.Equal("POST", call.Method);
        Assert.Equal("api/orders", call.Path);
        Assert.Same(request, call.Body);
        Assert.Equal(OrderId, order.Id);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task CreateAsync_LetsValidationAndConflictFailuresReachTheCaller()
    {
        // Arrange
        var errors = new Dictionary<string, IReadOnlyList<string>> { ["items[0].serialImeiId"] = ["Device is Sold."] };
        var repository = Repository((_, _, _) => throw RecordingApiClient.Http(400, fieldErrors: errors), out var api);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.CreateAsync(new CreateOrderRequest()));

        // Assert
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("Device is Sold.", Assert.Single(error.FieldErrors["items[0].serialImeiId"]));
        Assert.Single(api.Calls);
    }

    [Fact]
    public async Task CreateAsync_RejectsAMalformedCreatedOrder()
    {
        // Arrange
        var repository = Repository((_, _, _) => "{}", out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.CreateAsync(new CreateOrderRequest()));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    // ---------- UpdateStatusAsync ----------

    [Fact]
    public async Task UpdateStatusAsync_PatchesTheTargetStatusByName()
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(status: "Completed"), out var api);

        // Act
        var order = await repository.UpdateStatusAsync(OrderId, OrderStatus.Completed);

        // Assert
        var call = api.Single;
        Assert.Equal("PATCH", call.Method);
        Assert.Equal($"api/orders/{OrderId:D}/status", call.Path);
        Assert.Equal("Completed", Assert.IsType<UpdateOrderStatusRequest>(call.Body).TargetStatus);
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_LetsConflictAndNotFoundReachTheCaller()
    {
        // Arrange
        var repository = Repository((_, _, _) => throw RecordingApiClient.Http(409, detail: "Device is no longer available."), out var api);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.UpdateStatusAsync(OrderId, OrderStatus.Completed));

        // Assert
        Assert.Equal(409, error.StatusCode);
        Assert.Equal("Device is no longer available.", error.Detail);
        Assert.Single(api.Calls);
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsAResponseForAnotherOrder()
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), out _);

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => repository.UpdateStatusAsync(OrderId, OrderStatus.Completed));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsAnEmptyIdOrUndefinedStatusBeforeSending()
    {
        // Arrange
        var repository = Repository((_, _, _) => Detail(), out var api);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateStatusAsync(Guid.Empty, OrderStatus.Completed));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.UpdateStatusAsync(OrderId, (OrderStatus)99));
        Assert.Empty(api.Calls);
    }
}
