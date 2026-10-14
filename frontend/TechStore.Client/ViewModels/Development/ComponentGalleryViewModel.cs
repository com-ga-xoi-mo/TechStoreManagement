using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TechStore.Client.ViewModels.Development;

public sealed partial class ComponentGalleryViewModel : ObservableObject
{
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string lastQuery = "Chưa gửi tìm kiếm";
    [ObservableProperty] private int submitCount;
    [ObservableProperty] private int clearCount;
    [ObservableProperty] private int actionCount;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private bool canSearch = true;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(EmptyActionCommand))]
    private bool canAct = true;
    [ObservableProperty] private int themeIndex = 1;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string metricError = "";
    [ObservableProperty] private string metricValue = "482.650.000";
    [ObservableProperty] private string metricDetail = "so với tuần trước";
    [ObservableProperty] private string badgeLabel = "Đang sửa chữa";
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string phoneError = "";

    public string[] Themes { get; } = ["Theo hệ thống", "Sáng", "Tối"];
    public GalleryRow[] Rows { get; } =
    [
        new("#DH-8921", "Nguyễn Thị Mỹ", "iPhone 15 Pro Max 256GB Titan Tự Nhiên", "34.990.000 ₫"),
        new("#DH-8920", "Trần Hoàng Phương Anh", "MacBook Air M3 16GB / 512GB", "32.490.000 ₫"),
        new("#DH-8919", "Vũ Quỳnh Trang", "Tai nghe AirPods Pro 2 USB-C", "5.890.000 ₫")
    ];

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private void Search(string? query)
    {
        LastQuery = query ?? "";
        SubmitCount++;
    }

    [RelayCommand]
    private void ClearSearch() { SearchText = ""; ClearCount++; }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void EmptyAction() => ActionCount++;

    [RelayCommand]
    private void ShowLoading() { IsLoading = true; MetricError = ""; }

    [RelayCommand]
    private void ShowError() { IsLoading = false; MetricError = "Chưa tải được số liệu minh họa."; }

    [RelayCommand]
    private void ShowContent() { IsLoading = false; MetricError = ""; }

    [RelayCommand]
    private void ValidatePhone() => PhoneError = IsValidPhone(Phone) ? "" : "Số điện thoại phải có 10 chữ số và bắt đầu bằng 0.";

    partial void OnPhoneChanged(string value)
    {
        if (PhoneError.Length > 0 && IsValidPhone(value)) PhoneError = "";
    }

    private static bool IsValidPhone(string value) => value.Length == 10 && value[0] == '0' && value.All(c => c is >= '0' and <= '9');
}

public sealed record GalleryRow(string Code, string Customer, string Product, string Amount);
