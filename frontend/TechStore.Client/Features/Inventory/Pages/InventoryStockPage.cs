using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Features.Categories.Pages;
using TechStore.Client.Features.Inventory.Models;
using TechStore.Client.Features.Inventory.ViewModels;
using static TechStore.Client.Features.Categories.Pages.CategoryWorkspaceStyles;

namespace TechStore.Client.Features.Inventory.Pages;

/// <summary>Inventory stock workspace implemented entirely with C# Markup.</summary>
public sealed class InventoryStockPage : Page
{
    public InventoryStockViewModel ViewModel { get; }

    public InventoryStockPage(InventoryStockViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        RequestedTheme = ElementTheme.Light;
        Background = Brush("#00000000");
        FontFamily = Typeface;
        var workspace = new StackPanel { Spacing = 20 };
        workspace.Children.Add(BuildFilters());
        workspace.Children.Add(BuildCard());
        Content = workspace;
        Loaded += (_, _) => ViewModel.LoadCommand.Execute(null);
        Unloaded += (_, _) => ViewModel.CancelLoading();
    }

    private UIElement BuildHeader()
    {
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        title.Children.Add(Text("Danh mục và Kho", 20, Slate900, 700));
        var total = Text(size: 14, color: Slate500);
        Bind(total, TextBlock.TextProperty, ViewModel, nameof(ViewModel.TotalLabel));
        title.Children.Add(total);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var export = Button(Text("Xuất báo cáo", color: Slate500), tooltip: "Xuất báo cáo tồn kho");
        export.Command = ViewModel.ExportReportCommand;
        actions.Children.Add(export);
        var importContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        importContent.Children.Add(Icon("add", "#FFFFFF", 18));
        importContent.Children.Add(Text("Nhập kho mới", 14, "#FFFFFF", 600));
        var import = Button(importContent, primary: true, tooltip: "Nhập kho mới");
        import.Command = ViewModel.ImportStockCommand;
        actions.Children.Add(import);
        return new ResponsivePair(title, actions);
    }

    private UIElement BuildTabs()
    {
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        AddTab(tabs, "Danh mục", Text("—", 12, Slate500, 600), false);
        var stockCount = Text("0", 12, Primary, 600);
        Bind(stockCount, TextBlock.TextProperty, ViewModel, nameof(ViewModel.TotalCount));
        AddTab(tabs, "Tồn kho", stockCount, true);
        AddTab(tabs, "Serial / IMEI", Text("—", 12, Slate500, 600), false);
        return new Border
        {
            Child = tabs,
            BorderBrush = Brush(Slate200),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
    }

    private static void AddTab(Panel parent, string label, UIElement count, bool active)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(Text(label, 14, active ? Primary : Slate500, active ? (ushort)600 : (ushort)500));
        content.Children.Add(Badge(count, active ? "#EFF6FF" : "#F1F5F9"));
        parent.Children.Add(new Border
        {
            Child = content,
            Padding = new Thickness(4, 12, 4, 12),
            BorderThickness = new Thickness(0, 0, 0, 2),
            BorderBrush = Brush(active ? Primary : "#00000000")
        });
    }

