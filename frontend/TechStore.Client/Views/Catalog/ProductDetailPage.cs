using System.Collections.Generic;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Catalog;

public sealed class ProductDetailPage : Page, IRoutePage
{
    public ProductDetailViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public ProductDetailPage(ProductDetailViewModel viewModel, ApiClientOptions apiOptions)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        var apiBase = apiOptions.Validate();

        var heading = new TextBlock()
            .Text("Chi tiết sản phẩm")
            .Style(AppResources.GetStyle(AppTypography.HeadingText))
            .Foreground(AppColors.Brushes.Text)
            .IsTabStop(true);

        Loaded += (_, _) => heading.Focus(FocusState.Programmatic);

        var actionBar = new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(10)
            .Visibility(x => x.Binding(() => ViewModel.CanManage).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Children(
                new Button()
                    .Content("Sửa")
                    .Command(x => x.Binding(() => ViewModel.EditCommand))
                    .Style(AppControlStyles.Styles.Button),
                new Button()
                    .Content(x => x.Binding(() => ViewModel.ToggleStatusText))
                    .Command(x => x.Binding(() => ViewModel.ToggleStatusCommand))
                    .Style(AppControlStyles.Styles.Button),
                new Button()
                    .Content("Xóa")
                    .Command(x => x.Binding(() => ViewModel.DeleteCommand))
                    .Style(AppControlStyles.Styles.DangerButton));

        var actionErrorBanner = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.ActionError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasActionError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var backButton = new Button()
            .Content("←  Quay lại danh sách")
            .Command(x => x.Binding(() => ViewModel.BackCommand))
            .Style(AppControlStyles.Styles.QuietButton)
            .HorizontalAlignment(HorizontalAlignment.Left);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(backButton, "Quay lại danh sách sản phẩm");

        var headerStack = new StackPanel()
            .Spacing(10)
            .Children(
                backButton,
                heading,
                actionBar,
                actionErrorBanner);

        var loadingRing = new ProgressRing()
            .IsActive(x => x.Binding(() => ViewModel.IsLoading))
            .Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center);

