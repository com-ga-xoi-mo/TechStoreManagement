using TechStore.Client.Models.Orders;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.ViewModels.Sales;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Orders;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Orders;

public sealed class ReturnWorkflowTests
{
    private sealed class Returns : IReturnRepository
    {
        public Guid OrderId { get; } = Guid.NewGuid();
        public ReturnBalance Balance { get; } = new(Guid.NewGuid(), "Điện thoại", Guid.NewGuid(), "SER-001", 1, 123.45m);
        public IReadOnlyList<ReturnBalance>? Balances;
        public bool EmptyQueue;
        public ReturnHistory? Row;
        public IReadOnlyList<LegacyRefund>? Legacy;
        public string? Review;
        public List<Guid> Lookups { get; } = [];
        public List<CreateReturnRequest> Creates { get; } = [];
        public List<Guid> Settlements { get; } = [];
        public ReconcileReturnRequest? Reconciliation;
        public List<InspectReturnRequest> Inspected { get; } = [];
        public InspectionBatch Batch = new(Guid.NewGuid(), Guid.NewGuid(), "Cáp", null, 3, 2, 1, 0, 0);
        public Func<CancellationToken, Task>? Delay;
        public bool LoseReply;
        public ApiException? Error;

        public async Task<OrderReturnSummary> OrderAsync(Guid orderId, CancellationToken token)
        {
            if (Delay is { } wait) await wait(token);
            if (orderId != OrderId) throw new ApiException(ApiErrorKind.Http, "not found", 404);
            return new("ORD-1", Balances ?? [Balance], Row is null ? [] : [Row], Legacy, Review);
        }

        public Task<ReturnHistory> GetAsync(Guid id, CancellationToken token)
        {
            Lookups.Add(id);
            return Task.FromResult(Row is { } row && row.Id == id ? row : throw new ApiException(ApiErrorKind.Http, "", 404));
        }

        public Task<ReturnHistory> CreateAsync(CreateReturnRequest request, CancellationToken token)
        {
            Creates.Add(request);
            if (Error is not null) throw Error;
            Row = new(request.RequestId, "Pending", request.Reason, null, null, null, DateTime.UtcNow, null, [new(Balance.ItemId, 1, 100m, 23.45m, 0m)]);
            return Reply(Row);
        }

        public Task<ReturnHistory> SettleAsync(Guid id, string method, CancellationToken token)
        {
            Settlements.Add(id);
            if (Error is not null) throw Error;
            Row = Row! with { Status = method == "Cash" ? "Refunded" : "AwaitingBank", Method = method, BankApprovedAt = method == "VietQr" ? DateTime.UtcNow : null };
            return Reply(Row);
        }

        public Task<ReturnHistory> ReconcileAsync(Guid id, ReconcileReturnRequest request, CancellationToken token)
        {
            Reconciliation = request;
            return Reply(Row = Row! with { Status = request.Paid ? "Refunded" : "Failed", BankReference = request.BankReference, RefundedAt = request.PaidAt?.UtcDateTime });
        }

        public Task<ReturnHistory> LinkAsync(Guid id, Guid replacementOrderId, CancellationToken token) =>
            Reply(Row = Row! with { ReplacementOrderId = replacementOrderId });

        public Task<IReadOnlyList<InspectionBatch>> InspectionsAsync(CancellationToken token) => Error != null ? Task.FromException<IReadOnlyList<InspectionBatch>>(Error) : Task.FromResult<IReadOnlyList<InspectionBatch>>(EmptyQueue ? [] : [Batch]);

        public Task<InspectionBatch> InspectAsync(Guid id, InspectReturnRequest request, CancellationToken token)
        {
            if (Inspected.All(i => i.OperationId != request.OperationId))
                Batch = Batch with { Pending = Batch.Pending - request.Quantity, Defective = Batch.Defective + request.Quantity };
            Inspected.Add(request);
            return Reply(Batch);
        }

        private Task<T> Reply<T>(T value)
        {
            if (!LoseReply) return Task.FromResult(value);
            LoseReply = false;
            throw new ApiException(ApiErrorKind.Network, "lost response");
        }
    }

