using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Models.Warranty;
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
        Content = new StackPanel().Spacing(12).Children(
            new ProgressBar().IsIndeterminate(true)
                .Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert((bool loading) => Visible(loading))),
            new ContentControl().HorizontalContentAlignment(HorizontalAlignment.Stretch)
                .Content(x => x.Binding(() => ViewModel.Details).Convert((WarrantyDetails? details) => BuildDetails(details))),
            new TextBlock().TextWrapping(TextWrapping.Wrap).Foreground(AppColors.Brushes.DangerText)
                .Text(x => x.Binding(() => ViewModel.OperationMessage))
                .Visibility(x => x.Binding(() => ViewModel.OperationMessage).Convert((string message) => Visible(!string.IsNullOrWhiteSpace(message)))),
            new StackPanel().Spacing(6).Visibility(x => x.Binding(() => ViewModel.IsError).Convert((bool v) => Visible(v))).Children(
                new TextBlock().TextWrapping(TextWrapping.Wrap).Foreground(AppColors.Brushes.DangerText).Text(x => x.Binding(() => ViewModel.ErrorMessage)),
                new Button().Content("Thử lại").Command(ViewModel.RetryCommand)));
    }

    private UIElement BuildDetails(WarrantyDetails? item)
    {
        if (item is null) return new Grid();

        var row = item.Row;
        var product = string.IsNullOrWhiteSpace(item.VariantName) ? row.Product : $"{row.Product} · {item.VariantName}";
        var processing = new StackPanel().Spacing(8).Children(
            Field("Lỗi khách báo", item.Issue),
            Field("Kết luận kiểm định", item.Inspection));
        if (!string.IsNullOrWhiteSpace(item.Rejection))
            processing.Children.Add(Field("Lý do từ chối", item.Rejection));
        if (!string.IsNullOrWhiteSpace(item.Replacement))
            processing.Children.Add(Field("IMEI / Sê-ri máy thay", item.Replacement));
        if (ViewModel.HasCost)
            processing.Children.Add(Field("Giá vốn máy thay", ViewModel.CostLabel));

        var details = new StackPanel().Spacing(14).Children(
            new Grid().ColumnDefinitions("*, Auto").ColumnSpacing(12).Children(
                new TextBlock().Text(row.Code).Style(AppResources.GetStyle(AppTypography.HeadingText))
                    .Foreground(AppColors.Brushes.Text),
                new StatusBadge().Grid(column: 1).VerticalAlignment(VerticalAlignment.Center)
                    .Label(row.StatusLabel).Tone(row.StatusTone)),
            Section("Thiết bị và đơn hàng", new StackPanel().Spacing(8).Children(
                Field("Sản phẩm", product),
                Field("IMEI / Sê-ri", row.Device),
                Field("Đơn gốc", item.OrderCode),
                Field("Ngày mua", WarrantyLabels.Date(item.PurchasedAt)),
                Field("Hạn bảo hành", $"{WarrantyLabels.Date(item.WarrantyStartAt)} đến {WarrantyLabels.Date(item.WarrantyEndAt)}"))),
            Section("Liên hệ", new StackPanel().Spacing(8).Children(
                Field("Tên liên hệ", item.ContactName),
                Field("Số điện thoại", item.ContactPhone))),
            Section("Xử lý bảo hành", processing));

        if (BuildForm(ViewModel.ActionForm) is { } form)
            details.Children.Add(Section("Thao tác", form));
        if (item.Events.Count > 0)
            details.Children.Add(Section("Lịch sử", BuildHistory(item.Events)));
        return details;
    }

    private static Border Section(string title, UIElement content) => new Border()
        .BorderBrush(AppColors.Brushes.Stroke).BorderThickness(1).CornerRadius(AppMetrics.Values.ControlRadius)
        .Padding(14).Child(new StackPanel().Spacing(10).Children(
            new TextBlock().Text(title).Style(AppResources.GetStyle(AppTypography.SectionText)).Foreground(AppColors.Brushes.Text),
            content));

    private static Grid Field(string label, string? value) => new Grid().ColumnDefinitions("138, *").ColumnSpacing(12).Children(
        new TextBlock().Text(label).TextWrapping(TextWrapping.Wrap).Foreground(AppColors.Brushes.SecondaryText),
        new TextBlock().Grid(column: 1).Text(string.IsNullOrWhiteSpace(value) ? "—" : value).TextWrapping(TextWrapping.Wrap)
            .Foreground(AppColors.Brushes.Text).IsTextSelectionEnabled(true));

    private static StackPanel BuildHistory(IReadOnlyList<WarrantyEvent> events)
    {
        var history = new StackPanel();
        for (var index = 0; index < events.Count; index++)
        {
            var entry = events[index];
            var row = new StackPanel().Spacing(4).Children(
                new TextBlock().Text(WarrantyLabels.Status(entry.To)).Foreground(AppColors.Brushes.Text)
                    .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold).TextWrapping(TextWrapping.Wrap),
                new TextBlock().Text($"{WarrantyLabels.Date(entry.At)} · {entry.Actor}")
                    .Style(AppResources.GetStyle(AppTypography.CaptionText)).Foreground(AppColors.Brushes.SecondaryText)
                    .TextWrapping(TextWrapping.Wrap));
            if (!string.IsNullOrWhiteSpace(entry.Note))
                row.Children.Add(new TextBlock().Text(entry.Note).Foreground(AppColors.Brushes.Text).TextWrapping(TextWrapping.Wrap));
            history.Children.Add(new Border().BorderBrush(AppColors.Brushes.Stroke)
                .BorderThickness(new Thickness(0, index == 0 ? 0 : 1, 0, 0))
                .Padding(new Thickness(0, 8, 0, 8)).Child(row));
        }
        return history;
    }

    private UIElement? BuildForm(WarrantyCaseDetailDialogViewModel.FormKind form) => form switch
    {
        WarrantyCaseDetailDialogViewModel.FormKind.Inspection => BuildInspection(),
        WarrantyCaseDetailDialogViewModel.FormKind.Repair => Form(NoteBox(), new Button().Content("Báo sửa xong").Command(ViewModel.RepairedCommand)),
        WarrantyCaseDetailDialogViewModel.FormKind.Replacement => BuildReplacement(),
        WarrantyCaseDetailDialogViewModel.FormKind.Return => new Button().Content("Trả máy cho khách").Command(ViewModel.ReturnCommand),
        _ => null
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
