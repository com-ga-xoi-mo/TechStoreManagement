using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Shared.Enums;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory.Dialogs;

public sealed class PurchaseReceiptDetailDialog : ContentDialog
{
    public PurchaseReceiptDetailDialogViewModel ViewModel { get; }

    public PurchaseReceiptDetailDialog(PurchaseReceiptDetailDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        DefaultButton = ContentDialogButton.None;
        PrimaryButtonText = string.Empty;
        SecondaryButtonText = string.Empty;
        CloseButtonText = string.Empty;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(10);
        Width = 680;
        MaxWidth = 680;
        Resources["ContentDialogMaxWidth"] = 680d;
        Resources["ContentDialogPadding"] = new Thickness(0);
        Resources["ContentDialogCommandSpace"] = 0d;
        this.Background(Theme(AppColors.Surface));
        Content = BuildLayout();
        ViewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => ViewModel.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs e) => Hide();

    private Grid BuildLayout()
    {
        var root = new Grid { Width = 680, MaxWidth = 680 }.Background(Theme(AppColors.Surface));
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(BuildHeader());

        var scroll = new ScrollViewer
        {
            MaxHeight = 650,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildBody()
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private Grid BuildHeader()
    {
        var header = new Grid
        {
            Padding = new Thickness(20, 16, 16, 16),
            BorderThickness = new Thickness(0, 0, 0, 1)
        }.BorderBrush(Theme(AppColors.Stroke));

        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = Text("", 18, AppColors.Text, 600);
        title.Text(x => x.Binding(() => ViewModel.Title));
        header.Children.Add(title);

        var close = Button(Icon("close", AppColors.SecondaryText, 20), tooltip: "Đóng");
        close.Command(x => x.Binding(() => ViewModel.CloseCommand));
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        return header;
    }

    private UIElement BuildBody()
    {
        var root = new Grid { Padding = new Thickness(20) };

        var loading = new ProgressRing
        {
            Width = 32,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 40, 0, 40)
        }.Foreground(Theme(AppColors.Primary));
        loading.IsActive(x => x.Binding(() => ViewModel.IsLoading));
        loading.Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert(ShowIf));
        root.Children.Add(loading);

        var error = Text("", 14, AppColors.DangerText);
        error.HorizontalAlignment = HorizontalAlignment.Center;
        error.Margin = new Thickness(0, 30, 0, 30);
        error.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        error.Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf));
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(error);

        var content = new StackPanel { Spacing = 20 };
        content.Visibility(x => x.Binding(() => ViewModel.Receipt).Convert((PurchaseReceiptDetail? r) => ShowIf(r != null)));

        // Summary Cards
        var summaryCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1)
        }
        .Background(Theme(AppColors.Subtle))
        .BorderBrush(Theme(AppColors.Stroke));

        var summaryGrid = new Grid { RowSpacing = 10, ColumnSpacing = 16 };
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summaryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        summaryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        summaryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Supplier
        var supplierInfo = new StackPanel { Spacing = 2 };
        supplierInfo.Children.Add(Text("NHÀ CUNG CẤP", 11, AppColors.SecondaryText, 600));
        var supplierName = Text("", 14, AppColors.Text, 600);
        supplierName.Text(x => x.Binding(() => ViewModel.Receipt!.SupplierName));
        supplierInfo.Children.Add(supplierName);
        Grid.SetRow(supplierInfo, 0);
        Grid.SetColumn(supplierInfo, 0);
        summaryGrid.Children.Add(supplierInfo);

        // Document Number
        var docInfo = new StackPanel { Spacing = 2 };
        docInfo.Children.Add(Text("SỐ CHỨNG TỪ", 11, AppColors.SecondaryText, 600));
        var docNum = Text("", 14, AppColors.Text, 500);
        docNum.Text(x => x.Binding(() => ViewModel.Receipt!.DocumentNumberDisplay));
        docInfo.Children.Add(docNum);
        Grid.SetRow(docInfo, 0);
        Grid.SetColumn(docInfo, 1);
        summaryGrid.Children.Add(docInfo);

        // Receiver
        var receiverInfo = new StackPanel { Spacing = 2 };
        receiverInfo.Children.Add(Text("NGƯỜI NHẬP", 11, AppColors.SecondaryText, 600));
        var receiverName = Text("", 14, AppColors.Text, 500);
        receiverName.Text(x => x.Binding(() => ViewModel.Receipt!.ReceiverDisplay));
        receiverInfo.Children.Add(receiverName);
        Grid.SetRow(receiverInfo, 1);
        Grid.SetColumn(receiverInfo, 0);
        summaryGrid.Children.Add(receiverInfo);

        // Received At
        var timeInfo = new StackPanel { Spacing = 2 };
        timeInfo.Children.Add(Text("NGÀY GIỜ NHẬP", 11, AppColors.SecondaryText, 600));
        var receivedTime = Text("", 14, AppColors.Text, 500);
        receivedTime.Text(x => x.Binding(() => ViewModel.Receipt!.ReceivedAtFormatted));
        timeInfo.Children.Add(receivedTime);
        Grid.SetRow(timeInfo, 1);
        Grid.SetColumn(timeInfo, 1);
        summaryGrid.Children.Add(timeInfo);

        // Total Cost
        var totalInfo = new StackPanel { Spacing = 2 };
        totalInfo.Children.Add(Text("TỔNG TIỀN PHIẾU NHẬP", 11, AppColors.SecondaryText, 600));
        var totalCost = Text("", 16, AppColors.InfoText, 700);
        totalCost.Text(x => x.Binding(() => ViewModel.Receipt!.TotalCostFormatted));
        totalInfo.Children.Add(totalCost);
        Grid.SetRow(totalInfo, 2);
        Grid.SetColumn(totalInfo, 0);
        summaryGrid.Children.Add(totalInfo);

        // Note
        var noteInfo = new StackPanel { Spacing = 2 };
        noteInfo.Children.Add(Text("GHI CHÚ", 11, AppColors.SecondaryText, 600));
        var noteText = Text("", 14, AppColors.SecondaryText, 400);
        noteText.Text(x => x.Binding(() => ViewModel.Receipt!.Note));
        noteInfo.Children.Add(noteText);
        Grid.SetRow(noteInfo, 2);
        Grid.SetColumn(noteInfo, 1);
        summaryGrid.Children.Add(noteInfo);

        summaryCard.Child = summaryGrid;
        content.Children.Add(summaryCard);

        // Lines Section
        content.Children.Add(Text("DANH SÁCH MẶT HÀNG", 13, AppColors.Text, 600));

        var linesContainer = new PurchaseReceiptLinesHost();
        linesContainer.ItemsSource(x => x.Binding(() => ViewModel.Receipt!.Lines));
        content.Children.Add(linesContainer);

        content.Children.Add(Text("", 14, AppColors.Text, 600).Text(x => x.Binding(() => ViewModel.StatusLabel)));
        var actions = new StackPanel { Spacing = 8 };
        actions.Visibility(x => x.Binding(() => ViewModel.IsOpenForDelivery).Convert(ShowIf));
        var entries = new ItemsControl().ItemsSource(x => x.Binding(() => ViewModel.DeliveryLines))
            .ItemTemplate<DeliveryLineDraft>(draft => new StackPanel().Spacing(4).Children(
                new TextBlock().Text(x => x.Binding(() => draft.Label)),
                new TextBox { Header = "Số lượng nhận đợt này", PlaceholderText = "Để trống nếu chưa nhận" }
                    .Text(x => x.Binding(() => draft.QuantityText).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                new TextBox { Header = "Serial/IMEI thực nhận (mỗi dòng một mã)", AcceptsReturn = true }
                    .Visibility(x => x.Binding(() => draft.Line.IsSerialTracked).Convert(ShowIf))
                    .Text(x => x.Binding(() => draft.Codes).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged))));
        entries.IsEnabled(x => x.Binding(() => ViewModel.CanEdit));
        actions.Children.Add(new TextBox { Header = "Số chứng từ đợt này", MaxLength = 100, PlaceholderText = "Mặc định dùng chứng từ của phiếu" }
            .IsEnabled(x => x.Binding(() => ViewModel.CanEdit))
            .Text(x => x.Binding(() => ViewModel.DeliveryDocument).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)));
        actions.Children.Add(entries);
        actions.Children.Add(new Button { Content = "Ghi nhận đợt hàng" }.Command(x => x.Binding(() => ViewModel.ReceiveCommand))
            .IsEnabled(x => x.Binding(() => ViewModel.CanReceive)));
        actions.Children.Add(new CheckBox { Content = "Xác nhận hủy toàn bộ số lượng còn chờ nhận" }
            .IsEnabled(x => x.Binding(() => ViewModel.CanEdit))
            .IsChecked(x => x.Binding(() => ViewModel.ConfirmCancel).Mode(BindingMode.TwoWay)));
        actions.Children.Add(new Button { Content = "Hủy phần chưa nhận" }.Command(x => x.Binding(() => ViewModel.CancelRemainderCommand))
            .IsEnabled(x => x.Binding(() => ViewModel.CanCancel)));
        actions.Children.Add(new Button { Content = "Tải lại phiếu" }.Command(x => x.Binding(() => ViewModel.ReloadCommand))
            .Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf)));
        content.Children.Add(actions);
        Grid.SetRow(content, 1);
        root.Children.Add(content);
        return root;
    }

    private Grid BuildFooter()
    {
        var footer = new Grid
        {
            Padding = new Thickness(20, 12, 20, 12),
            BorderThickness = new Thickness(0, 1, 0, 0)
        }
        .Background(Theme(AppColors.Subtle))
        .BorderBrush(Theme(AppColors.Stroke));

        var close = Button(Text("Đóng", 14, AppColors.SecondaryText, 500));
        close.Height = 36;
        close.Padding = new Thickness(20, 0, 20, 0);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Command(x => x.Binding(() => ViewModel.CloseCommand));
        footer.Children.Add(close);

        return footer;
    }
}

