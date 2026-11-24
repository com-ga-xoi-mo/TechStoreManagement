using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using Windows.Storage.Streams;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.Styles;
using TechStore.Client.Views.Inventory;
using TechStore.Client.Views.Orders;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Testing;

internal static class ReturnsThemeProbe
{
    public static async Task<List<ProbeReportItem>> RunAsync(UserControl host, Func<Task> settle, CancellationToken token)
    {
        var reports = new List<ProbeReportItem>();
        var configuration = new ConfigurationBuilder().Build();
        var session = new InMemorySessionStore();
        var fixture = new ReturnApi();
        using var http = new HttpClient(fixture) { BaseAddress = new Uri(ApiClientOptions.DevelopmentBaseUrl) };
        var services = new ServiceCollection().AddFrontendFoundation(configuration).AddFrontendUi(configuration);
        services.AddSingleton<ISessionStore>(session);
        services.AddSingleton<IReturnRepository>(new ReturnRepository(new ApiClient(http, new ApiClientOptions())));
        await using var provider = services.BuildServiceProvider();

        foreach (var role in new[] { "SalesStaff", "Manager", "Admin" })
        {
            session.SetSession("probe", "probe", role, role, role);
            Console.WriteLine($"[RETURNS] Mounting {role}...");
            var page = provider.GetRequiredService<OrderReturnsPage>();
            page.ViewModel.OrderId = fixture.OrderId;
            host.Content = page;
            await page.ViewModel.ActivateAsync(token);
            await settle();
            await CheckViewportsAsync(page, role, settle, reports);
            var cash = Descendants<Button>(page).SingleOrDefault(b => Equals(b.Content, "Duyệt chi tiền mặt"));
            var qty = Descendants<TextBox>(page).Single(b => Equals(b.Header, "Số lượng trả"));
            var visible = cash is not null && IsVisible(cash);
            qty.Text = "1";
            await settle();
            var bound = page.ViewModel.Lines.Single().Quantity == "1";
            var rolePassed = role == "Manager" ? visible : !visible;
            if (role == "SalesStaff")
            {
                var reason = Descendants<TextBox>(page).Single(b => Equals(b.Header, "Lý do trả hàng"));
                reason.Text = "Khách trả một bộ sạc";
                await settle();
                await CaptureAsync(page, "01-sales");
                await page.ViewModel.SubmitCommand.ExecuteAsync(null);
                await settle();
                rolePassed &= page.ViewModel.Selected is { Status: "Pending" }
                    && Descendants<TextBlock>(page).Any(t => t.Text?.Contains("Trắng · USB65-W × 1") == true);
                await CaptureAsync(page, "02-request");
            }
            if (role == "Manager")
            {
                await page.ViewModel.SettleCommand.ExecuteAsync(cash!.CommandParameter);
                await page.ViewModel.SettleCommand.ExecuteAsync(cash!.CommandParameter);
                await settle();
                var scroll = (ScrollViewer)page.Content;
                scroll.ChangeView(null, scroll.ScrollableHeight, null, true);
                await settle();
                await CaptureAsync(page, "03-manager");
                rolePassed &= fixture.Settlements.Count == 2 && fixture.Settlements.All(id => id == fixture.RequestId)
                    && Descendants<TextBlock>(page).Any(t => t.Text == "Cần mở ca của bạn.");
                await page.ViewModel.SettleCommand.ExecuteAsync("VietQr");
                await settle();
                scroll.ChangeView(null, scroll.ScrollableHeight, null, true);
                await settle();
                await CaptureAsync(page, "04-bank");
                rolePassed &= Descendants<TextBlock>(page).Any(t => t.Text?.Contains("Đã duyệt chi ngân hàng") == true);
                page.ViewModel.BankReference = "DEMO-REF-031";
                page.ViewModel.BankAmount = "330000";
                page.ViewModel.BankPaidTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture);
                await page.ViewModel.ReconcileCommand.ExecuteAsync("Paid");
                await settle();
                scroll.ChangeView(null, scroll.ScrollableHeight, null, true);
                await settle();
                rolePassed &= page.ViewModel.Selected is { Status: "Refunded", BankReference: "DEMO-REF-031" };
                await CaptureAsync(page, "05-settled");
            }
            if (role == "Admin")
            {
                var exchange = Descendants<Button>(page).Single(b => Equals(b.Content, "Lập đơn đổi tại quầy"));
                var scroll = (ScrollViewer)page.Content;
                scroll.ChangeView(null, scroll.ScrollableHeight, null, true);
                await settle();
                rolePassed &= IsVisible(exchange) && exchange.Command == page.ViewModel.ExchangeCommand;
                await CaptureAsync(page, "05b-admin");
            }
            var amountLabel = page.ViewModel.Lines.Single().Label;
            rolePassed &= amountLabel.Contains(role == "SalesStaff" ? "990.000 ₫" : "660.000 ₫", StringComparison.Ordinal) && Enum.TryParse<Symbol>(
                ShellDestinations.Get(ShellDestination.ReturnInspections).Symbol, out _);
            reports.Add(new($"Returns {role} controls", "Mounted order route + HTTP fixture",
                "Role actions, two-way quantity, stable refund ID and shift errors", $"cashVisible={visible}, quantityBound={bound}", bound && rolePassed, ""));
            page.ViewModel.Deactivate();
        }