    private UIElement BuildFilters()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 280 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 170 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 170 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 170 });

        var search = new TextBox
        {
            Height = 40, CornerRadius = new CornerRadius(8), FontSize = 14, FontFamily = Typeface,
            Background = Brush("#FFFFFF"), Foreground = Brush(Slate900), BorderBrush = Brush(Slate200),
            BorderThickness = new Thickness(1), Padding = new Thickness(40, 8, 12, 8),
            PlaceholderText = "Tìm theo tên sản phẩm hoặc mã SKU..."
        };
        AutomationProperties.SetName(search, "Tìm theo tên sản phẩm hoặc mã SKU");
        Bind(search, TextBox.TextProperty, ViewModel, nameof(ViewModel.SearchText), BindingMode.TwoWay);
        var searchSlot = new Grid();
        searchSlot.Children.Add(search);
        var searchIcon = Icon("search", "#94A3B8", 18);
        searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
        searchIcon.Margin = new Thickness(12, 0, 0, 0);
        searchIcon.IsHitTestVisible = false;
        searchSlot.Children.Add(searchIcon);
        Grid.SetColumn(searchSlot, 0);
        grid.Children.Add(searchSlot);

        var category = CreateCombo(ViewModel.CategoryOptions, ViewModel, "Danh mục", nameof(ViewModel.SelectedCategory));
        Bind(category, ComboBox.IsEnabledProperty, ViewModel, nameof(ViewModel.IsCategoryFilterAvailable));
        Grid.SetColumn(category, 1);
        grid.Children.Add(category);

        var status = CreateCombo(ViewModel.StatusOptions, ViewModel, "Trạng thái tồn", nameof(ViewModel.StockStatusFilter), nameof(InventoryStockStatusOption.Label), nameof(InventoryStockStatusOption.Value));
        Grid.SetColumn(status, 2);
        grid.Children.Add(status);

        var warehouse = CreateCombo(ViewModel.WarehouseOptions, ViewModel, "Kho", nameof(ViewModel.SelectedWarehouse));
        Bind(warehouse, ComboBox.IsEnabledProperty, ViewModel, nameof(ViewModel.IsWarehouseFilterAvailable));
        Grid.SetColumn(warehouse, 3);
        grid.Children.Add(warehouse);
        return grid;
    }

    private static ComboBox CreateCombo<T>(IReadOnlyList<T> items, object source, string name, string selectedPath, string? displayPath = null, string? valuePath = null)
    {
        var combo = new ComboBox
        {
            Height = 40, MinWidth = 160, CornerRadius = new CornerRadius(8), FontSize = 13, FontFamily = Typeface,
            Background = Brush("#FFFFFF"), Foreground = Brush("#334155"), BorderBrush = Brush(Slate200),
            BorderThickness = new Thickness(1), Padding = new Thickness(12, 6, 32, 6), ItemsSource = items,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        if (displayPath is not null) combo.DisplayMemberPath = displayPath;
        if (valuePath is not null) combo.SelectedValuePath = valuePath;
        AutomationProperties.SetName(combo, name);
        Bind(combo, ComboBox.SelectedValueProperty, source, selectedPath, BindingMode.TwoWay);
        return combo;
    }

    private UIElement BuildCard()
    {
        var table = new InventoryStockTableRows(ViewModel);
        Bind(table, ItemsControl.ItemsSourceProperty, ViewModel, nameof(ViewModel.VisibleItems));
        var body = new StackPanel();
        var tableScroll = new ScrollViewer
        {
            Content = new StackPanel { Children = { BuildTableHeader(), table } },
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        body.Children.Add(tableScroll);

        var states = new Grid { MinHeight = 72, Padding = new Thickness(24) };
        var busy = new ProgressRing { Width = 24, Height = 24, Foreground = Brush(Primary), IsActive = true, HorizontalAlignment = HorizontalAlignment.Center };
        Bind(busy, VisibilityProperty, ViewModel, nameof(ViewModel.IsLoading), converter: new VisibilityConverter());
        states.Children.Add(busy);
        var empty = Text(color: Slate500);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        Bind(empty, TextBlock.TextProperty, ViewModel, nameof(ViewModel.EmptyText));
        Bind(empty, VisibilityProperty, ViewModel, nameof(ViewModel.IsEmpty), converter: new VisibilityConverter());
        states.Children.Add(empty);
        var errorPanel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        var error = Text(color: "#B91C1C");
        error.HorizontalAlignment = HorizontalAlignment.Center;
        Bind(error, TextBlock.TextProperty, ViewModel, nameof(ViewModel.ErrorMessage));
        errorPanel.Children.Add(error);
        var retry = Button(Text("Thử lại", color: Primary), tooltip: "Tải lại tồn kho");
        retry.Command = ViewModel.LoadCommand;
        errorPanel.Children.Add(retry);
        Bind(errorPanel, VisibilityProperty, ViewModel, nameof(ViewModel.HasError), converter: new VisibilityConverter());
        states.Children.Add(errorPanel);
        body.Children.Add(states);

        var footerText = Text(color: Slate500);
        Bind(footerText, TextBlock.TextProperty, ViewModel, nameof(ViewModel.FooterText));
        var pager = new InventoryStockPageButtons(ViewModel);
        body.Children.Add(new Border
        {
            Background = Brush(Slate50), BorderBrush = Brush(Slate200), BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16), Child = new ResponsivePair(footerText, pager)
        });
        return new Border
        {
            Child = body, Background = Brush("#FFFFFF"), BorderBrush = Brush(Slate200), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Shadow = new ThemeShadow(), Translation = new System.Numerics.Vector3(0, 0, 2)
        };
    }

    private static Grid BuildTableHeader()
    {
        var grid = InventoryStockTableRows.Columns();
        var labels = new[] { "SẢN PHẨM / SKU", "DANH MỤC", "GIÁ BÁN", "CÓ SẴN (TỒN)", "ĐANG GIỮ", "TRẠNG THÁI", "THAO TÁC" };
        for (var i = 0; i < labels.Length; i++)
        {
            var text = Text(labels[i], 11, Slate500, 600);
            text.CharacterSpacing = 45;
            text.HorizontalAlignment = i is >= 2 and <= 5 ? HorizontalAlignment.Center : i == 6 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            var cell = new Border { Child = text, Padding = new Thickness(16, 14, 16, 14) };
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }
        grid.Background = Brush(Slate50);
        return grid;
    }
}

