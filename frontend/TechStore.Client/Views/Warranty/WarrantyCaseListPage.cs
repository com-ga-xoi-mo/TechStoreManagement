using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Models.Warranty;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.ViewModels.Warranty;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Warranty;

public sealed class WarrantyCaseListPage : Page, IRoutePage
{
    public WarrantyCaseListViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;
    private const double TableMinWidth = 940;
    private readonly IServiceProvider _services;
    private WarrantyDialog? _dialog;

    public WarrantyCaseListPage(WarrantyCaseListViewModel viewModel, IServiceProvider services)
    {
        ViewModel = viewModel;
        _services = services;
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            ViewModel.IntakeRequested += ShowIntake;
            ViewModel.CaseDetailRequested += ShowDetail;
        };
        Unloaded += (_, _) =>
        {
            ViewModel.IntakeRequested -= ShowIntake;
            ViewModel.CaseDetailRequested -= ShowDetail;
            _dialog?.Hide();
        };

        this.Background(AppColors.Brushes.Canvas).Content(new ScrollViewer().Content(
            new StackPanel().Padding(24).Spacing(12).Children(
                new Grid().ColumnDefinitions("*, Auto").Children(
                    new TextBlock().Text("Bảo hành").Style(AppResources.GetStyle(AppTypography.HeadingText)).Foreground(AppColors.Brushes.Text),
                    new Button().Grid(column: 1).Content("Tiếp nhận").Style(AppControlStyles.Styles.PrimaryButton)
                        .Background(AppColors.Brushes.Primary).Foreground(AppColors.Brushes.OnPrimary)
                        .Command(ViewModel.IntakeCommand).Visibility(Visible(ViewModel.CanIntake))),
                new Grid().ColumnDefinitions("*, Auto, Auto").ColumnSpacing(12).Children(
                    new TextBox().PlaceholderText("Mã hồ sơ, IMEI, mã đơn hoặc số điện thoại")
                        .Text(x => x.Binding(() => ViewModel.Query).Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)),
                    new ComboBox().Grid(column: 1).ItemsSource(ViewModel.Filters).DisplayMemberPath(nameof(WarrantyStatusFilter.Label))
                        .SelectedItem(x => x.Binding(() => ViewModel.SelectedFilter).Mode(BindingMode.TwoWay)),
                    new Button().Grid(column: 2).Content("Tìm kiếm").Command(ViewModel.ApplyFilterCommand)),
                new ProgressBar().IsIndeterminate(true).Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert((bool v) => Visible(v))),
                new TextBlock().Text("Không tìm thấy hồ sơ bảo hành").Foreground(AppColors.Brushes.SecondaryText)
                    .Visibility(x => x.Binding(() => ViewModel.IsEmpty).Convert((bool v) => Visible(v))),
                new StackPanel().Spacing(8).Visibility(x => x.Binding(() => ViewModel.IsError).Convert((bool v) => Visible(v)))
                    .Children(new TextBlock().Foreground(AppColors.Brushes.DangerText).Text(x => x.Binding(() => ViewModel.ErrorMessage)),
                        new Button().Content("Thử lại").Command(ViewModel.RetryCommand)),
                new StackPanel().Spacing(8).Visibility(x => x.Binding(() => ViewModel.IsContent).Convert((bool v) => Visible(v))).Children(
                    BuildTable(),
                    new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                        new Button().Content("Trang trước").Command(ViewModel.PreviousCommand),
                        new TextBlock().Foreground(AppColors.Brushes.SecondaryText)
                            .VerticalAlignment(VerticalAlignment.Center).Text(x => x.Binding(() => ViewModel.PageLabel)),
                        new Button().Content("Trang sau").Command(ViewModel.NextCommand))))));
    }

    private ScrollViewer BuildTable() => new ScrollViewer()
        .HorizontalScrollMode(ScrollMode.Enabled).HorizontalScrollBarVisibility(ScrollBarVisibility.Auto)
        .VerticalScrollMode(ScrollMode.Disabled).VerticalScrollBarVisibility(ScrollBarVisibility.Disabled)
        .HorizontalContentAlignment(HorizontalAlignment.Stretch)
        .Content(new Border().MinWidth(TableMinWidth).Background(AppColors.Brushes.Surface)
            .BorderBrush(AppColors.Brushes.Stroke).BorderThickness(1).CornerRadius(AppMetrics.Values.CardRadius)
            .Child(new StackPanel().Children(
                new Border().Background(AppColors.Brushes.Subtle).BorderBrush(AppColors.Brushes.Stroke)
                    .BorderThickness(new Thickness(0, 0, 0, 1)).Padding(new Thickness(14, 10, 14, 10))
                    .Child(TableColumns().Children(
                        Header("Mã hồ sơ"), Header("Sản phẩm").Grid(column: 1),
                        Header("IMEI / Sê-ri").Grid(column: 2), Header("Khách hàng").Grid(column: 3),
                        Header("Trạng thái").Grid(column: 4), Header("Ngày tiếp nhận").Grid(column: 5))),
                new ItemsControl().ItemsSource(x => x.Binding(() => ViewModel.Rows)).ItemTemplate<WarrantyRow>(BuildRow))));

    private Border BuildRow(WarrantyRow row) => new Border()
        .BorderBrush(AppColors.Brushes.Stroke).BorderThickness(new Thickness(0, 0, 0, 1))
        .Child(new Button().Style(AppControlStyles.Styles.QuietButton).CornerRadius(0).BorderThickness(0)
            .Padding(new Thickness(14, 10, 14, 10)).MinHeight(48)
            .Command(ViewModel.OpenCaseCommand).CommandParameter(x => x.Binding(() => row))
            .HorizontalAlignment(HorizontalAlignment.Stretch).HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .Content(TableColumns().Children(
                Cell().FontWeight(Microsoft.UI.Text.FontWeights.SemiBold).Text(x => x.Binding(() => row.Code)),
                Cell().Grid(column: 1).Text(x => x.Binding(() => row.Product)),
                Cell().Grid(column: 2).Text(x => x.Binding(() => row.Device)),
                Cell().Grid(column: 3).Text(x => x.Binding(() => row.DisplayCustomer)),
                new StatusBadge().Grid(column: 4).VerticalAlignment(VerticalAlignment.Center)
                    .Label(x => x.Binding(() => row.StatusLabel)).Tone(x => x.Binding(() => row.StatusTone)),
                Cell().Grid(column: 5).Foreground(AppColors.Brushes.SecondaryText).Text(x => x.Binding(() => row.OpenedLabel)))));

    private static Grid TableColumns() => new Grid().ColumnDefinitions("130, *, 148, 132, 154, 140").ColumnSpacing(12);
    private static TextBlock Cell() => new TextBlock().Style(AppResources.GetStyle(AppTypography.BodyText))
        .Foreground(AppColors.Brushes.Text).VerticalAlignment(VerticalAlignment.Center).TextTrimming(TextTrimming.CharacterEllipsis);
    private static TextBlock Header(string label) => new TextBlock().Text(label).Style(AppResources.GetStyle(AppTypography.CaptionText))
        .Foreground(AppColors.Brushes.SecondaryText).FontWeight(Microsoft.UI.Text.FontWeights.SemiBold);

    private async void ShowIntake()
    {
        if (_dialog != null || XamlRoot is null) return;
        var dialog = _services.GetRequiredService<WarrantyIntakeDialog>();
        _dialog = dialog;
        try
        {
            await dialog.ShowAsync(XamlRoot, ActualTheme);
            if (dialog.ViewModel.Succeeded && IsLoaded) await ViewModel.RefreshAfterIntakeAsync();
        }
        finally { _dialog = null; }
    }

    private async void ShowDetail(Guid id)
    {
        if (_dialog != null || XamlRoot is null) return;
        var dialog = _services.GetRequiredService<WarrantyCaseDetailDialog>();
        dialog.ViewModel.CaseId = id;
        _dialog = dialog;
        try
        {
            await dialog.ShowAsync(XamlRoot, ActualTheme);
            if (dialog.ViewModel.HasChanges && IsLoaded) await ViewModel.RefreshAsync();
        }
        finally { _dialog = null; }
    }

    internal static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
