using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory;

/// <summary>Inventory stock workspace implemented entirely with C# Markup.</summary>
public sealed class InventoryStockPage : Page
{
    public InventoryStockViewModel ViewModel { get; }

    public InventoryStockPage(InventoryStockViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        var workspace = new Grid { RowSpacing = 20 };
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var filters = BuildFilters();
        Grid.SetRow(filters, 0);
        workspace.Children.Add(filters);
        var card = BuildCard();
        Grid.SetRow(card, 1);
        workspace.Children.Add(card);
        Content = workspace;
        Loaded += (_, _) =>
        {
            if (!ViewModel.HasLoaded && !ViewModel.IsLoading)
                ViewModel.LoadCommand.Execute(null);
        };
        Unloaded += (_, _) => ViewModel.CancelLoading();
    }

    private FrameworkElement BuildFilters()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 280 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 170 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 170 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 170 });

        var search = new TextBox
        {
            Height = 40, CornerRadius = new CornerRadius(8), FontSize = 14,
            BorderThickness = new Thickness(1), Padding = new Thickness(40, 8, 12, 8),
            PlaceholderText = "Tìm theo tên sản phẩm hoặc mã SKU..."
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        AutomationProperties.SetName(search, "Tìm theo tên sản phẩm hoặc mã SKU");
        search.Text(x => x.Binding(() => ViewModel.SearchText).Mode(BindingMode.TwoWay));
        var searchSlot = new Grid();
        searchSlot.Children.Add(search);
        var searchIcon = Icon("search", AppColors.SecondaryText, 18);
        searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
        searchIcon.Margin = new Thickness(12, 0, 0, 0);
        searchIcon.IsHitTestVisible = false;
        searchSlot.Children.Add(searchIcon);
        Grid.SetColumn(searchSlot, 0);
        grid.Children.Add(searchSlot);

        var category = CreateCombo(ViewModel.CategoryOptions, "Danh mục", nameof(CategoryOption.Label), nameof(CategoryOption.Id));
        category.PlaceholderText = "Danh mục: Tất cả";
        category.ItemsSource(x => x.Binding(() => ViewModel.CategoryOptions));
        category.SelectedValue(x => x.Binding(() => ViewModel.SelectedCategoryId).Mode(BindingMode.TwoWay));
        category.IsEnabled(x => x.Binding(() => ViewModel.IsCategoryFilterAvailable));
        Grid.SetColumn(category, 1);
        grid.Children.Add(category);

        var status = CreateCombo(ViewModel.StatusOptions, "Trạng thái tồn", nameof(InventoryStockStatusOption.Label), nameof(InventoryStockStatusOption.Value));
        status.SelectedValue(x => x.Binding(() => ViewModel.StockStatusFilter).Mode(BindingMode.TwoWay));
        Grid.SetColumn(status, 2);
        grid.Children.Add(status);

        var warehouse = CreateCombo(ViewModel.WarehouseOptions, "Kho");
        warehouse.SelectedValue(x => x.Binding(() => ViewModel.SelectedWarehouse).Mode(BindingMode.TwoWay));
        warehouse.IsEnabled(x => x.Binding(() => ViewModel.IsWarehouseFilterAvailable));
        Grid.SetColumn(warehouse, 3);
        grid.Children.Add(warehouse);
        return grid;
    }

    private static ComboBox CreateCombo<T>(IReadOnlyList<T> items, string name, string? displayPath = null, string? valuePath = null)
    {
        var combo = new ComboBox
        {
            Height = 40, MinWidth = 160, CornerRadius = new CornerRadius(8), FontSize = 13,
            BorderThickness = new Thickness(1), Padding = new Thickness(12, 6, 32, 6), ItemsSource = items,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.SecondaryText))
        .BorderBrush(Theme(AppColors.Stroke));
        if (displayPath is not null) combo.DisplayMemberPath = displayPath;
        if (valuePath is not null) combo.SelectedValuePath = valuePath;
        AutomationProperties.SetName(combo, name);
        return combo;
    }

    private FrameworkElement BuildCard()
    {
        var table = new InventoryStockTableRows(ViewModel);
        table.ItemsSource(x => x.Binding(() => ViewModel.VisibleItems));
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = BuildTableHeader();
        Grid.SetRow(header, 0);
        body.Children.Add(header);

        var rowsScroll = new ScrollViewer
        {
            Content = table,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(rowsScroll, 1);
        body.Children.Add(rowsScroll);

        var states = new Grid();
        var busy = new ProgressRing { Width = 24, Height = 24, Margin = new Thickness(0, 24, 0, 24), IsActive = false, HorizontalAlignment = HorizontalAlignment.Center }
            .Foreground(Theme(AppColors.Primary));
        busy.IsActive(x => x.Binding(() => ViewModel.IsLoading));
        busy.Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert(ShowIf));
        states.Children.Add(busy);
        var empty = Text(color: AppColors.SecondaryText);
        empty.Margin = new Thickness(0, 24, 0, 24);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Text(x => x.Binding(() => ViewModel.EmptyText));
        empty.Visibility(x => x.Binding(() => ViewModel.IsEmpty).Convert(ShowIf));
        states.Children.Add(empty);
        var errorPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 24, 0, 24), HorizontalAlignment = HorizontalAlignment.Center };
        var error = Text(color: AppColors.DangerText);
        error.HorizontalAlignment = HorizontalAlignment.Center;
        error.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        errorPanel.Children.Add(error);
        var retry = Button(Text("Thử lại", color: AppColors.InfoText), tooltip: "Tải lại tồn kho");
        retry.Command(x => x.Binding(() => ViewModel.LoadCommand));
        errorPanel.Children.Add(retry);
        errorPanel.Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf));
        states.Children.Add(errorPanel);
        Grid.SetRow(states, 2);
        body.Children.Add(states);

        var footerText = Text(color: AppColors.SecondaryText);
        footerText.Text(x => x.Binding(() => ViewModel.FooterText));
        var pager = new InventoryStockPageButtons(ViewModel);
        var footer = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16), Child = new ResponsivePair(footerText, pager)
        }.Background(Theme(AppColors.Canvas)).BorderBrush(Theme(AppColors.Stroke));
        Grid.SetRow(footer, 3);
        body.Children.Add(footer);

        return new Border
        {
            Child = body, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12)
        }.Background(Theme(AppColors.Surface)).BorderBrush(Theme(AppColors.Stroke));
    }

    private static Border BuildTableHeader()
    {
        var grid = InventoryStockTableRows.Columns();
        var labels = new[] { "SẢN PHẨM / SKU", "DANH MỤC", "GIÁ BÁN", "CÓ SẴN (TỒN)", "ĐANG GIỮ", "TRẠNG THÁI", "THAO TÁC" };
        for (var i = 0; i < labels.Length; i++)
        {
            var text = Text(labels[i], 11, AppColors.SecondaryText, 600);
            text.CharacterSpacing = 45;
            text.HorizontalAlignment = i == 2 ? HorizontalAlignment.Right : i is >= 3 and <= 5 ? HorizontalAlignment.Center : i == 6 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            Grid.SetColumn(text, i);
            grid.Children.Add(text);
        }
        return new Border
        {
            Child = grid,
            BorderThickness = new Thickness(0, 0, 0, 1)
        }.Background(Theme(AppColors.Canvas)).BorderBrush(Theme(AppColors.Stroke));
    }
}

