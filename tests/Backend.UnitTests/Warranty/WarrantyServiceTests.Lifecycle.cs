using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Warranty.Entities;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests.Warranty;

public sealed partial class WarrantyServiceTests
{
    [Fact]
    public async Task Lifecycle_RejectReplacementThenRepairAndReturn_RecordsChronologicalEvents()
    {
        var fixture = new Fixture(Fixture.Now.AddTicks(19));
        var id = Guid.NewGuid();
        await fixture.Service.ReceiveAsync(id, fixture.Request(), fixture.Actor, default);
        fixture.Calls.Clear();
        var warehouse = new AuthenticatedUser(Guid.NewGuid(), RoleType.WarehouseStaff, 0);
        var manager = new AuthenticatedUser(Guid.NewGuid(), RoleType.Manager, 0);
        fixture.Cases.ActorNames[manager.Id] = "Quản lý";

        var proposed = await fixture.Service.InspectAsync(id, new("Replace", "Không lên nguồn"), warehouse, default);
        Assert.Equal("PendingReplacement", proposed.Data!.Status);
        Assert.Equal("Không lên nguồn", proposed.Data.InspectionNote);
        Assert.Equal("Repairing", (await fixture.Service.RejectReplacementAsync(id, new("Có thể sửa nguồn"), manager, default)).Data!.Status);
        Assert.Equal("Repaired", (await fixture.Service.MarkRepairedAsync(id, new("Đã sửa"), warehouse, default)).Data!.Status);
        Assert.Equal(new[] { "case", "case", "case" }, fixture.Calls);

        fixture.Calls.Clear();
        var returned = await fixture.Service.ReturnAsync(id, fixture.Actor, default);
        Assert.Equal("ReturnedToCustomer", returned.Data!.Status);
        Assert.Equal(Fixture.Now.AddTicks(10), returned.Data.ClosedAt);
        Assert.Equal(fixture.Actor.Id, returned.Data.ClosedById);
        Assert.Equal(new[] { "Received", "PendingReplacement", "Repairing", "Repaired", "ReturnedToCustomer" },
            returned.Data.Events.Select(e => e.ToStatus));
        Assert.Equal((manager.Id, "Quản lý", "Có thể sửa nguồn"),
            (returned.Data.Events[2].ActorId, returned.Data.Events[2].ActorName, returned.Data.Events[2].Note));
        Assert.Equal(new[] { "order", "case", "inventory" }, fixture.Calls);
        Assert.Equal(WarrantyAction.ReturnToCustomer, fixture.Inventory.Calls[^1].Action);

        var replay = await fixture.Service.ReturnAsync(id, fixture.Actor, default);
        Assert.True(replay.IsReplay);
        Assert.Equal(returned.Data.Events, replay.Data!.Events);
        Assert.Equal(2, fixture.Inventory.Calls.Count);
    }

    [Theory]
    [InlineData("Repair", "Repairing")]
    [InlineData("Reject", "Rejected")]
    public async Task Inspect_ValidOutcome_DoesNotCallInventory(string outcome, string status)
    {
        var fixture = new Fixture();
        var claim = fixture.AddClaim(WarrantyCaseStatus.Received);

        var result = await fixture.Service.InspectAsync(claim.Id, new(outcome, "Kiểm tra", outcome == "Reject" ? "Máy vào nước" : null),
            fixture.Actor, default);

        Assert.Equal(status, result.Data!.Status);
        Assert.Equal(outcome == "Reject" ? "Máy vào nước" : null, result.Data.RejectionReason);
        Assert.Equal("Kiểm tra", Assert.Single(result.Data.Events).Note);
        Assert.Empty(fixture.Inventory.Calls);
    }

    [Fact]
    public async Task Inspect_RejectRequiresReason_AndClosedCaseReportsStatus()
    {
        var fixture = new Fixture();
        var claim = fixture.AddClaim(WarrantyCaseStatus.Received);
        var invalid = await fixture.Service.InspectAsync(claim.Id, new("Reject", "Kiểm tra"), fixture.Actor, default);
        Assert.Contains("rejectionReason", invalid.Errors!.Keys);
        Assert.Equal(WarrantyCaseStatus.Received, claim.Status);

        claim.Status = WarrantyCaseStatus.ReturnedToCustomer;
        var conflict = await fixture.Service.InspectAsync(claim.Id, new("Repair", "Ghi chú"), fixture.Actor, default);
        Assert.Equal("ReturnedToCustomer", conflict.CurrentStatus);
        Assert.Empty(fixture.Cases.Events);
        var missing = await fixture.Service.MarkRepairedAsync(Guid.NewGuid(), new(), fixture.Actor, default);
        Assert.Null(missing.Data);
        Assert.Null(missing.Conflict);
    }

