using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Inventory;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory;

public sealed class PurchaseReceiptListView : Page
{
    public PurchaseReceiptListViewModel ViewModel { get; }

    public PurchaseReceiptListView(PurchaseReceiptListViewModel viewModel)
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star), MinWidth = 200 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Search box
        var search = new TextBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 14,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(40, 8, 12, 8),
            PlaceholderText = "Tìm theo số phiếu, số chứng từ..."
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));

        AutomationProperties.SetName(search, "Tìm phiếu nhập");
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

        // Supplier filter ComboBox
        var supplierCombo = new ComboBox
        {
            Height = 40,
            MinWidth = 180,
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 32, 6),
            DisplayMemberPath = nameof(SupplierFilterOption.Name),
            SelectedValuePath = nameof(SupplierFilterOption.Id),
            // A null SelectedValue does not select the "all" option (its Id is null), so show the same text.
            PlaceholderText = "Nhà cung cấp: Tất cả",
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.SecondaryText))
        .BorderBrush(Theme(AppColors.Stroke));

        AutomationProperties.SetName(supplierCombo, "Lọc nhà cung cấp");
        supplierCombo.ItemsSource(x => x.Binding(() => ViewModel.SupplierOptions));
        supplierCombo.SelectedValue(x => x.Binding(() => ViewModel.SelectedSupplierId).Mode(BindingMode.TwoWay));
        Grid.SetColumn(supplierCombo, 1);
        grid.Children.Add(supplierCombo);

        // Create Receipt Button
        var addBtn = Button(
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    Icon("add", AppColors.OnPrimary, 18),
                    Text("Tạo phiếu nhập", 14, AppColors.OnPrimary, 600)
                }
            },
            primary: true,
            tooltip: "Tạo phiếu nhập hàng mới");
        addBtn.Height = 40;
        addBtn.Padding = new Thickness(16, 0, 16, 0);
        addBtn.Command(x => x.Binding(() => ViewModel.CreateReceiptCommand));
        addBtn.Visibility(x => x.Binding(() => ViewModel.CanWrite).Convert(ShowIf));
        Grid.SetColumn(addBtn, 2);
        grid.Children.Add(addBtn);

        return grid;
    }

    private FrameworkElement BuildCard()
    {
        var table = new PurchaseReceiptTableRows(ViewModel);
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

        var empty = Text("Không tìm thấy phiếu nhập nào.", 14, AppColors.SecondaryText);
        empty.Margin = new Thickness(0, 24, 0, 24);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Visibility(x => x.Binding(() => ViewModel.IsEmpty).Convert(ShowIf));
        states.Children.Add(empty);

        // Error panel
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

        var retry = Button(Text("Thử lại", 14, AppColors.InfoText), tooltip: "Tải lại danh sách phiếu nhập");
        retry.Command(x => x.Binding(() => ViewModel.LoadCommand));
        retry.Visibility(x => x.Binding(() => ViewModel.IsForbidden).Convert((bool forbidden) => ShowIf(!forbidden)));
        errorPanel.Children.Add(retry);

        errorPanel.Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf));
        states.Children.Add(errorPanel);

        Grid.SetRow(states, 2);
        body.Children.Add(states);

        var footerText = Text("", 13, AppColors.SecondaryText);
        footerText.Text(x => x.Binding(() => ViewModel.FooterText));
        var pager = new PurchaseReceiptListPageButtons(ViewModel);
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
        var grid = PurchaseReceiptTableRows.Columns();
        var labels = new[] { "SỐ PHIẾU", "NGÀY NHẬN", "NHÀ CUNG CẤP", "SỐ CHỨNG TỪ", "SỐ MẶT HÀNG", "TỔNG TIỀN", "THAO TÁC" };
        for (var i = 0; i < labels.Length; i++)
        {
            var text = Text(labels[i], 11, AppColors.SecondaryText, 600);
            text.CharacterSpacing = 45;
            text.HorizontalAlignment = i == 4 ? HorizontalAlignment.Center : i is 5 or 6 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
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

internal sealed class PurchaseReceiptTableRows(PurchaseReceiptListViewModel viewModel) : ItemsControl
{
    internal static Grid Columns()
    {
        var grid = new Grid
        {
            Padding = new Thickness(16, 12, 16, 12),
            ColumnSpacing = 12
        };
        foreach (var star in new[] { 1.6, 1.4, 2.0, 1.4, 1.0, 1.6, 0.8 })
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
        if (element is ContentControl container && item is PurchaseReceiptSummary row)
            container.Content = BuildRow(row);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ContentControl container) container.Content = null;
        base.ClearContainerForItemOverride(element, item);
    }

    private UIElement BuildRow(PurchaseReceiptSummary row)
    {
        var grid = Columns();

        // PoNumber
        var poNum = Text(row.PoNumber, 14, AppColors.Text, 600);
        Grid.SetColumn(poNum, 0);
        grid.Children.Add(poNum);

        // ReceivedAt
        var receivedAt = Text(row.ReceivedAtFormatted, 13, AppColors.SecondaryText);
        Grid.SetColumn(receivedAt, 1);
        grid.Children.Add(receivedAt);

        // SupplierName
        var supplierName = Text(row.SupplierName, 13, AppColors.Text, 500);
        Grid.SetColumn(supplierName, 2);
        grid.Children.Add(supplierName);

        // Document Number
        var docNum = Text(row.DocumentNumberDisplay, 13, AppColors.SecondaryText);
        Grid.SetColumn(docNum, 3);
        grid.Children.Add(docNum);

        // Line Count
        var lineCount = Text(row.LineCount.ToString("N0"), 13, AppColors.Text);
        lineCount.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(lineCount, 4);
        grid.Children.Add(lineCount);

        // Total Cost
        var totalCost = Text(row.TotalCostFormatted, 13, AppColors.InfoText, 600);
        totalCost.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(totalCost, 5);
        grid.Children.Add(totalCost);

        // Row actions
        var detail = RowAction("Xem chi tiết", row, viewModel);
        detail.Command(x => x.Binding(() => viewModel.ViewDetailCommand));

        var actions = RowActionsButton("Thao tác phiếu nhập", detail);
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(actions, 6);
        grid.Children.Add(actions);

        return new Border
        {
            Child = grid,
            BorderThickness = new Thickness(0, 0, 0, 1)
        }.BorderBrush(Theme(AppColors.Stroke));
    }
}

internal sealed class PurchaseReceiptListPageButtons : StackPanel
{
    public PurchaseReceiptListViewModel ViewModel { get; }

    internal PurchaseReceiptListPageButtons(PurchaseReceiptListViewModel viewModel)
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
