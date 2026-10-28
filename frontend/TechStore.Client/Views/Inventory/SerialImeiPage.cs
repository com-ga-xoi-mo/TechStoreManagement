using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Models.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Styles;
using TechStore.Shared.DTOs;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory;

public sealed class SerialImeiPage : Page
{
    public SerialImeiViewModel ViewModel { get; }

    public SerialImeiPage(SerialImeiViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        var root = new Grid { RowSpacing = 16 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var filters = BuildFilters();
        Grid.SetRow(filters, 0);
        root.Children.Add(filters);
        var banner = BuildProductFilterBanner();
        Grid.SetRow(banner, 1);
        root.Children.Add(banner);
        var card = BuildCard();
        Grid.SetRow(card, 2);
        root.Children.Add(card);
        Content = root;
        Loaded += (_, _) =>
        {
            if (!ViewModel.HasLoaded && !ViewModel.IsLoading)
                ViewModel.LoadCommand.Execute(null);
        };
        Unloaded += (_, _) => ViewModel.CancelLoading();
    }

    private FrameworkElement BuildProductFilterBanner()
    {
        var label = Text("Đang xem serial của:", 13, AppColors.InfoText, 500);
        var product = Text("", 13, AppColors.InfoText, 600);
        product.Text(x => x.Binding(() => ViewModel.ProductFilterLabel));
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Icon("qr_code", AppColors.InfoText, 16), label, product }
        };

        var clearContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Icon("close", AppColors.InfoText, 14), Text("Bỏ lọc", 12, AppColors.InfoText, 600) }
        };
        var clear = Button(clearContent, tooltip: "Bỏ lọc theo sản phẩm");
        clear.Command(x => x.Binding(() => ViewModel.ClearProductFilterCommand));

        return new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Child = new ResponsivePair(left, clear)
        }
        .Background(Theme(AppColors.InfoFill))
        .BorderBrush(Theme(AppColors.InfoStroke))
        .Visibility(x => x.Binding(() => ViewModel.HasProductFilter).Convert(ShowIf));
    }

    private FrameworkElement BuildFilters()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 280 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 220 });
        var search = new TextBox
        {
            Height = 40, CornerRadius = new CornerRadius(8), FontSize = 14,
            BorderThickness = new Thickness(1), Padding = new Thickness(40, 8, 12, 8),
            PlaceholderText = "Tìm theo Serial, IMEI hoặc sản phẩm"
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        AutomationProperties.SetName(search, "Tìm theo Serial, IMEI hoặc sản phẩm");
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
        var status = new ComboBox
        {
            Height = 40, CornerRadius = new CornerRadius(8), FontSize = 13,
            BorderThickness = new Thickness(1), Padding = new Thickness(12, 6, 32, 6),
            ItemsSource = ViewModel.StatusOptions, DisplayMemberPath = nameof(SerialImeiStatusOption.Label),
            SelectedValuePath = nameof(SerialImeiStatusOption.Value)
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.SecondaryText))
        .BorderBrush(Theme(AppColors.Stroke));
        status.SelectedValue(x => x.Binding(() => ViewModel.StatusFilter).Mode(BindingMode.TwoWay));
        Grid.SetColumn(status, 1);
        grid.Children.Add(status);
        return grid;
    }

    private FrameworkElement BuildCard()
    {
        var table = new SerialRows();
        table.ItemsSource(x => x.Binding(() => ViewModel.Items));
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = BuildHeader();
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
        var empty = Text("Chưa có Serial / IMEI phù hợp.", color: AppColors.SecondaryText);
        empty.Margin = new Thickness(0, 24, 0, 24);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Visibility(x => x.Binding(() => ViewModel.IsEmpty).Convert(ShowIf));
        states.Children.Add(empty);
        var error = Text(color: AppColors.DangerText);
        error.Margin = new Thickness(0, 24, 0, 24);
        error.HorizontalAlignment = HorizontalAlignment.Center;
        error.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        error.Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf));
        states.Children.Add(error);
        Grid.SetRow(states, 2);
        body.Children.Add(states);
        var footerText = Text(color: AppColors.SecondaryText);
        footerText.Text(x => x.Binding(() => ViewModel.FooterText));
        var footer = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16), Child = new ResponsivePair(footerText, new SerialPageButtons(ViewModel))
        }.Background(Theme(AppColors.Canvas)).BorderBrush(Theme(AppColors.Stroke));
        Grid.SetRow(footer, 3);
        body.Children.Add(footer);
        return new Border
        {
            Child = body, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12)
        }.Background(Theme(AppColors.Surface)).BorderBrush(Theme(AppColors.Stroke));
    }

    private static Border BuildHeader()
    {
        var grid = SerialRows.Columns();
        foreach (var (label, index) in new[] { ("SERIAL / IMEI", 0), ("SẢN PHẨM", 1), ("SKU", 2), ("TRẠNG THÁI", 3), ("NGÀY NHẬP", 4) })
        {
            var text = Text(label, 11, AppColors.SecondaryText, 600);
            text.CharacterSpacing = 45;
            var cell = new Border { Child = text, Padding = new Thickness(16, 14, 16, 14) };
            Grid.SetColumn(cell, index);
            grid.Children.Add(cell);
        }
        return new Border
        {
            Child = grid,
            BorderThickness = new Thickness(0, 0, 0, 1)
        }.Background(Theme(AppColors.Canvas)).BorderBrush(Theme(AppColors.Stroke));
    }
}

internal sealed class SerialRows : ItemsControl
{
    internal static Grid Columns()
    {
        var grid = new Grid();
        foreach (var star in new[] { 1.4, 1.4, 1.0, 1.0, 1.0 })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(star, GridUnitType.Star) });
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
        Add(grid, Text(row.SerialNumber ?? row.Imei ?? "—", 13, AppColors.Text, 600), 0);
        Add(grid, Text(row.ProductName, 13, AppColors.Text, 500), 1);
        Add(grid, Text(row.VariantName ?? "—", 12, AppColors.SecondaryText), 2);
        Add(grid, Badge(Text(SerialImeiViewModel.StatusText(row.Status), 12, AppColors.SecondaryText, 600), StatusTone.Neutral), 3);
        Add(grid, Text(row.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), 12, AppColors.SecondaryText), 4);
        return new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) }.BorderBrush(Theme(AppColors.Stroke));
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
    public SerialImeiViewModel ViewModel { get; }
    public SerialPageButtons(SerialImeiViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        var previous = Button(Icon("left", size: 18), tooltip: "Trang trước");
        previous.Command(x => x.Binding(() => ViewModel.PreviousPageCommand));
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
        next.IsEnabled(x => x.Binding(() => ViewModel.CanGoNext));
        Children.Add(next);
    }
}
