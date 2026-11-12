using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TechStore.Client.Controls;
using TechStore.Client.Models.Identity;
using TechStore.Client.Styles;
using TechStore.Client.ViewModels.Identity;
using TechStore.Client.ViewModels.Pages;
using TechStore.Client.Views.Shell;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Identity;

public sealed class UserManagementPage : Page, IRoutePage
{
    public UserManagementViewModel ViewModel { get; }
    PageViewModel IRoutePage.ViewModel => ViewModel;
    private readonly PasswordBox _password = new();

    public UserManagementPage(UserManagementViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        this.Background(AppColors.Brushes.Canvas).Content(new ScrollViewer()
            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
            .Content(new StackPanel().Padding(24).Spacing(16).Children(
                new StackPanel().Spacing(16).Children(
                new Button().Content("← Cài đặt").Command(ViewModel.BackCommand),
                new TextBlock().Text("Quản lý tài khoản").FontSize(24).Foreground(AppColors.Brushes.Text),
                new TextBlock().Text("Tạo nhân viên, phân quyền và kiểm soát truy cập.").Foreground(AppColors.Brushes.SecondaryText),
                BuildFilters()),
                new StackPanel().Spacing(8).Children(
                new ProgressRing().IsActive(x => x.Binding(() => ViewModel.IsLoading))
                    .Visibility(x => x.Binding(() => ViewModel.IsLoading).Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed)),
                new TextBlock().Foreground(AppColors.Brushes.DangerText).TextWrapping(TextWrapping.Wrap)
                    .Text(x => x.Binding(() => ViewModel.ErrorMessage)),
                new Button().Content("Thử lại").Command(ViewModel.RetryCommand)
                    .Visibility(x => x.Binding(() => ViewModel.IsError).Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed)),
                new TextBlock().Text("Không có tài khoản phù hợp.").Foreground(AppColors.Brushes.SecondaryText)
                    .Visibility(x => x.Binding(() => ViewModel.IsEmpty).Convert((bool value) => value ? Visibility.Visible : Visibility.Collapsed))),
                BuildList(),
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                    new Button().Content("Trang trước").Command(ViewModel.PreviousPageCommand),
                    new TextBlock().VerticalAlignment(VerticalAlignment.Center).Foreground(AppColors.Brushes.SecondaryText)
                        .Text(x => x.Binding(() => ViewModel.PageDescription)),
                    new Button().Content("Trang sau").Command(ViewModel.NextPageCommand)),
                BuildForm())));
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private UIElement BuildFilters() => new StackPanel().Spacing(8).Children(
        new SearchBoxControl().PlaceholderText("Tên đăng nhập, họ tên hoặc email")
            .Text(x => x.Binding(() => ViewModel.Query).Mode(BindingMode.TwoWay)).SearchCommand(ViewModel.SearchCommand),
        new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
            new ComboBox().Header("Vai trò").ItemsSource(ViewModel.RoleFilterOptions)
                .SelectedIndex(x => x.Binding(() => ViewModel.RoleFilterIndex).Mode(BindingMode.TwoWay)),
            new ComboBox().Header("Trạng thái").ItemsSource(ViewModel.StatusOptions)
                .SelectedIndex(x => x.Binding(() => ViewModel.StatusFilterIndex).Mode(BindingMode.TwoWay)),
            new Button().Content("Lọc").VerticalAlignment(VerticalAlignment.Bottom).Command(ViewModel.SearchCommand)));

    private UIElement BuildList() => new ListView().MaxHeight(320)
        .ItemsSource(x => x.Binding(() => ViewModel.Accounts))
        .SelectionMode(ListViewSelectionMode.None)
        .ItemTemplate<UserAccount>(account => new Button().HorizontalAlignment(HorizontalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch).Command(ViewModel.SelectAccountCommand)
            .CommandParameter(x => x.Binding(() => account)).Content(new Grid().ColumnDefinitions("2*, 3*, 2*").Padding(8).ColumnSpacing(12).Children(
            new StackPanel().Grid(column: 0).Children(
                new TextBlock().Foreground(AppColors.Brushes.Text).Text(x => x.Binding(() => account.Username)),
                new TextBlock().Foreground(AppColors.Brushes.SecondaryText).Text(x => x.Binding(() => account.FullName))),
            new TextBlock().Grid(column: 1).Foreground(AppColors.Brushes.SecondaryText).TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => account.Email)),
            new StackPanel().Grid(column: 2).Children(
                new TextBlock().Foreground(AppColors.Brushes.Text).Text(x => x.Binding(() => account.RoleLabel)),
                new TextBlock().Foreground(AppColors.Brushes.SecondaryText).Text(x => x.Binding(() => account.StatusLabel))))));

    private UIElement BuildForm()
    {
        _password.Header("Mật khẩu mới (8–128 ký tự)").MaxLength(128);
        return new Border().Background(AppColors.Brushes.Surface).BorderBrush(AppColors.Brushes.Stroke)
            .BorderThickness(new Thickness(1)).CornerRadius(new CornerRadius(12)).Padding(20)
            .Child(new StackPanel().Spacing(10).Children(
                new StackPanel().Spacing(10).Children(
                new TextBlock().FontSize(20).Foreground(AppColors.Brushes.Text).Text(x => x.Binding(() => ViewModel.FormTitle)),
                new Button().Content("Tạo tài khoản mới / Đóng biểu mẫu hiện tại").Command(ViewModel.NewAccountCommand),
                new TextBlock().Foreground(AppColors.Brushes.SuccessText).TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => ViewModel.Notice)),
                new TextBlock().Foreground(AppColors.Brushes.DangerText).TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => ViewModel.FormError)),
                new TextBox().Header("Tên đăng nhập").MaxLength(64)
                    .Text(x => x.Binding(() => ViewModel.Username).Mode(BindingMode.TwoWay))
                    .IsReadOnly(x => x.Binding(() => ViewModel.IsEditing)),
                new TextBlock().Foreground(AppColors.Brushes.DangerText).TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => ViewModel.UsernameError))),
                new StackPanel().Spacing(10).Children(
                new TextBox().Header("Họ và tên").MaxLength(200).Text(x => x.Binding(() => ViewModel.FullName).Mode(BindingMode.TwoWay)),
                new TextBlock().Foreground(AppColors.Brushes.DangerText).TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => ViewModel.FullNameError)),
                new TextBox().Header("Email").MaxLength(254).Text(x => x.Binding(() => ViewModel.Email).Mode(BindingMode.TwoWay)),
                new TextBlock().Foreground(AppColors.Brushes.DangerText).TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => ViewModel.EmailError)),
                new ComboBox().Header("Vai trò").ItemsSource(ViewModel.RoleOptions)
                    .SelectedIndex(x => x.Binding(() => ViewModel.SelectedRoleIndex).Mode(BindingMode.TwoWay)),
                new TextBlock().Foreground(AppColors.Brushes.DangerText).TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => ViewModel.RoleError))),
                new StackPanel().Spacing(10).Children(
                _password,
                new TextBlock().Foreground(AppColors.Brushes.DangerText).TextWrapping(TextWrapping.Wrap).Text(x => x.Binding(() => ViewModel.PasswordError)),
                new TextBlock().Text("Khi sửa tài khoản, dùng các nút riêng để đổi vai trò hoặc đặt lại mật khẩu.")
                    .TextWrapping(TextWrapping.Wrap).Foreground(AppColors.Brushes.SecondaryText),
                new Button().Content("Lưu thông tin").Command(ViewModel.SaveCommand)
                    .Background(AppColors.Brushes.Primary).Foreground(AppColors.Brushes.OnPrimary)
                    .IsEnabled(x => x.Binding(() => ViewModel.IsBusy).Convert((bool busy) => !busy)),
                new StackPanel().Spacing(8)
                    .Visibility(x => x.Binding(() => ViewModel.IsEditing).Convert((bool editing) => editing ? Visibility.Visible : Visibility.Collapsed))
                    .Children(
                        new Button().Content("Đổi vai trò").Command(ViewModel.ChangeRoleCommand),
                        new Button().Content(new TextBlock().Text(x => x.Binding(() => ViewModel.AccessActionLabel))).Command(ViewModel.ToggleStatusCommand),
                        new Button().Content("Đặt lại mật khẩu").Command(ViewModel.ResetPasswordCommand)))));
    }

    // Password is not a DependencyProperty on WinUI; scoped exception documented in design D8.
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _password.PasswordChanged += OnPasswordChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _password.Password = ViewModel.Password;
    }
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _password.PasswordChanged -= OnPasswordChanged;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _password.Password = string.Empty;
        ViewModel.Password = string.Empty;
    }
    private void OnPasswordChanged(object sender, RoutedEventArgs args) => ViewModel.Password = _password.Password;
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ViewModel.Password) && _password.Password != ViewModel.Password)
            _password.Password = ViewModel.Password;
    }
}