    private static InMemorySessionStore Login(string role)
    {
        var session = new InMemorySessionStore();
        session.SetSession("token", "refresh", "employee", "Employee", role);
        return session;
    }

    private static async Task<OrderReturnsViewModel> Open(Returns api, string role)
    {
        var vm = new OrderReturnsViewModel(api, Login(role), new FakeNavigation(), new FakePosRepository()) { OrderId = api.OrderId };
        await vm.ActivateAsync();
        return vm;
    }

    private static void Fill(OrderReturnsViewModel vm)
    {
        vm.Reason = "Sai kích cỡ";
        Assert.Single(vm.Lines).Quantity = "1";
    }

    [Fact]
    public async Task Sales_UsesServerBalancesAndSerial_RecoversExistingRequestWithoutResending()
    {
        var api = new Returns { LoseReply = true };
        var vm = await Open(api, "SalesStaff");
        Assert.Equal(123.45m, Assert.Single(vm.Lines).Balance.Amount);
        Fill(vm);
        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.False(vm.CanEditDraft);
        await vm.SubmitCommand.ExecuteAsync(null);

        var request = Assert.Single(api.Creates);
        Assert.Equal(api.Balance.SerialId, Assert.Single(request.Lines).SerialImeiId);
        Assert.Equal(request.RequestId, Assert.Single(api.Lookups));
        Assert.Equal(request.RequestId, vm.Selected!.Id);
        Assert.False(vm.ShowApproval);
        await vm.SettleCommand.ExecuteAsync("Cash");
        Assert.Empty(api.Settlements);
    }

    [Fact]
    public async Task MissingOrder_IsHiddenAndLineErrorsAppearOnTheSubmittedRow()
    {
        var api = new Returns();
        var vm = new OrderReturnsViewModel(api, Login("SalesStaff"), new FakeNavigation(), new FakePosRepository()) { OrderId = Guid.NewGuid() };
        await vm.ActivateAsync();
        Assert.Empty(vm.Lines);
        Assert.Contains("không còn tồn tại", vm.Message);

        vm = await Open(api, "SalesStaff");
        Fill(vm);
        api.Error = new(ApiErrorKind.Http, "invalid", 400, fieldErrors: new Dictionary<string, IReadOnlyList<string>>
        {
            ["lines[0].serialImeiId"] = ["IMEI không khớp."]
        });
        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Equal("IMEI không khớp.", vm.Lines[0].Error);
        Assert.True(vm.CanEditDraft);
    }

