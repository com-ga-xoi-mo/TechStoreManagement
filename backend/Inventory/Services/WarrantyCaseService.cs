using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory.Services;

public record WarrantyResult(WarrantyCaseDto? Data, string? Conflict = null)
{
    public static WarrantyResult Missing() => new(Data: null);
    public static WarrantyResult Rejected(string detail) => new(null, detail);
}

public interface IWarrantyCaseService
{
    Task<WarrantyResult> GetAsync(Guid id, CancellationToken ct);
    Task<WarrantyResult> ReceiveAsync(Guid id, ReceiveWarrantyRequest request, AuthenticatedUser actor, CancellationToken ct);
    Task<WarrantyResult> ResolveAsync(Guid id, Guid? replacementId, AuthenticatedUser actor, CancellationToken ct);
}

public sealed class WarrantyCaseService(IWarrantyCaseRepository cases, IInventoryAfterSaleRepository sales,
    ISerialImeiRepository serials, IInventoryAfterSaleService inventory, IUnitOfWork work, TimeProvider clock) : IWarrantyCaseService
{
    public async Task<WarrantyResult> GetAsync(Guid id, CancellationToken ct) =>
        await cases.GetAsync(id, ct) is { } claim ? Map(claim) : WarrantyResult.Missing();

    public Task<WarrantyResult> ReceiveAsync(Guid id, ReceiveWarrantyRequest request, AuthenticatedUser actor, CancellationToken ct) =>
        WriteAsync(async () =>
        {
            var order = await sales.LockOrderAsync(request.OrderId, ct);
            if (order == null || order.CustomerId != request.CustomerId)
                return WarrantyResult.Missing();
            var existing = await cases.LockAsync(id, ct);
            if (existing != null)
                return existing.OrderId == request.OrderId && existing.OrderItemId == request.OrderItemId &&
                    existing.DeviceId == request.DeviceId && existing.CustomerId == request.CustomerId
                    ? Map(existing) : WarrantyResult.Rejected("Mã phiếu bảo hành đã được dùng cho yêu cầu khác.");

            var item = order.Items.SingleOrDefault(i => i.Id == request.OrderItemId);
            if (item?.SerialImeiId == null || order.CompletedAt == null || order.Status is not (OrderStatus.Completed or OrderStatus.PartiallyRefunded) ||
                await cases.ReturnedQuantityAsync(item.Id, ct) >= item.Quantity)
                return WarrantyResult.Rejected("Dòng hàng chưa giao hoặc đã hoàn hết.");

            // Inventory locks the device in the shared order and validates its current sale. Its transition
            // remains staged until the coverage date check below and the case insert both succeed.
            var receiveId = Guid.NewGuid();
            var received = await inventory.ApplyWarrantyAsync(receiveId, id, order.Id, item.Id, request.DeviceId,
                WarrantyAction.ReceiveForRepair, null, actor.Id, ct);
            if (!received.IsSuccess) return WarrantyResult.Rejected(received.Conflict!.Message);
            var device = (await serials.GetByIdsAsync([request.DeviceId], ct)).Single();
            var now = clock.GetUtcNow().UtcDateTime;
            if (device.WarrantyStartAt is not DateTime start || device.WarrantyEndAt is not DateTime end || start > now || now >= end)
                return WarrantyResult.Rejected("Thiết bị nằm ngoài thời hạn bảo hành tại lúc tiếp nhận.");
            var claim = new WarrantyCase
            {
                Id = id, OrderId = order.Id, OrderItemId = item.Id, CustomerId = order.CustomerId, DeviceId = device.Id,
                OpenedAt = now, OpenedById = actor.Id, WarrantyStartAt = start, WarrantyEndAt = end,
                Status = WarrantyCaseStatus.UnderRepair, ReceiveOperationId = receiveId
            };
            cases.Add(claim);
            return Map(claim);
        }, ct);

    public async Task<WarrantyResult> ResolveAsync(Guid id, Guid? replacementId, AuthenticatedUser actor, CancellationToken ct)
    {
        var original = await cases.GetAsync(id, ct);
        if (original == null) return WarrantyResult.Missing();
        return await WriteAsync(async () =>
        {
            var order = await sales.LockOrderAsync(original.OrderId, ct);
            var claim = await cases.LockAsync(id, ct);
            if (claim == null || order == null) return WarrantyResult.Missing();
            var target = replacementId.HasValue ? WarrantyCaseStatus.Replaced : WarrantyCaseStatus.ReturnedToCustomer;
            if (claim.Status != WarrantyCaseStatus.UnderRepair)
                return claim.Status == target && claim.ReplacementDeviceId == replacementId
                    ? Map(claim) : WarrantyResult.Rejected("Phiếu đã kết thúc với một quyết định khác.");
            var item = order.Items.Single(i => i.Id == claim.OrderItemId);
            if (order.CustomerId != claim.CustomerId || await cases.ReturnedQuantityAsync(item.Id, ct) >= item.Quantity)
                return WarrantyResult.Rejected("Khách hoặc quyền bảo hành của dòng bán đã thay đổi.");
            var operationId = Guid.NewGuid();
            var action = replacementId.HasValue ? WarrantyAction.Replace : WarrantyAction.ReturnToCustomer;
            var result = await inventory.ApplyWarrantyAsync(operationId, id, claim.OrderId, claim.OrderItemId,
                claim.DeviceId, action, replacementId, actor.Id, ct);
            if (!result.IsSuccess) return WarrantyResult.Rejected(result.Conflict!.Message);
            claim.Status = target;
            claim.ClosedById = actor.Id;
            claim.ClosedAt = clock.GetUtcNow().UtcDateTime;
            claim.ResolutionOperationId = operationId;
            claim.ReplacementDeviceId = replacementId;
            claim.MovementId = result.MovementId;
            claim.ReplacementUnitCost = result.UnitCost;
            return Map(claim);
        }, ct);
    }

    private async Task<WarrantyResult> WriteAsync(Func<Task<WarrantyResult>> operation, CancellationToken ct)
    {
        await work.BeginTransactionAsync(ct);
        try
        {
            var result = await operation();
            if (result.Data == null)
            {
                await work.RollbackAsync(CancellationToken.None);
                return result;
            }
            await work.SaveChangesAsync(ct);
            await work.CommitAsync(ct);
            return result;
        }
        catch
        {
            await work.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static WarrantyResult Map(WarrantyCase c) => new(new WarrantyCaseDto(c.Id, c.OrderId, c.OrderItemId, c.CustomerId, c.DeviceId,
        c.Status.ToString(), c.OpenedAt, c.WarrantyStartAt, c.WarrantyEndAt, c.ClosedAt, c.ReplacementDeviceId, c.MovementId));
}
