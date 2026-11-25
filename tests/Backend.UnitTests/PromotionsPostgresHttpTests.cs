using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

// M4 Vy over real HTTP: JWT pipeline, controllers, services, repositories and PostgreSQL.
public sealed class PromotionsPostgresHttpTests
{
    private const decimal Price = 10_000_000m;
    private const string Password = "HttpEmployee@123";

    [PostgresFact]
    public async Task CreateOrder_VoucherVipAndManual_PriceExactlyOneSourceAndNeverConsumeAVoucherUse()
    {
        // Arrange
        await using var world = await World.CreateAsync(devices: 8);
        var customerId = await world.AddCustomerAsync(CustomerTier.Gold, points: 1_600);
        await world.CreateVoucherAsync("SALE10", "Percentage", 10m);

        // Act + Assert: voucher code typed in lower case, priced for a Sales cashier.
        var voucher = await world.CreateOrderAsync(world.Sales, o => o.VoucherCode = " sale10 ");
        Assert.Equal(HttpStatusCode.Created, voucher.Status);
        Assert.Equal((Price, 1_000_000m, 900_000m, 9_900_000m, "Voucher"),
            (voucher.Order!.Subtotal, voucher.Order.DiscountAmount, voucher.Order.TaxAmount, voucher.Order.TotalAmount, voucher.Order.DiscountSource));
        Assert.Equal(1_000_000m, Assert.Single(voucher.Order.Items).OrderDiscountAmount);

        // VIP: Gold customer gets 5% of the goods, then VAT on the rest.
        var vip = await world.CreateOrderAsync(world.Sales, o => { o.CustomerId = customerId; o.ApplyVipDiscount = true; });
        Assert.Equal(HttpStatusCode.Created, vip.Status);
        Assert.Equal((500_000m, 950_000m, 10_450_000m, "Vip"),
            (vip.Order!.DiscountAmount, vip.Order.TaxAmount, vip.Order.TotalAmount, vip.Order.DiscountSource));

        // Manual discount keeps working and is the only one that counts toward the Sales limit.
        var manual = await world.CreateOrderAsync(world.Sales, o => o.OrderDiscountAmount = 1_000_000m);
        Assert.Equal("Manual", manual.Order!.DiscountSource);
        var manualTooMuch = await world.CreateOrderAsync(world.Sales, o => o.OrderDiscountAmount = 1_100_000m);
        Assert.Equal(HttpStatusCode.BadRequest, manualTooMuch.Status);
        Assert.Contains("discount", manualTooMuch.Body);

        // Two sources in one order, a VIP discount without a customer, and an unknown code are all refused.
        var two = await world.CreateOrderAsync(world.Sales, o => { o.VoucherCode = "SALE10"; o.ApplyVipDiscount = true; o.CustomerId = customerId; });
        Assert.Equal(HttpStatusCode.BadRequest, two.Status);
        Assert.Contains("orderDiscount", two.Body);
        var vipGuest = await world.CreateOrderAsync(world.Sales, o => o.ApplyVipDiscount = true);
        Assert.Equal(HttpStatusCode.BadRequest, vipGuest.Status);
        Assert.Contains("applyVipDiscount", vipGuest.Body);
        var unknown = await world.CreateOrderAsync(world.Sales, o => o.VoucherCode = "NOPE");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.Status);
        Assert.Contains("voucherCode", unknown.Body);

        // A line discount above the Sales limit is refused even when the voucher itself is allowed.
        var lineTooMuch = await world.CreateOrderAsync(world.Sales, o => { o.VoucherCode = "SALE10"; o.Items![0].DiscountAmount = 1_100_000m; });
        Assert.Equal(HttpStatusCode.BadRequest, lineTooMuch.Status);
        Assert.Contains("discount", lineTooMuch.Body);

        // Pricing is a quote of the discount: it holds and consumes nothing, so the voucher still has every use.
        var listed = (await world.Manager.GetFromJsonAsync<PagedResultDto<VoucherDto>>("/api/vouchers?q=SALE10"))!;
        var stored = Assert.Single(listed.Items);
        Assert.Equal((0, 0, 100), (stored.UsedCount, stored.ReservedCount, stored.UsageLimit));
        await using var db = world.Database.CreateContext();
        Assert.Equal(0, await db.VoucherRedemptions.CountAsync());

