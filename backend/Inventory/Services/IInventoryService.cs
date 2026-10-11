using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory.Services;

public record InventoryStockSearchResult(
    bool IsSuccess,
    PagedResultDto<InventoryStockDto>? Data,
    Dictionary<string, string[]>? ValidationErrors)
{
    public static InventoryStockSearchResult Success(PagedResultDto<InventoryStockDto> data) =>
        new(true, data, null);

    public static InventoryStockSearchResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, null, errors);
}

public record InitializeStockResult(
    bool IsSuccess,
    InventoryStockDto? Data,
    Dictionary<string, string[]>? ValidationErrors,
    bool IsConflict,
    string? ConflictReason)
{
    public static InitializeStockResult Success(InventoryStockDto data) =>
        new(true, data, null, false, null);

    public static InitializeStockResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, null, errors, false, null);

    public static InitializeStockResult Conflict(string reason) =>
        new(false, null, null, true, reason);
}

public interface IInventoryService
{
    Task<InventoryStockSearchResult> GetStocksAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<InitializeStockResult> InitializeStockAsync(
        InitializeStockRequest request,
        Guid performedByUserId,
        CancellationToken cancellationToken = default);
}
