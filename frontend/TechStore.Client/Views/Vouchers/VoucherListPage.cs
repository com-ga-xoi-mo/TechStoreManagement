using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Vouchers;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.ViewModels.Vouchers;
using TechStore.Client.Views.Inventory;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Vouchers;

/// <summary>Voucher management for Admin and Manager: list, search, status filter, create, edit and switch on or off.</summary>
public sealed class VoucherListPage : Page, IRoutePage
{
    private readonly VoucherEditorDialogViewModel _editor;
    private bool _isShowingEditor;

    public VoucherListViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public VoucherListPage(VoucherListViewModel viewModel, VoucherEditorDialogViewModel editor)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        DataContext = ViewModel;
        this.Background(Theme(AppColors.Canvas));

        var workspace = new Grid { RowSpacing = 16, Padding = new Thickness(24) };
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = BuildHeader();
        Grid.SetRow(header, 0);
        workspace.Children.Add(header);

        var actionError = Text("", 13, AppColors.DangerText);
        actionError.TextWrapping = TextWrapping.Wrap;
        actionError.Text(x => x.Binding(() => ViewModel.ActionErrorMessage));
        actionError.Visibility(x => x.Binding(() => ViewModel.HasActionError).Convert(ShowIf));
        Grid.SetRow(actionError, 1);
        workspace.Children.Add(actionError);

        var filters = BuildFilters();
        Grid.SetRow(filters, 2);
        workspace.Children.Add(filters);

        var card = BuildCard();
        Grid.SetRow(card, 3);
        workspace.Children.Add(card);

        Content = workspace;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private FrameworkElement BuildHeader()
    {
        var back = Button(
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { Text("←", 14, AppColors.SecondaryText, 600), Text("Khách hàng", 14, AppColors.SecondaryText, 500) }
            },
            tooltip: "Quay lại danh sách khách hàng");
        back.HorizontalAlignment = HorizontalAlignment.Left;
        back.Command(x => x.Binding(() => ViewModel.BackCommand));

