using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;

namespace TechStore.Client.ViewModels.Inventory;

public sealed partial class PurchaseReceiptDetailDialogViewModel(IPurchaseReceiptRepository receiptRepository) : ObservableObject
{
    [ObservableProperty] private PurchaseReceiptDetail? _receipt;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string Title => Receipt is null ? "Chi tiết phiếu nhập" : $"Chi tiết phiếu nhập {Receipt.PoNumber}";

    public event EventHandler? RequestClose;

    [RelayCommand]
    public async Task LoadAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasError));

        try
        {
            var result = await receiptRepository.GetByIdAsync(receiptId, cancellationToken);
            if (result.Succeeded && result.Data != null)
            {
                Receipt = result.Data;
            }
            else
            {
                ErrorMessage = result.Failure switch
                {
                    ReceiptFailureKind.NotFound => "Phiếu nhập không tồn tại.",
                    ReceiptFailureKind.Forbidden => "Bạn không có quyền xem phiếu nhập.",
                    ReceiptFailureKind.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                    _ => "Không thể tải chi tiết phiếu nhập. Vui lòng thử lại."
                };
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể tải chi tiết phiếu nhập. Vui lòng thử lại.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(Title));
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
