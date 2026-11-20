using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Inventory;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory;

public sealed class SupplierListView : Page
{
    public SupplierListViewModel ViewModel { get; }

    public SupplierListView(SupplierListViewModel viewModel)
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
    }

    private FrameworkElement BuildFilters()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 280 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 180 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Search box
        var search = new TextBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 14,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(40, 8, 12, 8),
            PlaceholderText = "Tìm theo tên, người liên hệ, SĐT hoặc email..."
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));

        AutomationProperties.SetName(search, "Tìm nhà cung cấp");
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

        // Status filter
        var status = CreateCombo(ViewModel.StatusOptions, "Trạng thái hoạt động", nameof(SupplierStatusOption.Label), nameof(SupplierStatusOption.Value));
        status.SelectedValue(x => x.Binding(() => ViewModel.StatusFilter).Mode(BindingMode.TwoWay));
        Grid.SetColumn(status, 1);
        grid.Children.Add(status);

        // Add button
        var addBtn = Button(
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    Icon("add", AppColors.OnPrimary, 18),
                    Text("Thêm nhà cung cấp", 14, AppColors.OnPrimary, 600)
                }
            },
            primary: true,
            tooltip: "Thêm nhà cung cấp mới");
        addBtn.Height = 40;
        addBtn.Padding = new Thickness(16, 0, 16, 0);
        addBtn.Command(x => x.Binding(() => ViewModel.CreateSupplierCommand));
        addBtn.Visibility(x => x.Binding(() => ViewModel.CanWrite).Convert(ShowIf));
        Grid.SetColumn(addBtn, 2);
        grid.Children.Add(addBtn);

        return grid;
    }

    private static ComboBox CreateCombo<T>(IReadOnlyList<T> items, string name, string? displayPath = null, string? valuePath = null)
    {
        var combo = new ComboBox
        {
            Height = 40,
            MinWidth = 160,
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 32, 6),
            ItemsSource = items,
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
        var table = new SupplierTableRows(ViewModel);
        table.ItemsSource(x => x.Binding(() => ViewModel.Items));

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
        var busy = new ProgressRing
        {
            Width = 24,
            Height = 24,
            Margin = new Thickness(0, 24, 0, 24),
            IsActive = false,
            HorizontalAlignment = HorizontalAlignment.Center
        }.Foreground(Theme(AppColors.Primary));
        busy.IsActive(x => x.Binding(() => ViewModel.IsLoading));
        busy.Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert(ShowIf));
        states.Children.Add(busy);

        var empty = Text("Không tìm thấy nhà cung cấp nào.", 14, AppColors.SecondaryText);
        empty.Margin = new Thickness(0, 24, 0, 24);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Visibility(x => x.Binding(() => ViewModel.IsEmpty).Convert(ShowIf));
        states.Children.Add(empty);

        var errorPanel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 24, 0, 24),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var error = Text("", 14, AppColors.DangerText);
        error.HorizontalAlignment = HorizontalAlignment.Center;
        error.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        errorPanel.Children.Add(error);
        var retry = Button(Text("Thử lại", 14, AppColors.InfoText), tooltip: "Tải lại danh sách nhà cung cấp");
        retry.Command(x => x.Binding(() => ViewModel.LoadCommand));
        retry.Visibility(x => x.Binding(() => ViewModel.IsForbidden).Convert((bool forbidden) => ShowIf(!forbidden)));
        errorPanel.Children.Add(retry);
        errorPanel.Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf));
        states.Children.Add(errorPanel);

        Grid.SetRow(states, 2);
        body.Children.Add(states);

        var footerText = Text("", 13, AppColors.SecondaryText);
        footerText.Text(x => x.Binding(() => ViewModel.FooterText));
        var pager = new SupplierListPageButtons(ViewModel);
        var footer = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16),
            Child = new ResponsivePair(footerText, pager)
        }
        .Background(Theme(AppColors.Canvas))
        .BorderBrush(Theme(AppColors.Stroke));
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

    private static Border BuildTableHeader()
    {
        var grid = SupplierTableRows.Columns();
        var labels = new[] { "TÊN NHÀ CUNG CẤP", "NGƯỜI LIÊN HỆ", "SỐ ĐIỆN THOẠI", "EMAIL", "SỐ PHIẾU", "TRẠNG THÁI", "THAO TÁC" };
        for (var i = 0; i < labels.Length; i++)
        {
            var text = Text(labels[i], 11, AppColors.SecondaryText, 600);
            text.CharacterSpacing = 45;
            text.HorizontalAlignment = i is 4 or 5 ? HorizontalAlignment.Center : i == 6 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            Grid.SetColumn(text, i);
            grid.Children.Add(text);
        }
        return new Border
        {
            Child = grid,
            BorderThickness = new Thickness(0, 0, 0, 1)
        }
        .Background(Theme(AppColors.Canvas))
        .BorderBrush(Theme(AppColors.Stroke));
    }
}

