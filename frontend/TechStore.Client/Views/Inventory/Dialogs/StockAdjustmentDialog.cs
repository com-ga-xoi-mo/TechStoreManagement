using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Controls;
using TechStore.Client.Views.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory.Dialogs;

public sealed class StockAdjustmentDialog : ContentDialog
{
    public StockAdjustmentDialogViewModel ViewModel { get; }

    public StockAdjustmentDialog(StockAdjustmentDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
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
        this.Background(Theme(AppColors.Surface));
        Content = BuildLayout();
        ViewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => ViewModel.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs e) => Hide();

    private Grid BuildLayout()
    {
        var root = new Grid { Width = 480, MaxWidth = 480 }.Background(Theme(AppColors.Surface));
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
        var header = new Grid { Padding = new Thickness(16, 12, 12, 8), BorderThickness = new Thickness(0, 0, 0, 1) }
            .BorderBrush(Theme(AppColors.Stroke));
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(Text("Điều chỉnh tồn kho", 16, AppColors.Text, 600));
        var close = Button(Icon("close", AppColors.SecondaryText, 20), tooltip: "Đóng điều chỉnh tồn kho"); close.Command(x => x.Binding(() => ViewModel.CloseCommand)); Grid.SetColumn(close, 1); header.Children.Add(close);
        return header;
    }

