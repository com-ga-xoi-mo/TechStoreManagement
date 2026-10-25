using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TechStore.Client.Features.Categories.Dialogs;
using TechStore.Client.Features.Categories.Models;
using TechStore.Client.Features.Categories.Services;
using TechStore.Client.Features.Categories.ViewModels;
using TechStore.Client.Features.Inventory.Dialogs;
using TechStore.Client.Features.Inventory.Models;
using TechStore.Client.Features.Inventory.Pages;
using TechStore.Client.Features.Inventory.ViewModels;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using TechStore.Shared.DTOs;
using static TechStore.Client.Features.Categories.Pages.CategoryWorkspaceStyles;

namespace TechStore.Client.Features.Categories.Pages;

/// <summary>Native C# layout and bindings only; all category state belongs to the ViewModel.</summary>
public sealed class CategoryManagementPage : Page, IRoutePage
{
    private readonly CategoryDialogRequests _dialogRequests;
    private readonly ICategoryApiClient _categoryApiClient;
    private readonly IInventoryApiClient _inventoryApiClient;
    private readonly IProductRepository _productRepository;
    private readonly ContentDialogPresenter _dialogPresenter;
    private readonly InventoryStockPage _inventoryStockPage;
    private readonly SerialImeiPage _serialImeiPage;
    private readonly InventoryStockViewModel _inventoryStockViewModel;
    private readonly SerialImeiViewModel _serialImeiViewModel;
    private readonly CategoryDeleteCoordinator _deleteCoordinator = new();
    private readonly AsyncRelayCommand<CategoryDialogRequest> _showDialog;
    private readonly CategoryRouteViewModel _routeViewModel;
    public CategoryManagementViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => _routeViewModel;

