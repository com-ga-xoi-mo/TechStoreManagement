using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Warranty;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Warranty.WarrantyCaseListPage;

namespace TechStore.Client.Views.Warranty;

public sealed class WarrantyCaseDetailDialog : WarrantyDialog
{
    public WarrantyCaseDetailDialogViewModel ViewModel { get; }

    public WarrantyCaseDetailDialog(WarrantyCaseDetailDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        Content = new StackPanel().MinWidth(420).Spacing(8).Children(
            new TextBlock().TextWrapping(TextWrapping.Wrap).Foreground(AppColors.Brushes.Text).Text(x => x.Binding(() => ViewModel.Summary)),
            new ContentControl().Content(x => x.Binding(() => ViewModel.ActionForm).Convert((WarrantyCaseDetailDialogViewModel.FormKind form) => BuildForm(form))),
            new TextBlock().TextWrapping(TextWrapping.Wrap).Foreground(AppColors.Brushes.DangerText).Text(x => x.Binding(() => ViewModel.OperationMessage)),
            new StackPanel().Spacing(6).Visibility(x => x.Binding(() => ViewModel.IsError).Convert((bool v) => Visible(v))).Children(
                new TextBlock().TextWrapping(TextWrapping.Wrap).Foreground(AppColors.Brushes.DangerText).Text(x => x.Binding(() => ViewModel.ErrorMessage)),
                new Button().Content("Thử lại").Command(ViewModel.RetryCommand)));
    }

    private object BuildForm(WarrantyCaseDetailDialogViewModel.FormKind form) => form switch
    {
        WarrantyCaseDetailDialogViewModel.FormKind.Inspection => BuildInspection(),
        WarrantyCaseDetailDialogViewModel.FormKind.Repair => Form(NoteBox(), new Button().Content("Báo sửa xong").Command(ViewModel.RepairedCommand)),
        WarrantyCaseDetailDialogViewModel.FormKind.Replacement => BuildReplacement(),
        WarrantyCaseDetailDialogViewModel.FormKind.Return => new Button().Content("Trả máy cho khách").Command(ViewModel.ReturnCommand),
        _ => new Border()
    };

    private StackPanel BuildInspection() => Form(
        new ComboBox().Header("Kết luận kiểm định").ItemsSource(ViewModel.Outcomes).DisplayMemberPath("Label")
            .SelectedItem(x => x.Binding(() => ViewModel.SelectedOutcome).Mode(BindingMode.TwoWay)),
        new TextBox().Header("Lý do từ chối").MaxLength(500)
            .Visibility(x => x.Binding(() => ViewModel.IsRejection).Convert((bool v) => Visible(v)))
            .Text(x => x.Binding(() => ViewModel.RejectionReason).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
        NoteBox(),
        new Button().Content("Lưu kiểm định").Command(ViewModel.InspectCommand));

    private StackPanel BuildReplacement() => Form(
        NoteBox(),
        new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
            new Button().Content("Chọn máy thay").Command(ViewModel.FindReplacementsCommand),
            new Button().Content("Từ chối đổi").Command(ViewModel.RejectReplacementCommand)),
        new StackPanel().Spacing(8).Visibility(x => x.Binding(() => ViewModel.ShowReplacements).Convert((bool v) => Visible(v))).Children(
            new ComboBox().Header("Máy thay còn trong kho").ItemsSource(x => x.Binding(() => ViewModel.Replacements)).DisplayMemberPath("Code")
                .SelectedItem(x => x.Binding(() => ViewModel.SelectedReplacement).Mode(BindingMode.TwoWay)),
            new TextBlock().TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => ViewModel.ReplacementMessage)),
            new Button().Content("Duyệt đổi").IsEnabled(x => x.Binding(() => ViewModel.CanReplace)).Command(ViewModel.ReplaceCommand)));

    private StackPanel Form(params UIElement[] children) =>
        new StackPanel { DataContext = ViewModel }.Spacing(8).Children(children);

    private TextBox NoteBox() => new TextBox().Header("Ghi chú").MaxLength(1000)
        .Text(x => x.Binding(() => ViewModel.Note).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));

    protected override string DialogTitle => "Chi tiết hồ sơ bảo hành";
    protected override Task ActivateAsync() => ViewModel.ActivateAsync();
    protected override void Deactivate() => ViewModel.Deactivate();
}