    private UIElement BuildBody()
    {
        var body = new StackPanel { Padding = new Thickness(16), Spacing = 8 };
        body.Children.Add(BoundText(nameof(ViewModel.ProductName), 15, AppColors.Text, 600));
        body.Children.Add(Badge(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { Icon("qr_code", AppColors.SecondaryText, 14), BoundText(nameof(ViewModel.Sku), 13, AppColors.SecondaryText, 600) } }, StatusTone.Neutral));
        body.Children.Add(BuildCurrentStockCards());
        body.Children.Add(Text("Loại điều chỉnh", 13, AppColors.Text, 600));
        body.Children.Add(BuildSegmentedControl());
        body.Children.Add(RequiredLabel("Số lượng"));
        body.Children.Add(BuildAmountInput());
        body.Children.Add(RequiredLabel("Lý do"));
        var reason = new TextBox { MinHeight = 64, MaxLength = 200, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, PlaceholderText = "Nhập lý do điều chỉnh kho..." };
        reason.Text(x => x.Binding(() => ViewModel.Reason).Mode(BindingMode.TwoWay)); body.Children.Add(reason);
        body.Children.Add(Text("Lý do sẽ được ghi vào lịch sử biến động kho", 11, AppColors.SecondaryText));
        body.Children.Add(BuildPreview());
        var error = Text("", 12, AppColors.DangerText);
        error.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        error.Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf));
        body.Children.Add(error);
        return body;
    }

    private static StackPanel RequiredLabel(string label)
    {
        var required = Text("*", 13, AppColors.DangerText, 700);
        required.Margin = new Thickness(3, 0, 0, 0);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { Text(label, 13, AppColors.Text, 600), required }
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
        var value = BoundText(path, 16, AppColors.Text, 700);
        var card = new Border
        {
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12, 10, 12, 10),
            Child = new StackPanel { Spacing = 4, Children = { Text(label, 12, AppColors.SecondaryText), value } }
        }.Background(Theme(AppColors.Subtle));
        Grid.SetColumn(card, column); return card;
    }

    private Grid BuildSegmentedControl()
    {
        var grid = new Grid
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            ColumnSpacing = 4,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }.Background(Theme(AppColors.Subtle));
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var addActive = Button(Text("Nhập thêm", 14, AppColors.OnPrimary, 600), primary: true);
        addActive.Height = 32;
        addActive.HorizontalAlignment = HorizontalAlignment.Stretch;
        addActive.HorizontalContentAlignment = HorizontalAlignment.Center;
        addActive.Padding = new Thickness(12, 0, 12, 0);
        addActive.Visibility(x => x.Binding(() => ViewModel.IsIncrease).Convert(ShowIf));

        var addInactive = Button(Text("Nhập thêm", 14, AppColors.SecondaryText, 600));
        addInactive.Height = 32;
        addInactive.HorizontalAlignment = HorizontalAlignment.Stretch;
        addInactive.HorizontalContentAlignment = HorizontalAlignment.Center;
        addInactive.Padding = new Thickness(12, 0, 12, 0);
        addInactive.Command(x => x.Binding(() => ViewModel.SetIncreaseCommand));
        addInactive.Visibility(x => x.Binding(() => ViewModel.IsIncrease).Convert((bool increase) => ShowIf(!increase)));

        var decreaseActive = Button(Text("Giảm tồn", 14, AppColors.OnPrimary, 600), primary: true);
        decreaseActive.Height = 32;
        decreaseActive.HorizontalAlignment = HorizontalAlignment.Stretch;
        decreaseActive.HorizontalContentAlignment = HorizontalAlignment.Center;
        decreaseActive.Padding = new Thickness(12, 0, 12, 0);
        decreaseActive.Visibility(x => x.Binding(() => ViewModel.IsIncrease).Convert((bool increase) => ShowIf(!increase)));

        var decreaseInactive = Button(Text("Giảm tồn", 14, AppColors.SecondaryText, 600));
        decreaseInactive.Height = 32;
        decreaseInactive.HorizontalAlignment = HorizontalAlignment.Stretch;
        decreaseInactive.HorizontalContentAlignment = HorizontalAlignment.Center;
        decreaseInactive.Padding = new Thickness(12, 0, 12, 0);
        decreaseInactive.Command(x => x.Binding(() => ViewModel.SetDecreaseCommand));
        decreaseInactive.Visibility(x => x.Binding(() => ViewModel.IsIncrease).Convert(ShowIf));

        var addHost = new Grid().Children(addActive, addInactive);
        var decreaseHost = new Grid().Children(decreaseActive, decreaseInactive);
        Grid.SetColumn(decreaseHost, 1);
        grid.Children.Add(addHost);
        grid.Children.Add(decreaseHost);
        return grid;
    }

    private Grid BuildAmountInput()
    {
        var amount = new NumberBox { Minimum = 0, Maximum = 1000000, Height = 40, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden, HorizontalContentAlignment = HorizontalAlignment.Left };
        amount.Value(x => x.Binding(() => ViewModel.Amount).Mode(BindingMode.TwoWay));
        var grid = new Grid(); grid.Children.Add(amount);
        var unit = Text("sản phẩm", 13, AppColors.SecondaryText, 500); unit.HorizontalAlignment = HorizontalAlignment.Right; unit.Margin = new Thickness(0, 0, 14, 0); unit.IsHitTestVisible = false; grid.Children.Add(unit); return grid;
    }

    private Border BuildPreview() => new Border
    {
        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(14, 12, 14, 12),
        Child = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = GridLength.Auto } },
            Children = { Text("Tồn mới:", 14, AppColors.SecondaryText, 600), BuildPreviewValue().Grid(column: 1) }
        }
    }.Background(Theme(AppColors.InfoFill)).BorderBrush(Theme(AppColors.InfoStroke));

    private StackPanel BuildPreviewValue() => new()
    {
        Orientation = Orientation.Horizontal, Spacing = 6,
        Children = { BoundText(nameof(ViewModel.CurrentQuantity), 14, AppColors.SecondaryText), Text("→", 16, AppColors.SecondaryText), BoundText(nameof(ViewModel.NewQuantity), 18, AppColors.InfoText, 700), Text("sản phẩm", 13, AppColors.InfoText, 700) }
    };

    private Grid BuildFooter()
    {
        var footer = new Grid { Padding = new Thickness(16, 8, 16, 8), BorderThickness = new Thickness(0, 1, 0, 0) }
            .Background(Theme(AppColors.Subtle)).BorderBrush(Theme(AppColors.Stroke));
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var cancel = Button(Text("Hủy", 14, AppColors.SecondaryText, 500)); cancel.Height = 36; cancel.Padding = new Thickness(16, 0, 16, 0); cancel.Command(x => x.Binding(() => ViewModel.CloseCommand));
        var save = Button(Text("Xác nhận", 14, AppColors.OnPrimary, 500), primary: true);
        save.Height = 36;
        save.Padding = new Thickness(20, 0, 20, 0);
        save.Command(x => x.Binding(() => ViewModel.ConfirmCommand));
        save.IsEnabled(x => x.Binding(() => ViewModel.CanSubmit));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { cancel, save } }; Grid.SetColumn(buttons, 1); footer.Children.Add(buttons); return footer;
    }

    private TextBlock BoundText(string path, double size, string color, ushort weight = 400)
    {
        var text = Text("", size, color, weight);
        text.Text(x => x.Binding<StockAdjustmentDialogViewModel>(path));
        return text;
    }
}