    public CategoryManagementPage(
        CategoryManagementViewModel viewModel,
        CategoryDialogRequests dialogRequests,
        ICategoryApiClient categoryApiClient,
        IInventoryApiClient inventoryApiClient,
        IProductRepository productRepository,
        ContentDialogPresenter dialogPresenter,
        InventoryStockViewModel inventoryStockViewModel,
        SerialImeiViewModel serialImeiViewModel)
    {
        ViewModel = viewModel;
        _dialogRequests = dialogRequests;
        _categoryApiClient = categoryApiClient;
        _inventoryApiClient = inventoryApiClient;
        _productRepository = productRepository;
        _dialogPresenter = dialogPresenter;
        _inventoryStockViewModel = inventoryStockViewModel;
        _serialImeiViewModel = serialImeiViewModel;
        _inventoryStockPage = new InventoryStockPage(inventoryStockViewModel);
        _serialImeiPage = new SerialImeiPage(serialImeiViewModel);
        _routeViewModel = new CategoryRouteViewModel(viewModel);
        _showDialog = new AsyncRelayCommand<CategoryDialogRequest>(ShowDialogAsync);
        // Subscribe immediately as well as on Loaded. ItemsControl rows can be
        // realized before the parent Loaded callback on Uno/Skia.
        _inventoryStockViewModel.DeleteRequested += OnInventoryDeleteRequested;
        _inventoryStockViewModel.StockDetailRequested += OnInventoryStockDetailRequested;
        _inventoryStockViewModel.StockAdjustmentRequested += OnInventoryStockAdjustmentRequested;
        _inventoryStockViewModel.ActionRequested += OnInventoryActionRequested;
        DataContext = viewModel;
        RequestedTheme = ElementTheme.Light;
        Background = Brush(Slate50);
        FontFamily = Typeface;
        var workspace = new StackPanel { Spacing = 24, Padding = new Thickness(24) };
        var header = BuildHeader();
        var tabs = BuildTabs();
        workspace.Children.Add(header);
        workspace.Children.Add(tabs);
        var categoryContent = new StackPanel { Spacing = 24 };
        categoryContent.Children.Add(BuildFilters());
        categoryContent.Children.Add(BuildCard());
        Bind(categoryContent, VisibilityProperty, ViewModel, nameof(ViewModel.IsCategoryTab), converter: new VisibilityConverter());
        workspace.Children.Add(categoryContent);
        Bind(_inventoryStockPage, VisibilityProperty, ViewModel, nameof(ViewModel.IsInventoryTab), converter: new VisibilityConverter());
        workspace.Children.Add(_inventoryStockPage);
        Bind(_serialImeiPage, VisibilityProperty, ViewModel, nameof(ViewModel.IsSerialTab), converter: new VisibilityConverter());
        workspace.Children.Add(_serialImeiPage);
        var workspaceScroll = new ScrollViewer
        {
            Content = workspace, HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        var root = new Grid();
        root.Children.Add(workspaceScroll);
        var successToast = BuildInventorySuccessToast();
        root.Children.Add(successToast);
        Content = root;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private UIElement BuildHeader()
    {
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        title.Children.Add(Text("Danh mục và Kho", 20, Slate900, 700));
        var categoryTotal = Text(size: 14, color: Slate500);
        Bind(categoryTotal, TextBlock.TextProperty, ViewModel, nameof(ViewModel.TotalLabel));
        Bind(categoryTotal, VisibilityProperty, ViewModel, nameof(ViewModel.IsCategoryTab), converter: new VisibilityConverter());
        title.Children.Add(categoryTotal);
        var inventoryTotal = Text(size: 14, color: Slate500);
        Bind(inventoryTotal, TextBlock.TextProperty, _inventoryStockViewModel, nameof(InventoryStockViewModel.TotalLabel));
        Bind(inventoryTotal, VisibilityProperty, ViewModel, nameof(ViewModel.IsInventoryTab), converter: new VisibilityConverter());
        title.Children.Add(inventoryTotal);

        var actions = new Grid();
        var categoryAction = BuildPrimaryAction("add", "Thêm danh mục", "Thêm danh mục", ViewModel.AddCategoryCommand);
        Bind(categoryAction, VisibilityProperty, ViewModel, nameof(ViewModel.IsCategoryTab), converter: new VisibilityConverter());
        actions.Children.Add(categoryAction);
        var inventoryActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var exportReport = Button(Text("Xuất báo cáo", color: Slate500), tooltip: "Xuất báo cáo tồn kho");
        exportReport.Command = _inventoryStockViewModel.ExportReportCommand;
        inventoryActions.Children.Add(exportReport);
        inventoryActions.Children.Add(BuildPrimaryAction("add", "Nhập kho mới", "Nhập kho mới", _inventoryStockViewModel.ImportStockCommand));
        Bind(inventoryActions, VisibilityProperty, ViewModel, nameof(ViewModel.IsInventoryTab), converter: new VisibilityConverter());
        actions.Children.Add(inventoryActions);
        return new ResponsivePair(title, actions);
    }

    private static Button BuildPrimaryAction(string icon, string label, string tooltip, System.Windows.Input.ICommand command)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(Icon(icon, "#FFFFFF", 18));
        content.Children.Add(Text(label, 14, "#FFFFFF", 600));
        var button = Button(content, primary: true, tooltip: tooltip);
        button.Command = command;
        return button;
    }

    private UIElement BuildTabs()
    {
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        var categoryCount = Text("0", 12, Primary, 600);
        Bind(categoryCount, TextBlock.TextProperty, ViewModel, nameof(ViewModel.TotalCount));
        tabs.Children.Add(CreateTabButton("Danh mục", categoryCount, CategoryWorkspaceTab.Categories));
        var inventoryCount = Text("0", 12, Primary, 600);
        Bind(inventoryCount, TextBlock.TextProperty, _inventoryStockViewModel, nameof(InventoryStockViewModel.TotalCount));
        tabs.Children.Add(CreateTabButton("Tồn kho", inventoryCount, CategoryWorkspaceTab.Inventory));
        var serialCount = Text("0", 12, Slate500, 600);
        Bind(serialCount, TextBlock.TextProperty, _serialImeiViewModel, nameof(SerialImeiViewModel.TotalCount));
        tabs.Children.Add(CreateTabButton("Serial / IMEI", serialCount, CategoryWorkspaceTab.SerialImei));
        return new Border
        {
            Child = tabs, BorderBrush = Brush(Slate200), BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, -8, 0, 0)
        };
    }

