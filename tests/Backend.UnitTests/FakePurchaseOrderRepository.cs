using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public class FakePurchaseOrderRepository : IPurchaseOrderRepository
{
    public List<PurchaseOrder> PurchaseOrders { get; } = [];
    public List<PurchaseOrder> StagedOrders { get; } = [];
    public List<SerialImei> Devices { get; } = [];
    public FakeSupplierRepository? SupplierRepository { get; set; }
    public FakeProductRepository? ProductRepository { get; set; }

    public void Add(PurchaseOrder purchaseOrder)
    {
        StagedOrders.Add(purchaseOrder);
        PurchaseOrders.Add(purchaseOrder);
    }

    public void Rollback()
    {
        foreach (var order in StagedOrders)
        {
            PurchaseOrders.Remove(order);
        }
        StagedOrders.Clear();
    }

    public Task<Guid?> FindIdByClientRequestIdAsync(Guid clientRequestId, CancellationToken cancellationToken = default)
    {
        var match = PurchaseOrders.FirstOrDefault(p => p.ClientRequestId == clientRequestId);
        return Task.FromResult(match?.Id);
    }

    public Task<(IReadOnlyList<PurchaseReceiptSummaryDto> Items, int TotalCount)> SearchReceiptsAsync(
        PurchaseReceiptSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = PurchaseOrders
            .Where(p => p.Status == PurchaseOrderStatus.Received)
            .AsQueryable();

        if (criteria.SupplierId.HasValue)
        {
            query = query.Where(p => p.SupplierId == criteria.SupplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var s = criteria.Search.Trim();
            query = query.Where(p =>
                p.PoNumber.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (p.SupplierDocumentNumber != null && p.SupplierDocumentNumber.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                (p.Supplier != null && p.Supplier.Name.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(p => p.ReceivedAt ?? p.CreatedAt)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .AsEnumerable()
            .Select(p => new PurchaseReceiptSummaryDto
            {
                Id = p.Id,
                PoNumber = p.PoNumber,
                SupplierId = p.SupplierId,
                SupplierName = p.Supplier?.Name ?? string.Empty,
                SupplierDocumentNumber = p.SupplierDocumentNumber,
                ReceivedAt = p.ReceivedAt,
                ReceivedByUserId = p.ReceivedByUserId,
                ReceivedByName = p.ReceivedByUser?.FullName,
                LineCount = p.Items.Count,
                TotalQuantity = p.Items.Sum(i => i.ReceivedQuantity),
                TotalCost = p.TotalCost
            })
            .ToList();

        return Task.FromResult(((IReadOnlyList<PurchaseReceiptSummaryDto>)items, totalCount));
    }

    public Task<(PurchaseOrder? Order, IReadOnlyList<SerialImei> Devices)> GetReceiptDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var order = PurchaseOrders.FirstOrDefault(p => p.Id == id);
        if (order != null)
        {
            if (order.Supplier == null && SupplierRepository != null)
            {
                var s = SupplierRepository.Suppliers.FirstOrDefault(s => s.Id == order.SupplierId);
                if (s != null) order.Supplier = s;
            }
            if (ProductRepository != null)
            {
                foreach (var item in order.Items)
                {
                    if (item.Product == null && ProductRepository.Products.TryGetValue(item.ProductId, out var prod))
                    {
                        item.Product = prod;
                    }
                    if (item.Variant == null && item.VariantId.HasValue && item.Product != null)
                    {
                        item.Variant = item.Product.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value);
                    }
                }
            }
        }
        var devices = Devices.Where(d => d.PurchaseOrderId == id).ToList();
        return Task.FromResult<(PurchaseOrder?, IReadOnlyList<SerialImei>)>((order, devices));
    }
}
