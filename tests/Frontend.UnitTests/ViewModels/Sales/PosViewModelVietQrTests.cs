using TechStore.Client.Models.Sales;
using TechStore.Client.Repositories.Sales;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Client.UnitTests.ViewModels.Orders;
using CatalogFactory = TechStore.Client.UnitTests.ViewModels.Orders.Catalog;
using TechStore.Client.ViewModels.Sales;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Sales;

internal sealed class FakeVietQrRepository : IVietQrRepository
{
    public List<CreateVietQrRequest> Created { get; } = [];
    public List<Guid> Cancelled { get; } = [];
    public List<(Guid Id, string Reference, decimal Amount)> Confirmed { get; } = [];
    public List<(Guid Id, string Note)> Resolved { get; } = [];
    public Dictionary<Guid, VietQrPayment> Payments { get; } = [];
    public List<VietQrBankEvent> Events { get; } = [];
    public Queue<Exception> CreateFailures { get; } = new();
    public Exception? ThrowOnGet { get; set; }
    public Exception? ThrowOnCancel { get; set; }
    public int Gets { get; private set; }
    public Func<VietQrPayment> OnCreate { get; set; } = null!;

    public Task<VietQrPayment> CreateAsync(CreateVietQrRequest request, CancellationToken cancellationToken = default)
    {
        Created.Add(request);
        if (CreateFailures.TryDequeue(out var failure)) return Task.FromException<VietQrPayment>(failure);
        var payment = OnCreate();
        Payments[payment.Id] = payment;
        return Task.FromResult(payment);
    }

    public Task<VietQrPayment> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Gets++;
        return ThrowOnGet is not null ? Task.FromException<VietQrPayment>(ThrowOnGet) : Task.FromResult(Payments[id]);
    }

    public Task<IReadOnlyList<VietQrPayment>> ListAsync(VietQrStatus? status, Guid? posSessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VietQrPayment>>(Payments.Values
            .Where(p => (status is null || p.Status == status) && (posSessionId is null || p.PosSessionId == posSessionId))
            .Select(p => p with { QrImagePng = null }).ToArray());

    public Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (ThrowOnCancel is not null) return Task.FromException(ThrowOnCancel);
        Cancelled.Add(id);
        Payments[id] = Payments[id] with { Status = VietQrStatus.Failed };
        return Task.CompletedTask;
    }

    public Task<VietQrPayment> ConfirmAsync(Guid id, string referenceCode, decimal receivedAmount, CancellationToken cancellationToken = default)
    {
        Confirmed.Add((id, referenceCode, receivedAmount));
        return Task.FromResult(Payments[id] = Payments[id] with { Status = VietQrStatus.Confirmed });
    }

    public Task<IReadOnlyList<VietQrBankEvent>> ListEventsAsync(VietQrEventStatus? status, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VietQrBankEvent>>(Events.Where(e => status is null || e.Status == status).ToArray());

    public Task<VietQrBankEvent> ResolveEventAsync(Guid id, string note, CancellationToken cancellationToken = default)
    {
        Resolved.Add((id, note));
        var index = Events.FindIndex(e => e.Id == id);
        return Task.FromResult(Events[index] = Events[index] with { Status = VietQrEventStatus.Resolved, ResolutionNote = note });
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;

    // The page loop waits on this clock; tests drive ticks by hand, so no timer may ever fire.
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new IdleTimer();

    private sealed class IdleTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal static class QrPayments
{
    public static readonly DateTime Start = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);

    public static VietQrPayment Pending(Guid? shiftId = null, decimal amount = 10_000_000m, TimeSpan? life = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), "ORD-202611-0042", VietQrStatus.Pending, amount, "TSABCDEFGH23", "970422", "0123456789",
        "TECHSTORE", "000201...6304ABCD", "iVBORw0KGgo=", Start, Start + (life ?? TimeSpan.FromMinutes(15)), shiftId, "Thu ngân", null);
}

