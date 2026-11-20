using TechStore.Client.Models.Inventory;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Inventory;

public class SupplierRepository(IApiClient api) : ISupplierRepository
{
    public async Task<SupplierSearchResult> GetSuppliersAsync(
        SupplierFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = BuildQueryString(request);
            var result = await api.GetAsync<PagedResultDto<SupplierDto>>(
                "api/inventory/suppliers" + query,
                cancellationToken);
            return SupplierSearchResult.Success(result);
        }
        catch (ApiException ex)
        {
            return SupplierSearchResult.Failed(MapFailureKind(ex), ex.Detail, ex.FieldErrors);
        }
        catch (Exception)
        {
            return SupplierSearchResult.Failed(SupplierFailureKind.Network);
        }
    }

    public async Task<SupplierDetailResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await api.GetAsync<SupplierDto>(
                $"api/inventory/suppliers/{id:D}",
                cancellationToken);
            return SupplierDetailResult.Success(result);
        }
        catch (ApiException ex)
        {
            return SupplierDetailResult.Failed(MapFailureKind(ex), ex.Detail);
        }
        catch (Exception)
        {
            return SupplierDetailResult.Failed(SupplierFailureKind.Network);
        }
    }

    public async Task<SupplierMutationResult> CreateAsync(
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await api.PostAsync<SaveSupplierRequest, SupplierDto>(
                "api/inventory/suppliers",
                request,
                cancellationToken);
            return SupplierMutationResult.Success(result);
        }
        catch (ApiException ex)
        {
            return SupplierMutationResult.Failed(MapFailureKind(ex), ex.Detail, ex.FieldErrors);
        }
        catch (Exception)
        {
            return SupplierMutationResult.Failed(SupplierFailureKind.Network);
        }
    }

    public async Task<SupplierMutationResult> UpdateAsync(
        Guid id,
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await api.PutAsync<SaveSupplierRequest, SupplierDto>(
                $"api/inventory/suppliers/{id:D}",
                request,
                cancellationToken);
            return SupplierMutationResult.Success(result);
        }
        catch (ApiException ex)
        {
            return SupplierMutationResult.Failed(MapFailureKind(ex), ex.Detail, ex.FieldErrors);
        }
        catch (Exception)
        {
            return SupplierMutationResult.Failed(SupplierFailureKind.Network);
        }
    }

    public async Task<SupplierMutationResult> SetStatusAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await api.PatchAsync<SetSupplierStatusRequest, SupplierDto>(
                $"api/inventory/suppliers/{id:D}/status",
                new SetSupplierStatusRequest { IsActive = isActive },
                cancellationToken);
            return SupplierMutationResult.Success(result);
        }
        catch (ApiException ex)
        {
            return SupplierMutationResult.Failed(MapFailureKind(ex), ex.Detail, ex.FieldErrors);
        }
        catch (Exception)
        {
            return SupplierMutationResult.Failed(SupplierFailureKind.Network);
        }
    }

    private static string BuildQueryString(SupplierFilterRequest request)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            queryParams.Add($"search={Uri.EscapeDataString(request.Search.Trim())}");
        }

        if (request.IsActive.HasValue)
        {
            queryParams.Add($"isActive={request.IsActive.Value.ToString().ToLowerInvariant()}");
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

    private static SupplierFailureKind MapFailureKind(ApiException ex)
    {
        if (ex.Kind != ApiErrorKind.Http)
        {
            return SupplierFailureKind.Network;
        }

        return ex.StatusCode switch
        {
            400 => SupplierFailureKind.BadRequest,
            401 => SupplierFailureKind.Unauthorized,
            403 => SupplierFailureKind.Forbidden,
            404 => SupplierFailureKind.NotFound,
            409 => SupplierFailureKind.Conflict,
            _ => SupplierFailureKind.Network
        };
    }
}
