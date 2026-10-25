using TechStore.Client.Repositories.Inventory;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

public enum StockAdjustmentKind
{
    Increase,
    Decrease
}

public sealed partial class StockAdjustmentDialogViewModel(
    InventoryStockRow row,
    IInventoryRepository inventoryRepository) : ObservableObject
{
    private readonly IInventoryRepository _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));

    public InventoryStockRow Row { get; } = row ?? throw new ArgumentNullException(nameof(row));
    public string ProductName => Row.ProductName;
    public string Sku => Row.Sku;
    public int CurrentQuantity => Row.Quantity;
    public int ReservedQuantity => Row.ReservedQuantity;
    public int AvailableQuantity => Row.AvailableQuantity;

    [ObservableProperty] private StockAdjustmentKind _kind = StockAdjustmentKind.Increase;
    [ObservableProperty] private int _amount;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public int SignedQuantityChange => Kind == StockAdjustmentKind.Increase ? Amount : -Amount;
    public int NewQuantity => CurrentQuantity + SignedQuantityChange;
    public bool IsIncrease => Kind == StockAdjustmentKind.Increase;
    public bool CanSubmit => !IsBusy && Amount > 0 && !string.IsNullOrWhiteSpace(Reason) && NewQuantity >= ReservedQuantity && NewQuantity >= 0;
    public string PreviewText => $"Tồn mới: {CurrentQuantity:N0} → {Math.Max(0, NewQuantity):N0}";
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public event EventHandler? RequestClose;
    public event Action<InventoryStockDetailDto>? AdjustmentSucceeded;

    partial void OnKindChanged(StockAdjustmentKind value)
    {
        OnPropertyChanged(nameof(IsIncrease));
        NotifyProjection();
    }
    partial void OnAmountChanged(int value) => NotifyProjection();
    partial void OnReasonChanged(string value) => NotifyProjection();
    partial void OnIsBusyChanged(bool value) => NotifyProjection();
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private void NotifyProjection()
    {
        OnPropertyChanged(nameof(SignedQuantityChange));
        OnPropertyChanged(nameof(NewQuantity));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(CanSubmit));
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void SetIncrease() => Kind = StockAdjustmentKind.Increase;

    [RelayCommand]
    private void SetDecrease() => Kind = StockAdjustmentKind.Decrease;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        if (!CanSubmit) return;
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await _inventoryRepository.AdjustStockAsync(
                Row.Id,
                new AdjustStockRequest
                {
                    QuantityChange = SignedQuantityChange,
                    Reason = Reason.Trim(),
                    Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()
                },
                cancellationToken);
            if (result.Succeeded && result.Data is not null)
            {
                AdjustmentSucceeded?.Invoke(result.Data);
                RequestClose?.Invoke(this, EventArgs.Empty);
                return;
            }

            ErrorMessage = result.Message ?? result.Failure switch
            {
                InventoryStockDetailFailure.Forbidden => "Bạn không có quyền điều chỉnh tồn kho.",
                InventoryStockDetailFailure.Conflict => "Không thể giảm thấp hơn số lượng đang giữ chỗ.",
                InventoryStockDetailFailure.NotFound => "Không tìm thấy tồn kho này.",
                _ => "Không thể điều chỉnh tồn kho."
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }
}
