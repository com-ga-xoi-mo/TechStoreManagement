using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
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
                    new ItemsControl().ItemsSource(x => x.Binding(() => ViewModel.Rows)).ItemTemplate<WarrantyRow>(BuildRow),
                    new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                        new Button().Content("Trang trước").Command(ViewModel.PreviousCommand),
                        new TextBlock().VerticalAlignment(VerticalAlignment.Center).Text(x => x.Binding(() => ViewModel.PageLabel)),
                        new Button().Content("Trang sau").Command(ViewModel.NextCommand))))));
    }

    private Button BuildRow(WarrantyRow row) => new Button().Command(ViewModel.OpenCaseCommand)
        .CommandParameter(x => x.Binding(() => row)).HorizontalAlignment(HorizontalAlignment.Stretch)
        .HorizontalContentAlignment(HorizontalAlignment.Stretch).Margin(0, 0, 0, 4)
        .Content(new Grid().ColumnDefinitions("2*, 3*, 2*, 2*, 2*, 2*").ColumnSpacing(8).Children(
            new TextBlock().Text(x => x.Binding(() => row.Code)).Foreground(AppColors.Brushes.Text),
            new TextBlock().Grid(column: 1).Text(x => x.Binding(() => row.Product)).TextTrimming(TextTrimming.CharacterEllipsis),
            new TextBlock().Grid(column: 2).Text(x => x.Binding(() => row.Device)),
            new TextBlock().Grid(column: 3).Text(x => x.Binding(() => row.DisplayCustomer)).TextTrimming(TextTrimming.CharacterEllipsis),
            new TextBlock().Grid(column: 4).Text(x => x.Binding(() => row.StatusLabel)),
            new TextBlock().Grid(column: 5).Text(x => x.Binding(() => row.OpenedLabel))));

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
