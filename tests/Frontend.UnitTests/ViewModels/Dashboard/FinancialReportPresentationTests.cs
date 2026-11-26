using TechStore.Client.Models.Analytics;
using TechStore.Client.ViewModels.Dashboard;
using TechStore.Shared.DTOs;

namespace TechStore.Client.UnitTests.ViewModels.Dashboard;

public sealed class FinancialReportPresentationTests
{
    private static readonly DateOnly Date = new(2024, 10, 14);

    private static FinancialReportTotals Totals(
        decimal? netSales = 1000m, decimal? netProfit = 250m, decimal? cash = 300m, decimal? qr = 100m,
        decimal received = 400m, long mismatch = 0) =>
        new(3, 5, mismatch, 1200m, 90m, received, 700m, 500m,
            RefundPaidAmount: 20m, RefundedSalesExcludingTax: 200m, RestockedCost: 100m, WarrantyCost: 40m,
            NetSalesExcludingTax: netSales, NetCostOfGoodsSold: 600m, NetGrossProfit: netProfit, RefundTaxAmount: 15m,
            CashReceivedAmount: cash, CashRefundPaidAmount: 5m, VietQrReceivedAmount: qr, VietQrRefundPaidAmount: 15m);

    private static FinancialReportSnapshot Snapshot(FinancialReportTotals summary, FinancialReportDay[]? days = null,
        FinancialReportProductDto[]? byRevenue = null, FinancialReportCashShift[]? shifts = null, bool nullShifts = false) =>
        new(new FinancialReportRange(Date, Date), new DateTimeOffset(2024, 10, 14, 2, 0, 0, TimeSpan.Zero),
            days ?? [new FinancialReportDay(Date, summary)], summary,
            byRevenue ?? [], byRevenue ?? [], [], nullShifts ? null : shifts ?? []);

    [Fact]
    public void GrossMargin_IsComputedOnlyWhenBothFiguresAreKnownAndSalesArePositive()
    {
        // Act
        var known = new FinancialReportPresentation(Snapshot(Totals()));
        var noSales = new FinancialReportPresentation(Snapshot(Totals(netSales: null)));
        var zeroSales = new FinancialReportPresentation(Snapshot(Totals(netSales: 0m)));
        var noProfit = new FinancialReportPresentation(Snapshot(Totals(netProfit: null)));

        // Assert
        Assert.Equal(25m, known.GrossMarginPercent);
        Assert.Equal("Biên lãi gộp 25%", known.GrossProfitHeadline.Detail);
        Assert.Equal("250 ₫", known.GrossProfitHeadline.Value);
        Assert.All([noSales, zeroSales, noProfit], view =>
        {
            Assert.Null(view.GrossMarginPercent);
            Assert.Equal("Chưa tính được biên lãi gộp", view.GrossProfitHeadline.Detail);
        });
        Assert.Equal("Chưa tính được", noProfit.GrossProfitHeadline.Value);
    }

    [Fact]
    public void Headlines_ShowTheFourRequestedFiguresAndKeepLegacyKpis()
    {
        // Act
        var view = new FinancialReportPresentation(Snapshot(Totals(mismatch: 2)));

        // Assert
        Assert.Equal("Doanh thu thuần chưa thuế", view.NetSalesHeadline.Title);
        Assert.Equal("1.000 ₫", view.NetSalesHeadline.Value);
        Assert.Equal("3 đơn đã giao · 5 sản phẩm", view.NetSalesHeadline.Detail);
        Assert.Equal("Tiền đã thu", view.ReceivedHeadline.Title);
        Assert.Equal("Tiền đã chi hoàn 20 ₫", view.ReceivedHeadline.Detail);
        Assert.Equal("Chi phí máy thay bảo hành", view.WarrantyHeadline.Title);
        Assert.Contains("tách khỏi lãi gộp", view.WarrantyHeadline.Detail);
        Assert.Contains("2 đơn lệch phân bổ", view.WarrantyHeadline.Detail);
        Assert.DoesNotContain("lệch phân bổ", new FinancialReportPresentation(Snapshot(Totals())).WarrantyHeadline.Detail);
    }

    [Fact]
    public void Channels_ShowSharesSixFiguresAndUnassignedNote()
    {
        // Act
        var view = new FinancialReportPresentation(Snapshot(Totals(received: 450m)));

        // Assert
        var channels = view.Channels;
        Assert.Equal("Tiền mặt 75%", channels.CashShareText);
        Assert.Equal("VietQR 25%", channels.QrShareText);
        Assert.Equal("450 ₫", channels.CenterValue);
        Assert.Contains("50 ₫", channels.UnassignedNote);
        Assert.Equal(
            ["Thu tiền mặt", "Chi hoàn tiền mặt", "Thu VietQR", "Chi hoàn VietQR", "Tiền đã thu", "Tiền đã chi hoàn"],
            channels.Figures.Select(value => value.Label));
        Assert.Equal(["300 ₫", "5 ₫", "100 ₫", "15 ₫", "450 ₫", "20 ₫"], channels.Figures.Select(value => value.Text));
        Assert.False(channels.Donut(0, 0, 10).IsNeutral);
        Assert.Empty(new FinancialReportPresentation(Snapshot(Totals())).Channels.UnassignedNote);
    }