internal sealed class InventoryStockTableRows(InventoryStockViewModel viewModel) : ItemsControl
{
    internal static Grid Columns()
    {
        var grid = new Grid
        {
            Padding = new Thickness(16, 12, 16, 12),
            ColumnSpacing = 12
        };
        foreach (var star in new[] { 2.4, 1.2, 1.1, 1.0, 0.8, 1.1, 1.0 })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(star, GridUnitType.Star) });
        return grid;
    }

    protected override DependencyObject GetContainerForItemOverride() => new ContentControl
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch, IsTabStop = false, Padding = new Thickness(0)
    };

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ContentControl container && item is InventoryStockRow row) container.Content = BuildRow(row);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ContentControl container) container.Content = null;
        base.ClearContainerForItemOverride(element, item);
    }

    private UIElement BuildRow(InventoryStockRow row)
    {
        var grid = Columns();
        var primaryTextColor = row.IsDimmed ? AppColors.SecondaryText : AppColors.Text;
        var name = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        name.Children.Add(Text(row.ProductName, 14, primaryTextColor, 600));
        name.Children.Add(Text(row.Sku, 11, AppColors.SecondaryText, 400));
        Grid.SetColumn(name, 0);
        grid.Children.Add(name);

        var categoryBadge = Text(row.CategoryName, 13, AppColors.SecondaryText);
        Grid.SetColumn(categoryBadge, 1);
        grid.Children.Add(categoryBadge);

        var price = Text(row.PriceText, 13, primaryTextColor, 500);
        price.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(price, 2);
        grid.Children.Add(price);

        var available = Text(row.AvailableText, 13, primaryTextColor, 600);
        available.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(available, 3);
        grid.Children.Add(available);

        var reserved = Text(row.ReservedText, 13, AppColors.SecondaryText, 400);
        reserved.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(reserved, 4);
        grid.Children.Add(reserved);

        var statusKeys = StatusBadgePresentation.GetKeys(row.StatusTone);
        var status = Badge(Text(row.StatusText, 12, statusKeys.Text, 600), row.StatusTone);
        status.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(status, 5);
        grid.Children.Add(status);

        var detail = RowAction("Xem chi tiết", row, viewModel);
        detail.Command(x => x.Binding(() => viewModel.ViewSerialsCommand));
        var adjust = RowAction("Điều chỉnh tồn", row, viewModel);
        adjust.Command(x => x.Binding(() => viewModel.AdjustStockCommand));
        var delete = RowAction("Xóa tồn", row, viewModel, danger: true);
        delete.Command(x => x.Binding(() => viewModel.DeleteStockCommand));
        var actions = RowActionsButton($"Thao tác cho {row.ProductName}", detail, adjust, delete);
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(actions, 6);
        grid.Children.Add(actions);

        return new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) }.BorderBrush(Theme(AppColors.Stroke));
    }
}