        session.SetSession("probe", "probe", "warehouse", "Kho", "WarehouseStaff");
        Console.WriteLine("[RETURNS] Mounting WarehouseStaff...");
        var warehouse = provider.GetRequiredService<ReturnInspectionsPage>();
        host.Content = warehouse;
        fixture.Queue = QueueResponse.Unavailable;
        await warehouse.ViewModel.ActivateAsync(token);
        await settle();
        var loadFailed = warehouse.ViewModel.IsError && Descendants<TextBlock>(warehouse).Any(t => t.Text == warehouse.ViewModel.ErrorMessage)
            && !Descendants<TextBlock>(warehouse).Any(t => t.Text == "Chưa có lô hàng trả.");
        fixture.Queue = QueueResponse.Empty;
        await warehouse.ViewModel.RefreshCommand.ExecuteAsync(null);
        await settle();
        var emptyShown = warehouse.ViewModel.IsEmpty && Descendants<TextBlock>(warehouse).Any(t => t.Text == "Chưa có lô hàng trả.");
        fixture.Queue = QueueResponse.Populated;
        await warehouse.ViewModel.RefreshCommand.ExecuteAsync(null);
        reports.Add(new("Returns Warehouse load recovery", "Failed load, empty response, then populated queue",
            "Error and empty messages are distinct", $"error={loadFailed}, empty={emptyShown}",
            loadFailed && emptyShown && warehouse.ViewModel.IsContent, ""));
        await settle();
        await CheckViewportsAsync(warehouse, "WarehouseStaff", settle, reports);
        foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark })
        {
            warehouse.RequestedTheme = theme;
            await settle();
            var button = Descendants<Button>(warehouse).Single(b => Equals(b.Content, "Hàng lỗi"));
            var title = Descendants<TextBlock>(warehouse).Single(t => t.Text == "Kiểm định hàng trả");
            var canvas = warehouse.Background as SolidColorBrush;
            var background = canvas is null ? "" : $"#{canvas.Color.R:X2}{canvas.Color.G:X2}{canvas.Color.B:X2}";
            var palette = theme == ElementTheme.Light ? AppColors.Light : AppColors.Dark;
            var focused = button.Focus(FocusState.Keyboard);
            var text = title.Foreground as SolidColorBrush;
            var foreground = text is null ? "" : $"#{text.Color.R:X2}{text.Color.G:X2}{text.Color.B:X2}";
            var pass = focused && warehouse.ActualTheme == theme && background == palette[AppColors.Canvas] && foreground == palette[AppColors.Text];
            await CaptureAsync(warehouse, theme == ElementTheme.Light ? "06-warehouse-light" : "07-warehouse-dark");
            reports.Add(new($"Returns Warehouse {theme} and keyboard", "Mounted production inspection form",
                "Title and canvas follow theme, decision button accepts keyboard focus", $"theme={warehouse.ActualTheme}, text={foreground}, canvas={background}, focus={focused}", pass, ""));
        }

        var picker = Descendants<ComboBox>(warehouse).Single();
        picker.SelectedIndex = 1;
        await settle();
        var quantityBox = Descendants<TextBox>(warehouse).Single(b => Equals(b.Header, "Số lượng kiểm định"));
        var device = warehouse.ViewModel.Selected;
        var deviceBound = device?.Serial == "359111111111119" && device.Label.Contains("PHONE-BLUE") &&
            !quantityBox.IsEnabled && quantityBox.Text == "1";
        picker.SelectedIndex = 0;
        await settle();
        reports.Add(new("Returns Warehouse serialized selection", "Select an IMEI, then return to a bulk batch",
            "Fixed one unit for the device and editable bulk quantity", device?.Label ?? "", deviceBound && quantityBox.IsEnabled, ""));

        var defect = Descendants<Button>(warehouse).Single(b => Equals(b.Content, "Hàng lỗi"));
        await warehouse.ViewModel.InspectCommand.ExecuteAsync(defect.CommandParameter);
        warehouse.ViewModel.Deactivate();
        var reopened = provider.GetRequiredService<ReturnInspectionsPage>();
        host.Content = reopened;
        await reopened.ViewModel.ActivateAsync(token);
        await settle();
        var selected = reopened.ViewModel.Selected;
        var resultShown = Descendants<TextBlock>(reopened).Any(t => t.Text?.Contains("Hàng lỗi 1") == true);
        reports.Add(new("Returns Warehouse reopened batch", "New mounted page after partial inspection",
            "One pending, one defective and one restocked", selected?.Label ?? "", selected is { Pending: 1, Defective: 1, Restocked: 1 } && resultShown, ""));
        await CaptureAsync(reopened, "08-warehouse-partial");
        reopened.ViewModel.Deactivate();
        host.Content = null;
        return reports;
    }

    private static async Task CheckViewportsAsync(Page page, string role, Func<Task> settle, List<ProbeReportItem> reports)
    {
        foreach (var (width, height) in new[] { (800, 600), (1024, 768), (1440, 1000) })
        {
            page.Width = width;
            page.Height = height;
            await settle();
            page.UpdateLayout();
            var scroll = (ScrollViewer)page.Content;
            var actual = $"{page.ActualWidth}x{page.ActualHeight}, horizontal overflow {scroll.ScrollableWidth}";
            reports.Add(new($"Returns {role} {width}x{height}", "Mounted page layout",
                "Requested dimensions and no horizontal scrolling", actual,
                page.ActualWidth == width && page.ActualHeight == height && scroll.ScrollableWidth == 0, ""));
        }
        page.Width = 1024;
        page.Height = 640;
        await settle();
    }

    private static async Task CaptureAsync(Page page, string name)
    {
        var directory = Environment.GetEnvironmentVariable("TECHSTORE_RETURNS_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var target = new RenderTargetBitmap();
        await target.RenderAsync(page, 1024, 640);
        var pixels = await target.GetPixelsAsync();
        using var reader = DataReader.FromBuffer(pixels);
        var bytes = new byte[pixels.Length];
        reader.ReadBytes(bytes);
        using var image = SKImage.FromPixelCopy(new SKImageInfo(target.PixelWidth, target.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul), bytes);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(System.IO.Path.Combine(directory, name + ".png"));
        png.SaveTo(file);
    }

    private static bool IsVisible(DependencyObject? node)
    {
        for (; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is UIElement { Visibility: Visibility.Collapsed }) return false;
        return true;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private enum QueueResponse { Populated, Empty, Unavailable }

    private sealed class ReturnApi : HttpMessageHandler
    {
        public Guid OrderId { get; } = Guid.NewGuid();
        public Guid RequestId { get; private set; } = Guid.NewGuid();
        private ReturnDto? _receipt;
        public List<Guid> Settlements { get; } = [];
        private readonly Guid _item = Guid.NewGuid();
        private readonly Guid _inspectionRequest = Guid.NewGuid();
        private readonly ReturnInspectionDto _device = new(Guid.NewGuid(), Guid.NewGuid(), "Điện thoại",
            Guid.NewGuid(), "359111111111119", 1, 1, 0, 0, 0, "PHONE-BLUE");
        public QueueResponse Queue { get; set; }
        private int _defective;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken token)
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/api/returns" && req.Method == HttpMethod.Post)
            {
                var body = (await req.Content!.ReadFromJsonAsync<CreateReturnRequest>(token))!;
                RequestId = body.RequestId;
                _receipt = new(RequestId, OrderId, "Pending", body.Reason, null, null, null, DateTime.UtcNow, null,
                    [new(_item, 1, 300000m, 30000m, 0)]);
                return Ok(_receipt);
            }
            if (path == $"/api/returns/order/{OrderId}")
                return Ok(new OrderReturnsDto(OrderId, "ORD-202611-0031", "Completed",
                    [new(_item, "Bộ sạc USB-C 65W", null, null, _receipt?.Status == "Refunded" ? 2 : 3, _receipt?.Status == "Refunded" ? 660000m : 990000m, "USB65-W", "Trắng")],
                    _receipt is null ? [] : [_receipt]));
            if (path == $"/api/returns/{RequestId}/settle")
            {
                var method = (await req.Content!.ReadFromJsonAsync<SettleReturnRequest>(token))!.Method;
                if (method == "VietQr") return Ok(_receipt = _receipt! with { Status = "AwaitingBank", Method = method, BankApprovedAt = DateTime.UtcNow });
                Settlements.Add(RequestId);
                return new(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { detail = "Cần mở ca của bạn." }) };
            }
            if (path == $"/api/returns/{RequestId}/reconcile")
            {
                var body = (await req.Content!.ReadFromJsonAsync<ReconcileReturnRequest>(token))!;
                return Ok(_receipt = _receipt! with { Status = "Refunded", BankReference = body.BankReference, RefundedAt = body.PaidAt?.UtcDateTime });
            }
            if (path == $"/api/inventory/returns/{_item}/inspections")
            {
                var body = await req.Content!.ReadFromJsonAsync<InspectReturnRequest>(token);
                if (body!.Outcome == "Defective") _defective += body.Quantity;
                return Ok(Batch());
            }
            if (path == "/api/inventory/returns")
            {
                if (Queue == QueueResponse.Unavailable) return new(HttpStatusCode.ServiceUnavailable);
                return Ok(Queue == QueueResponse.Empty ? Array.Empty<ReturnInspectionDto>() : new[] { Batch(), _device });
            }
            return new(HttpStatusCode.NotFound);
        }

        private ReturnInspectionDto Batch() => new(_item, _inspectionRequest, "Bộ sạc USB-C 65W", null, null, 3, 2 - _defective, 1, _defective, 0);
        private static HttpResponseMessage Ok<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
