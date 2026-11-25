using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Warranty.Entities;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Warranty.Services;

public sealed partial class WarrantyCaseService
{
    public Task<WarrantyResult> InspectAsync(Guid id, InspectWarrantyRequest request, AuthenticatedUser actor, CancellationToken ct)
    {
        var target = request.Outcome?.Trim() switch
        {
            "Repair" => WarrantyCaseStatus.Repairing,
            "Replace" => WarrantyCaseStatus.PendingReplacement,
            "Reject" => WarrantyCaseStatus.Rejected,
            _ => (WarrantyCaseStatus?)null
        };
        var errors = new Dictionary<string, string[]>();
        if (target == null)
            errors["outcome"] = ["Chọn Repair, Replace hoặc Reject."];
        var note = RequiredText(request.Note, "note", 1000, errors);
        var rejection = target == WarrantyCaseStatus.Rejected
            ? RequiredText(request.RejectionReason, "rejectionReason", 500, errors)
            : null;
        return errors.Count > 0
            ? Task.FromResult(new WarrantyResult(null) { Errors = errors })
            : ChangeAsync(id, WarrantyCaseStatus.Received, target!.Value, note, actor, ct, claim =>
            {
                claim.InspectionNote = note;
                claim.RejectionReason = rejection;
            });
    }

    public Task<WarrantyResult> MarkRepairedAsync(Guid id, MarkWarrantyRepairedRequest request, AuthenticatedUser actor, CancellationToken ct)
    {
        var note = request.Note?.Trim();
        if (note?.Length > 1000)
            return Task.FromResult(new WarrantyResult(null)
            {
                Errors = new Dictionary<string, string[]> { ["note"] = ["Ghi chú không được quá 1000 ký tự."] }
            });
        return ChangeAsync(id, WarrantyCaseStatus.Repairing, WarrantyCaseStatus.Repaired,
            string.IsNullOrEmpty(note) ? null : note, actor, ct);
    }

    public Task<WarrantyResult> RejectReplacementAsync(Guid id, RejectWarrantyReplacementRequest request, AuthenticatedUser actor, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var note = RequiredText(request.Note, "note", 1000, errors);
        return errors.Count > 0
            ? Task.FromResult(new WarrantyResult(null) { Errors = errors })
            : ChangeAsync(id, WarrantyCaseStatus.PendingReplacement, WarrantyCaseStatus.Repairing, note, actor, ct);
    }

    public Task<WarrantyResult> ReplaceAsync(Guid id, Guid replacementId, AuthenticatedUser actor, CancellationToken ct)
    {
        if (replacementId == Guid.Empty)
            return Task.FromResult(new WarrantyResult(null)
            {
                Errors = new Dictionary<string, string[]> { ["replacementDeviceId"] = ["Chọn thiết bị thay thế."] }
            });
        return FinishAsync(id, WarrantyCaseStatus.Replaced, replacementId, actor, ct);
    }

    public Task<WarrantyResult> ReturnAsync(Guid id, AuthenticatedUser actor, CancellationToken ct) =>
        FinishAsync(id, WarrantyCaseStatus.ReturnedToCustomer, null, actor, ct);

    private Task<WarrantyResult> ChangeAsync(Guid id, WarrantyCaseStatus expected, WarrantyCaseStatus target, string? note,
        AuthenticatedUser actor, CancellationToken ct, Action<WarrantyCase>? update = null) =>
        WriteAsync(async () =>
        {
            var claim = await cases.LockAsync(id, ct);
            if (claim == null)
                return WarrantyResult.Missing();
            if (claim.Status != expected || !WarrantyCaseTransitions.CanTransition(claim.Status, target))
                return StatusConflict(claim);

            update?.Invoke(claim);
            ChangeStatus(claim, target, actor.Id, note, ToDatabaseTime(clock.GetUtcNow().UtcDateTime));
            return Map(claim);
        }, ct, CanViewReplacementCost(actor.Role));

    private async Task<WarrantyResult> FinishAsync(Guid id, WarrantyCaseStatus target, Guid? replacementId,
        AuthenticatedUser actor, CancellationToken ct)
    {
        var original = await cases.GetAsync(id, ct);
        if (original == null)
            return WarrantyResult.Missing();

        return await WriteAsync(async () =>
        {
            var order = await sales.LockOrderAsync(original.OrderId, ct);
            var claim = await cases.LockAsync(id, ct);
            if (claim == null || order == null)
                return WarrantyResult.Missing();
            if (claim.Status == target && claim.ReplacementDeviceId == replacementId)
                return Map(claim) with { IsReplay = true };
            if (!WarrantyCaseTransitions.CanTransition(claim.Status, target))
                return StatusConflict(claim);

            var item = order.Items.SingleOrDefault(i => i.Id == claim.OrderItemId);
            if (item == null || order.CompletedAt == null || order.Status is not (OrderStatus.Completed or OrderStatus.PartiallyRefunded)
                || order.CustomerId != claim.CustomerId || await cases.ReturnedQuantityAsync(item.Id, ct) >= item.Quantity)
                return WarrantyResult.Rejected("Khách hoặc quyền bảo hành của dòng bán đã thay đổi.");

            var opId = Guid.NewGuid();
            var action = replacementId.HasValue ? WarrantyAction.Replace : WarrantyAction.ReturnToCustomer;
            var result = await inventory.ApplyWarrantyAsync(opId, id, claim.OrderId, item.Id,
                claim.DeviceId, action, replacementId, actor.Id, ct);
            if (!result.IsSuccess)
                return WarrantyResult.Rejected(replacementId.HasValue
                    ? "Máy thay không còn trong kho hoặc khác loại. Hãy chọn máy khác."
                    : DeviceInventoryConflict);

            var now = ToDatabaseTime(clock.GetUtcNow().UtcDateTime);
            claim.ClosedById = actor.Id;
            claim.ClosedAt = now;
            claim.ResolutionOperationId = opId;
            claim.ReplacementDeviceId = replacementId;
            claim.MovementId = result.MovementId;
            claim.ReplacementUnitCost = result.UnitCost;
            ChangeStatus(claim, target, actor.Id, null, now);
            return Map(claim);
        }, ct, CanViewReplacementCost(actor.Role));
    }

    private const string DeviceInventoryConflict = "Thiết bị không còn thuộc lần bán này hoặc đang có yêu cầu trả hàng.";

    private void ChangeStatus(WarrantyCase claim, WarrantyCaseStatus status, Guid actorId, string? note, DateTime now)
    {
        var previous = claim.Status;
        claim.Status = status;
        cases.AddEvent(new WarrantyCaseEvent
        {
            CaseId = claim.Id, FromStatus = previous, ToStatus = status,
            ActorId = actorId, Note = note, CreatedAt = now
        });
    }

    private static WarrantyResult StatusConflict(WarrantyCase claim) =>
        WarrantyResult.Rejected("Thao tác không hợp lệ với trạng thái hiện tại của hồ sơ.")
            with { CurrentStatus = claim.Status.ToString() };
}