internal sealed class InventoryStockPageButtons : StackPanel
{
    public InventoryStockViewModel ViewModel { get; }

    internal InventoryStockPageButtons(InventoryStockViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        var previous = Button(Icon("left", size: 18), tooltip: "Trang trước");
        previous.Command(x => x.Binding(() => ViewModel.PreviousPageCommand));
        previous.Height = 32;
        previous.IsEnabled(x => x.Binding(() => ViewModel.CanGoPrevious));
        Children.Add(previous);
        var items = new ItemsControl()
            .ItemsSource(x => x.Binding(() => ViewModel.PageButtons))
            .ItemsPanel<StackPanel>(panel => panel.Orientation(Orientation.Horizontal).Spacing(6))
            .ItemTemplate<PageButtonItem>(item =>
                new Grid().Children(
                    new Button().Style(AppControlStyles.Styles.PrimaryButton)
                        .Content(x => x.Binding(() => item.Number))
                        .Visibility(x => x.Binding(() => item.IsCurrent).Convert(ShowIf)),
                    new Button().Style(AppControlStyles.Styles.Button)
                        .Content(x => x.Binding(() => item.Number))
                        .Command(ViewModel.GoToPageCommand)
                        .CommandParameter(x => x.Binding(() => item.Number))
                        .Visibility(x => x.Binding(() => item.IsCurrent).Convert((bool current) => ShowIf(!current)))));
        Children.Add(items);
        var next = Button(Icon("right", size: 18), tooltip: "Trang sau");
        next.Command(x => x.Binding(() => ViewModel.NextPageCommand));
        next.Height = 32;
        next.IsEnabled(x => x.Binding(() => ViewModel.CanGoNext));
        Children.Add(next);
    }
}
