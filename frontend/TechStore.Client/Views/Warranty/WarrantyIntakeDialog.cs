using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Warranty;
using TechStore.Client.Views.Customers;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Warranty;

public sealed class WarrantyIntakeDialog : WarrantyDialog
{
    public WarrantyIntakeDialogViewModel ViewModel { get; }

    public WarrantyIntakeDialog(WarrantyIntakeDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        ViewModel.CaseChanged += OnCaseChanged;

        Content = new StackPanel().Spacing(10).Children(
            new ContentControl().HorizontalContentAlignment(HorizontalAlignment.Stretch)
                .Content(x => x.Binding(() => ViewModel.ShowIntake).Convert((bool next) => next ? BuildIntake() : BuildLookup())),
            new TextBlock().TextWrapping(TextWrapping.Wrap).Foreground(AppColors.Brushes.Text)
                .Text(x => x.Binding(() => ViewModel.LookupSummary)),
            new TextBlock().TextWrapping(TextWrapping.Wrap).Foreground(AppColors.Brushes.DangerText)
                .Text(x => x.Binding(() => ViewModel.ErrorMessage)));
    }

    private StackPanel BuildLookup() => Form(
        new TextBox().Header("IMEI hoặc số sê-ri").Text(x => x.Binding(() => ViewModel.Code).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
        new TextBox().Header("Mã đơn").Text(x => x.Binding(() => ViewModel.OrderCode).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
        new Button().Content("Chọn khách hàng").Command(ViewModel.ToggleCustomerPickerCommand),
        new ContentControl().HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .Content(x => x.Binding(() => ViewModel.ShowCustomerPicker).Convert((bool open) => BuildCustomerPicker(open))),
        new Button().Content("Kiểm tra bảo hành").Command(ViewModel.LookupCommand));

    private UIElement BuildCustomerPicker(bool open) => open ? new CustomerPickerView(ViewModel.CustomerPicker) : new Border();

    private StackPanel BuildIntake() => Form(
        new TextBox().Header("Lỗi khách báo").MaxLength(1000).Text(x => x.Binding(() => ViewModel.Issue).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
        new TextBox().Header("Tên liên hệ").MaxLength(100).Text(x => x.Binding(() => ViewModel.ContactName).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
        new TextBox().Header("Số điện thoại liên hệ").MaxLength(20).Text(x => x.Binding(() => ViewModel.ContactPhone).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
        new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
            new Button().Content("Quay lại").Command(ViewModel.BackCommand),
            new Button().Content("Tiếp nhận").Style(AppControlStyles.Styles.PrimaryButton).Command(ViewModel.SubmitCommand)));

    private StackPanel Form(params UIElement[] children) =>
        new StackPanel { DataContext = ViewModel }.Spacing(8).Children(children);

    protected override string DialogTitle => "Tiếp nhận bảo hành";
    protected override Task ActivateAsync() => ViewModel.ActivateAsync();
    protected override void Deactivate()
    {
        ViewModel.CaseChanged -= OnCaseChanged;
        ViewModel.Deactivate();
    }
    private void OnCaseChanged() => Hide();
}
