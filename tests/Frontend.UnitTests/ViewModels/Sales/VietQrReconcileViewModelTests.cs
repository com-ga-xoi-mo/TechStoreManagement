using TechStore.Client.Models.Sales;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Client.UnitTests.ViewModels.Orders;
using TechStore.Client.ViewModels.Sales;
using TechStore.Shared.Enums;

namespace TechStore.Client.UnitTests.ViewModels.Sales;

public sealed class VietQrReconcileViewModelTests
{
    private static VietQrBankEvent Event(VietQrEventReason reason = VietQrEventReason.LateAfterExpiry, VietQrEventStatus status = VietQrEventStatus.NeedsReconciliation) => new(
        Guid.NewGuid(), "sandbox", "FT" + Guid.NewGuid().ToString("N")[..6], 11_000_000m, "CK TSABCDEFGH23", "0123456789",
        QrPayments.Start, "ORD-202611-0042", status, reason, null, null);

    private static async Task<(VietQrReconcileViewModel ViewModel, FakeVietQrRepository Repository, FakeNavigation Navigation)> OpenAsync(
        int pending = 1, int events = 1)
    {
        var repository = new FakeVietQrRepository();
        for (var i = 0; i < pending; i++)
        {
            var payment = QrPayments.Pending(Guid.NewGuid(), 11_000_000m);
            repository.Payments[payment.Id] = payment;
        }
        for (var i = 0; i < events; i++) repository.Events.Add(Event());
        var navigation = new FakeNavigation();
        var viewModel = new VietQrReconcileViewModel(repository, navigation);
        await viewModel.ActivateAsync();
        return (viewModel, repository, navigation);
    }

    [Fact]
    public async Task Load_ShowsOnlyPendingRequestsAndOpenEvents()
    {
        // Arrange
        var repository = new FakeVietQrRepository();
        var open = QrPayments.Pending();
        var done = QrPayments.Pending() with { Status = VietQrStatus.Confirmed };
        repository.Payments[open.Id] = open;
        repository.Payments[done.Id] = done;
        repository.Events.Add(Event());
        repository.Events.Add(Event(status: VietQrEventStatus.Resolved));
        var viewModel = new VietQrReconcileViewModel(repository, new FakeNavigation());

        // Act
        await viewModel.ActivateAsync();

        // Assert
        Assert.Equal([open.Id], viewModel.Pending.Select(p => p.Id));
        Assert.Single(viewModel.Events);
        Assert.True(viewModel.HasPending);
        Assert.True(viewModel.HasEvents);
        Assert.False(viewModel.HasNoPending);
    }

    [Fact]
    public async Task Load_NothingToDo_ShowsBothEmptyStates()
    {
        // Act
        var (viewModel, _, _) = await OpenAsync(pending: 0, events: 0);

        // Assert
        Assert.True(viewModel.HasNoPending);
        Assert.True(viewModel.HasNoEvents);
    }

    [Fact]
    public async Task SelectPayment_PrefillsTheAmountAndNeedsAReferenceBeforeConfirming()
    {
        // Arrange
        var (viewModel, _, _) = await OpenAsync();
        var payment = viewModel.Pending[0];

        // Act
        viewModel.SelectPaymentCommand.Execute(payment);

        // Assert
        Assert.True(viewModel.HasSelectedPayment);
        Assert.Equal("11000000", viewModel.AmountText);
        Assert.Contains("ORD-202611-0042", viewModel.SelectedPaymentText);
        Assert.False(viewModel.CanConfirm);

        // Act
        viewModel.ReferenceText = "  FT26112500123 ";

        // Assert
        Assert.True(viewModel.CanConfirm);
        viewModel.AmountText = "abc";
        Assert.False(viewModel.CanConfirm);
        viewModel.AmountText = "0";
        Assert.False(viewModel.CanConfirm);
    }

    [Fact]
    public async Task ConfirmPayment_SendsTheReferenceAndAmountThenRemovesTheRow()
    {
        // Arrange
        var (viewModel, repository, _) = await OpenAsync(events: 0);
        var payment = viewModel.Pending[0];
        viewModel.SelectPaymentCommand.Execute(payment);
        viewModel.ReferenceText = "FT26112500123";

        // Act
        await viewModel.ConfirmPaymentCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal([(payment.Id, "FT26112500123", 11_000_000m)], repository.Confirmed);
        Assert.Empty(viewModel.Pending);
        Assert.False(viewModel.HasSelectedPayment);
        Assert.Contains("ORD-202611-0042", viewModel.ActionMessage);
        Assert.False(viewModel.HasActionError);
    }

    [Fact]
    public async Task ConfirmPayment_ServerRefuses_ShowsTheErrorAndKeepsTheSelection()
    {
        // Arrange
        var (viewModel, repository, _) = await OpenAsync();
        viewModel.SelectPaymentCommand.Execute(viewModel.Pending[0]);
        viewModel.ReferenceText = "FT1";
        var failing = new ThrowingConfirm(repository, RecordingApiClient.Http(409, "Mã tham chiếu đã được dùng."));
        var guarded = new VietQrReconcileViewModel(failing, new FakeNavigation());
        await guarded.ActivateAsync();
        guarded.SelectPaymentCommand.Execute(guarded.Pending[0]);
        guarded.ReferenceText = "FT1";

        // Act
        await guarded.ConfirmPaymentCommand.ExecuteAsync(null);

        // Assert
        Assert.True(guarded.HasActionError);
        Assert.True(guarded.HasSelectedPayment);
        Assert.Single(guarded.Pending);
        Assert.False(guarded.IsBusy);
    }

