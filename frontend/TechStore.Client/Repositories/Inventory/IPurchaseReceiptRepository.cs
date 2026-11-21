using TechStore.Client.Models.Inventory;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Inventory;

public enum ReceiptFailureKind
{
    None,
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Network
}

public sealed record PurchaseReceiptSearchResult(
    bool Succeeded,
    IReadOnlyList<PurchaseReceiptSummary> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    ReceiptFailureKind Failure = ReceiptFailureKind.None,
    string? Message = null)
{
    public static PurchaseReceiptSearchResult Success(PagedResultDto<PurchaseReceiptSummaryDto> paged) =>
        new(
            true,
            paged.Items.Select(PurchaseReceiptSummary.FromDto).ToList(),
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages);

    public static PurchaseReceiptSearchResult Failed(ReceiptFailureKind failure, string? message = null) =>
        new(false, [], 1, 20, 0, 0, failure, message);
}

public sealed record PurchaseReceiptDetailResult(
    bool Succeeded,
    PurchaseReceiptDetail? Data = null,
    ReceiptFailureKind Failure = ReceiptFailureKind.None,
    string? Message = null)
{
    public static PurchaseReceiptDetailResult Success(PurchaseReceiptDto dto) =>
        new(true, PurchaseReceiptDetail.FromDto(dto));

    public static PurchaseReceiptDetailResult Failed(ReceiptFailureKind failure, string? message = null) =>
        new(false, null, failure, message);
}

public sealed record PurchaseReceiptCreateResult(
    bool Succeeded,
    PurchaseReceiptDetail? Data = null,
    bool IsReplay = false,
    ReceiptFailureKind Failure = ReceiptFailureKind.None,
    string? Message = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrors = null,
    IReadOnlyList<string>? DuplicateIdentifiers = null)
{
    public static PurchaseReceiptCreateResult Created(PurchaseReceiptDto dto) =>
        new(true, PurchaseReceiptDetail.FromDto(dto), IsReplay: false);

    public static PurchaseReceiptCreateResult Replayed(PurchaseReceiptDto dto) =>
        new(true, PurchaseReceiptDetail.FromDto(dto), IsReplay: true);

    public static PurchaseReceiptCreateResult Failed(
        ReceiptFailureKind failure,
        string? message = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors = null,
        IReadOnlyList<string>? duplicateIdentifiers = null) =>
        new(false, null, false, failure, message, fieldErrors, duplicateIdentifiers);
}

public interface IPurchaseReceiptRepository
{
    Task<PurchaseReceiptDetailResult> ReceiveAsync(Guid id, ReceivePurchaseDeliveryRequest delivery, CancellationToken cancellationToken = default);
    Task<PurchaseReceiptDetailResult> CancelRemainderAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PurchaseReceiptSearchResult> GetReceiptsAsync(
        PurchaseReceiptFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseReceiptDetailResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PurchaseReceiptCreateResult> CreateAsync(
        CreatePurchaseReceiptRequest request,
        CancellationToken cancellationToken = default);
}
