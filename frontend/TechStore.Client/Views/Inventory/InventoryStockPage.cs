using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Views.Inventory;
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
        var workspace = new StackPanel { Spacing = 20 };
        workspace.Children.Add(BuildFilters());
        workspace.Children.Add(BuildCard());
        Content = workspace;
        Loaded += (_, _) => ViewModel.LoadCommand.Execute(null);
        Unloaded += (_, _) => ViewModel.CancelLoading();
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

    private UIElement BuildCard()
    {
        var table = new InventoryStockTableRows(ViewModel);
        table.ItemsSource(x => x.Binding(() => ViewModel.VisibleItems));
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

        var states = new Grid();
        var busy = new ProgressRing { Width = 24, Height = 24, Margin = new Thickness(0, 24, 0, 24), IsActive = true, HorizontalAlignment = HorizontalAlignment.Center }
            .Foreground(Theme(AppColors.Primary));
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
        body.Children.Add(states);

        var footerText = Text(color: AppColors.SecondaryText);
        footerText.Text(x => x.Binding(() => ViewModel.FooterText));
        var pager = new InventoryStockPageButtons(ViewModel);
        body.Children.Add(new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16), Child = new ResponsivePair(footerText, pager)
        }.Background(Theme(AppColors.Canvas)).BorderBrush(Theme(AppColors.Stroke)));
        return new Border
        {
            Child = body, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Shadow = new ThemeShadow(), Translation = new System.Numerics.Vector3(0, 0, 2)
        }.Background(Theme(AppColors.Surface)).BorderBrush(Theme(AppColors.Stroke));
    }

    private static Grid BuildTableHeader()
    {
        var grid = InventoryStockTableRows.Columns();
        var labels = new[] { "SẢN PHẨM / SKU", "DANH MỤC", "GIÁ BÁN", "CÓ SẴN (TỒN)", "ĐANG GIỮ", "TRẠNG THÁI", "THAO TÁC" };
        for (var i = 0; i < labels.Length; i++)
        {
            var text = Text(labels[i], 11, AppColors.SecondaryText, 600);
            text.CharacterSpacing = 45;
            text.HorizontalAlignment = i is >= 2 and <= 5 ? HorizontalAlignment.Center : i == 6 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            var cell = new Border { Child = text, Padding = new Thickness(16, 14, 16, 14) };
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }
        grid.Background(Theme(AppColors.Canvas));
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
        var iconKeys = StatusBadgePresentation.GetKeys(row.Icon.Tone);
        identity.Children.Add(new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1), Child = Icon(row.Icon.Symbol, iconKeys.Text)
        }.Background(Theme(iconKeys.Fill)).BorderBrush(Theme(iconKeys.Stroke)));
        var name = new StackPanel { Spacing = 2 };
        name.Children.Add(Text(row.ProductName, 14, AppColors.Text, 600));
        name.Children.Add(Text(row.Sku, 11, AppColors.SecondaryText, 400));
        identity.Children.Add(name);
        AddCell(grid, identity, 0);

        AddCell(grid, Badge(Text(row.CategoryName, 12, AppColors.SecondaryText), StatusTone.Neutral), 1);
        var price = Text(row.PriceText, 13, AppColors.Text, 500);
        price.HorizontalAlignment = HorizontalAlignment.Right;
        AddCell(grid, price, 2);
        var available = Text(row.AvailableText, 13, AppColors.Text, 600);
        available.HorizontalAlignment = HorizontalAlignment.Center;
        AddCell(grid, available, 3);
        var reserved = Text(row.ReservedText, 13, AppColors.SecondaryText, 400);
        reserved.HorizontalAlignment = HorizontalAlignment.Center;
        AddCell(grid, reserved, 4);
        var statusKeys = StatusBadgePresentation.GetKeys(row.StatusTone);
        var status = Badge(Text(row.StatusText, 12, statusKeys.Text, 600), row.StatusTone);
        status.HorizontalAlignment = HorizontalAlignment.Center;
        AddCell(grid, status, 5);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        var serial = Button(Icon("visibility", AppColors.SecondaryText, 17), tooltip: $"Xem Serial/IMEI {row.ProductName}");
        serial.DataContext = viewModel;
        serial.Command(x => x.Binding(() => viewModel.ViewSerialsCommand));
        serial.CommandParameter = row;
        var adjust = Button(Icon("edit", AppColors.SecondaryText, 17), tooltip: $"Điều chỉnh tồn {row.ProductName}");
        adjust.DataContext = viewModel;
        adjust.Command(x => x.Binding(() => viewModel.AdjustStockCommand));
        adjust.CommandParameter = row;
        var delete = Button(Icon("delete_outline", AppColors.DangerText, 17), tooltip: $"Xóa tồn {row.ProductName}");
        delete.DataContext = viewModel;
        delete.Command(x => x.Binding(() => viewModel.DeleteStockCommand));
        delete.CommandParameter = row;
        actions.Children.Add(serial);
        actions.Children.Add(adjust);
        actions.Children.Add(delete);
        AddCell(grid, actions, 6);
        return new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) }.BorderBrush(Theme(AppColors.Stroke));
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
                        .Command(x => x.Binding(() => ViewModel.GoToPageCommand))
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
