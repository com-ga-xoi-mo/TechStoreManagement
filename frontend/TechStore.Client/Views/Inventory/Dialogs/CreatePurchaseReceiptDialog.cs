using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Inventory;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory.Dialogs;

public sealed class CreatePurchaseReceiptDialog : ContentDialog
{
    public CreatePurchaseReceiptDialogViewModel ViewModel { get; }

    public CreatePurchaseReceiptDialog(CreatePurchaseReceiptDialogViewModel viewModel)
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
        Width = 760;
        MaxWidth = 760;
        Resources["ContentDialogMaxWidth"] = 760d;
        Resources["ContentDialogPadding"] = new Thickness(0);
        Resources["ContentDialogCommandSpace"] = 0d;
        this.Background(Theme(AppColors.Surface));
        Content = BuildLayout();
        ViewModel.RequestClose += OnRequestClose;
        Loaded += (_, _) => _ = ViewModel.InitializeAsync();
        Closed += (_, _) => ViewModel.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs e) => Hide();

    private Grid BuildLayout()
    {
        var root = new Grid { Width = 760, MaxWidth = 760 }.Background(Theme(AppColors.Surface));
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

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(Icon("inventory", AppColors.Primary, 22));
        titleStack.Children.Add(Text("Tạo phiếu nhập hàng", 18, AppColors.Text, 600));
        header.Children.Add(titleStack);

        var close = Button(Icon("close", AppColors.SecondaryText, 20), tooltip: "Hủy bỏ");
        close.Command(x => x.Binding(() => ViewModel.CancelCommand));
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        return header;
    }

    private UIElement BuildBody()
    {
        var body = new StackPanel { Padding = new Thickness(20), Spacing = 16 };

        // Section 1: General info (Supplier, Document number, Note)
        var generalGrid = new Grid { ColumnSpacing = 12, RowSpacing = 10 };
        generalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        generalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        generalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        generalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Supplier Picker
        var supplierPanel = new StackPanel { Spacing = 4 };
        supplierPanel.Children.Add(RequiredLabel("Nhà cung cấp"));
        var supplierCombo = new ComboBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 32, 6),
            DisplayMemberPath = nameof(SupplierItem.Name),
            PlaceholderText = "Chọn nhà cung cấp...",
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        supplierCombo.ItemsSource(x => x.Binding(() => ViewModel.Suppliers));
        supplierCombo.SelectedItem(x => x.Binding(() => ViewModel.SelectedSupplier).Mode(BindingMode.TwoWay));
        supplierPanel.Children.Add(supplierCombo);
        Grid.SetColumn(supplierPanel, 0);
        Grid.SetRow(supplierPanel, 0);
        generalGrid.Children.Add(supplierPanel);

        // Document Number
        var docPanel = new StackPanel { Spacing = 4 };
        docPanel.Children.Add(Text("Số chứng từ / hóa đơn", 13, AppColors.Text, 600));
        var docInput = CreateTextInput("Ví dụ: HD-2026/01...");
        docInput.Text(x => x.Binding(() => ViewModel.SupplierDocumentNumber).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        docPanel.Children.Add(docInput);
        Grid.SetColumn(docPanel, 1);
        Grid.SetRow(docPanel, 0);
        generalGrid.Children.Add(docPanel);

        // Note
        var notePanel = new StackPanel { Spacing = 4 };
        notePanel.Children.Add(Text("Ghi chú phiếu nhập", 13, AppColors.Text, 600));
        var noteInput = CreateTextInput("Nhập ghi chú thêm cho đợt nhập hàng này...");
        noteInput.Text(x => x.Binding(() => ViewModel.Note).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        notePanel.Children.Add(noteInput);
        Grid.SetColumnSpan(notePanel, 2);
        Grid.SetRow(notePanel, 1);
        generalGrid.Children.Add(notePanel);

        body.Children.Add(generalGrid);

        // Section 2: Product Picker (to add line)
        var pickerCard = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1)
        }
        .Background(Theme(AppColors.Subtle))
        .BorderBrush(Theme(AppColors.Stroke));

        var pickerStack = new StackPanel { Spacing = 10 };
        pickerStack.Children.Add(Text("Thêm mặt hàng vào phiếu", 13, AppColors.Text, 600));

        var pickerRow = new Grid { ColumnSpacing = 10 };
        pickerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        pickerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        pickerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Product Combo
        var productCombo = new ComboBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 32, 6),
            DisplayMemberPath = nameof(ImportStockProductOption.DisplayText),
            PlaceholderText = "Chọn sản phẩm cần nhập...",
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        productCombo.ItemsSource(x => x.Binding(() => ViewModel.ProductOptions));
        productCombo.SelectedItem(x => x.Binding(() => ViewModel.SelectedProduct).Mode(BindingMode.TwoWay));
        Grid.SetColumn(productCombo, 0);
        pickerRow.Children.Add(productCombo);

        // Variant Combo
        var variantCombo = new ComboBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 32, 6),
            DisplayMemberPath = nameof(ImportStockVariantOption.DisplayText),
            PlaceholderText = "Chọn biến thể...",
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        variantCombo.ItemsSource(x => x.Binding(() => ViewModel.VariantOptions));
        variantCombo.SelectedItem(x => x.Binding(() => ViewModel.SelectedVariant).Mode(BindingMode.TwoWay));
        variantCombo.Visibility(x => x.Binding(() => ViewModel.HasVariants).Convert(ShowIf));
        Grid.SetColumn(variantCombo, 1);
        pickerRow.Children.Add(variantCombo);

        // Add Line Button
        var addLineBtn = Button(
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Icon("add", AppColors.OnPrimary, 16), Text("Thêm dòng", 13, AppColors.OnPrimary, 600) } },
            primary: true,
            tooltip: "Thêm mặt hàng này vào phiếu nhập");
        addLineBtn.Height = 40;
        addLineBtn.Padding = new Thickness(14, 0, 14, 0);
        addLineBtn.Command(x => x.Binding(() => ViewModel.AddLineCommand));
        addLineBtn.IsEnabled(x => x.Binding(() => ViewModel.CanAddLine));
        Grid.SetColumn(addLineBtn, 2);
        pickerRow.Children.Add(addLineBtn);

        pickerStack.Children.Add(pickerRow);
        pickerCard.Child = pickerStack;
        body.Children.Add(pickerCard);

        // Section 3: Lines list
        var linesHeader = new Grid();
        linesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        linesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        linesHeader.Children.Add(Text("DANH SÁCH MẶT HÀNG TRONG PHIẾU", 13, AppColors.Text, 600));

        var linesCountText = Text("", 12, AppColors.SecondaryText);
        linesCountText.Text(x => x.Binding(() => ViewModel.Lines.Count).Convert((int count) => $"{count} dòng"));
        Grid.SetColumn(linesCountText, 1);
        linesHeader.Children.Add(linesCountText);
        body.Children.Add(linesHeader);

        var linesHost = new CreatePurchaseReceiptLinesHost(ViewModel);
        linesHost.ItemsSource(x => x.Binding(() => ViewModel.Lines));
        body.Children.Add(linesHost);

        // Section 4: Totals Preview Card
        var totalsCard = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1)
        }
        .Background(Theme(AppColors.InfoFill))
        .BorderBrush(Theme(AppColors.InfoStroke));

        var totalsGrid = new Grid();
        totalsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        totalsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var totalsLeft = new StackPanel { Spacing = 2 };
        var totalQtyText = Text("", 13, AppColors.InfoText, 500);
        totalQtyText.Text(x => x.Binding(() => ViewModel.TotalQuantityPreview).Convert((int q) => $"Tổng số lượng: {q:N0} sản phẩm"));
        totalsLeft.Children.Add(totalQtyText);
        totalsLeft.Children.Add(Text("Tổng tiền xem trước (hệ thống sẽ tính lại khi lưu)", 11, AppColors.SecondaryText));
        totalsGrid.Children.Add(totalsLeft);

        var totalsRight = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Spacing = 2 };
        totalsRight.Children.Add(Text("TỔNG TIỀN DỰ KIẾN", 11, AppColors.SecondaryText, 600));
        var totalCostText = Text("", 18, AppColors.InfoText, 700);
        totalCostText.Text(x => x.Binding(() => ViewModel.TotalCostFormatted));
        totalsRight.Children.Add(totalCostText);
        Grid.SetColumn(totalsRight, 1);
        totalsGrid.Children.Add(totalsRight);

        totalsCard.Child = totalsGrid;
        body.Children.Add(totalsCard);

        // Validation Error
        var validationError = Text("", 13, AppColors.DangerText, 500);
        validationError.TextWrapping = TextWrapping.Wrap;
        validationError.Text(x => x.Binding(() => ViewModel.ValidationMessage));
        validationError.Visibility(x => x.Binding(() => ViewModel.HasValidationError).Convert(ShowIf));
        body.Children.Add(validationError);

        // Server Error & Duplicate Identifiers
        var serverError = Text("", 13, AppColors.DangerText, 500);
        serverError.TextWrapping = TextWrapping.Wrap;
        serverError.Text(x => x.Binding(() => ViewModel.ErrorMessage));
        serverError.Visibility(x => x.Binding(() => ViewModel.HasError).Convert(ShowIf));
        body.Children.Add(serverError);

        var duplicates = Text("", 13, AppColors.DangerText, 500);
        duplicates.TextWrapping = TextWrapping.Wrap;
        duplicates.Text(x => x.Binding(() => ViewModel.DuplicateIdentifiersText));
        duplicates.Visibility(x => x.Binding(() => ViewModel.HasDuplicateIdentifiers).Convert(ShowIf));
        body.Children.Add(duplicates);

        return body;
    }

    private static TextBox CreateTextInput(string placeholder) =>
        new TextBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8, 12, 8),
            PlaceholderText = placeholder
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));

    private static StackPanel RequiredLabel(string label)
    {
        var required = Text("*", 13, AppColors.DangerText, 700);
        required.Margin = new Thickness(3, 0, 0, 0);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { Text(label, 13, AppColors.Text, 600), required }
        };
    }

    private Grid BuildFooter()
    {
        var footer = new Grid
        {
            Padding = new Thickness(20, 14, 20, 14),
            BorderThickness = new Thickness(0, 1, 0, 0)
        }
        .Background(Theme(AppColors.Subtle))
        .BorderBrush(Theme(AppColors.Stroke));

        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cancel = Button(Text("Hủy", 14, AppColors.SecondaryText, 500));
        cancel.Height = 38;
        cancel.Padding = new Thickness(18, 0, 18, 0);
        cancel.Command(x => x.Binding(() => ViewModel.CancelCommand));

        var confirm = Button(Text("Xác nhận nhập", 14, AppColors.OnPrimary, 600), primary: true);
        confirm.Height = 38;
        confirm.Padding = new Thickness(24, 0, 24, 0);
        confirm.Command(x => x.Binding(() => ViewModel.SubmitCommand));
        confirm.IsEnabled(x => x.Binding(() => ViewModel.CanSubmit));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children = { cancel, confirm }
        };
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);

        return footer;
    }
}

