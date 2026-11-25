using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests.Warranty;

public sealed partial class WarrantyServiceTests
{
    [Fact]
    public async Task Receive_EligibleDevice_CreatesCaseAndReplaysWithoutSecondInventoryOperation()
    {
        var fixture = new Fixture(Fixture.Now.AddTicks(17));
        var caseId = Guid.NewGuid();
        var first = await fixture.Service.ReceiveAsync(caseId, fixture.Request(), fixture.Actor, default);

        Assert.True(first.Created);
        Assert.Equal("BH-202611-0001", first.Data!.CaseCode);
        Assert.Equal("Received", first.Data.Status);
        Assert.Equal(Fixture.Now.AddTicks(10), first.Data.OpenedAt);
        var received = Assert.Single(first.Data.Events);
        Assert.Equal(first.Data.OpenedAt, received.CreatedAt);
        Assert.Null(received.FromStatus);
        Assert.Equal("Received", received.ToStatus);
        Assert.Equal(fixture.Actor.Id, received.ActorId);
        Assert.Equal(new[] { "order", "case", "inventory" }, fixture.Calls);
        Assert.Equal(WarrantyAction.ReceiveForRepair, Assert.Single(fixture.Inventory.Calls).Action);

        var replay = await fixture.Service.ReceiveAsync(caseId, fixture.Request(), fixture.Actor, default);
        Assert.True(replay.IsReplay);
        Assert.False(replay.Created);
        Assert.Equal(first.Data.CaseCode, replay.Data!.CaseCode);
        Assert.Equal(first.Data.OpenedAt, replay.Data.OpenedAt);
        Assert.Equal(received, Assert.Single(replay.Data.Events));
        Assert.Single(fixture.Cases.Cases);
        Assert.Single(fixture.Inventory.Calls);
        Assert.Equal(1, fixture.Work.SaveChangesCount);
        Assert.Equal(2, fixture.Work.CommitCount);
    }

    [Fact]
    public async Task Receive_InvalidContactAndCaseId_ReturnsFieldErrorsWithoutTransaction()
    {
        var fixture = new Fixture();
        var result = await fixture.Service.ReceiveAsync(Guid.Empty,
            fixture.Request() with { ContactPhone = " ", ContactName = new string('x', 101) }, fixture.Actor, default);

        Assert.NotNull(result.Errors);
        Assert.Contains("caseId", result.Errors.Keys);
        Assert.Contains("contactPhone", result.Errors.Keys);
        Assert.Contains("contactName", result.Errors.Keys);
        Assert.Equal(0, fixture.Work.BeginTransactionCount);
        Assert.Empty(fixture.Inventory.Calls);
    }

    [Fact]
    public async Task Receive_ExpiredOrPendingReturn_RollsBackWithReasons()
    {
        var fixture = new Fixture(Fixture.Now.AddMonths(12));
        fixture.Cases.PendingReturns.Add(fixture.Item.Id);

        var result = await fixture.Service.ReceiveAsync(Guid.NewGuid(), fixture.Request(), fixture.Actor, default);

        Assert.Null(result.Data);
        Assert.Contains(WarrantyReasonCodes.Expired, result.Reasons!);
        Assert.Contains(WarrantyReasonCodes.PendingReturn, result.Reasons!);
        Assert.Empty(fixture.Inventory.Calls);
        Assert.Equal(1, fixture.Work.RollbackCount);
    }

    [Fact]
    public async Task Receive_CaseIdAlreadyUsedForDifferentDevice_ReturnsConflict()
    {
        var fixture = new Fixture();
        var prior = fixture.AddClaim(TechStore.Api.Warranty.Entities.WarrantyCaseStatus.Received);
        var other = new SerialImei { SerialNumber = "OTHER", Status = SerialImeiStatus.InStock };
        fixture.Cases.Devices.Add(other);

        var result = await fixture.Service.ReceiveAsync(prior.Id,
            fixture.Request() with { Code = other.SerialNumber }, fixture.Actor, default);

        Assert.Null(result.Data);
        Assert.NotNull(result.Conflict);
        Assert.Empty(fixture.Inventory.Calls);
        Assert.Equal(1, fixture.Work.RollbackCount);
    }

    [Fact]
    public async Task Receive_StockConflict_RollsBackBeforeCreatingCase()
    {
        var fixture = new Fixture();
        fixture.Inventory.Result = new(false, new FulfillmentProblem(0, "Thiết bị không còn khả dụng."));

        var result = await fixture.Service.ReceiveAsync(Guid.NewGuid(), fixture.Request(), fixture.Actor, default);

        Assert.Equal("Thiết bị không còn thuộc lần bán này hoặc đang có yêu cầu trả hàng.", result.Conflict);
        Assert.Null(result.Data);
        Assert.Empty(fixture.Cases.Cases);
        Assert.Empty(fixture.Cases.Events);
        Assert.Equal(1, fixture.Work.RollbackCount);
        Assert.Equal(0, fixture.Work.SaveChangesCount);
    }

    [Fact]
    public async Task Receive_EligibilityChangesWhileStockIsLocked_RollsBack()
    {
        var fixture = new Fixture();
        fixture.Inventory.OnCall = _ => fixture.Device.WarrantyEndAt = Fixture.Now;

        var result = await fixture.Service.ReceiveAsync(Guid.NewGuid(), fixture.Request(), fixture.Actor, default);

        Assert.Contains(WarrantyReasonCodes.Expired, result.Reasons!);
        Assert.Empty(fixture.Cases.Cases);
        Assert.Single(fixture.Inventory.Calls);
        Assert.Equal(1, fixture.Work.RollbackCount);
    }

    [Theory]
    [InlineData("idx_warranty_cases_code", 1, true)]
    [InlineData("idx_warranty_cases_code", 5, false)]
    [InlineData("uq_warranty_cases_open_device", 1, false)]
    public async Task Receive_DuplicateConstraint_RetriesOnlyCaseCode(string constraint, int failures, bool succeeds)
    {
        var fixture = new Fixture();
        fixture.Work.OnRollback = () =>
        {
            fixture.Cases.Cases.Clear();
            fixture.Cases.Events.Clear();
        };
        for (var i = 0; i < failures; i++)
            fixture.Work.SaveExceptionQueue.Enqueue(new DuplicateKeyException(constraint, "Duplicate."));

        var result = await fixture.Service.ReceiveAsync(Guid.NewGuid(), fixture.Request(), fixture.Actor, default);

        Assert.Equal(succeeds, result.Created);
        Assert.Equal(failures, fixture.Work.RollbackCount);
        Assert.Equal(failures + (succeeds ? 1 : 0), fixture.Inventory.Calls.Count);
        Assert.Equal(fixture.Inventory.Calls.Count, fixture.Inventory.Calls.Select(call => call.OperationId).Distinct().Count());
        if (constraint == "uq_warranty_cases_open_device")
            Assert.Contains(WarrantyReasonCodes.OpenCase, result.Reasons!);
    }
}
