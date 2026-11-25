using System.Net.Http.Json;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Repositories.Loyalty;
using TechStore.Client.Repositories.Vouchers;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Customers;
using TechStore.Client.ViewModels.Vouchers;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
using Xunit.Abstractions;

namespace TechStore.Client.IntegrationTests;

/// <summary>
/// Opt-in live check of the voucher screens and the customer loyalty card: the real ViewModels and repositories against a
/// running API and PostgreSQL. It leaves one voucher (switched off) and one customer (deactivated) behind, both with unique
/// names. <c>TECHSTORE_API_BASE_URL=http://localhost:5176/ dotnet test tests/Frontend.IntegrationTests --filter VoucherLoyalty</c>.
/// </summary>
[Collection("Live API database")]
public sealed class VoucherLoyaltyApiIntegrationTests(ITestOutputHelper output)
{
    [OptInFact]
    public async Task LiveVouchers_ManagementFlowWorksThroughTheViewModels()
    {
        using var admin = await Rig.SignInAsync("admin", "Admin@123");
        var code = "UI" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var vouchers = new VoucherRepository(admin.Api);

        // ---- create through the editor ----
        var editor = new VoucherEditorDialogViewModel(vouchers);
        editor.Initialize(null);
        editor.Code = code.ToLowerInvariant();
        editor.Description = "Live UI check";
        editor.DiscountValue = "10";
        editor.MinOrderAmount = "1.000.000";
        editor.MaxDiscountAmount = "100.000";
        editor.UsageLimit = "5";
        editor.ExpiresAt = DateTimeOffset.Now.AddDays(30);
        var closed = 0;
        editor.RequestClose += (_, _) => closed++;
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.True(editor.SavedVoucher is not null, $"create failed: {editor.FormError} {editor.CodeError}");
        Assert.Equal(code, editor.SavedVoucher!.Code);
        Assert.Equal(1, closed);
        output.WriteLine($"created {code} id={editor.SavedVoucher.Id}");

        try
        {
            // ---- the same code again is refused with 409 and the dialog stays open ----
            var duplicate = new VoucherEditorDialogViewModel(vouchers);
            duplicate.Initialize(null);
            duplicate.Code = code;
            duplicate.DiscountValue = "5";
            duplicate.UsageLimit = "1";
            duplicate.ExpiresAt = DateTimeOffset.Now.AddDays(5);
            await duplicate.SaveCommand.ExecuteAsync(null);
            Assert.Contains("đã tồn tại", duplicate.CodeError);
            Assert.Null(duplicate.SavedVoucher);

            // ---- the list finds it by code, with the status the rules give ----
            var list = new VoucherListViewModel(vouchers, new RecordingNavigation());
            await list.ActivateAsync();
            list.SearchText = code;
            await list.SearchCommand.ExecuteAsync(null);
            var row = Assert.Single(list.Rows);
            Assert.Equal(code, row.Code);
            Assert.Equal("10% · tối đa 100.000 ₫", row.DiscountLabel);
            Assert.Equal("1.000.000 ₫", row.MinOrderLabel);
            Assert.Equal("0 / 5 lượt", row.UsageLabel);
            Assert.Equal("Đang hoạt động", row.StatusLabel);

            // ---- edit: the code and type stay, the values change ----
            var edit = new VoucherEditorDialogViewModel(vouchers);
            edit.Initialize(row.Voucher);
            Assert.False(edit.IsCodeEditable);
            edit.DiscountValue = "12,5";
            edit.UsageLimit = "10";
            await edit.SaveCommand.ExecuteAsync(null);
            Assert.True(edit.SavedVoucher is not null, $"update failed: {edit.FormError} {edit.UsageLimitError} {edit.ExpiresAtError}");
            Assert.Equal(12.5m, edit.SavedVoucher!.DiscountValue);
            Assert.Equal(10, edit.SavedVoucher.UsageLimit);
            Assert.Equal(code, edit.SavedVoucher.Code);

            // ---- switch off and on through the list ----
            await list.NotifySavedAsync();
            await list.ToggleActiveCommand.ExecuteAsync(Assert.Single(list.Rows));
            Assert.Equal("Đã tắt", Assert.Single(list.Rows).StatusLabel);
            await list.ToggleActiveCommand.ExecuteAsync(Assert.Single(list.Rows));
            var back = Assert.Single(list.Rows);
            Assert.Equal("Đang hoạt động", back.StatusLabel);
            Assert.Equal("12,5% · tối đa 100.000 ₫", back.DiscountLabel);
            Assert.Equal(string.Empty, list.ActionErrorMessage);

            // ---- a status filter matches the server ----
            list.SelectedStatusIndex = 2;
            await list.SearchCommand.ExecuteAsync(null);
            Assert.Empty(list.Rows);
            Assert.True(list.ShowEmptyState);
        }
        finally
        {
            await vouchers.SetActiveAsync(editor.SavedVoucher!.Id, false);
        }
    }

