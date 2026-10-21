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
    private const double CardHeight = 300;

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

        var headerRow = new Grid()
            .ColumnSpacing(12)
            .ColumnDefinitions("*, Auto, Auto")
            .Children(
                new StackPanel()
                    .Spacing(4)
                    .Children(
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.HeadingText))
                            .Foreground(AppColors.Brushes.Text)
                            .Text("Danh sách sản phẩm"),
                        CatalogViewParts.Caption("Tìm, lọc và quản lý sản phẩm, giá bán và thông số kỹ thuật.")),
                refreshButton.Grid(column: 1).VerticalAlignment(VerticalAlignment.Center),
                addProductButton.Grid(column: 2).VerticalAlignment(VerticalAlignment.Center));

        var searchBox = new SearchBoxControl()
            .Label("Tìm kiếm sản phẩm")
            .PlaceholderText("Tên, SKU, hãng hoặc barcode")
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
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .MinHeight(40);
        AutomationProperties.SetName(categoryBox, "Lọc theo danh mục");

        var activeStatusBox = new ComboBox()
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .SelectedValuePath("Tag")
            .SelectedValue(x => x.Binding(() => ViewModel.ActiveFilter).Mode(BindingMode.TwoWay))
            .MinHeight(40)
            .Items(
                new ComboBoxItem { Content = "Tất cả trạng thái", Tag = "all" },
                new ComboBoxItem { Content = "Đang hoạt động", Tag = "active" },
                new ComboBoxItem { Content = "Ngừng hoạt động", Tag = "inactive" });
        AutomationProperties.SetName(activeStatusBox, "Lọc trạng thái sản phẩm");

        var categoryField = new StackPanel().Spacing(5).Children(CatalogViewParts.Caption("Danh mục"), categoryBox);
        var statusField = new StackPanel().Spacing(5).Children(CatalogViewParts.Caption("Trạng thái"), activeStatusBox);

        var specRows = new ItemsControl()
            .ItemsSource(x => x.Binding(() => ViewModel.SpecFilters))
            .ItemTemplate<SpecFilterRow>(BuildSpecFilterRow);
        AutomationProperties.SetName(specRows, "Điều kiện lọc theo thông số kỹ thuật");

        var specsField = new StackPanel()
            .Spacing(8)
            .Visibility(x => x.Binding(() => ViewModel.IsSpecFilterOpen).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Children(
                CatalogViewParts.Caption("Thông số kỹ thuật: chọn tên rồi chọn giá trị đang có (số trong ngoặc là số sản phẩm)"),
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Spacing(10)
                    .Visibility(x => x.Binding(() => ViewModel.HasSpecOptionsMessage).Convert((bool v) => CatalogViewParts.ShowIf(v)))
                    .Children(
                        new TextBlock()
                            .Style(AppResources.GetStyle(AppTypography.CaptionText))
                            .Foreground(AppColors.Brushes.SecondaryText)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(x => x.Binding(() => ViewModel.SpecOptionsMessage)),
                        new Button()
                            .Content("Tải lại")
                            .Command(x => x.Binding(() => ViewModel.RetrySpecKeysCommand))
                            .Style(AppControlStyles.Styles.QuietButton)
                            .Visibility(x => x.Binding(() => ViewModel.HasSpecOptionsError).Convert((bool v) => CatalogViewParts.ShowIf(v)))),
                specRows,
                new Button()
                    .Content("+ Thêm điều kiện")
                    .Command(x => x.Binding(() => ViewModel.AddSpecFilterCommand))
                    .Style(AppControlStyles.Styles.QuietButton));

        var specToggleButton = new Button()
            .Content(x => x.Binding(() => ViewModel.IsSpecFilterOpen).Convert((bool open) => open ? "Ẩn lọc thông số" : "Lọc theo thông số"))
            .Command(x => x.Binding(() => ViewModel.ToggleSpecFilterCommand))
            .Style(AppControlStyles.Styles.QuietButton);

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

        var filterRow = new Grid()
            .ColumnSpacing(12)
            .ColumnDefinitions("*, *, Auto, Auto")
            .Children(
                categoryField,
                statusField.Grid(column: 1),
                specToggleButton.Grid(column: 2).VerticalAlignment(VerticalAlignment.Bottom),
                resetFiltersButton.Grid(column: 3).VerticalAlignment(VerticalAlignment.Bottom));

        var errorRow = new Grid()
            .ColumnSpacing(12)
            .ColumnDefinitions("*, Auto")
            .Visibility(x => x.Binding(() => ViewModel.HasFilterMessages).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Children(
                errorSummaryPanel,
                retryCategoriesButton.Grid(column: 1).VerticalAlignment(VerticalAlignment.Center));

        var resultsBar = new Grid()
            .ColumnSpacing(12)
            .MinHeight(36)
            .ColumnDefinitions("*, Auto")
            .Children(
                _listHeading,
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).VerticalAlignment(VerticalAlignment.Center).Grid(column: 1).Children(
                    CatalogViewParts.Caption("Sắp xếp").VerticalAlignment(VerticalAlignment.Center), sortBox));

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
                MinItemHeight = CardHeight,
                MinColumnSpacing = 14,
                MinRowSpacing = 14,
                ItemsStretch = UniformGridLayoutItemsStretch.Fill,
                Orientation = Orientation.Horizontal
            })
            .ItemTemplate<ProductSummary>(BuildCard);
        AutomationProperties.SetName(_repeater, "Danh sách sản phẩm");
        _repeater.ElementPrepared += OnProductElementPrepared;

        var resultsArea = new Grid()
            .MinHeight(220)
            .Children(
                progressBar,
                errorPanel,
                emptyState,
                initialLoading,
                outOfRange,
                _repeater);

        var paginationRow = new Grid()
            .ColumnSpacing(12)
            .MinHeight(40)
            .Margin(new Thickness(20, 10, 20, 14))
            .ColumnDefinitions("*, Auto, Auto, Auto")
            .Visibility(x => x.Binding(() => ViewModel.ShowPagination).Convert((bool v) => CatalogViewParts.ShowIf(v)))
            .Children(
                new TextBlock()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Style(AppResources.GetStyle(AppTypography.CaptionText))
                    .Foreground(AppColors.Brushes.SecondaryText)
                    .Text(x => x.Binding(() => ViewModel.Snapshot).Convert((ProductPage? sp) =>
                        sp is null ? string.Empty : $"Trang {sp.Page} / {sp.TotalPages} · {sp.TotalCount} kết quả")),
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).VerticalAlignment(VerticalAlignment.Center).Grid(column: 1).Children(
                    CatalogViewParts.Caption("Mỗi trang").VerticalAlignment(VerticalAlignment.Center), pageSizeBox),
                new Button()
                    .Grid(column: 2)
                    .Content("Trang trước")
                    .Command(x => x.Binding(() => ViewModel.PreviousPageCommand))
                    .IsEnabled(x => x.Binding(() => ViewModel.HasPreviousPage))
                    .Style(AppControlStyles.Styles.Button),
                new Button()
                    .Grid(column: 3)
                    .Content("Trang sau")
                    .Command(x => x.Binding(() => ViewModel.NextPageCommand))
                    .IsEnabled(x => x.Binding(() => ViewModel.HasNextPage))
                    .Style(AppControlStyles.Styles.Button));

        var filtersPanel = new Border()
            .Style(AppControlStyles.Styles.Panel)
            .Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1))
            .Child(new StackPanel()
                .Spacing(10)
                .Children(
                    searchRow,
                    filterRow,
                    specsField,
                    errorRow));

        // The whole page scrolls so a short window still shows products; only paging stays fixed.
        _scrollViewer = new ScrollViewer()
            .VerticalScrollMode(ScrollMode.Auto)
            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .HorizontalScrollMode(ScrollMode.Disabled)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
            .Content(new StackPanel()
                .Padding(new Thickness(20, 20, 20, 4))
                .Spacing(14)
                .Children(
                    headerRow,
                    filtersPanel,
                    resultsBar,
                    resultsArea));

        Content = new Grid()
            .RowDefinitions("*, Auto")
            .Background(AppColors.Brushes.Canvas)
            .Children(
                _scrollViewer,
                paginationRow.Grid(row: 1));

        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    // `row` is null while the template is built; it is only used inside binding lambdas (AGENTS.md item templates).
    private UIElement BuildSpecFilterRow(SpecFilterRow row)
    {
        var keyBox = new ComboBox()
            .ItemsSource(x => x.Binding(() => row.KeyChoices))
            .DisplayMemberPath("DisplayName")
            .SelectedValuePath("Value")
            .SelectedValue(x => x.Binding(() => row.SelectedKey).Mode(BindingMode.TwoWay))
            .PlaceholderText("Chọn thông số")
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .MinHeight(40);
        AutomationProperties.SetName(keyBox, "Tên thông số");

        var valueBox = new ComboBox()
            .ItemsSource(x => x.Binding(() => row.ValueChoices))
            .DisplayMemberPath("DisplayName")
            .SelectedValuePath("Value")
            .SelectedValue(x => x.Binding(() => row.SelectedFilterValue).Mode(BindingMode.TwoWay))
            .IsEnabled(x => x.Binding(() => row.HasKey))
            .PlaceholderText("Chọn giá trị")
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .MinHeight(40);
        AutomationProperties.SetName(valueBox, "Giá trị thông số");

        return new Grid()
            .ColumnSpacing(10)
            .Margin(new Thickness(0, 0, 0, 6))
            .ColumnDefinitions("*, *, Auto")
            .Children(
                keyBox,
                valueBox.Grid(column: 1),
                new Button()
                    .Grid(column: 2)
                    .Content("Bỏ")
                    .Command(ViewModel.RemoveSpecFilterCommand)
                    .CommandParameter(x => x.Binding(() => row))
                    .Style(AppControlStyles.Styles.QuietButton)
                    .VerticalAlignment(VerticalAlignment.Center));
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
            .RowDefinitions("120, Auto, Auto, Auto, *, Auto")
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
            .MinHeight(CardHeight)
            .Child(cardGrid);

        var button = new Button()
            .Padding(new Thickness(0))
            .BorderThickness(new Thickness(0))
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch)
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
        var repeaterTop = _repeater.TransformToVisual((UIElement)_scrollViewer.Content).TransformPoint(default).Y;
        var targetOffset = repeaterTop + index / columns * (CardHeight + spacing);
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