        var emptyState = new EmptyStateControl()
            .Title("Không tìm thấy sản phẩm")
            .Description("Sản phẩm có thể đã bị xóa hoặc không còn khả dụng. Quay lại danh sách để tiếp tục.")
            .ActionText("Quay lại danh sách")
            .ActionCommand(x => x.Binding(() => ViewModel.BackCommand))
            .IconSource(AppIcons.Get(AppIcons.EmptyIcon))
            .Visibility(x => x.Binding(() => ViewModel.IsNotFound).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var errorPanel = new StackPanel()
            .Spacing(12)
            .Visibility(x => x.Binding(() => ViewModel.IsError).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Children(
                new TextBlock()
                    .Text(x => x.Binding(() => ViewModel.ErrorMessage))
                    .Style(AppControlStyles.Styles.FieldError)
                    .Foreground(AppColors.Brushes.DangerText),
                new Button()
                    .Content("Thử tải lại")
                    .Command(x => x.Binding(() => ViewModel.RetryCommand))
                    .Style(AppControlStyles.Styles.Button)
                    .HorizontalAlignment(HorizontalAlignment.Left));

        var imageContainer = new Border()
            .CornerRadius(AppMetrics.Values.CardRadius)
            .Background(AppColors.Brushes.Subtle)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .MinHeight(260)
            .Child(CatalogViewParts.ProductImage(x => x.Binding(() => ViewModel.Product!.ImageUrl), apiBase, 44));

        var mainInfo = new StackPanel()
            .Spacing(12)
            .VerticalAlignment(VerticalAlignment.Top)
            .Children(
                new TextBlock()
                    .Text(x => x.Binding(() => ViewModel.Product!.Name))
                    .Style(AppResources.GetStyle(AppTypography.HeadingText))
                    .Foreground(AppColors.Brushes.Text)
                    .TextWrapping(TextWrapping.Wrap),
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(10)
                    .Children(
                        new StatusBadge()
                            .Label(x => x.Binding(() => ViewModel.Product!.IsActive).Convert((bool a) => CatalogPresentation.ActiveLabel(a)))
                            .Tone(x => x.Binding(() => ViewModel.Product!.IsActive).Convert((bool a) => CatalogPresentation.ActiveTone(a))),
                        new TextBlock()
                            .Text(x => x.Binding(() => ViewModel.Product!.CategoryName))
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .VerticalAlignment(VerticalAlignment.Center)),
                new TextBlock()
                    .Text(x => x.Binding(() => ViewModel.Product).Convert((ProductDetail? p) => p is null ? string.Empty : $"SKU: {p.Sku}   ·   Hãng: {p.Brand}"))
                    .Style(AppResources.GetStyle(AppTypography.BodyText))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .TextWrapping(TextWrapping.Wrap),
                new TextBlock()
                    .Text(x => x.Binding(() => ViewModel.Product).Convert((ProductDetail? p) => p is null ? string.Empty : $"Barcode: {CatalogPresentation.OptionalText(p.Barcode)}   ·   Theo dõi serial: {CatalogPresentation.SerialTrackedLabel(p.IsSerialTracked)}"))
                    .Style(AppResources.GetStyle(AppTypography.BodyText))
                    .Foreground(AppColors.Brushes.Text)
                    .TextWrapping(TextWrapping.Wrap),
                new TextBlock()
                    .Text(x => x.Binding(() => ViewModel.Product!.BasePrice).Convert((decimal pr) => CatalogPresentation.FormatMoney(pr)))
                    .Style(AppTypography.Styles.MetricText)
                    .Foreground(AppColors.Brushes.Text),
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(6)
                    .Children(
                        new TextBlock()
                            .Text("Giá vốn:")
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.SecondaryText),
                        new TextBlock()
                            .Text(x => x.Binding(() => ViewModel.Product!.CostPrice).Convert((decimal cp) => CatalogPresentation.FormatMoney(cp)))
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.SecondaryText)));

        var topGrid = new Grid()
            .ColumnSpacing(20)
            .RowSpacing(16)
            .ColumnDefinitions("280, *")
            .Children(
                imageContainer,
                mainInfo.Grid(column: 1));