        var count = Text("", 13, AppColors.SecondaryText);
        count.Text(x => x.Binding(() => ViewModel.CountLabel));
        var titleLine = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { Text("Quản lý voucher", 24, AppColors.Text, 600), count }
        };
        var heading = new StackPanel
        {
            Spacing = 3,
            Children =
            {
                titleLine,
                Text("Tạo mã giảm giá, theo dõi lượt dùng và bật hoặc tắt voucher.", 13, AppColors.SecondaryText)
            }
        };

        var add = Button(
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { Icon("add", AppColors.OnPrimary, 18), Text("Tạo voucher", 14, AppColors.OnPrimary, 600) }
            },
            primary: true,
            tooltip: "Tạo voucher mới");
        add.Height = 40;
        add.Padding = new Thickness(16, 0, 16, 0);
        add.Command(x => x.Binding(() => ViewModel.OpenCreateCommand));
        add.Visibility(x => x.Binding(() => ViewModel.CanManageVouchers).Convert(ShowIf));

        var titleRow = new Grid { ColumnSpacing = 12 };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleRow.Children.Add(heading);
        Grid.SetColumn(add, 1);
        titleRow.Children.Add(add);

        return new StackPanel { Spacing = 10, Children = { back, titleRow } };
    }

    private FrameworkElement BuildFilters()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 240 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 180 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var search = new TextBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 14,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(40, 8, 12, 8),
            PlaceholderText = "Tìm theo mã hoặc mô tả..."
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        AutomationProperties.SetName(search, "Tìm voucher theo mã hoặc mô tả");
        search.Text(x => x.Binding(() => ViewModel.SearchText).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));

        var searchSlot = new Grid();
        searchSlot.Children.Add(search);
        var searchIcon = Icon("search", AppColors.SecondaryText, 18);
        searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
        searchIcon.Margin = new Thickness(12, 0, 0, 0);
        searchIcon.IsHitTestVisible = false;
        searchSlot.Children.Add(searchIcon);
        grid.Children.Add(searchSlot);

        var status = new ComboBox
        {
            Height = 40,
            MinWidth = 160,
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 32, 6),
            ItemsSource = VoucherListViewModel.StatusOptions,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.SecondaryText))
        .BorderBrush(Theme(AppColors.Stroke));
        AutomationProperties.SetName(status, "Lọc voucher theo trạng thái");
        status.SelectedIndex(x => x.Binding(() => ViewModel.SelectedStatusIndex).Mode(BindingMode.TwoWay));
        Grid.SetColumn(status, 1);
        grid.Children.Add(status);

        var clear = Button(Text("Xóa bộ lọc", 13, AppColors.SecondaryText, 500), tooltip: "Xóa tìm kiếm và bộ lọc");
        clear.Height = 40;
        clear.Padding = new Thickness(12, 0, 12, 0);
        clear.Command(x => x.Binding(() => ViewModel.ClearSearchCommand));
        Grid.SetColumn(clear, 2);
        grid.Children.Add(clear);
        return grid;
    }

    private FrameworkElement BuildCard()
    {
        var rows = new VoucherTableRows(ViewModel);
        rows.ItemsSource(x => x.Binding(() => ViewModel.Rows));

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
            Content = rows,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        rowsScroll.Visibility(x => x.Binding(() => ViewModel.ShowTable).Convert(ShowIf));
        Grid.SetRow(rowsScroll, 1);
        body.Children.Add(rowsScroll);

        var states = BuildStates();
        Grid.SetRow(states, 2);
        body.Children.Add(states);

        var footer = BuildFooter();
        Grid.SetRow(footer, 3);
        body.Children.Add(footer);

        return new Border
        {
            Child = body,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12)
        }
        .Background(Theme(AppColors.Surface))
        .BorderBrush(Theme(AppColors.Stroke));
    }

    private FrameworkElement BuildStates()
    {
        var states = new Grid();

        var busy = new ProgressRing
        {
            Width = 24,
            Height = 24,
            Margin = new Thickness(0, 24, 0, 24),
            IsActive = false,
            HorizontalAlignment = HorizontalAlignment.Center
        }.Foreground(Theme(AppColors.Primary));
        busy.IsActive(x => x.Binding(() => ViewModel.IsLoading));
        busy.Visibility(x => x.Binding(() => ViewModel.ShowInitialLoading).Convert(ShowIf));
        states.Children.Add(busy);

        var empty = Text("Không có voucher phù hợp.", 14, AppColors.SecondaryText);
        empty.Margin = new Thickness(0, 24, 0, 24);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Visibility(x => x.Binding(() => ViewModel.ShowEmptyState).Convert(ShowIf));
        states.Children.Add(empty);

        var errorPanel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 24, 0, 24),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var error = Text("", 14, AppColors.DangerText);
        error.TextWrapping = TextWrapping.Wrap;
        error.HorizontalAlignment = HorizontalAlignment.Center;
        error.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        errorPanel.Children.Add(error);
        var retry = Button(Text("Thử lại", 14, AppColors.InfoText), tooltip: "Tải lại danh sách voucher");
        retry.HorizontalAlignment = HorizontalAlignment.Center;
        retry.Command(x => x.Binding(() => ViewModel.RetryCommand));
        errorPanel.Children.Add(retry);
        errorPanel.Visibility(x => x.Binding(() => ViewModel.IsError).Convert(ShowIf));
        states.Children.Add(errorPanel);
        return states;
    }

    private FrameworkElement BuildFooter()
    {
        var range = Text("", 13, AppColors.SecondaryText);
        range.Text(x => x.Binding(() => ViewModel.RangeLabel));

        var previous = new Button { Content = "Trang trước", Padding = new Thickness(12, 6, 12, 6) }
            .Command(x => x.Binding(() => ViewModel.PreviousPageCommand))
            .IsEnabled(x => x.Binding(() => ViewModel.HasPreviousPage));
        var pageText = Text("", 12, AppColors.Text, 600);
        pageText.Text(x => x.Binding(() => ViewModel.PageLabel));
        var next = new Button { Content = "Trang sau", Padding = new Thickness(12, 6, 12, 6) }
            .Command(x => x.Binding(() => ViewModel.NextPageCommand))
            .IsEnabled(x => x.Binding(() => ViewModel.HasNextPage));
        var pager = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { previous, pageText, next } };

        return new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16),
            Child = new ResponsivePair(range, pager)
        }
        .Background(Theme(AppColors.Canvas))
        .BorderBrush(Theme(AppColors.Stroke))
        .Visibility(x => x.Binding(() => ViewModel.ShowPagination).Convert(ShowIf));
    }

    private static Border BuildTableHeader()
    {
        var grid = VoucherTableRows.Columns();
        var labels = new[] { "MÃ VOUCHER", "MỨC GIẢM", "ĐƠN TỐI THIỂU", "LƯỢT DÙNG", "HẾT HẠN", "TRẠNG THÁI", "THAO TÁC" };
        for (var i = 0; i < labels.Length; i++)
        {
            var text = Text(labels[i], 11, AppColors.SecondaryText, 600);
            text.CharacterSpacing = 45;
            text.HorizontalAlignment = i == 5 ? HorizontalAlignment.Center : i == 6 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            Grid.SetColumn(text, i);
            grid.Children.Add(text);
        }

        return new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) }
            .Background(Theme(AppColors.Canvas))
            .BorderBrush(Theme(AppColors.Stroke));
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.VoucherEditorRequested -= OnVoucherEditorRequested;
        ViewModel.VoucherEditorRequested += OnVoucherEditorRequested;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) =>
        ViewModel.VoucherEditorRequested -= OnVoucherEditorRequested;

    private void OnVoucherEditorRequested(VoucherItem? voucher) => _ = ShowEditorAsync(voucher);

    private async Task ShowEditorAsync(VoucherItem? voucher)
    {
        if (_isShowingEditor || XamlRoot is null) return;
        _isShowingEditor = true;
        try
        {
            _editor.Initialize(voucher);
            var dialog = new VoucherEditorDialog(_editor) { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
            if (_editor.SavedVoucher is not null) await ViewModel.NotifySavedAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Voucher editor failed: {exception}");
        }
        finally
        {
            _isShowingEditor = false;
        }
    }
}

