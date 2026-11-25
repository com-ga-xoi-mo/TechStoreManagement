using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Vouchers;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Vouchers;

/// <summary>Create or edit one voucher. In edit the code and the discount type are read-only.</summary>
public sealed class VoucherEditorDialog : ContentDialog
{
    public VoucherEditorDialogViewModel ViewModel { get; }

    public VoucherEditorDialog(VoucherEditorDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        DefaultButton = ContentDialogButton.None;
        PrimaryButtonText = string.Empty;
        SecondaryButtonText = string.Empty;
        CloseButtonText = string.Empty;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(8);
        Width = 520;
        MaxWidth = 520;
        Resources["ContentDialogMaxWidth"] = 520d;
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
        var root = new Grid { Width = 520, MaxWidth = 520 }.Background(Theme(AppColors.Surface));
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(BuildHeader());

        var scroll = new ScrollViewer
        {
            MaxHeight = 620,
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
            Padding = new Thickness(16, 12, 12, 12),
            BorderThickness = new Thickness(0, 0, 0, 1)
        }.BorderBrush(Theme(AppColors.Stroke));
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = Text("", 16, AppColors.Text, 600);
        title.Text(x => x.Binding(() => ViewModel.Title));
        header.Children.Add(title);

        var close = Button(Icon("close", AppColors.SecondaryText, 20), tooltip: "Đóng");
        close.Command(x => x.Binding(() => ViewModel.CancelCommand));
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        return header;
    }

