using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
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

public sealed class ProductListPage : Page, IRoutePage
{
    private readonly Uri _apiBase;
    private readonly TextBlock _listHeading;
    private readonly ScrollViewer _scrollViewer;
    private readonly ItemsRepeater _repeater;

    public ProductListViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public ProductListPage(ProductListViewModel viewModel, ApiClientOptions apiOptions)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        _apiBase = apiOptions.Validate();

        _listHeading = new TextBlock()
            .IsTabStop(true)
            .VerticalAlignment(VerticalAlignment.Center)
            .Style(AppResources.GetStyle(AppTypography.SectionText))
            .Foreground(AppColors.Brushes.Text)
            .Text(x => x.Binding(() => ViewModel.TotalCount).Convert((int count) => $"{count} sản phẩm"));
        AutomationProperties.SetName(_listHeading, "Số kết quả sản phẩm");

        var addProductButton = new Button()
            .Content("+ Thêm sản phẩm")
            .Command(x => x.Binding(() => ViewModel.CreateProductCommand))
            .Style(AppControlStyles.Styles.PrimaryButton)
            .Visibility(x => x.Binding(() => ViewModel.CanManage).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var refreshButton = new Button()
            .Content("Làm mới")
            .Command(x => x.Binding(() => ViewModel.RefreshCommand))
            .Style(AppControlStyles.Styles.Button);
        AutomationProperties.SetName(refreshButton, "Làm mới danh sách sản phẩm");

        var topRow = new Grid()
            .MinHeight(36)
            .ColumnSpacing(12)
            .ColumnDefinitions("*, Auto, Auto")
            .Children(
                _listHeading,
                addProductButton.Grid(column: 1),
                refreshButton.Grid(column: 2));

        var searchBox = new SearchBoxControl()
            .Label("Tìm kiếm sản phẩm")
            .PlaceholderText("Tên sản phẩm, SKU hoặc barcode")
            .Text(x => x.Binding(() => ViewModel.SearchText).Mode(BindingMode.TwoWay))
            .SearchCommand(x => x.Binding(() => ViewModel.SearchCommand))
            .ClearCommand(x => x.Binding(() => ViewModel.ClearSearchCommand));

        var searchButton = new Button()
            .Content("Tìm kiếm")
            .Command(x => x.Binding(() => ViewModel.SearchCommand))
            .Style(AppControlStyles.Styles.PrimaryButton)
            .VerticalAlignment(VerticalAlignment.Bottom);

        var searchRow = new Grid()
            .ColumnSpacing(10)
            .ColumnDefinitions("*, Auto")
            .Children(
                searchBox,
                searchButton.Grid(column: 1));

        var categoryBox = new ComboBox()
            .ItemsSource(x => x.Binding(() => ViewModel.Categories))
            .DisplayMemberPath("DisplayPath")
            .SelectedValuePath("Id")
            .SelectedValue(x => x.Binding(() => ViewModel.SelectedCategoryId).Mode(BindingMode.TwoWay))
            .PlaceholderText("Tất cả danh mục")
            .MinHeight(40);
        AutomationProperties.SetName(categoryBox, "Lọc theo danh mục");

        var brandBox = new TextBox()
            .Text(x => x.Binding(() => ViewModel.BrandText).Mode(BindingMode.TwoWay))
            .PlaceholderText("Nhập thương hiệu")
            .Style(AppControlStyles.Styles.TextBox)
            .MinHeight(40);
        AutomationProperties.SetName(brandBox, "Lọc theo thương hiệu");

        var activeStatusBox = new ComboBox()
            .SelectedValuePath("Tag")
            .SelectedValue(x => x.Binding(() => ViewModel.ActiveFilter).Mode(BindingMode.TwoWay))
            .MinHeight(40)
            .Items(
                new ComboBoxItem { Content = "Tất cả trạng thái", Tag = "all" },
                new ComboBoxItem { Content = "Đang hoạt động", Tag = "active" },
                new ComboBoxItem { Content = "Ngừng hoạt động", Tag = "inactive" });
        AutomationProperties.SetName(activeStatusBox, "Lọc trạng thái sản phẩm");

        var filterRow1 = new Grid()
            .ColumnSpacing(12)
            .RowSpacing(8)
            .ColumnDefinitions("1.15*, 1*, 1*")
            .Children(
                new StackPanel().Spacing(5).Children(CatalogViewParts.Caption("Danh mục"), categoryBox),
                new StackPanel().Spacing(5).Grid(column: 1).Children(CatalogViewParts.Caption("Thương hiệu"), brandBox),
                new StackPanel().Spacing(5).Grid(column: 2).Children(CatalogViewParts.Caption("Trạng thái sản phẩm"), activeStatusBox));

        var minPriceBox = new TextBox()
            .Text(x => x.Binding(() => ViewModel.MinPriceText).Mode(BindingMode.TwoWay))
            .PlaceholderText("Không giới hạn")
            .Style(AppControlStyles.Styles.TextBox)
            .InputScope(new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } })
            .MinHeight(40);
        AutomationProperties.SetName(minPriceBox, "Giá tối thiểu");

        var maxPriceBox = new TextBox()
            .Text(x => x.Binding(() => ViewModel.MaxPriceText).Mode(BindingMode.TwoWay))
            .PlaceholderText("Không giới hạn")
            .Style(AppControlStyles.Styles.TextBox)
            .InputScope(new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } })
            .MinHeight(40);
        AutomationProperties.SetName(maxPriceBox, "Giá tối đa");

        var specsBox = new TextBox()
            .Text(x => x.Binding(() => ViewModel.SpecsText).Mode(BindingMode.TwoWay))
            .PlaceholderText("ram=16\nhas_5g=true")
            .Style(AppControlStyles.Styles.TextBox)
            .AcceptsReturn(true)
            .TextWrapping(TextWrapping.Wrap)
            .MinHeight(58);
        AutomationProperties.SetName(specsBox, "Lọc theo thông số kỹ thuật");

        var filterRow2 = new Grid()
            .ColumnSpacing(12)
            .ColumnDefinitions("1*, 1*, 1.5*")
            .Children(
                new StackPanel().Spacing(5).Children(CatalogViewParts.Caption("Giá tối thiểu (₫)"), minPriceBox),
                new StackPanel().Spacing(5).Grid(column: 1).Children(CatalogViewParts.Caption("Giá tối đa (₫)"), maxPriceBox),
                new StackPanel().Spacing(5).Grid(column: 2).Children(CatalogViewParts.Caption("Thông số (mỗi dòng key=value)"), specsBox));

        var sortBox = new ComboBox()
            .SelectedValuePath("Tag")
            .SelectedValue(x => x.Binding(() => ViewModel.SelectedSort).Mode(BindingMode.TwoWay))
            .MinWidth(170)
            .Items(
                new ComboBoxItem { Content = "Mới nhất", Tag = "createdAt:desc" },
                new ComboBoxItem { Content = "Cũ nhất", Tag = "createdAt:asc" },
                new ComboBoxItem { Content = "Tên A–Z", Tag = "name:asc" },
                new ComboBoxItem { Content = "Tên Z–A", Tag = "name:desc" },
                new ComboBoxItem { Content = "Giá thấp trước", Tag = "price:asc" },
                new ComboBoxItem { Content = "Giá cao trước", Tag = "price:desc" });
        AutomationProperties.SetName(sortBox, "Sắp xếp sản phẩm");

        var pageSizeBox = new ComboBox()
            .ItemsSource(ViewModel.PageSizes)
            .SelectedItem(x => x.Binding(() => ViewModel.SelectedPageSize).Mode(BindingMode.TwoWay))
            .MinWidth(72);
        AutomationProperties.SetName(pageSizeBox, "Số sản phẩm mỗi trang");

        var errorSummaryPanel = new StackPanel()
            .Spacing(4)
            .VerticalAlignment(VerticalAlignment.Center)
            .Children(
                new TextBlock()
                    .Text(x => x.Binding(() => ViewModel.CategoryError))
                    .Style(AppControlStyles.Styles.FieldError)
                    .Foreground(AppColors.Brushes.DangerText)
                    .Visibility(x => x.Binding(() => ViewModel.HasCategoryError).Convert((bool v) => CatalogViewParts.ShowIf(v))),
                new TextBlock()
                    .Text(x => x.Binding(() => ViewModel.ValidationMessage))
                    .Style(AppControlStyles.Styles.FieldError)
                    .Foreground(AppColors.Brushes.DangerText)
                    .Visibility(x => x.Binding(() => ViewModel.HasValidationError).Convert((bool v) => CatalogViewParts.ShowIf(v))),
                new TextBlock()
                    .Text(x => x.Binding(() => ViewModel.FieldErrorSummary))
                    .Style(AppControlStyles.Styles.FieldError)
                    .Foreground(AppColors.Brushes.DangerText)
                    .TextWrapping(TextWrapping.Wrap));

        var retryCategoriesButton = new Button()
            .Content("Thử tải danh mục")
            .Command(x => x.Binding(() => ViewModel.RetryCategoriesCommand))
            .Style(AppControlStyles.Styles.Button)
            .Visibility(x => x.Binding(() => ViewModel.HasCategoryError).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var resetFiltersButton = new Button()
            .Content("Đặt lại bộ lọc")
            .Command(x => x.Binding(() => ViewModel.ResetFiltersCommand))
            .Style(AppControlStyles.Styles.Button);

        var filterRow3 = new Grid()
            .ColumnSpacing(12)
            .ColumnDefinitions("Auto, Auto, *, Auto, Auto")
            .Children(
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).VerticalAlignment(VerticalAlignment.Center).Children(
                    CatalogViewParts.Caption("Sắp xếp"), sortBox),
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).VerticalAlignment(VerticalAlignment.Center).Grid(column: 1).Children(
                    CatalogViewParts.Caption("Mỗi trang"), pageSizeBox),
                errorSummaryPanel.Grid(column: 2),
                retryCategoriesButton.Grid(column: 3),
                resetFiltersButton.Grid(column: 4));

        var progressBar = new ProgressBar()
            .IsIndeterminate(true)
            .Height(3)
            .VerticalAlignment(VerticalAlignment.Top)
            .Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var errorPanel = new StackPanel()
            .Spacing(8)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Top)
            .Visibility(x => x.Binding(() => ViewModel.IsError).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Children(
                new Border()
                    .Padding(new Thickness(12))
                    .CornerRadius(AppMetrics.Values.CardRadius)
                    .Background(AppColors.Brushes.DangerFill)
                    .BorderBrush(AppColors.Brushes.DangerStroke)
                    .BorderThickness(new Thickness(1))
                    .Child(
                        new Grid()
                            .ColumnSpacing(12)
                            .ColumnDefinitions("*, Auto")
                            .Children(
                                new TextBlock()
                                    .Text(x => x.Binding(() => ViewModel.ErrorMessage))
                                    .TextWrapping(TextWrapping.Wrap)
                                    .Foreground(AppColors.Brushes.DangerText)
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new Button()
                                    .Grid(column: 1)
                                    .Content("Thử lại")
                                    .Command(x => x.Binding(() => ViewModel.RetryCommand))
                                    .Style(AppControlStyles.Styles.Button))),
                new TextBlock()
                    .Text("Đang hiển thị kết quả thành công trước đó cho bộ lọc cũ.")
                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .Visibility(x => x.Binding(() => ViewModel.HasResultsForPreviousQuery).Convert((bool v) => CatalogViewParts.ShowIf(v))));

        var emptyState = new EmptyStateControl()
            .Title("Không có sản phẩm phù hợp")
            .Description("Thay đổi từ khóa hoặc bộ lọc để tìm sản phẩm khác.")
            .ActionText("Đặt lại bộ lọc")
            .ActionCommand(x => x.Binding(() => ViewModel.ResetFiltersCommand))
            .IconSource(AppIcons.Get(AppIcons.EmptyIcon))
            .Visibility(x => x.Binding(() => ViewModel.ShowEmptyState).Convert((bool v) => CatalogViewParts.ShowIf(v)));

        var initialLoading = new StackPanel()
            .Spacing(10)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .Visibility(x => x.Binding(() => ViewModel.ShowInitialLoading).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Children(
                new ProgressRing().IsActive(true).Width(38).Height(38),
                new TextBlock().Text("Đang tải Catalog").Style(AppResources.GetStyle(AppTypography.BodyText)).Foreground(AppColors.Brushes.Text));

        var outOfRange = new StackPanel()
            .Spacing(10)
            .VerticalAlignment(VerticalAlignment.Center)
            .Visibility(x => x.Binding(() => ViewModel.IsOutOfRange).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Children(
                new TextBlock()
                    .Text(x => x.Binding(() => ViewModel.ResultContext))
                    .Style(AppResources.GetStyle(AppTypography.BodyText))
                    .Foreground(AppColors.Brushes.Text)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .TextAlignment(TextAlignment.Center),
                new Button()
                    .Content("Về trang đầu")
                    .Command(x => x.Binding(() => ViewModel.FirstPageCommand))
                    .Style(AppControlStyles.Styles.PrimaryButton)
                    .HorizontalAlignment(HorizontalAlignment.Center));

        _repeater = new ItemsRepeater()
            .ItemsSource(x => x.Binding(() => ViewModel.Products))
            .Layout(new UniformGridLayout
            {
                MinItemWidth = 258,
                MinItemHeight = 340,
                MinColumnSpacing = 14,
                MinRowSpacing = 14,
                ItemsStretch = UniformGridLayoutItemsStretch.Fill,
                Orientation = Orientation.Horizontal
            })
            .ItemTemplate<ProductSummary>(BuildCard);
        AutomationProperties.SetName(_repeater, "Danh sách sản phẩm");
        _repeater.ElementPrepared += OnProductElementPrepared;

        _scrollViewer = new ScrollViewer()
            .VerticalScrollMode(ScrollMode.Auto)
            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .HorizontalScrollMode(ScrollMode.Disabled)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
            .Content(_repeater);

        var resultsArea = new Grid()
            .Children(
                progressBar,
                errorPanel,
                emptyState,
                initialLoading,
                outOfRange,
                _scrollViewer);

        var paginationRow = new Grid()
            .ColumnSpacing(12)
            .MinHeight(40)
            .ColumnDefinitions("*, Auto, Auto")
            .Visibility(x => x.Binding(() => ViewModel.ShowPagination).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Children(
                new TextBlock()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .Text(x => x.Binding(() => ViewModel.Snapshot).Convert((ProductPage? sp) =>
                        sp is null ? string.Empty : $"Trang {sp.Page} / {sp.TotalPages} · {sp.TotalCount} kết quả")),
                new Button()
                    .Grid(column: 1)
                    .Content("Trang trước")
                    .Command(x => x.Binding(() => ViewModel.PreviousPageCommand))
                    .IsEnabled(x => x.Binding(() => ViewModel.HasPreviousPage))
                    .Style(AppControlStyles.Styles.Button),
                new Button()
                    .Grid(column: 2)
                    .Content("Trang sau")
                    .Command(x => x.Binding(() => ViewModel.NextPageCommand))
                    .IsEnabled(x => x.Binding(() => ViewModel.HasNextPage))
                    .Style(AppControlStyles.Styles.Button));

        Content = new Grid()
            .Padding(new Thickness(20))
            .RowSpacing(14)
            .RowDefinitions("Auto, Auto, Auto, Auto, Auto, *, Auto")
            .Background(AppColors.Brushes.Canvas)
            .Children(
                topRow,
                searchRow.Grid(row: 1),
                filterRow1.Grid(row: 2),
                filterRow2.Grid(row: 3),
                filterRow3.Grid(row: 4),
                resultsArea.Grid(row: 5),
                paginationRow.Grid(row: 6));

        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private Button BuildCard(ProductSummary product)
    {
        var imageGrid = CatalogViewParts.ProductImage(x => x.Binding(() => product.ImageUrl), _apiBase, 34);

        var nameBlock = new TextBlock()
            .Text(x => x.Binding(() => product.Name))
            .Style(AppResources.GetStyle(AppTypography.SectionText))
            .Foreground(AppColors.Brushes.Text)
            .MaxLines(2)
            .TextTrimming(TextTrimming.CharacterEllipsis);
        AutomationProperties.SetName(nameBlock, "Tên sản phẩm");

        var skuBrandBlock = new TextBlock()
            .Text(x => x.Binding(() => product).Convert((ProductSummary p) => p is null ? string.Empty : $"{p.Sku} · {p.Brand}"))
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.SecondaryText)
            .TextWrapping(TextWrapping.Wrap);

        var categoryBlock = new TextBlock()
            .Text(x => x.Binding(() => product.CategoryName))
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.SecondaryText)
            .MaxLines(1)
            .TextTrimming(TextTrimming.CharacterEllipsis);

        var statusBadge = new StatusBadge()
            .Label(x => x.Binding(() => product.IsActive).Convert((bool a) => CatalogPresentation.ActiveLabel(a)))
            .Tone(x => x.Binding(() => product.IsActive).Convert((bool a) => CatalogPresentation.ActiveTone(a)));

        var variantCountBlock = new TextBlock()
            .Text(x => x.Binding(() => product.VariantCount).Convert((int count) => CatalogPresentation.VariantCountLabel(count)))
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.SecondaryText)
            .VerticalAlignment(VerticalAlignment.Center);

        var priceRangeBlock = new TextBlock()
            .Text(x => x.Binding(() => product).Convert((ProductSummary p) => p is null ? string.Empty : CatalogPresentation.PriceRange(p.PriceFrom, p.PriceTo)))
            .Style(AppResources.GetStyle(AppTypography.SectionText))
            .Foreground(AppColors.Brushes.Text);

        var bottomStack = new StackPanel()
            .Spacing(7)
            .Children(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(8)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Children(statusBadge, variantCountBlock),
                priceRangeBlock);

        var cardGrid = new Grid()
            .RowSpacing(9)
            .RowDefinitions("160, Auto, Auto, Auto, *, Auto")
            .Children(
                imageGrid,
                nameBlock.Grid(row: 1),
                skuBrandBlock.Grid(row: 2),
                categoryBlock.Grid(row: 3),
                bottomStack.Grid(row: 5));

        var border = new Border()
            .Padding(new Thickness(12))
            .CornerRadius(AppMetrics.Values.CardRadius)
            .BorderThickness(new Thickness(1))
            .BorderBrush(AppColors.Brushes.Stroke)
            .Background(AppColors.Brushes.Surface)
            .MinHeight(340)
            .Child(cardGrid);

        var button = new Button()
            .Padding(new Thickness(0))
            .BorderThickness(new Thickness(0))
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .VerticalContentAlignment(VerticalAlignment.Stretch)
            .Command(ViewModel.OpenProductCommand)
            .CommandParameter(x => x.Binding(() => product))
            .Content(border);
        button.Background = null;
        AutomationProperties.SetName(button, "Thẻ sản phẩm");
        return button;
    }

    public void SetRoute(ProductQuery? returnCriteria, Guid? focusProductId) =>
        ViewModel.SetRoute(returnCriteria, focusProductId);

    private void OnPageLoaded(object sender, RoutedEventArgs args) =>
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

    private void OnPageUnloaded(object sender, RoutedEventArgs args) =>
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ProductListViewModel.Snapshot))
        {
            DispatcherQueue.TryEnqueue(RestoreFocusAfterLoad);
        }
    }

    private void RestoreFocusAfterLoad()
    {
        if (ViewModel.FocusProductId is not { } focusId) return;
        var product = ViewModel.Products.FirstOrDefault(item => item.Id == focusId);
        if (product is null)
        {
            _listHeading.Focus(FocusState.Programmatic);
            ViewModel.ClearFocusRequest();
            return;
        }

        var index = ViewModel.Products.ToList().FindIndex(item => item.Id == product.Id);
        var itemWidth = 258d;
        var spacing = 14d;
        var columns = Math.Max(1, (int)((_scrollViewer.ViewportWidth + spacing) / (itemWidth + spacing)));
        var targetOffset = index / columns * (340d + spacing);
        _scrollViewer.ChangeView(null, targetOffset, null, disableAnimation: true);
        if (_repeater.TryGetElement(index) is Button item)
        {
            item.Focus(FocusState.Programmatic);
            ViewModel.ClearFocusRequest();
        }
    }

    private void OnProductElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (ViewModel.FocusProductId is not { } focusId
            || args.Element is not Button { DataContext: ProductSummary product }
            || product.Id != focusId)
        {
            return;
        }

        args.Element.Focus(FocusState.Programmatic);
        ViewModel.ClearFocusRequest();
    }
}