    private Button CreateTabButton(string label, object count, CategoryWorkspaceTab tab)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var labelText = Text(label, 14, Slate500, 600);
        BindingOperations.SetBinding(labelText, TextBlock.ForegroundProperty, new Binding
        {
            Source = ViewModel, Path = new PropertyPath(nameof(ViewModel.SelectedTab)),
            Converter = new TabColorConverter(), ConverterParameter = tab
        });
        content.Children.Add(labelText);
        var countText = count is TextBlock textBlock ? textBlock : Text((string)count, 12, Slate500, 600);
        var badge = Badge(countText, "#F1F5F9");
        BindingOperations.SetBinding(badge, Border.BackgroundProperty, new Binding
        {
            Source = ViewModel, Path = new PropertyPath(nameof(ViewModel.SelectedTab)),
            Converter = new TabBadgeConverter(), ConverterParameter = tab
        });
        content.Children.Add(badge);
        var underline = new Border
        {
            Child = content,
            Padding = new Thickness(4, 12, 4, 12),
            BorderThickness = new Thickness(0, 0, 0, 2)
        };
        BindingOperations.SetBinding(underline, Border.BorderBrushProperty, new Binding
        {
            Source = ViewModel, Path = new PropertyPath(nameof(ViewModel.SelectedTab)),
            Converter = new TabUnderlineConverter(), ConverterParameter = tab
        });
        var button = Button(underline, tooltip: $"Mở {label}");
        button.Padding = new Thickness(4, 0, 4, 0);
        button.Command = ViewModel.SelectTabCommand;
        button.CommandParameter = tab;
        return button;
    }

    private UIElement BuildFilters()
    {
        var search = new TextBox
        {
            Height = 40, CornerRadius = new CornerRadius(8), FontSize = 14, FontFamily = Typeface,
            Background = Brush("#FFFFFF"), Foreground = Brush(Slate900), BorderBrush = Brush(Slate200),
            BorderThickness = new Thickness(1), Padding = new Thickness(40, 8, 12, 8),
            PlaceholderText = "Tìm theo tên danh mục", HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(search, "Tìm theo tên danh mục");
        Bind(search, TextBox.TextProperty, ViewModel, nameof(ViewModel.SearchText), BindingMode.TwoWay);
        var searchBox = new Grid { MaxWidth = 512, HorizontalAlignment = HorizontalAlignment.Stretch };
        searchBox.Children.Add(search);
        var searchIcon = Icon("search", "#94A3B8", 18);
        searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
        searchIcon.Margin = new Thickness(12, 0, 0, 0);
        searchIcon.IsHitTestVisible = false;
        searchBox.Children.Add(searchIcon);
        var status = new ComboBox
        {
            Height = 40, MinWidth = 200, CornerRadius = new CornerRadius(8), FontSize = 14, FontFamily = Typeface,
            Background = Brush("#FFFFFF"), Foreground = Brush("#334155"), BorderBrush = Brush(Slate200),
            BorderThickness = new Thickness(1), Padding = new Thickness(12, 6, 32, 6),
            ItemsSource = ViewModel.StatusOptions, DisplayMemberPath = nameof(CategoryStatusOption.Label),
            SelectedValuePath = nameof(CategoryStatusOption.Value), HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(status, "Trạng thái danh mục");
        Bind(status, ComboBox.SelectedValueProperty, ViewModel, nameof(ViewModel.StatusFilter), BindingMode.TwoWay);
        var searchSlot = new Grid();
        searchBox.HorizontalAlignment = HorizontalAlignment.Left;
        searchSlot.Children.Add(searchBox);
        searchSlot.SizeChanged += (_, args) => searchBox.Width = Math.Min(512, args.NewSize.Width);
        return new ResponsivePair(searchSlot, status) { Margin = new Thickness(0, -8, 0, 0) };
    }

    private UIElement BuildCard()
    {
        var body = new StackPanel();
        var table = new StackPanel { MinWidth = 880 };
        var header = CategoryTableRows.Columns();
        header.Background = Brush(Slate50);
        var labels = new[] { "TÊN DANH MỤC", "DANH MỤC CHA", "SỐ SẢN PHẨM", "TRẠNG THÁI", "THAO TÁC" };
        for (var i = 0; i < labels.Length; i++)
        {
            var text = Text(labels[i], 12, Slate500, 600);
            text.CharacterSpacing = 50;
            text.HorizontalAlignment = i is 2 or 3 ? HorizontalAlignment.Center : i == 4 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            var cell = new Border { Child = text, Padding = new Thickness(16, 14, 16, 14) };
            Grid.SetColumn(cell, i);
            header.Children.Add(cell);
        }
        table.Children.Add(new Border { Child = header, BorderBrush = Brush(Slate200), BorderThickness = new Thickness(0, 0, 0, 1) });
        var rows = new CategoryTableRows(ViewModel);
        Bind(rows, ItemsControl.ItemsSourceProperty, ViewModel, nameof(ViewModel.VisibleRows));
        table.Children.Add(rows);
        body.Children.Add(new ScrollViewer
        {
            Content = table, HorizontalScrollMode = ScrollMode.Enabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        });
        var states = new StackPanel { Spacing = 12, Padding = new Thickness(24) };
        var busy = new ProgressRing { IsActive = true, Width = 24, Height = 24, Foreground = Brush(Primary) };
        Bind(busy, VisibilityProperty, ViewModel, nameof(ViewModel.IsLoading), converter: new VisibilityConverter());
        Bind(busy, ProgressRing.IsActiveProperty, ViewModel, nameof(ViewModel.IsLoading));
        states.Children.Add(busy);
        var empty = Text(color: Slate500);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        Bind(empty, TextBlock.TextProperty, ViewModel, nameof(ViewModel.EmptyText));
        Bind(empty, VisibilityProperty, ViewModel, nameof(ViewModel.IsEmpty), converter: new VisibilityConverter());
        states.Children.Add(empty);
        var errorPanel = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        var error = Text(color: "#B91C1C");
        error.TextWrapping = TextWrapping.Wrap;
        Bind(error, TextBlock.TextProperty, ViewModel, nameof(ViewModel.ErrorMessage));
        errorPanel.Children.Add(error);
        var retry = Button(Text("Thử lại", color: Primary), tooltip: "Tải lại danh mục");
        retry.Command = ViewModel.LoadCommand;
        errorPanel.Children.Add(retry);
        Bind(errorPanel, VisibilityProperty, ViewModel, nameof(ViewModel.HasError), converter: new VisibilityConverter());
        states.Children.Add(errorPanel);
        // A collapsed state wrapper removes the whitespace in the successful table view.
        var stateWrapper = new Border { Child = states };
        void UpdateStateVisibility(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName is nameof(ViewModel.IsLoading) or nameof(ViewModel.IsEmpty) or nameof(ViewModel.HasError))
                stateWrapper.Visibility = ViewModel.IsLoading || ViewModel.IsEmpty || ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
        }
        stateWrapper.Loaded += (_, _) => { ViewModel.PropertyChanged += UpdateStateVisibility; UpdateStateVisibility(null, new(nameof(ViewModel.IsLoading))); };
        stateWrapper.Unloaded += (_, _) => ViewModel.PropertyChanged -= UpdateStateVisibility;
        body.Children.Add(stateWrapper);
        var footerText = Text(color: Slate500);
        footerText.TextWrapping = TextWrapping.Wrap;
        Bind(footerText, TextBlock.TextProperty, ViewModel, nameof(ViewModel.FooterText));
        var paging = new CategoryPageButtons(ViewModel);
        body.Children.Add(new Border
        {
            Background = Brush(Slate50), BorderBrush = Brush(Slate200), BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16), Child = new ResponsivePair(footerText, paging)
        });
        return new Border
        {
            Child = body, Background = Brush("#FFFFFF"), BorderBrush = Brush(Slate200), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Shadow = new ThemeShadow(), Translation = new System.Numerics.Vector3(0, 0, 2)
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _dialogRequests.Requested -= OnDialogRequested;
        _dialogRequests.Requested += OnDialogRequested;
        _inventoryStockViewModel.DeleteRequested -= OnInventoryDeleteRequested;
        _inventoryStockViewModel.DeleteRequested += OnInventoryDeleteRequested;
        _inventoryStockViewModel.StockDetailRequested -= OnInventoryStockDetailRequested;
        _inventoryStockViewModel.StockDetailRequested += OnInventoryStockDetailRequested;
        _inventoryStockViewModel.StockAdjustmentRequested -= OnInventoryStockAdjustmentRequested;
        _inventoryStockViewModel.StockAdjustmentRequested += OnInventoryStockAdjustmentRequested;
        _inventoryStockViewModel.ActionRequested -= OnInventoryActionRequested;
        _inventoryStockViewModel.ActionRequested += OnInventoryActionRequested;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _dialogRequests.Requested -= OnDialogRequested;
        _inventoryStockViewModel.DeleteRequested -= OnInventoryDeleteRequested;
        _inventoryStockViewModel.StockDetailRequested -= OnInventoryStockDetailRequested;
        _inventoryStockViewModel.StockAdjustmentRequested -= OnInventoryStockAdjustmentRequested;
        _inventoryStockViewModel.ActionRequested -= OnInventoryActionRequested;
        _inventoryStockViewModel.DismissSuccessToast();
        _serialImeiViewModel.CancelLoading();
        ViewModel.CancelLoading();
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
        var vm = new ImportStockDialogViewModel(_productRepository, _inventoryApiClient);
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
            await _inventoryStockViewModel.LoadCommand.ExecuteAsync(null);
            if (!_inventoryStockViewModel.HasError)
                await _inventoryStockViewModel.ShowSuccessToastAsync("Đã nhập kho thành công.");
        }
    }

    private async Task ShowStockDetailAsync(InventoryStockRow row)
    {
        var vm = new StockDetailHistoryDialogViewModel(row, _inventoryApiClient);
        var adjustmentRequested = false;
        vm.AdjustmentRequested += (_, _) => adjustmentRequested = true;
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
    }

    private async Task ShowStockAdjustmentAsync(InventoryStockRow row)
    {
        var vm = new StockAdjustmentDialogViewModel(row, _inventoryApiClient);
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
            await _inventoryStockViewModel.LoadCommand.ExecuteAsync(null);
            if (!_inventoryStockViewModel.HasError)
                await _inventoryStockViewModel.ShowSuccessToastAsync("Đã điều chỉnh tồn kho thành công.");
        }
    }

    private async Task ShowInventoryDeleteAsync(InventoryStockDeleteRequest? request)
    {
        if (request is null) return;

        // The inventory page is nested in this route page. Resolve the root at the
        // time of the click because Uno can assign XamlRoot after Loaded, and a
        // stale null here used to make the trash button appear unresponsive.
        var dialogRoot = XamlRoot ?? _inventoryStockPage.XamlRoot;

        var dialogVm = new DeleteStockItemDialogViewModel(_inventoryApiClient, request);
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

            // TryShowAsync can return false during a Shell attach/detach race or
            // while the presenter is serializing another dialog. If a live root is
            // available, make one direct attempt instead of silently dropping the
            // user's delete action.
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
        await _inventoryStockViewModel.LoadCommand.ExecuteAsync(null);
        if (!_inventoryStockViewModel.HasError)
            await _inventoryStockViewModel.ShowSuccessToastAsync(completed.Message);
    }

    private UIElement BuildInventorySuccessToast()
    {
        var message = Text(size: 12, color: Slate500);
        message.TextWrapping = TextWrapping.Wrap;
        Bind(message, TextBlock.TextProperty, _inventoryStockViewModel, nameof(InventoryStockViewModel.SuccessToastMessage));

        var close = Button(Icon("close", Slate500, 16), tooltip: "Đóng thông báo");
        close.Command = _inventoryStockViewModel.DismissToastCommand;
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
            Child = Icon("check_circle", "#059669", 18)
        };
        successIcon.Background(AppColors.Brushes.SuccessFill);
        content.Children.Add(successIcon);
        Grid.SetColumn(successIcon, 0);
        var copy = new StackPanel { Spacing = 2 };
        copy.Children.Add(Text("Thành công", 13, Slate900, 600));
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
        toast.BorderBrush(AppColors.Brushes.SuccessStroke);
        Bind(toast, VisibilityProperty, _inventoryStockViewModel, nameof(InventoryStockViewModel.IsSuccessToastVisible), converter: new VisibilityConverter());
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
            var dialogVm = new CreateCategoryDialogViewModel(_categoryApiClient, ViewModel.RootCategories);
            dialogVm.CategoryCreated += async (_, _) =>
            {
                await ViewModel.LoadCommand.ExecuteAsync(null);
            };

            var dialog = new CreateCategoryDialog(dialogVm)
            {
                XamlRoot = XamlRoot,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            await dialog.ShowAsync();
            return;
        }

        if (request.Action == CategoryDialogAction.Edit)
        {
            var category = FindCategory(ViewModel.RootCategories, request.CategoryId);
            if (category is null) return;

            var dialogVm = new EditCategoryDialogViewModel(
                _categoryApiClient,
                category,
                ViewModel.RootCategories.ToArray());
            var updated = false;
            dialogVm.CategoryUpdated += (_, _) => updated = true;

            _ = await _dialogPresenter.TryShowAsync(
                () => new EditCategoryDialog(dialogVm),
                CancellationToken.None);

            if (updated)
                await ViewModel.LoadCommand.ExecuteAsync(null);
            return;
        }

        var target = FindCategory(ViewModel.RootCategories, request.CategoryId);
        if (target is null) return;

        var parentName = FindParentName(ViewModel.RootCategories, target.Id);
        var deleteVm = new DeleteCategoryDialogViewModel(_categoryApiClient, target, parentName);
        await _deleteCoordinator.PresentAsync(
            deleteVm,
            () => _dialogPresenter.TryShowAsync(
                () => new DeleteCategoryDialog(deleteVm),
                CancellationToken.None),
            () => ViewModel.LoadCommand.ExecuteAsync(null));
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

    private sealed class CategoryRouteViewModel(CategoryManagementViewModel categoryViewModel) : PageViewModel
    {
        protected override async Task<bool> LoadContentAsync(CancellationToken cancellationToken)
        {
            await categoryViewModel.LoadCommand.ExecuteAsync(null);
            cancellationToken.ThrowIfCancellationRequested();
            return categoryViewModel.HasLoaded && !categoryViewModel.HasError;
        }

        protected override void OnDeactivated() => categoryViewModel.CancelLoading();
    }
}

