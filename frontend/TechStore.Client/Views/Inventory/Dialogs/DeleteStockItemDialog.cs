using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Views.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory.Dialogs;

/// <summary>Stock deletion confirmation modal rendered entirely with C# Markup.</summary>
public sealed class DeleteStockItemDialog : ContentDialog
{
    public DeleteStockItemDialogViewModel ViewModel { get; }

    public DeleteStockItemDialog(DeleteStockItemDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        DefaultButton = ContentDialogButton.None;
        PrimaryButtonText = string.Empty;
        SecondaryButtonText = string.Empty;
        CloseButtonText = string.Empty;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(16);
        MinWidth = 320;
        MaxWidth = 460;
        Width = 460;
        Resources["ContentDialogMaxWidth"] = 460d;
        Resources["ContentDialogMinWidth"] = 320d;
        Resources["ContentDialogPadding"] = new Thickness(0);
        Resources["ContentDialogTopPadding"] = 0d;
        Resources["ContentDialogBottomPadding"] = 0d;
        Resources["ContentDialogTitleMargin"] = new Thickness(0);
        Resources["ContentDialogContentMargin"] = new Thickness(0);
        Resources["ContentDialogCommandSpace"] = 0d;
        this.Background(AppColors.Brushes.Surface);
        Content = BuildLayout();

        ViewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => ViewModel.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs args) => Hide();

    private UIElement BuildLayout()
    {
        var shell = new Grid()
            .Width(460)
            .MaxWidth(460)
            .Background(AppColors.Brushes.Surface)
            .CornerRadius(new CornerRadius(16))
            .RowDefinitions(
                new RowDefinition().Height(GridLength.Auto),
                new RowDefinition().Height(GridLength.Auto));

        shell.Children.Add(BuildBody().Grid(row: 0));
        shell.Children.Add(BuildFooter().Grid(row: 1));

        var close = BuildCloseButton();
        close.Margin = new Thickness(0, 16, 16, 0);
        Canvas.SetZIndex(close, 1);
        shell.Children.Add(close);
        return shell;
    }

    private UIElement BuildBody() =>
        new StackPanel()
            .Padding(new Thickness(24))
            .Spacing(0)
            .Children(
                new StackPanel()
                    .Spacing(0)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Children(
                        new Border()
                            .Width(48)
                            .Height(48)
                            .CornerRadius(new CornerRadius(12))
                            .Background(AppColors.Brushes.DangerFill)
                            .BorderBrush(AppColors.Brushes.DangerStroke)
                            .BorderThickness(new Thickness(1))
                            .Margin(new Thickness(0, 0, 0, 16))
                            .Child(Icon("delete_forever", AppColors.DangerText, 26)
                                .HorizontalAlignment(HorizontalAlignment.Center)
                                .VerticalAlignment(VerticalAlignment.Center)),
                        new TextBlock()
                            .FontFamily(AppTypography.Fonts.SemiBoldFont)
                            .FontSize(18)
                            .FontWeight(Weight(600))
                            .Foreground(AppColors.Brushes.Text)
                            .TextAlignment(TextAlignment.Center)
                            .Margin(new Thickness(0, 0, 0, 4))
                            .Text("Xác nhận xóa sản phẩm"),
                        new TextBlock()
                            .MaxWidth(400)
                            .FontFamily(AppTypography.Fonts.RegularFont)
                            .FontSize(13)
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .TextAlignment(TextAlignment.Center)
                            .TextWrapping(TextWrapping.Wrap)
                            .Text("Bạn có chắc chắn muốn xóa sản phẩm này khỏi danh mục tồn kho không?")),
                BuildStockCard(),
                BuildError());

    private Button BuildCloseButton()
    {
        var close = new Button()
            .Width(32)
            .Height(32)
            .Padding(new Thickness(0))
            .CornerRadius(new CornerRadius(8))
            .Background(AppColors.Brushes.Surface)
            .BorderThickness(new Thickness(0))
            .HorizontalAlignment(HorizontalAlignment.Right)
            .VerticalAlignment(VerticalAlignment.Top)
            .Content(Icon("close", AppColors.SecondaryText, 20))
            .Command(x => x.Binding(() => ViewModel.CancelCommand));
        AutomationProperties.SetName(close, "Đóng cửa sổ xác nhận xóa tồn kho");
        return close;
    }

