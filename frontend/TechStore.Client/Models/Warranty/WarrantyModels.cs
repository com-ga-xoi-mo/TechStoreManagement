using TechStore.Shared.DTOs;

namespace TechStore.Client.Models.Warranty;

public sealed record WarrantyRow(Guid Id, string Code, string Status, string Product, string? Device, string? Customer, DateTime OpenedAt)
{
    public string StatusLabel => WarrantyLabels.Status(Status);
    public string DisplayCustomer => Customer ?? "Khách lẻ";
    public string OpenedLabel => WarrantyLabels.Date(OpenedAt);
}

public sealed record WarrantyPage(IReadOnlyList<WarrantyRow> Items, int Page, int TotalPages, int TotalCount);
public sealed record WarrantyLookup(bool Eligible, IReadOnlyList<string> Reasons, string Product, string? Device,
    DateTime? WarrantyEndAt, string? OrderCode, string? CustomerName, string? CustomerPhone);
public sealed record WarrantyEvent(string? From, string To, string Actor, string? Note, DateTime At)
{
    public string Label => $"{WarrantyLabels.Date(At)} · {Actor} · {WarrantyLabels.Status(To)}" +
        (string.IsNullOrWhiteSpace(Note) ? "" : $" · {Note}");
}

public sealed record WarrantyDetails(WarrantyRow Row, Guid ProductId, Guid? VariantId, string? VariantName,
    string? OrderCode, DateTime? PurchasedAt, DateTime WarrantyStartAt, DateTime WarrantyEndAt, string? Issue,
    string? ContactName, string? ContactPhone, string? Inspection, string? Rejection,
    string? Replacement, decimal? ReplacementCost, IReadOnlyList<WarrantyEvent> Events);
public sealed record WarrantyReplacement(Guid Id, string Code);

public static class WarrantyLabels
{
    public static string Status(string status) => status switch
    {
        "Received" => "Chờ kiểm định",
        "Repairing" => "Đang sửa",
        "PendingReplacement" => "Chờ duyệt đổi",
        "Repaired" => "Đã sửa, chờ trả",
        "Rejected" => "Từ chối bảo hành",
        "ReturnedToCustomer" => "Đã trả khách",
        "Replaced" => "Đã đổi máy",
        _ => status
    };

    public static string Reason(string reason) => reason switch
    {
        WarrantyReasonCodes.NotSold => "Thiết bị hiện không ở trạng thái đã bán.",
        WarrantyReasonCodes.NoCoveringSale => "Không tìm thấy lần bán còn quyền bảo hành.",
        WarrantyReasonCodes.Expired => "Đã hết hạn bảo hành hoặc chưa đến ngày bắt đầu.",
        WarrantyReasonCodes.LineReturned => "Dòng hàng đã được trả hết.",
        WarrantyReasonCodes.PendingReturn => "Đang có yêu cầu trả hàng.",
        WarrantyReasonCodes.OpenCase => "Thiết bị đang có hồ sơ bảo hành mở.",
        WarrantyReasonCodes.OrderCodeRequired => "Nhập mã đơn của khách lẻ.",
        WarrantyReasonCodes.OrderMismatch => "Mã đơn không khớp với lần bán.",
        WarrantyReasonCodes.CustomerMismatch => "Khách hàng không khớp với lần bán.",
        _ => "Thiết bị chưa đủ điều kiện bảo hành."
    };

    public static string Date(DateTime? value) => value is { } date
        ? date.ToUniversalTime().AddHours(7).ToString("dd/MM/yyyy HH:mm")
        : "—";
}
