using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.Api.Inventory.Repositories;

public class PurchaseOrderRepository(AppDbContext dbContext) : IPurchaseOrderRepository
{
    public async Task<PurchaseOrder?> LockReceiptAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await dbContext.PurchaseOrders
            .FromSqlInterpolated($"SELECT * FROM purchase_orders WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (order != null)
            await dbContext.Entry(order).Collection(p => p.Items).Query().Include(i => i.Product).LoadAsync(cancellationToken);
        return order;
    }

    public Task<bool> HasDeliveryAsync(Guid receiptId, Guid requestId, CancellationToken cancellationToken) =>
        dbContext.InventoryMovements.AnyAsync(m => m.ReceiptRequestId == requestId
            && m.PurchaseOrderItem!.PurchaseOrderId == receiptId, cancellationToken);

    public void Add(PurchaseOrder purchaseOrder) => dbContext.PurchaseOrders.Add(purchaseOrder);

    public async Task<Guid?> FindIdByClientRequestIdAsync(Guid clientRequestId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(p => p.ClientRequestId == clientRequestId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<PurchaseReceiptSummaryDto> Items, int TotalCount)> SearchReceiptsAsync(
        PurchaseReceiptSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(p => p.Status != PurchaseOrderStatus.Draft);

        if (criteria.SupplierId.HasValue)
        {
            query = query.Where(p => p.SupplierId == criteria.SupplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.Trim();
            var pattern = $"%{EscapeLikePattern(search)}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.PoNumber, pattern, "!") ||
                (p.SupplierDocumentNumber != null && EF.Functions.ILike(p.SupplierDocumentNumber, pattern, "!")) ||
                EF.Functions.ILike(p.Supplier.Name, pattern, "!"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.ReceivedAt ?? p.CreatedAt)
            .ThenByDescending(p => p.Id) // stable paging when several receipts share a timestamp
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(p => new PurchaseReceiptSummaryDto
            {
                Id = p.Id,
                Status = p.Status,
                PoNumber = p.PoNumber,
                SupplierId = p.SupplierId,
                SupplierName = p.Supplier.Name,
                SupplierDocumentNumber = p.SupplierDocumentNumber,
                ReceivedAt = p.ReceivedAt,
                ReceivedByUserId = p.ReceivedByUserId,
                ReceivedByName = p.ReceivedByUser != null ? p.ReceivedByUser.FullName : null,
                LineCount = p.Items.Count,
                TotalQuantity = p.Items.Sum(i => i.ReceivedQuantity),
                TotalCost = p.TotalCost
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(PurchaseOrder? Order, IReadOnlyList<SerialImei> Devices)> GetReceiptDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.ReceivedByUser)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .Include(p => p.Items)
                .ThenInclude(i => i.Variant)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (order == null)
        {
            return (null, []);
        }

        var devices = await dbContext.SerialImeis
            .AsNoTracking()
            .Where(s => s.PurchaseOrderId == id)
            .ToListAsync(cancellationToken);

        return (order, devices);
    }

    private static string EscapeLikePattern(string value) => value.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
}