public sealed class PosViewModelVietQrTests
{
    private sealed class Rig
    {
        public FakePosRepository Pos { get; } = new();
        public FakeDeviceRepository Devices { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public FakeVietQrRepository VietQr { get; } = new();
        public InMemorySessionStore Session { get; } = new();
        public ManualTimeProvider Time { get; } = new(new DateTimeOffset(QrPayments.Start));
        public PosDraftStore Drafts { get; }
        public PosViewModel ViewModel { get; private set; }

        public Rig()
        {
            Pos.Price = request =>
            {
                var subtotal = request.Items!.Sum(i => Products.Details[i.ProductId!.Value].BasePrice * i.Quantity!.Value);
                return Task.FromResult(new PosQuoteDto(subtotal, 0, 0, subtotal));
            };
            VietQr.OnCreate = () => QrPayments.Pending(Pos.Current?.Id);
            Drafts = new PosDraftStore(Session);
            ViewModel = New();
        }

        private PosViewModel New() => new(Pos, Devices, Products, Drafts, vietQr: VietQr, time: Time);

        public async Task<PosViewModel> Reopen()
        {
            ViewModel.Deactivate();
            ViewModel = New();
            await ViewModel.ActivateAsync();
            return ViewModel;
        }

        public async Task AddPhone(string imei = "358912345678900")
        {
            var product = Products.Add(CatalogFactory.Product("iPhone", serialTracked: true, basePrice: 10_000_000m));
            Devices.Scans[imei] = new ScannedDevice(CatalogFactory.Device(imei), product.Id, null);
            ViewModel.ScanText = imei;
            await ViewModel.ScanCommand.ExecuteAsync(null);
        }
    }

    private static async Task<Rig> ReadyRig()
    {
        var rig = new Rig();
        rig.Pos.Current = PosShifts.Open();
        await rig.ViewModel.ActivateAsync();
        await rig.AddPhone();
        rig.ViewModel.ChoosePaymentCommand.Execute("VietQR");
        await rig.ViewModel.RefreshQuoteCommand.ExecuteAsync(null);
        return rig;
    }

    [Fact]
    public async Task ChoosingVietQr_TurnsCashPaymentOffAndAllowsCreatingTheCode()
    {
        // Arrange
        var rig = await ReadyRig();

        // Assert
        Assert.True(rig.ViewModel.PayByQr);
        Assert.True(rig.ViewModel.CanCreateQr);
        rig.ViewModel.CashText = "20000000";
        Assert.False(rig.ViewModel.CanPay);

        // Act
        rig.ViewModel.ChoosePaymentCommand.Execute("Cash");

        // Assert
        Assert.False(rig.ViewModel.PayByQr);
        Assert.False(rig.ViewModel.CanCreateQr);
        Assert.True(rig.ViewModel.CanPay);
    }

    [Fact]
    public async Task CreateQr_SendsTheCartWithOneKeyAndLocksTheCart()
    {
        // Arrange
        var rig = await ReadyRig();

        // Act
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);

        // Assert
        var request = Assert.Single(rig.VietQr.Created);
        Assert.Equal(rig.Pos.Current!.Id, request.PosSessionId);
        Assert.NotNull(request.CheckoutKey);
        Assert.Equal(10_000_000m, request.ExpectedTotal);
        Assert.Single(request.Items!);
        Assert.True(rig.ViewModel.IsQrActive);
        Assert.False(rig.ViewModel.CanEditCart);
        Assert.False(rig.ViewModel.CanChoosePayment);
        Assert.False(rig.ViewModel.CanCreateQr);
        Assert.Equal("15:00", rig.ViewModel.QrCountdownText);
        Assert.Contains("10.000.000", rig.ViewModel.QrAmountText);
        rig.ViewModel.Deactivate();
    }

    [Fact]
    public async Task CreateQr_LostConnection_RetriesWithTheSameKeyAndANewKeyAfterARefusal()
    {
        // Arrange
        var rig = await ReadyRig();
        rig.VietQr.CreateFailures.Enqueue(new ApiException(ApiErrorKind.Network, "offline"));

        // Act: unknown outcome
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        Assert.False(rig.ViewModel.IsQrActive);
        Assert.True(rig.ViewModel.HasQrError);
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(2, rig.VietQr.Created.Count);
        Assert.Equal(rig.VietQr.Created[0].CheckoutKey, rig.VietQr.Created[1].CheckoutKey);
        Assert.True(rig.ViewModel.IsQrActive);
        rig.ViewModel.Deactivate();

        // Act: a refusal clears the key
        var other = await ReadyRig();
        other.VietQr.CreateFailures.Enqueue(RecordingApiClient.Http(409, "Het hang"));
        await other.ViewModel.CreateQrCommand.ExecuteAsync(null);
        other.VietQr.CreateFailures.Enqueue(new ApiException(ApiErrorKind.Network, "offline"));
        await other.ViewModel.CreateQrCommand.ExecuteAsync(null);
        Assert.NotEqual(other.VietQr.Created[0].CheckoutKey, other.VietQr.Created[1].CheckoutKey);
    }