    private UIElement BuildBody()
    {
        var body = new StackPanel { Padding = new Thickness(16), Spacing = 10 };

        body.Children.Add(Label("Mã voucher", required: true));
        var code = TextInput("Ví dụ: SALE10, TET-2027...");
        code.Text(x => x.Binding(() => ViewModel.Code).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        code.IsReadOnly(x => x.Binding(() => ViewModel.IsEdit));
        body.Children.Add(code);
        body.Children.Add(ErrorLabel(nameof(VoucherEditorDialogViewModel.CodeError)));

        body.Children.Add(Label("Mô tả"));
        var description = TextInput("Ghi chú cho nhân viên (tùy chọn)...");
        description.MinHeight = 60;
        description.AcceptsReturn = true;
        description.TextWrapping = TextWrapping.Wrap;
        description.Text(x => x.Binding(() => ViewModel.Description).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        body.Children.Add(description);
        body.Children.Add(ErrorLabel(nameof(VoucherEditorDialogViewModel.DescriptionError)));

        body.Children.Add(Label("Loại giảm giá", required: true));
        var type = new ComboBox
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            FontSize = 13,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 32, 6),
            ItemsSource = VoucherEditorDialogViewModel.TypeOptions,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
        .FontFamily(AppTypography.Fonts.RegularFont)
        .Background(Theme(AppColors.Surface))
        .Foreground(Theme(AppColors.Text))
        .BorderBrush(Theme(AppColors.Stroke));
        type.SelectedIndex(x => x.Binding(() => ViewModel.DiscountTypeIndex).Mode(BindingMode.TwoWay));
        type.IsEnabled(x => x.Binding(() => ViewModel.IsTypeEditable));
        body.Children.Add(type);

        var valueLabel = Text("", 13, AppColors.Text, 600);
        valueLabel.Text(x => x.Binding(() => ViewModel.IsPercentage)
            .Convert((bool percent) => percent ? "Giá trị giảm (%)" : "Giá trị giảm (₫)"));
        body.Children.Add(valueLabel);
        var value = TextInput("Ví dụ: 10 hoặc 500.000");
        value.Text(x => x.Binding(() => ViewModel.DiscountValue).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        body.Children.Add(value);
        body.Children.Add(ErrorLabel(nameof(VoucherEditorDialogViewModel.DiscountValueError)));

        body.Children.Add(Label("Đơn tối thiểu (₫)"));
        var min = TextInput("Để trống nếu không yêu cầu");
        min.Text(x => x.Binding(() => ViewModel.MinOrderAmount).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        body.Children.Add(min);
        body.Children.Add(ErrorLabel(nameof(VoucherEditorDialogViewModel.MinOrderAmountError)));

        var capBlock = new StackPanel { Spacing = 10 };
        capBlock.Children.Add(Label("Giảm tối đa (₫)"));
        var max = TextInput("Để trống nếu không giới hạn");
        max.Text(x => x.Binding(() => ViewModel.MaxDiscountAmount).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        capBlock.Children.Add(max);
        capBlock.Children.Add(ErrorLabel(nameof(VoucherEditorDialogViewModel.MaxDiscountAmountError)));
        capBlock.Visibility(x => x.Binding(() => ViewModel.ShowMaxDiscount).Convert(ShowIf));
        body.Children.Add(capBlock);

        body.Children.Add(Label("Số lượt dùng tối đa", required: true));
        var limit = TextInput("Ví dụ: 100");
        limit.Text(x => x.Binding(() => ViewModel.UsageLimit).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        body.Children.Add(limit);
        body.Children.Add(ErrorLabel(nameof(VoucherEditorDialogViewModel.UsageLimitError)));

        body.Children.Add(Label("Dùng được đến hết ngày", required: true));
        var expiry = new CalendarDatePicker
        {
            PlaceholderText = "Chọn ngày hết hạn",
            DateFormat = "{day.integer(2)}/{month.integer(2)}/{year.full}",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        expiry.Date(x => x.Binding(() => ViewModel.ExpiresAt).Mode(BindingMode.TwoWay));
        body.Children.Add(expiry);
        body.Children.Add(ErrorLabel(nameof(VoucherEditorDialogViewModel.ExpiresAtError)));

        var formError = Text("", 12, AppColors.DangerText);
        formError.TextWrapping = TextWrapping.Wrap;
        formError.Text(x => x.Binding(() => ViewModel.FormError));
        formError.Visibility(x => x.Binding(() => ViewModel.HasFormError).Convert(ShowIf));
        body.Children.Add(formError);
        return body;
    }

    private static TextBox TextInput(string placeholder) =>
        new TextBox
        {
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

    private static StackPanel Label(string label, bool required = false)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Children = { Text(label, 13, AppColors.Text, 600) } };
        if (!required) return panel;
        var star = Text("*", 13, AppColors.DangerText, 700);
        star.Margin = new Thickness(3, 0, 0, 0);
        panel.Children.Add(star);
        return panel;
    }

    // The binding path comes from the text of the lambda, so a Func passed through a variable would bind to "getter".
    // The property name is passed as a string instead.
    private static TextBlock ErrorLabel(string propertyName)
    {
        var error = Text("", 12, AppColors.DangerText);
        error.TextWrapping = TextWrapping.Wrap;
        error.Text(x => x.Binding(propertyName));
        error.Visibility(x => x.Binding(propertyName).Convert((string? err) => ShowIf(!string.IsNullOrWhiteSpace(err))));
        return error;
    }

    private Grid BuildFooter()
    {
        var footer = new Grid
        {
            Padding = new Thickness(16, 12, 16, 12),
            BorderThickness = new Thickness(0, 1, 0, 0)
        }
        .Background(Theme(AppColors.Subtle))
        .BorderBrush(Theme(AppColors.Stroke));
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cancel = Button(Text("Hủy", 14, AppColors.SecondaryText, 500));
        cancel.Height = 36;
        cancel.Padding = new Thickness(16, 0, 16, 0);
        cancel.Command(x => x.Binding(() => ViewModel.CancelCommand));

        var saveText = Text("", 14, AppColors.OnPrimary, 500);
        saveText.Text(x => x.Binding(() => ViewModel.SaveButtonText));
        var save = Button(saveText, primary: true);
        save.Height = 36;
        save.Padding = new Thickness(20, 0, 20, 0);
        save.Command(x => x.Binding(() => ViewModel.SaveCommand));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { cancel, save } };
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        return footer;
    }
}
