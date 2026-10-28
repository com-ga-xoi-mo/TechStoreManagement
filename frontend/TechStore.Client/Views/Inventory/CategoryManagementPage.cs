using TechStore.Client.Repositories.Inventory;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Controls;
using TechStore.Client.Views.Inventory.Dialogs;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using TechStore.Shared.DTOs;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory;

/// <summary>Native C# layout and bindings only; all category state belongs to the ViewModel.</summary>
public sealed class CategoryManagementPage : Page, IRoutePage
{
    private readonly CategoryDialogRequests _dialogRequests;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly ContentDialogPresenter _dialogPresenter;
    private readonly InventoryStockPage _inventoryStockPage;
    private readonly SerialImeiPage _serialImeiPage;
    private readonly CategoryDeleteCoordinator _deleteCoordinator = new();
    private readonly AsyncRelayCommand<CategoryDialogRequest> _showDialog;
    public CategoryManagementViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public CategoryManagementPage(
        CategoryManagementViewModel viewModel,
        CategoryDialogRequests dialogRequests,
        IInventoryRepository inventoryRepository,
        IProductRepository productRepository,
        ContentDialogPresenter dialogPresenter)
    {
        ViewModel = viewModel;
        _dialogRequests = dialogRequests;
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _dialogPresenter = dialogPresenter;
        _inventoryStockPage = new InventoryStockPage(viewModel.Inventory);
        _serialImeiPage = new SerialImeiPage(viewModel.Serials);
        _showDialog = new AsyncRelayCommand<CategoryDialogRequest>(ShowDialogAsync);
        // Subscribe immediately as well as on Loaded. ItemsControl rows can be
        // realized before the parent Loaded callback on Uno/Skia.
        ViewModel.Inventory.DeleteRequested += OnInventoryDeleteRequested;
        ViewModel.Inventory.StockDetailRequested += OnInventoryStockDetailRequested;
        ViewModel.Inventory.StockAdjustmentRequested += OnInventoryStockAdjustmentRequested;
        ViewModel.Inventory.ActionRequested += OnInventoryActionRequested;
        DataContext = viewModel;
        this.Background(Theme(AppColors.Canvas));
        var workspace = new Grid { RowSpacing = 24, Padding = new Thickness(24) };
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = BuildHeader();
        Grid.SetRow(header, 0);
        var tabs = BuildTabs();
        Grid.SetRow(tabs, 1);
        workspace.Children.Add(header);
        workspace.Children.Add(tabs);
        var categoryContent = new Grid { RowSpacing = 20 };
        categoryContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        categoryContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        categoryContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var categoryFilters = BuildFilters();
        Grid.SetRow(categoryFilters, 0);
        categoryContent.Children.Add(categoryFilters);
        var statusError = Text(color: AppColors.DangerText);
        statusError.TextWrapping = TextWrapping.Wrap;
        statusError.Text(x => x.Binding(() => ViewModel.StatusUpdateError));
        statusError.Visibility(x => x.Binding(() => ViewModel.HasStatusUpdateError).Convert(ShowIf));
        Grid.SetRow(statusError, 1);
        categoryContent.Children.Add(statusError);
        var categoryCard = BuildCard();
        Grid.SetRow(categoryCard, 2);
        categoryContent.Children.Add(categoryCard);
        categoryContent.Visibility(x => x.Binding(() => ViewModel.IsCategoryTab).Convert(ShowIf));
        Grid.SetRow(categoryContent, 2);
        workspace.Children.Add(categoryContent);
        var inventoryHost = new Border { Child = _inventoryStockPage };
        inventoryHost.Visibility(x => x.Binding(() => ViewModel.IsInventoryTab).Convert(ShowIf));
        Grid.SetRow(inventoryHost, 2);
        workspace.Children.Add(inventoryHost);
        var serialHost = new Border { Child = _serialImeiPage };
        serialHost.Visibility(x => x.Binding(() => ViewModel.IsSerialTab).Convert(ShowIf));
        Grid.SetRow(serialHost, 2);
        workspace.Children.Add(serialHost);
        var root = new Grid();
        root.Children.Add(workspace);
        var successToast = BuildInventorySuccessToast();
        root.Children.Add(successToast);
        Content = root;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private FrameworkElement BuildHeader()
    {
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        title.Children.Add(Text("Danh mục và Kho", 20, AppColors.Text, 700));
        var categoryTotal = Text(size: 14, color: AppColors.SecondaryText);
        categoryTotal.Text(x => x.Binding(() => ViewModel.TotalLabel));
        categoryTotal.Visibility(x => x.Binding(() => ViewModel.IsCategoryTab).Convert(ShowIf));
        title.Children.Add(categoryTotal);
        var inventoryTotal = Text(size: 14, color: AppColors.SecondaryText);
        inventoryTotal.Text(x => x.Binding(() => ViewModel.Inventory.TotalLabel));
        var inventoryTotalHost = new Border { Child = inventoryTotal };
        inventoryTotalHost.Visibility(x => x.Binding(() => ViewModel.IsInventoryTab).Convert(ShowIf));
        title.Children.Add(inventoryTotalHost);
        var serialTotal = Text(size: 14, color: AppColors.SecondaryText);
        serialTotal.Text(x => x.Binding(() => ViewModel.Serials.TotalLabel));
        var serialTotalHost = new Border { Child = serialTotal };
        serialTotalHost.Visibility(x => x.Binding(() => ViewModel.IsSerialTab).Convert(ShowIf));
        title.Children.Add(serialTotalHost);

        var actions = new Grid();
        var categoryAction = BuildPrimaryAction("add", "Thêm danh mục", "Thêm danh mục");
        categoryAction.Command(x => x.Binding(() => ViewModel.AddCategoryCommand));
        categoryAction.Visibility(x => x.Binding(() => ViewModel.IsCategoryTab).Convert(ShowIf));
        actions.Children.Add(categoryAction);
        var inventoryActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var exportReportText = Text(color: AppColors.SecondaryText);
        exportReportText.Text(x => x.Binding(() => ViewModel.Inventory.ExportButtonText));
        var exportReport = Button(exportReportText, tooltip: "Xuất báo cáo tồn kho");
        exportReport.Command(x => x.Binding(() => ViewModel.Inventory.ExportReportCommand));
        inventoryActions.Children.Add(exportReport);
        var importAction = BuildPrimaryAction("add", "Nhập kho mới", "Nhập kho mới");
        importAction.Command(x => x.Binding(() => ViewModel.Inventory.ImportStockCommand));
        inventoryActions.Children.Add(importAction);
        inventoryActions.Visibility(x => x.Binding(() => ViewModel.IsInventoryTab).Convert(ShowIf));
        actions.Children.Add(inventoryActions);
        var serialActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var serialImportAction = BuildPrimaryAction("add", "Nhập kho mới", "Nhập kho mới");
        serialImportAction.Command(x => x.Binding(() => ViewModel.Inventory.ImportStockCommand));
        serialActions.Children.Add(serialImportAction);
        serialActions.Visibility(x => x.Binding(() => ViewModel.IsSerialTab).Convert(ShowIf));
        actions.Children.Add(serialActions);
        return new ResponsivePair(title, actions);
    }

    private static Button BuildPrimaryAction(string icon, string label, string tooltip)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(Icon(icon, AppColors.OnPrimary, 18));
        content.Children.Add(Text(label, 14, AppColors.OnPrimary, 600));
        return Button(content, primary: true, tooltip: tooltip);
    }

