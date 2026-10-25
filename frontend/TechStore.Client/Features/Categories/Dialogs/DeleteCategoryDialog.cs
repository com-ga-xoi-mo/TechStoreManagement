using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Features.Categories.ViewModels;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;
using static TechStore.Client.Features.Categories.Pages.CategoryWorkspaceStyles;

namespace TechStore.Client.Features.Categories.Dialogs;

/// <summary>Safe delete confirmation modal. The complete visual tree is C# Markup.</summary>
public sealed class DeleteCategoryDialog : ContentDialog
{
    public DeleteCategoryDialogViewModel ViewModel { get; }

    public DeleteCategoryDialog(DeleteCategoryDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        RequestedTheme = ElementTheme.Default;
        FontFamily = Typeface;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Margin = new Thickness(0);
        DefaultButton = ContentDialogButton.None;
        PrimaryButtonText = string.Empty;
        SecondaryButtonText = string.Empty;
        CloseButtonText = string.Empty;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(12);
        MinWidth = 320;
        MaxWidth = 480;
        Width = 480;
        Resources["ContentDialogMaxWidth"] = 480d;
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

    private UIElement BuildLayout() =>
        new Grid()
            .Width(480)
            .MaxWidth(480)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch)
            .Background(AppColors.Brushes.Surface)
            .CornerRadius(new CornerRadius(12))
            .RowDefinitions(
                new RowDefinition().Height(GridLength.Auto),
                new RowDefinition().Height(new GridLength(1, GridUnitType.Star)),
                new RowDefinition().Height(GridLength.Auto))
            .Children(
                BuildBody().Grid(row: 0),
                BuildFooter().Grid(row: 1));

    private UIElement BuildBody() =>
        new ScrollViewer()
            .MaxHeight(620)
            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
            .Content(
                new Grid()
                    .Padding(new Thickness(24, 24, 24, 20))
                    .RowDefinitions(
                        new RowDefinition().Height(GridLength.Auto),
                        new RowDefinition().Height(GridLength.Auto),
                        new RowDefinition().Height(GridLength.Auto),
                        new RowDefinition().Height(GridLength.Auto))
                    .Children(
                        BuildCloseButton().Grid(row: 0),
                        BuildWarningHeading().Grid(row: 1),
                        BuildCategoryCard().Grid(row: 2),
                        BuildConstraintCard().Grid(row: 3)));

    private UIElement BuildCloseButton()
    {
        var close = new Button()
            .Width(32)
            .Height(32)
            .Padding(new Thickness(0))
            .CornerRadius(new CornerRadius(8))
            .Background(AppColors.Brushes.Surface)
            .BorderThickness(new Thickness(0))
            .HorizontalAlignment(HorizontalAlignment.Right)
            .Content(Icon("close", Slate500, 20))
            .Command(x => x.Binding(() => ViewModel.CancelCommand));
        AutomationProperties.SetName(close, "Đóng cửa sổ xác nhận xóa");
        return close;
    }

    private UIElement BuildWarningHeading() =>
        new StackPanel()
            .Spacing(8)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Children(
                new Border()
                    .Width(48)
                    .Height(48)
                    .CornerRadius(new CornerRadius(24))
                    .Background(AppColors.Brushes.DangerFill)
                    .Child(Icon("delete_forever", "#B91C1C", 24)
                        .HorizontalAlignment(HorizontalAlignment.Center)
                        .VerticalAlignment(VerticalAlignment.Center)),
                new TextBlock()
                    .FontFamily(Typeface)
                    .FontSize(20)
                    .FontWeight(Weight(700))
                    .Foreground(AppColors.Brushes.Text)
                    .TextAlignment(TextAlignment.Center)
                    .Text("Xác nhận xóa danh mục?"),
                new TextBlock()
                    .MaxWidth(390)
                    .FontFamily(Typeface)
                    .FontSize(13)
                    .FontWeight(Weight(400))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .TextAlignment(TextAlignment.Center)
                    .TextWrapping(TextWrapping.Wrap)
                    .Text("Hành động này không thể hoàn tác. Vui lòng kiểm tra kỹ các ràng buộc dữ liệu bên dưới."));

