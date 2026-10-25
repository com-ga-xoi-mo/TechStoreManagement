using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Features.Inventory.ViewModels;
using TechStore.Shared.DTOs;
using Uno.Extensions.Markup;
using static TechStore.Client.Features.Categories.Pages.CategoryWorkspaceStyles;

namespace TechStore.Client.Features.Inventory.Dialogs;

public sealed class StockDetailHistoryDialog : ContentDialog
{
    public StockDetailHistoryDialogViewModel ViewModel { get; }

    public StockDetailHistoryDialog(StockDetailHistoryDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        RequestedTheme = ElementTheme.Default;
        FontFamily = Typeface;
        DefaultButton = ContentDialogButton.None;
        PrimaryButtonText = string.Empty;
        SecondaryButtonText = string.Empty;
        CloseButtonText = string.Empty;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(16);
        Width = 820;
        MaxWidth = 820;
        Resources["ContentDialogMaxWidth"] = 820d;
        Resources["ContentDialogPadding"] = new Thickness(0);
        Resources["ContentDialogCommandSpace"] = 0d;
        Background = Brush("#F8FAFC");
        Content = BuildLayout();
        ViewModel.RequestClose += OnRequestClose;
        ViewModel.AdjustmentRequested += OnAdjustmentRequested;
        Loaded += (_, _) => _ = ViewModel.LoadCommand.ExecuteAsync(null);
        Closed += (_, _) =>
        {
            ViewModel.RequestClose -= OnRequestClose;
            ViewModel.AdjustmentRequested -= OnAdjustmentRequested;
        };
    }

    private void OnRequestClose(object? sender, EventArgs e) => Hide();
    private void OnAdjustmentRequested(object? sender, EventArgs e) => Hide();

    private Grid BuildLayout()
    {
        var root = new Grid { Width = 820, MaxWidth = 820, Background = Brush("#F7F9FB") };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(BuildHeader());
        var scroll = new ScrollViewer { MaxHeight = 650, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = BuildBody() };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        var footer = BuildFooter(); Grid.SetRow(footer, 2); root.Children.Add(footer);
        return root;
    }