    private UIElement BuildStockCard() =>
        new Border()
            .Margin(new Thickness(0, 20, 0, 0))
            .Background(AppColors.Brushes.Subtle)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(12))
            .Child(
                new StackPanel()
                    .Spacing(8)
                    .Children(
                        new Grid()
                            .ColumnDefinitions(
                                new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                                new ColumnDefinition().Width(GridLength.Auto))
                            .Children(
                                new TextBlock()
                                    .FontFamily(AppTypography.Fonts.SemiBoldFont)
                                    .FontSize(14)
                                    .FontWeight(Weight(600))
                                    .Foreground(AppColors.Brushes.Text)
                                    .TextTrimming(TextTrimming.CharacterEllipsis)
                                    .Text(x => x.Binding(() => ViewModel.ProductName))
                                    .Grid(column: 0),
                                new TextBlock()
                                    .FontFamily(AppTypography.Fonts.RegularFont)
                                    .FontSize(11)
                                    .Foreground(AppColors.Brushes.SecondaryText)
                                    .TextTrimming(TextTrimming.CharacterEllipsis)
                                    .Text(x => x.Binding(() => ViewModel.Sku))
                                    .Grid(column: 1)),
                        new Border()
                            .Background(AppColors.Brushes.DangerFill)
                            .BorderBrush(AppColors.Brushes.DangerStroke)
                            .BorderThickness(new Thickness(1))
                            .CornerRadius(new CornerRadius(8))
                            .Padding(new Thickness(10, 8, 10, 8))
                            .Child(
                                new TextBlock()
                                    .FontFamily(AppTypography.Fonts.MediumFont)
                                    .FontSize(12)
                                    .FontWeight(Weight(500))
                                    .Foreground(AppColors.Brushes.DangerText)
                                    .TextWrapping(TextWrapping.Wrap)
                                    .Text(x => x.Binding(() => ViewModel.SerialWarningText)))));

    private UIElement BuildError() =>
        new Border()
            .Margin(new Thickness(0, 12, 0, 0))
            .Visibility(x => x.Binding(() => ViewModel.HasError).Convert((bool value) => ShowIf(value)))
            .Background(AppColors.Brushes.DangerFill)
            .BorderBrush(AppColors.Brushes.DangerStroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(10, 8, 10, 8))
            .Child(
                new TextBlock()
                    .FontFamily(AppTypography.Fonts.RegularFont)
                    .FontSize(12)
                    .Foreground(AppColors.Brushes.DangerText)
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(x => x.Binding(() => ViewModel.ErrorMessage)));

    private UIElement BuildFooter() =>
        new Border()
            .Background(AppColors.Brushes.Surface)
            .Padding(new Thickness(24, 0, 24, 24))
            .Child(
                new Grid()
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)))
                    .ColumnSpacing(12)
                    .Children(
                        new Button()
                            .Height(40)
                            .Padding(new Thickness(16, 10, 16, 10))
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .MinWidth(0)
                            .FontFamily(AppTypography.Fonts.RegularFont)
                            .FontSize(14)
                            .Foreground(AppColors.Brushes.Text)
                            .Background(AppColors.Brushes.Surface)
                            .BorderBrush(AppColors.Brushes.Stroke)
                            .BorderThickness(new Thickness(1))
                            .CornerRadius(new CornerRadius(8))
                            .HorizontalContentAlignment(HorizontalAlignment.Center)
                            .VerticalContentAlignment(VerticalAlignment.Center)
                            .Content("Hủy bỏ")
                            .IsEnabled(x => x.Binding(() => ViewModel.CanCancel))
                            .Command(x => x.Binding(() => ViewModel.CancelCommand))
                            .Grid(column: 0),
                        new Button()
                            .Height(40)
                            .Padding(new Thickness(16, 10, 16, 10))
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .MinWidth(0)
                            .FontFamily(AppTypography.Fonts.SemiBoldFont)
                            .FontSize(14)
                            .FontWeight(Weight(600))
                            .Foreground(AppColors.Brushes.DangerText)
                            .Background(AppColors.Brushes.DangerFill)
                            .BorderBrush(AppColors.Brushes.DangerStroke)
                            .BorderThickness(new Thickness(1))
                            .CornerRadius(new CornerRadius(8))
                            .HorizontalContentAlignment(HorizontalAlignment.Center)
                            .VerticalContentAlignment(VerticalAlignment.Center)
                            .Content(BuildConfirmContent())
                            .IsEnabled(x => x.Binding(() => ViewModel.CanDelete))
                            .Command(x => x.Binding(() => ViewModel.ConfirmDeleteCommand))
                            .Grid(column: 1)));

    private UIElement BuildConfirmContent() =>
        new Grid()
            .ColumnDefinitions(
                new ColumnDefinition().Width(GridLength.Auto),
                new ColumnDefinition().Width(GridLength.Auto))
            .ColumnSpacing(6)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .Children(
                new ProgressRing()
                    .Width(16)
                    .Height(16)
                    .Foreground(AppColors.Brushes.DangerText)
                    .IsActive(x => x.Binding(() => ViewModel.IsBusy))
                    .Visibility(x => x.Binding(() => ViewModel.IsBusy).Convert((bool value) => ShowIf(value)))
                    .Grid(column: 0),
                new TextBlock()
                    .FontFamily(AppTypography.Fonts.SemiBoldFont)
                    .FontSize(14)
                    .FontWeight(Weight(600))
                    .Foreground(AppColors.Brushes.DangerText)
                    .Text(x => x.Binding(() => ViewModel.IsBusy).Convert((bool busy) => busy ? "Đang xóa..." : "Xác nhận xóa"))
                    .Grid(column: 1));

    private static Windows.UI.Text.FontWeight Weight(ushort value) => new() { Weight = value };
    private static Visibility ShowIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