internal sealed class VoucherTableRows(VoucherListViewModel viewModel) : ItemsControl
{
    internal static Grid Columns()
    {
        var grid = new Grid { Padding = new Thickness(16, 12, 16, 12), ColumnSpacing = 12 };
        foreach (var star in new[] { 2.2, 1.6, 1.3, 1.9, 1.1, 1.3, 0.7 })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(star, GridUnitType.Star) });
        return grid;
    }

    protected override DependencyObject GetContainerForItemOverride() => new ContentControl
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        IsTabStop = false,
        Padding = new Thickness(0)
    };

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ContentControl container && item is VoucherRow row) container.Content = BuildRow(row);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ContentControl container) container.Content = null;
        base.ClearContainerForItemOverride(element, item);
    }

    private UIElement BuildRow(VoucherRow row)
    {
        var grid = Columns();
        var dim = row.CanDeactivate ? AppColors.Text : AppColors.SecondaryText;

        var codePanel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        codePanel.Children.Add(Text(row.Code, 14, dim, 600));
        codePanel.Children.Add(Text(row.Description, 11, AppColors.SecondaryText));
        Grid.SetColumn(codePanel, 0);
        grid.Children.Add(codePanel);

        var discount = Text(row.DiscountLabel, 13, dim, 500);
        Grid.SetColumn(discount, 1);
        grid.Children.Add(discount);

        var min = Text(row.MinOrderLabel, 13, AppColors.SecondaryText);
        Grid.SetColumn(min, 2);
        grid.Children.Add(min);

        var usage = Text(row.UsageLabel, 13, dim);
        usage.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(usage, 3);
        grid.Children.Add(usage);

        var expiry = Text(row.ExpiryLabel, 13, AppColors.SecondaryText);
        Grid.SetColumn(expiry, 4);
        grid.Children.Add(expiry);

        var tone = Enum.TryParse<StatusTone>(row.StatusTone, out var parsed) ? parsed : StatusTone.Neutral;
        var keys = StatusBadgePresentation.GetKeys(tone);
        var status = TableStatusBadge(Text(row.StatusLabel, 12, keys.Text, 600), tone);
        status.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(status, 5);
        grid.Children.Add(status);

        var edit = RowAction("Sửa", row, viewModel);
        edit.Command(x => x.Binding(() => viewModel.OpenEditCommand));
        var toggle = RowAction(row.CanDeactivate ? "Tắt voucher" : "Bật lại", row, viewModel, danger: row.CanDeactivate);
        toggle.Command(x => x.Binding(() => viewModel.ToggleActiveCommand));
        var actions = RowActionsButton("Thao tác voucher", edit, toggle);
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        // Inside a row the DataContext is the row, so the button gets the list ViewModel to bind CanManageVouchers.
        actions.DataContext = viewModel;
        actions.Visibility(x => x.Binding(() => viewModel.CanManageVouchers).Convert(ShowIf));
        Grid.SetColumn(actions, 6);
        grid.Children.Add(actions);

        return new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) }
            .BorderBrush(Theme(AppColors.Stroke));
    }
}
