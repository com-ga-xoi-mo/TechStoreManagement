using TechStore.Client.Models.Warranty;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Repositories.Warranty;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Client.ViewModels.Customers;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.ViewModels.Warranty;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Warranty;

public sealed class WarrantyWorkspaceTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Variant = Guid.NewGuid();
    private static readonly WarrantyRow Row = new(Id, "BH-202611-0001", "Received", "Máy", "351", "Lan", DateTime.UtcNow);

    [Fact]
    public void Actions_FollowStatusAndRole()
    {
        var statuses = new[] { "Received", "Repairing", "PendingReplacement", "Repaired", "Rejected", "ReturnedToCustomer", "Replaced" };
        var roles = new[] { "Admin", "Manager", "SalesStaff", "WarehouseStaff", null };
        foreach (var status in statuses)
        foreach (var role in roles)
        {
            var result = WarrantyCaseActions.For(status, role);
            Assert.Equal(status == "Received" && role is "Admin" or "WarehouseStaff", result.Inspect);
            Assert.Equal(status == "Repairing" && role is "Admin" or "WarehouseStaff", result.Repaired);
            Assert.Equal(status == "PendingReplacement" && role is "Admin" or "Manager", result.DecideReplacement);
            Assert.Equal(status is "Repaired" or "Rejected" && role is "Admin" or "Manager" or "SalesStaff", result.Return);
        }
    }

    [Fact]
    public async Task List_LoadFilterPageErrorRetryAndCancel()
    {
        var repo = new Repo();
        var model = new WarrantyCaseListViewModel(repo, Session("SalesStaff"));
        await model.ActivateAsync();
        Assert.True(model.CanIntake);
        Assert.Equal(PageContentState.Content, model.ContentState);
        model.Query = "0901";
        model.SelectedFilter = model.Filters.Single(x => x.Value == "Received");
        await model.ApplyFilterCommand.ExecuteAsync(null);
        await model.NextCommand.ExecuteAsync(null);
        Assert.Equal(("0901", "Received", 2), repo.ListCall);

        repo.List = _ => throw Error(ApiErrorKind.Network);
        await model.RefreshAsync();
        Assert.Equal(PageContentState.Error, model.ContentState);
        Assert.Empty(model.Rows);
        repo.List = _ => Task.FromResult(new WarrantyPage([], 2, 0, 0));
        await model.RetryCommand.ExecuteAsync(null);
        Assert.True(model.IsEmpty);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repo.List = async token =>
        {
            using var registration = token.Register(() => canceled.TrySetResult());
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new WarrantyPage([], 1, 0, 0);
        };
        var pending = model.RefreshAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        model.Deactivate();
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await pending;
        Assert.Empty(model.Rows);
    }

    [Fact]
    public async Task Intake_BlocksIneligibleDeviceAndRetainsIdAfterNetworkFailure()
    {
        var repo = new Repo();
        var customerRepository = new CustomerRepository(new RecordingApiClient((_, _, _) => throw new NotSupportedException()));
        var model = new WarrantyIntakeDialogViewModel(repo,
            new CustomerPickerViewModel(customerRepository, new CustomerFormViewModel(customerRepository))) { Code = "351" };
        var signal = 0;
        model.CaseChanged += () => signal++;
        repo.Lookup = new(false, [WarrantyReasonCodes.Expired], "Máy", "351", DateTime.UtcNow, null, null, null);
        await model.LookupCommand.ExecuteAsync(null);
        Assert.False(model.ShowIntake);
        Assert.Contains("hết hạn", model.ErrorMessage);
        Assert.Contains("Hạn bảo hành", model.LookupSummary);

        repo.Lookup = new(true, [], "Máy", "351", DateTime.UtcNow, "ORD-01", "Lan", "0901234567");
        model.OrderCode = "ORD-01";
        await model.LookupCommand.ExecuteAsync(null);
        Assert.True(model.ShowIntake);
        Assert.Equal("Lan", model.ContactName);
        Assert.Equal("0901234567", model.ContactPhone);
        model.Issue = "Lỗi nguồn";
        repo.FailIntake = true;
        await model.SubmitCommand.ExecuteAsync(null);
        Assert.False(model.Succeeded);
        Assert.Contains("kết nối", model.ErrorMessage);

        repo.FailIntake = false;
        await model.SubmitCommand.ExecuteAsync(null);
        Assert.True(model.Succeeded);
        Assert.Equal(1, signal);
        Assert.Equal(new[] { model.CaseId, model.CaseId }, repo.IntakeIds);
    }

    [Theory]
    [InlineData("Received", "WarehouseStaff", "inspect", "Repairing")]
    [InlineData("Repairing", "WarehouseStaff", "repaired", "Repaired")]
    [InlineData("PendingReplacement", "Manager", "reject", "Repairing")]
    [InlineData("PendingReplacement", "Manager", "replace", "Replaced")]
    [InlineData("Repaired", "SalesStaff", "return", "ReturnedToCustomer")]
    public async Task Detail_ExecutesAllowedDecisionAndUpdatesFromServer(string status, string role, string operation, string target)
    {
        var repo = new Repo { Detail = Details(status), Next = Details(target) };
        var spare = new WarrantyReplacement(Guid.NewGuid(), "001");
        repo.Spares = [spare];
        var model = new WarrantyCaseDetailDialogViewModel(repo, Session(role)) { CaseId = Id, Note = "Đã kiểm tra" };
        await model.ActivateAsync();
        switch (operation)
        {
            case "inspect": await model.InspectCommand.ExecuteAsync(null); break;
            case "repaired": await model.RepairedCommand.ExecuteAsync(null); break;
            case "reject": await model.RejectReplacementCommand.ExecuteAsync(null); break;
            case "replace":
                await model.FindReplacementsCommand.ExecuteAsync(null);
                Assert.Equal((Product, (Guid?)Variant), repo.StockQuery);
                model.SelectedReplacement = Assert.Single(model.Replacements);
                await model.ReplaceCommand.ExecuteAsync(null);
                break;
            case "return": await model.ReturnCommand.ExecuteAsync(null); break;
        }
        Assert.Equal(operation, repo.LastOperation);
        Assert.Equal(target, model.Details!.Row.Status);
        Assert.True(model.HasChanges);
    }

    [Fact]
    public async Task Detail_EmptyReplacementListKeepsRejectAvailable_And409Refreshes()
    {
        var repo = new Repo { Detail = Details("PendingReplacement") };
        var model = new WarrantyCaseDetailDialogViewModel(repo, Session("Manager")) { CaseId = Id };
        await model.ActivateAsync();
        await model.FindReplacementsCommand.ExecuteAsync(null);
        Assert.False(model.CanReplace);
        Assert.True(model.ReplacementAllowed);
        Assert.Contains("Không còn máy", model.ReplacementMessage);

        repo.Detail = Details("Replaced");
        repo.OperationError = new WarrantyRequestException(new ApiException(ApiErrorKind.Http, "conflict", 409, detail: "Đã được xử lý."));
        await model.RejectReplacementCommand.ExecuteAsync(null);
        Assert.Contains("Đã được xử lý", model.OperationMessage);
        Assert.Equal("Replaced", model.Details!.Row.Status);
        Assert.False(model.ReplacementAllowed);

        var sales = new WarrantyCaseDetailDialogViewModel(repo, Session("SalesStaff")) { CaseId = Id };
        await sales.ActivateAsync();
        Assert.False(sales.HasCost);
        Assert.Empty(sales.CostLabel);
    }

    private static WarrantyDetails Details(string status) => new(Row with { Status = status }, Product, Variant, "Đen",
        "ORD-01", DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "Lỗi nguồn", "Lan", "0901", null, null, null, null, []);

    private static InMemorySessionStore Session(string role)
    {
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "staff", "Staff", role);
        return session;
    }

    private static WarrantyRequestException Error(ApiErrorKind kind) => new(new ApiException(kind, "test"));

    private sealed class Repo : IWarrantyRepository
    {
        public (string?, string?, int) ListCall { get; private set; }
        public Func<CancellationToken, Task<WarrantyPage>> List { get; set; } =
            _ => Task.FromResult(new WarrantyPage([Row], 1, 2, 21));
        public WarrantyLookup Lookup { get; set; } = null!;
        public WarrantyDetails Detail { get; set; } = Details("Received");
        public WarrantyDetails Next { get; set; } = Details("Received");
        public bool FailIntake { get; set; }
        public List<Guid> IntakeIds { get; } = [];
        public string? LastOperation { get; private set; }
        public Exception? OperationError { get; set; }
        public IReadOnlyList<WarrantyReplacement> Spares { get; set; } = [];
        public (Guid, Guid?) StockQuery { get; private set; }

        public Task<WarrantyPage> ListAsync(string? query, string? status, int page, CancellationToken token)
        {
            ListCall = (query, status, page);
            return List(token);
        }

        public Task<WarrantyLookup> LookupAsync(string code, string? orderCode, Guid? customerId, CancellationToken token) => Task.FromResult(Lookup);
        public Task<WarrantyDetails> GetAsync(Guid id, CancellationToken token) => Task.FromResult(Detail);
        public Task<WarrantyDetails> ReceiveAsync(Guid id, ReceiveWarrantyRequest request, CancellationToken token)
        {
            IntakeIds.Add(id);
            return FailIntake ? Task.FromException<WarrantyDetails>(Error(ApiErrorKind.Network)) : Task.FromResult(Detail);
        }

        public Task<IReadOnlyList<WarrantyReplacement>> ReplacementsAsync(Guid productId, Guid? variantId, CancellationToken token)
        {
            StockQuery = (productId, variantId);
            return Task.FromResult(Spares);
        }

        public Task<WarrantyDetails> InspectAsync(Guid id, InspectWarrantyRequest request, CancellationToken token) => Act("inspect");
        public Task<WarrantyDetails> RepairedAsync(Guid id, string? note, CancellationToken token) => Act("repaired");
        public Task<WarrantyDetails> ReplaceAsync(Guid id, Guid replacementDeviceId, CancellationToken token) => Act("replace");
        public Task<WarrantyDetails> RejectReplacementAsync(Guid id, string note, CancellationToken token) => Act("reject");
        public Task<WarrantyDetails> ReturnAsync(Guid id, CancellationToken token) => Act("return");
        private Task<WarrantyDetails> Act(string name)
        {
            LastOperation = name;
            return OperationError == null ? Task.FromResult(Next) : Task.FromException<WarrantyDetails>(OperationError);
        }
    }
}
