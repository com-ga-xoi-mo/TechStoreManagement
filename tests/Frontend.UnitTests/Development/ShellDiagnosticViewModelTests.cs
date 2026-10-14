using TechStore.Client.Services.Dialog;
using TechStore.Client.ViewModels.Development;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.UnitTests.Development;

public sealed class ShellDiagnosticViewModelTests
{
    [Fact]
    public void FailureProbe_UsesSharedPageErrorStateWithoutFabricatingData()
    {
        var viewModel = new ShellDiagnosticViewModel(new DialogServiceFake());

        viewModel.SimulateLoadFailureCommand.Execute(null);

        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.Equal("Không thể tải nội dung. Hãy thử lại.", viewModel.ErrorMessage);
        Assert.Contains("lỗi kiểm tra", viewModel.DiagnosticStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmProbe_UsesDialogContractAndReportsResult()
    {
        var dialogs = new DialogServiceFake { Result = DialogResult.Confirmed };
        var viewModel = new ShellDiagnosticViewModel(dialogs);

        await viewModel.ShowConfirmDialogCommand.ExecuteAsync(null);

        Assert.Equal("Kiểm tra hộp thoại", dialogs.LastRequest?.Title);
        Assert.Equal("Xác nhận", dialogs.LastRequest?.PrimaryButtonText);
        Assert.Equal("Đã xác nhận hộp thoại thử nghiệm.", viewModel.DiagnosticStatus);
    }

    private sealed class DialogServiceFake : IDialogService
    {
        public DialogResult Result { get; init; } = DialogResult.Dismissed;
        public DialogRequest? LastRequest { get; private set; }

        public Task<DialogResult> ConfirmAsync(DialogRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }

        public Task<DialogResult> InformAsync(DialogRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}
