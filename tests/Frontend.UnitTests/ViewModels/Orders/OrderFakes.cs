using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Orders;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Orders;

/// <summary>Hand-written doubles shared by the Orders ViewModel tests (the project uses no mocking library).</summary>
internal sealed class FakeOrderRepository : IOrderRepository
{
    public Func<OrderQuery, CancellationToken, Task<OrderPage>> OnSearch { get; set; } =
        (query, _) => Task.FromResult(Orders.Page(query));

    public Func<Guid, CancellationToken, Task<OrderDetail?>> OnGet { get; set; } =
        (id, _) => Task.FromResult<OrderDetail?>(Orders.Detail(id));

    public Func<CreateOrderRequest, CancellationToken, Task<OrderDetail>> OnCreate { get; set; } =
        (_, _) => Task.FromResult(Orders.Detail(Guid.NewGuid()));

    public Func<Guid, OrderStatus, CancellationToken, Task<OrderDetail>> OnUpdate { get; set; } =
        (id, status, _) => Task.FromResult(Orders.Detail(id, status));

    public List<OrderQuery> Queries { get; } = [];
    public List<(Guid Id, OrderStatus Target)> Updates { get; } = [];
    public List<CreateOrderRequest> Created { get; } = [];
    public List<Guid> Fetched { get; } = [];
    public int SearchCount => Queries.Count;

    public Task<OrderPage> SearchAsync(OrderQuery query, CancellationToken cancellationToken = default)
    {
        Queries.Add(query);
        return OnSearch(query, cancellationToken);
    }

    public Task<OrderDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Fetched.Add(id);
        return OnGet(id, cancellationToken);
    }

    public Task<OrderDetail> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        Created.Add(request);
        return OnCreate(request, cancellationToken);
    }

    public Task<OrderDetail> UpdateStatusAsync(Guid id, OrderStatus target, CancellationToken cancellationToken = default)
    {
        Updates.Add((id, target));
        return OnUpdate(id, target, cancellationToken);
    }
}

internal sealed class FakeDialogService : IDialogService
{
    public DialogResult Result { get; set; } = DialogResult.Confirmed;
    public Exception? Throw { get; set; }
    public List<DialogRequest> Requests { get; } = [];

    public Task<DialogResult> ConfirmAsync(DialogRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (Throw is not null) return Task.FromException<DialogResult>(Throw);
        return Task.FromResult(Result);
    }

    public Task<DialogResult> InformAsync(DialogRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (Throw is not null) return Task.FromException<DialogResult>(Throw);
        return Task.FromResult(DialogResult.Confirmed);
    }
}

internal sealed class FakeDetailPresenter : IOrderDetailPresenter
{
    public List<OrderDetail> Shown { get; } = [];

    public Task ShowAsync(OrderDetail order, CancellationToken cancellationToken = default)
    {
        Shown.Add(order);
        return Task.CompletedTask;
    }
}

internal sealed class FakeNavigation : INavigationService
{
    public List<AppRoute> Routes { get; } = [];
    public AppRoute CurrentRoute { get; private set; } = AppRoute.Root(ShellDestination.Orders);
    public ShellDestination CurrentDestination => CurrentRoute.Destination;
    public event EventHandler<ShellDestination>? DestinationChanged;
    public event EventHandler<AppRoute>? RouteChanged;

    public NavigationOutcome Navigate(ShellDestination destination) => Navigate(AppRoute.Root(destination));

    public NavigationOutcome Navigate(AppRoute route)
    {
        var previous = CurrentRoute.Destination;
        Routes.Add(route);
        CurrentRoute = route;
        RouteChanged?.Invoke(this, route);
        if (previous != route.Destination) DestinationChanged?.Invoke(this, route.Destination);
        return NavigationOutcome.Navigated;
    }
}

