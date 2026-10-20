using System.Diagnostics;
using TechStore.Client.Models.Orders;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Orders;
using TechStore.Shared.Enums;
using Xunit.Abstractions;

namespace TechStore.Client.IntegrationTests;

/// <summary>
/// Opt-in live check of the Orders screens' logic: real ViewModels and repositories against a running API and
/// PostgreSQL. Run it on a freshly seeded database (it sells 3 of the 4 seeded iPhone devices):
/// <c>TECHSTORE_API_BASE_URL=http://localhost:5176/ dotnet test tests/Frontend.IntegrationTests</c>.
/// </summary>
public sealed class OrdersApiIntegrationTests(ITestOutputHelper output)
{
    private const string IphoneKeyword = "IP15PM-256-NT";
    private const string HeadphonesKeyword = "SONY-WH1000XM5";

    [OptInFact]
    public async Task LiveOrders_CounterFlowWorksThroughTheViewModels()
    {
        using var rig = LiveRig.Create();

        // ---- 1. draft with a serial-tracked device: estimate must equal what the server prices ----
        var draft = rig.NewCreate();
        var iphone = await rig.PickProduct(draft, IphoneKeyword);
        var variantPrice = draft.SelectedVariant?.Price ?? iphone.BasePrice;
        await Eventually(() => !draft.IsLoadingDevices && draft.Devices.Count >= 3,
            "Preflight failed: the iPhone needs at least 3 InStock devices. Reset the database (docker compose down -v) and restart the API.");
        var devices = draft.Devices.ToArray();
        draft.SelectedDevice = devices[0];
        draft.AddLineCommand.Execute(null);
        Assert.Equal(variantPrice, draft.EstimatedTotal);

        await draft.SaveDraftCommand.ExecuteAsync(null);
        var notice = Assert.Single(rig.Dialogs.Notices);
        Assert.Contains("chờ xử lý", notice.Message);
        var draftCode = notice.Code;
        var draftOrder = await rig.FindByCode(draftCode);
        Assert.Equal(OrderStatus.Pending, draftOrder.Status);
        Assert.Equal(draft.EstimatedTotal, draftOrder.TotalAmount);
        Assert.Equal(AppRoute.Root(ShellDestination.Orders), rig.Navigation.Routes.Last());
        output.WriteLine($"draft {draftCode}: Pending, server total {draftOrder.TotalAmount:N0} == estimate");

        // ---- 2. pay & complete: order Completed and the device leaves InStock ----
        var sale = rig.NewCreate();
        await rig.PickProduct(sale, IphoneKeyword);
        await Eventually(() => !sale.IsLoadingDevices && sale.Devices.Count >= 3, "device list did not load");
        var soldDevice = sale.Devices.Single(d => d.Id == devices[1].Id);
        sale.SelectedDevice = soldDevice;
        sale.AddLineCommand.Execute(null);
        sale.SelectedPayment = sale.PaymentOptions.Single(p => p.Method == PaymentMethod.VietQr);
        await sale.PayAndCompleteCommand.ExecuteAsync(null);
        Assert.Contains("đã hoàn tất", rig.Dialogs.Notices.Last().Message);
        var saleCode = rig.Dialogs.Notices.Last().Code;
        var saleOrder = await rig.FindByCode(saleCode);
        Assert.Equal(OrderStatus.Completed, saleOrder.Status);
        Assert.Equal(PaymentMethod.VietQr, saleOrder.PaymentMethod);
        var remaining = await rig.DeviceRepository.GetInStockAsync(iphone.Id, draft.SelectedVariant?.Id);
        Assert.DoesNotContain(remaining.Items, d => d.Id == soldDevice.Id);
        output.WriteLine($"sale {saleCode}: Completed via VietQr, device {soldDevice.Display} left InStock");

        // ---- 3. race: two drafts hold the same device; the second completion is refused by the server ----
        var raceA = await rig.DraftFor(IphoneKeyword, devices[2].Id);
        var raceB = await rig.DraftFor(IphoneKeyword, devices[2].Id);
        var list = rig.NewList();
        await list.ActivateAsync();
        await rig.Find(list, raceA);
        await list.ChangeStatusCommand.ExecuteAsync(list.Rows.Single().CompleteAction);
        Assert.Contains(raceA, list.ActionMessage);
        Assert.Equal(OrderStatus.Completed, (await rig.FindByCode(raceA)).Status);

        await rig.Find(list, raceB);
        await list.ChangeStatusCommand.ExecuteAsync(list.Rows.Single().CompleteAction);
        Assert.True(list.HasActionMessage);
        Assert.DoesNotContain("đã chuyển sang", list.ActionMessage);
        Assert.Equal(OrderStatus.Pending, (await rig.FindByCode(raceB)).Status);
        output.WriteLine($"race: {raceA} Completed; {raceB} refused -> \"{list.ActionMessage}\"");

        await rig.Find(list, raceB);
        await list.ChangeStatusCommand.ExecuteAsync(list.Rows.Single().CancelAction);
        Assert.Equal(OrderStatus.Cancelled, (await rig.FindByCode(raceB)).Status);

        // ---- 4. no-IMEI product with a line discount: estimate equals the server total ----
        var accessories = rig.NewCreate();
        var headphones = await rig.PickProduct(accessories, HeadphonesKeyword);
        accessories.QuantityText = "2";
        accessories.DiscountText = "100000";
        accessories.AddLineCommand.Execute(null);
        var expected = 2 * headphones.BasePrice - 100_000m;
        Assert.Equal(expected, accessories.EstimatedTotal);
        await accessories.PayAndCompleteCommand.ExecuteAsync(null);
        Assert.Contains("đã hoàn tất", rig.Dialogs.Notices.Last().Message);
        var accessoryCode = rig.Dialogs.Notices.Last().Code;
        var accessoryOrder = await rig.FindByCode(accessoryCode);
        Assert.Equal(expected, accessoryOrder.TotalAmount);
        Assert.Equal(OrderStatus.Completed, accessoryOrder.Status);
        output.WriteLine($"{accessoryCode}: 2 x {headphones.BasePrice:N0} - 100.000 = {accessoryOrder.TotalAmount:N0} (server == estimate)");

        // ---- 5. server validation error lands on its line and the draft survives ----
        var tooMany = rig.NewCreate();
        await rig.PickProduct(tooMany, HeadphonesKeyword);
        tooMany.QuantityText = "10000";
        tooMany.AddLineCommand.Execute(null);
        await tooMany.SaveDraftCommand.ExecuteAsync(null);
        Assert.True(tooMany.Lines[0].HasError, "the stock error should be attached to line 0");
        Assert.True(tooMany.CanSubmit);
        Assert.Empty(tooMany.PartialFailureMessage);
        output.WriteLine($"stock error on line 0: \"{tooMany.Lines[0].Error}\"");

        // ---- 6. detail shows the IMEI of the device that was sold ----
        await rig.Find(list, saleCode);
        await list.OpenDetailCommand.ExecuteAsync(list.Rows.Single());
        var shown = Assert.Single(rig.Presenter.Shown);
        Assert.Equal(soldDevice.Imei, Assert.Single(shown.Lines).Imei);
        Assert.Equal(OrderStatus.Completed, shown.Status);

        // ---- 7. filtering and an empty search ----
        list.SelectedStatusFilter = list.StatusFilters.Single(f => f.Value == OrderStatus.Completed);
        await Eventually(() => !list.IsLoading, "status filter did not finish");
        Assert.NotEmpty(list.Rows);
        Assert.All(list.Rows, row => Assert.Equal(OrderStatus.Completed, row.Order.Status));
        list.SearchText = $"no-such-order-{Guid.NewGuid():N}";
        await list.SearchCommand.ExecuteAsync(null);
        Assert.Empty(list.Rows);
        Assert.True(list.ShowEmptyState);

        // ---- 8. a missing order is a clean 404 ----
        Assert.Null(await rig.OrderRepository.GetByIdAsync(Guid.NewGuid()));
        output.WriteLine("checks: estimate==server total (serial and no-IMEI), VietQr, device leaves InStock, 409 race, 400 on line, detail IMEI, filters, 404");
    }

