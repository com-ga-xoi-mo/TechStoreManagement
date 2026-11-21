using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory.Services;

public interface IPurchaseReceiptService
{
    Task<PurchaseReceiptResult> ReceiveAsync(Guid id, ReceivePurchaseDeliveryRequest delivery, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<PurchaseReceiptResult> CancelRemainderAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<PurchaseReceiptResult> CreateAsync(
        CreatePurchaseReceiptRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<PurchaseReceiptSearchResult> SearchAsync(
        PurchaseReceiptFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseReceiptDetailResult> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
