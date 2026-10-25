using TechStore.Client.Repositories.Inventory;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Features.Inventory.Models;
using TechStore.Client.Services.Api;

namespace TechStore.Client.Features.Inventory.ViewModels;

public sealed record InventoryStockDeleteCompleted(
    Guid StockId,
    string Message);

/// <summary>Headless state and commands for deleting one inventory stock row.</summary>
public sealed partial class DeleteStockItemDialogViewModel : ObservableObject
{
    public const string SuccessMessage = "Đã xóa sản phẩm thành công khỏi danh mục tồn kho.";

    private readonly IInventoryRepository _apiClient;

    public Guid StockId { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Sku { get; }
    public int? TotalSerialsCount { get; }
    public string TotalSerialsCountText => TotalSerialsCount is int count
        ? count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))
        : "—";
    public string SerialWarningText => TotalSerialsCount is int count
        ? $"⚠️ Cảnh báo: Thao tác này sẽ xóa vĩnh viễn sản phẩm và {count:N0} mã Serial / IMEI liên quan. Hành động này không thể hoàn tác."
        : "⚠️ Cảnh báo: Thao tác này sẽ xóa vĩnh viễn sản phẩm. Số mã Serial / IMEI liên quan chưa được máy chủ cung cấp. Hành động này không thể hoàn tác.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanDelete => !IsBusy;
    public bool CanCancel => !IsBusy;

    public event EventHandler? RequestClose;
    public event EventHandler<InventoryStockDeleteCompleted>? DeleteSucceeded;

    public DeleteStockItemDialogViewModel(
        IInventoryRepository apiClient,
        InventoryStockDeleteRequest target)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        ArgumentNullException.ThrowIfNull(target);

        StockId = target.StockId;
        ProductId = target.ProductId;
        ProductName = target.ProductName;
        Sku = target.Sku;
        TotalSerialsCount = target.TotalSerialsCount;
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand(CanExecute = nameof(CanDelete))]
    public async Task ConfirmDeleteAsync(CancellationToken cancellationToken = default)
    {
        if (!CanDelete) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _apiClient.DeleteStockAsync(StockId, cancellationToken);
            if (!result.Succeeded)
            {
                ErrorMessage = MapFailure(result.Failure, result.Message);
                return;
            }

            DeleteSucceeded?.Invoke(this, new InventoryStockDeleteCompleted(StockId, SuccessMessage));
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiException exception)
        {
            ErrorMessage = MapApiException(exception);
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể xóa sản phẩm khỏi tồn kho. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);

    private static string MapApiException(ApiException exception) => exception.Kind switch
    {
        ApiErrorKind.Http when exception.StatusCode == 401 => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        ApiErrorKind.Http when exception.StatusCode == 403 => "Bạn không có quyền xóa sản phẩm khỏi tồn kho.",
        ApiErrorKind.Http when exception.StatusCode == 400 => "Không thể xóa tồn kho vì dữ liệu sản phẩm hoặc Serial/IMEI chưa hợp lệ.",
        ApiErrorKind.Http when exception.StatusCode == 404 => "Sản phẩm tồn kho không còn tồn tại. Vui lòng tải lại danh sách.",
        ApiErrorKind.Http when exception.StatusCode == 409 => "Không thể xóa vì sản phẩm đang có dữ liệu liên kết trong kho.",
        ApiErrorKind.Timeout => "Xóa sản phẩm quá thời gian. Vui lòng thử lại.",
        ApiErrorKind.Network => "Không thể kết nối máy chủ tồn kho.",
        _ => "Không thể xóa sản phẩm khỏi tồn kho. Vui lòng thử lại."
    };

    private static string MapFailure(InventoryStockDeleteFailure failure, string? message) => failure switch
    {
        InventoryStockDeleteFailure.Unsupported => message ?? "Máy chủ chưa cung cấp thao tác xóa tồn kho an toàn.",
        InventoryStockDeleteFailure.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        InventoryStockDeleteFailure.Forbidden => "Bạn không có quyền xóa sản phẩm khỏi tồn kho.",
        InventoryStockDeleteFailure.BadRequest => message ?? "Không thể xóa tồn kho vì dữ liệu chưa hợp lệ.",
        InventoryStockDeleteFailure.NotFound => "Sản phẩm tồn kho không còn tồn tại. Vui lòng tải lại danh sách.",
        InventoryStockDeleteFailure.Conflict => message ?? "Không thể xóa vì sản phẩm đang có dữ liệu liên kết trong kho.",
        InventoryStockDeleteFailure.Unavailable => message ?? "Máy chủ tồn kho hiện không khả dụng.",
        _ => message ?? "Không thể xóa sản phẩm khỏi tồn kho. Vui lòng thử lại."
    };
}
