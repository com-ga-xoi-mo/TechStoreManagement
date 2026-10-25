using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Features.Categories.Pages;
using TechStore.Client.Features.Inventory.ViewModels;
using Uno.Extensions.Markup;
using static TechStore.Client.Features.Categories.Pages.CategoryWorkspaceStyles;

namespace TechStore.Client.Features.Inventory.Dialogs;

public sealed class StockAdjustmentDialog : ContentDialog
{
    public StockAdjustmentDialogViewModel ViewModel { get; }

    public StockAdjustmentDialog(StockAdjustmentDialogViewModel viewModel)
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
        CornerRadius = new CornerRadius(8);
        Width = 480;
        MaxWidth = 480;
        Resources["ContentDialogMaxWidth"] = 480d;
        Resources["ContentDialogPadding"] = new Thickness(0);
        Resources["ContentDialogCommandSpace"] = 0d;
        Background = Brush("#FFFFFF");
        Content = BuildLayout();
        ViewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => ViewModel.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs e) => Hide();

    private Grid BuildLayout()
    {
        var root = new Grid { Width = 480, MaxWidth = 480, Background = Brush("#FFFFFF") };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(BuildHeader());
        var scroll = new ScrollViewer { MaxHeight = 620, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = BuildBody() };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        var footer = BuildFooter(); Grid.SetRow(footer, 2); root.Children.Add(footer);
        return root;
    }

    private Grid BuildHeader()
    {
        var header = new Grid { Padding = new Thickness(16, 12, 12, 8), BorderBrush = Brush("#E0E3E5"), BorderThickness = new Thickness(0, 0, 0, 1) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(Text("Điều chỉnh tồn kho", 16, Slate900, 600));
        var close = Button(Icon("close", Slate500, 20), tooltip: "Đóng điều chỉnh tồn kho"); close.Command = ViewModel.CloseCommand; Grid.SetColumn(close, 1); header.Children.Add(close);
        return header;
    }

    private UIElement BuildBody()
    {
        var body = new StackPanel { Padding = new Thickness(16), Spacing = 8 };
        body.Children.Add(BoundText(nameof(ViewModel.ProductName), 15, Slate900, 600));
        body.Children.Add(Badge(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { Icon("qr_code", Slate500, 14), BoundText(nameof(ViewModel.Sku), 13, Slate500, 600) } }, "#E5E7EB"));
        body.Children.Add(BuildCurrentStockCards());
        body.Children.Add(Text("Loại điều chỉnh", 13, Slate900, 600));
        body.Children.Add(BuildSegmentedControl());
        body.Children.Add(RequiredLabel("Số lượng"));
        body.Children.Add(BuildAmountInput());
        body.Children.Add(RequiredLabel("Lý do"));
        var reason = new TextBox { MinHeight = 64, MaxLength = 200, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, PlaceholderText = "Nhập lý do điều chỉnh kho..." };
        Bind(reason, TextBox.TextProperty, ViewModel, nameof(ViewModel.Reason), BindingMode.TwoWay); body.Children.Add(reason);
        body.Children.Add(Text("Lý do sẽ được ghi vào lịch sử biến động kho", 11, Slate500));
        body.Children.Add(BuildPreview());
        var error = Text("", 12, "#DC2626"); Bind(error, TextBlock.TextProperty, ViewModel, nameof(ViewModel.ErrorMessage)); Bind(error, VisibilityProperty, ViewModel, nameof(ViewModel.HasError), converter: new VisibilityConverter()); body.Children.Add(error);
        return body;
    }

    private static StackPanel RequiredLabel(string label)
    {
        var required = Text("*", 13, "#DC2626", 700);
        required.Margin = new Thickness(3, 0, 0, 0);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { Text(label, 13, Slate900, 600), required }
        };
    }

    private Grid BuildCurrentStockCards()
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(InfoCard("Tồn hiện tại", nameof(ViewModel.CurrentQuantity), 0)); grid.Children.Add(InfoCard("Đang giữ", nameof(ViewModel.ReservedQuantity), 1)); return grid;
    }

    private Border InfoCard(string label, string path, int column)
    {
        var value = BoundText(path, 16, Slate900, 700);
        var card = new Border { Background = Brush("#F1F3F5"), CornerRadius = new CornerRadius(5), Padding = new Thickness(12, 10, 12, 10), Child = new StackPanel { Spacing = 4, Children = { Text(label, 12, Slate500), value } } };
        Grid.SetColumn(card, column); return card;
    }

    private Grid BuildSegmentedControl()
    {
        var grid = new Grid
        {
            Background = Brush("#F2F4F6"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            ColumnSpacing = 4,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var add = Button(Text("Nhập thêm", 14, "#FFFFFF", 600));
        add.Height = 32;
        add.HorizontalAlignment = HorizontalAlignment.Stretch;
        add.HorizontalContentAlignment = HorizontalAlignment.Center;
        add.Padding = new Thickness(12, 0, 12, 0);
        add.Command = ViewModel.SetIncreaseCommand;
        var decrease = Button(Text("Giảm tồn", 14, Slate500, 600));
        decrease.Height = 32;
        decrease.HorizontalAlignment = HorizontalAlignment.Stretch;
        decrease.HorizontalContentAlignment = HorizontalAlignment.Center;
        decrease.Padding = new Thickness(12, 0, 12, 0);
        decrease.Command = ViewModel.SetDecreaseCommand;
        Bind(add, Control.BackgroundProperty, ViewModel, nameof(ViewModel.IsIncrease), converter: new SegmentBackgroundConverter(true)); Bind(add, Control.ForegroundProperty, ViewModel, nameof(ViewModel.IsIncrease), converter: new SegmentForegroundConverter(true));
        Bind(decrease, Control.BackgroundProperty, ViewModel, nameof(ViewModel.IsIncrease), converter: new SegmentBackgroundConverter(false)); Bind(decrease, Control.ForegroundProperty, ViewModel, nameof(ViewModel.IsIncrease), converter: new SegmentForegroundConverter(false));
        grid.Children.Add(add); Grid.SetColumn(decrease, 1); grid.Children.Add(decrease); return grid;
    }

    private Grid BuildAmountInput()
    {
        var amount = new NumberBox { Minimum = 0, Maximum = 1000000, Height = 40, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden, HorizontalContentAlignment = HorizontalAlignment.Left };
        Bind(amount, NumberBox.ValueProperty, ViewModel, nameof(ViewModel.Amount), BindingMode.TwoWay);
        var grid = new Grid(); grid.Children.Add(amount);
        var unit = Text("sản phẩm", 13, Slate500, 500); unit.HorizontalAlignment = HorizontalAlignment.Right; unit.Margin = new Thickness(0, 0, 14, 0); unit.IsHitTestVisible = false; grid.Children.Add(unit); return grid;
    }

    private Border BuildPreview() => new()
    {
        Background = Brush("#EAF2FF"), BorderBrush = Brush("#C9DDFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(14, 12, 14, 12),
        Child = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = GridLength.Auto } },
            Children = { Text("Tồn mới:", 14, Slate500, 600), BuildPreviewValue().Grid(column: 1) }
        }
    };

    private StackPanel BuildPreviewValue() => new()
    {
        Orientation = Orientation.Horizontal, Spacing = 6,
        Children = { BoundText(nameof(ViewModel.CurrentQuantity), 14, Slate500), Text("→", 16, Slate500), BoundText(nameof(ViewModel.NewQuantity), 18, Primary, 700), Text("sản phẩm", 13, Primary, 700) }
    };

    private Grid BuildFooter()
    {
        var footer = new Grid { Padding = new Thickness(16, 8, 16, 8), Background = Brush("#F2F4F6"), BorderBrush = Brush("#E0E3E5"), BorderThickness = new Thickness(0, 1, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var cancel = Button(Text("Hủy", 14, Slate500, 500)); cancel.Height = 36; cancel.Padding = new Thickness(16, 0, 16, 0); cancel.Command = ViewModel.CloseCommand;
        var save = Button(Text("Xác nhận", 14, "#FFFFFF", 500), primary: true);
        save.Height = 36;
        save.Padding = new Thickness(20, 0, 20, 0);
        save.Background = Brush(Primary);
        save.Foreground = Brush("#FFFFFF");
        save.Resources["ButtonBackgroundPointerOver"] = Brush("#1D4ED8");
        save.Resources["ButtonBackgroundPressed"] = Brush("#1E40AF");
        save.Resources["ButtonForegroundPointerOver"] = Brush("#FFFFFF");
        save.Command = ViewModel.ConfirmCommand;
        Bind(save, Control.IsEnabledProperty, ViewModel, nameof(ViewModel.CanSubmit));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { cancel, save } }; Grid.SetColumn(buttons, 1); footer.Children.Add(buttons); return footer;
    }

    private TextBlock BoundText(string path, double size, string color, ushort weight = 400) { var text = Text("", size, color, weight); Bind(text, TextBlock.TextProperty, ViewModel, path); return text; }
}

internal sealed class SegmentBackgroundConverter(bool increase) : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is bool active && active == increase ? Brush("#2563EB") : Brush("#F1F3F5");
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed class SegmentForegroundConverter(bool increase) : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is bool active && active == increase ? Brush("#FFFFFF") : Brush(Slate500);
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
