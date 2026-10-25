using System.Globalization;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Services.Api;

public sealed class InventoryApiClient(IApiClient apiClient) : IInventoryApiClient
{
    public Task<PagedResultDto<SerialImeiDto>> GetSerialsAsync(
        SerialImeiFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSerialRequest(request);
        return apiClient.GetAsync<PagedResultDto<SerialImeiDto>>(BuildSerialPath(request), cancellationToken);
    }

    public Task<PagedResultDto<InventoryStockDto>> GetStocksAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        return apiClient.GetAsync<PagedResultDto<InventoryStockDto>>(
            BuildPath(request), cancellationToken);
    }

    public Task<InventoryStockDeleteResult> DeleteStockAsync(
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        if (stockId == Guid.Empty) throw new ArgumentException("Stock ID is required.", nameof(stockId));
        cancellationToken.ThrowIfCancellationRequested();
        return DeleteStockCoreAsync(stockId, cancellationToken);
    }

    public Task<InventoryStockDetailResult> GetStockDetailAsync(
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        if (stockId == Guid.Empty) throw new ArgumentException("Stock ID is required.", nameof(stockId));
        cancellationToken.ThrowIfCancellationRequested();
        return GetStockDetailCoreAsync(stockId, cancellationToken);
    }

    public Task<InventoryStockAdjustmentResult> AdjustStockAsync(
        Guid stockId,
        AdjustStockRequest request,
        CancellationToken cancellationToken = default)
    {
        if (stockId == Guid.Empty) throw new ArgumentException("Stock ID is required.", nameof(stockId));
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return AdjustStockCoreAsync(stockId, request, cancellationToken);
    }

    public Task<InventoryStockInitializeResult> InitializeStockAsync(
        InitializeStockRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.ProductId.HasValue || request.ProductId == Guid.Empty)
            throw new ArgumentException("Product ID is required.", nameof(request));
        if (request.Quantity is not > 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Opening quantity must be greater than zero.");
        return InitializeStockCoreAsync(request, cancellationToken);
    }

    public Task<InventorySerialImportResult> BatchImportSerialsAsync(
        BatchImportSerialImeiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.ProductId.HasValue || request.ProductId == Guid.Empty)
            throw new ArgumentException("Product ID is required.", nameof(request));
        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("At least one serial or IMEI is required.", nameof(request));
        return BatchImportSerialsCoreAsync(request, cancellationToken);
    }

    private async Task<InventoryStockDetailResult> GetStockDetailCoreAsync(
        Guid stockId,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await apiClient.GetAsync<InventoryStockDetailDto>(
                $"api/inventory/stocks/{stockId:D}/detail", cancellationToken);
            return InventoryStockDetailResult.Success(data);
        }
        catch (ApiException exception)
        {
            return new InventoryStockDetailResult(false, Failure: MapFailure(exception), Message: exception.Detail ?? exception.Message);
        }
    }

    private async Task<InventoryStockInitializeResult> InitializeStockCoreAsync(
        InitializeStockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await apiClient.PostAsync<InitializeStockRequest, InventoryStockDto>(
                "api/inventory/stocks/initialize", request, cancellationToken);
            return InventoryStockInitializeResult.Success(data);
        }
        catch (ApiException exception)
        {
            return new InventoryStockInitializeResult(false, Failure: MapFailure(exception), Message: exception.Detail ?? exception.Message);
        }
    }

    private async Task<InventorySerialImportResult> BatchImportSerialsCoreAsync(
        BatchImportSerialImeiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await apiClient.PostAsync<BatchImportSerialImeiRequest, BatchImportSerialResponse>(
                "api/inventory/serials/batch-import", request, cancellationToken);
            return InventorySerialImportResult.Success(data.ImportedCount);
        }
        catch (ApiException exception)
        {
            return new InventorySerialImportResult(false, Failure: MapFailure(exception), Message: exception.Detail ?? exception.Message);
        }
    }

    private async Task<InventoryStockAdjustmentResult> AdjustStockCoreAsync(
        Guid stockId,
        AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await apiClient.PostAsync<AdjustStockRequest, InventoryStockDetailDto>(
                $"api/inventory/stocks/{stockId:D}/adjust", request, cancellationToken);
            return InventoryStockAdjustmentResult.Success(data);
        }
        catch (ApiException exception)
        {
            return new InventoryStockAdjustmentResult(false, Failure: MapFailure(exception), Message: exception.Detail ?? exception.Message);
        }
    }

    private async Task<InventoryStockDeleteResult> DeleteStockCoreAsync(
        Guid stockId,
        CancellationToken cancellationToken)
    {
        try
        {
            await apiClient.DeleteAsync($"api/inventory/stocks/{stockId:D}", cancellationToken);
            return InventoryStockDeleteResult.Success();
        }
        catch (ApiException exception) when (exception.Kind == ApiErrorKind.Http)
        {
            var failure = exception.StatusCode switch
            {
                400 => InventoryStockDeleteFailure.BadRequest,
                401 => InventoryStockDeleteFailure.Unauthorized,
                403 => InventoryStockDeleteFailure.Forbidden,
                404 => InventoryStockDeleteFailure.NotFound,
                409 => InventoryStockDeleteFailure.Conflict,
                _ => InventoryStockDeleteFailure.Unavailable
            };
            return new InventoryStockDeleteResult(false, failure, exception.Detail ?? exception.Message);
        }
        catch (ApiException exception) when (exception.Kind is ApiErrorKind.Network or ApiErrorKind.Timeout)
        {
            return new InventoryStockDeleteResult(false, InventoryStockDeleteFailure.Unavailable, exception.Message);
        }
    }

    internal static string BuildPath(InventoryStockFilterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parameters = new List<string>();
        Add("search", request.Search);
        Add("categoryName", request.CategoryName);
        Add("productId", request.ProductId?.ToString("D"));
        Add("variantId", request.VariantId?.ToString("D"));
        Add("lowStock", request.LowStock?.ToString().ToLowerInvariant());
        Add("outOfStock", request.OutOfStock?.ToString().ToLowerInvariant());
        Add("inStock", request.InStock?.ToString().ToLowerInvariant());
        Add("page", request.Page?.ToString(CultureInfo.InvariantCulture));
        Add("pageSize", request.PageSize?.ToString(CultureInfo.InvariantCulture));
        return parameters.Count == 0
            ? "api/inventory/stocks"
            : $"api/inventory/stocks?{string.Join('&', parameters)}";

        void Add(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            parameters.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }
    }

    internal static string BuildSerialPath(SerialImeiFilterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parameters = new List<string>();
        Add("search", request.Search);
        Add("productId", request.ProductId?.ToString("D"));
        Add("variantId", request.VariantId?.ToString("D"));
        Add("status", request.Status?.ToString());
        Add("page", request.Page?.ToString(CultureInfo.InvariantCulture));
        Add("pageSize", request.PageSize?.ToString(CultureInfo.InvariantCulture));
        return parameters.Count == 0
            ? "api/inventory/serials"
            : $"api/inventory/serials?{string.Join('&', parameters)}";

        void Add(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            parameters.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }
    }

    private static void Validate(InventoryStockFilterRequest request)
    {
        if (request.Page is < 1)
            throw new ArgumentOutOfRangeException(nameof(request), "Page must be at least one.");
        if (request.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(request), "Page size must be between one and one hundred.");
    }

    private static void ValidateSerialRequest(SerialImeiFilterRequest request)
    {
        if (request.Page is < 1)
            throw new ArgumentOutOfRangeException(nameof(request), "Page must be at least one.");
        if (request.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(request), "Page size must be between one and one hundred.");
    }

    private static InventoryStockDetailFailure MapFailure(ApiException exception) =>
        exception.Kind is ApiErrorKind.Network or ApiErrorKind.Timeout
            ? InventoryStockDetailFailure.Unavailable
            : exception.StatusCode switch
            {
                400 => InventoryStockDetailFailure.BadRequest,
                401 => InventoryStockDetailFailure.Unauthorized,
                403 => InventoryStockDetailFailure.Forbidden,
                404 => InventoryStockDetailFailure.NotFound,
                409 => InventoryStockDetailFailure.Conflict,
                _ => InventoryStockDetailFailure.Unavailable
            };

    private sealed class BatchImportSerialResponse
    {
        public int ImportedCount { get; set; }
    }
}