    private static async Task Eventually(Func<bool> condition, string failure, int seconds = 15)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.Elapsed.TotalSeconds > seconds) throw new InvalidOperationException(failure);
            await Task.Delay(50);
        }
    }

    private sealed class LiveRig : IDisposable
    {
        private readonly HttpClient _http;
        private readonly ApiClient _api;
        public IOrderRepository OrderRepository { get; }
        public IOrderDeviceRepository DeviceRepository { get; }
        private readonly IProductRepository _products;
        public RecordingNavigation Navigation { get; } = new();
        public RecordingDialogs Dialogs { get; } = new();
        public RecordingPresenter Presenter { get; } = new();

        private LiveRig(HttpClient http, ApiClient api)
        {
            _http = http;
            _api = api;
            OrderRepository = new OrderRepository(api);
            DeviceRepository = new OrderDeviceRepository(api);
            _products = new ProductRepository(api);
        }

        public static LiveRig Create()
        {
            var baseUrl = Environment.GetEnvironmentVariable("TECHSTORE_API_BASE_URL");
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("Set TECHSTORE_API_BASE_URL to opt in to the live Orders API integration test.");

            var options = new ApiClientOptions { BaseUrl = baseUrl, TimeoutSeconds = 20 };
            options.Validate();
            var handler = new BearerTokenHandler(new InMemorySessionStore())
            {
                InnerHandler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }
            };
            var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            return new LiveRig(http, new ApiClient(http, options));
        }

        public OrderCreateViewModel NewCreate() =>
            new(OrderRepository, DeviceRepository, _products, Navigation, Dialogs);

        public OrderListViewModel NewList() => new(OrderRepository, Navigation, Dialogs, Presenter);

        public async Task<TechStore.Client.Models.Catalog.ProductDetail> PickProduct(OrderCreateViewModel vm, string keyword)
        {
            vm.ProductSearchText = keyword;
            await vm.SearchProductsCommand.ExecuteAsync(null);
            var choice = vm.ProductResults.FirstOrDefault()
                ?? throw new InvalidOperationException($"Preflight failed: no active product matches '{keyword}'. Is the API seeded?");
            await vm.SelectProductCommand.ExecuteAsync(choice.Summary);
            return vm.SelectedProduct ?? throw new InvalidOperationException($"Product '{keyword}' could not be selected: {vm.LineError}");
        }

        /// <summary>Saves a one-device draft and returns its order code.</summary>
        public async Task<string> DraftFor(string keyword, Guid deviceId)
        {
            var vm = NewCreate();
            await PickProduct(vm, keyword);
            await Eventually(() => !vm.IsLoadingDevices && vm.Devices.Any(d => d.Id == deviceId), "device list did not load");
            vm.SelectedDevice = vm.Devices.Single(d => d.Id == deviceId);
            vm.AddLineCommand.Execute(null);
            await vm.SaveDraftCommand.ExecuteAsync(null);
            return Dialogs.Notices.Last().Code;
        }

        public async Task<OrderDetail> FindByCode(string code)
        {
            var page = await OrderRepository.SearchAsync(new OrderQuery { Query = code, PageSize = 5 });
            var summary = Assert.Single(page.Items, o => o.OrderCode == code);
            return (await OrderRepository.GetByIdAsync(summary.Id))!;
        }

        public async Task Find(OrderListViewModel list, string code)
        {
            list.SearchText = code;
            await list.SearchCommand.ExecuteAsync(null);
            Assert.Single(list.Rows);
        }

        public void Dispose() => _http.Dispose();
    }

    private sealed class RecordingNavigation : INavigationService
    {
        public List<AppRoute> Routes { get; } = [];
        public AppRoute CurrentRoute { get; private set; } = AppRoute.Root(ShellDestination.Orders);
        public ShellDestination CurrentDestination => CurrentRoute.Destination;
        public event EventHandler<ShellDestination>? DestinationChanged { add { } remove { } }
        public event EventHandler<AppRoute>? RouteChanged { add { } remove { } }
        public NavigationOutcome Navigate(ShellDestination destination) => Navigate(AppRoute.Root(destination));

        public NavigationOutcome Navigate(AppRoute route)
        {
            Routes.Add(route);
            CurrentRoute = route;
            return NavigationOutcome.Navigated;
        }
    }

    private sealed class RecordingDialogs : IDialogService
    {
        /// <summary>The "order created" notices, each with the order code found in its message.</summary>
        public List<(string Code, string Message)> Notices { get; } = [];

        public Task<DialogResult> ConfirmAsync(DialogRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(DialogResult.Confirmed);

        public Task<DialogResult> InformAsync(DialogRequest request, CancellationToken cancellationToken = default)
        {
            var code = System.Text.RegularExpressions.Regex.Match(request.Message, @"ORD-\d{6}-\d{4}").Value;
            Notices.Add((code, request.Message));
            return Task.FromResult(DialogResult.Confirmed);
        }
    }

    private sealed class RecordingPresenter : IOrderDetailPresenter
    {
        public List<OrderDetail> Shown { get; } = [];

        public Task ShowAsync(OrderDetail order, CancellationToken cancellationToken = default)
        {
            Shown.Add(order);
            return Task.CompletedTask;
        }
    }
}
