using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Customers;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Customers;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Customers;

public sealed class CustomerListPage : Page, IRoutePage
{
    private const double CustomerTableMinimumWidth = 1160;
    private static readonly string[] TierLabels = ["Tất cả hạng", "Tiêu chuẩn", "Bạc", "Vàng", "Bạch kim"];
    private static readonly string[] ActivityLabels = ["Đang hoạt động", "Đã vô hiệu hóa", "Tất cả trạng thái"];
    private readonly ProgressRing _loading = new() { Width = 32, Height = 32 };
    private readonly Border _emptyState;
    private readonly Border _errorState;
    private readonly TextBlock _errorText;
    private readonly TextBlock _countText;
    private readonly TextBox _searchBox;
    private readonly ComboBox _tierFilter;
    private readonly ComboBox _activityFilter;
    private readonly CustomerFormViewModel _formViewModel;
    private readonly CustomerDetailViewModel _detailViewModel;
    private readonly StackPanel _table;
    private bool _isShowingCreateDialog;
    private bool _isShowingDetailDialog;
    private bool _isShowingDeactivationDialog;
    public IAsyncRelayCommand ShowCreateDialogCommand { get; }
    public IAsyncRelayCommand<CustomerSummary> ShowCustomerDetailsCommand { get; }
    public IAsyncRelayCommand<CustomerSummary> ShowCustomerEditCommand { get; }

    public CustomerListViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public CustomerListPage(
        CustomerListViewModel viewModel,
        CustomerFormViewModel formViewModel,
        CustomerDetailViewModel detailViewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        _formViewModel = formViewModel ?? throw new ArgumentNullException(nameof(formViewModel));
        _detailViewModel = detailViewModel ?? throw new ArgumentNullException(nameof(detailViewModel));
        ShowCreateDialogCommand = new AsyncRelayCommand(ShowCreateDialogAsync);
        ShowCustomerDetailsCommand = new AsyncRelayCommand<CustomerSummary>(ShowCustomerDetailsFromRowAsync);
        ShowCustomerEditCommand = new AsyncRelayCommand<CustomerSummary>(ShowCustomerEditFromRowAsync);
        this.Background(AppColors.Brushes.Canvas);

        _countText = Text("", 13, AppColors.SecondaryText)
            .Text(x => x.Binding(() => ViewModel.CountLabel));
        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        var heading = new StackPanel { Spacing = 3 };
        var titleLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        titleLine.Children.Add(new TextBlock().Text("Danh sách khách hàng")
            .Style(AppResources.GetStyle(AppTypography.HeadingText)).Foreground(AppColors.Brushes.Text));
        titleLine.Children.Add(_countText);
        heading.Children.Add(titleLine);
        heading.Children.Add(new TextBlock().Text("Quản lý hồ sơ, hạng thành viên và lịch sử mua hàng.")
            .Style(AppResources.GetStyle(AppTypography.CaptionText)).Foreground(AppColors.Brushes.SecondaryText)
            .TextWrapping(TextWrapping.Wrap));
        var addButton = PrimaryButton("＋  Thêm khách hàng");
        // Modal lifetime commands belong to this View, not its data-context ViewModel.
        addButton.Command(ShowCreateDialogCommand);
        Grid.SetColumn(addButton, 1);
        titleRow.Children.Add(heading);
        titleRow.Children.Add(addButton);

        _searchBox = new TextBox()
            .PlaceholderText("Tìm theo tên hoặc số điện thoại...")
            .Height(40)
            .MinWidth(280)
            .MaxWidth(430)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .CornerRadius(new CornerRadius(10))
            .Padding(new Thickness(12, 6, 12, 6))
            .Text(x => x.Binding(() => ViewModel.SearchText)
                .Mode(BindingMode.TwoWay)
                .UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        AutomationProperties.SetName(_searchBox, "Tìm khách hàng theo tên hoặc số điện thoại");

        _tierFilter = new ComboBox
        {
            ItemsSource = TierLabels,
            SelectedIndex = 0,
            MinWidth = 150,
            Height = 40,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 4, 8, 4)
        };
        _tierFilter.SelectedIndex(x => x.Binding(() => ViewModel.SelectedTierIndex).Mode(BindingMode.TwoWay));
        AutomationProperties.SetName(_tierFilter, "Lọc khách hàng theo hạng thành viên");

        _activityFilter = new ComboBox
        {
            ItemsSource = ActivityLabels,
            SelectedIndex = 0,
            MinWidth = 175,
            Height = 40,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 4, 8, 4)
        };
        _activityFilter.SelectedIndex(x => x.Binding(() => ViewModel.SelectedActivityIndex).Mode(BindingMode.TwoWay));
        AutomationProperties.SetName(_activityFilter, "Lọc khách hàng theo trạng thái hoạt động");