internal static class Orders
{
    public static readonly Guid FirstId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static OrderSummary Summary(
        Guid? id = null,
        string code = "ORD-202610-0001",
        OrderStatus status = OrderStatus.Pending,
        decimal total = 30_000_000m,
        string? customer = null) => new(
        id ?? Guid.NewGuid(), code, status, PaymentMethod.Cash, null, customer, total, 1,
        new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc), null, null);

    public static OrderPage Page(OrderQuery query, int count = 1, int total = -1, int? page = null, int totalPages = 1)
    {
        var items = Enumerable.Range(1, count)
            .Select(i => Summary(i == 1 ? FirstId : Guid.NewGuid(), $"ORD-202610-{i:0000}"))
            .ToArray();
        return new OrderPage(items, page ?? query.Page, query.PageSize, total < 0 ? count : total, count == 0 ? 0 : totalPages);
    }

    public static OrderDetail Detail(Guid id, OrderStatus status = OrderStatus.Pending, string code = "ORD-202610-0001") => new(
        id, code, status, PaymentMethod.Cash, null, null, null, 30_000_000m, 0m, 0m, 30_000_000m, null,
        new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc), null, null,
        [new OrderLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "358912345678902", null,
            "iPhone 15 Pro Max", "256GB", "IP15PM-256-NT", 30_000_000m, 1, 0m, 30_000_000m)]);

    public static ApiException Http(int status, string? detail = null) =>
        new(ApiErrorKind.Http, $"HTTP {status}", status, title: "Error", detail: detail);
}

internal sealed class FakeProductRepository : IProductRepository
{
    public Dictionary<Guid, ProductDetail> Details { get; } = [];
    public List<ProductQuery> Queries { get; } = [];
    public Func<ProductQuery, Task<ProductPage>>? OnSearch { get; set; }

    public Task<ProductPage> SearchAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        Queries.Add(query);
        if (OnSearch is not null) return OnSearch(query);
        var items = Details.Values.Select(Catalog.Summary).ToArray();
        return Task.FromResult(new ProductPage(items, 1, query.PageSize, items.Length, items.Length == 0 ? 0 : 1));
    }

    public Task<ProductDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Details.TryGetValue(id, out var detail) ? detail : null);

    public ProductDetail Add(ProductDetail detail)
    {
        Details[detail.Id] = detail;
        return detail;
    }
}

internal sealed class FakeDeviceRepository : IOrderDeviceRepository
{
    public Dictionary<(Guid Product, Guid? Variant), AvailableDevicePage> Pages { get; } = [];
    public List<(Guid Product, Guid? Variant)> Calls { get; } = [];
    public Exception? Throw { get; set; }

    public Task<AvailableDevicePage> GetInStockAsync(Guid productId, Guid? variantId, CancellationToken cancellationToken = default)
    {
        Calls.Add((productId, variantId));
        if (Throw is not null) return Task.FromException<AvailableDevicePage>(Throw);
        return Task.FromResult(Pages.TryGetValue((productId, variantId), out var page) ? page : new AvailableDevicePage([], 0));
    }
}

internal static class Catalog
{
    public static ProductDetail Product(
        string name = "iPhone 15 Pro Max",
        bool serialTracked = true,
        decimal basePrice = 30_000_000m,
        IEnumerable<ProductVariant>? variants = null,
        bool active = true,
        Guid? id = null)
    {
        var productId = id ?? Guid.NewGuid();
        return new ProductDetail(
            productId, Guid.NewGuid(), "Điện thoại", name, "SKU-" + name.Replace(' ', '-'), null, "Brand", basePrice, 1m, null, null,
            new Dictionary<string, JsonElement>(), serialTracked, active, DateTime.UtcNow, null,
            (variants ?? []).Select(v => v with { ProductId = productId }).ToArray(), []);
    }

    public static ProductVariant Variant(string name, decimal price, bool active = true, Guid? id = null) => new(
        id ?? Guid.NewGuid(), Guid.Empty, name, "VAR-" + name, null, price,
        new Dictionary<string, JsonElement>(), new Dictionary<string, JsonElement>(), active, DateTime.UtcNow, null);

    public static ProductSummary Summary(ProductDetail p) => new(
        p.Id, p.Name, p.Sku, p.Brand, p.CategoryId, p.CategoryName,
        p.Variants.Count > 0 ? p.Variants.Min(v => v.Price) : p.BasePrice,
        p.Variants.Count > 0 ? p.Variants.Max(v => v.Price) : p.BasePrice,
        p.Variants.Count, p.IsSerialTracked, p.IsActive, null);

    public static AvailableDevice Device(string imei) => new(Guid.NewGuid(), imei, null);
}
