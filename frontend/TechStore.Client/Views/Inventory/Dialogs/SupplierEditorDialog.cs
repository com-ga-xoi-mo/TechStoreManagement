using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Inventory;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Inventory.Dialogs;

public sealed class SupplierEditorDialog : ContentDialog
{
    public SupplierEditorDialogViewModel ViewModel { get; }

    public SupplierEditorDialog(SupplierEditorDialogViewModel viewModel)
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
        Width = 500;
        MaxWidth = 500;
        Resources["ContentDialogMaxWidth"] = 500d;
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
        var root = new Grid { Width = 500, MaxWidth = 500 }.Background(Theme(AppColors.Surface));
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
        var body = new StackPanel { Padding = new Thickness(16), Spacing = 12 };

        // Name
        body.Children.Add(RequiredLabel("Tên nhà cung cấp"));
        var nameInput = CreateTextInput("Nhập tên nhà cung cấp...");
        nameInput.Text(x => x.Binding(() => ViewModel.Name).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        body.Children.Add(nameInput);
        body.Children.Add(CreateErrorLabel(() => ViewModel.NameError));

        // Contact name
        body.Children.Add(Text("Người liên hệ", 13, AppColors.Text, 600));
        var contactInput = CreateTextInput("Nhập tên người đại diện / liên hệ (tùy chọn)...");
        contactInput.Text(x => x.Binding(() => ViewModel.ContactName).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        body.Children.Add(contactInput);
        body.Children.Add(CreateErrorLabel(() => ViewModel.ContactNameError));

        // Phone
        body.Children.Add(RequiredLabel("Số điện thoại"));
        var phoneInput = CreateTextInput("Ví dụ: 028 7300 7300 hoặc +84 901 234 567...");
        phoneInput.Text(x => x.Binding(() => ViewModel.Phone).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        body.Children.Add(phoneInput);
        body.Children.Add(CreateErrorLabel(() => ViewModel.PhoneError));

        // Email
        body.Children.Add(Text("Email", 13, AppColors.Text, 600));
        var emailInput = CreateTextInput("Ví dụ: sales@supplier.vn (tùy chọn)...");
        emailInput.Text(x => x.Binding(() => ViewModel.Email).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        body.Children.Add(emailInput);
        body.Children.Add(CreateErrorLabel(() => ViewModel.EmailError));

        // Address
        body.Children.Add(Text("Địa chỉ", 13, AppColors.Text, 600));
        var addressInput = CreateTextInput("Nhập địa chỉ trụ sở / kho hàng (tùy chọn)...");
        addressInput.MinHeight = 60;
        addressInput.AcceptsReturn = true;
        addressInput.TextWrapping = TextWrapping.Wrap;
        addressInput.Text(x => x.Binding(() => ViewModel.Address).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        body.Children.Add(addressInput);
        body.Children.Add(CreateErrorLabel(() => ViewModel.AddressError));

        // General error banner
        var generalError = Text("", 12, AppColors.DangerText);
        generalError.TextWrapping = TextWrapping.Wrap;
        generalError.Text(x => x.Binding(() => ViewModel.GeneralError));
        generalError.Visibility(x => x.Binding(() => ViewModel.GeneralError).Convert((string? err) => ShowIf(!string.IsNullOrWhiteSpace(err))));
        body.Children.Add(generalError);

        return body;
    }

    private static TextBox CreateTextInput(string placeholder) =>
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

    private TextBlock CreateErrorLabel(Func<string?> getter)
    {
        var error = Text("", 12, AppColors.DangerText);
        error.Text(x => x.Binding(getter));
        error.Visibility(x => x.Binding(getter).Convert((string? err) => ShowIf(!string.IsNullOrWhiteSpace(err))));
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
        saveText.Text(x => x.Binding(() => ViewModel.PrimaryButtonText));

        var save = Button(saveText, primary: true);
        save.Height = 36;
        save.Padding = new Thickness(20, 0, 20, 0);
        save.Command(x => x.Binding(() => ViewModel.SaveCommand));
        save.IsEnabled(x => x.Binding(() => ViewModel.IsBusy).Convert((bool busy) => !busy));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { cancel, save }
        };
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);

        return footer;
    }
}
