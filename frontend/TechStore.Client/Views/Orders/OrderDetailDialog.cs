using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Orders;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Styles;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Orders.OrderViewParts;

namespace TechStore.Client.Views.Orders;

/// <summary>
/// Read-only view of one order: header facts, every line with its IMEI/serial, and the totals the server computed.
/// It has no actions on purpose, so a confirmation dialog can never end up stacked on top of it.
/// </summary>
public sealed class OrderDetailDialog : ContentDialog
{
    public OrderDetailDialog(OrderDetail order)
    {
        ArgumentNullException.ThrowIfNull(order);

        Title = $"Chi tiết đơn {order.OrderCode}";
        CloseButtonText = "Đóng";
        DefaultButton = ContentDialogButton.Close;
        Content = new ScrollViewer()
            .MaxHeight(520)
            .MinWidth(640)
            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .Content(
                new StackPanel()
                    .Spacing(16)
                    .Padding(new Thickness(0, 0, 12, 0))
                    .Children(
                        BuildFacts(order),
                        BuildLines(order),
                        BuildTotals(order)));
    }

    private static UIElement BuildFacts(OrderDetail order)
    {
        var facts = new Grid()
            .ColumnSpacing(16)
            .RowSpacing(6)
            .ColumnDefinitions(
                new ColumnDefinition().Width(new GridLength(150, GridUnitType.Pixel)),
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)));

        var rows = new List<(string Label, string Value)>
        {
            ("Phương thức thanh toán", OrderPresentation.PaymentLabel(order.PaymentMethod)),
            ("Khách hàng", CustomerText(order)),
            ("Tạo lúc", OrderPresentation.FormatDateTime(order.CreatedAt)),
            ("Thanh toán lúc", OrderPresentation.FormatOptionalDateTime(order.PaidAt)),
            ("Hoàn tất lúc", OrderPresentation.FormatOptionalDateTime(order.CompletedAt))
        };
        if (!string.IsNullOrWhiteSpace(order.Notes)) rows.Add(("Ghi chú", order.Notes.Trim()));

        for (var i = 0; i < rows.Count; i++)
        {
            facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            facts.Children.Add(Caption(rows[i].Label).Grid(row: i, column: 0));
            facts.Children.Add(Body(rows[i].Value).Grid(row: i, column: 1));
        }

        return new StackPanel()
            .Spacing(10)
            .Children(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(10)
                    .Children(
                        new TextBlock().Style(AppTypography.Styles.SectionText).Text(order.OrderCode),
                        new StatusBadge()
                            .Label(OrderPresentation.StatusLabel(order.Status))
                            .Tone(OrderPresentation.StatusTone(order.Status))
                            .VerticalAlignment(VerticalAlignment.Center)),
                facts);
    }

    private static string CustomerText(OrderDetail order)
    {
        var name = OrderPresentation.CustomerDisplay(order.CustomerName);
        return string.IsNullOrWhiteSpace(order.CustomerPhone) ? name : $"{name} · {order.CustomerPhone.Trim()}";
    }

    private static UIElement BuildLines(OrderDetail order)
    {
        var lines = new StackPanel().Spacing(0);
        lines.Children.Add(
            new Border()
                .BorderBrush(AppColors.Brushes.Stroke)
                .BorderThickness(new Thickness(0, 0, 0, 1))
                .Padding(new Thickness(0, 0, 0, 6))
                .Child(HeaderText($"CÁC DÒNG HÀNG ({order.Lines.Count})")));

        foreach (var line in order.Lines)
        {
            var title = string.IsNullOrWhiteSpace(line.VariantName) ? line.ProductName : $"{line.ProductName} · {line.VariantName}";
            var details = new StackPanel().Spacing(2).Children(
                Body(title).FontWeight(Microsoft.UI.Text.FontWeights.SemiBold),
                Caption(line.Sku));
            if (!string.IsNullOrWhiteSpace(line.Imei)) details.Children.Add(Caption($"IMEI: {line.Imei.Trim()}"));
            if (!string.IsNullOrWhiteSpace(line.SerialNumber)) details.Children.Add(Caption($"Serial: {line.SerialNumber.Trim()}"));

            // Right-align through TextAlignment: a right-aligned wrapping TextBlock measured to nothing on Skia.
            details.Children.Add(Body(OrderPresentation.FormatMoney(line.TotalPrice)).FontWeight(Microsoft.UI.Text.FontWeights.SemiBold));
            details.Children.Add(Caption(PriceBreakdown(line)));

            lines.Children.Add(
                new Border()
                    .BorderBrush(AppColors.Brushes.Stroke)
                    .BorderThickness(new Thickness(0, 0, 0, 1))
                    .Padding(new Thickness(0, 8, 0, 8))
                    .Child(details));
        }

        return lines;
    }

    private static string PriceBreakdown(OrderLine line)
    {
        var text = $"{line.Quantity:N0} × {OrderPresentation.FormatMoney(line.UnitPrice)}";
        return line.DiscountAmount > 0 ? $"{text} − {OrderPresentation.FormatMoney(line.DiscountAmount)}" : text;
    }

    private static UIElement BuildTotals(OrderDetail order)
    {
        var rows = new List<(string Label, decimal Value, bool Strong)>
        {
            ("Tạm tính", order.Subtotal, false),
            ("Giảm giá", order.DiscountAmount, false),
            ("Thuế", order.TaxAmount, false),
            ("Tổng cộng", order.TotalAmount, true)
        };

        // Same two-column layout as the facts above. Text placed in a right-hand column of this dialog was laid
        // out but never painted on Skia, so the amounts sit in the left-aligned value column instead.
        var totals = new Grid()
            .ColumnSpacing(16)
            .RowSpacing(6)
            .ColumnDefinitions(
                new ColumnDefinition().Width(new GridLength(150, GridUnitType.Pixel)),
                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)));
        for (var i = 0; i < rows.Count; i++)
        {
            totals.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = rows[i].Strong ? Body(rows[i].Label).FontWeight(Microsoft.UI.Text.FontWeights.SemiBold) : Caption(rows[i].Label);
            var value = Body(OrderPresentation.FormatMoney(rows[i].Value));
            if (rows[i].Strong) value.FontWeight(Microsoft.UI.Text.FontWeights.Bold);
            totals.Children.Add(label.Grid(row: i, column: 0));
            totals.Children.Add(value.Grid(row: i, column: 1));
        }

        return totals;
    }

    private static TextBlock Body(string text) =>
        new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.BodyText))
            .Foreground(AppColors.Brushes.Text)
            .TextWrapping(TextWrapping.Wrap)
            .Text(text);
}

/// <summary>Opens <see cref="OrderDetailDialog"/> through the Shell's dialog host so it follows the same rules as every modal.</summary>
public sealed class OrderDetailDialogPresenter(ContentDialogPresenter host) : IOrderDetailPresenter
{
    public async Task ShowAsync(OrderDetail order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        // False means another dialog is open or the Shell is not ready; the request is simply ignored.
        _ = await host.TryShowAsync(() => new OrderDetailDialog(order), cancellationToken);
    }
}
