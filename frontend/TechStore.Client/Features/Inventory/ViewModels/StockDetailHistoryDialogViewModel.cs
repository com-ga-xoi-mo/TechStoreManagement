using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Features.Inventory.Models;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;

namespace TechStore.Client.Features.Inventory.ViewModels;

public sealed partial class StockDetailHistoryDialogViewModel(
    InventoryStockRow row,
    IInventoryApiClient apiClient) : ObservableObject
{
    private readonly IInventoryApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));

    public InventoryStockRow Row { get; } = row ?? throw new ArgumentNullException(nameof(row));
    public string ProductName => Row.ProductName;
    public string Sku => Row.Sku;
    public string CategoryName => Row.CategoryName;

    [ObservableProperty] private InventoryStockDetailDto? _detail;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public int Quantity => Detail?.Quantity ?? Row.Quantity;
    public int AvailableQuantity => Detail?.AvailableQuantity ?? Row.AvailableQuantity;
    public int ReservedQuantity => Detail?.ReservedQuantity ?? Row.ReservedQuantity;
    public int TotalSerialsCount => Detail?.TotalSerialsCount ?? Row.TotalSerialsCount ?? 0;
    public IReadOnlyList<InventoryMovementDto> RecentMovements => Detail?.RecentMovements ?? [];
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasMovements => RecentMovements.Count > 0;
    public string MovementCountText => $"{RecentMovements.Count} giao dịch gần nhất";
    public string MovementSummaryText => $"Hiển thị {RecentMovements.Count} biến động gần nhất";
    public string SerialsLinkText => $"Xem {TotalSerialsCount} Serial / IMEI của sản phẩm này";
    public string LastUpdatedText
    {
        get
        {
            var updated = Detail?.UpdatedAt;
            if (updated is null && RecentMovements.Count > 0)
            {
                updated = RecentMovements.Max(movement => movement.CreatedAt);
            }

            if (updated is null && Detail is { CreatedAt: var createdAt } && createdAt > DateTime.MinValue)
            {
                updated = createdAt;
            }

            if (updated is not DateTime timestamp)
            {
                return "Cập nhật lần cuối: —";
            }

            var local = timestamp.ToLocalTime();
            var display = local.Date == DateTime.Now.Date
                ? $"hôm nay {local:HH:mm}"
                : local.ToString("dd/MM/yyyy HH:mm");
            return $"Cập nhật lần cuối: {display}";
        }
    }

    public event EventHandler? RequestClose;
    public event EventHandler? AdjustmentRequested;
    public event EventHandler? SerialsRequested;

    partial void OnDetailChanged(InventoryStockDetailDto? value)
    {
        OnPropertyChanged(nameof(Quantity));
        OnPropertyChanged(nameof(AvailableQuantity));
        OnPropertyChanged(nameof(ReservedQuantity));
        OnPropertyChanged(nameof(TotalSerialsCount));
        OnPropertyChanged(nameof(RecentMovements));
        OnPropertyChanged(nameof(HasMovements));
        OnPropertyChanged(nameof(MovementCountText));
        OnPropertyChanged(nameof(MovementSummaryText));
        OnPropertyChanged(nameof(SerialsLinkText));
        OnPropertyChanged(nameof(LastUpdatedText));
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _apiClient.GetStockDetailAsync(Row.Id, cancellationToken);
            if (result.Succeeded && result.Data is not null)
            {
                Detail = result.Data;
                return;
            }

            ErrorMessage = result.Message ?? result.Failure switch
            {
                InventoryStockDetailFailure.NotFound => "Không tìm thấy tồn kho này.",
                InventoryStockDetailFailure.Forbidden => "Bạn không có quyền xem chi tiết tồn kho.",
                _ => "Không thể tải chi tiết tồn kho."
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RequestAdjustment() => AdjustmentRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RequestSerials() => SerialsRequested?.Invoke(this, EventArgs.Empty);
}