    private Grid BuildHeader()
    {
        var iconBox = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), Background = Brush("#E8F0FF"), Child = Icon("inventory", Primary, 20) };
        var titleLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center, Children = { iconBox, Text("Chi tiết tồn kho thiết bị", 20, Slate900, 600) } };
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(40, 4, 0, 0) };
        chips.Children.Add(Badge(BoundText(nameof(ViewModel.Sku), 13, Slate500, 600), "#E5E7EB"));
        chips.Children.Add(Text("·", 14, Slate500));
        chips.Children.Add(Badge(BoundText(nameof(ViewModel.CategoryName), 13, Slate500, 500), "#E5E7EB"));
        var copy = new StackPanel { Spacing = 0, Children = { titleLine, chips } };
        var close = Button(Icon("close", Slate500, 21), tooltip: "Đóng chi tiết tồn kho"); close.Command(x => x.Binding(() => ViewModel.CloseCommand));
        var header = new Grid { Padding = new Thickness(24, 16, 16, 16), Background = Brush("#FFFFFF") };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(copy); Grid.SetColumn(close, 1); header.Children.Add(close); return header;
    }

    private UIElement BuildBody() => new StackPanel
    {
        Padding = new Thickness(24, 16, 24, 16), Spacing = 16,
        Children = { BuildOverviewSection(), BuildHistoryCard() }
    };

    private Border BuildOverviewSection()
    {
        var content = new StackPanel { Spacing = 16 };
        content.Children.Add(BuildProductCard());
        content.Children.Add(BuildMetricCards());
        return new Border
        {
            Background = Brush("#FFFFFF"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = content
        };
    }

    private Border BuildProductCard()
    {
        var identity = new Grid { ColumnSpacing = 12 };
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        identity.Children.Add(new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(10), Background = Brush("#E0E3E5"), Child = Icon("phone", Slate500, 34) });
        var meta = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        meta.Children.Add(BoundText(nameof(ViewModel.ProductName), 18, Slate900, 600));
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Badge(BoundText(nameof(ViewModel.Sku), 13, Slate500, 600), "#E5E7EB"), Text("·", 14, Slate500), BoundText(nameof(ViewModel.CategoryName), 13, Slate500) } };
        meta.Children.Add(line); Grid.SetColumn(meta, 1); identity.Children.Add(meta);
        return new Border { Background = Brush("#F2F4F6"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = identity };
    }

    private Grid BuildMetricCards()
    {
        var metrics = new Grid { ColumnSpacing = 10 };
        for (var i = 0; i < 3; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.Children.Add(MetricCard("Tổng tồn thực tế", nameof(ViewModel.Quantity), "Sản phẩm vật lý tại kho", "inventory", Slate500, 0));
        metrics.Children.Add(MetricCard("Sẵn sàng bán", nameof(ViewModel.AvailableQuantity), "Còn hàng", "check_circle", Primary, 1));
        metrics.Children.Add(MetricCard("Đang giữ chỗ", nameof(ViewModel.ReservedQuantity), "Đơn hàng đang giữ", "lock", Slate500, 2));
        return metrics;
    }

    private Border MetricCard(string label, string valuePath, string footnote, string icon, string accent, int column)
    {
        var top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(Text(label, 14, Slate500, 500)); var iconElement = Icon(icon, accent, 20); Grid.SetColumn(iconElement, 1); top.Children.Add(iconElement);
        var footnoteView = footnote == "Sản phẩm vật lý tại kho"
            ? new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Children = { BoundText(valuePath, 12, Slate500, 500), Text("sản phẩm vật lý tại kho", 12, Slate500, 500) } }
            : new StackPanel { Orientation = Orientation.Horizontal, Children = { Text(footnote, 12, Slate500, 500) } };
        var stack = new StackPanel { Spacing = 6, Children = { top, BoundMetricValue(valuePath, accent == Primary ? Primary : Slate900), footnoteView } };
        var card = new Border { Background = Brush("#FFFFFF"), BorderBrush = Brush("#E0E3E5"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 10, 10, 10), Child = stack };
        Grid.SetColumn(card, column); return card;
    }

    private StackPanel BoundMetricValue(string path, string color)
    {
        var value = BoundText(path, 28, color, 700);
        var unit = Text("sản phẩm", 13, Slate500, 400);
        return new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Spacing = 8, Children = { value, unit } };
    }

    private Border BuildHistoryCard()
    {
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center, Children = { Text("Lịch sử biến động gần đây", 20, Slate900, 700), Badge(BoundText(nameof(ViewModel.MovementCountText), 12, Slate500, 600), "#DCE8FF") } };
        var history = new StackPanel { Spacing = 12, Children = { heading } };
        history.Children.Add(new Border { BorderBrush = Brush(Slate200), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = BuildMovementTable() });
        var serials = Button(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { Icon("qr_code", Primary, 16), BoundText(nameof(ViewModel.SerialsLinkText), 12, Primary, 600), Icon("right", Primary, 16) } });
        serials.HorizontalAlignment = HorizontalAlignment.Left;
        serials.Command(x => x.Binding(() => ViewModel.RequestSerialsCommand));
        var serialFooter = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        serialFooter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        serialFooter.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        serialFooter.Children.Add(serials);
        var shown = Text("", 12, Slate500);
        shown.Text(x => x.Binding(() => ViewModel.MovementSummaryText));
        Grid.SetColumn(shown, 1); serialFooter.Children.Add(shown);
        history.Children.Add(serialFooter);
        return new Border { Background = Brush("#FFFFFF"), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Child = history };
    }

    private StackPanel BuildMovementTable()
    {
        var table = new StackPanel { Children = { MovementHeader() } };
        var list = new ItemsControl(); list.ItemsSource(x => x.Binding(() => ViewModel.RecentMovements)); list.ItemTemplate<InventoryMovementDto>(_ => MovementTemplate()); table.Children.Add(list); return table;
    }

    private static Grid MovementHeader()
    {
        var row = MovementGrid(); row.Background = Brush("#E5E7E9"); row.Padding = new Thickness(10);
        var labels = new[] { "NGÀY GIỜ", "LOẠI", "SỐ LƯỢNG", "TỒN SAU", "GHI CHÚ" };
        for (var i = 0; i < labels.Length; i++) { var text = Text(labels[i], 11, Slate500, 700); text.CharacterSpacing = 35; Grid.SetColumn(text, i); row.Children.Add(text); }
        return row;
    }

    private static Grid MovementTemplate()
    {
        var row = MovementGrid(); row.Padding = new Thickness(10); row.BorderBrush = Brush("#E5E7EB"); row.BorderThickness = new Thickness(0, 0, 0, 1);
        AddBound(row, nameof(InventoryMovementDto.CreatedAt), 0, new MovementDateConverter());
        AddBound(row, nameof(InventoryMovementDto.MovementType), 1, new MovementTypeConverter());
        AddBound(row, nameof(InventoryMovementDto.QuantityChange), 2, new MovementQuantityConverter(), new MovementQuantityBrushConverter());
        AddBound(row, nameof(InventoryMovementDto.QuantityAfter), 3, new QuantityAfterConverter()); AddBound(row, nameof(InventoryMovementDto.Note), 4); return row;
    }

    private static Grid MovementGrid()
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.3, GridUnitType.Star) }); return row;
    }

    private static void AddBound(Grid row, string path, int column, IValueConverter? converter = null, IValueConverter? foregroundConverter = null)
    {
        var text = Text("—", 13, Slate500);
        if (converter is null)
            text.Text(x => x.Binding<InventoryMovementDto>(path));
        else
            text.Text(x => x.Binding<InventoryMovementDto>(path).Converter(converter));
        if (foregroundConverter is not null)
            text.Foreground(x => x.Binding<InventoryMovementDto>(path).Converter(foregroundConverter));
        Grid.SetColumn(text, column); row.Children.Add(text);
    }

    private TextBlock BoundText(string path, double size, string color, ushort weight = 400)
    {
        var text = Text("", size, color, weight);
        text.Text(x => x.Binding<StockDetailHistoryDialogViewModel>(path));
        return text;
    }

    private Grid BuildFooter()
    {
        var footer = new Grid { Padding = new Thickness(24, 8, 24, 8), Background = Brush("#F2F4F6"), BorderBrush = Brush("#E0E3E5"), BorderThickness = new Thickness(0, 1, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); footer.Children.Add(BoundText(nameof(ViewModel.LastUpdatedText), 13, Slate500));
        var adjust = Button(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Icon("tune", Slate900, 18), Text("Điều chỉnh tồn kho", 14, Slate900, 600) } }); adjust.Command(x => x.Binding(() => ViewModel.RequestAdjustmentCommand));
        var close = Button(Text("Đóng", 14, "#FFFFFF", 600), primary: true); close.Command(x => x.Binding(() => ViewModel.CloseCommand));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { adjust, close } }; Grid.SetColumn(buttons, 1); footer.Children.Add(buttons); return footer;
    }
}

internal sealed class MovementDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is DateTime date ? date.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "—";
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed class MovementTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value?.ToString() switch { "Sale" => "Bán hàng", "CustomerReturn" => "Trả hàng", "Adjustment" => "Điều chỉnh", "PurchaseReceipt" => "Nhập kho", _ => value?.ToString() ?? "—" };
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed class MovementQuantityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is int quantity ? $"{(quantity >= 0 ? "+" : string.Empty)}{quantity:N0} sản phẩm" : "—";
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed class QuantityAfterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is int quantity ? $"{quantity:N0} sản phẩm" : "—";
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed class MovementQuantityBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is int quantity && quantity < 0 ? Brush("#DC2626") : Brush("#059669");
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
