using System.Text.Json;
using TechStore.Client.Models.Vouchers;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Vouchers;

public sealed class VoucherRepository(IApiClient apiClient) : IVoucherRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The activate and deactivate endpoints take no body; the client still needs a JSON value to send.</summary>
    private sealed record NoBody;

    public async Task<VoucherPage> SearchAsync(VoucherQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var json = await apiClient.GetAsync<JsonElement>(BuildSearchPath(query), cancellationToken);
        var page = Deserialize<PagedResultDto<VoucherDto>>(json, "voucher page");
        if (page.Page < 1 || page.PageSize is < 1 or > 100 || page.TotalCount < 0 || page.Items is null)
            throw InvalidResponse("Voucher page metadata is outside the API contract.");
        var expectedPages = page.TotalCount == 0 ? 0 : (int)Math.Ceiling((double)page.TotalCount / page.PageSize);
        if (page.TotalPages != expectedPages)
            throw InvalidResponse("Voucher page totalPages does not match totalCount and pageSize.");
        return new(page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }

    public async Task<VoucherItem> CreateAsync(CreateVoucherRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = await apiClient.PostAsync<CreateVoucherRequest, JsonElement>("api/vouchers", request, cancellationToken);
        return Map(Deserialize<VoucherDto>(json, "created voucher"));
    }

    public async Task<VoucherItem> UpdateAsync(Guid voucherId, UpdateVoucherRequest request, CancellationToken cancellationToken = default)
    {
        EnsureId(voucherId);
        ArgumentNullException.ThrowIfNull(request);
        var json = await apiClient.PutAsync<UpdateVoucherRequest, JsonElement>($"api/vouchers/{voucherId:D}", request, cancellationToken);
        return MapSameVoucher(Deserialize<VoucherDto>(json, "updated voucher"), voucherId);
    }

    public async Task<VoucherItem> SetActiveAsync(Guid voucherId, bool isActive, CancellationToken cancellationToken = default)
    {
        EnsureId(voucherId);
        var action = isActive ? "activate" : "deactivate";
        var json = await apiClient.PatchAsync<NoBody, JsonElement>($"api/vouchers/{voucherId:D}/{action}", new NoBody(), cancellationToken);
        return MapSameVoucher(Deserialize<VoucherDto>(json, "voucher status"), voucherId);
    }

    private static string BuildSearchPath(VoucherQuery query)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Query)) values.Add($"q={Uri.EscapeDataString(query.Query.Trim())}");
        if (query.IsActive.HasValue) values.Add($"isActive={query.IsActive.Value.ToString().ToLowerInvariant()}");
        values.Add($"page={Math.Max(1, query.Page)}");
        values.Add($"pageSize={Math.Clamp(query.PageSize, 1, 100)}");
        return $"api/vouchers?{string.Join('&', values)}";
    }

    private static VoucherItem MapSameVoucher(VoucherDto dto, Guid expectedId)
    {
        var voucher = Map(dto);
        if (voucher.Id != expectedId) throw InvalidResponse("The voucher id did not match the requested id.");
        return voucher;
    }

    private static VoucherItem Map(VoucherDto dto)
    {
        if (dto.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(dto.Code)
            || dto.DiscountType is not (VoucherItem.Percentage or VoucherItem.FixedAmount)
            || dto.DiscountValue <= 0
            || dto.MinOrderAmount < 0
            || dto.MaxDiscountAmount < 0
            || dto.UsageLimit < 1
            || dto.UsedCount < 0
            || dto.ReservedCount < 0)
            throw InvalidResponse("A voucher contains invalid values.");
        return new(dto.Id, dto.Code, dto.Description, dto.DiscountType, dto.DiscountValue, dto.MinOrderAmount,
            dto.MaxDiscountAmount, dto.UsageLimit, dto.UsedCount, dto.ReservedCount, dto.ExpiresAt, dto.IsActive, dto.CreatedAt);
    }

    private static T Deserialize<T>(JsonElement json, string description) where T : class
    {
        try
        {
            return json.Deserialize<T>(JsonOptions) ?? throw InvalidResponse($"The {description} response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse($"The {description} response did not match the API contract.", exception);
        }
    }

    private static void EnsureId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A voucher id is required.", nameof(id));
    }

    private static ApiException InvalidResponse(string message, Exception? inner = null) =>
        new(ApiErrorKind.InvalidResponse, message, innerException: inner);
}