    private FrameworkElement BuildTabs()
    {
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        tabs.Children.Add(CreateTabButton("Danh mục", color =>
        {
            var text = Text("0", 12, color, 600);
            text.Text(x => x.Binding(() => ViewModel.TotalCount));
            return text;
        }, CategoryWorkspaceTab.Categories));
        tabs.Children.Add(CreateTabButton("Tồn kho", color =>
        {
            var text = Text("0", 12, color, 600);
            text.Text(x => x.Binding(() => ViewModel.Inventory.TotalCount));
            return text;
        }, CategoryWorkspaceTab.Inventory));
        tabs.Children.Add(CreateTabButton("Serial / IMEI", color =>
        {
            var text = Text("0", 12, color, 600);
            text.Text(x => x.Binding(() => ViewModel.Serials.TotalCount));
            return text;
        }, CategoryWorkspaceTab.SerialImei));
        return new Border
        {
            Child = tabs,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, -8, 0, 0)
        }.BorderBrush(Theme(AppColors.Stroke));
    }

    private Button CreateTabButton(string label, Func<string, TextBlock> buildCount, CategoryWorkspaceTab tab)
    {
        var activeContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        activeContent.Children.Add(Text(label, 14, AppColors.InfoText, 600));
        activeContent.Children.Add(Badge(buildCount(AppColors.InfoText), StatusTone.Info));
        var activeTab = new Border
        {
            Child = activeContent,
            Padding = new Thickness(4, 12, 4, 12),
            BorderThickness = new Thickness(0, 0, 0, 2)
        }
        .BorderBrush(Theme(AppColors.Focus))
        .Visibility(x => x.Binding(() => ViewModel.SelectedTab).Convert((CategoryWorkspaceTab selected) => ShowIf(selected == tab)));

        var inactiveContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        inactiveContent.Children.Add(Text(label, 14, AppColors.SecondaryText, 600));
        inactiveContent.Children.Add(Badge(buildCount(AppColors.SecondaryText), StatusTone.Neutral));
        var inactiveTab = new Border
        {
            Child = inactiveContent,
            Padding = new Thickness(4, 12, 4, 12),
            BorderThickness = new Thickness(0, 0, 0, 2)
        }
        .Visibility(x => x.Binding(() => ViewModel.SelectedTab).Convert((CategoryWorkspaceTab selected) => ShowIf(selected != tab)));

        var host = new Grid().Children(activeTab, inactiveTab);
        var button = Button(host, tooltip: $"Mở {label}");
        button.Padding = new Thickness(4, 0, 4, 0);
        button.Command(x => x.Binding(() => ViewModel.SelectTabCommand));
        button.CommandParameter = tab;
        return button;
    }

    private FrameworkElement BuildFilters()
    {
        var search = new TextBox
        {
            Height = 40, CornerRadius = new CornerRadius(8), FontSize = 14,
            BorderThickness = new Thickness(1), Padding = new Thickness(40, 8, 12, 8),
            PlaceholderText = "Tìm theo tên danh mục", HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(search, "Tìm theo tên danh mục");
        search.Text(x => x.Binding(() => ViewModel.SearchText).Mode(BindingMode.TwoWay));
        var searchBox = new Grid { MaxWidth = 512, HorizontalAlignment = HorizontalAlignment.Stretch };
        searchBox.Children.Add(search);
        var searchIcon = Icon("search", AppColors.SecondaryText, 18);
        searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
        searchIcon.Margin = new Thickness(12, 0, 0, 0);
        searchIcon.IsHitTestVisible = false;
        searchBox.Children.Add(searchIcon);
        var status = new ComboBox
        {
            Height = 40, MinWidth = 200, CornerRadius = new CornerRadius(8), FontSize = 14,
            BorderThickness = new Thickness(1), Padding = new Thickness(12, 6, 32, 6),
            ItemsSource = ViewModel.StatusOptions, DisplayMemberPath = nameof(CategoryStatusOption.Label),
            SelectedValuePath = nameof(CategoryStatusOption.Value), HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.SecondaryText))
        .BorderBrush(Theme(AppColors.Stroke));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(status, "Trạng thái danh mục");
        status.SelectedValue(x => x.Binding(() => ViewModel.StatusFilter).Mode(BindingMode.TwoWay));
        var searchSlot = new Grid();
        searchBox.HorizontalAlignment = HorizontalAlignment.Left;
        searchSlot.Children.Add(searchBox);
        searchSlot.SizeChanged += (_, args) => searchBox.Width = Math.Min(512, args.NewSize.Width);
        return new ResponsivePair(searchSlot, status) { Margin = new Thickness(0, -8, 0, 0) };
    }

    private FrameworkElement BuildCard()
    {
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = CategoryTableRows.Columns();
        header.Background(Theme(AppColors.Canvas));
        var labels = new[] { "TÊN DANH MỤC", "DANH MỤC CHA", "SỐ SẢN PHẨM", "TRẠNG THÁI", "THAO TÁC" };
        for (var i = 0; i < labels.Length; i++)
        {
            var text = Text(labels[i], 12, AppColors.SecondaryText, 600);
            text.CharacterSpacing = 50;
            text.HorizontalAlignment = i is 2 or 3 ? HorizontalAlignment.Center : i == 4 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            var cell = new Border { Child = text, Padding = new Thickness(16, 14, 16, 14) };
            Grid.SetColumn(cell, i);
            header.Children.Add(cell);
        }
        var headerBorder = new Border { Child = header, BorderThickness = new Thickness(0, 0, 0, 1) }.BorderBrush(Theme(AppColors.Stroke));
        Grid.SetRow(headerBorder, 0);
        body.Children.Add(headerBorder);

        var rows = new CategoryTableRows(ViewModel);
        rows.ItemsSource(x => x.Binding(() => ViewModel.VisibleRows));
        var rowsScroll = new ScrollViewer
        {
            Content = rows,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(rowsScroll, 1);
        body.Children.Add(rowsScroll);

        var states = new StackPanel { Spacing = 12, Padding = new Thickness(24) };
        var busy = new ProgressRing { IsActive = false, Width = 24, Height = 24 }.Foreground(Theme(AppColors.Primary));
        busy.Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert(ShowIf));
        busy.IsActive(x => x.Binding(() => ViewModel.IsLoading));
        states.Children.Add(busy);
        var empty = Text(color: AppColors.SecondaryText);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Text(x => x.Binding(() => ViewModel.EmptyText));
        empty.Visibility(x => x.Binding(() => ViewModel.IsEmpty).Convert(ShowIf));
        states.Children.Add(empty);
        var errorPanel = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        var error = Text(color: AppColors.DangerText);
        error.TextWrapping = TextWrapping.Wrap;
        error.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        errorPanel.Children.Add(error);
        var retry = Button(Text("Thử lại", color: AppColors.InfoText), tooltip: "Tải lại danh mục");
        retry.Command(x => x.Binding(() => ViewModel.RetryCommand));
        errorPanel.Children.Add(retry);
        errorPanel.Visibility(x => x.Binding(() => ViewModel.IsError).Convert(ShowIf));
        states.Children.Add(errorPanel);
        // A collapsed state wrapper removes the whitespace in the successful table view.
        var stateWrapper = new Border { Child = states }
            .Visibility(x => x.Binding(() => ViewModel.ShowStatePanel).Convert(ShowIf));
        Grid.SetRow(stateWrapper, 2);
        body.Children.Add(stateWrapper);

        var footerText = Text(color: AppColors.SecondaryText);
        footerText.TextWrapping = TextWrapping.Wrap;
        footerText.Text(x => x.Binding(() => ViewModel.FooterText));
        var paging = new CategoryPageButtons(ViewModel);
        var footer = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16),
            Child = new ResponsivePair(footerText, paging)
        }.Background(Theme(AppColors.Canvas)).BorderBrush(Theme(AppColors.Stroke));
        Grid.SetRow(footer, 3);
        body.Children.Add(footer);
        return new Border
        {
            Child = body,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12)
        }.Background(Theme(AppColors.Surface)).BorderBrush(Theme(AppColors.Stroke));
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _dialogRequests.Requested -= OnDialogRequested;
        _dialogRequests.Requested += OnDialogRequested;
        ViewModel.Inventory.DeleteRequested -= OnInventoryDeleteRequested;
        ViewModel.Inventory.DeleteRequested += OnInventoryDeleteRequested;
        ViewModel.Inventory.StockDetailRequested -= OnInventoryStockDetailRequested;
        ViewModel.Inventory.StockDetailRequested += OnInventoryStockDetailRequested;
        ViewModel.Inventory.StockAdjustmentRequested -= OnInventoryStockAdjustmentRequested;
        ViewModel.Inventory.StockAdjustmentRequested += OnInventoryStockAdjustmentRequested;
        ViewModel.Inventory.ActionRequested -= OnInventoryActionRequested;
        ViewModel.Inventory.ActionRequested += OnInventoryActionRequested;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _dialogRequests.Requested -= OnDialogRequested;
        ViewModel.Inventory.DeleteRequested -= OnInventoryDeleteRequested;
        ViewModel.Inventory.StockDetailRequested -= OnInventoryStockDetailRequested;
        ViewModel.Inventory.StockAdjustmentRequested -= OnInventoryStockAdjustmentRequested;
        ViewModel.Inventory.ActionRequested -= OnInventoryActionRequested;
        ViewModel.Inventory.DismissSuccessToast();
    }

    private void OnInventoryDeleteRequested(InventoryStockDeleteRequest request)
    {
        // Keep this as a direct async hand-off: AsyncRelayCommand intentionally
        // swallows UI exceptions, which made a dialog construction failure look
        // like an unresponsive trash button during desktop startup.
        _ = ShowInventoryDeleteAsync(request);
    }

    private void OnInventoryStockDetailRequested(InventoryStockRow row) => _ = ShowStockDetailAsync(row);

    private void OnInventoryStockAdjustmentRequested(InventoryStockRow row) => _ = ShowStockAdjustmentAsync(row);

    private void OnInventoryActionRequested(InventoryStockAction action)
    {
        if (action == InventoryStockAction.Import)
            _ = ShowImportStockAsync();
    }

    private async Task ShowImportStockAsync()
    {
        var vm = new ImportStockDialogViewModel(_productRepository, _inventoryRepository);
        var imported = false;
        vm.ImportSucceeded += _ => imported = true;
        var root = XamlRoot ?? _inventoryStockPage.XamlRoot;
        var shown = false;
        try
        {
            if (_dialogPresenter.IsReady)
                shown = await _dialogPresenter.TryShowAsync(() => new ImportStockDialog(vm), CancellationToken.None);
            if (!shown && root is not null)
            {
                var dialog = new ImportStockDialog(vm) { XamlRoot = root };
                await dialog.ShowAsync();
                shown = true;
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Inventory import dialog failed: {exception}");
        }

        if (shown && imported)
        {
            await ViewModel.Inventory.LoadCommand.ExecuteAsync(null);
            if (!ViewModel.Inventory.HasError)
                await ViewModel.Inventory.ShowSuccessToastAsync("Đã nhập kho thành công.");
        }
    }

    private async Task ShowStockDetailAsync(InventoryStockRow row)
    {
        var vm = new StockDetailHistoryDialogViewModel(row, _inventoryRepository);
        var adjustmentRequested = false;
        var serialsRequested = false;
        vm.AdjustmentRequested += (_, _) => adjustmentRequested = true;
        vm.SerialsRequested += (_, _) => serialsRequested = true;
        var root = XamlRoot ?? _inventoryStockPage.XamlRoot;
        var shown = false;
        try
        {
            if (_dialogPresenter.IsReady)
                shown = await _dialogPresenter.TryShowAsync(() => new StockDetailHistoryDialog(vm), CancellationToken.None);
            if (!shown && root is not null)
            {
                var dialog = new StockDetailHistoryDialog(vm) { XamlRoot = root };
                await dialog.ShowAsync();
                shown = true;
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Stock detail dialog failed: {exception}");
        }

        if (shown && adjustmentRequested)
            await ShowStockAdjustmentAsync(row);
        else if (shown && serialsRequested)
            ViewModel.ShowSerialsFor(row);
    }

    private async Task ShowStockAdjustmentAsync(InventoryStockRow row)
    {
        var vm = new StockAdjustmentDialogViewModel(row, _inventoryRepository);
        InventoryStockDetailDto? updated = null;
        vm.AdjustmentSucceeded += result => updated = result;
        var root = XamlRoot ?? _inventoryStockPage.XamlRoot;
        var shown = false;
        try
        {
            if (_dialogPresenter.IsReady)
                shown = await _dialogPresenter.TryShowAsync(() => new StockAdjustmentDialog(vm), CancellationToken.None);
            if (!shown && root is not null)
            {
                var dialog = new StockAdjustmentDialog(vm) { XamlRoot = root };
                await dialog.ShowAsync();
                shown = true;
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Stock adjustment dialog failed: {exception}");
        }

        if (updated is not null)
        {
            await ViewModel.Inventory.LoadCommand.ExecuteAsync(null);
            if (!ViewModel.Inventory.HasError)
                await ViewModel.Inventory.ShowSuccessToastAsync("Đã điều chỉnh tồn kho thành công.");
        }
    }

    private async Task ShowInventoryDeleteAsync(InventoryStockDeleteRequest? request)
    {
        if (request is null) return;

        // The inventory page is nested in this route page. Resolve the root at the
        // time of the click because Uno can assign XamlRoot after Loaded, and a
        // stale null here used to make the trash button appear unresponsive.
        var dialogRoot = XamlRoot ?? _inventoryStockPage.XamlRoot;

        var dialogVm = new DeleteStockItemDialogViewModel(_inventoryRepository, request);
        InventoryStockDeleteCompleted? completed = null;
        dialogVm.DeleteSucceeded += (_, outcome) => completed = outcome;

        bool shown = false;
        try
        {
            if (_dialogPresenter.IsReady)
            {
                shown = await _dialogPresenter.TryShowAsync(
                    () => new DeleteStockItemDialog(dialogVm),
                    CancellationToken.None);
            }

            if (!shown && dialogRoot is not null)
            {
                var dialog = new DeleteStockItemDialog(dialogVm) { XamlRoot = dialogRoot! };
                await dialog.ShowAsync();
                shown = true;
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Inventory delete dialog failed: {exception}");
            if (!shown && dialogRoot is not null)
            {
                try
                {
                    var dialog = new DeleteStockItemDialog(dialogVm) { XamlRoot = dialogRoot };
                    await dialog.ShowAsync();
                    shown = true;
                }
                catch (Exception fallbackException)
                {
                    System.Diagnostics.Debug.WriteLine($"Inventory delete dialog fallback failed: {fallbackException}");
                }
            }
        }

        if (!shown || completed is null) return;

        // The row is never removed optimistically: only the authoritative GET can reconcile it.
        await ViewModel.Inventory.LoadCommand.ExecuteAsync(null);
        if (!ViewModel.Inventory.HasError)
            await ViewModel.Inventory.ShowSuccessToastAsync(completed.Message);
    }

    private UIElement BuildInventorySuccessToast()
    {
        var message = Text(size: 12, color: AppColors.SecondaryText);
        message.TextWrapping = TextWrapping.Wrap;
        message.Text(x => x.Binding(() => ViewModel.Inventory.SuccessToastMessage));

        var close = Button(Icon("close", AppColors.SecondaryText, 16), tooltip: "Đóng thông báo");
        close.Command(x => x.Binding(() => ViewModel.Inventory.DismissToastCommand));
        close.Width = 28;
        close.Height = 28;
        close.Padding = new Thickness(0);

        var content = new Grid { ColumnSpacing = 10 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var successIcon = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Child = Icon("check_circle", AppColors.SuccessText, 18)
        };
        successIcon.Background(AppColors.Brushes.SuccessFill);
        successIcon.Visibility(x => x.Binding(() => ViewModel.Inventory.IsSuccessToastKind).Convert(ShowIf));
        var errorIcon = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Child = Icon("warning", AppColors.DangerText, 18)
        };
        errorIcon.Background(AppColors.Brushes.DangerFill);
        errorIcon.Visibility(x => x.Binding(() => ViewModel.Inventory.IsErrorToast).Convert(ShowIf));
        var iconHost = new Grid().Children(successIcon, errorIcon);
        content.Children.Add(iconHost);
        Grid.SetColumn(iconHost, 0);
        var copy = new StackPanel { Spacing = 2 };
        var title = Text("", 13, AppColors.Text, 600);
        title.Text(x => x.Binding(() => ViewModel.Inventory.ToastTitle));
        copy.Children.Add(title);
        copy.Children.Add(message);
        Grid.SetColumn(copy, 1);
        content.Children.Add(copy);
        Grid.SetColumn(close, 2);
        content.Children.Add(close);

        var toast = new Border
        {
            Width = 380,
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 16, 16, 0),
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Shadow = new ThemeShadow(),
            Child = content
        };
        toast.Background(AppColors.Brushes.Surface);
        toast.BorderBrush(AppColors.Brushes.Stroke);
        toast.Visibility(x => x.Binding(() => ViewModel.Inventory.IsSuccessToastVisible).Convert(ShowIf));
        return toast;
    }

    private void OnDialogRequested(object? sender, CategoryDialogRequest request)
    {
        if (_showDialog.CanExecute(request)) _showDialog.Execute(request);
    }

    private async Task ShowDialogAsync(CategoryDialogRequest? request)
    {
        if (request is null || XamlRoot is null) return;
        if (request.Action == CategoryDialogAction.Add)
        {
            var dialogVm = new CreateCategoryDialogViewModel(ViewModel.Categories, ViewModel.RootCategories);
            var created = false;
            dialogVm.CategoryCreated += (_, _) => created = true;

            var shown = false;
            if (_dialogPresenter.IsReady)
            {
                shown = await _dialogPresenter.TryShowAsync(
                    () => new CreateCategoryDialog(dialogVm),
                    CancellationToken.None);
            }
            if (!shown)
            {
                var dialog = new CreateCategoryDialog(dialogVm)
                {
                    XamlRoot = XamlRoot,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                await dialog.ShowAsync();
            }

            if (created)
                await ViewModel.RefreshAsync();
            return;
        }

        if (request.Action == CategoryDialogAction.Edit)
        {
            var category = FindCategory(ViewModel.RootCategories, request.CategoryId);
            if (category is null) return;

            var dialogVm = new EditCategoryDialogViewModel(
                ViewModel.Categories,
                category,
                ViewModel.RootCategories.ToArray());
            var updated = false;
            dialogVm.CategoryUpdated += (_, _) => updated = true;

            _ = await _dialogPresenter.TryShowAsync(
                () => new EditCategoryDialog(dialogVm),
                CancellationToken.None);

            if (updated)
                await ViewModel.RefreshAsync();
            return;
        }

        var target = FindCategory(ViewModel.RootCategories, request.CategoryId);
        if (target is null) return;

        var parentName = FindParentName(ViewModel.RootCategories, target.Id);
        var deleteVm = new DeleteCategoryDialogViewModel(ViewModel.Categories, target, parentName);
        await _deleteCoordinator.PresentAsync(
            deleteVm,
            () => _dialogPresenter.TryShowAsync(
                () => new DeleteCategoryDialog(deleteVm),
                CancellationToken.None),
            () => ViewModel.RefreshAsync());
    }

    private static CategoryNode? FindCategory(IEnumerable<CategoryNode> roots, Guid? id)
    {
        if (id is null) return null;
        var visited = new HashSet<Guid>();
        CategoryNode? Search(IEnumerable<CategoryNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!visited.Add(node.Id)) continue;
                if (node.Id == id.Value) return node;
                var found = Search(node.Children);
                if (found is not null) return found;
            }
            return null;
        }

        return Search(roots);
    }

    private static string? FindParentName(IEnumerable<CategoryNode> roots, Guid targetId)
    {
        var visited = new HashSet<Guid>();
        string? Search(IEnumerable<CategoryNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!visited.Add(node.Id)) continue;
                foreach (var child in node.Children)
                {
                    if (child.Id == targetId) return node.Name;
                    var found = Search([child]);
                    if (found is not null) return found;
                }
            }
            return null;
        }

        return Search(roots);
    }
}

internal sealed class CategoryPageButtons : StackPanel
{
    public CategoryManagementViewModel ViewModel { get; }
    internal CategoryPageButtons(CategoryManagementViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        var previous = Button(Icon("left", size: 18), tooltip: "Trang trước");
        previous.Command(x => x.Binding(() => ViewModel.PreviousPageCommand));
        previous.Height = 32;
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
        Children.Add(next);
    }
}
