using TechStore.Client.Models.Warranty;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Warranty;

public interface IWarrantyRepository
{
    Task<WarrantyLookup> LookupAsync(string code, string? orderCode, Guid? customerId, CancellationToken token);
    Task<WarrantyPage> ListAsync(string? query, string? status, int page, CancellationToken token);
    Task<WarrantyDetails> GetAsync(Guid id, CancellationToken token);
    Task<WarrantyDetails> ReceiveAsync(Guid id, ReceiveWarrantyRequest request, CancellationToken token);
    Task<WarrantyDetails> InspectAsync(Guid id, InspectWarrantyRequest request, CancellationToken token);
    Task<WarrantyDetails> RepairedAsync(Guid id, string? note, CancellationToken token);
    Task<WarrantyDetails> ReplaceAsync(Guid id, Guid replacementDeviceId, CancellationToken token);
    Task<WarrantyDetails> RejectReplacementAsync(Guid id, string note, CancellationToken token);
    Task<WarrantyDetails> ReturnAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<WarrantyReplacement>> ReplacementsAsync(Guid productId, Guid? variantId, CancellationToken token);
}

public sealed class WarrantyRequestException(ApiException error) : Exception(MessageFor(error), error)
{
    public int? StatusCode => error.StatusCode;
    public string? CurrentStatus => error.CurrentStatus;

    private static string MessageFor(ApiException error) => error switch
    {
        { StatusCode: 400 } => error.FieldErrors.Values.SelectMany(x => x).FirstOrDefault() ?? "Dữ liệu bảo hành không hợp lệ.",
        { StatusCode: 401 } => "Phiên đăng nhập đã hết hạn.",
        { StatusCode: 403 } => "Bạn không có quyền thực hiện thao tác bảo hành này.",
        { StatusCode: 404 } => "Không tìm thấy thiết bị hoặc hồ sơ bảo hành.",
        { StatusCode: 409, Reasons.Count: > 0 } => string.Join("\n", error.Reasons.Select(WarrantyLabels.Reason)),
        { StatusCode: 409 } => error.Detail ?? "Hồ sơ đã thay đổi. Hãy tải lại.",
        { Kind: ApiErrorKind.Network or ApiErrorKind.Timeout } => "Không thể kết nối máy chủ. Hãy thử lại.",
        _ => "Không thể xử lý yêu cầu bảo hành. Hãy thử lại."
    };
}

public sealed class WarrantyRepository(IApiClient api) : IWarrantyRepository
{
    private const string Route = "api/warranties";

    public Task<WarrantyLookup> LookupAsync(string code, string? orderCode, Guid? customerId, CancellationToken token) =>
        SendAsync(async () =>
        {
            var path = $"{Route}/lookup?code={Uri.EscapeDataString(code.Trim())}";
            if (!string.IsNullOrWhiteSpace(orderCode)) path += $"&orderCode={Uri.EscapeDataString(orderCode.Trim())}";
            if (customerId.HasValue) path += $"&customerId={customerId:D}";
            var dto = await api.GetAsync<WarrantyLookupDto>(path, token);
            return new WarrantyLookup(dto.Eligible, dto.Reasons, dto.ProductName,
                dto.Imei ?? dto.SerialNumber, dto.WarrantyEndAt, dto.OrderCode, dto.CustomerName, dto.CustomerPhone);
        });

    public Task<WarrantyPage> ListAsync(string? query, string? status, int page, CancellationToken token) =>
        SendAsync(async () =>
        {
            var path = $"{Route}?page={page}&pageSize=20";
            if (!string.IsNullOrWhiteSpace(query)) path += $"&q={Uri.EscapeDataString(query.Trim())}";
            if (!string.IsNullOrWhiteSpace(status)) path += $"&status={Uri.EscapeDataString(status)}";
            var dto = await api.GetAsync<PagedResultDto<WarrantyCaseSummaryDto>>(path, token);
            return new WarrantyPage(dto.Items.Select(x => new WarrantyRow(x.Id, x.CaseCode, x.Status, x.ProductName,
                x.DeviceCode, x.CustomerName, x.OpenedAt)).ToArray(), dto.Page, dto.TotalPages, dto.TotalCount);
        });

    public Task<WarrantyDetails> GetAsync(Guid id, CancellationToken token) =>
        SendAsync(async () => Map(await api.GetAsync<WarrantyCaseDto>($"{Route}/{id:D}", token)));
    public Task<WarrantyDetails> ReceiveAsync(Guid id, ReceiveWarrantyRequest request, CancellationToken token) => Post(id, "receive", request, token);
    public Task<WarrantyDetails> InspectAsync(Guid id, InspectWarrantyRequest request, CancellationToken token) => Post(id, "inspect", request, token);
    public Task<WarrantyDetails> RepairedAsync(Guid id, string? note, CancellationToken token) => Post(id, "repaired", new MarkWarrantyRepairedRequest(note), token);
    public Task<WarrantyDetails> ReplaceAsync(Guid id, Guid replacementDeviceId, CancellationToken token) => Post(id, "replace", new ReplaceWarrantyDeviceRequest(replacementDeviceId), token);
    public Task<WarrantyDetails> RejectReplacementAsync(Guid id, string note, CancellationToken token) => Post(id, "reject-replacement", new RejectWarrantyReplacementRequest(note), token);
    public Task<WarrantyDetails> ReturnAsync(Guid id, CancellationToken token) => Post(id, "return", new { }, token);

    public Task<IReadOnlyList<WarrantyReplacement>> ReplacementsAsync(Guid productId, Guid? variantId, CancellationToken token) =>
        SendAsync<IReadOnlyList<WarrantyReplacement>>(async () =>
        {
            var uri = $"api/inventory/serials?productId={productId:D}&status=InStock&pageSize=100";
            if (variantId.HasValue) uri += $"&variantId={variantId:D}";
            var result = new List<WarrantyReplacement>();
            for (var page = 1; ; page++)
            {
                var response = await api.GetAsync<PagedResultDto<SerialImeiDto>>($"{uri}&page={page}", token);
                result.AddRange(response.Items.Where(x => x.ProductId == productId && x.VariantId == variantId && x.Status == SerialImeiStatus.InStock)
                    .Select(x => new WarrantyReplacement(x.Id, x.Imei ?? x.SerialNumber ?? x.Id.ToString("D"))));
                if (page >= response.TotalPages) return result;
            }
        });

    private Task<WarrantyDetails> Post<T>(Guid id, string operation, T request, CancellationToken token) =>
        SendAsync(async () => Map(await api.PostAsync<T, WarrantyCaseDto>($"{Route}/{id:D}/{operation}", request, token)));

    private static WarrantyDetails Map(WarrantyCaseDto dto) => new(
        new(dto.Id, dto.CaseCode, dto.Status, dto.ProductName ?? "—", dto.DeviceCode, dto.CustomerName, dto.OpenedAt),
        dto.ProductId, dto.VariantId, dto.VariantName, dto.OrderCode, dto.PurchasedAt, dto.WarrantyStartAt, dto.WarrantyEndAt,
        dto.IssueDescription, dto.ContactName, dto.ContactPhone, dto.InspectionNote, dto.RejectionReason,
        dto.ReplacementDeviceCode, dto.ReplacementUnitCost, dto.Events.Select(x => new WarrantyEvent(x.FromStatus,
            x.ToStatus, x.ActorName, x.Note, x.CreatedAt)).ToArray());

    private static async Task<T> SendAsync<T>(Func<Task<T>> call)
    {
        try { return await call(); }
        catch (ApiException error) { throw new WarrantyRequestException(error); }
    }
}
