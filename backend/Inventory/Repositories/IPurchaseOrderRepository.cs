using TechStore.Api.Inventory.Entities;
using TechStore.Shared.DTOs;

namespace TechStore.Api.Inventory.Repositories;

public interface IPurchaseOrderRepository
{
    // Stages the purchase order together with the lines in its Items collection.
    void Add(PurchaseOrder purchaseOrder);

    Task<Guid?> FindIdByClientRequestIdAsync(Guid clientRequestId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<PurchaseReceiptSummaryDto> Items, int TotalCount)> SearchReceiptsAsync(
        PurchaseReceiptSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<(PurchaseOrder? Order, IReadOnlyList<SerialImei> Devices)> GetReceiptDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