    [Fact]
    public async Task CreateQr_BankNotConfigured_ExplainsAndStaysEditable()
    {
        // Arrange
        var rig = await ReadyRig();
        rig.VietQr.CreateFailures.Enqueue(RecordingApiClient.Http(503, "VietQR is not configured"));

        // Act
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("cấu hình", rig.ViewModel.QrError);
        Assert.True(rig.ViewModel.CanEditCart);
        Assert.False(rig.ViewModel.IsQrActive);
    }

    [Fact]
    public async Task Tick_PollsEveryThirdSecondAndUpdatesTheCountdown()
    {
        // Arrange
        var rig = await ReadyRig();
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        var before = rig.VietQr.Gets;

        // Act
        rig.Time.Advance(TimeSpan.FromSeconds(1));
        await rig.ViewModel.TickAsync();
        rig.Time.Advance(TimeSpan.FromSeconds(1));
        await rig.ViewModel.TickAsync();

        // Assert
        Assert.Equal(before, rig.VietQr.Gets);
        Assert.Equal("14:58", rig.ViewModel.QrCountdownText);

        // Act
        rig.Time.Advance(TimeSpan.FromSeconds(1));
        await rig.ViewModel.TickAsync();

        // Assert
        Assert.Equal(before + 1, rig.VietQr.Gets);
        rig.ViewModel.Deactivate();
    }

    [Fact]
    public async Task Poll_Confirmed_ShowsTheReceiptClearsTheCartAndRefreshesTheShift()
    {
        // Arrange
        var rig = await ReadyRig();
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        var id = rig.ViewModel.Qr!.Id;
        rig.VietQr.Payments[id] = rig.VietQr.Payments[id] with { Status = VietQrStatus.Confirmed };
        rig.Pos.Current = rig.Pos.Current! with { VietQrSalesTotal = 10_000_000m };

        // Act
        await rig.ViewModel.PollAsync();

        // Assert
        Assert.Null(rig.ViewModel.Qr);
        Assert.True(rig.ViewModel.HasReceipt);
        Assert.Contains("ORD-202611-0042", rig.ViewModel.ReceiptText);
        Assert.Contains("VietQR", rig.ViewModel.ReceiptText);
        Assert.Empty(rig.ViewModel.Lines);
        Assert.False(rig.ViewModel.PayByQr);
        Assert.Equal(10_000_000m, rig.ViewModel.Shift!.VietQrSalesTotal);
        Assert.Contains("10.000.000", rig.ViewModel.ShiftQrText);
        Assert.Empty(rig.Pos.Checkouts);
        rig.ViewModel.DismissReceiptCommand.Execute(null);
        Assert.False(rig.ViewModel.HasReceipt);
    }

    [Theory]
    [InlineData(VietQrStatus.Expired, "hết hạn")]
    [InlineData(VietQrStatus.Failed, "hủy")]
    public async Task Poll_ExpiredOrCancelled_UnlocksTheCartAndTheNextCodeUsesANewKey(VietQrStatus ended, string word)
    {
        // Arrange
        var rig = await ReadyRig();
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        var id = rig.ViewModel.Qr!.Id;
        rig.VietQr.Payments[id] = rig.VietQr.Payments[id] with { Status = ended };

        // Act
        await rig.ViewModel.PollAsync();

        // Assert
        Assert.Null(rig.ViewModel.Qr);
        Assert.Contains(word, rig.ViewModel.QrNotice);
        Assert.True(rig.ViewModel.CanEditCart);
        Assert.Single(rig.ViewModel.Lines);
        await rig.ViewModel.RefreshQuoteCommand.ExecuteAsync(null);
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        Assert.NotEqual(rig.VietQr.Created[0].CheckoutKey, rig.VietQr.Created[1].CheckoutKey);
        rig.ViewModel.Deactivate();
    }

    [Fact]
    public async Task Poll_NetworkError_KeepsTheRequestAndRecoversOnTheNextAnswer()
    {
        // Arrange
        var rig = await ReadyRig();
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        rig.VietQr.ThrowOnGet = new ApiException(ApiErrorKind.Network, "offline");

        // Act
        await rig.ViewModel.PollAsync();

        // Assert
        Assert.True(rig.ViewModel.IsQrActive);
        Assert.True(rig.ViewModel.HasQrError);

        // Act
        rig.VietQr.ThrowOnGet = null;
        await rig.ViewModel.PollAsync();

        // Assert
        Assert.True(rig.ViewModel.IsQrActive);
        Assert.False(rig.ViewModel.HasQrError);
        rig.ViewModel.Deactivate();
    }

