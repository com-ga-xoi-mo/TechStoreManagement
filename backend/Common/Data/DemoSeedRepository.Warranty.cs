using Microsoft.EntityFrameworkCore;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Warranty.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Common.Data;

internal sealed partial class DemoSeedRepository
{
    public async Task<bool> StageWarrantyWorkflowsAsync(CancellationToken ct)
    {
        if (!dbContext.Database.IsRelational())
            return false;

        var now = new DateTime(2026, 10, 28, 4, 0, 0, DateTimeKind.Utc);
        var clock = new DemoWarrantyTime(now);
        var inventory = new InventoryAfterSaleService(new InventoryAfterSaleRepository(dbContext),
            new SerialImeiRepository(dbContext), new InventoryStockRepository(dbContext), clock);
        var cases = dbContext.Set<WarrantyCase>();
        var users = await dbContext.Users.AsNoTracking().Where(x => x.Username == "admin" || x.Username == "warehouse1")
            .ToDictionaryAsync(x => x.Username, x => x.Id, ct);
        var added = false;
        var entries = new[]
        {
            (Order: "DEMO-202610-0002", Target: WarrantyCaseStatus.Received),
            (Order: "DEMO-202610-0003", Target: WarrantyCaseStatus.Repairing),
            (Order: "DEMO-202610-0004", Target: WarrantyCaseStatus.PendingReplacement),
            (Order: "DEMO-202610-0005", Target: WarrantyCaseStatus.Replaced)
        };
        for (var index = 0; index < entries.Length; index++)
        {
            var (orderCode, target) = entries[index];
            var id = AfterSaleId($"warranty-workflow:{orderCode}");
            if (await cases.AsNoTracking().AnyAsync(x => x.Id == id, ct))
                continue;

            var item = await dbContext.OrderItems.AsNoTracking()
                .Include(x => x.Order).ThenInclude(x => x.Customer).Include(x => x.SerialImei)
                .SingleAsync(x => x.Order.OrderCode == orderCode, ct);
            if (item.Order.Status != OrderStatus.Completed || item.Order.Customer == null ||
                item.SerialImei is not { Status: SerialImeiStatus.Sold, WarrantyStartAt: { } start, WarrantyEndAt: { } end } device
                || now < start || now >= end)
                continue;

            SerialImei? spare = null;
            if (target == WarrantyCaseStatus.Replaced)
            {
                spare = await dbContext.SerialImeis.AsNoTracking()
                    .Where(x => x.ProductId == item.ProductId && x.VariantId == item.VariantId && x.Status == SerialImeiStatus.InStock
                        && x.Imei != null && x.Imei.StartsWith("490000000"))
                    .OrderBy(x => x.Imei).FirstOrDefaultAsync(ct);
                if (spare == null)
                    continue;
            }

            clock.Now = now.AddMinutes(index * 10);
            var actor = users["admin"];
            var receiveOp = AfterSaleId($"warranty-workflow-intake:{orderCode}");
            var received = await inventory.ApplyWarrantyAsync(receiveOp, id, item.OrderId, item.Id, device.Id,
                WarrantyAction.ReceiveForRepair, null, actor, ct);
            if (!received.IsSuccess)
                continue;

            var prefix = "BH-202610-";
            var codes = await cases.AsNoTracking().Where(c => c.CaseCode.StartsWith(prefix)).Select(c => c.CaseCode).ToListAsync(ct);
            codes.AddRange(cases.Local.Select(c => c.CaseCode).Where(c => c.StartsWith(prefix, StringComparison.Ordinal)));
            var max = codes.Select(c => int.TryParse(c.AsSpan(prefix.Length), out var suffix) ? suffix : 0).DefaultIfEmpty().Max();
            var claim = new WarrantyCase
            {
                Id = id, CaseCode = prefix + (max + 1).ToString("D4"), OrderId = item.OrderId, OrderItemId = item.Id,
                CustomerId = item.Order.CustomerId, DeviceId = device.Id, OpenedById = actor, OpenedAt = clock.Now,
                WarrantyStartAt = start, WarrantyEndAt = end, ReceiveOperationId = receiveOp,
                IssueDescription = "Máy hoạt động không ổn định", ContactName = item.Order.Customer.FullName,
                ContactPhone = item.Order.Customer.Phone, Status = WarrantyCaseStatus.Received
            };
            claim.Events.Add(new WarrantyCaseEvent { CaseId = id, ActorId = actor, ToStatus = claim.Status, CreatedAt = clock.Now });
            if (target != WarrantyCaseStatus.Received)
            {
                clock.Now = clock.Now.AddMinutes(1);
                claim.InspectionNote = target == WarrantyCaseStatus.Repairing ? "Kiểm tra nguồn và tiến hành sửa" : "Không thể sửa, đề nghị đổi máy";
                AddDemoWarrantyEvent(claim, target == WarrantyCaseStatus.Repairing ? target : WarrantyCaseStatus.PendingReplacement,
                    users["warehouse1"], clock.Now, claim.InspectionNote);
            }
            if (spare != null)
            {
                clock.Now = clock.Now.AddMinutes(1);
                var replacementOp = AfterSaleId($"warranty-workflow-replace:{orderCode}");
                var replacement = await inventory.ApplyWarrantyAsync(replacementOp, id, item.OrderId, item.Id, device.Id,
                    WarrantyAction.Replace, spare.Id, actor, ct);
                if (!replacement.IsSuccess)
                    throw new InvalidOperationException($"Could not seed warranty replacement: {replacement.Conflict?.Message}");
                claim.ClosedAt = clock.Now;
                claim.ClosedById = actor;
                claim.ResolutionOperationId = replacementOp;
                claim.ReplacementDeviceId = spare.Id;
                claim.MovementId = replacement.MovementId;
                claim.ReplacementUnitCost = replacement.UnitCost;
                AddDemoWarrantyEvent(claim, WarrantyCaseStatus.Replaced, actor, clock.Now, null);
            }
            cases.Add(claim);
            added = true;
        }
        return added;
    }

    private static void AddDemoWarrantyEvent(WarrantyCase claim, WarrantyCaseStatus status, Guid actor, DateTime at, string? note)
    {
        claim.Events.Add(new WarrantyCaseEvent
        {
            CaseId = claim.Id, FromStatus = claim.Status, ToStatus = status, ActorId = actor, Note = note, CreatedAt = at
        });
        claim.Status = status;
    }

    private sealed class DemoWarrantyTime(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
