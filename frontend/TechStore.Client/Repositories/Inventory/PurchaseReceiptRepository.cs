using TechStore.Client.Models.Inventory;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Inventory;

public class PurchaseReceiptRepository(IApiClient api) : IPurchaseReceiptRepository
{
    public async Task<PurchaseReceiptSearchResult> GetReceiptsAsync(
        PurchaseReceiptFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = BuildQueryString(request);
            var result = await api.GetAsync<PagedResultDto<PurchaseReceiptSummaryDto>>(
                "api/inventory/purchase-receipts" + query,
                cancellationToken);
            return PurchaseReceiptSearchResult.Success(result);
        }
        catch (ApiException ex)
        {
            return PurchaseReceiptSearchResult.Failed(MapFailureKind(ex), ex.Detail);
        }
        catch (Exception)
        {
            return PurchaseReceiptSearchResult.Failed(ReceiptFailureKind.Network);
        }
    }

    public async Task<PurchaseReceiptDetailResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await api.GetAsync<PurchaseReceiptDto>(
                $"api/inventory/purchase-receipts/{id:D}",
                cancellationToken);
            return PurchaseReceiptDetailResult.Success(result);
        }
        catch (ApiException ex)
        {
            return PurchaseReceiptDetailResult.Failed(MapFailureKind(ex), ex.Detail);
        }
        catch (Exception)
        {
            return PurchaseReceiptDetailResult.Failed(ReceiptFailureKind.Network);
        }
    }

    public Task<PurchaseReceiptDetailResult> ReceiveAsync(Guid id, ReceivePurchaseDeliveryRequest delivery, CancellationToken cancellationToken = default) =>
        UpdateAsync($"api/inventory/purchase-receipts/{id}/deliveries", delivery, cancellationToken);

    public Task<PurchaseReceiptDetailResult> CancelRemainderAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync($"api/inventory/purchase-receipts/{id}/cancel-remainder", new { }, cancellationToken);

    private async Task<PurchaseReceiptDetailResult> UpdateAsync<T>(string route, T request, CancellationToken cancellationToken)
    {
        try
        {
            return PurchaseReceiptDetailResult.Success(await api.PostAsync<T, PurchaseReceiptDto>(route, request, cancellationToken));
        }
        catch (ApiException ex)
        {
            var message = ex.FieldErrors.Values.SelectMany(errors => errors).FirstOrDefault() ?? ex.Detail;
            return PurchaseReceiptDetailResult.Failed(MapFailureKind(ex), message);
        }
    }

    public async Task<PurchaseReceiptCreateResult> CreateAsync(
        CreatePurchaseReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await api.PostWithStatusAsync<CreatePurchaseReceiptRequest, PurchaseReceiptDto>(
                "api/inventory/purchase-receipts",
                request,
                cancellationToken);

            return response.IsCreated
                ? PurchaseReceiptCreateResult.Created(response.Data)
                : PurchaseReceiptCreateResult.Replayed(response.Data);
        }
        catch (ApiException ex)
        {
            return PurchaseReceiptCreateResult.Failed(
                MapFailureKind(ex),
                ex.Detail,
                ex.FieldErrors,
                ex.DuplicateIdentifiers);
        }
        catch (Exception)
        {
            return PurchaseReceiptCreateResult.Failed(ReceiptFailureKind.Network);
        }
    }

    private static string BuildQueryString(PurchaseReceiptFilterRequest request)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            queryParams.Add($"search={Uri.EscapeDataString(request.Search.Trim())}");
        }

        if (request.SupplierId.HasValue)
        {
            queryParams.Add($"supplierId={request.SupplierId.Value:D}");
        }

        if (request.Page.HasValue)
        {
            queryParams.Add($"page={request.Page.Value}");
        }

        if (request.PageSize.HasValue)
        {
            queryParams.Add($"pageSize={request.PageSize.Value}");
        }

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }

    private static ReceiptFailureKind MapFailureKind(ApiException ex)
    {
        if (ex.Kind != ApiErrorKind.Http)
        {
            return ReceiptFailureKind.Network;
        }

        return ex.StatusCode switch
        {
            400 => ReceiptFailureKind.BadRequest,
            401 => ReceiptFailureKind.Unauthorized,
            403 => ReceiptFailureKind.Forbidden,
            404 => ReceiptFailureKind.NotFound,
            409 => ReceiptFailureKind.Conflict,
            _ => ReceiptFailureKind.Network
        };
    }
}