    [Fact]
    public async Task Tick_PastTheDeadline_AsksTheServerAtOnceAndShowsExpired()
    {
        // Arrange
        var rig = await ReadyRig();
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        var id = rig.ViewModel.Qr!.Id;
        rig.VietQr.Payments[id] = rig.VietQr.Payments[id] with { Status = VietQrStatus.Expired };
        rig.Time.Advance(TimeSpan.FromMinutes(16));

        // Act
        await rig.ViewModel.TickAsync();

        // Assert
        Assert.Null(rig.ViewModel.Qr);
        Assert.Contains("hết hạn", rig.ViewModel.QrNotice);
        Assert.Equal(string.Empty, rig.ViewModel.QrCountdownText);
    }

    [Fact]
    public async Task CancelQr_CancelsOnTheServerAndUnlocksTheCart()
    {
        // Arrange
        var rig = await ReadyRig();
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        var id = rig.ViewModel.Qr!.Id;

        // Act
        await rig.ViewModel.CancelQrCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal([id], rig.VietQr.Cancelled);
        Assert.False(rig.ViewModel.IsQrActive);
        Assert.True(rig.ViewModel.CanEditCart);
        Assert.Contains("hủy", rig.ViewModel.QrNotice);
    }

    [Fact]
    public async Task CancelQr_WhenThePaymentJustArrived_ShowsTheSaleInsteadOfCancelling()
    {
        // Arrange
        var rig = await ReadyRig();
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        var id = rig.ViewModel.Qr!.Id;
        rig.VietQr.ThrowOnCancel = RecordingApiClient.Http(409, "Đã thanh toán");
        rig.VietQr.Payments[id] = rig.VietQr.Payments[id] with { Status = VietQrStatus.Confirmed };

        // Act
        await rig.ViewModel.CancelQrCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(rig.ViewModel.Qr);
        Assert.True(rig.ViewModel.HasReceipt);
        Assert.Empty(rig.ViewModel.Lines);
    }

    [Fact]
    public async Task LeavingThePage_StopsPollingAndComingBackResumesTheSameRequest()
    {
        // Arrange
        var rig = await ReadyRig();
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        var id = rig.ViewModel.Qr!.Id;

        // Act
        var next = await rig.Reopen();

        // Assert
        Assert.Equal(id, next.Qr!.Id);
        Assert.True(next.PayByQr);
        Assert.Single(next.Lines);
        Assert.False(next.CanEditCart);
        next.Deactivate();
    }

    [Fact]
    public async Task ComingBack_AfterThePaymentArrived_ShowsTheReceiptNotTheOldCart()
    {
        // Arrange
        var rig = await ReadyRig();
        await rig.ViewModel.CreateQrCommand.ExecuteAsync(null);
        var id = rig.ViewModel.Qr!.Id;
        rig.VietQr.Payments[id] = rig.VietQr.Payments[id] with { Status = VietQrStatus.Confirmed };

        // Act
        var next = await rig.Reopen();

        // Assert
        Assert.Null(next.Qr);
        Assert.True(next.HasReceipt);
        Assert.Empty(next.Lines);
    }

    [Fact]
    public async Task Opening_WithoutADraft_ResumesTheServersPendingRequestOfThisShift()
    {
        // Arrange
        var rig = new Rig();
        rig.Pos.Current = PosShifts.Open();
        var pending = QrPayments.Pending(rig.Pos.Current.Id);
        rig.VietQr.Payments[pending.Id] = pending;

        // Act
        await rig.ViewModel.ActivateAsync();

        // Assert
        Assert.Equal(pending.Id, rig.ViewModel.Qr!.Id);
        Assert.Equal("iVBORw0KGgo=", rig.ViewModel.Qr.QrImagePng);
        Assert.True(rig.ViewModel.IsQrActive);
        rig.ViewModel.Deactivate();
    }

    [Fact]
    public async Task Opening_WithAnotherShiftsRequest_DoesNotShowIt()
    {
        // Arrange
        var rig = new Rig();
        rig.Pos.Current = PosShifts.Open();
        var other = QrPayments.Pending(Guid.NewGuid());
        rig.VietQr.Payments[other.Id] = other;

        // Act
        await rig.ViewModel.ActivateAsync();

        // Assert
        Assert.Null(rig.ViewModel.Qr);
    }
}
