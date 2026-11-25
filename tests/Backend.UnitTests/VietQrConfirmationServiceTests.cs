using FluentAssertions;
using Microsoft.Extensions.Options;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class VietQrConfirmationServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
    private static readonly AuthenticatedUser SalesA = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), RoleType.SalesStaff, 0);
    private static readonly AuthenticatedUser Manager = new(Guid.Parse("cccccccc-0000-0000-0000-000000000003"), RoleType.Manager, 0);

    private readonly FakeOrderRepository _orders = new();
    private readonly FakePosSessionRepository _sessions = new();
    private readonly FakeVietQrHoldRepository _requests;
    private readonly FakeVietQrHoldService _holds;
    private readonly FakeInventoryFulfillmentService _fulfillment = new();
    private readonly FakeVoucherService _vouchers = new();
    private readonly FakeRefundLoyalty _loyalty = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly VietQrConfirmationService _service;
    private readonly PosSession _shift;
    private readonly Order _order;
    private readonly VietQrTransaction _qr;

    public VietQrConfirmationServiceTests()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(Now));
        _requests = new FakeVietQrHoldRepository(_orders);
        _holds = new FakeVietQrHoldService(_requests);
        var settlement = new SaleSettlementService(_fulfillment, _vouchers, _loyalty);
        var bank = new VietQrOptions { BankBin = "970422", AccountNumber = "0123456789", AccountName = "CUA HANG", WebhookSecret = "s" };
        _service = new VietQrConfirmationService(_requests, _sessions, _orders, _holds, settlement, _unitOfWork, Options.Create(bank), time);

        _shift = _sessions.AddSession(SalesA.Id, opening: 1_000_000m);
        _order = NewOrder(10_000_000m);
        _order.PosSessionId = _shift.Id;
        _orders.Orders[_order.Id] = _order;
        _qr = _requests.AddRequest(_order, _shift.Id, expiresAt: Now.AddMinutes(10));
        _qr.TransactionCode = "TSABCD234567";
    }

    // ---------------------------------------------------------------- manual

    [Fact]
    public async Task ConfirmManualAsync_MatchingAmount_CompletesTheSaleAndCreditsTheQrTotal()
    {
        // Act
        var result = await _service.ConfirmManualAsync(_qr.Id, Confirm("FT26112500123", 10_000_000m), Manager);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Result.Should().Be("Applied");
        _qr.Status.Should().Be(VietQrStatus.Confirmed);
        _qr.Provider.Should().Be("manual");
        _qr.ProviderTransactionId.Should().Be("FT26112500123");
        _qr.ConfirmedByUserId.Should().Be(Manager.Id);
        _qr.ConfirmedAt.Should().Be(Now);
        _order.Status.Should().Be(OrderStatus.Completed);
        _order.PaidAt.Should().Be(Now);
        _shift.VietQrSalesTotal.Should().Be(10_000_000m);
        _shift.CashSalesTotal.Should().Be(0m);
        _fulfillment.ConfirmCallCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
        _requests.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmManualAsync_WrongAmount_ReturnsValidationErrorAndChangesNothing()
    {
        // Act
        var result = await _service.ConfirmManualAsync(_qr.Id, Confirm("FT1", 9_000_000m), Manager);

        // Assert
        result.Kind.Should().Be(PosResultKind.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("receivedAmount");
        _qr.Status.Should().Be(VietQrStatus.Pending);
        _order.Status.Should().Be(OrderStatus.Pending);
        _shift.VietQrSalesTotal.Should().Be(0m);
        _requests.Events.Should().BeEmpty();
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null, 1000)]
    [InlineData("  ", 1000)]
    [InlineData("FT1", null)]
    [InlineData("FT1", 0)]
    public async Task ConfirmManualAsync_MissingReferenceOrAmount_ReturnsValidationError(string? reference, int? amount)
    {
        // Act
        var result = await _service.ConfirmManualAsync(_qr.Id, new ConfirmVietQrRequest { ReferenceCode = reference, ReceivedAmount = amount }, Manager);

        // Assert
        result.Kind.Should().Be(PosResultKind.ValidationFailed);
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task ConfirmManualAsync_UnknownRequest_ReturnsNotFound()
    {
        // Act
        var result = await _service.ConfirmManualAsync(Guid.NewGuid(), Confirm("FT1", 1m), Manager);

        // Assert
        result.Kind.Should().Be(PosResultKind.NotFound);
    }

    [Fact]
    public async Task ConfirmManualAsync_ReferenceAlreadyUsedByAnotherRequest_ReturnsConflictAndChangesNothing()
    {
        // Arrange
        var other = _requests.AddRequest(NewOrder(1_000_000m), _shift.Id, VietQrStatus.Confirmed);
        other.Provider = "manual";
        other.ProviderTransactionId = "FT1";

        // Act
        var result = await _service.ConfirmManualAsync(_qr.Id, Confirm("FT1", 10_000_000m), Manager);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        _qr.Status.Should().Be(VietQrStatus.Pending);
        _shift.VietQrSalesTotal.Should().Be(0m);
        _requests.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmManualAsync_SameReferenceTwice_IsAReplayThatSellsOnce()
    {
        // Arrange
        await _service.ConfirmManualAsync(_qr.Id, Confirm("FT1", 10_000_000m), Manager);

        // Act
        var again = await _service.ConfirmManualAsync(_qr.Id, Confirm("FT1", 10_000_000m), Manager);

        // Assert
        again.IsSuccess.Should().BeTrue();
        again.Data!.Result.Should().Be("Replay");
        _fulfillment.ConfirmCallCount.Should().Be(1);
        _shift.VietQrSalesTotal.Should().Be(10_000_000m);
    }

    [Fact]
    public async Task ConfirmManualAsync_AlreadyPaidWithAnotherReference_ReturnsConflict()
    {
        // Arrange
        await _service.ConfirmManualAsync(_qr.Id, Confirm("FT1", 10_000_000m), Manager);

        // Act
        var result = await _service.ConfirmManualAsync(_qr.Id, Confirm("FT2", 10_000_000m), Manager);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        _fulfillment.ConfirmCallCount.Should().Be(1);
        _shift.VietQrSalesTotal.Should().Be(10_000_000m);
    }

    [Fact]
    public async Task ConfirmManualAsync_RequestAlreadyExpired_KeepsTheMoneyAsAnEventAndDoesNotSell()
    {
        // Arrange
        _qr.Status = VietQrStatus.Expired;
        _order.Status = OrderStatus.Cancelled;

        // Act
        var result = await _service.ConfirmManualAsync(_qr.Id, Confirm("FT1", 10_000_000m), Manager);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        var stored = _requests.Events.Should().ContainSingle().Subject;
        stored.Reason.Should().Be(VietQrEventReason.LateAfterExpiry);
        stored.Status.Should().Be(VietQrEventStatus.NeedsReconciliation);
        stored.Provider.Should().Be("manual");
        stored.Amount.Should().Be(10_000_000m);
        _order.Status.Should().Be(OrderStatus.Cancelled);
        _fulfillment.ConfirmCallCount.Should().Be(0);
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task ConfirmManualAsync_PendingPastItsDeadline_ExpiresItFirstThenKeepsTheMoney()
    {
        // Arrange
        _qr.ExpiresAt = Now.AddSeconds(-5);

        // Act
        var result = await _service.ConfirmManualAsync(_qr.Id, Confirm("FT1", 10_000_000m), Manager);

        // Assert
        _holds.Expired.Should().Equal(_qr.Id);
        result.Kind.Should().Be(PosResultKind.Conflict);
        _requests.Events.Should().ContainSingle().Which.Reason.Should().Be(VietQrEventReason.LateAfterExpiry);
        _order.Status.Should().Be(OrderStatus.Cancelled);
        _shift.VietQrSalesTotal.Should().Be(0m);
    }

    [Fact]
    public async Task ConfirmManualAsync_SettlementRefused_ReturnsConflictAndKeepsTheRequestWaiting()
    {
        // Arrange
        _fulfillment.ConfirmConflict = new FulfillmentProblem(0, "Device is not reserved.");

        // Act
        var result = await _service.ConfirmManualAsync(_qr.Id, Confirm("FT1", 10_000_000m), Manager);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        _qr.Status.Should().Be(VietQrStatus.Pending);
        _qr.ProviderTransactionId.Should().BeNull();
        _shift.VietQrSalesTotal.Should().Be(0m);
        _requests.Events.Should().BeEmpty();
        _unitOfWork.CommitCount.Should().Be(0);
    }

    // --------------------------------------------------------------- webhook

    [Fact]
    public async Task ReceiveSandboxAsync_MatchingNotice_CompletesTheSaleAndStoresAnAppliedEvent()
    {
        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m, "CK TSABCD234567 thanh toan"));

        // Assert
        result.Data!.Result.Should().Be("Applied");
        result.Data.Payment!.Status.Should().Be("Confirmed");
        _qr.Provider.Should().Be("sandbox");
        _qr.ProviderTransactionId.Should().Be("FT100");
        _qr.ConfirmedByUserId.Should().BeNull();
        _order.Status.Should().Be(OrderStatus.Completed);
        _shift.VietQrSalesTotal.Should().Be(10_000_000m);
        var applied = _requests.Events.Should().ContainSingle().Subject;
        applied.Status.Should().Be(VietQrEventStatus.Applied);
        applied.Reason.Should().Be(VietQrEventReason.Applied);
        applied.VietQrTransactionId.Should().Be(_qr.Id);
        applied.Payload.Should().Contain("FT100");
    }

    [Fact]
    public async Task ReceiveSandboxAsync_TransferCodeInLowerCase_StillMatches()
    {
        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m, "tsabcd234567"));

        // Assert
        result.Data!.Result.Should().Be("Applied");
    }

    [Fact]
    public async Task ReceiveSandboxAsync_SameNoticeTwice_SellsOnceAndStoresOneEvent()
    {
        // Arrange
        await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m));

        // Act
        var again = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m));

        // Assert
        again.IsSuccess.Should().BeTrue();
        again.Data!.Result.Should().Be("Duplicate");
        again.Data.Payment!.Status.Should().Be("Confirmed");
        _requests.Events.Should().ContainSingle();
        _fulfillment.ConfirmCallCount.Should().Be(1);
        _shift.VietQrSalesTotal.Should().Be(10_000_000m);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_WrongAmount_KeepsTheMoneyAsAnEventAndLeavesTheRequestWaiting()
    {
        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 9_000_000m));

        // Assert
        result.Data!.Result.Should().Be("Recorded");
        result.Data.Reason.Should().Be(VietQrEventReason.AmountMismatch);
        _qr.Status.Should().Be(VietQrStatus.Pending);
        _order.Status.Should().Be(OrderStatus.Pending);
        _requests.Events.Should().ContainSingle().Which.Status.Should().Be(VietQrEventStatus.NeedsReconciliation);
        _shift.VietQrSalesTotal.Should().Be(0m);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_WrongAccount_IsRecordedAsAccountMismatch()
    {
        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m, account: "9999999999"));

        // Assert
        result.Data!.Reason.Should().Be(VietQrEventReason.AccountMismatch);
        _qr.Status.Should().Be(VietQrStatus.Pending);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_UnknownTransferCode_IsRecordedAsUnmatched()
    {
        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m, "TSZZZZZZZZZZ"));

        // Assert
        result.Data!.Reason.Should().Be(VietQrEventReason.Unmatched);
        var stored = _requests.Events.Should().ContainSingle().Subject;
        stored.VietQrTransactionId.Should().BeNull();
        _qr.Status.Should().Be(VietQrStatus.Pending);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_ContentWithoutAnyTransferCode_IsRecordedAsUnmatched()
    {
        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m, "chuyen tien mua may"));

        // Assert
        result.Data!.Reason.Should().Be(VietQrEventReason.Unmatched);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_AfterExpiry_KeepsTheMoneyAndDoesNotCompleteTheOrder()
    {
        // Arrange
        _qr.Status = VietQrStatus.Expired;
        _order.Status = OrderStatus.Cancelled;

        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m));

        // Assert
        result.Data!.Reason.Should().Be(VietQrEventReason.LateAfterExpiry);
        _order.Status.Should().Be(OrderStatus.Cancelled);
        _shift.VietQrSalesTotal.Should().Be(0m);
        _fulfillment.ConfirmCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_AfterCancel_IsRecordedAsLateAfterCancel()
    {
        // Arrange
        _qr.Status = VietQrStatus.Failed;
        _order.Status = OrderStatus.Cancelled;

        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m));

        // Assert
        result.Data!.Reason.Should().Be(VietQrEventReason.LateAfterCancel);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_AfterTheShiftClosed_NeverChangesTheClosedShift()
    {
        // Arrange
        _shift.Status = PosSessionStatus.Closed;

        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m));

        // Assert
        result.Data!.Reason.Should().Be(VietQrEventReason.LateAfterShiftClose);
        _shift.VietQrSalesTotal.Should().Be(0m);
        _order.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_PendingPastItsDeadline_IsExpiredThenRecordedAsLate()
    {
        // Arrange
        _qr.ExpiresAt = Now.AddSeconds(-1);

        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m));

        // Assert
        _holds.Expired.Should().Equal(_qr.Id);
        result.Data!.Reason.Should().Be(VietQrEventReason.LateAfterExpiry);
        _order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_SecondTransferForAnAlreadyPaidOrder_IsKeptForReconciliation()
    {
        // Arrange
        await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m));

        // Act
        var second = await _service.ReceiveSandboxAsync(Notice("FT200", 10_000_000m));

        // Assert
        second.Data!.Reason.Should().Be(VietQrEventReason.DuplicateReference);
        _fulfillment.ConfirmCallCount.Should().Be(1);
        _shift.VietQrSalesTotal.Should().Be(10_000_000m);
        _requests.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_SettlementRefused_KeepsTheMoneyAndTheRequestStaysWaiting()
    {
        // Arrange
        _fulfillment.ConfirmConflict = new FulfillmentProblem(0, "Device is not reserved.");

        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m));

        // Assert
        result.Data!.Result.Should().Be("Recorded");
        result.Data.Reason.Should().Be(VietQrEventReason.SettlementFailed);
        _qr.Status.Should().Be(VietQrStatus.Pending);
        _order.Status.Should().Be(OrderStatus.Pending);
        _shift.VietQrSalesTotal.Should().Be(0m);
        _requests.Events.Should().ContainSingle().Which.Status.Should().Be(VietQrEventStatus.NeedsReconciliation);
    }

    [Fact]
    public async Task ReceiveSandboxAsync_ConcurrentDuplicateLosesTheEventIndexRace_ReturnsTheStoredAnswer()
    {
        // Arrange: the other request committed its event between our check and our save
        _unitOfWork.ExceptionToThrowOnSave = new DuplicateKeyException("uq_vietqr_events_provider_transaction");
        _unitOfWork.OnRollback = () =>
        {
            _unitOfWork.ExceptionToThrowOnSave = null;
            if (!_requests.Events.Any())
                _requests.Events.Add(new VietQrBankEvent
                {
                    Provider = "sandbox", ProviderTransactionId = "FT100", Status = VietQrEventStatus.Applied,
                    Reason = VietQrEventReason.Applied, VietQrTransactionId = _qr.Id
                });
        };

        // Act
        var result = await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Result.Should().Be("Duplicate");
    }

    // ---------------------------------------------------------------- events

    [Fact]
    public async Task SearchEventsAsync_FiltersByStatusNewestFirst()
    {
        // Arrange
        await _service.ReceiveSandboxAsync(Notice("FT1", 1m));
        await _service.ReceiveSandboxAsync(Notice("FT2", 10_000_000m));

        // Act
        var open = await _service.SearchEventsAsync(new VietQrEventSearchRequest { Status = "NeedsReconciliation" });
        var applied = await _service.SearchEventsAsync(new VietQrEventSearchRequest { Status = "Applied" });
        var bad = await _service.SearchEventsAsync(new VietQrEventSearchRequest { Status = "Open" });

        // Assert
        open.Data!.TotalCount.Should().Be(1);
        applied.Data!.TotalCount.Should().Be(1);
        bad.ValidationErrors.Should().ContainKey("status");
    }

    [Fact]
    public async Task ResolveEventAsync_WithANote_RecordsWhoWhenAndWhy()
    {
        // Arrange
        await _service.ReceiveSandboxAsync(Notice("FT1", 1m));
        var id = _requests.Events.Single().Id;

        // Act
        var result = await _service.ResolveEventAsync(id, new ResolveVietQrEventRequest { Note = "  Da hoan tien mat cho khach  " }, Manager);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be("Resolved");
        result.Data.ResolutionNote.Should().Be("Da hoan tien mat cho khach");
        result.Data.ResolvedByUserId.Should().Be(Manager.Id);
        result.Data.ResolvedAt.Should().Be(Now);
    }

    [Fact]
    public async Task ResolveEventAsync_Twice_KeepsTheFirstResolution()
    {
        // Arrange
        await _service.ReceiveSandboxAsync(Notice("FT1", 1m));
        var id = _requests.Events.Single().Id;
        await _service.ResolveEventAsync(id, new ResolveVietQrEventRequest { Note = "first" }, Manager);
        var other = new AuthenticatedUser(Guid.NewGuid(), RoleType.Admin, 0);

        // Act
        var second = await _service.ResolveEventAsync(id, new ResolveVietQrEventRequest { Note = "second" }, other);

        // Assert
        second.IsSuccess.Should().BeTrue();
        second.Data!.ResolutionNote.Should().Be("first");
        second.Data.ResolvedByUserId.Should().Be(Manager.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ResolveEventAsync_WithoutANote_ReturnsValidationError(string? note)
    {
        // Act
        var result = await _service.ResolveEventAsync(Guid.NewGuid(), new ResolveVietQrEventRequest { Note = note }, Manager);

        // Assert
        result.ValidationErrors.Should().ContainKey("note");
    }

    [Fact]
    public async Task ResolveEventAsync_NoteLongerThan500_ReturnsValidationError()
    {
        // Act
        var result = await _service.ResolveEventAsync(Guid.NewGuid(), new ResolveVietQrEventRequest { Note = new string('x', 501) }, Manager);

        // Assert
        result.ValidationErrors.Should().ContainKey("note");
    }

    [Fact]
    public async Task ResolveEventAsync_AppliedEvent_IsRefused()
    {
        // Arrange
        await _service.ReceiveSandboxAsync(Notice("FT100", 10_000_000m));
        var id = _requests.Events.Single().Id;

        // Act
        var result = await _service.ResolveEventAsync(id, new ResolveVietQrEventRequest { Note = "x" }, Manager);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
    }

    [Fact]
    public async Task ResolveEventAsync_UnknownEvent_ReturnsNotFound()
    {
        // Act
        var result = await _service.ResolveEventAsync(Guid.NewGuid(), new ResolveVietQrEventRequest { Note = "x" }, Manager);

        // Assert
        result.Kind.Should().Be(PosResultKind.NotFound);
    }

    private static ConfirmVietQrRequest Confirm(string reference, decimal amount) =>
        new() { ReferenceCode = reference, ReceivedAmount = amount };

    private static VietQrBankNotice Notice(string id, decimal amount, string content = "TSABCD234567", string account = "0123456789") =>
        new(id, amount, content, account, Now.AddSeconds(-30), $"{{\"transactionId\":\"{id}\"}}");

    private static Order NewOrder(decimal total)
    {
        var order = new Order
        {
            OrderCode = "ORD-" + Guid.NewGuid().ToString("N")[..6],
            CashierUserId = SalesA.Id,
            PaymentMethod = PaymentMethod.VietQr,
            Subtotal = total,
            TotalAmount = total,
            Status = OrderStatus.Pending
        };
        order.Items.Add(new OrderItem
        {
            OrderId = order.Id, ProductId = Guid.NewGuid(), ProductName = "iPhone", Sku = "IP15", Quantity = 1, UnitPrice = total, TotalPrice = total
        });
        return order;
    }
}
