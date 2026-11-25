using FluentAssertions;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Services;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class PosShiftServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
    private static readonly AuthenticatedUser SalesA = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), RoleType.SalesStaff, 0);
    private static readonly AuthenticatedUser SalesB = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"), RoleType.SalesStaff, 0);
    private static readonly AuthenticatedUser Manager = new(Guid.Parse("cccccccc-0000-0000-0000-000000000003"), RoleType.Manager, 0);

    private readonly FakePosSessionRepository _sessions = new();
    private readonly FakeVietQrHoldRepository _vietQr = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly PosShiftService _service;

    public PosShiftServiceTests()
    {
        _service = new PosShiftService(_sessions, _vietQr, _unitOfWork, new FixedTimeProvider(new DateTimeOffset(Now)));
    }

    [Fact]
    public async Task OpenAsync_NoOpenShift_CreatesOpenShiftWithOpeningBalance()
    {
        // Arrange
        var request = new OpenShiftRequest { OpeningBalance = 2_000_000m };

        // Act
        var result = await _service.OpenAsync(request, SalesA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be("Open");
        result.Data.OpeningBalance.Should().Be(2_000_000m);
        result.Data.CashSalesTotal.Should().Be(0m);
        result.Data.CashierUserId.Should().Be(SalesA.Id);
        result.Data.OpenedAt.Should().Be(Now);
        _sessions.AddedSessions.Should().ContainSingle();
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task OpenAsync_AlreadyHasOpenShift_ReturnsConflict()
    {
        // Arrange
        _sessions.AddSession(SalesA.Id);

        // Act
        var result = await _service.OpenAsync(new OpenShiftRequest { OpeningBalance = 0m }, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        _sessions.AddedSessions.Should().BeEmpty();
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task OpenAsync_SimultaneousRequestWonTheIndexRace_ReturnsConflictAndRollsBack()
    {
        // Arrange
        _unitOfWork.ExceptionToThrowOnSave = new DuplicateKeyException("uq_pos_sessions_cashier_open");

        // Act
        var result = await _service.OpenAsync(new OpenShiftRequest { OpeningBalance = 0m }, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(10.123)]
    public async Task OpenAsync_InvalidOpeningBalance_ReturnsValidationErrorAndStoresNothing(double value)
    {
        // Arrange
        var request = new OpenShiftRequest { OpeningBalance = (decimal)value };

        // Act
        var result = await _service.OpenAsync(request, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("openingBalance");
        _sessions.AddedSessions.Should().BeEmpty();
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task OpenAsync_MissingOpeningBalance_ReturnsValidationError()
    {
        // Act
        var result = await _service.OpenAsync(new OpenShiftRequest(), SalesA);

        // Assert
        result.ValidationErrors.Should().ContainKey("openingBalance");
        _sessions.AddedSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrentAsync_WithoutOpenShift_ReturnsNotFound()
    {
        // Act
        var result = await _service.GetCurrentAsync(SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.NotFound);
    }

    [Fact]
    public async Task SearchAsync_SalesStaff_SeesOnlyOwnShiftsEvenWhenFilteringForAnotherCashier()
    {
        // Arrange
        _sessions.AddSession(SalesA.Id);
        _sessions.AddSession(SalesB.Id);

        // Act
        var result = await _service.SearchAsync(new PosShiftSearchRequest { CashierUserId = SalesB.Id }, SalesA);

        // Assert
        result.Data!.Items.Should().ContainSingle().Which.CashierUserId.Should().Be(SalesA.Id);
        result.Data.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_Manager_SeesEveryCashier()
    {
        // Arrange
        _sessions.AddSession(SalesA.Id);
        _sessions.AddSession(SalesB.Id);

        // Act
        var result = await _service.SearchAsync(new PosShiftSearchRequest(), Manager);

        // Assert
        result.Data!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_UnknownStatus_ReturnsValidationError()
    {
        // Act
        var result = await _service.SearchAsync(new PosShiftSearchRequest { Status = "Paused" }, Manager);

        // Assert
        result.ValidationErrors.Should().ContainKey("status");
    }

    [Fact]
    public async Task GetByIdAsync_OtherUsersShiftAsSalesStaff_ReturnsNotFound()
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id);

        // Act
        var asOther = await _service.GetByIdAsync(shift.Id, SalesB);
        var asManager = await _service.GetByIdAsync(shift.Id, Manager);

        // Assert
        asOther.Kind.Should().Be(PosResultKind.NotFound);
        asManager.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CloseAsync_ExactCash_ClosesWithZeroDiscrepancy()
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id, opening: 2_000_000m);
        shift.CashSalesTotal = 5_000_000m;

        // Act
        var result = await _service.CloseAsync(shift.Id, new CloseShiftRequest { ClosingBalance = 7_000_000m }, SalesA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be("Closed");
        result.Data.ExpectedClosingBalance.Should().Be(7_000_000m);
        result.Data.ClosingBalance.Should().Be(7_000_000m);
        result.Data.Discrepancy.Should().Be(0m);
        result.Data.ClosedByUserId.Should().Be(SalesA.Id);
        result.Data.ClosedAt.Should().Be(Now);
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task CloseAsync_ShortageWithNote_StoresDiscrepancyAndNote()
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id, opening: 1_000_000m);
        shift.CashSalesTotal = 500_000m;

        // Act
        var result = await _service.CloseAsync(
            shift.Id, new CloseShiftRequest { ClosingBalance = 1_400_000m, Note = "  Tra nham tien thua  " }, SalesA);

        // Assert
        result.Data!.Discrepancy.Should().Be(-100_000m);
        result.Data.ClosingNote.Should().Be("Tra nham tien thua");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CloseAsync_DiscrepancyWithoutNote_ReturnsValidationErrorAndShiftStaysOpen(string? note)
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id, opening: 1_000_000m);

        // Act
        var result = await _service.CloseAsync(shift.Id, new CloseShiftRequest { ClosingBalance = 900_000m, Note = note }, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("note");
        shift.Status.Should().Be(PosSessionStatus.Open);
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task CloseAsync_OtherSalesStaff_ReturnsNotFoundButManagerCanClose()
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id, opening: 1_000_000m);

        // Act
        var asOther = await _service.CloseAsync(shift.Id, new CloseShiftRequest { ClosingBalance = 1_000_000m }, SalesB);
        var asManager = await _service.CloseAsync(shift.Id, new CloseShiftRequest { ClosingBalance = 1_000_000m }, Manager);

        // Assert
        asOther.Kind.Should().Be(PosResultKind.NotFound);
        asManager.IsSuccess.Should().BeTrue();
        asManager.Data!.ClosedByUserId.Should().Be(Manager.Id);
        asManager.Data.CashierUserId.Should().Be(SalesA.Id);
    }

    [Fact]
    public async Task CloseAsync_AlreadyClosed_ReturnsConflictAndChangesNothing()
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id, status: PosSessionStatus.Closed);
        shift.ClosingBalance = 1m;

        // Act
        var result = await _service.CloseAsync(shift.Id, new CloseShiftRequest { ClosingBalance = 5m, Note = "x" }, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        shift.ClosingBalance.Should().Be(1m);
    }

    [Fact]
    public async Task CloseAsync_InvalidAmountOrLongNote_ReturnsValidationErrors()
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id);

        // Act
        var result = await _service.CloseAsync(
            shift.Id, new CloseShiftRequest { ClosingBalance = -5m, Note = new string('x', 501) }, SalesA);

        // Assert
        result.ValidationErrors.Should().ContainKeys("closingBalance", "note");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task CloseAsync_WithAPendingQr_IsRefusedNamingTheOrderAndTheShiftStaysOpen()
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id, opening: 1_000_000m);
        var order = new TechStore.Api.Orders.Entities.Order { OrderCode = "ORD-202611-0007", CashierUserId = SalesA.Id, TotalAmount = 5m };
        _vietQr.AddRequest(order, shift.Id);

        // Act
        var result = await _service.CloseAsync(shift.Id, new CloseShiftRequest { ClosingBalance = 1_000_000m }, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.Conflict);
        result.ConflictReason.Should().Contain("ORD-202611-0007").And.Contain("1");
        shift.Status.Should().Be(PosSessionStatus.Open);
        _unitOfWork.CommitCount.Should().Be(0);
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Theory]
    [InlineData(VietQrStatus.Confirmed)]
    [InlineData(VietQrStatus.Failed)]
    [InlineData(VietQrStatus.Expired)]
    public async Task CloseAsync_QrAlreadyEnded_DoesNotBlockTheClose(VietQrStatus ended)
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id, opening: 1_000_000m);
        _vietQr.AddRequest(new TechStore.Api.Orders.Entities.Order { OrderCode = "ORD-1", CashierUserId = SalesA.Id, TotalAmount = 5m }, shift.Id, ended);

        // Act
        var result = await _service.CloseAsync(shift.Id, new CloseShiftRequest { ClosingBalance = 1_000_000m }, SalesA);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CloseAsync_QrOfAnotherShift_DoesNotBlockThisClose()
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id, opening: 1_000_000m);
        _vietQr.AddRequest(new TechStore.Api.Orders.Entities.Order { OrderCode = "ORD-1", CashierUserId = SalesB.Id, TotalAmount = 5m }, Guid.NewGuid());

        // Act
        var result = await _service.CloseAsync(shift.Id, new CloseShiftRequest { ClosingBalance = 1_000_000m }, SalesA);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CloseAsync_QrSalesAreNotPartOfTheExpectedCash()
    {
        // Arrange
        var shift = _sessions.AddSession(SalesA.Id, opening: 1_000_000m);
        shift.CashSalesTotal = 500_000m;
        shift.VietQrSalesTotal = 2_000_000m;

        // Act
        var result = await _service.CloseAsync(shift.Id, new CloseShiftRequest { ClosingBalance = 1_500_000m }, SalesA);

        // Assert
        result.Data!.ExpectedClosingBalance.Should().Be(1_500_000m);
        result.Data.Discrepancy.Should().Be(0m);
        result.Data.VietQrSalesTotal.Should().Be(2_000_000m);
    }

    [Fact]
    public async Task CloseAsync_UnknownShift_ReturnsNotFound()
    {
        // Act
        var result = await _service.CloseAsync(Guid.NewGuid(), new CloseShiftRequest { ClosingBalance = 0m }, SalesA);

        // Assert
        result.Kind.Should().Be(PosResultKind.NotFound);
    }
}