internal sealed class CategoryPageButtons : StackPanel
{
    private readonly CategoryManagementViewModel _viewModel;
    internal CategoryPageButtons(CategoryManagementViewModel viewModel)
    {
        _viewModel = viewModel;
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        Loaded += (_, _) => { _viewModel.PropertyChanged += OnChanged; Render(); };
        Unloaded += (_, _) => _viewModel.PropertyChanged -= OnChanged;
    }

    private void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(_viewModel.CurrentPage)) Render();
    }

    private void Render()
    {
        Children.Clear();
        var previous = Button(Icon("left", size: 18), tooltip: "Trang trước");
        previous.Command = _viewModel.PreviousPageCommand;
        previous.Height = 32;
        Children.Add(previous);
        foreach (var page in _viewModel.PageNumbers)
        {
            var active = page == _viewModel.CurrentPage;
            var button = Button(Text(page.ToString(), 12, active ? "#FFFFFF" : "#334155", 600), active, $"Trang {page}");
            button.Width = 32;
            button.Height = 32;
            button.Padding = new Thickness(0);
            button.Command = _viewModel.GoToPageCommand;
            button.CommandParameter = page;
            if (!active) { button.Background = Brush("#FFFFFF"); button.BorderBrush = Brush(Slate200); button.BorderThickness = new Thickness(1); }
            Children.Add(button);
        }
        var next = Button(Icon("right", size: 18), tooltip: "Trang sau");
        next.Command = _viewModel.NextPageCommand;
        next.Height = 32;
        Children.Add(next);
    }
}

internal sealed class TabColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is CategoryWorkspaceTab selected && parameter is CategoryWorkspaceTab tab && selected == tab
            ? Brush(Primary)
            : Brush(Slate500);

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed class TabBadgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is CategoryWorkspaceTab selected && parameter is CategoryWorkspaceTab tab && selected == tab
            ? Brush("#EFF6FF")
            : Brush("#F1F5F9");

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed class TabUnderlineConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is CategoryWorkspaceTab selected && parameter is CategoryWorkspaceTab tab && selected == tab
            ? Brush(Primary)
            : Brush("#00000000");

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