        var searchAndFilters = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };
        searchAndFilters.Children.Add(_searchBox);
        Grid.SetColumn(_tierFilter, 1);
        Grid.SetColumn(_activityFilter, 2);
        searchAndFilters.Children.Add(_tierFilter);
        searchAndFilters.Children.Add(_activityFilter);

        var rows = new ItemsControl()
            .ItemsSource(x => x.Binding(() => ViewModel.Customers))
            .ItemTemplate<CustomerSummary>(customer => BuildCustomerRow(customer));
        var table = new StackPanel { Spacing = 0 };
        _table = table;
        table.MinWidth = CustomerTableMinimumWidth;
        table.Width = CustomerTableMinimumWidth;
        table.Children.Add(BuildHeaderRow());
        table.Children.Add(rows);

        _loading.HorizontalAlignment = HorizontalAlignment.Center;
        _loading.VerticalAlignment = VerticalAlignment.Center;
        _emptyState = BuildMessageCard("Chưa có khách hàng", "Hãy thử đổi từ khóa hoặc bộ lọc để tìm khách hàng.", "○");
        _emptyState.Visibility(x => x.Binding(() => ViewModel.ShowEmptyState)
            .Convert((bool visible) => visible ? Visibility.Visible : Visibility.Collapsed));
        _errorText = Text("", 13, AppColors.DangerText);
        _errorText.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        _errorState = BuildErrorCard(_errorText)
            .Visibility(x => x.Binding(() => ViewModel.IsError)
                .Convert((bool visible) => visible ? Visibility.Visible : Visibility.Collapsed));
        _loading.IsActive(x => x.Binding(() => ViewModel.IsLoading));
        _loading.Visibility(x => x.Binding(() => ViewModel.IsLoading)
            .Convert((bool visible) => visible ? Visibility.Visible : Visibility.Collapsed));

        var bodyGrid = new Grid
        {
            RowSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        var tableScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = table
        };
        void SizeTableToViewport()
        {
            var width = Math.Max(tableScroll.ViewportWidth, CustomerTableMinimumWidth);
            if (Math.Abs(table.Width - width) > 0.5)
                table.Width = width;
        }
        tableScroll.SizeChanged += (_, _) => SizeTableToViewport();
        tableScroll.Loaded += (_, _) => SizeTableToViewport();
        var tableCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = tableScroll
        }.Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke);
        var contentLayer = new Grid();
        contentLayer.Children.Add(tableCard);
        contentLayer.Children.Add(_loading);
        contentLayer.Children.Add(_emptyState);
        contentLayer.Children.Add(_errorState);
        Grid.SetRow(contentLayer, 1);
        bodyGrid.Children.Add(searchAndFilters);
        bodyGrid.Children.Add(contentLayer);

        var footer = BuildPagination();
        Grid.SetRow(footer, 2);
        bodyGrid.Children.Add(footer);

        var root = new Grid
        {
            Padding = new Thickness(28, 24, 28, 24),
            RowSpacing = 18,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };
        Grid.SetRow(titleRow, 0);
        Grid.SetRow(bodyGrid, 1);
        root.Children.Add(titleRow);
        root.Children.Add(bodyGrid);
        Content = root;

    }

    private async Task ShowCreateDialogAsync()
    {
        ViewModel.OpenCreateDialogCommand.Execute(null);
        await ShowCustomerFormAsync(null);
    }

    private async Task ShowCustomerDetailsFromRowAsync(CustomerSummary? customer)
    {
        if (customer is null) return;
        ViewModel.OpenCustomerDetailsCommand.Execute(customer);
        await ShowCustomerDetailsAsync(customer);
    }

    private async Task ShowCustomerEditFromRowAsync(CustomerSummary? customer)
    {
        if (customer is null) return;

        await ViewModel.LoadCustomerForEditCommand.ExecuteAsync(customer);
        if (ViewModel.CustomerToEdit is { } profile)
            await ShowCustomerFormAsync(profile);
        else
            await ShowCustomerDetailsAsync(customer);
    }

    private Grid BuildHeaderRow()
    {
        var grid = CreateTableGrid();
        grid.Background(AppColors.Brushes.Subtle);
        grid.Padding = new Thickness(0, 13, 0, 13);
        var headers = new[] { "KHÁCH HÀNG", "SĐT", "HẠNG", "ĐIỂM TÍCH LŨY", "CHI TIÊU", "LẦN MUA GẦN NHẤT", "THAO TÁC" };
        for (var i = 0; i < headers.Length; i++)
        {
            var text = Text(headers[i], 10, AppColors.SecondaryText, true);
            text.TextWrapping = TextWrapping.Wrap;
            if (i is 3 or 4) text.TextAlignment = TextAlignment.Right;
            if (i == 6)
            {
                text.HorizontalAlignment = HorizontalAlignment.Stretch;
                text.TextAlignment = TextAlignment.Center;
            }
            AddCell(grid, text, i);
        }
        return grid;
    }

    private UIElement BuildCustomerRow(CustomerSummary customer)
    {
        var row = CreateTableGrid();
        row.Padding = new Thickness(0, 10, 0, 10);
        row.BorderBrush(AppColors.Brushes.Stroke);
        row.BorderThickness = new Thickness(0, 0, 0, 1);
        row.MinHeight = 62;

        var identity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(BuildTierAvatar(customer));
        var nameButton = new Button
        {
            Content = Text("", 13, AppColors.Text, true)
                .Text(x => x.Binding(() => customer.FullName)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left
        }.Background(AppColors.Brushes.Surface);
        AutomationProperties.SetName(nameButton, "Mở hồ sơ khách hàng");
        nameButton.Command(ShowCustomerDetailsCommand);
        nameButton.CommandParameter(x => x.Binding());
        identity.Children.Add(nameButton);
        AddCell(row, identity, 0);
        AddCell(row, Text("", 12, AppColors.Text).Text(x => x.Binding(() => customer.Phone)), 1);
        AddCell(row, BuildTierBadge(customer), 2);
        AddCell(row, RightText("", AppColors.Text).Text(x => x.Binding(() => customer.LoyaltyPoints)
            .Convert((int points) => $"{points.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} điểm")), 3);
        AddCell(row, RightText("", AppColors.Text, true).Text(x => x.Binding(() => customer.TotalSpent)
            .Convert((decimal amount) => $"{amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫")), 4);
        var lastPurchase = Text("", 12, AppColors.SecondaryText)
            .Text(x => x.Binding(() => customer.LastPurchaseAt).Convert(FormatLastPurchase));
        lastPurchase.TextTrimming = TextTrimming.CharacterEllipsis;
        AddCell(row, lastPurchase, 5);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var edit = new Button
        {
            Content = new FontIcon
            {
                Glyph = "\uE70F",
                FontSize = 13,
            }.Foreground(AppColors.Brushes.Primary),
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }.Background(AppColors.Brushes.Surface);
        AutomationProperties.SetName(edit, "Chỉnh sửa thông tin khách hàng");
        edit.Command(ShowCustomerEditCommand);
        edit.CommandParameter(x => x.Binding());

        var details = new Button
        {
            Content = new FontIcon
            {
                Glyph = "\uE890",
                FontSize = 13,
            }.Foreground(AppColors.Brushes.Primary),
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }.Background(AppColors.Brushes.Surface);
        AutomationProperties.SetName(details, "Xem hồ sơ khách hàng");
        details.Command(ShowCustomerDetailsCommand);
        details.CommandParameter(x => x.Binding());
        actions.Children.Add(details);
        actions.Children.Add(edit);
        AddCell(row, actions, 6);
        return row;
    }

    private static FrameworkElement BuildTierAvatar(CustomerSummary customer)
    {
        var variants = new Grid { Width = 34, Height = 34 };
        foreach (var tier in CustomerTierPresentation.All)
        {
            var initials = new TextBlock
            {
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
                .Foreground(ThemeResource.Get<Brush>(tier.TextResourceKey))
                .Text(x => x.Binding(() => customer.FullName).Convert(GetInitials));
            variants.Children.Add(new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Child = initials
            }
                .Background(ThemeResource.Get<Brush>(tier.FillResourceKey))
                .Visibility(x => x.Binding(() => customer.Tier)
                    .Convert((string value) => value == tier.Tier ? Visibility.Visible : Visibility.Collapsed)));
        }

        return variants;
    }

    private static FrameworkElement BuildTierBadge(CustomerSummary customer)
    {
        var variants = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var tier in CustomerTierPresentation.All)
        {
            var label = new TextBlock
            {
                Text = tier.BadgeLabel,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            }.Foreground(ThemeResource.Get<Brush>(tier.TextResourceKey));
            variants.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Child = label,
                HorizontalAlignment = HorizontalAlignment.Left
            }
                .Background(ThemeResource.Get<Brush>(tier.FillResourceKey))
                .Visibility(x => x.Binding(() => customer.Tier)
                    .Convert((string value) => value == tier.Tier ? Visibility.Visible : Visibility.Collapsed)));
        }

        return variants;
    }

    private Grid BuildPagination()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Padding = new Thickness(12, 10, 12, 0)
        };
        var rangeText = Text("", 12, AppColors.SecondaryText)
            .Text(x => x.Binding(() => ViewModel.RangeLabel));
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var previous = new Button { Content = "Trang trước", Padding = new Thickness(12, 6, 12, 6) }
            .Command(x => x.Binding(() => ViewModel.PreviousPageCommand))
            .IsEnabled(x => x.Binding(() => ViewModel.HasPreviousPage));
        var pageText = Text("", 12, AppColors.Text, true)
            .Text(x => x.Binding(() => ViewModel.PageLabel));
        var next = new Button { Content = "Trang sau", Padding = new Thickness(12, 6, 12, 6) }
            .Command(x => x.Binding(() => ViewModel.NextPageCommand))
            .IsEnabled(x => x.Binding(() => ViewModel.HasNextPage));
        controls.Children.Add(previous);
        controls.Children.Add(pageText);
        controls.Children.Add(next);
        grid.Children.Add(rangeText);
        Grid.SetColumn(controls, 1);
        grid.Children.Add(controls);
        return grid;
    }

    private Border BuildMessageCard(string title, string description, string glyph)
    {
        var content = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(Text(glyph, 24, AppColors.SecondaryText, true));
        content.Children.Add(Text(title, 16, AppColors.Text, true));
        content.Children.Add(Text(description, 13, AppColors.SecondaryText));
        return new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(28),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        }.Background(AppColors.Brushes.Surface);
    }

    private Border BuildErrorCard(TextBlock errorText)
    {
        var content = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(errorText);
        var retry = new Button { Content = "Thử tải lại", Padding = new Thickness(14, 7, 14, 7) }
            .Foreground(AppColors.Brushes.DangerText);
        retry.Command(x => x.Binding(() => ViewModel.RetryCommand));
        content.Children.Add(retry);
        return new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        }.Background(AppColors.Brushes.DangerFill)
            .BorderBrush(AppColors.Brushes.DangerStroke);
    }

    private Grid CreateTableGrid()
    {
        var grid = new Grid
        {
            MinWidth = CustomerTableMinimumWidth,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(2.1, GridUnitType.Star), MinWidth = 250 },
                new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star), MinWidth = 150 },
                new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star), MinWidth = 120 },
                new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star), MinWidth = 145 },
                new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star), MinWidth = 165 },
                new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star), MinWidth = 180 },
                new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star), MinWidth = 128 }
            }
        };
        grid.SetBinding(FrameworkElement.WidthProperty, new Binding
        {
            Source = _table,
            Path = new PropertyPath(nameof(FrameworkElement.Width)),
            Mode = BindingMode.OneWay
        });
        return grid;
    }

    private static void AddCell(Grid grid, FrameworkElement element, int column)
    {
        if (element is FrameworkElement frameworkElement)
        {
            frameworkElement.Margin = new Thickness(column is 0 or 6 ? 16 : 8, 0, column is 0 or 6 ? 16 : 8, 0);
            frameworkElement.VerticalAlignment = VerticalAlignment.Center;
        }
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private static TextBlock RightText(string value, string colorKey, bool bold = false)
    {
        var text = Text(value, 12, colorKey, bold);
        text.TextAlignment = TextAlignment.Right;
        return text;
    }

    private static TextBlock Text(string value, double size, string colorKey, bool bold = false) =>
        new TextBlock()
            .Text(value)
            .FontSize(size)
            .Foreground(ThemeResource.Get<Brush>(colorKey))
            .FontWeight(bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal)
            .VerticalAlignment(VerticalAlignment.Center);

    private static Button PrimaryButton(string text) => new Button
    {
        Content = Text(text, 13, AppColors.OnPrimary, true),
        Padding = new Thickness(16, 10, 16, 10),
        CornerRadius = new CornerRadius(7),
        HorizontalAlignment = HorizontalAlignment.Right
    }.Background(AppColors.Brushes.Primary)
        .Foreground(AppColors.Brushes.OnPrimary);

    private static string FormatLastPurchase(DateTime? date)
    {
        if (!date.HasValue) return "Chưa mua hàng";
        var local = date.Value.ToLocalTime();
        return local.Date == DateTime.Now.Date
            ? $"Hôm nay, {local:HH:mm}"
            : local.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
    }

    private static string GetInitials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => string.Concat(char.ToUpperInvariant(words[0][0]), char.ToUpperInvariant(words[^1][0]))
        };
    }

    private async Task ShowCustomerFormAsync(CustomerProfile? profile)
    {
        if (_isShowingCreateDialog || XamlRoot is null) return;
        _isShowingCreateDialog = true;
        var returnFocusTarget = _searchBox;
        var form = _formViewModel;
        form.Initialize(profile);
        var popup = new Popup
        {
            XamlRoot = XamlRoot,
            IsLightDismissEnabled = false
        };
        var dialogClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeCommand = new RelayCommand(() => popup.IsOpen = false);
        var saveCommand = new AsyncRelayCommand(async () =>
        {
            await form.SaveCommand.ExecuteAsync(null);
            if (form.SavedProfile is null) return;
            ViewModel.CloseCreateDialogCommand.Execute(null);
            await ViewModel.SearchCommand.ExecuteAsync(null);
            popup.IsOpen = false;
        });
        var formView = new CustomerFormDialogView(form, saveCommand, closeCommand)
        {
            MaxHeight = Math.Max(520, XamlRoot.Size.Height - 64),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var overlay = new Grid
        {
            Width = XamlRoot.Size.Width,
            Height = XamlRoot.Size.Height
        }.Background(ThemeResource.Get<Brush>("ContentDialogSmokeFill"));
        overlay.Children.Add(formView);
        popup.Child = overlay;
        popup.Opened += (_, _) => formView.InitialFocusTarget.Focus(FocusState.Programmatic);
        popup.Closed += (_, _) => dialogClosed.TrySetResult(true);
        try
        {
            popup.IsOpen = true;
            await dialogClosed.Task;
        }
        finally
        {
            ViewModel.CloseCreateDialogCommand.Execute(null);
            _isShowingCreateDialog = false;
            returnFocusTarget?.Focus(FocusState.Programmatic);
        }
    }

    private async Task ShowCustomerDetailsAsync(CustomerSummary customer)
    {
        if (_isShowingDetailDialog || XamlRoot is null) return;
        _isShowingDetailDialog = true;
        var returnFocusTarget = _searchBox;
        var details = _detailViewModel;
        var deactivationRequested = false;
        var popup = new Popup
        {
            XamlRoot = XamlRoot,
            IsLightDismissEnabled = true
        };
        var dialogClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void CloseDetail() => popup.IsOpen = false;
        var deactivateCommand = new RelayCommand(() =>
        {
            deactivationRequested = true;
            CloseDetail();
        });
        var closeCommand = new RelayCommand(CloseDetail);
        var detailView = new CustomerDetailDialogView(
            details,
            deactivateCommand,
            closeCommand)
        {
            MaxHeight = Math.Max(500, XamlRoot.Size.Height - 64),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var overlay = new Grid
        {
            Width = XamlRoot.Size.Width,
            Height = XamlRoot.Size.Height
        }.Background(ThemeResource.Get<Brush>("ContentDialogSmokeFill"));
        overlay.Children.Add(detailView);
        popup.Child = overlay;
        popup.Opened += (_, _) => detailView.InitialFocusTarget.Focus(FocusState.Programmatic);
        popup.Closed += (_, _) => dialogClosed.TrySetResult(true);

        try
        {
            using var loadCancellation = new CancellationTokenSource();
            popup.Closed += (_, _) => loadCancellation.Cancel();
            popup.IsOpen = true;
            try
            {
                await details.LoadAsync(customer.Id, loadCancellation.Token);
            }
            catch (OperationCanceledException) when (loadCancellation.IsCancellationRequested)
            {
            }
            await dialogClosed.Task;
        }
        finally
        {
            _isShowingDetailDialog = false;
            ViewModel.SelectedCustomer = null;
        }

        if (details.ActivityChanged)
            await ViewModel.SearchCommand.ExecuteAsync(null);

        if (deactivationRequested)
        {
            ViewModel.RequestDeactivationCommand.Execute(customer);
            await ShowDeactivationConfirmationAsync();
        }

        returnFocusTarget?.Focus(FocusState.Programmatic);
    }

    private async Task ShowDeactivationConfirmationAsync()
    {
        if (_isShowingDeactivationDialog || XamlRoot is null || ViewModel.CustomerToDeactivate is not { } customer) return;
        _isShowingDeactivationDialog = true;
        var popup = new Popup
        {
            XamlRoot = XamlRoot,
            IsLightDismissEnabled = false
        };
        var dialogClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeCommand = new RelayCommand(() => popup.IsOpen = false);
        var confirmCommand = new AsyncRelayCommand(async () =>
        {
            await ViewModel.ConfirmDeactivationCommand.ExecuteAsync(null);
            if (!ViewModel.IsDeactivationConfirmationOpen) popup.IsOpen = false;
        });
        var dialogView = new CustomerDeactivationDialogView(ViewModel, confirmCommand, closeCommand)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var overlay = new Grid
        {
            Width = XamlRoot.Size.Width,
            Height = XamlRoot.Size.Height
        }.Background(ThemeResource.Get<Brush>("ContentDialogSmokeFill"));
        overlay.Children.Add(dialogView);
        popup.Child = overlay;
        popup.Opened += (_, _) => dialogView.InitialFocusTarget.Focus(FocusState.Programmatic);
        popup.Closed += (_, _) => dialogClosed.TrySetResult(true);
        try
        {
            popup.IsOpen = true;
            await dialogClosed.Task;
        }
        finally
        {
            if (ViewModel.IsDeactivationConfirmationOpen)
                ViewModel.CancelDeactivationCommand.Execute(null);
            _isShowingDeactivationDialog = false;
        }
    }
}
