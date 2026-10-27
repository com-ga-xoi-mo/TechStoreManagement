using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory.Dialogs;

/// <summary>Compact edit modal for one category. The entire visual tree is C# Markup.</summary>
public sealed class EditCategoryDialog : ContentDialog
{
    public EditCategoryDialogViewModel ViewModel { get; }

    public EditCategoryDialog(EditCategoryDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
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

        // The presenter owns the dialog lifetime; this event only translates a VM result to Hide().
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
                BuildHeader().Grid(row: 0),
                BuildBody().Grid(row: 1),
                BuildFooter().Grid(row: 2));

    private UIElement BuildHeader()
    {
        var close = new Button()
            .Width(32)
            .Height(32)
            .Padding(new Thickness(0))
            .CornerRadius(new CornerRadius(8))
            .Background(AppColors.Brushes.Surface)
            .BorderThickness(new Thickness(0))
            .Content(Icon("close", AppColors.SecondaryText, 20))
            .Command(x => x.Binding(() => ViewModel.CancelCommand));
        AutomationProperties.SetName(close, "Đóng cửa sổ sửa danh mục");

        var badge = new Grid().Children(
            new Border()
                .Visibility(x => x.Binding(() => ViewModel.IsRoot).Convert((bool value) => ShowIf(value)))
                .Background(AppColors.Brushes.InfoFill)
                .BorderBrush(AppColors.Brushes.InfoStroke)
                .BorderThickness(new Thickness(1))
                .CornerRadius(new CornerRadius(4))
                .Padding(new Thickness(8, 2, 8, 2))
                .Child(new TextBlock().Text("Danh mục gốc").FontFamily(AppTypography.Fonts.SemiBoldFont).FontSize(12).FontWeight(Weight(600)).Foreground(AppColors.Brushes.InfoText)),
            new Border()
                .Visibility(x => x.Binding(() => ViewModel.IsRoot).Convert((bool value) => ShowIf(!value)))
                .Background(AppColors.Brushes.Subtle)
                .BorderBrush(AppColors.Brushes.Stroke)
                .BorderThickness(new Thickness(1))
                .CornerRadius(new CornerRadius(4))
                .Padding(new Thickness(8, 2, 8, 2))
                .Child(new TextBlock().Text("Danh mục con").FontFamily(AppTypography.Fonts.SemiBoldFont).FontSize(12).FontWeight(Weight(600)).Foreground(AppColors.Brushes.SecondaryText)));

        return new Border()
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .Padding(new Thickness(24, 16, 16, 16))
            .Child(
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(GridLength.Auto),
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(GridLength.Auto))
                    .ColumnSpacing(10)
                    .Children(
                        new Border()
                            .Width(32).Height(32)
                            .CornerRadius(new CornerRadius(8))
                            .Background(AppColors.Brushes.InfoFill)
                            .Child(Icon("edit_note", AppColors.InfoText, 18).HorizontalAlignment(HorizontalAlignment.Center).VerticalAlignment(VerticalAlignment.Center))
                            .Grid(column: 0),
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(10)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Children(
                                new TextBlock().Text("Sửa danh mục").FontFamily(AppTypography.Fonts.SemiBoldFont).FontSize(18).FontWeight(Weight(600)).Foreground(AppColors.Brushes.Text),
                                badge)
                            .Grid(column: 1),
                        close.Grid(column: 2)));
    }

    private UIElement BuildBody() =>
        new ScrollViewer()
            .MaxHeight(560)
            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
            .Content(
                new StackPanel()
                    .Spacing(16)
                    .Padding(new Thickness(24, 20, 24, 20))
                    .Children(
                        BuildError(),
                        BuildNameField(),
                        BuildParentField(),
                        BuildContextCards()));

    private UIElement BuildError() =>
        new Border()
            .Visibility(x => x.Binding(() => ViewModel.HasError).Convert((bool value) => ShowIf(value)))
            .Background(AppColors.Brushes.DangerFill)
            .BorderBrush(AppColors.Brushes.DangerStroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(12, 8, 12, 8))
            .Child(new TextBlock()
                .FontFamily(AppTypography.Fonts.RegularFont)
                .FontSize(13)
                .FontWeight(Weight(400))
                .Foreground(AppColors.Brushes.DangerText)
                .TextWrapping(TextWrapping.Wrap)
                .Text(x => x.Binding(() => ViewModel.ErrorMessage)));

    private UIElement BuildNameField() =>
        new StackPanel()
            .Spacing(6)
            .Children(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(2)
                    .Children(
                        Label("Tên danh mục"),
                        new TextBlock().Text("*").FontSize(13).FontWeight(Weight(700)).Foreground(AppColors.Brushes.DangerText)),
                new TextBox()
                    .Style(AppControlStyles.Styles.TextBox)
                    .FontFamily(AppTypography.Fonts.MediumFont)
                    .FontSize(14)
                    .FontWeight(Weight(500))
                    .Height(40)
                    .CornerRadius(new CornerRadius(6))
                    .Padding(new Thickness(14, 8, 14, 0))
                    .VerticalContentAlignment(VerticalAlignment.Center)
                    .Background(AppColors.Brushes.Surface)
                    .Foreground(AppColors.Brushes.Text)
                    .BorderBrush(AppColors.Brushes.Stroke)
                    .BorderThickness(new Thickness(1))
                    .Text(x => x.Binding(() => ViewModel.Name).Mode(BindingMode.TwoWay)));

    private UIElement BuildParentField() =>
        new StackPanel()
            .Spacing(6)
            .Children(
                Label("Danh mục cha"),
                new ComboBox()
                    .Style(AppControlStyles.Styles.ComboBox)
                    .FontFamily(AppTypography.Fonts.RegularFont)
                    .FontSize(14)
                    .FontWeight(Weight(400))
                    .Height(40)
                    .CornerRadius(new CornerRadius(6))
                    .Padding(new Thickness(14, 8, 14, 0))
                    .VerticalContentAlignment(VerticalAlignment.Center)
                    .Background(AppColors.Brushes.Surface)
                    .Foreground(AppColors.Brushes.Text)
                    .BorderBrush(AppColors.Brushes.Stroke)
                    .BorderThickness(new Thickness(1))
                    .ItemsSource(ViewModel.ParentOptions)
                    .DisplayMemberPath(nameof(ParentCategoryOption.DisplayName))
                    .SelectedItem(x => x.Binding(() => ViewModel.SelectedParentOption).Mode(BindingMode.TwoWay)),
                new TextBlock()
                    .FontFamily(AppTypography.Fonts.RegularFont)
                    .FontSize(12)
                    .FontWeight(Weight(400))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .Text(x => x.Binding(() => ViewModel.ParentHint)));

    private UIElement BuildContextCards() =>
        new Grid().Children(
            ContextCard(
                isRoot: true,
                icon: Icon("info", AppColors.InfoText, 20),
                background: AppColors.Brushes.EditContextFill,
                border: AppColors.Brushes.EditContextStroke),
            ContextCard(
                isRoot: false,
                icon: Icon("account_tree", AppColors.InfoText, 18),
                background: AppColors.Brushes.Subtle,
                border: AppColors.Brushes.Stroke));

    private UIElement ContextCard(
        bool isRoot,
        FrameworkElement icon,
        Action<IDependencyPropertyBuilder<Microsoft.UI.Xaml.Media.Brush>> background,
        Action<IDependencyPropertyBuilder<Microsoft.UI.Xaml.Media.Brush>> border) =>
        new Border()
            .Visibility(x => x.Binding(() => ViewModel.IsRoot).Convert((bool value) => ShowIf(isRoot ? value : !value)))
            .Background(background)
            .BorderBrush(border)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(12))
            .Child(
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(GridLength.Auto),
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)))
                    .ColumnSpacing(10)
                    .Children(
                        icon.VerticalAlignment(VerticalAlignment.Top).Grid(column: 0),
                        new TextBlock()
                            .FontFamily(AppTypography.Fonts.RegularFont)
                            .FontSize(12)
                            .FontWeight(Weight(400))
                            .Foreground(AppColors.Brushes.Text)
                            .TextWrapping(TextWrapping.Wrap)
                            .Text(x => x.Binding(() => ViewModel.ContextText))
                            .Grid(column: 1)));

    private UIElement BuildDescriptionField() =>
        new StackPanel()
            .Spacing(6)
            .Children(
                Label("Mô tả chi tiết"),
                new TextBox()
                    .Style(AppControlStyles.Styles.TextBox)
                    .MinHeight(76)
                    .AcceptsReturn(true)
                    .TextWrapping(TextWrapping.Wrap)
                    .Padding(new Thickness(14, 8, 14, 8))
                    .Background(AppColors.Brushes.Surface)
                    .Foreground(AppColors.Brushes.Text)
                    .BorderBrush(AppColors.Brushes.Stroke)
                    .Text(x => x.Binding(() => ViewModel.Description).Mode(BindingMode.TwoWay)));

    private UIElement BuildActiveField() =>
        new Border()
            .Background(AppColors.Brushes.Subtle)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(8))
            .Padding(new Thickness(12, 10, 12, 10))
            .Child(
                new Grid()
                    .ColumnDefinitions(
                        new ColumnDefinition().Width(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition().Width(GridLength.Auto))
                    .Children(
                        new StackPanel()
                            .Spacing(2)
                            .Children(
                                new TextBlock().Style(AppResources.GetStyle(AppTypography.BodyText)).Foreground(AppColors.Brushes.Text).Text("Trạng thái kích hoạt"),
                                new TextBlock().Style(AppResources.GetStyle(AppTypography.CaptionText)).Foreground(AppColors.Brushes.SecondaryText).Text("Cho phép danh mục xuất hiện trong các luồng bán hàng."))
                            .Grid(column: 0),
                        new ToggleSwitch()
                            .OnContent(string.Empty)
                            .OffContent(string.Empty)
                            .IsOn(x => x.Binding(() => ViewModel.IsActive).Mode(BindingMode.TwoWay))
                            .Grid(column: 1)));

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
                            .FontFamily(AppTypography.Fonts.MediumFont)
                            .FontSize(13)
                            .FontWeight(Weight(500))
                            .Content("Hủy")
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
                            .Style(AppControlStyles.Styles.PrimaryButton)
                            .FontFamily(AppTypography.Fonts.SemiBoldFont)
                            .FontSize(13)
                            .FontWeight(Weight(600))
                            .CornerRadius(new CornerRadius(6))
                            .Background(AppColors.Brushes.Primary)
                            .Foreground(AppColors.Brushes.OnPrimary)
                            .Height(40)
                            .MinWidth(0)
                            .Content(new Grid()
                                .ColumnDefinitions(
                                    new ColumnDefinition().Width(GridLength.Auto),
                                    new ColumnDefinition().Width(GridLength.Auto))
                                .ColumnSpacing(6)
                                .Children(
                                    new ProgressRing().Width(16).Height(16).Foreground(AppColors.Brushes.OnPrimary)
                                        .IsActive(x => x.Binding(() => ViewModel.IsBusy))
                                        .Visibility(x => x.Binding(() => ViewModel.IsBusy).Convert((bool busy) => ShowIf(busy))).Grid(column: 0),
                                    new TextBlock().FontFamily(AppTypography.Fonts.SemiBoldFont).FontSize(13).FontWeight(Weight(600)).Foreground(AppColors.Brushes.OnPrimary)
                                        .Text(x => x.Binding(() => ViewModel.IsBusy).Convert((bool busy) => busy ? "Đang lưu..." : "Lưu thay đổi"))
                                        .Grid(column: 1)))
                            .IsEnabled(x => x.Binding(() => ViewModel.CanSave))
                            .Command(x => x.Binding(() => ViewModel.SaveCommand))));

    private static TextBlock Label(string text) =>
        new TextBlock()
            .FontFamily(AppTypography.Fonts.MediumFont)
            .FontSize(13)
            .Foreground(AppColors.Brushes.Text)
            .FontWeight(Weight(500))
            .Text(text);

    private static Windows.UI.Text.FontWeight Weight(ushort value) => new() { Weight = value };

    private static Visibility ShowIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
