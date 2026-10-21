using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
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

public sealed class ProductEditorPage : Page, IRoutePage
{
    private readonly Uri _apiBase;

    public ProductEditorViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public ProductEditorPage(ProductEditorViewModel viewModel, ApiClientOptions apiOptions)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        _apiBase = apiOptions.Validate();

        var titleBlock = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.PageTitle))
            .Style(AppResources.GetStyle(AppTypography.HeadingText))
            .Foreground(AppColors.Brushes.Text)
            .IsTabStop(true);

        Loaded += (_, _) => titleBlock.Focus(FocusState.Programmatic);

        var backButton = new Button()
            .Content("←  Quay lại")
            .Command(x => x.Binding(() => ViewModel.CancelCommand))
            .Style(AppControlStyles.Styles.QuietButton)
            .HorizontalAlignment(HorizontalAlignment.Left);

        var generalErrorBanner = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.GeneralError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasGeneralError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var headerStack = new StackPanel()
            .Spacing(10)
            .Children(
                backButton,
                titleBlock,
                generalErrorBanner);

        var loadingRing = new ProgressRing()
            .IsActive(x => x.Binding(() => ViewModel.IsLoading))
            .Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center);

        var emptyState = new EmptyStateControl()
            .Title("Không tìm thấy sản phẩm")
            .Description("Sản phẩm có thể đã bị xóa hoặc không còn khả dụng.")
            .ActionText("Quay lại danh sách")
            .ActionCommand(x => x.Binding(() => ViewModel.BackToListCommand))
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

        // 1. Basic Info & Image preview
        var imagePreview = new Border()
            .CornerRadius(AppMetrics.Values.CardRadius)
            .Background(AppColors.Brushes.Subtle)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .MinHeight(260)
            .Child(CatalogViewParts.ProductImage(x => x.Binding(() => ViewModel.ImageUrl), _apiBase, 44));

        var imageUrlBox = new TextBox()
            .Header("URL ảnh")
            .Text(x => x.Binding(() => ViewModel.ImageUrl).Mode(BindingMode.TwoWay))
            .Style(AppControlStyles.Styles.TextBox);

        var imageUrlError = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.ImageUrlError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasImageUrlError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var leftImageCol = new StackPanel()
            .Spacing(10)
            .Children(
                imagePreview,
                imageUrlBox,
                imageUrlError);

        var nameBox = new TextBox()
            .Header("Tên sản phẩm *")
            .Text(x => x.Binding(() => ViewModel.Name).Mode(BindingMode.TwoWay))
            .Style(AppControlStyles.Styles.TextBox);

        var nameError = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.NameError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasNameError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var categoryBox = new ComboBox()
            .Header("Danh mục *")
            .ItemsSource(x => x.Binding(() => ViewModel.Categories))
            .DisplayMemberPath("DisplayPath")
            .SelectedValuePath("Id")
            .SelectedValue(x => x.Binding(() => ViewModel.SelectedCategoryId).Mode(BindingMode.TwoWay))
            .HorizontalAlignment(HorizontalAlignment.Stretch);

        var categoryError = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.CategoryError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasCategoryError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var brandBox = new TextBox()
            .Header("Thương hiệu *")
            .Text(x => x.Binding(() => ViewModel.Brand).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged))
            .Style(AppControlStyles.Styles.TextBox);

        var brandHint = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.BrandHint))
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.SecondaryText)
            .TextWrapping(TextWrapping.Wrap)
            .Visibility(x => x.Binding(() => ViewModel.HasBrandHint).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var brandSuggestions = new ItemsControl()
            .ItemsSource(x => x.Binding(() => ViewModel.BrandSuggestions))
            .ItemTemplate<SpecChoice>(BuildBrandSuggestion)
            .Visibility(x => x.Binding(() => ViewModel.HasBrandSuggestions).Convert((bool v) => CatalogViewParts.ShowIf(v)));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(brandSuggestions, "Gợi ý thương hiệu");

        var brandError = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.BrandError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasBrandError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var skuBox = new TextBox()
            .Header("SKU *")
            .Text(x => x.Binding(() => ViewModel.Sku).Mode(BindingMode.TwoWay))
            .IsReadOnly(x => x.Binding(() => ViewModel.IsEditMode))
            .Style(AppControlStyles.Styles.TextBox);

        var skuError = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.SkuError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasSkuError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var barcodeBox = new TextBox()
            .Header("Barcode")
            .Text(x => x.Binding(() => ViewModel.Barcode).Mode(BindingMode.TwoWay))
            .IsEnabled(x => x.Binding(() => ViewModel.IsBarcodeEnabled))
            .Style(AppControlStyles.Styles.TextBox);

        var barcodeHint = new TextBlock()
            .Text("Sản phẩm có biến thể dùng barcode của từng biến thể.")
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.SecondaryText)
            .TextWrapping(TextWrapping.Wrap)
            .Visibility(x => x.Binding(() => ViewModel.HasVariants).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var barcodeError = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.BarcodeError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasBarcodeError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var descBox = new TextBox()
            .Header("Mô tả")
            .Text(x => x.Binding(() => ViewModel.Description).Mode(BindingMode.TwoWay))
            .AcceptsReturn(true)
            .TextWrapping(TextWrapping.Wrap)
            .MinHeight(64)
            .Style(AppControlStyles.Styles.TextBox);

        var descError = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.DescriptionError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasDescriptionError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var rightInfoCol = new StackPanel()
            .Spacing(12)
            .Children(
                CatalogViewParts.SectionTitle("1. Thông tin cơ bản"),
                new StackPanel().Spacing(4).Children(nameBox, nameError),
                new Grid()
                    .ColumnSpacing(12)
                    .ColumnDefinitions("6*, 5*")
                    .Children(
                        new StackPanel().Spacing(4).Children(categoryBox, categoryError),
                        new StackPanel().Spacing(4).Grid(column: 1).Children(brandBox, brandHint, brandSuggestions, brandError)),
                new Grid()
                    .ColumnSpacing(12)
                    .ColumnDefinitions("1*, 1*")
                    .Children(
                        new StackPanel().Spacing(4).Children(skuBox, skuError),
                        new StackPanel().Spacing(4).Grid(column: 1).Children(barcodeBox, barcodeHint, barcodeError)),
                new StackPanel().Spacing(4).Children(descBox, descError));

        var topSectionGrid = new Grid()
            .ColumnSpacing(20)
            .ColumnDefinitions("1*, 2*")
            .Children(
                leftImageCol,
                rightInfoCol.Grid(column: 1));

        // 2. Pricing & Margin section
        var basePriceBox = new TextBox()
            .Header("Giá bán * (₫)")
            .Text(x => x.Binding(() => ViewModel.BasePriceText).Mode(BindingMode.TwoWay))
            .InputScope(new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } })
            .Style(AppControlStyles.Styles.TextBox);

        var basePriceError = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.BasePriceError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasBasePriceError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var costPriceBox = new TextBox()
            .Header("Giá vốn tham chiếu * (₫)")
            .Text(x => x.Binding(() => ViewModel.CostPriceText).Mode(BindingMode.TwoWay))
            .InputScope(new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } })
            .Style(AppControlStyles.Styles.TextBox);

        var costPriceError = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.CostPriceError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasCostPriceError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var marginProfitText = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.MarginText))
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.SuccessText)
            .VerticalAlignment(VerticalAlignment.Center)
            .Visibility(x => x.Binding(() => ViewModel.HasMargin && !ViewModel.IsMarginLoss).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var marginLossText = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.MarginText))
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.WarningText)
            .VerticalAlignment(VerticalAlignment.Center)
            .Visibility(x => x.Binding(() => ViewModel.HasMargin && ViewModel.IsMarginLoss).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var pricingSection = new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new Grid()
                            .ColumnDefinitions("*, Auto")
                            .Children(
                                CatalogViewParts.SectionTitle("2. Giá bán & giá vốn"),
                                new StackPanel().Grid(column: 1).Orientation(Orientation.Horizontal).Children(marginProfitText, marginLossText)),
                        new Grid()
                            .ColumnSpacing(12)
                            .ColumnDefinitions("1*, 1*")
                            .Children(
                                new StackPanel().Spacing(4).Children(basePriceBox, basePriceError),
                                new StackPanel().Spacing(4).Grid(column: 1).Children(costPriceBox, costPriceError))));

        // 3. Specs section
        var specsHeader = new Grid()
            .ColumnDefinitions("*, Auto")
            .Children(
                CatalogViewParts.SectionTitle("3. Thông số kỹ thuật"),
                new Button()
                    .Grid(column: 1)
                    .Content("+ Thêm thông số")
                    .Command(x => x.Binding(() => ViewModel.AddSpecCommand))
                    .Style(AppControlStyles.Styles.Button));

        var specsGeneralError = new TextBlock()
            .Text(x => x.Binding(() => ViewModel.SpecsError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => ViewModel.HasSpecsError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var specsSection = new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        specsHeader,
                        new TextBlock()
                            .Text(x => x.Binding(() => ViewModel.SpecCatalogMessage))
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.WarningText)
                            .TextWrapping(TextWrapping.Wrap)
                            .Visibility(x => x.Binding(() => ViewModel.HasSpecCatalogMessage).Convert((bool v) => CatalogViewParts.ShowIf(v))),
                        specsGeneralError,
                        new ItemsControl()
                            .ItemsSource(x => x.Binding(() => ViewModel.Specs))
                            .ItemTemplate<SpecRowItem>(BuildSpecRow)));

        // 4. Variants section
        var variantsHeader = new Grid()
            .ColumnDefinitions("*, Auto")
            .Children(
                CatalogViewParts.SectionTitle("4. Biến thể"),
                new Button()
                    .Grid(column: 1)
                    .Content("+ Thêm biến thể")
                    .Command(x => x.Binding(() => ViewModel.AddVariantCommand))
                    .Style(AppControlStyles.Styles.Button));

        var variantsSection = new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        variantsHeader,
                        new ItemsControl()
                            .ItemsSource(x => x.Binding(() => ViewModel.Variants))
                            .ItemTemplate<VariantRowItem>(BuildVariantRow)));

        // Serial Tracking Switch
        var serialSwitch = new ToggleSwitch()
            .Header("Quản lý từng máy bằng Serial/IMEI")
            .IsOn(x => x.Binding(() => ViewModel.IsSerialTracked).Mode(BindingMode.TwoWay));

        // Footer Actions
        var saveProgress = new ProgressRing()
            .IsActive(x => x.Binding(() => ViewModel.IsSaving))
            .Visibility(x => x.Binding(() => ViewModel.IsSaving).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var footerActions = new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(12)
            .HorizontalAlignment(HorizontalAlignment.Right)
            .Children(
                saveProgress,
                new Button()
                    .Content("Hủy bỏ")
                    .Command(x => x.Binding(() => ViewModel.CancelCommand))
                    .Style(AppControlStyles.Styles.Button),
                new Button()
                    .Content("Lưu thay đổi")
                    .Command(x => x.Binding(() => ViewModel.SaveCommand))
                    .Style(AppControlStyles.Styles.PrimaryButton));

        var formScroll = new ScrollViewer()
            .Visibility(x => x.Binding(() => ViewModel.IsContent).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Content(
                // Stretch (not Left): a left-aligned panel is only as wide as its content, so star columns collapse.
                new StackPanel()
                    .Spacing(18)
                    .MaxWidth(1100)
                    .Children(
                        topSectionGrid,
                        pricingSection,
                        specsSection,
                        variantsSection,
                        serialSwitch,
                        footerActions));

        var contentGrid = new Grid()
            .Children(
                loadingRing,
                emptyState,
                errorPanel,
                formScroll);

        Content = new Grid()
            .Padding(new Thickness(24))
            .RowSpacing(18)
            .RowDefinitions("Auto, *")
            .Background(AppColors.Brushes.Canvas)
            .Children(
                headerStack,
                contentGrid.Grid(row: 1));
    }

    private UIElement BuildSpecRow(SpecRowItem row) => BuildSpecRowContent(row, ViewModel.RemoveSpecCommand);

    // `row` is null while the template is built; it is only used inside binding lambdas (AGENTS.md item templates).
    private UIElement BuildSpecRowContent(SpecRowItem row, System.Windows.Input.ICommand removeCommand)
    {
        var nameBox = new TextBox()
            .PlaceholderText("Tên thông số, ví dụ RAM hoặc Tần số quét (Hz)")
            .Text(x => x.Binding(() => row.Name).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged))
            .Style(AppControlStyles.Styles.TextBox);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(nameBox, "Tên thông số");

        var kindBox = new ComboBox()
            .ItemsSource(new[] { "Chữ", "Số", "Có/Không" })
            .SelectedIndex(x => x.Binding(() => row.KindIndex).Mode(BindingMode.TwoWay))
            .IsEnabled(x => x.Binding(() => row.IsKindEditable))
            .HorizontalAlignment(HorizontalAlignment.Stretch);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(kindBox, "Kiểu giá trị");

        var valBox = new TextBox()
            .PlaceholderText("Giá trị")
            .Text(x => x.Binding(() => row.Value).Mode(BindingMode.TwoWay))
            .Style(AppControlStyles.Styles.TextBox);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(valBox, "Giá trị thông số");

        var unitText = new TextBlock()
            .Text(x => x.Binding(() => row.Unit))
            .Style(AppResources.GetStyle(AppTypography.BodyText))
            .Foreground(AppColors.Brushes.SecondaryText)
            .VerticalAlignment(VerticalAlignment.Center)
            .Visibility(x => x.Binding(() => row.HasUnit).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var removeBtn = new Button()
            .Content("Bỏ")
            .Command(removeCommand)
            .CommandParameter(x => x.Binding(() => row))
            .Style(AppControlStyles.Styles.QuietButton);

        var inputGrid = new Grid()
            .ColumnSpacing(10)
            .ColumnDefinitions("3*, 120, 2*, Auto, Auto")
            .Children(
                nameBox,
                kindBox.Grid(column: 1),
                valBox.Grid(column: 2),
                unitText.Grid(column: 3),
                removeBtn.Grid(column: 4));

        var keyLine = new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(8)
            .Children(
                new TextBlock()
                    .Text(x => x.Binding(() => row.KeyCaption))
                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .VerticalAlignment(VerticalAlignment.Center),
                new HyperlinkButton()
                    .Content("Sửa key")
                    .Command(ViewModel.ToggleSpecKeyEditorCommand)
                    .CommandParameter(x => x.Binding(() => row))
                    .Padding(new Thickness(4, 0, 4, 0))
                    .MinHeight(0)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Visibility(x => x.Binding(() => row.CanEditKey).Convert((bool v) => CatalogViewParts.ShowIf(v))));

        var keyBox = new TextBox()
            .Header("Key (chữ thường, số, dấu gạch dưới)")
            .Text(x => x.Binding(() => row.Key).Mode(BindingMode.TwoWay))
            .Style(AppControlStyles.Styles.TextBox)
            .MaxWidth(360)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Visibility(x => x.Binding(() => row.IsKeyEditorOpen).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var suggestions = new ItemsControl()
            .ItemsSource(x => x.Binding(() => row.Suggestions))
            .ItemTemplate<SpecSuggestion>(BuildSpecSuggestion)
            .Visibility(x => x.Binding(() => row.HasSuggestions).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var errorText = new TextBlock()
            .Text(x => x.Binding(() => row.Error))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => row.HasError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        return new StackPanel()
            .Spacing(4)
            .Margin(new Thickness(0, 0, 0, 10))
            .Children(inputGrid, keyLine, keyBox, suggestions, errorText);
    }

    // `choice` is null while the template is built; it is only used inside binding lambdas.
    private UIElement BuildBrandSuggestion(SpecChoice choice) =>
        new Button()
            .Content(x => x.Binding(() => choice.DisplayName))
            .Command(ViewModel.PickBrandCommand)
            .CommandParameter(x => x.Binding(() => choice))
            .Style(AppControlStyles.Styles.QuietButton)
            .HorizontalAlignment(HorizontalAlignment.Left);

    private UIElement BuildSpecSuggestion(SpecSuggestion suggestion) =>
        new Button()
            .Content(x => x.Binding(() => suggestion.DisplayName))
            .Command(ViewModel.PickSpecSuggestionCommand)
            .CommandParameter(x => x.Binding(() => suggestion))
            .Style(AppControlStyles.Styles.QuietButton)
            .HorizontalAlignment(HorizontalAlignment.Left);

    private UIElement BuildVariantRow(VariantRowItem variant)
    {
        var nameBox = new TextBox()
            .Header("Tên biến thể *")
            .Text(x => x.Binding(() => variant.VariantName).Mode(BindingMode.TwoWay))
            .Style(AppControlStyles.Styles.TextBox);

        var nameError = new TextBlock()
            .Text(x => x.Binding(() => variant.NameError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => variant.HasNameError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var skuBox = new TextBox()
            .Header("SKU")
            .Text(x => x.Binding(() => variant.Sku).Mode(BindingMode.TwoWay))
            .IsReadOnly(x => x.Binding(() => variant.IsSkuEditable).Convert((bool e) => !e))
            .Style(AppControlStyles.Styles.TextBox);

        var skuError = new TextBlock()
            .Text(x => x.Binding(() => variant.SkuError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => variant.HasSkuError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var priceBox = new TextBox()
            .Header("Giá bán * (₫)")
            .Text(x => x.Binding(() => variant.PriceText).Mode(BindingMode.TwoWay))
            .InputScope(new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } })
            .Style(AppControlStyles.Styles.TextBox);

        var priceError = new TextBlock()
            .Text(x => x.Binding(() => variant.PriceError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => variant.HasPriceError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var barcodeBox = new TextBox()
            .Header("Barcode")
            .Text(x => x.Binding(() => variant.Barcode).Mode(BindingMode.TwoWay))
            .Style(AppControlStyles.Styles.TextBox);

        var barcodeError = new TextBlock()
            .Text(x => x.Binding(() => variant.BarcodeError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => variant.HasBarcodeError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var activeCheck = new CheckBox()
            .Content("Đang bán")
            .IsChecked(x => x.Binding(() => variant.IsActive).Mode(BindingMode.TwoWay))
            .VerticalAlignment(VerticalAlignment.Bottom);

        var removeBtn = new Button()
            .Content("Bỏ")
            .Command(ViewModel.RemoveVariantCommand)
            .CommandParameter(x => x.Binding(() => variant))
            .Style(AppControlStyles.Styles.QuietButton)
            .VerticalAlignment(VerticalAlignment.Bottom)
            .Visibility(x => x.Binding(() => variant.IsSaved).Convert((bool s) => CatalogViewParts.ShowIf(!s)));

        var rowGrid = new Grid()
            .ColumnSpacing(10)
            .ColumnDefinitions("6*, 5*, 5*, 5*, Auto, Auto")
            .Children(
                new StackPanel().Spacing(4).Children(nameBox, nameError),
                new StackPanel().Spacing(4).Grid(column: 1).Children(skuBox, skuError),
                new StackPanel().Spacing(4).Grid(column: 2).Children(priceBox, priceError),
                new StackPanel().Spacing(4).Grid(column: 3).Children(barcodeBox, barcodeError),
                activeCheck.Grid(column: 4),
                removeBtn.Grid(column: 5));

        var variantSpecsError = new TextBlock()
            .Text(x => x.Binding(() => variant.SpecsError))
            .Style(AppControlStyles.Styles.FieldError)
            .Foreground(AppColors.Brushes.DangerText)
            .Visibility(x => x.Binding(() => variant.HasSpecsError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var subSpecsHeader = new Grid()
            .ColumnDefinitions("*, Auto")
            .Children(
                new TextBlock()
                    .Text("Thông số riêng:")
                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .VerticalAlignment(VerticalAlignment.Center),
                new Button()
                    .Grid(column: 1)
                    .Content("+ Thêm thông số")
                    .Command(ViewModel.AddVariantSpecCommand)
                    .CommandParameter(x => x.Binding(() => variant))
                    .Style(AppControlStyles.Styles.QuietButton));

        var subSpecsList = new ItemsControl()
            .ItemsSource(x => x.Binding(() => variant.Specs))
            .ItemTemplate<SpecRowItem>(BuildVariantSpecRow);

        return new Border()
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
                        rowGrid,
                        variantSpecsError,
                        subSpecsHeader,
                        subSpecsList));
    }

    private UIElement BuildVariantSpecRow(SpecRowItem spec) => BuildSpecRowContent(spec, ViewModel.RemoveVariantSpecCommand);

    public void SetRoute(Guid? productId, ProductQuery? returnQuery) =>
        ViewModel.SetRoute(productId, returnQuery);
}