internal sealed class SupplierTableRows(SupplierListViewModel viewModel) : ItemsControl
{
    internal static Grid Columns()
    {
        var grid = new Grid
        {
            Padding = new Thickness(16, 12, 16, 12),
            ColumnSpacing = 12
        };
        foreach (var star in new[] { 2.4, 1.4, 1.3, 1.8, 0.9, 1.3, 0.9 })
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
        if (element is ContentControl container && item is SupplierItem row)
            container.Content = BuildRow(row);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ContentControl container) container.Content = null;
        base.ClearContainerForItemOverride(element, item);
    }

    private UIElement BuildRow(SupplierItem row)
    {
        var grid = Columns();
        var primaryTextColor = row.IsActive ? AppColors.Text : AppColors.SecondaryText;

        // Name + Address
        var namePanel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        namePanel.Children.Add(Text(row.Name, 14, primaryTextColor, 600));
        if (!string.IsNullOrWhiteSpace(row.Address))
        {
            namePanel.Children.Add(Text(row.Address, 11, AppColors.SecondaryText, 400));
        }
        Grid.SetColumn(namePanel, 0);
        grid.Children.Add(namePanel);

        // Contact name
        var contact = Text(string.IsNullOrWhiteSpace(row.ContactName) ? "—" : row.ContactName, 13, AppColors.SecondaryText);
        Grid.SetColumn(contact, 1);
        grid.Children.Add(contact);

        // Phone
        var phone = Text(row.Phone, 13, primaryTextColor, 500);
        Grid.SetColumn(phone, 2);
        grid.Children.Add(phone);

        // Email
        var email = Text(string.IsNullOrWhiteSpace(row.Email) ? "—" : row.Email, 13, AppColors.SecondaryText);
        Grid.SetColumn(email, 3);
        grid.Children.Add(email);

        // Receipt count
        var count = Text(row.ReceiptCount.ToString("N0"), 13, primaryTextColor, 600);
        count.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(count, 4);
        grid.Children.Add(count);

        // Status badge
        var tone = row.IsActive ? StatusTone.Success : StatusTone.Neutral;
        var statusKeys = StatusBadgePresentation.GetKeys(tone);
        var status = TableStatusBadge(Text(row.IsActive ? "Đang sử dụng" : "Ngừng sử dụng", 12, statusKeys.Text, 600), tone);
        status.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(status, 5);
        grid.Children.Add(status);

        // Row actions
        var edit = RowAction("Sửa", row, viewModel);
        edit.Command(x => x.Binding(() => viewModel.EditSupplierCommand));

        var toggle = RowAction(row.IsActive ? "Ngừng sử dụng" : "Sử dụng lại", row, viewModel, danger: row.IsActive);
        toggle.Command(x => x.Binding(() => viewModel.ToggleStatusCommand));

        var actions = RowActionsButton("Thao tác nhà cung cấp", edit, toggle);
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        // Inside a row the DataContext is the row, so the button gets the list ViewModel to bind CanWrite.
        actions.DataContext = viewModel;
        actions.Visibility(x => x.Binding(() => viewModel.CanWrite).Convert(ShowIf));
        Grid.SetColumn(actions, 6);
        grid.Children.Add(actions);

        return new Border
        {
            Child = grid,
            BorderThickness = new Thickness(0, 0, 0, 1)
        }.BorderBrush(Theme(AppColors.Stroke));
    }
}

internal sealed class SupplierListPageButtons : StackPanel
{
    public SupplierListViewModel ViewModel { get; }

    internal SupplierListPageButtons(SupplierListViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        Orientation = Orientation.Horizontal;
        Spacing = 8;

        var previous = new Button { Content = "Trang trước", Padding = new Thickness(12, 6, 12, 6) }
            .Command(x => x.Binding(() => ViewModel.PreviousPageCommand))
            .IsEnabled(x => x.Binding(() => ViewModel.CanGoPrevious));

        var pageText = Text("", 12, AppColors.Text, 600);
        pageText.Text(x => x.Binding(() => ViewModel.PageLabel));
        pageText.VerticalAlignment = VerticalAlignment.Center;

        Children.Add(previous);
        Children.Add(pageText);

        var next = new Button { Content = "Trang sau", Padding = new Thickness(12, 6, 12, 6) }
            .Command(x => x.Binding(() => ViewModel.NextPageCommand))
            .IsEnabled(x => x.Binding(() => ViewModel.CanGoNext));

        Children.Add(next);
    }
}