    [Fact]
    public async Task Return_RepairingIsConflict_AndRejectedCanBeHandedBack()
    {
        var fixture = new Fixture();
        var claim = fixture.AddClaim(WarrantyCaseStatus.Repairing);
        Assert.Equal("Repairing", (await fixture.Service.ReturnAsync(claim.Id, fixture.Actor, default)).CurrentStatus);
        Assert.Empty(fixture.Inventory.Calls);

        claim.Status = WarrantyCaseStatus.Rejected;
        Assert.Equal("ReturnedToCustomer", (await fixture.Service.ReturnAsync(claim.Id, fixture.Actor, default)).Data!.Status);
        Assert.Equal(WarrantyAction.ReturnToCustomer, Assert.Single(fixture.Inventory.Calls).Action);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(22000)]
    public async Task Replace_PendingReplacement_PreservesNullableCostAndOnlyReplaysSameChoice(int? cost)
    {
        var fixture = new Fixture();
        var claim = fixture.AddClaim(WarrantyCaseStatus.PendingReplacement);
        var spare = new SerialImei { SerialNumber = "SPARE" };
        fixture.Cases.Devices.Add(spare);
        var movementId = Guid.NewGuid();
        fixture.Inventory.Result = new(true, MovementId: movementId, UnitCost: cost);
        var manager = fixture.Actor with { Role = RoleType.Manager };

        var replaced = await fixture.Service.ReplaceAsync(claim.Id, spare.Id, manager, default);
        Assert.Equal("Replaced", replaced.Data!.Status);
        Assert.Equal((decimal?)cost, replaced.Data.ReplacementUnitCost);
        Assert.Equal(movementId, replaced.Data.MovementId);
        Assert.Equal(spare.SerialNumber, replaced.Data.ReplacementDeviceCode);
        Assert.Equal("PendingReplacement", Assert.Single(replaced.Data.Events).FromStatus);

        var replay = await fixture.Service.ReplaceAsync(claim.Id, spare.Id, manager, default);
        Assert.True(replay.IsReplay);
        Assert.Equal(replaced.Data.Events, replay.Data!.Events);
        var conflict = await fixture.Service.ReplaceAsync(claim.Id, Guid.NewGuid(), manager, default);
        Assert.Equal("Replaced", conflict.CurrentStatus);
        Assert.Single(fixture.Inventory.Calls);
    }

    [Fact]
    public async Task Replace_StockConflict_RollsBackWithoutEventOrClosure()
    {
        var fixture = new Fixture();
        var claim = fixture.AddClaim(WarrantyCaseStatus.PendingReplacement);
        fixture.Inventory.Result = new(false, new FulfillmentProblem(0, "Thiết bị thay không còn trong kho."));

        var result = await fixture.Service.ReplaceAsync(claim.Id, Guid.NewGuid(), fixture.Actor, default);
        Assert.NotNull(result.Conflict);
        Assert.Equal(WarrantyCaseStatus.PendingReplacement, claim.Status);
        Assert.Null(claim.ClosedAt);
        Assert.Empty(fixture.Cases.Events);
        Assert.Equal(1, fixture.Work.RollbackCount);
    }

    [Fact]
    public async Task Inspect_SaveFails_RollsBackTransaction()
    {
        var fixture = new Fixture();
        var claim = fixture.AddClaim(WarrantyCaseStatus.Received);
        fixture.Work.ExceptionToThrowOnSave = new InvalidOperationException("Write failure.");
        fixture.Work.OnRollback = () =>
        {
            claim.Status = WarrantyCaseStatus.Received;
            claim.InspectionNote = null;
            claim.Events.Clear();
            fixture.Cases.Events.Clear();
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.InspectAsync(claim.Id, new("Repair", "Kiểm tra"), fixture.Actor, default));
        Assert.Equal(1, fixture.Work.RollbackCount);
        Assert.Equal(0, fixture.Work.CommitCount);
        Assert.Empty(claim.Events);
    }
}
