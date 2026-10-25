using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Features.Categories.Pages;
using TechStore.Client.Features.Inventory.ViewModels;
using TechStore.Shared.DTOs;
using static TechStore.Client.Features.Categories.Pages.CategoryWorkspaceStyles;

namespace TechStore.Client.Features.Inventory.Pages;

public sealed class SerialImeiPage : Page
{
    public SerialImeiViewModel ViewModel { get; }

    public SerialImeiPage(SerialImeiViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        RequestedTheme = ElementTheme.Light;
        Background = Brush("#00000000");
        FontFamily = Typeface;
        var root = new StackPanel { Spacing = 20 };
        root.Children.Add(BuildFilters());
        root.Children.Add(BuildCard());
        Content = root;
        Loaded += (_, _) => ViewModel.LoadCommand.Execute(null);
        Unloaded += (_, _) => ViewModel.CancelLoading();
    }

    private UIElement BuildFilters()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 280 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 220 });
        var search = new TextBox
        {
            Height = 40, CornerRadius = new CornerRadius(8), FontSize = 14, FontFamily = Typeface,
            Background = Brush("#FFFFFF"), Foreground = Brush(Slate900), BorderBrush = Brush(Slate200),
            BorderThickness = new Thickness(1), Padding = new Thickness(40, 8, 12, 8),
            PlaceholderText = "Tìm theo Serial, IMEI hoặc sản phẩm"
        };
        AutomationProperties.SetName(search, "Tìm theo Serial, IMEI hoặc sản phẩm");
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
        var status = new ComboBox
        {
            Height = 40, CornerRadius = new CornerRadius(8), FontSize = 13, FontFamily = Typeface,
            Background = Brush("#FFFFFF"), Foreground = Brush("#334155"), BorderBrush = Brush(Slate200),
            BorderThickness = new Thickness(1), Padding = new Thickness(12, 6, 32, 6),
            ItemsSource = ViewModel.StatusOptions, DisplayMemberPath = nameof(SerialImeiStatusOption.Label),
            SelectedValuePath = nameof(SerialImeiStatusOption.Value)
        };
        Bind(status, ComboBox.SelectedValueProperty, ViewModel, nameof(ViewModel.StatusFilter), BindingMode.TwoWay);
        Grid.SetColumn(status, 1);
        grid.Children.Add(status);
        return grid;
    }

    private UIElement BuildCard()
    {
        var table = new SerialRows();
        Bind(table, ItemsControl.ItemsSourceProperty, ViewModel, nameof(ViewModel.Items));
        var body = new StackPanel();
        body.Children.Add(new ScrollViewer
        {
            Content = new StackPanel { Children = { BuildHeader(), table } },
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        });
        var states = new Grid { MinHeight = 72, Padding = new Thickness(24) };
        var busy = new ProgressRing { Width = 24, Height = 24, Foreground = Brush(Primary), IsActive = true, HorizontalAlignment = HorizontalAlignment.Center };
        Bind(busy, VisibilityProperty, ViewModel, nameof(ViewModel.IsLoading), converter: new VisibilityConverter());
        states.Children.Add(busy);
        var empty = Text("Chưa có Serial / IMEI phù hợp.", color: Slate500);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        Bind(empty, VisibilityProperty, ViewModel, nameof(ViewModel.IsEmpty), converter: new VisibilityConverter());
        states.Children.Add(empty);
        var error = Text(color: "#B91C1C");
        error.HorizontalAlignment = HorizontalAlignment.Center;
        Bind(error, TextBlock.TextProperty, ViewModel, nameof(ViewModel.ErrorMessage));
        Bind(error, VisibilityProperty, ViewModel, nameof(ViewModel.HasError), converter: new VisibilityConverter());
        states.Children.Add(error);
        body.Children.Add(states);
        var footer = Text(color: Slate500);
        Bind(footer, TextBlock.TextProperty, ViewModel, nameof(ViewModel.FooterText));
        body.Children.Add(new Border { Background = Brush(Slate50), Padding = new Thickness(16), Child = new ResponsivePair(footer, new SerialPageButtons(ViewModel)) });
        return new Border { Child = body, Background = Brush("#FFFFFF"), BorderBrush = Brush(Slate200), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Shadow = new ThemeShadow() };
    }

    private static Grid BuildHeader()
    {
        var grid = SerialRows.Columns();
        foreach (var (label, index) in new[] { ("SERIAL / IMEI", 0), ("SẢN PHẨM", 1), ("SKU", 2), ("TRẠNG THÁI", 3), ("NGÀY NHẬP", 4) })
        {
            var text = Text(label, 11, Slate500, 600);
            text.CharacterSpacing = 45;
            Grid.SetColumn(text, index);
            grid.Children.Add(new Border { Child = text, Padding = new Thickness(16, 14, 16, 14) });
        }
        grid.Background = Brush(Slate50);
        return grid;
    }
}

internal sealed class SerialRows : ItemsControl
{
    internal static Grid Columns()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star), MinWidth = 210 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star), MinWidth = 210 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 160 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 150 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 150 });
        return grid;
    }

    protected override DependencyObject GetContainerForItemOverride() => new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0) };
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ContentControl container && item is SerialImeiDto row) container.Content = BuildRow(row);
    }
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ContentControl container) container.Content = null;
        base.ClearContainerForItemOverride(element, item);
    }
    private static UIElement BuildRow(SerialImeiDto row)
    {
        var grid = Columns();
        Add(grid, Text(row.SerialNumber ?? row.Imei ?? "—", 13, Slate900, 600), 0);
        Add(grid, Text(row.ProductName, 13, Slate900, 500), 1);
        Add(grid, Text(row.VariantName ?? "—", 12, Slate500), 2);
        Add(grid, Badge(Text(SerialImeiViewModel.StatusText(row.Status), 12, Slate500, 600), "#F1F5F9"), 3);
        Add(grid, Text(row.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), 12, Slate500), 4);
        return new Border { Child = grid, BorderBrush = Brush("#F1F5F9"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }
    private static void Add(Grid grid, UIElement content, int column)
    {
        var cell = new Border { Child = content, Padding = new Thickness(16, 13, 16, 13) };
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }
}

internal sealed class SerialPageButtons : StackPanel
{
    private readonly SerialImeiViewModel _viewModel;
    public SerialPageButtons(SerialImeiViewModel viewModel)
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
        var previous = Button(Icon("left", size: 18), tooltip: "Trang trước");
        previous.Command = _viewModel.PreviousPageCommand;
        Bind(previous, Control.IsEnabledProperty, _viewModel, nameof(_viewModel.CanGoPrevious));
        Children.Add(previous);
        foreach (var page in _viewModel.PageNumbers)
        {
            var button = Button(Text(page.ToString(), 12, Slate500, 600), tooltip: $"Trang {page}");
            button.Command = _viewModel.GoToPageCommand;
            button.CommandParameter = page;
            Children.Add(button);
        }
        var next = Button(Icon("right", size: 18), tooltip: "Trang sau");
        next.Command = _viewModel.NextPageCommand;
        Bind(next, Control.IsEnabledProperty, _viewModel, nameof(_viewModel.CanGoNext));
        Children.Add(next);
    }
}