        var descriptionPanel = new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        CatalogViewParts.SectionTitle("Mô tả"),
                        new TextBlock()
                            .Text(x => x.Binding(() => ViewModel.Product!.Description).Convert((string? d) => CatalogPresentation.OptionalText(d)))
                            .TextWrapping(TextWrapping.Wrap)
                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                            .Foreground(AppColors.Brushes.Text)));

        var specsPanel = new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        CatalogViewParts.SectionTitle("Thông số sản phẩm"),
                        new ItemsControl()
                            .ItemsSource(x => x.Binding(() => ViewModel.Product!.Specs))
                            .ItemTemplate<KeyValuePair<string, JsonElement>>(pair =>
                                new Grid()
                                    .Padding(new Thickness(0, 6, 0, 6))
                                    .ColumnSpacing(18)
                                    .ColumnDefinitions("180, *")
                                    .Children(
                                        new TextBlock()
                                            .Text(x => x.Binding(() => pair.Key))
                                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                                            .Foreground(AppColors.Brushes.SecondaryText),
                                        new TextBlock()
                                            .Grid(column: 1)
                                            .Text(x => x.Binding(() => pair.Value).Convert((JsonElement elem) => CatalogPresentation.SpecValueText(elem)))
                                            .Style(AppResources.GetStyle(AppTypography.BodyText))
                                            .Foreground(AppColors.Brushes.Text)
                                            .TextWrapping(TextWrapping.Wrap))),
                        new TextBlock()
                            .Text("Không có thông số bổ sung.")
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .Visibility(x => x.Binding(() => ViewModel.HasNoSpecs).Convert((bool v) => CatalogViewParts.ShowIf(v)))));

        var variantsPanel = new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        CatalogViewParts.SectionTitle("Biến thể"),
                        new ItemsControl()
                            .ItemsSource(x => x.Binding(() => ViewModel.Product!.Variants))
                            .ItemTemplate<ProductVariant>(v =>
                                new Border()
                                    .Padding(new Thickness(14, 12, 14, 12))
                                    .Margin(new Thickness(0, 0, 0, 8))
                                    .CornerRadius(AppMetrics.Values.CardRadius)
                                    .Background(AppColors.Brushes.Canvas)
                                    .BorderBrush(AppColors.Brushes.Stroke)
                                    .BorderThickness(new Thickness(1))
                                    .Child(
                                        new StackPanel()
                                            .Spacing(8)
                                            .Children(
                                                new Grid()
                                                    .ColumnSpacing(12)
                                                    .ColumnDefinitions("*, Auto")
                                                    .Children(
                                                        new StackPanel()
                                                            .Spacing(4)
                                                            .Children(
                                                                new TextBlock()
                                                                    .Text(x => x.Binding(() => v.VariantName))
                                                                    .Style(AppResources.GetStyle(AppTypography.BodyText))
                                                                    .Foreground(AppColors.Brushes.Text),
                                                                new TextBlock()
                                                                    .Text(x => x.Binding(() => v).Convert((ProductVariant pv) => pv is null ? string.Empty : $"SKU: {pv.Sku}   ·   Barcode: {CatalogPresentation.OptionalText(pv.Barcode)}"))
                                                                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                                                                    .Foreground(AppColors.Brushes.SecondaryText)
                                                                    .TextWrapping(TextWrapping.Wrap)),
                                                        new TextBlock()
                                                            .Grid(column: 1)
                                                            .Text(x => x.Binding(() => v.Price).Convert((decimal pr) => CatalogPresentation.FormatMoney(pr)))
                                                            .Style(AppResources.GetStyle(AppTypography.SectionText))
                                                            .Foreground(AppColors.Brushes.Text)
                                                            .VerticalAlignment(VerticalAlignment.Center)),
                                                new ItemsControl()
                                                    .ItemsSource(x => x.Binding(() => v.EffectiveSpecs))
                                                    .ItemTemplate<KeyValuePair<string, JsonElement>>(p =>
                                                        new TextBlock()
                                                            .Text(x => x.Binding(() => p).Convert((KeyValuePair<string, JsonElement> pair) => $"{pair.Key}: {CatalogPresentation.SpecValueText(pair.Value)}"))
                                                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                                                            .Foreground(AppColors.Brushes.SecondaryText)
                                                            .TextWrapping(TextWrapping.Wrap))))),
                        new TextBlock()
                            .Text("Sản phẩm này không có biến thể.")
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .Visibility(x => x.Binding(() => ViewModel.HasNoVariants).Convert((bool v) => CatalogViewParts.ShowIf(v)))));

        var scrollContent = new ScrollViewer()
            .Visibility(x => x.Binding(() => ViewModel.IsContent).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Content(
                new StackPanel()
                    .Spacing(18)
                    .MaxWidth(1100)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .Children(
                        topGrid,
                        descriptionPanel,
                        specsPanel,
                        variantsPanel));

        var contentGrid = new Grid()
            .Children(
                loadingRing,
                emptyState,
                errorPanel,
                scrollContent);

        Content = new Grid()
            .Padding(new Thickness(24))
            .RowSpacing(18)
            .RowDefinitions("Auto, *")
            .Background(AppColors.Brushes.Canvas)
            .Children(
                headerStack,
                contentGrid.Grid(row: 1));
    }

    public void SetRoute(Guid productId, ProductQuery? returnCriteria, Guid? focusProductId) =>
        ViewModel.SetRoute(productId, returnCriteria, focusProductId);
}