    [Fact]
    public async Task SelectEvent_NeedsANoteBeforeResolving()
    {
        // Arrange
        var (viewModel, _, _) = await OpenAsync();
        viewModel.SelectEventCommand.Execute(viewModel.Events[0]);

        // Assert
        Assert.True(viewModel.HasSelectedEvent);
        Assert.False(viewModel.CanResolve);

        // Act
        viewModel.NoteText = "   ";
        Assert.False(viewModel.CanResolve);
        viewModel.NoteText = "Đã hoàn tiền cho khách";

        // Assert
        Assert.True(viewModel.CanResolve);
        viewModel.NoteText = new string('x', VietQrReconcileViewModel.MaxNoteLength + 1);
        Assert.False(viewModel.CanResolve);
    }

    [Fact]
    public async Task ResolveEvent_SendsTheNoteThenRemovesTheRow()
    {
        // Arrange
        var (viewModel, repository, _) = await OpenAsync(pending: 0);
        var bankEvent = viewModel.Events[0];
        viewModel.SelectEventCommand.Execute(bankEvent);
        viewModel.NoteText = " Đã hoàn tiền ";

        // Act
        await viewModel.ResolveEventCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal([(bankEvent.Id, "Đã hoàn tiền")], repository.Resolved);
        Assert.Empty(viewModel.Events);
        Assert.False(viewModel.HasSelectedEvent);
        Assert.Contains(bankEvent.ProviderTransactionId, viewModel.ActionMessage);
    }

    [Fact]
    public async Task SelectCommands_IgnoreAnythingThatIsNotARow()
    {
        // Arrange
        var (viewModel, _, _) = await OpenAsync();

        // Act
        viewModel.SelectPaymentCommand.Execute(viewModel);
        viewModel.SelectEventCommand.Execute(null);

        // Assert
        Assert.False(viewModel.HasSelectedPayment);
        Assert.False(viewModel.HasSelectedEvent);
    }

    [Fact]
    public async Task Back_GoesToTheOrdersList()
    {
        // Arrange
        var (viewModel, _, navigation) = await OpenAsync();

        // Act
        viewModel.BackCommand.Execute(null);

        // Assert
        Assert.Equal(AppRoute.Root(ShellDestination.Orders), Assert.Single(navigation.Routes));
    }

    private sealed class ThrowingConfirm(FakeVietQrRepository inner, Exception failure) : TechStore.Client.Repositories.Sales.IVietQrRepository
    {
        public Task<VietQrPayment> CreateAsync(TechStore.Shared.Requests.CreateVietQrRequest request, CancellationToken cancellationToken = default) => inner.CreateAsync(request, cancellationToken);
        public Task<VietQrPayment> GetAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetAsync(id, cancellationToken);
        public Task<IReadOnlyList<VietQrPayment>> ListAsync(VietQrStatus? status, Guid? posSessionId, CancellationToken cancellationToken = default) => inner.ListAsync(status, posSessionId, cancellationToken);
        public Task CancelAsync(Guid id, CancellationToken cancellationToken = default) => inner.CancelAsync(id, cancellationToken);
        public Task<VietQrPayment> ConfirmAsync(Guid id, string referenceCode, decimal receivedAmount, CancellationToken cancellationToken = default) => Task.FromException<VietQrPayment>(failure);
        public Task<IReadOnlyList<VietQrBankEvent>> ListEventsAsync(VietQrEventStatus? status, CancellationToken cancellationToken = default) => inner.ListEventsAsync(status, cancellationToken);
        public Task<VietQrBankEvent> ResolveEventAsync(Guid id, string note, CancellationToken cancellationToken = default) => inner.ResolveEventAsync(id, note, cancellationToken);
    }
}

public sealed class VietQrReconcileRouteTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Manager", true)]
    [InlineData("SalesStaff", false)]
    [InlineData("WarehouseStaff", false)]
    [InlineData(null, false)]
    public void ReconcileRoute_FollowsTheServerPermission(string? role, bool expected)
    {
        // Act and Assert
        Assert.Equal(expected, RoleAccess.CanReconcileVietQr(role));
        Assert.Equal(expected, RoleAccess.CanOpen(AppRoute.VietQrReconcile(), role));
    }

    [Fact]
    public void ReconcileRoute_StaysUnderOrdersAndAddsNoMenuEntry()
    {
        // Act
        var route = AppRoute.VietQrReconcile();

        // Assert
        Assert.Equal(ShellDestination.Orders, route.Destination);
        Assert.Equal(AppRouteKind.VietQrReconcile, route.Kind);
        Assert.NotEqual(AppRoute.Pos(), route);
        Assert.DoesNotContain(ShellDestinations.All, item => item.Title.Contains("VietQR"));
    }
}