    [Fact]
    public async Task SessionChange_CancelsLoadAndDropsLateOrder()
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        var api = new Returns { Delay = token => { observed = token; start.SetResult(); return finish.Task; } };
        var session = Login("SalesStaff");
        var vm = new OrderReturnsViewModel(api, session, new FakeNavigation(), new FakePosRepository()) { OrderId = api.OrderId };
        var load = vm.ActivateAsync();
        await start.Task;
        session.Clear();
        Assert.True(observed.IsCancellationRequested);
        finish.SetResult();
        await load;
        Assert.Empty(vm.Lines);
        Assert.Empty(vm.OrderCode);
        Assert.False(vm.CanInteract);
    }

    [Fact]
    public async Task HistoricalAllocation_ExposesActualLegacyReceiptsAndBlocksNewRequestDuringReview()
    {
        var api = new Returns { Review = "Thiếu phân bổ giảm giá." };
        api.Legacy = [new(Guid.NewGuid(), api.Balance.ItemId, 1, 200m, "Cash", DateTime.UtcNow, "Hỏng màn hình", false)];
        var vm = await Open(api, "Manager");
        Assert.Contains("Hỏng màn hình", vm.LegacyHistory);
        Assert.Contains("200", vm.LegacyHistory);
        Assert.Equal(api.Review, vm.ReviewReason);
        Fill(vm);
        Assert.False(vm.CanSubmit);
        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Empty(api.Creates);
    }

    [Fact]
    public async Task Manager_SeesShiftFailure_RefetchesLostBankReplyAndReconcilesOnce()
    {
        var api = new Returns();
        var vm = await Open(api, "Manager");
        Fill(vm);
        await vm.SubmitCommand.ExecuteAsync(null);
        var id = vm.Selected!.Id;
        api.Error = new(ApiErrorKind.Http, "", 409, detail: "Cần mở ca của bạn.");
        await vm.SettleCommand.ExecuteAsync("Cash");
        Assert.Equal("Cần mở ca của bạn.", vm.Message);
        api.Error = null;
        api.LoseReply = true;
        await vm.SettleCommand.ExecuteAsync("VietQr");
        await vm.SettleCommand.ExecuteAsync("VietQr");
        Assert.Equal(new[] { id, id }, api.Settlements);
        Assert.Equal(id, Assert.Single(api.Lookups));
        Assert.True(vm.ShowReconciliation);
        Assert.False(vm.ShowApproval);
        vm.BankReference = "REF-1";
        vm.BankAmount = "100";
        await vm.ReconcileCommand.ExecuteAsync("Paid");
        Assert.True(vm.ShowReconciliation);
        Assert.Contains("chờ đối soát", vm.Message);
        vm.BankAmount = "123,45";
        vm.BankPaidTime = "20/11/2026 14:35";
        await vm.ReconcileCommand.ExecuteAsync("Paid");
        Assert.Equal("Refunded", vm.Selected!.Status);
        Assert.Equal(new DateTime(2026, 11, 20, 14, 35, 0), api.Reconciliation!.PaidAt!.Value.LocalDateTime);
        Assert.True(vm.ShowLink);
        Assert.False(vm.ShowCheckout);
        vm.ReplacementOrder = Guid.NewGuid().ToString();
        await vm.LinkCommand.ExecuteAsync(null);
        Assert.NotNull(vm.Selected!.ReplacementOrderId);
    }

    [Theory]
    [InlineData("24/11/2026 14:35:12", true, 0)]
    [InlineData("24/11/2026 14:35:12.1234567", true, 1234567)]
    [InlineData("24/11/2026 14:35:12,1234567", true, 1234567)]
    [InlineData("2026-11-24T14:35:12.1234567+07:00", false, 1234567)]
    [InlineData("2026-11-24T07:35:12.1234567Z", false, 1234567)]
    public async Task Reconcile_ReportsExactUtcPaymentWithinRequestMinute(string input, bool local, int fractionTicks)
    {
        var instant = local
            ? new DateTime(2026, 11, 24, 14, 35, 12, DateTimeKind.Local).ToUniversalTime()
            : new DateTime(2026, 11, 24, 7, 35, 12, DateTimeKind.Utc);
        var expected = instant.AddTicks(fractionTicks);
        var repo = new Returns();
        repo.Row = new(Guid.NewGuid(), "AwaitingBank", "Sai mẫu", "VietQr", null, null,
            instant.AddSeconds(-2), null, [new(repo.Balance.ItemId, 1, 100m, 20m, 0m)], instant.AddSeconds(-1));
        var vm = await Open(repo, "Manager");
        vm.BankReference = "REF-2";
        vm.BankAmount = "120";
        vm.BankPaidTime = input;
        await vm.ReconcileCommand.ExecuteAsync("Paid");
        var paidAt = Assert.IsType<DateTimeOffset>(repo.Reconciliation?.PaidAt);
        Assert.Equal(TimeSpan.Zero, paidAt.Offset);
        Assert.Equal(expected.Ticks, paidAt.UtcTicks);
    }

    [Fact]
    public async Task Manager_OpensOwnShift_WithoutEnteringCheckoutOrReopeningAnActiveShift()
    {
        var repo = new Returns();
        var shifts = new FakePosRepository();
        var vm = new OrderReturnsViewModel(repo, Login("Manager"), new FakeNavigation(), shifts) { OrderId = repo.OrderId };
        await vm.ActivateAsync();
        vm.OpeningCash = "250000";
        await vm.OpenRefundShiftCommand.ExecuteAsync(null);
        await vm.OpenRefundShiftCommand.ExecuteAsync(null);
        Assert.Equal(250000m, Assert.Single(shifts.Opened));
        Assert.Contains("đang mở", vm.Message);
        Assert.False(vm.CanCheckout);
    }

    [Fact]
    public async Task Inspections_FailedLoadAndRetry_ShowEmptyOnlyAfterSuccessfulResponse()
    {
        var repo = new Returns { EmptyQueue = true, Error = new(ApiErrorKind.Network, "offline") };
        var vm = new ReturnInspectionsViewModel(repo, Login("WarehouseStaff"));
        Assert.Empty(vm.EmptyMessage);
        await vm.ActivateAsync();
        Assert.True(vm.IsError);
        Assert.Contains("kết nối", vm.ErrorMessage);
        Assert.Empty(vm.EmptyMessage);
        repo.Error = null;
        await vm.RetryCommand.ExecuteAsync(null);
        Assert.True(vm.IsEmpty);
        Assert.NotEmpty(vm.EmptyMessage);
        Assert.Empty(vm.Message);
        repo.EmptyQueue = false;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.IsContent);
        Assert.Empty(vm.EmptyMessage);
    }

    [Fact]
    public async Task LegacyBankCase_RequiresApprovalEvidenceWhenConfirmingPayment()
    {
        var repo = new Returns();
        var approval = DateTimeOffset.UtcNow.AddHours(-1);
        repo.Row = new(Guid.NewGuid(), "AwaitingBank", "Đổi màu", "VietQr", null, null,
            approval.AddDays(-1).UtcDateTime, null, [new(repo.Balance.ItemId, 1, 100m, 0m, 0m)]);
        var vm = await Open(repo, "Manager");
        Assert.True(vm.NeedsLegacyApprovalTime);
        vm.BankReference = "LEGACY";
        vm.BankAmount = "100";
        vm.BankPaidTime = approval.AddMinutes(5).ToString("O");
        await vm.ReconcileCommand.ExecuteAsync("Paid");
        Assert.Null(repo.Reconciliation);
        vm.LegacyApprovalTime = approval.ToString("O");
        await vm.ReconcileCommand.ExecuteAsync("Paid");
        Assert.Equal(approval, repo.Reconciliation!.VerifiedLegacyApprovalAt);
    }

    [Fact]
    public async Task SamePriceVariants_AreIdentifiableInDraftAndBothHistoryFormats()
    {
        var blue = new ReturnBalance(Guid.NewGuid(), "Cáp", null, null, 1, 100m, "USB-B", "Xanh");
        var red = blue with { ItemId = Guid.NewGuid(), Sku = "USB-R", VariantName = "Đỏ" };
        var repo = new Returns { Balances = [blue, red] };
        repo.Row = new(Guid.NewGuid(), "Pending", "Đổi màu", null, null, null, DateTime.UtcNow, null,
            [new(red.ItemId, 1, 100m, 0m, 0m)]);
        repo.Legacy = [new(Guid.NewGuid(), blue.ItemId, 1, 100m, "Cash", DateTime.UtcNow, "Lỗi", true)];
        var vm = await Open(repo, "SalesStaff");
        Assert.Contains("Xanh · USB-B", vm.Lines[0].Label);
        Assert.Contains("Đỏ · USB-R", vm.Lines[1].Label);
        Assert.Contains("Đỏ · USB-R", vm.Details);
        Assert.Contains("Xanh · USB-B", vm.LegacyHistory);
    }

    [Fact]
    public async Task Inspection_RetriesSameOperationAndReopensWithServerRemainder()
    {
        var api = new Returns { LoseReply = true };
        var session = Login("WarehouseStaff");
        var vm = new ReturnInspectionsViewModel(api, session);
        await vm.ActivateAsync();
        await vm.InspectCommand.ExecuteAsync("Defective");
        Assert.True(vm.NeedsRetry);
        await vm.InspectCommand.ExecuteAsync(null);
        Assert.False(vm.NeedsRetry);
        Assert.Equal(api.Inspected[0], api.Inspected[1]);

        var reopened = new ReturnInspectionsViewModel(api, session);
        await reopened.ActivateAsync();
        Assert.Equal(1, reopened.Selected!.Pending);
        Assert.Equal(1, reopened.Selected.Restocked);
        Assert.Equal(1, reopened.Selected.Defective);
        Assert.False(RoleAccess.CanOpen(AppRoute.OrderReturns(api.OrderId), "WarehouseStaff"));
    }
}