internal sealed class PurchaseReceiptLinesHost : ItemsControl
{
    protected override DependencyObject GetContainerForItemOverride() => new ContentControl
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        IsTabStop = false,
        Padding = new Thickness(0)
    };

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ContentControl container && item is PurchaseReceiptLine line)
            container.Content = BuildLineView(line);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ContentControl container) container.Content = null;
        base.ClearContainerForItemOverride(element, item);
    }

    private UIElement BuildLineView(PurchaseReceiptLine line)
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1)
        }
        .Background(Theme(AppColors.Canvas))
        .BorderBrush(Theme(AppColors.Stroke));

        // Line header grid
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

        var productInfo = new StackPanel { Spacing = 2 };
        productInfo.Children.Add(Text(line.DisplayName, 14, AppColors.Text, 600));
        productInfo.Children.Add(Text($"SKU: {line.DisplaySku}", 12, AppColors.SecondaryText));
        Grid.SetColumn(productInfo, 0);
        grid.Children.Add(productInfo);

        var quantity = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        quantity.Children.Add(Text("Đã nhận / Đặt", 11, AppColors.SecondaryText));
        quantity.Children.Add(Text($"{line.Quantity:N0} / {line.OrderedQuantity:N0}", 13, AppColors.Text, 600));
        Grid.SetColumn(quantity, 1);
        grid.Children.Add(quantity);

        var unitCost = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        unitCost.Children.Add(Text("Đơn giá", 11, AppColors.SecondaryText));
        unitCost.Children.Add(Text(line.UnitCostFormatted, 13, AppColors.Text, 500));
        Grid.SetColumn(unitCost, 2);
        grid.Children.Add(unitCost);

        var lineTotal = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        lineTotal.Children.Add(Text("Thành tiền", 11, AppColors.SecondaryText));
        lineTotal.Children.Add(Text(line.LineTotalFormatted, 13, AppColors.InfoText, 600));
        Grid.SetColumn(lineTotal, 3);
        grid.Children.Add(lineTotal);

        panel.Children.Add(grid);

        // If line has devices (Serial / IMEI)
        if (line.Devices.Count > 0)
        {
            var deviceSection = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
            deviceSection.Children.Add(Text($"Danh sách mã Serial / IMEI ({line.Devices.Count} máy, bảo hành {line.WarrantyMonths} tháng):", 12, AppColors.SecondaryText, 500));

            var devicesWrap = new VariableSizedWrapGrid
            {
                Orientation = Orientation.Horizontal,
                MaximumRowsOrColumns = 3,
                ItemWidth = 190
            };

            foreach (var d in line.Devices)
            {
                var deviceItem = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Margin = new Thickness(0, 2, 8, 2)
                };

                deviceItem.Children.Add(Text(d.Identifier, 12, AppColors.Text, 500));

                var tone = d.Status switch
                {
                    SerialImeiStatus.InStock => StatusTone.Success,
                    SerialImeiStatus.Reserved => StatusTone.Info,
                    SerialImeiStatus.Sold => StatusTone.Neutral,
                    SerialImeiStatus.Defective => StatusTone.Danger,
                    _ => StatusTone.Warning
                };

                var statusBadge = TableStatusBadge(Text(d.StatusLabel, 10, AppColors.Text, 500), tone);
                deviceItem.Children.Add(statusBadge);

                devicesWrap.Children.Add(deviceItem);
            }

            deviceSection.Children.Add(devicesWrap);
            panel.Children.Add(deviceSection);
        }

        return panel;
    }
}
