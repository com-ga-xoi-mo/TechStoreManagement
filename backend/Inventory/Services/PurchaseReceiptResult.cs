using TechStore.Shared.DTOs;

namespace TechStore.Api.Inventory.Services;

public record PurchaseReceiptResult(
    bool IsSuccess,
    bool IsReplay,
    PurchaseReceiptDto? Data,
    Dictionary<string, string[]>? ValidationErrors,
    bool IsNotFound,
    bool IsConflict,
    string? ConflictReason,
    IReadOnlyList<string>? DuplicateIdentifiers = null)
{
    public static PurchaseReceiptResult Created(PurchaseReceiptDto data) =>
        new(true, false, data, null, false, false, null);

    public static PurchaseReceiptResult Updated(PurchaseReceiptDto data) =>
        new(true, false, data, null, false, false, null);

    public static PurchaseReceiptResult Replayed(PurchaseReceiptDto data) =>
        new(true, true, data, null, false, false, null);

    public static PurchaseReceiptResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, false, null, errors, false, false, null);

    public static PurchaseReceiptResult NotFound() =>
        new(false, false, null, null, true, false, null);

    public static PurchaseReceiptResult Conflict(string reason, IReadOnlyList<string>? duplicateIdentifiers = null) =>
        new(false, false, null, null, false, true, reason, duplicateIdentifiers);
}

public record PurchaseReceiptSearchResult(
    bool IsSuccess,
    PagedResultDto<PurchaseReceiptSummaryDto>? Data,
    Dictionary<string, string[]>? ValidationErrors)
{
    public static PurchaseReceiptSearchResult Success(PagedResultDto<PurchaseReceiptSummaryDto> data) =>
        new(true, data, null);

    public static PurchaseReceiptSearchResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, null, errors);
}

public record PurchaseReceiptDetailResult(
    bool IsSuccess,
    PurchaseReceiptDto? Data,
    bool IsNotFound)
{
    public static PurchaseReceiptDetailResult Success(PurchaseReceiptDto data) =>
        new(true, data, false);

    public static PurchaseReceiptDetailResult NotFound() =>
        new(false, null, true);
}
