using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Features.Categories.Pages;
using TechStore.Client.Features.Inventory.ViewModels;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;
using static TechStore.Client.Features.Categories.Pages.CategoryWorkspaceStyles;

namespace TechStore.Client.Features.Inventory.Dialogs;

/// <summary>Initial stock import modal. Layout is authored entirely with C# Markup.</summary>
public sealed class ImportStockDialog : ContentDialog
{
    public ImportStockDialogViewModel ViewModel { get; }

    public ImportStockDialog(ImportStockDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        DefaultButton = ContentDialogButton.None;
        PrimaryButtonText = string.Empty;
        SecondaryButtonText = string.Empty;
        CloseButtonText = string.Empty;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(12);
        Width = 560;
        MaxWidth = 560;
        Resources["ContentDialogMaxWidth"] = 560d;
        Resources["ContentDialogPadding"] = new Thickness(0);
        Resources["ContentDialogCommandSpace"] = 0d;
        this.Background(Theme(AppColors.Surface));
        Content = BuildLayout();
        ViewModel.RequestClose += OnRequestClose;
        Loaded += (_, _) => _ = ViewModel.LoadProductsCommand.ExecuteAsync(null);
        Closed += (_, _) => ViewModel.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs e) => Hide();

    private Grid BuildLayout()
    {
        var root = new Grid { Width = 560, MaxWidth = 560 }.Background(Theme(AppColors.Surface));
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(BuildHeader());
        var scroll = new ScrollViewer
        {
            MaxHeight = 650,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = BuildBody()
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private Grid BuildHeader()
    {
        var iconBox = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(8),
            Child = Icon("inventory", AppColors.InfoText, 20)
        }.Background(Theme(AppColors.InfoFill));
        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { iconBox, Text("Nhập kho mới", 20, AppColors.Text, 600) }
        };
        var header = new Grid
        {
            Padding = new Thickness(20, 16, 14, 14),
            BorderThickness = new Thickness(0, 0, 0, 1)
        }.Background(Theme(AppColors.Surface)).BorderBrush(Theme(AppColors.Stroke));
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(title);
        var close = Button(Icon("close", AppColors.SecondaryText, 20), tooltip: "Đóng nhập kho");
        close.Command(x => x.Binding(() => ViewModel.CloseCommand));
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        return header;
    }

    private UIElement BuildBody()
    {
        var body = new StackPanel { Padding = new Thickness(20, 18, 20, 20), Spacing = 10 };
        body.Children.Add(RequiredLabel("Sản phẩm / SKU"));

        var productPicker = new ComboBox
        {
            Height = 42,
            PlaceholderText = "Chọn sản phẩm hoặc SKU...",
            DisplayMemberPath = nameof(ImportStockProductOption.DisplayText),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        productPicker.ItemsSource(x => x.Binding(() => ViewModel.ProductOptions));
        productPicker.SelectedItem(x => x.Binding(() => ViewModel.SelectedProduct).Mode(BindingMode.TwoWay));
        body.Children.Add(productPicker);

        var productMeta = Text("", 12, AppColors.SecondaryText);
        productMeta.Text(x => x.Binding(() => ViewModel.ProductMetaText));
        body.Children.Add(productMeta);

        var variantPicker = new ComboBox
        {
            Height = 42,
            PlaceholderText = "Chọn biến thể / SKU...",
            DisplayMemberPath = nameof(ImportStockVariantOption.DisplayText),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        variantPicker.ItemsSource(x => x.Binding(() => ViewModel.VariantOptions));
        variantPicker.SelectedItem(x => x.Binding(() => ViewModel.SelectedVariant).Mode(BindingMode.TwoWay));
        variantPicker.Visibility(x => x.Binding(() => ViewModel.HasVariants).Convert(ShowIf));
        body.Children.Add(variantPicker);

        body.Children.Add(RequiredLabel("Số lượng nhập kho"));
        var quantity = new NumberBox
        {
            Minimum = 1,
            Maximum = 1_000_000,
            Height = 42,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        quantity.Value(x => x.Binding(() => ViewModel.Quantity).Mode(BindingMode.TwoWay));
        body.Children.Add(quantity);

        var serialHeader = new Grid();
        serialHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        serialHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        serialHeader.Children.Add(Text("Danh sách Serial / IMEI", 13, AppColors.Text, 600));
        var serialCount = Text("", 12, AppColors.SecondaryText);
        serialCount.Text(x => x.Binding(() => ViewModel.SerialCountText));
        Grid.SetColumn(serialCount, 1);
        serialHeader.Children.Add(serialCount);
        body.Children.Add(serialHeader);

        var serials = new TextBox
        {
            MinHeight = 96,
            MaxLength = 20_000,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            PlaceholderText = "Mỗi mã một dòng (không bắt buộc)..."
        };
        serials.Text(x => x.Binding(() => ViewModel.SerialLines).Mode(BindingMode.TwoWay));
        body.Children.Add(serials);
        body.Children.Add(Text("Nếu nhập Serial/IMEI, số mã phải khớp với số lượng tồn ban đầu.", 11, AppColors.SecondaryText));

        body.Children.Add(Text("Ghi chú nhập kho", 13, AppColors.Text, 600));
        var reason = new TextBox
        {
            Height = 42,
            PlaceholderText = "Ví dụ: Nhập lô hàng đầu kỳ từ kho tổng..."
        };
        reason.Text(x => x.Binding(() => ViewModel.Reason).Mode(BindingMode.TwoWay));
        body.Children.Add(reason);

        var validation = Text("", 12, AppColors.DangerText);
        validation.TextWrapping = TextWrapping.Wrap;
        validation.Text(x => x.Binding(() => ViewModel.ValidationMessage));
        validation.Visibility(x => x.Binding(() => ViewModel.HasValidationError).Convert(ShowIf));
        body.Children.Add(validation);

        var error = Text("", 12, AppColors.DangerText);
        error.TextWrapping = TextWrapping.Wrap;
        error.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        error.Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf));
        body.Children.Add(error);
        return body;
    }

    private Grid BuildFooter()
    {
        var footer = new Grid
        {
            Padding = new Thickness(20, 10, 20, 10),
            BorderThickness = new Thickness(0, 1, 0, 0)
        }.Background(Theme(AppColors.Subtle)).BorderBrush(Theme(AppColors.Stroke));
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var cancel = Button(Text("Hủy", 14, AppColors.SecondaryText, 500));
        cancel.Height = 36;
        cancel.Padding = new Thickness(16, 0, 16, 0);
        cancel.Command(x => x.Binding(() => ViewModel.CloseCommand));
        var busy = new ProgressRing { Width = 16, Height = 16, IsActive = true }.Foreground(Theme(AppColors.OnPrimary));
        busy.Visibility(x => x.Binding(() => ViewModel.IsBusy).Convert(ShowIf));
        var saveContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { busy, Text("Nhập kho", 14, AppColors.OnPrimary, 500) }
        };
        var save = Button(saveContent, primary: true);
        save.Height = 36;
        save.Padding = new Thickness(20, 0, 20, 0);
        save.Command(x => x.Binding(() => ViewModel.ConfirmCommand));
        save.IsEnabled(x => x.Binding(() => ViewModel.CanSubmit));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { cancel, save } };
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        return footer;
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
}
