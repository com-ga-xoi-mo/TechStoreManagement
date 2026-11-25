using System.Globalization;
using TechStore.Client.Models.Analytics;

namespace TechStore.Client.ViewModels.Dashboard;

public sealed class FinancialReportPresentation
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private const string Unavailable = "Chưa tính được";

    public FinancialReportPresentation(FinancialReportSnapshot snapshot)
    {
        var start = snapshot.Range.From.ToString("dd/MM/yyyy", Vietnamese);
        var end = snapshot.Range.To.ToString("dd/MM/yyyy", Vietnamese);
        var asOf = snapshot.AsOf.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", Vietnamese);
        Reference = $"Kỳ {start}–{end} · Cập nhật {asOf} (UTC+07)";
        Summary = Totals(snapshot.Summary);
        Daily = snapshot.Daily.Select(day =>
        {
            var sections = Totals(day.Totals);
            return new FinancialReportDayRow(
                day.Date.ToString("dd/MM/yyyy", Vietnamese),
                Money(day.Totals.SalesExcludingTax),
                Money(day.Totals.RefundedSalesExcludingTax),
                sections,
                Money(day.Totals.NetSalesExcludingTax),
                Money(day.Totals.NetGrossProfit),
                Count(day.Totals.CompletedOrderCount));
        }).ToArray();

        NetSalesKpi = new("Doanh thu thuần chưa thuế", Money(snapshot.Summary.NetSalesExcludingTax),
            $"Bán {Money(snapshot.Summary.SalesExcludingTax)} · Hoàn {Money(snapshot.Summary.RefundedSalesExcludingTax)}");
        NetProfitKpi = new("Lãi gộp sau hoàn", Money(snapshot.Summary.NetGrossProfit),
            $"Lãi gộp bán {Money(snapshot.Summary.SalesGrossProfit)} · Giá vốn thuần {Money(snapshot.Summary.NetCostOfGoodsSold)}");
        CashFlowKpi = new("Tiền đã thu", Money(snapshot.Summary.PaymentReceivedAmount),
            $"Cash {Money(snapshot.Summary.CashReceivedAmount)} · VietQR {Money(snapshot.Summary.VietQrReceivedAmount)}");
        WarrantyAndOrdersKpi = new("Máy thay bảo hành", Money(snapshot.Summary.WarrantyCost),
            $"{Count(snapshot.Summary.CompletedOrderCount)} đơn đã giao · {Count(snapshot.Summary.Quantity)} sản phẩm");

        var productsByQuantity = snapshot.TopProductsByQuantity.Select(row => new FinancialReportListRow(row.ProductName,
            $"Số lượng {Count(row.Quantity)} · Tiền hàng bán {Money(row.SalesExcludingTax)}")).ToArray();
        var productsByRevenue = snapshot.TopProductsByRevenue.Select(row => new FinancialReportListRow(row.ProductName,
            $"Tiền hàng bán {Money(row.SalesExcludingTax)} · Số lượng {Count(row.Quantity)}")).ToArray();
        var cashiers = snapshot.CashierPerformance.Select(row => new FinancialReportListRow(row.CashierName,
            $"{Count(row.CompletedOrderCount)} đơn · Số lượng {Count(row.Quantity)} · Tiền hàng bán {Money(row.SalesExcludingTax)}\n"
            + $"Giá vốn bán {Money(row.CostOfGoodsSold)} · Lãi gộp bán {Money(row.SalesGrossProfit)}")).ToArray();
        var shifts = snapshot.CashByShift?.Select(row => new FinancialReportListRow(row.PosSessionId.ToString(),
            $"Thu {Money(row.CashReceivedAmount)} · Chi hoàn {Money(row.CashRefundPaidAmount)} · Biến động {Money(row.CashNetMovement)}"))
            .ToArray();
        Lists =
        [
            new("Bán chạy theo số lượng", productsByQuantity, "Chưa có sản phẩm bán trong kỳ."),
            new("Bán chạy theo doanh thu", productsByRevenue, "Chưa có sản phẩm bán trong kỳ."),
            new("Hiệu suất người bán", cashiers, "Chưa có số liệu người bán trong kỳ."),
            new("Tiền mặt theo ca (trong kỳ)", shifts ?? [], shifts is null ? Unavailable : "Chưa có ca phát sinh tiền mặt.")
        ];
        IsEmpty = snapshot.Summary.AllocationMismatchOrderCount == 0
            && Summary.All(section => section.Values.All(value => value.Amount == 0))
            && snapshot.Daily.All(day => day.Totals.AllocationMismatchOrderCount == 0)
            && Daily.All(day => day.Sections.All(section => section.Values.All(value => value.Amount == 0)))
            && productsByQuantity.Length == 0 && productsByRevenue.Length == 0 && cashiers.Length == 0
            && shifts is { Length: 0 };

        AllocationNotice = snapshot.Summary.AllocationMismatchOrderCount == 0 ? string.Empty
            : $"{Count(snapshot.Summary.AllocationMismatchOrderCount)} đơn có chênh lệch phân bổ dòng hàng.";
    }

    public string Reference { get; }
    public string AllocationNotice { get; }
    public IReadOnlyList<FinancialReportSection> Summary { get; }
    public FinancialReportSection SalesSection => Summary[0];
    public FinancialReportSection CashFlowSection => Summary[1];
    public FinancialReportSection CostProfitSection => Summary[2];
    public FinancialReportSection WarrantySection => Summary[3];
    public FinancialReportKpiCard NetSalesKpi { get; }
    public FinancialReportKpiCard NetProfitKpi { get; }
    public FinancialReportKpiCard CashFlowKpi { get; }
    public FinancialReportKpiCard WarrantyAndOrdersKpi { get; }
    public IReadOnlyList<FinancialReportDayRow> Daily { get; }
    public IReadOnlyList<FinancialReportList> Lists { get; }
    public FinancialReportList TopQuantityList => Lists[0];
    public FinancialReportList TopRevenueList => Lists[1];
    public FinancialReportList CashierList => Lists[2];
    public FinancialReportList ShiftList => Lists[3];
    public bool IsEmpty { get; }

    internal static string Money(decimal? amount) => amount is { } value
        ? $"{value.ToString("#,0.##", Vietnamese)} ₫"
        : Unavailable;

    private static string Count(long value) => value.ToString("N0", Vietnamese);

    private static IReadOnlyList<FinancialReportSection> Totals(FinancialReportTotals totals) =>
    [
        new("Bán, hoàn và thuế",
        [
            new("Đơn đã giao", totals.CompletedOrderCount, Count(totals.CompletedOrderCount)),
            new("Số lượng bán", totals.Quantity, Count(totals.Quantity)),
            MoneyValue("Tiền hàng bán chưa thuế", totals.SalesExcludingTax),
            MoneyValue("Tiền hàng hoàn chưa thuế", totals.RefundedSalesExcludingTax),
            MoneyValue("Doanh thu thuần chưa thuế", totals.NetSalesExcludingTax),
            MoneyValue("Thuế bán", totals.SalesTax),
            MoneyValue("Thuế hoàn", totals.RefundTaxAmount)
        ]),
        new("Dòng tiền",
        [
            MoneyValue("Tiền đã thu", totals.PaymentReceivedAmount),
            MoneyValue("Tiền đã chi hoàn", totals.RefundPaidAmount),
            MoneyValue("Thu tiền mặt", totals.CashReceivedAmount),
            MoneyValue("Chi hoàn tiền mặt", totals.CashRefundPaidAmount),
            MoneyValue("Thu VietQR", totals.VietQrReceivedAmount),
            MoneyValue("Chi hoàn VietQR", totals.VietQrRefundPaidAmount)
        ]),
        new("Giá vốn và lãi gộp",
        [
            MoneyValue("Giá vốn bán", totals.CostOfGoodsSold),
            MoneyValue("Giá vốn hàng nhập lại", totals.RestockedCost),
            MoneyValue("Giá vốn thuần", totals.NetCostOfGoodsSold),
            MoneyValue("Lãi gộp bán", totals.SalesGrossProfit),
            MoneyValue("Lãi gộp sau hoàn", totals.NetGrossProfit)
        ]),
        new("Bảo hành", [MoneyValue("Chi phí máy thay bảo hành", totals.WarrantyCost)])
    ];

    private static FinancialReportValue MoneyValue(string label, decimal? amount) => new(label, amount, Money(amount));
}

public sealed record FinancialReportSection(string Title, IReadOnlyList<FinancialReportValue> Values);
public sealed record FinancialReportValue(string Label, decimal? Amount, string Text);
public sealed record FinancialReportKpiCard(string Title, string Value, string Detail);
public sealed record FinancialReportDayRow(string Date, string Sales, string Refunds, IReadOnlyList<FinancialReportSection> Sections,
    string NetSales = "", string NetProfit = "", string OrderCount = "")
{
    public FinancialReportSection SalesSection => Sections[0];
    public FinancialReportSection CashFlowSection => Sections[1];
    public FinancialReportSection CostProfitSection => Sections[2];
    public FinancialReportSection WarrantySection => Sections[3];
}
public sealed record FinancialReportList(string Title, IReadOnlyList<FinancialReportListRow> Rows, string EmptyMessage)
{
    public bool IsEmpty => Rows.Count == 0;
}
public sealed record FinancialReportListRow(string Title, string Figures);