    [OptInFact]
    public async Task LiveVouchers_SalesStaffIsRefusedByTheServer()
    {
        using var sales = await Rig.SignInAsync("cashier1", "Cashier@123");
        var list = new VoucherListViewModel(new VoucherRepository(sales.Api), new RecordingNavigation());

        await list.ActivateAsync();

        Assert.True(list.IsError);
        Assert.Contains("không có quyền", list.ErrorMessage);
        Assert.Empty(list.Rows);
    }

    [OptInFact]
    public async Task LiveLoyalty_SummaryCardShowsForAdminAndSales()
    {
        using var admin = await Rig.SignInAsync("admin", "Admin@123");
        var customers = new CustomerRepository(admin.Api);
        var phone = "09" + Random.Shared.Next(10_000_000, 99_999_999);
        var customer = await customers.CreateAsync(new CreateCustomerRequest { FullName = "Live Loyalty " + phone, Phone = phone });
        try
        {
            var adminCard = new CustomerDetailViewModel(customers, loyalty: new LoyaltyRepository(admin.Api));
            await adminCard.LoadAsync(customer.Id);
            Assert.True(adminCard.HasLoyaltySummary);
            Assert.Equal("Chưa có ưu đãi hạng", adminCard.TierDiscountLabel);
            Assert.Equal("Còn 500 điểm để lên hạng Bạc", adminCard.NextTierLabel);
            Assert.Equal(0.0, adminCard.TierProgress);

            using var sales = await Rig.SignInAsync("cashier1", "Cashier@123");
            // The app always has a session; for SalesStaff the ViewModel then skips the history the server refuses.
            var salesSession = new InMemorySessionStore();
            salesSession.SetSession("token", "refresh", "cashier1", "Thu ngân", "SalesStaff");
            var salesCard = new CustomerDetailViewModel(new CustomerRepository(sales.Api), salesSession, new LoyaltyRepository(sales.Api));
            await salesCard.LoadAsync(customer.Id);
            Assert.True(salesCard.HasLoyaltySummary);
            Assert.False(salesCard.CanManageCustomers);
            Assert.Null(salesCard.PointHistory);

            // An unknown customer is a 404: no card, and no crash.
            Assert.Null(await new LoyaltyRepository(admin.Api).GetSummaryAsync(Guid.NewGuid()));
        }
        finally
        {
            await customers.DeactivateAsync(customer.Id);
        }
    }

    private sealed class Rig : IDisposable
    {
        private readonly HttpClient _http;
        private readonly BearerTokenHandler _handler;
        public ApiClient Api { get; }

        private Rig(HttpClient http, BearerTokenHandler handler, ApiClient api)
        {
            _http = http;
            _handler = handler;
            Api = api;
        }

        public static async Task<Rig> SignInAsync(string username, string password)
        {
            var baseUrl = Environment.GetEnvironmentVariable("TECHSTORE_API_BASE_URL")
                ?? throw new InvalidOperationException("Set TECHSTORE_API_BASE_URL to opt in to the live API integration tests.");
            var options = new ApiClientOptions { BaseUrl = baseUrl, TimeoutSeconds = 15 };
            var baseUri = options.Validate();
            var session = new InMemorySessionStore();
            var handler = new BearerTokenHandler(session)
            {
                InnerHandler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }
            };
            var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            using var response = await http.PostAsJsonAsync(
                new Uri(baseUri, "api/auth/login"), new LoginRequest { Username = username, Password = password });
            response.EnsureSuccessStatusCode();
            var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>()
                ?? throw new InvalidOperationException("Login returned no body.");
            session.SetAccessToken(login.AccessToken);
            return new Rig(http, handler, new ApiClient(http, options));
        }

        public void Dispose()
        {
            _http.Dispose();
            _handler.Dispose();
        }
    }

    private sealed class RecordingNavigation : INavigationService
    {
        public AppRoute CurrentRoute => AppRoute.Root(ShellDestination.Customers);
        public ShellDestination CurrentDestination => ShellDestination.Customers;
        public event EventHandler<ShellDestination>? DestinationChanged { add { } remove { } }
        public event EventHandler<AppRoute>? RouteChanged { add { } remove { } }
        public void Reset(AppRoute route) { }
        public NavigationOutcome Navigate(ShellDestination destination) => NavigationOutcome.Navigated;
        public NavigationOutcome Navigate(AppRoute route) => NavigationOutcome.Navigated;
    }
}