        // The stored source is read back from the order detail.
        var detail = (await world.Sales.GetFromJsonAsync<OrderDto>($"/api/orders/{voucher.Order.Id}"))!;
        Assert.Equal("Voucher", detail.DiscountSource);
    }

    [PostgresFact]
    public async Task Vouchers_ManagementRulesRolesAndStatus_WorkThroughTheApi()
    {
        // Arrange
        await using var world = await World.CreateAsync(devices: 2);
        var request = new CreateVoucherRequest
        {
            Code = " tet2027 ", Description = "Tet sale", DiscountType = "Percentage", DiscountValue = 20m,
            MinOrderAmount = 5_000_000m, MaxDiscountAmount = 1_500_000m, UsageLimit = 2, ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        // Act + Assert: only Admin and Manager manage vouchers.
        using var asSales = await world.Sales.PostAsJsonAsync("/api/vouchers", request);
        Assert.Equal(HttpStatusCode.Forbidden, asSales.StatusCode);
        using var asWarehouse = await world.Warehouse.GetAsync("/api/vouchers");
        Assert.Equal(HttpStatusCode.Forbidden, asWarehouse.StatusCode);

        using var created = await world.Manager.PostAsJsonAsync("/api/vouchers", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var voucher = (await created.Content.ReadFromJsonAsync<VoucherDto>())!;
        Assert.Equal("TET2027", voucher.Code);

        using var duplicate = await world.Manager.PostAsJsonAsync("/api/vouchers", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var invalid = await world.Manager.PostAsJsonAsync("/api/vouchers", new CreateVoucherRequest { Code = "BAD CODE", DiscountType = "Percentage", DiscountValue = 120m });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var invalidBody = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("code", invalidBody);
        Assert.Contains("discountValue", invalidBody);
        Assert.Contains("expiresAt", invalidBody);

        // The cap applies to the percentage: 20% of 10,000,000 is capped at 1,500,000; minimum order is 5,000,000.
        var capped = await world.CreateOrderAsync(world.Admin, o => o.VoucherCode = "TET2027");
        Assert.Equal(1_500_000m, capped.Order!.DiscountAmount);
        var belowMinimum = await world.CreateOrderAsync(world.Admin, o => { o.VoucherCode = "TET2027"; o.Items![0].DiscountAmount = 6_000_000m; });
        Assert.Equal(HttpStatusCode.BadRequest, belowMinimum.Status);
        Assert.Contains("voucherCode", belowMinimum.Body);

        // Updating cannot cut the limit below the uses in play (none yet), then deactivation stops new use.
        using var updated = await world.Manager.PutAsJsonAsync($"/api/vouchers/{voucher.Id}", new UpdateVoucherRequest
        {
            Description = "Tet sale v2", DiscountValue = 15m, MinOrderAmount = 0m, MaxDiscountAmount = null, UsageLimit = 10, ExpiresAt = DateTime.UtcNow.AddDays(60)
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(15m, (await updated.Content.ReadFromJsonAsync<VoucherDto>())!.DiscountValue);

        using var deactivated = await world.Manager.PatchAsync($"/api/vouchers/{voucher.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        var afterOff = await world.CreateOrderAsync(world.Admin, o => o.VoucherCode = "TET2027");
        Assert.Equal(HttpStatusCode.BadRequest, afterOff.Status);
        using var reactivated = await world.Manager.PatchAsync($"/api/vouchers/{voucher.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        var afterOn = await world.CreateOrderAsync(world.Admin, o => o.VoucherCode = "TET2027");
        Assert.Equal(1_500_000m, afterOn.Order!.DiscountAmount);

        using var unknown = await world.Manager.GetAsync($"/api/vouchers/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [PostgresFact]
    public async Task LoyaltySummary_ReadsTierAndNextTier_ForCounterRolesOnly()
    {
        // Arrange
        await using var world = await World.CreateAsync(devices: 1);
        var customerId = await world.AddCustomerAsync(CustomerTier.Gold, points: 1_600);

        // Act
        var summary = (await world.Sales.GetFromJsonAsync<LoyaltySummaryDto>($"/api/customers/{customerId}/loyalty-summary"))!;
        using var unknown = await world.Sales.GetAsync($"/api/customers/{Guid.NewGuid()}/loyalty-summary");
        using var warehouse = await world.Warehouse.GetAsync($"/api/customers/{customerId}/loyalty-summary");

        // Assert
        Assert.Equal((1_600, "Gold", 5, "Platinum", 3_400), (summary.Points, summary.Tier, summary.TierDiscountPercent, summary.NextTier, summary.PointsToNextTier));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, warehouse.StatusCode);

        // An inactive customer is hidden from Sales exactly like the profile.
        using var deactivate = await world.Admin.PatchAsync($"/api/customers/{customerId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        using var hidden = await world.Sales.GetAsync($"/api/customers/{customerId}/loyalty-summary");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var visible = await world.Manager.GetAsync($"/api/customers/{customerId}/loyalty-summary");
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
    }

    public sealed record OrderResult(HttpStatusCode Status, OrderDto? Order, string Body);

    private sealed class World : IAsyncDisposable
    {
        private readonly IdentityPostgresApi _api;
        private readonly List<HttpClient> _clients = [];
        private readonly Guid _productId;
        private readonly List<Guid> _deviceIds = [];
        private int _nextDevice;

        public PostgresDatabase Database => _api.Database;
        public HttpClient Admin { get; }
        public HttpClient Manager { get; }
        public HttpClient Warehouse { get; }
        public HttpClient Sales { get; }

        private World(IdentityPostgresApi api, HttpClient admin, HttpClient manager, HttpClient warehouse, HttpClient sales, Guid productId)
        {
            _api = api;
            Admin = admin;
            Manager = manager;
            Warehouse = warehouse;
            Sales = sales;
            _productId = productId;
        }

        public static async Task<World> CreateAsync(int devices)
        {
            var api = await IdentityPostgresApi.CreateAsync();
            var clients = new List<HttpClient>();
            HttpClient NewClient()
            {
                var client = api.Factory.NewClient();
                clients.Add(client);
                return client;
            }

            try
            {
                var admin = NewClient();
                await IdentityPostgresApi.LoginAsync(admin);
                var sales = NewClient();
                var user = await IdentityPostgresApi.CreateUserAsync(admin, "SalesStaff");
                await IdentityPostgresApi.LoginAsync(sales, user.Username, Password);
                var manager = NewClient();
                await IdentityPostgresApi.LoginAsync(manager, "manager1", "Manager@123");
                var warehouse = NewClient();
                await IdentityPostgresApi.LoginAsync(warehouse, "warehouse1", "Warehouse@123");

                var key = Guid.NewGuid().ToString("N")[..12];
                using var categoryResponse = await admin.PostAsJsonAsync("/api/categories", new CreateCategoryRequest { Name = "Promo " + key, Slug = "promo-" + key, IsActive = true });
                categoryResponse.EnsureSuccessStatusCode();
                var category = (await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>())!;
                using var productResponse = await admin.PostAsJsonAsync("/api/products", new CreateProductRequest
                {
                    CategoryId = category.Id, Name = "Promo phone", Sku = "PRM-" + key, Brand = "Test",
                    BasePrice = Price, CostPrice = 5_000_000, IsSerialTracked = true
                });
                productResponse.EnsureSuccessStatusCode();
                var productId = (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!.Id;
                (await admin.PatchAsJsonAsync($"/api/products/{productId}/status", new UpdateProductStatusRequest { IsActive = true })).EnsureSuccessStatusCode();
                var imeis = Enumerable.Range(0, devices).Select(i => "35" + Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L) + i.ToString("D3")).ToList();
                using var import = await admin.PostAsJsonAsync("/api/inventory/serials/batch-import", new BatchImportSerialImeiRequest
                {
                    ProductId = productId,
                    DefaultUnitCost = 11_000_000m,
                    DefaultWarrantyMonths = 12,
                    Reason = "Promotion fixture",
                    Items = imeis.Select(i => new SerialImeiImportItemRequest { Imei = i }).ToList()
                });
                Assert.Equal(HttpStatusCode.Created, import.StatusCode);

                var world = new World(api, admin, manager, warehouse, sales, productId);
                world._clients.AddRange(clients);
                await using var db = api.Database.CreateContext();
                world._deviceIds.AddRange(await db.SerialImeis.AsNoTracking().Where(s => s.ProductId == productId).OrderBy(s => s.Imei).Select(s => s.Id).ToListAsync());
                return world;
            }
            catch
            {
                foreach (var client in clients) client.Dispose();
                await api.DisposeAsync();
                throw;
            }
        }

        // A customer made through the API, then given a tier and points directly: points are earned by completed sales.
        public async Task<Guid> AddCustomerAsync(CustomerTier tier, int points)
        {
            using var response = await Admin.PostAsJsonAsync("/api/customers", new CreateCustomerRequest { FullName = "Khách VIP", Phone = "09" + Random.Shared.Next(10_000_000, 99_999_999) });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var id = (await response.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
            await using var db = Database.CreateContext();
            var customer = await db.Customers.SingleAsync(c => c.Id == id);
            customer.Tier = tier;
            customer.LoyaltyPoints = points;
            await db.SaveChangesAsync();
            return id;
        }

        public async Task CreateVoucherAsync(string code, string type, decimal value)
        {
            using var response = await Manager.PostAsJsonAsync("/api/vouchers", new CreateVoucherRequest
            {
                Code = code, DiscountType = type, DiscountValue = value, UsageLimit = 100, ExpiresAt = DateTime.UtcNow.AddDays(30)
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // One serial-tracked device per order; orders stay Pending, so devices are never reserved by these calls.
        public async Task<OrderResult> CreateOrderAsync(HttpClient client, Action<CreateOrderRequest> configure)
        {
            var request = new CreateOrderRequest
            {
                Items = [new CreateOrderItemRequest { ProductId = _productId, SerialImeiId = _deviceIds[_nextDevice++ % _deviceIds.Count] }]
            };
            configure(request);
            using var response = await client.PostAsJsonAsync("/api/orders", request);
            var body = await response.Content.ReadAsStringAsync();
            var order = response.StatusCode == HttpStatusCode.Created
                ? System.Text.Json.JsonSerializer.Deserialize<OrderDto>(body, System.Text.Json.JsonSerializerOptions.Web)
                : null;
            return new OrderResult(response.StatusCode, order, body);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var client in _clients) client.Dispose();
            await _api.DisposeAsync();
        }
    }
}
