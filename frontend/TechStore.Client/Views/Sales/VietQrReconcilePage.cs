using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Sales;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.ViewModels.Sales;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Inventory.WorkspaceParts;

namespace TechStore.Client.Views.Sales;

/// <summary>Admin and Manager: confirm a VietQR payment by hand and reconcile bank money that did not complete a sale.</summary>
public sealed class VietQrReconcilePage : Page, IRoutePage
{
    public VietQrReconcileViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public VietQrReconcilePage(VietQrReconcileViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        this.Background(Theme(AppColors.Canvas));

        var pending = new PendingRows(ViewModel);
        pending.ItemsSource(x => x.Binding(() => ViewModel.Pending));
        var events = new EventRows(ViewModel);
        events.ItemsSource(x => x.Binding(() => ViewModel.Events));

        var message = Message(AppColors.Text);
        message.Text(x => x.Binding(() => ViewModel.ActionMessage));
        message.Visibility(x => x.Binding(() => ViewModel.HasActionMessage).Convert(ShowIf));
        var error = Message(AppColors.DangerText);
        error.Text(x => x.Binding(() => ViewModel.ActionError));
        error.Visibility(x => x.Binding(() => ViewModel.HasActionError).Convert(ShowIf));
        var noPending = EmptyText("Không có mã QR nào đang chờ.");
        noPending.Visibility(x => x.Binding(() => ViewModel.HasNoPending).Convert(ShowIf));
        var noEvents = EmptyText("Không có khoản tiền nào cần đối soát.");
        noEvents.Visibility(x => x.Binding(() => ViewModel.HasNoEvents).Convert(ShowIf));
        var confirmForm = BuildConfirmForm();
        confirmForm.Visibility(x => x.Binding(() => ViewModel.HasSelectedPayment).Convert(ShowIf));
        var resolveForm = BuildResolveForm();
        resolveForm.Visibility(x => x.Binding(() => ViewModel.HasSelectedEvent).Convert(ShowIf));

        var stack = new StackPanel
        {
            Spacing = 16,
            Padding = new Thickness(24),
            Children =
            {
                BuildHeader(),
                message,
                error,
                BuildSection(
                    "Mã QR đang chờ thanh toán",
                    "Chỉ xác nhận khi tiền đã về tài khoản: nhập mã tham chiếu và số tiền trên sao kê.",
                    pending,
                    noPending),
                confirmForm,
                BuildSection(
                    "Tiền cần đối soát",
                    "Tiền đến muộn, sai số tiền hoặc sai tài khoản không tự hoàn tất đơn. Xử lý rồi ghi chú lại.",
                    events,
                    noEvents),
                resolveForm
            }
        };
        Content = new ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled
        };
    }

    private UIElement BuildHeader()
    {
        var back = Button(
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { Text("←", 14, AppColors.SecondaryText, 600), Text("Đơn hàng", 14, AppColors.SecondaryText, 500) }
            },
            tooltip: "Quay lại danh sách đơn hàng");
        back.HorizontalAlignment = HorizontalAlignment.Left;
        back.Command = ViewModel.BackCommand;

        var refresh = Button(Text("Làm mới", 14, AppColors.Text, 600), tooltip: "Tải lại hai danh sách");
        refresh.Command = ViewModel.RefreshCommand;

        var title = new StackPanel
        {
            Spacing = 3,
            Children =
            {
                Text("Đối soát VietQR", 24, AppColors.Text, 600),
                Text("Xác nhận thanh toán thủ công và xử lý tiền chuyển khoản không khớp đơn.", 13, AppColors.SecondaryText)
            }
        };
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(title);
        Grid.SetColumn(refresh, 1);
        row.Children.Add(refresh);
        return new StackPanel { Spacing = 10, Children = { back, row } };
    }

    private static TextBlock Message(string color)
    {
        var block = Text("", 13, color);
        block.TextWrapping = TextWrapping.Wrap;
        return block;
    }

    private static TextBlock EmptyText(string text)
    {
        var empty = Text(text, 13, AppColors.SecondaryText);
        empty.Margin = new Thickness(16);
        return empty;
    }

    private static UIElement BuildSection(string title, string hint, UIElement rows, UIElement empty)
    {
        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = new StackPanel { Children = { rows, empty } }
        }
        .Background(Theme(AppColors.Surface))
        .BorderBrush(Theme(AppColors.Stroke));
        return new StackPanel
        {
            Spacing = 6,
            Children = { Text(title, 16, AppColors.Text, 600), Text(hint, 12, AppColors.SecondaryText), card }
        };
    }

    private Border BuildConfirmForm()
    {
        var summary = Text("", 13, AppColors.Text, 600);
        summary.TextWrapping = TextWrapping.Wrap;
        summary.Text(x => x.Binding(() => ViewModel.SelectedPaymentText));

        var reference = Field("Mã tham chiếu ngân hàng", "Ví dụ FT26112500123");
        reference.Text(x => x.Binding(() => ViewModel.ReferenceText).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        var amount = Field("Số tiền thực nhận (₫)", "");
        amount.Text(x => x.Binding(() => ViewModel.AmountText).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));

        var confirm = Button(Text("Xác nhận đã nhận tiền", 14, AppColors.OnPrimary, 600), primary: true, tooltip: "Hoàn tất đơn khi tiền đã về tài khoản");
        confirm.Command = ViewModel.ConfirmPaymentCommand;
        confirm.HorizontalAlignment = HorizontalAlignment.Left;

        return Form("Xác nhận thủ công", summary, reference, amount, confirm);
    }

    private Border BuildResolveForm()
    {
        var summary = Text("", 13, AppColors.Text, 600);
        summary.TextWrapping = TextWrapping.Wrap;
        summary.Text(x => x.Binding(() => ViewModel.SelectedEventText));

        var note = Field("Ghi chú xử lý (bắt buộc)", "Ví dụ: đã hoàn lại cho khách ngày 25/11");
        note.Text(x => x.Binding(() => ViewModel.NoteText).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged));
        note.MaxLength = VietQrReconcileViewModel.MaxNoteLength;

        var resolve = Button(Text("Đánh dấu đã xử lý", 14, AppColors.OnPrimary, 600), primary: true, tooltip: "Lưu ghi chú và đóng khoản này");
        resolve.Command = ViewModel.ResolveEventCommand;
        resolve.HorizontalAlignment = HorizontalAlignment.Left;

        return Form("Xử lý khoản tiền", summary, note, null, resolve);
    }

    private static TextBox Field(string label, string placeholder)
    {
        var box = new TextBox { Header = label, PlaceholderText = placeholder, Height = 64, MinWidth = 280 };
        AutomationProperties.SetName(box, label);
        return box;
    }

    private static Border Form(string title, TextBlock summary, TextBox first, TextBox? second, Button action)
    {
        var fields = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { first } };
        if (second is not null) fields.Children.Add(second);
        return new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = new StackPanel { Spacing = 10, Children = { Text(title, 15, AppColors.Text, 600), summary, fields, action } }
        }
        .Background(Theme(AppColors.Surface))
        .BorderBrush(Theme(AppColors.Stroke));
    }
}

