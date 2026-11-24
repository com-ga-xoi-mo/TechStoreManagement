using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Orders;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;
using static TechStore.Client.Views.Orders.OrderViewParts;

namespace TechStore.Client.Views.Inventory;

public sealed class ReturnInspectionsPage : Page, IRoutePage
{
    public ReturnInspectionsViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;

    public ReturnInspectionsPage(ReturnInspectionsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        var content = new StackPanel().Padding(24).Spacing(12).MaxWidth(820).HorizontalAlignment(HorizontalAlignment.Stretch);
        content.Children.Add(new TextBlock().Style(AppResources.GetStyle(AppTypography.HeadingText)).Foreground(AppColors.Brushes.Text).Text("Kiểm định hàng trả"));
        content.Children.Add(new Button().Content("Tải lại danh sách").Command(ViewModel.RefreshCommand));
        content.Children.Add(new TextBlock().Text(x => x.Binding(() => ViewModel.EmptyMessage)));
        content.Children.Add(new ComboBox().Header("Lô hàng / IMEI").PlaceholderText("Chọn lô hàng")
            .ItemsSource(x => x.Binding(() => ViewModel.Batches)).DisplayMemberPath("Label")
            .IsEnabled(x => x.Binding(() => ViewModel.CanChange))
            .SelectedItem(x => x.Binding(() => ViewModel.Selected).Mode(BindingMode.TwoWay)));
        content.Children.Add(new TextBlock().TextWrapping(TextWrapping.Wrap).IsTextSelectionEnabled(true).Text(x => x.Binding(() => ViewModel.Result)));
        content.Children.Add(new StackPanel().Spacing(8).Visibility(x => x.Binding(() => ViewModel.CanInspect).Convert((bool v) => ShowIf(v)))
            .Children(new TextBox().Header("Số lượng kiểm định").IsEnabled(x => x.Binding(() => ViewModel.CanEditQuantity)).Text(x => x.Binding(() => ViewModel.Quantity)
                    .Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                new Button().Content("Đạt · Nhập lại kho").Command(ViewModel.InspectCommand).CommandParameter("InStock"),
                new Button().Content("Hàng lỗi").Command(ViewModel.InspectCommand).CommandParameter("Defective"),
                new Button().Content("Cần sửa chữa").Command(ViewModel.InspectCommand).CommandParameter("UnderRepair")));
        content.Children.Add(new Button().Content("Thử lại lần kiểm định vừa gửi").Command(ViewModel.InspectCommand)
            .Visibility(x => x.Binding(() => ViewModel.NeedsRetry).Convert((bool v) => ShowIf(v))));
        content.Children.Add(new TextBlock().TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => ViewModel.Message)));
        this.IsEnabled(x => x.Binding(() => ViewModel.CanInteract)).Background(AppColors.Brushes.Canvas)
            .Content(new ScrollViewer().Content(content));
    }
}
