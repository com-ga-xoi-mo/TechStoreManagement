using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Services.Dialog;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Development;

/// <summary>Explicit gallery probes for cancellation, error recovery and dialog contracts.</summary>
public sealed partial class ShellDiagnosticViewModel(IDialogService dialogs) : PageViewModel
{
    private DiagnosticLoadMode _loadMode;

    [ObservableProperty]
    private string diagnosticStatus = "Các thao tác dưới đây chỉ kiểm tra lifecycle và dialog.";

    [RelayCommand]
    private void StartPendingLoad()
    {
        _loadMode = DiagnosticLoadMode.WaitForCancellation;
        DiagnosticStatus = "Đang chờ — hãy đóng gallery để kiểm tra hủy tải.";
        _ = ReloadAsync();
    }

    [RelayCommand]
    private void SimulateLoadFailure()
    {
        _loadMode = DiagnosticLoadMode.Fail;
        DiagnosticStatus = "Đang tạo lỗi kiểm tra để thử trạng thái và nút tải lại.";
        _ = ReloadAsync();
    }

    [RelayCommand]
    private async Task ShowConfirmDialogAsync()
    {
        try
        {
            var result = await dialogs.ConfirmAsync(
                new DialogRequest(
                    "Kiểm tra hộp thoại",
                    "Đây là hộp thoại phát triển. Không có dữ liệu nào được thay đổi.",
                    "Xác nhận",
                    "Hủy"),
                ActivationToken);
            DiagnosticStatus = result switch
            {
                DialogResult.Confirmed => "Đã xác nhận hộp thoại thử nghiệm.",
                DialogResult.Cancelled => "Đã hủy hộp thoại thử nghiệm.",
                _ => "Đã đóng hộp thoại thử nghiệm."
            };
        }
        catch (OperationCanceledException)
        {
            DiagnosticStatus = "Hộp thoại đã được hủy cùng vòng đời gallery.";
        }
        catch (DialogUnavailableException)
        {
            DiagnosticStatus = "Hộp thoại chưa sẵn sàng; hãy thử lại sau khi gallery tải xong.";
        }
    }

    protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken) => _loadMode switch
    {
        DiagnosticLoadMode.WaitForCancellation => WaitForCancellationAsync(cancellationToken),
        DiagnosticLoadMode.Fail => Task.FromException<bool>(new InvalidOperationException("Development-only load failure.")),
        _ => Task.FromResult(false)
    };

    private static async Task<bool> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return false;
    }

    private enum DiagnosticLoadMode
    {
        Normal,
        WaitForCancellation,
        Fail
    }
}