internal sealed class CreatePurchaseReceiptLinesHost(CreatePurchaseReceiptDialogViewModel viewModel) : ItemsControl
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
        if (element is ContentControl container && item is ReceiptLineDraft line)
            container.Content = BuildDraftLineView(line);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ContentControl container) container.Content = null;
        base.ClearContainerForItemOverride(element, item);
    }

    private UIElement BuildDraftLineView(ReceiptLineDraft line)
    {
        var card = new Border
        {
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1)
        }
        .Background(Theme(AppColors.Canvas))
        .BorderBrush(Theme(AppColors.Stroke));

        var stack = new StackPanel { Spacing = 10 };

        // Line Header: Name + Remove button
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Spacing = 2 };
        titleStack.Children.Add(Text(line.DisplayName, 14, AppColors.Text, 600));
        titleStack.Children.Add(Text($"SKU: {line.DisplaySku}", 12, AppColors.SecondaryText));
        headerGrid.Children.Add(titleStack);

        var removeBtn = Button(
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { Icon("delete", AppColors.DangerText, 16), Text("Xóa dòng", 12, AppColors.DangerText, 500) } },
            tooltip: "Xóa mặt hàng này khỏi phiếu");
        // Assigned directly: inside a line the DataContext is the line, so a binding
        // to the dialog command would resolve against ReceiptLineDraft and find nothing.
        removeBtn.Command = viewModel.RemoveLineCommand;
        removeBtn.CommandParameter = line;
        Grid.SetColumn(removeBtn, 1);
        headerGrid.Children.Add(removeBtn);

        stack.Children.Add(headerGrid);

        // Inputs row: Quantity, UnitCost, LineTotal
        var inputsGrid = new Grid { ColumnSpacing = 12 };
        inputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        inputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });

        // Quantity
        var qtyPanel = new StackPanel { Spacing = 4 };
        qtyPanel.Children.Add(Text("Số lượng", 12, AppColors.SecondaryText, 500));
        var qtyInput = new TextBox
        {
            Height = 36,
            CornerRadius = new CornerRadius(6),
            FontSize = 13,
            Padding = new Thickness(10, 6, 10, 6),
            BorderThickness = new Thickness(1)
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        qtyInput.Text(x => x.Binding(() => line.QuantityText).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        qtyPanel.Children.Add(qtyInput);
        Grid.SetColumn(qtyPanel, 0);
        inputsGrid.Children.Add(qtyPanel);

        // Unit Cost
        var costPanel = new StackPanel { Spacing = 4 };
        costPanel.Children.Add(Text("Giá nhập (VND)", 12, AppColors.SecondaryText, 500));
        var costInput = new TextBox
        {
            Height = 36,
            CornerRadius = new CornerRadius(6),
            FontSize = 13,
            Padding = new Thickness(10, 6, 10, 6),
            BorderThickness = new Thickness(1),
            PlaceholderText = "Ví dụ: 15.000.000"
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        costInput.Text(x => x.Binding(() => line.UnitCostText).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        costPanel.Children.Add(costInput);
        Grid.SetColumn(costPanel, 1);
        inputsGrid.Children.Add(costPanel);

        // Line Total Preview
        var totalPanel = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
        totalPanel.Children.Add(Text("Thành tiền dự kiến", 12, AppColors.SecondaryText, 500));
        var totalText = Text("", 14, AppColors.InfoText, 600);
        totalText.Text(x => x.Binding(() => line.LineTotalFormatted));
        totalPanel.Children.Add(totalText);
        Grid.SetColumn(totalPanel, 2);
        inputsGrid.Children.Add(totalPanel);

        stack.Children.Add(inputsGrid);

        // If Serial-Tracked: Warranty Months and Serial/IMEI Box
        if (line.IsSerialTracked)
        {
            var serialSection = new StackPanel { Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };

            var warrantyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            warrantyRow.Children.Add(Text("Thời hạn bảo hành:", 12, AppColors.SecondaryText));
            var warrantyInput = new TextBox
            {
                Width = 70,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                FontSize = 12,
                Padding = new Thickness(8, 4, 8, 4),
                BorderThickness = new Thickness(1)
            }
            .FontFamily(AppTypography.Fonts.RegularFont)
            .Background(Theme(AppColors.Surface))
            .Foreground(Theme(AppColors.Text))
            .BorderBrush(Theme(AppColors.Stroke));
            warrantyInput.Text(x => x.Binding(() => line.WarrantyMonthsText).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
            warrantyRow.Children.Add(warrantyInput);
            warrantyRow.Children.Add(Text("tháng", 12, AppColors.SecondaryText));
            serialSection.Children.Add(warrantyRow);

            var boxHeader = new Grid();
            boxHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            boxHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            boxHeader.Children.Add(Text("Mã Serial / IMEI (mỗi dòng một mã, hoặc cách nhau bởi dấu phẩy, chấm phẩy, tab):", 12, AppColors.SecondaryText));

            var countText = Text("", 12, AppColors.InfoText, 500);
            countText.Text(x => x.Binding(() => line.DeviceCount).Convert((int count) => $"Đã nhập {count} mã"));
            Grid.SetColumn(countText, 1);
            boxHeader.Children.Add(countText);
            serialSection.Children.Add(boxHeader);

            var serialInput = new TextBox
            {
                MinHeight = 65,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                CornerRadius = new CornerRadius(6),
                FontSize = 12,
                Padding = new Thickness(10, 6, 10, 6),
                BorderThickness = new Thickness(1),
                PlaceholderText = "Ví dụ:\n351234567890123\n351234567890124"
            }
            .FontFamily(AppTypography.Fonts.RegularFont)
            .Background(Theme(AppColors.Surface))
            .Foreground(Theme(AppColors.Text))
            .BorderBrush(Theme(AppColors.Stroke));
            serialInput.Text(x => x.Binding(() => line.SerialLines).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
            serialSection.Children.Add(serialInput);

            stack.Children.Add(serialSection);
        }

        card.Child = stack;
        return card;
    }
}
