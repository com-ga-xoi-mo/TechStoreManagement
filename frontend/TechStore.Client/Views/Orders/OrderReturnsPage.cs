using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Orders;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Orders.OrderViewParts;

namespace TechStore.Client.Views.Orders;

public sealed class OrderReturnsPage : Page, IRoutePage
{
    public OrderReturnsViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public OrderReturnsPage(OrderReturnsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        var content = new StackPanel().Padding(24).Spacing(12).MaxWidth(820).HorizontalAlignment(HorizontalAlignment.Stretch);
        content.Children.Add(new Button().Content("← Đơn hàng").Command(ViewModel.BackCommand));
        content.Children.Add(new TextBlock().Style(AppResources.GetStyle(AppTypography.HeadingText)).Foreground(AppColors.Brushes.Text).Text("Trả hàng và hoàn tiền"));
        content.Children.Add(new TextBlock().Text(x => x.Binding(() => ViewModel.OrderCode)));
        content.Children.Add(new Button().Content("Tải lại").Command(ViewModel.RefreshCommand));
        content.Children.Add(new StackPanel().Spacing(8)
            .Children(new ItemsControl().IsEnabled(x => x.Binding(() => ViewModel.CanEditDraft)).ItemsSource(x => x.Binding(() => ViewModel.Lines))
                .ItemTemplate<ReturnDraftLine>(line => new StackPanel().Spacing(3).Margin(0, 4, 0, 4)
                    .Children(
                        new TextBlock().TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => line.Label)),
                        new TextBox().Header("Số lượng trả").PlaceholderText("0")
                            .Text(x => x.Binding(() => line.Quantity).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                        new TextBlock().Foreground(AppColors.Brushes.DangerText).Text(x => x.Binding(() => line.Error)))),
                new TextBox().IsEnabled(x => x.Binding(() => ViewModel.CanEditDraft)).Header("Lý do trả hàng").Text(x => x.Binding(() => ViewModel.Reason)
                    .Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged))));
        content.Children.Add(new TextBlock().TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => ViewModel.ReviewReason)));
        content.Children.Add(new Button().Content("Gửi yêu cầu").Command(ViewModel.SubmitCommand).IsEnabled(x => x.Binding(() => ViewModel.CanSubmit)));
        content.Children.Add(new TextBlock().TextWrapping(TextWrapping.Wrap).IsTextSelectionEnabled(true).Text(x => x.Binding(() => ViewModel.LegacyHistory)));
        content.Children.Add(new ComboBox().Header("Lịch sử trả hàng").PlaceholderText("Chưa có yêu cầu")
            .ItemsSource(x => x.Binding(() => ViewModel.History)).DisplayMemberPath("Label")
            .SelectedItem(x => x.Binding(() => ViewModel.Selected).Mode(BindingMode.TwoWay)));
        content.Children.Add(new TextBlock().TextWrapping(TextWrapping.Wrap).IsTextSelectionEnabled(true).Text(x => x.Binding(() => ViewModel.Details)));
        if (ViewModel.IsManager)
            content.Children.Add(new StackPanel().Spacing(8)
                .Visibility(x => x.Binding(() => ViewModel.IsManager).Convert((bool v) => ShowIf(v)))
                .Children(new TextBox().Header("Số tiền đầu ca (₫)").Text(x => x.Binding(() => ViewModel.OpeningCash)
                        .Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                    new Button().Content("Mở ca hoàn tiền của tôi").Command(ViewModel.OpenRefundShiftCommand)));
        if (ViewModel.CanManage)
            content.Children.Add(new StackPanel().Spacing(8).Orientation(Orientation.Horizontal)
                .Visibility(x => x.Binding(() => ViewModel.ShowApproval).Convert((bool v) => ShowIf(v)))
                .Children(new Button().Content("Duyệt chi tiền mặt").Command(ViewModel.SettleCommand).CommandParameter("Cash"),
                    new Button().Content("Hoàn qua ngân hàng").Command(ViewModel.SettleCommand).CommandParameter("VietQr")));
        if (ViewModel.CanManage)
            content.Children.Add(new StackPanel().Spacing(8)
                .Visibility(x => x.Binding(() => ViewModel.ShowReconciliation).Convert((bool v) => ShowIf(v)))
                .Children(new TextBox().Header("Mã chứng từ ngân hàng").Text(x => x.Binding(() => ViewModel.BankReference)
                        .Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                    new TextBox().Header("Số tiền thực chi (₫)").Text(x => x.Binding(() => ViewModel.BankAmount)
                        .Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                    new TextBox().Header("Ngày giờ duyệt chi theo chứng từ gốc (phiếu cũ)")
                        .Visibility(x => x.Binding(() => ViewModel.NeedsLegacyApprovalTime).Convert((bool v) => ShowIf(v)))
                        .Text(x => x.Binding(() => ViewModel.LegacyApprovalTime).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                    new TextBox().Header("Ngày giờ thực chi").PlaceholderText("24/11/2026 14:35:12,125 hoặc 2026-11-24T14:35:12.125+07:00")
                        .Text(x => x.Binding(() => ViewModel.BankPaidTime).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                    new Button().Content("Xác nhận ngân hàng đã chi").Command(ViewModel.ReconcileCommand).CommandParameter("Paid"),
                    new Button().Content("Xác nhận ngân hàng từ chối").Command(ViewModel.ReconcileCommand).CommandParameter("Failed")));
        if (ViewModel.CanCheckout)
            content.Children.Add(new Button().Content("Lập đơn đổi tại quầy").Command(ViewModel.ExchangeCommand)
                .Visibility(x => x.Binding(() => ViewModel.ShowCheckout).Convert((bool v) => ShowIf(v))));
        if (ViewModel.CanManage)
            content.Children.Add(new StackPanel().Spacing(8)
                .Visibility(x => x.Binding(() => ViewModel.ShowLink).Convert((bool v) => ShowIf(v)))
                .Children(new TextBox().Header("ID đơn đổi đã thanh toán").Text(x => x.Binding(() => ViewModel.ReplacementOrder)
                        .Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                    new Button().Content("Liên kết đơn đổi").Command(ViewModel.LinkCommand)));
        content.Children.Add(new TextBlock().TextWrapping(TextWrapping.Wrap).IsTextSelectionEnabled(true).Text(x => x.Binding(() => ViewModel.Message)));
        this.IsEnabled(x => x.Binding(() => ViewModel.CanInteract)).Background(AppColors.Brushes.Canvas)
            .Content(new ScrollViewer().Content(content));
    }
}
