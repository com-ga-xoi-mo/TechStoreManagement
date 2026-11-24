using TechStore.Api.Warranty.Entities;
using TechStore.Shared.DTOs;

namespace TechStore.Api.Warranty.Services;

public sealed record WarrantySearchResult(PagedResultDto<WarrantyCaseSummaryDto>? Data, IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed partial class WarrantyCaseService
{
    public async Task<WarrantySearchResult> SearchAsync(string? status, string? query, int page, int pageSize, bool includeCost, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1)
            errors["page"] = ["Số trang phải từ 1 trở lên."];
        if (pageSize is < 1 or > 100)
            errors["pageSize"] = ["Số hồ sơ mỗi trang phải từ 1 đến 100."];
        WarrantyCaseStatus? selectedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var value = status.Trim();
            if (Enum.TryParse<WarrantyCaseStatus>(value, true, out var parsed)
                && string.Equals(value, parsed.ToString(), StringComparison.OrdinalIgnoreCase))
                selectedStatus = parsed;
            else
                errors["status"] = ["Trạng thái hồ sơ không hợp lệ."];
        }
        if (errors.Count > 0)
            return new(null, errors);

        var (items, total) = await cases.SearchAsync(new(selectedStatus, query?.Trim(), page, pageSize), ct);
        var list = items.Select(row => new WarrantyCaseSummaryDto(
            row.Claim.Id, row.Claim.CaseCode, row.Claim.Status.ToString(), row.Claim.OpenedAt, row.ProductName,
            row.DeviceCode, row.OrderCode, row.CustomerName, row.Claim.ContactPhone,
            includeCost ? row.Claim.ReplacementUnitCost : null)).ToArray();
        return new(new PagedResultDto<WarrantyCaseSummaryDto>(list, page, pageSize, total, (int)Math.Ceiling((double)total / pageSize)));
    }
}
