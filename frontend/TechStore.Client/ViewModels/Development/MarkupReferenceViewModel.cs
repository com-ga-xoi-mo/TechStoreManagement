using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Controls;
using TechStore.Client.Services.Development;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Development;

/// <summary>
/// State for the C# Markup reference specimen. Fixed sample data only: it never calls a repository or the API.
/// Contains no WinUI types so it stays testable headless.
/// </summary>
public sealed partial class MarkupReferenceViewModel : PageViewModel
{
    public const string NoSubmissionText = "Chưa gửi tìm kiếm";
    public const string SampleErrorMessage = "Chưa tải được số liệu minh họa.";

    private readonly IMarkupReferenceNavigator _navigator;

    public MarkupReferenceViewModel(IMarkupReferenceNavigator navigator)
    {
        _navigator = navigator;
        ContentState = PageContentState.Content;
    }

    [ObservableProperty] private string query = "";
    [ObservableProperty] private string lastSubmitted = NoSubmissionText;
    [ObservableProperty] private int submitCount;
    /// <summary>0 = follow system, 1 = light, 2 = dark.</summary>
    [ObservableProperty] private int themeIndex;

    public string[] Themes { get; } = ["Theo hệ thống", "Sáng", "Tối"];

    public MarkupReferenceMetric[] Metrics { get; } =
    [
        new("Tổng doanh thu", "482.650.000", "₫", "+12,5%", "so với tuần trước", StatusTone.Success),
        new("Số đơn hàng", "128", "đơn", "+8,2%", "Trong tuần này", StatusTone.Success),
        new("Cảnh báo tồn kho", "7", "sản phẩm", "Cần kiểm tra", "Dưới mức tồn an toàn", StatusTone.Danger)
    ];

    public MarkupReferenceBadge[] Badges { get; } =
    [
        new("Hoàn thành", StatusTone.Success),
        new("Chờ thanh toán", StatusTone.Warning),
        new("Đã hủy", StatusTone.Danger),
        new("Đang xử lý", StatusTone.Info)
    ];

    [RelayCommand]
    private void Search(string? text)
    {
        LastSubmitted = text ?? "";
        SubmitCount++;
    }

    [RelayCommand]
    private void Reset()
    {
        Query = "";
        LastSubmitted = NoSubmissionText;
        SubmitCount = 0;
    }

    [RelayCommand]
    private void ShowLoading()
    {
        ErrorMessage = string.Empty;
        ContentState = PageContentState.Loading;
    }

    [RelayCommand]
    private void ShowEmpty()
    {
        ErrorMessage = string.Empty;
        ContentState = PageContentState.Empty;
    }

    [RelayCommand]
    private void ShowError()
    {
        ErrorMessage = SampleErrorMessage;
        ContentState = PageContentState.Error;
    }

    [RelayCommand]
    private void ShowContent()
    {
        ErrorMessage = string.Empty;
        ContentState = PageContentState.Content;
    }

    [RelayCommand]
    private void Back() => _navigator.ShowGallery();

    /// <summary>Retry on the specimen simply returns to the content state; there is nothing to reload.</summary>
    protected override Task RetryContentAsync()
    {
        ShowContent();
        return Task.CompletedTask;
    }
}

public sealed record MarkupReferenceMetric(string Title, string Value, string Unit, string Trend, string Detail, StatusTone Tone);

public sealed record MarkupReferenceBadge(string Label, StatusTone Tone);
