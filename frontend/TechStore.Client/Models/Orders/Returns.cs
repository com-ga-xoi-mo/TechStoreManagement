using TechStore.Client.Controls.Presentation;

namespace TechStore.Client.Models.Orders;

public sealed record ReturnBalance(Guid ItemId, string Product, Guid? SerialId, string? Serial, int Remaining, decimal Amount,
    string? Sku = null, string? VariantName = null)
{
    public string Description => string.Join(" · ", new[] { Product, VariantName, Sku, Serial }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public string Label => $"{Description} · Còn {Remaining} · {OrderPresentation.FormatMoney(Amount)}";
}

public sealed record ReturnLine(Guid ItemId, int Quantity, decimal Net, decimal Tax, decimal Discount);
public sealed record ReturnHistory(Guid Id, string Status, string Reason, string? Method, string? BankReference,
    Guid? ReplacementOrderId, DateTime CreatedAt, DateTime? RefundedAt, IReadOnlyList<ReturnLine> Lines, DateTime? BankApprovedAt = null)
{
    public decimal Amount => Lines.Sum(line => line.Net + line.Tax);
    public string Label => $"{OrderPresentation.FormatDateTime(CreatedAt)} · {StatusLabel} · {OrderPresentation.FormatMoney(Amount)}";
    public string StatusLabel => Status switch
    {
        "Pending" => "Chờ duyệt", "AwaitingBank" => "Chờ đối soát ngân hàng",
        "Refunded" => "Đã chi", "Failed" => "Chi không thành công", _ => Status
    };
}

public sealed record LegacyRefund(Guid Id, Guid ItemId, int Quantity, decimal Amount, string Method, DateTime RefundedAt,
    string Reason, bool IsRestocked);

public sealed record OrderReturnSummary(string OrderCode, IReadOnlyList<ReturnBalance> Balances, IReadOnlyList<ReturnHistory> History,
    IReadOnlyList<LegacyRefund>? LegacyRefunds = null, string? ReviewReason = null);

public sealed record InspectionBatch(Guid LineId, Guid RequestId, string Product, string? Serial, int Quantity,
    int Pending, int Restocked, int Defective, int UnderRepair, string? Sku = null)
{
    public string Label => $"{Product}{(string.IsNullOrWhiteSpace(Sku) ? "" : $" · {Sku}")}{(Serial is null ? "" : $" · {Serial}")} · Chờ kiểm định {Pending}/{Quantity}";
    public string Result => $"Phiếu {RequestId:D}\nĐã nhập lại {Restocked} · Hàng lỗi {Defective} · Đang sửa {UnderRepair}";
}