    private UIElement BuildCategoryCard() =>
        new Border()
            .Margin(new Thickness(0, 20, 0, 12))
            .Background(AppColors.Brushes.Subtle)
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(12))
            .Child(
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(GridLength.Auto),
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)))
                    .ColumnSpacing(12)
                    .Children(
                        new Border()
                            .Width(40)
                            .Height(40)
                            .CornerRadius(new CornerRadius(8))
                            .Background(Brush(ViewModel.Icon.Background))
                            .BorderBrush(Brush(ViewModel.Icon.Border))
                            .BorderThickness(new Thickness(1))
                            .Child(Icon(ViewModel.Icon.Symbol, ViewModel.Icon.Foreground, 20)
                                .HorizontalAlignment(HorizontalAlignment.Center)
                                .VerticalAlignment(VerticalAlignment.Center))
                            .Grid(column: 0),
                        new StackPanel()
                            .Spacing(3)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Children(
                                new TextBlock()
                                    .FontFamily(Typeface)
                                    .FontSize(14)
                                    .FontWeight(Weight(600))
                                    .Foreground(AppColors.Brushes.Text)
                                    .TextTrimming(TextTrimming.CharacterEllipsis)
                                    .Text(x => x.Binding(() => ViewModel.CategoryName)),
                                new TextBlock()
                                    .FontFamily(Typeface)
                                    .FontSize(12)
                                    .FontWeight(Weight(400))
                                    .Foreground(AppColors.Brushes.SecondaryText)
                                    .TextTrimming(TextTrimming.CharacterEllipsis)
                                    .Text(x => x.Binding(() => ViewModel.LevelName)))
                            .Grid(column: 1)));

    private UIElement BuildConstraintCard() =>
        new Border()
            .Background(AppColors.Brushes.Subtle)
            .CornerRadius(new CornerRadius(12))
            .Padding(new Thickness(12))
            .Child(
                new StackPanel()
                    .Spacing(8)
                    .Children(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .Children(
                                Icon("warning", "#B91C1C", 18),
                                new TextBlock()
                                    .FontFamily(Typeface)
                                    .FontSize(13)
                                    .FontWeight(Weight(600))
                                    .Foreground(AppColors.Brushes.Text)
                                    .Text("Cảnh báo ràng buộc dữ liệu:")),
                        ConstraintRow(ViewModel.ChildConstraintText, ViewModel.HasChildren),
                        ConstraintRow(ViewModel.ProductConstraintText, ViewModel.HasProducts),
                        BuildError()));

    private static UIElement ConstraintRow(string text, bool visible) =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(8)
            .Visibility(visible ? Visibility.Visible : Visibility.Collapsed)
            .Children(
                new TextBlock()
                    .FontFamily(Typeface)
                    .FontSize(13)
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .Text("•"),
                new TextBlock()
                    .FontFamily(Typeface)
                    .FontSize(12)
                    .FontWeight(Weight(400))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(text));

    private UIElement BuildError() =>
        new Border()
            .Visibility(x => x.Binding(() => ViewModel.HasError).Convert((bool value) => ShowIf(value)))
            .Background(AppColors.Brushes.DangerFill)
            .BorderBrush(AppColors.Brushes.DangerStroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(10, 8, 10, 8))
            .Child(
                new TextBlock()
                    .FontFamily(Typeface)
                    .FontSize(12)
                    .Foreground(AppColors.Brushes.DangerText)
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(x => x.Binding(() => ViewModel.ErrorMessage)));

    private UIElement BuildFooter() =>
        new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 1, 0, 0))
            .Background(AppColors.Brushes.Subtle)
            .Padding(new Thickness(24, 14, 24, 14))
            .Child(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(10)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .Children(
                        new Button()
                            .Style(AppControlStyles.Styles.Button)
                            .FontFamily(Typeface)
                            .FontSize(13)
                            .FontWeight(Weight(500))
                            .Content("Hủy bỏ")
                            .CornerRadius(new CornerRadius(6))
                            .Background(AppColors.Brushes.Surface)
                            .Foreground(AppColors.Brushes.Text)
                            .BorderBrush(AppColors.Brushes.Stroke)
                            .BorderThickness(new Thickness(1))
                            .Height(40)
                            .MinWidth(0)
                            .IsEnabled(x => x.Binding(() => ViewModel.IsBusy).Convert((bool busy) => !busy))
                            .Command(x => x.Binding(() => ViewModel.CancelCommand)),
                        new Button()
                            .Style(AppControlStyles.Styles.DangerButton)
                            .FontFamily(Typeface)
                            .FontSize(13)
                            .FontWeight(Weight(600))
                            .CornerRadius(new CornerRadius(6))
                            .Background(AppColors.Brushes.DangerText)
                            .Foreground(AppColors.Brushes.OnPrimary)
                            .Height(40)
                            .MinWidth(0)
                            .Content(new Grid()
                                .ColumnDefinitions(
                                    new ColumnDefinition().Width(GridLength.Auto),
                                    new ColumnDefinition().Width(GridLength.Auto))
                                .ColumnSpacing(6)
                                .Children(
                                    new ProgressRing()
                                        .Width(16)
                                        .Height(16)
                                        .Foreground(AppColors.Brushes.OnPrimary)
                                        .IsActive(x => x.Binding(() => ViewModel.IsBusy))
                                        .Visibility(x => x.Binding(() => ViewModel.IsBusy).Convert((bool busy) => ShowIf(busy)))
                                        .Grid(column: 0),
                                    new TextBlock()
                                        .FontFamily(Typeface)
                                        .FontSize(13)
                                        .FontWeight(Weight(600))
                                        .Foreground(AppColors.Brushes.OnPrimary)
                                        .Text(x => x.Binding(() => ViewModel.IsBusy).Convert((bool busy) => busy ? "Đang xóa..." : "Xác nhận xóa"))
                                        .Grid(column: 1)))
                            .IsEnabled(x => x.Binding(() => ViewModel.CanDelete))
                            .Command(x => x.Binding(() => ViewModel.ConfirmDeleteCommand))));

    private static Windows.UI.Text.FontWeight Weight(ushort value) => new() { Weight = value };

    private static Visibility ShowIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