    [Theory]
    [InlineData(null, 100)]
    [InlineData(100, null)]
    public void Channels_AreNeutralWhenAChannelIsNotComputed(int? cash, int? qr)
    {
        // Act
        var channels = new FinancialReportPresentation(Snapshot(Totals(cash: cash, qr: qr))).Channels;

        // Assert
        Assert.True(channels.Donut(0, 0, 10).IsNeutral);
        Assert.EndsWith("—", channels.CashShareText);
        Assert.EndsWith("—", channels.QrShareText);
        Assert.Empty(channels.UnassignedNote);
        Assert.Contains("Chưa tính được", channels.Figures.Select(value => value.Text));
    }

    [Fact]
    public void ProfitStepsAndSupportSections_UseTheRequestedLabelsInOrder()
    {
        // Act
        var view = new FinancialReportPresentation(Snapshot(Totals()));

        // Assert
        Assert.Equal(
            ["Tiền hàng bán chưa thuế", "Tiền hàng hoàn chưa thuế", "Doanh thu thuần chưa thuế", "Giá vốn thuần", "Lãi gộp sau hoàn"],
            view.ProfitSteps.Select(step => step.Label));
        Assert.Equal("Máy thay bảo hành", view.WarrantyStep.Label);
        Assert.DoesNotContain(view.WarrantyStep, view.ProfitSteps);
        Assert.Equal("Đơn hàng & Thuế", view.OrdersTaxSection.Title);
        Assert.Equal(["Đơn đã giao", "Số lượng bán", "Thuế bán", "Thuế hoàn"], view.OrdersTaxSection.Values.Select(value => value.Label));
        Assert.Equal("Giá vốn bán, nhập lại & Lãi bán", view.CostSupportSection.Title);
        Assert.Equal(["Giá vốn bán", "Giá vốn hàng nhập lại", "Lãi gộp bán", "Chi phí máy thay bảo hành"],
            view.CostSupportSection.Values.Select(value => value.Label));
    }

    [Fact]
    public void ChartDays_KeepEveryDayZeroDaysAndUnknownFigures()
    {
        // Arrange
        var zero = Totals(netSales: 0m, netProfit: 0m, cash: 0m, qr: 0m, received: 0m);
        var unknown = Totals() with { CashReceivedAmount = null, NetGrossProfit = null };
        var days = new[]
        {
            new FinancialReportDay(Date, Totals()),
            new FinancialReportDay(Date.AddDays(1), zero),
            new FinancialReportDay(Date.AddDays(2), unknown)
        };

        // Act
        var chart = new FinancialReportPresentation(Snapshot(Totals(), days)).ChartDays;

        // Assert
        Assert.Equal(["14/10", "15/10", "16/10"], chart.Select(day => day.Label));
        Assert.Equal(1200m, chart[0].Sales);
        Assert.Equal(0m, chart[1].NetProfit);
        Assert.Null(chart[2].NetProfit);
        Assert.Null(chart[2].Cash);
        Assert.Contains("Chưa tính được", chart[2].CashFlowDescription);
    }

    [Fact]
    public void RankedLists_KeepServerOrderAndFiguresWithRatiosRelativeToTheLargest()
    {
        // Arrange
        var products = new[]
        {
            new FinancialReportProductDto(Guid.NewGuid(), "A", 5, 100m),
            new FinancialReportProductDto(Guid.NewGuid(), "B", 1, 400m),
            new FinancialReportProductDto(Guid.NewGuid(), "C", 2, 200m)
        };

        // Act
        var view = new FinancialReportPresentation(Snapshot(Totals(), byRevenue: products));

        // Assert
        var ranked = view.RankedRevenueList;
        Assert.Equal(["A", "B", "C"], ranked.Rows.Select(row => row.Title));
        Assert.Equal(view.TopRevenueList.Rows.Select(row => row.Figures), ranked.Rows.Select(row => row.Figures));
        Assert.Equal([0.25, 1, 0.5], ranked.Rows.Select(row => row.Ratio));
        Assert.Equal([1, 0.2, 0.4], view.RankedQuantityList.Rows.Select(row => row.Ratio));
        Assert.Equal(view.TopQuantityList.Title, view.RankedQuantityList.Title);
        Assert.Equal(4, view.RankedLists.Count);
    }

    [Fact]
    public void RankedShiftList_KeepsUnavailableMessageAndZeroRatioForUnknownCash()
    {
        // Arrange
        var shifts = new[]
        {
            new FinancialReportCashShift(Guid.NewGuid(), 50m, 0m, 50m),
            new FinancialReportCashShift(Guid.NewGuid(), null, null, null)
        };

        // Act
        var unavailable = new FinancialReportPresentation(Snapshot(Totals(), nullShifts: true)).RankedShiftList;
        var present = new FinancialReportPresentation(Snapshot(Totals(), shifts: shifts)).RankedShiftList;

        // Assert
        Assert.True(unavailable.IsEmpty);
        Assert.Equal("Chưa tính được", unavailable.EmptyMessage);
        Assert.Equal([1, 0], present.Rows.Select(row => row.Ratio));
        Assert.Contains("Chưa tính được", present.Rows[1].Figures);
    }
}