internal sealed class InventoryStockTableRows(InventoryStockViewModel viewModel) : ItemsControl
{
    internal static Grid Columns()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 280 });
        foreach (var width in new[] { 160, 120, 130, 110, 130, 130 })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width), MinWidth = width });
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
        grid.Opacity = row.IsDimmed ? 0.75 : 1;
        var identity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        identity.Children.Add(new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(8), Background = Brush(row.Icon.Background),
            BorderBrush = Brush(row.Icon.Border), BorderThickness = new Thickness(1), Child = Icon(row.Icon.Symbol, row.Icon.Foreground)
        });
        var name = new StackPanel { Spacing = 2 };
        name.Children.Add(Text(row.ProductName, 14, Slate900, 600));
        name.Children.Add(Text(row.Sku, 11, Slate500, 400));
        identity.Children.Add(name);
        AddCell(grid, identity, 0);

        AddCell(grid, Badge(Text(row.CategoryName, 12, Slate500), "#F1F5F9"), 1);
        var price = Text(row.PriceText, 13, Slate900, 500);
        price.HorizontalAlignment = HorizontalAlignment.Right;
        AddCell(grid, price, 2);
        var available = Text(row.AvailableText, 13, Slate900, 600);
        available.HorizontalAlignment = HorizontalAlignment.Center;
        AddCell(grid, available, 3);
        var reserved = Text(row.ReservedText, 13, Slate500, 400);
        reserved.HorizontalAlignment = HorizontalAlignment.Center;
        AddCell(grid, reserved, 4);
        var status = Badge(Text(row.StatusText, 12, row.StatusForeground, 600), row.StatusBackground);
        status.HorizontalAlignment = HorizontalAlignment.Center;
        AddCell(grid, status, 5);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        var serial = Button(Icon("visibility", Slate500, 17), tooltip: $"Xem Serial/IMEI {row.ProductName}");
        serial.Command = viewModel.ViewSerialsCommand;
        serial.CommandParameter = row;
        var adjust = Button(Icon("edit", Slate500, 17), tooltip: $"Điều chỉnh tồn {row.ProductName}");
        adjust.Command = viewModel.AdjustStockCommand;
        adjust.CommandParameter = row;
        var delete = Button(Icon("delete_outline", "#DC2626", 17), tooltip: $"Xóa tồn {row.ProductName}");
        delete.Command = viewModel.DeleteStockCommand;
        delete.CommandParameter = row;
        actions.Children.Add(serial);
        actions.Children.Add(adjust);
        actions.Children.Add(delete);
        AddCell(grid, actions, 6);
        return new Border { Child = grid, BorderBrush = Brush("#F1F5F9"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    private static void AddCell(Grid grid, UIElement content, int column)
    {
        var cell = new Border { Child = content, Padding = new Thickness(16, 13, 16, 13) };
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }
}

internal sealed class InventoryStockPageButtons : StackPanel
{
    private readonly InventoryStockViewModel _viewModel;

    internal InventoryStockPageButtons(InventoryStockViewModel viewModel)
    {
        _viewModel = viewModel;
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        var previous = Button(Icon("left", size: 18), tooltip: "Trang trước");
        previous.Command = _viewModel.PreviousPageCommand;
        previous.Height = 32;
        Bind(previous, Control.IsEnabledProperty, _viewModel, nameof(_viewModel.CanGoPrevious));
        Children.Add(previous);
        Children.Add(new InventoryStockPageNumberItems(_viewModel));
        var next = Button(Icon("right", size: 18), tooltip: "Trang sau");
        next.Command = _viewModel.NextPageCommand;
        next.Height = 32;
        Bind(next, Control.IsEnabledProperty, _viewModel, nameof(_viewModel.CanGoNext));
        Children.Add(next);
    }
}

internal sealed class InventoryStockPageNumberItems : StackPanel
{
    private readonly InventoryStockViewModel _viewModel;

    public InventoryStockPageNumberItems(InventoryStockViewModel viewModel)
    {
        _viewModel = viewModel;
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        _viewModel.PageNumbers.CollectionChanged += OnPageNumbersChanged;
        Unloaded += (_, _) => _viewModel.PageNumbers.CollectionChanged -= OnPageNumbersChanged;
        Render();
    }

    private void OnPageNumbersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args) => Render();

    private void Render()
    {
        Children.Clear();
        foreach (var page in _viewModel.PageNumbers)
        {
            var active = page == _viewModel.Page;
            var button = Button(Text(page.ToString(), 12, active ? "#FFFFFF" : "#334155", 600), active, $"Trang {page}");
            button.Width = 32;
            button.Height = 32;
            button.Padding = new Thickness(0);
            button.Command = _viewModel.GoToPageCommand;
            button.CommandParameter = page;
            if (!active)
            {
                button.Background = Brush("#FFFFFF");
                button.BorderBrush = Brush(Slate200);
                button.BorderThickness = new Thickness(1);
            }
            Children.Add(button);
        }
    }
}
