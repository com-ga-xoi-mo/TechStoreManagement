using System.Net;
using System.Text.Json;
using TechStore.Client.Models.Customers;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Services.Api;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Repositories.Customers;

public sealed class CustomerRepositoryTests
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SearchAsync_MapsAggregatesAndEncodesFilters()
    {
        const string json = """
            {"items":[{"id":"11111111-1111-1111-1111-111111111111","fullName":"Vy Nguyen","phone":"0901","loyaltyPoints":120,"tier":"Gold","isActive":true,"createdAt":"2026-01-01T00:00:00Z","orderCount":4,"totalSpent":152000.25,"lastPurchaseAt":"2026-01-02T00:00:00Z"}],"page":2,"pageSize":10,"totalCount":11,"totalPages":2}
            """;
        var api = new StubApiClient { GetResponse = json };
        var repository = new CustomerRepository(api);

        var page = await repository.SearchAsync(new CustomerQuery { Query = "Vy Nguyen +84", Tier = "Gold", IsActive = true, Page = 2, PageSize = 10 });

        var customer = Assert.Single(page.Items);
        Assert.Equal(CustomerId, customer.Id);
        Assert.Equal(4, customer.OrderCount);
        Assert.Equal(152000.25m, customer.TotalSpent);
        Assert.Equal(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), customer.LastPurchaseAt);
        Assert.Equal(2, page.Page);
        Assert.Contains("q=Vy%20Nguyen%20%2B84", api.LastPath);
        Assert.Contains("tier=Gold", api.LastPath);
        Assert.Contains("isActive=true", api.LastPath);
        Assert.Contains("page=2", api.LastPath);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"items\":[],\"page\":1,\"pageSize\":20,\"totalPages\":0}")]
    [InlineData("{\"items\":[],\"page\":0,\"pageSize\":20,\"totalCount\":0,\"totalPages\":0}")]
    public async Task SearchAsync_RejectsMalformedPage(string json)
    {
        var repository = new CustomerRepository(new StubApiClient { GetResponse = json });

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new CustomerQuery()));

        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task SearchAsync_RejectsMalformedCustomerItem()
    {
        const string json = """
            {"items":[{"id":"00000000-0000-0000-0000-000000000000","fullName":" ","phone":"0901","loyaltyPoints":0,"tier":"Gold","isActive":true,"createdAt":"2026-01-01T00:00:00Z","orderCount":0,"totalSpent":0,"lastPurchaseAt":null}],"page":1,"pageSize":20,"totalCount":1,"totalPages":1}
            """;
        var repository = new CustomerRepository(new StubApiClient { GetResponse = json });

        var error = await Assert.ThrowsAsync<ApiException>(() => repository.SearchAsync(new CustomerQuery()));

        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetByIdAsync_MapsProfileAndReturnsNullOnNotFound()
    {
        const string json = """
            {"id":"11111111-1111-1111-1111-111111111111","fullName":"Vy Nguyen","phone":"0901","email":null,"dateOfBirth":null,"address":"HCMC","loyaltyPoints":120,"tier":"Gold","isActive":true,"createdAt":"2026-01-01T00:00:00Z","updatedAt":null,"orderCount":4,"completedOrderCount":3,"cancelledOrderCount":1,"totalSpent":152000.25,"lastPurchaseAt":"2026-01-02T00:00:00Z"}
            """;
        var api = new StubApiClient { GetResponse = json };
        var repository = new CustomerRepository(api);

        var profile = await repository.GetByIdAsync(CustomerId);

        Assert.NotNull(profile);
        Assert.Equal("HCMC", profile.Address);
        Assert.Equal(4, profile.OrderCount);
        Assert.Equal(3, profile.CompletedOrderCount);
        Assert.Equal(1, profile.CancelledOrderCount);
        Assert.Equal($"api/customers/{CustomerId:D}", api.LastPath);

        var missingRepository = new CustomerRepository(new StubApiClient { GetException = ApiException.Http((int)HttpStatusCode.NotFound, null) });
        Assert.Null(await missingRepository.GetByIdAsync(CustomerId));
    }

    [Fact]
    public async Task HistoryMethods_MapPointOrderAndPurchasedItemPages()
    {
        var api = new StubApiClient
        {
            GetResponse = """
                {"items":[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","pointsChange":10,"balanceAfter":120,"reason":"Order completed","orderId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","createdAt":"2026-01-02T00:00:00Z"}],"page":1,"pageSize":20,"totalCount":1,"totalPages":1}
                """
        };
        var repository = new CustomerRepository(api);

        var points = await repository.GetLoyaltyHistoryAsync(CustomerId, 1, 20);
        api.GetResponse = """
            {"items":[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","orderCode":"ORD-1","status":"Completed","paymentMethod":"Cash","customerId":"11111111-1111-1111-1111-111111111111","customerName":"Vy","totalAmount":100000,"itemCount":1,"primaryProductName":"Phone","primaryVariantName":"256 GB","primaryCategoryName":"Smartphones","createdAt":"2026-01-01T00:00:00Z","paidAt":null,"completedAt":"2026-01-02T00:00:00Z"}],"page":1,"pageSize":20,"totalCount":1,"totalPages":1}
            """;
        var orders = await repository.GetOrderHistoryAsync(CustomerId, 1, 20);
        api.GetResponse = """
            {"items":[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","orderId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","orderCode":"ORD-1","completedAt":"2026-01-02T00:00:00Z","productId":"cccccccc-cccc-cccc-cccc-cccccccccccc","variantId":null,"productName":"Phone","categoryName":"Smartphones","variantName":null,"sku":"PH-1","serialImeiId":null,"serialNumber":null,"imei":"123456789012345","warrantyStartAt":"2026-01-02T00:00:00Z","warrantyEndAt":"2027-01-02T00:00:00Z","deviceStatus":"Sold","quantity":1,"totalPrice":100000}],"page":1,"pageSize":20,"totalCount":1,"totalPages":1}
            """;
        var items = await repository.GetPurchasedItemsAsync(CustomerId, 1, 20);

        Assert.Equal(10, Assert.Single(points.Items).PointsChange);
        Assert.Equal("Completed", Assert.Single(orders.Items).Status);
        Assert.Equal("Phone", Assert.Single(orders.Items).PrimaryProductName);
        Assert.Equal("Smartphones", Assert.Single(orders.Items).PrimaryCategoryName);
        Assert.Equal("123456789012345", Assert.Single(items.Items).Imei);
        Assert.Equal("Sold", Assert.Single(items.Items).DeviceStatus);
        Assert.Equal("Smartphones", Assert.Single(items.Items).CategoryName);
        Assert.Equal(new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc), Assert.Single(items.Items).WarrantyEndAt);
        Assert.Equal($"api/customers/{CustomerId:D}/purchased-items?page=1&pageSize=20", api.LastPath);
    }

    [Fact]
    public async Task CreateUpdateAndActivityChanges_UseSupportedMethodsAndPreserveApiValidation()
    {
        const string profile = """
            {"id":"11111111-1111-1111-1111-111111111111","fullName":"Vy Nguyen","phone":"0901","email":null,"dateOfBirth":null,"address":null,"loyaltyPoints":0,"tier":"Standard","isActive":true,"createdAt":"2026-01-01T00:00:00Z","updatedAt":null,"orderCount":0,"totalSpent":0,"lastPurchaseAt":null}
            """;
        var api = new StubApiClient { WriteResponse = profile };
        var repository = new CustomerRepository(api);
        var request = new CreateCustomerRequest { FullName = "Vy Nguyen", Phone = "0901" };

        await repository.CreateAsync(request);
        Assert.Equal("api/customers", api.LastPath);
        Assert.Same(request, api.LastRequest);
        await repository.UpdateAsync(CustomerId, new UpdateCustomerRequest { FullName = "Vy Nguyen", Phone = "0901" });
        Assert.Equal($"api/customers/{CustomerId:D}", api.LastPath);
        Assert.IsType<UpdateCustomerRequest>(api.LastRequest);
        await repository.DeactivateAsync(CustomerId);
        Assert.Equal($"api/customers/{CustomerId:D}/deactivate", api.LastPath);
        Assert.Equal(1, api.PatchCallCount);
        await repository.ActivateAsync(CustomerId);
        Assert.Equal($"api/customers/{CustomerId:D}/activate", api.LastPath);
        Assert.Equal(2, api.PatchCallCount);

        api.WriteException = ApiException.Http((int)HttpStatusCode.Conflict, null);
        var conflict = await Assert.ThrowsAsync<ApiException>(() => repository.CreateAsync(request));
        Assert.Equal((int)HttpStatusCode.Conflict, conflict.StatusCode);

        api.WriteException = ApiException.Http((int)HttpStatusCode.BadRequest, null);
        var validation = await Assert.ThrowsAsync<ApiException>(() => repository.UpdateAsync(CustomerId, new UpdateCustomerRequest()));
        Assert.Equal((int)HttpStatusCode.BadRequest, validation.StatusCode);

        api.WriteException = ApiException.Http((int)HttpStatusCode.ServiceUnavailable, null);
        var activityFailure = await Assert.ThrowsAsync<ApiException>(() => repository.ActivateAsync(CustomerId));
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, activityFailure.StatusCode);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.DeactivateAsync(CustomerId, new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task SearchAsync_PropagatesCancellationAndHttpFailures()
    {
        var canceledToken = new CancellationToken(true);
        var cancelRepository = new CustomerRepository(new StubApiClient());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelRepository.SearchAsync(new CustomerQuery(), canceledToken));

        var failure = ApiException.Http((int)HttpStatusCode.ServiceUnavailable, null);
        var failingRepository = new CustomerRepository(new StubApiClient { GetException = failure });
        var observed = await Assert.ThrowsAsync<ApiException>(() => failingRepository.SearchAsync(new CustomerQuery()));
        Assert.Same(failure, observed);
    }

    private sealed class StubApiClient : IApiClient
    {
        public object? GetResponse { get; set; }
        public object? WriteResponse { get; set; }
        public Exception? GetException { get; set; }
        public Exception? WriteException { get; set; }
        public string LastPath { get; private set; } = string.Empty;
        public object? LastRequest { get; private set; }
        public int PatchCallCount { get; private set; }

        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastPath = relativePath;
            if (GetException is not null) throw GetException;
            return Task.FromResult(ConvertResponse<TResponse>(GetResponse));
        }

        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => WriteAsync<TRequest, TResponse>(relativePath, request);
        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => WriteAsync<TRequest, TResponse>(relativePath, request);
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => WriteAsync<TRequest, TResponse>(relativePath, request);
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PatchAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastPath = relativePath;
            PatchCallCount++;
            if (WriteException is not null) throw WriteException;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastPath = relativePath;
            return Task.CompletedTask;
        }

        private Task<TResponse> WriteAsync<TRequest, TResponse>(string relativePath, TRequest request)
        {
            LastPath = relativePath;
            LastRequest = request;
            if (WriteException is not null) throw WriteException;
            return Task.FromResult(ConvertResponse<TResponse>(WriteResponse));
        }

        private static TResponse ConvertResponse<TResponse>(object? response)
        {
            if (response is string json)
            {
                using var document = JsonDocument.Parse(json);
                return (TResponse)(object)document.RootElement.Clone();
            }
            return (TResponse)response!;
        }
    }
}
