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

public record DeleteInventoryStockResult(
    bool IsSuccess,
    bool IsNotFound,
    bool IsConflict,
    string? ConflictReason)
{
    public static DeleteInventoryStockResult Success() => new(true, false, false, null);
    public static DeleteInventoryStockResult NotFound() => new(false, true, false, null);
    public static DeleteInventoryStockResult Conflict(string reason) => new(false, false, true, reason);
}

public record InventoryStockDetailResult(
    bool IsSuccess,
    InventoryStockDetailDto? Data,
    bool IsNotFound)
{
    public static InventoryStockDetailResult Success(InventoryStockDetailDto data) => new(true, data, false);
    public static InventoryStockDetailResult NotFound() => new(false, null, true);
}

public record AdjustInventoryStockResult(
    bool IsSuccess,
    InventoryStockDetailDto? Data,
    Dictionary<string, string[]>? ValidationErrors,
    bool IsNotFound,
    bool IsConflict,
    string? ConflictReason)
{
    public static AdjustInventoryStockResult Success(InventoryStockDetailDto data) => new(true, data, null, false, false, null);
    public static AdjustInventoryStockResult ValidationFailed(Dictionary<string, string[]> errors) => new(false, null, errors, false, false, null);
    public static AdjustInventoryStockResult NotFound() => new(false, null, null, true, false, null);
    public static AdjustInventoryStockResult Conflict(string reason) => new(false, null, null, false, true, reason);
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

    Task<DeleteInventoryStockResult> DeleteStockAsync(
        Guid stockId,
        CancellationToken cancellationToken = default);

    Task<InventoryStockDetailResult> GetStockDetailAsync(
        Guid stockId,
        CancellationToken cancellationToken = default);

    Task<AdjustInventoryStockResult> AdjustStockAsync(
        Guid stockId,
        AdjustStockRequest request,
        Guid performedByUserId,
        CancellationToken cancellationToken = default);
}