/// <summary>Shared look of the two lists: plain rows made from text and one button each.</summary>
internal abstract class ReconcileRows : ItemsControl
{
    protected override DependencyObject GetContainerForItemOverride() => new ContentControl
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        IsTabStop = false,
        Padding = new Thickness(0)
    };

    protected abstract UIElement BuildRow(object item);

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ContentControl container) container.Content = BuildRow(item);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ContentControl container) container.Content = null;
        base.ClearContainerForItemOverride(element, item);
    }

    protected static UIElement Line(UIElement details, Button action)
    {
        var grid = new Grid { Padding = new Thickness(16, 12, 16, 12), ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(details);
        action.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(action, 1);
        grid.Children.Add(action);
        return new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) }.BorderBrush(Theme(AppColors.Stroke));
    }

    protected static TextBlock Wrapped(string text, double size, string color, ushort weight = 400)
    {
        var block = Text(text, size, color, weight);
        block.TextWrapping = TextWrapping.Wrap;
        return block;
    }
}

internal sealed class PendingRows(VietQrReconcileViewModel viewModel) : ReconcileRows
{
    protected override UIElement BuildRow(object item)
    {
        if (item is not VietQrPayment payment) return new Border();

        var left = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Wrapped($"{payment.OrderCode} · {OrderPresentation.FormatMoney(payment.Amount)}", 14, AppColors.Text, 600),
                Wrapped($"Mã chuyển khoản {payment.TransactionCode} · thu ngân {payment.CashierName ?? "—"} · hết hạn {OrderPresentation.FormatDateTime(payment.ExpiresAt)}", 12, AppColors.SecondaryText)
            }
        };
        var select = Button(Text("Xác nhận", 13, AppColors.Text, 600), tooltip: "Nhập mã tham chiếu để xác nhận đơn này");
        select.Command = viewModel.SelectPaymentCommand;
        select.CommandParameter = payment;
        return Line(left, select);
    }
}

internal sealed class EventRows(VietQrReconcileViewModel viewModel) : ReconcileRows
{
    protected override UIElement BuildRow(object item)
    {
        if (item is not VietQrBankEvent bankEvent) return new Border();

        var left = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Wrapped($"{OrderPresentation.FormatMoney(bankEvent.Amount)} · {VietQrPresentation.ReasonText(bankEvent.Reason)}", 14, AppColors.Text, 600),
                Wrapped($"{bankEvent.Provider} {bankEvent.ProviderTransactionId} · \"{bankEvent.TransferContent}\" · {OrderPresentation.FormatDateTime(bankEvent.TransferredAt)}"
                    + (bankEvent.OrderCode is null ? string.Empty : $" · đơn {bankEvent.OrderCode}"), 12, AppColors.SecondaryText)
            }
        };
        var select = Button(Text("Xử lý", 13, AppColors.Text, 600), tooltip: "Ghi chú cách xử lý khoản tiền này");
        select.Command = viewModel.SelectEventCommand;
        select.CommandParameter = bankEvent;
        return Line(left, select);
    }
}
