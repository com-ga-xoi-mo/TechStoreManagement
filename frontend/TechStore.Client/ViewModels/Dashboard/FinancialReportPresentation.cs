using System.Globalization;
using TechStore.Client.Controls.Charts;
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

        BuildChartAndLayoutModels(snapshot);
    }

    private void BuildChartAndLayoutModels(FinancialReportSnapshot snapshot)
    {
        var totals = snapshot.Summary;
        ChartDays = snapshot.Daily.Select(day =>
        {
            var t = day.Totals;
            var date = day.Date.ToString("dd/MM/yyyy", Vietnamese);
            return new FinancialChartDay(day.Date.ToString("dd/MM", Vietnamese), t.SalesExcludingTax, t.RefundedSalesExcludingTax,
                t.NetGrossProfit, t.CashReceivedAmount, t.VietQrReceivedAmount, t.RefundPaidAmount,
                $"{date}: Hàng bán {Money(t.SalesExcludingTax)}, hàng hoàn {Money(t.RefundedSalesExcludingTax)}, lãi gộp sau hoàn {Money(t.NetGrossProfit)}",
                $"{date}: Thu tiền mặt {Money(t.CashReceivedAmount)}, thu VietQR {Money(t.VietQrReceivedAmount)}, tiền đã chi hoàn {Money(t.RefundPaidAmount)}");
        }).ToArray();

        GrossMarginPercent = totals.NetGrossProfit is { } profit && totals.NetSalesExcludingTax is { } net && net > 0m
            ? Math.Round(profit / net * 100m, 1)
            : null;
        var margin = GrossMarginPercent is { } percent
            ? $"Biên lãi gộp {percent.ToString("0.#", Vietnamese)}%"
            : "Chưa tính được biên lãi gộp";
        NetSalesHeadline = new("Doanh thu thuần chưa thuế", Money(totals.NetSalesExcludingTax),
            $"{Count(totals.CompletedOrderCount)} đơn đã giao · {Count(totals.Quantity)} sản phẩm");
        GrossProfitHeadline = new("Lãi gộp sau hoàn", Money(totals.NetGrossProfit), margin);
        ReceivedHeadline = new("Tiền đã thu", Money(totals.PaymentReceivedAmount),
            $"Tiền đã chi hoàn {Money(totals.RefundPaidAmount)}");
        WarrantyHeadline = new("Chi phí máy thay bảo hành", Money(totals.WarrantyCost),
            "Hiển thị riêng, tách khỏi lãi gộp"
            + (totals.AllocationMismatchOrderCount > 0 ? $" · {Count(totals.AllocationMismatchOrderCount)} đơn lệch phân bổ" : string.Empty));

        var donut = FinancialDonutLayout.Calculate(totals.CashReceivedAmount, totals.VietQrReceivedAmount,
            totals.PaymentReceivedAmount, 0, 0, 1);
        Channels = new FinancialReportChannels(totals.CashReceivedAmount, totals.VietQrReceivedAmount, totals.PaymentReceivedAmount,
            "Tiền đã thu", Money(totals.PaymentReceivedAmount),
            donut.IsNeutral ? "Tiền mặt —" : $"Tiền mặt {donut.CashPercent.ToString("0.#", Vietnamese)}%",
            donut.IsNeutral ? "VietQR —" : $"VietQR {donut.QrPercent.ToString("0.#", Vietnamese)}%",
            donut.UnassignedAmount is { } unassigned
                ? $"Chưa phân loại kênh: {Money(unassigned)} (tiền đã thu lớn hơn tiền mặt cộng VietQR)"
                : string.Empty,
            [
                MoneyValue("Thu tiền mặt", totals.CashReceivedAmount),
                MoneyValue("Chi hoàn tiền mặt", totals.CashRefundPaidAmount),
                MoneyValue("Thu VietQR", totals.VietQrReceivedAmount),
                MoneyValue("Chi hoàn VietQR", totals.VietQrRefundPaidAmount),
                MoneyValue("Tiền đã thu", totals.PaymentReceivedAmount),
                MoneyValue("Tiền đã chi hoàn", totals.RefundPaidAmount)
            ]);

        SalesStep = Step("", "Tiền hàng bán chưa thuế", totals.SalesExcludingTax);
        RefundStep = Step("−", "Tiền hàng hoàn chưa thuế", totals.RefundedSalesExcludingTax);
        NetSalesStep = Step("=", "Doanh thu thuần chưa thuế", totals.NetSalesExcludingTax);
        CostStep = Step("−", "Giá vốn thuần", totals.NetCostOfGoodsSold);
        GrossProfitStep = Step("=", "Lãi gộp sau hoàn", totals.NetGrossProfit);
        WarrantyStep = Step("", "Máy thay bảo hành", totals.WarrantyCost, "Tính riêng, không nằm trong chuỗi lãi gộp");
        ProfitSteps = [SalesStep, RefundStep, NetSalesStep, CostStep, GrossProfitStep];

        OrdersTaxSection = new("Đơn hàng & Thuế",
        [
            new("Đơn đã giao", totals.CompletedOrderCount, Count(totals.CompletedOrderCount)),
            new("Số lượng bán", totals.Quantity, Count(totals.Quantity)),
            MoneyValue("Thuế bán", totals.SalesTax),
            MoneyValue("Thuế hoàn", totals.RefundTaxAmount)
        ]);
        CostSupportSection = new("Giá vốn bán, nhập lại & Lãi bán",
        [
            MoneyValue("Giá vốn bán", totals.CostOfGoodsSold),
            MoneyValue("Giá vốn hàng nhập lại", totals.RestockedCost),
            MoneyValue("Lãi gộp bán", totals.SalesGrossProfit),
            MoneyValue("Chi phí máy thay bảo hành", totals.WarrantyCost)
        ]);

        RankedLists =
        [
            Ranked(Lists[0], snapshot.TopProductsByQuantity.Select(row => (decimal?)row.Quantity)),
            Ranked(Lists[1], snapshot.TopProductsByRevenue.Select(row => (decimal?)row.SalesExcludingTax)),
            Ranked(Lists[2], snapshot.CashierPerformance.Select(row => (decimal?)row.SalesExcludingTax)),
            Ranked(Lists[3], snapshot.CashByShift?.Select(row => row.CashReceivedAmount) ?? [])
        ];
    }

    private static FinancialReportStep Step(string connector, string label, decimal? amount, string note = "") =>
        new(connector, label, Money(amount), amount, note);

    // The bar of a row is its figure relative to the largest figure of the same list; the server's order is kept.
    private static FinancialReportRankedList Ranked(FinancialReportList list, IEnumerable<decimal?> values)
    {
        var figures = values.ToArray();
        var max = figures.Length == 0 ? 0m : figures.Max() ?? 0m;
        var rows = list.Rows.Select((row, index) =>
        {
            var value = index < figures.Length ? figures[index] : null;
            var ratio = max > 0m && value is { } amount && amount > 0m ? (double)Math.Min(1m, amount / max) : 0d;
            return new FinancialReportRankedRow(row.Title, row.Figures, ratio);
        }).ToArray();
        return new FinancialReportRankedList(list.Title, rows, list.EmptyMessage);
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

    /// <summary>Daily figures for the trend chart (every day of the range, zero days included, null kept as null).</summary>
    public IReadOnlyList<FinancialChartDay> ChartDays { get; private set; } = [];
    /// <summary>NetGrossProfit / NetSalesExcludingTax in percent, only when both are known and sales are above zero.</summary>
    public decimal? GrossMarginPercent { get; private set; }
    public FinancialReportHeadlineTile NetSalesHeadline { get; private set; } = null!;
    public FinancialReportHeadlineTile GrossProfitHeadline { get; private set; } = null!;
    public FinancialReportHeadlineTile ReceivedHeadline { get; private set; } = null!;
    public FinancialReportHeadlineTile WarrantyHeadline { get; private set; } = null!;
    public FinancialReportChannels Channels { get; private set; } = null!;
    public IReadOnlyList<FinancialReportStep> ProfitSteps { get; private set; } = [];
    public FinancialReportStep SalesStep { get; private set; } = null!;
    public FinancialReportStep RefundStep { get; private set; } = null!;
    public FinancialReportStep NetSalesStep { get; private set; } = null!;
    public FinancialReportStep CostStep { get; private set; } = null!;
    public FinancialReportStep GrossProfitStep { get; private set; } = null!;
    /// <summary>Shown next to, not inside, the gross profit chain.</summary>
    public FinancialReportStep WarrantyStep { get; private set; } = null!;
    public FinancialReportSection OrdersTaxSection { get; private set; } = null!;
    public FinancialReportSection CostSupportSection { get; private set; } = null!;
    public IReadOnlyList<FinancialReportRankedList> RankedLists { get; private set; } = [];
    public FinancialReportRankedList RankedQuantityList => RankedLists[0];
    public FinancialReportRankedList RankedRevenueList => RankedLists[1];
    public FinancialReportRankedList RankedCashierList => RankedLists[2];
    public FinancialReportRankedList RankedShiftList => RankedLists[3];

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
public sealed record FinancialReportHeadlineTile(string Title, string Value, string Detail);
public sealed record FinancialReportStep(string Connector, string Label, string Text, decimal? Amount, string Note = "");
public sealed record FinancialReportRankedRow(string Title, string Figures, double Ratio);
public sealed record FinancialReportRankedList(string Title, IReadOnlyList<FinancialReportRankedRow> Rows, string EmptyMessage)
{
    public bool IsEmpty => Rows.Count == 0;
}

/// <summary>Cash / VietQR split of the money received; shares are display-only and never feed other figures.</summary>
public sealed record FinancialReportChannels(
    decimal? Cash,
    decimal? Qr,
    decimal? Total,
    string CenterCaption,
    string CenterValue,
    string CashShareText,
    string QrShareText,
    string UnassignedNote,
    IReadOnlyList<FinancialReportValue> Figures)
{
    public FinancialDonutLayout Donut(double centerX, double centerY, double radius) =>
        FinancialDonutLayout.Calculate(Cash, Qr, Total, centerX, centerY, radius);

    public string Description =>
        $"{CenterCaption} {CenterValue}. {CashShareText}. {QrShareText}."
        + (UnassignedNote.Length > 0 ? $" {UnassignedNote}." : string.Empty);
}
