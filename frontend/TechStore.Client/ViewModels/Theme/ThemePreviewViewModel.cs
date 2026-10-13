using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TechStore.Client.ViewModels.Theme;

/// <summary>
/// Presentation-only ViewModel for inspecting theme tokens, typography, input states, and feedback states.
/// Fully self-contained; makes zero network, database, or external API calls.
/// </summary>
public sealed partial class ThemePreviewViewModel : ObservableObject
{
    [ObservableProperty]
    private decimal samplePrice = 34990000m;

    [ObservableProperty]
    private string? unavailablePrice = null;

    [ObservableProperty]
    private string inputText = "MacBook Pro 14 M3";

    [ObservableProperty]
    private string errorInputText = "SP-INVALID";

    [ObservableProperty]
    private string feedbackStatusMessage = "Sẵn sàng (Nhấn Thử Lại để kiểm tra đổi trạng thái UI)";

    [ObservableProperty]
    private int retryCount = 0;

    [RelayCommand]
    private void Retry()
    {
        RetryCount++;
        FeedbackStatusMessage = $"Đã thực hiện Thử Lại lần {RetryCount} lúc {System.DateTime.Now:HH:mm:ss} (Tương tác cục bộ thành công)";
    }
}
