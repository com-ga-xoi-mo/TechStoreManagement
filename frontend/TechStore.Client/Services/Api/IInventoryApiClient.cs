using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Services.Api;

public enum InventoryStockDeleteFailure
{
    None,
    Unsupported,
    Unauthorized,
    Forbidden,
    BadRequest,
    NotFound,
    Conflict,
    Unavailable
}

public enum InventoryStockDetailFailure
{
    None,
    Unauthorized,
    Forbidden,
    BadRequest,
    NotFound,
    Conflict,
    Unavailable
}

public sealed record InventoryStockDetailResult(
    bool Succeeded,
    InventoryStockDetailDto? Data = null,
    InventoryStockDetailFailure Failure = InventoryStockDetailFailure.None,
    string? Message = null)
{
    public static InventoryStockDetailResult Success(InventoryStockDetailDto data) => new(true, data);
}

public sealed record InventoryStockAdjustmentResult(
    bool Succeeded,
    InventoryStockDetailDto? Data = null,
    InventoryStockDetailFailure Failure = InventoryStockDetailFailure.None,
    string? Message = null)
{
    public static InventoryStockAdjustmentResult Success(InventoryStockDetailDto data) => new(true, data);
}

public sealed record InventoryStockInitializeResult(
    bool Succeeded,
    InventoryStockDto? Data = null,
    InventoryStockDetailFailure Failure = InventoryStockDetailFailure.None,
    string? Message = null)
{
    public static InventoryStockInitializeResult Success(InventoryStockDto data) => new(true, data);
}

public sealed record InventorySerialImportResult(
    bool Succeeded,
    int ImportedCount = 0,
    InventoryStockDetailFailure Failure = InventoryStockDetailFailure.None,
    string? Message = null)
{
    public static InventorySerialImportResult Success(int importedCount) => new(true, importedCount);
}

public sealed record InventoryStockDeleteResult(
    bool Succeeded,
    InventoryStockDeleteFailure Failure = InventoryStockDeleteFailure.None,
    string? Message = null)
{
    public static InventoryStockDeleteResult Success() => new(true);
    public static InventoryStockDeleteResult Unsupported(string? message = null) =>
        new(false, InventoryStockDeleteFailure.Unsupported, message);
}

public interface IInventoryApiClient
{
    Task<PagedResultDto<SerialImeiDto>> GetSerialsAsync(
        SerialImeiFilterRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromException<PagedResultDto<SerialImeiDto>>(new NotSupportedException("Serial/IMEI listing is not supported."));

    Task<PagedResultDto<InventoryStockDto>> GetStocksAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stock balance only when the server contract is explicitly supported.
    /// The default implementation is safe/deferred and never sends a request.
    /// </summary>
    Task<InventoryStockDeleteResult> DeleteStockAsync(
        Guid stockId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(InventoryStockDeleteResult.Unsupported());

    Task<InventoryStockDetailResult> GetStockDetailAsync(
        Guid stockId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new InventoryStockDetailResult(false, Failure: InventoryStockDetailFailure.Unavailable, Message: "Stock detail is not supported."));

    Task<InventoryStockAdjustmentResult> AdjustStockAsync(
        Guid stockId,
        AdjustStockRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new InventoryStockAdjustmentResult(false, Failure: InventoryStockDetailFailure.Unavailable, Message: "Stock adjustment is not supported."));

    Task<InventoryStockInitializeResult> InitializeStockAsync(
        InitializeStockRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new InventoryStockInitializeResult(false, Failure: InventoryStockDetailFailure.Unavailable, Message: "Stock initialization is not supported."));

    Task<InventorySerialImportResult> BatchImportSerialsAsync(
        BatchImportSerialImeiRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new InventorySerialImportResult(false, Failure: InventoryStockDetailFailure.Unavailable, Message: "Serial import is not supported."));
}
