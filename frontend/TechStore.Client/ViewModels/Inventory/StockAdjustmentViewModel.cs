using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.Services.Navigation;

namespace TechStore.Client.ViewModels.Inventory;

public sealed partial class StockAdjustmentViewModel : ObservableObject, IDisposable
{
    public IReadOnlyList<string> DirectionOptions { get; } = ["Tăng tồn", "Giảm tồn"];
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryChangeNotifier _changeNotifier;
    private readonly IInventoryNavigationService _inventoryNavigationService;
    private CancellationTokenSource? _activeRequest;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSerialBlocked))]
    [NotifyPropertyChangedFor(nameof(IsRegularBalance))]
    [NotifyPropertyChangedFor(nameof(ProjectedPhysicalQuantity))]
    [NotifyPropertyChangedFor(nameof(BalanceSummary))]
    [NotifyPropertyChangedFor(nameof(SerialBlockMessage))]
    private StockBalance? balance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectedPhysicalQuantity))]
    private StockAdjustmentDirection direction = StockAdjustmentDirection.Increase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectedPhysicalQuantity))]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    private string quantityText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    private string reason = string.Empty;

    [ObservableProperty]
    private bool isPreviewData;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditDraft))]
    private bool isSubmitting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditDraft))]
    private bool isReloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeedbackMessage))]
    private string? feedbackMessage;

    [ObservableProperty]
    private bool isConflict;

    [ObservableProperty]
    private bool isSucceeded;

    [ObservableProperty]
    private bool isClosed;

    public bool IsSerialBlocked => Balance?.IsSerialTracked is not false;
    public bool IsRegularBalance => Balance?.IsSerialTracked is false;
    public bool HasFeedbackMessage => !string.IsNullOrWhiteSpace(FeedbackMessage);
    public string SerialBlockMessage => Balance?.IsSerialTracked is null
        ? "Chưa xác định được cách theo dõi số lượng. Để an toàn, thao tác điều chỉnh đang bị khóa."
        : "Số lượng mặt hàng này được quản lý theo từng Serial/IMEI và không thể sửa trực tiếp tại đây.";
    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);
    public string? ValidationMessage =>
        !int.TryParse(QuantityText, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0
            ? "Nhập số lượng nguyên lớn hơn 0."
            : string.IsNullOrWhiteSpace(Reason)
                ? "Vui lòng nhập lý do điều chỉnh."
                : null;

    public bool CanSubmit => IsRegularBalance && !IsSubmitting && !IsReloading && ValidationMessage is null;
    public bool CanEditDraft => !IsSubmitting && !IsReloading;
    public int DirectionIndex
    {
        get => Direction == StockAdjustmentDirection.Increase ? 0 : 1;
        set => Direction = value == 1 ? StockAdjustmentDirection.Decrease : StockAdjustmentDirection.Increase;
    }

    public int? ProjectedPhysicalQuantity
    {
        get
        {
            if (Balance is null || !int.TryParse(QuantityText, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
                return null;
            try
            {
                var projected = checked(Balance.PhysicalQuantity + (Direction == StockAdjustmentDirection.Increase ? quantity : -quantity));
                return projected >= 0 ? projected : null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }
    }

    public string BalanceSummary => Balance is null
        ? "Không có dữ liệu số dư."
        : $"Tồn vật lý: {Balance.PhysicalQuantity} · Đang giữ: {Balance.ReservedQuantity} · Có thể bán: {Balance.AvailableQuantity}";

    public StockAdjustmentViewModel(
        IInventoryRepository inventoryRepository,
        IInventoryChangeNotifier changeNotifier,
        IInventoryNavigationService inventoryNavigationService)
    {
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _changeNotifier = changeNotifier ?? throw new ArgumentNullException(nameof(changeNotifier));
        _inventoryNavigationService = inventoryNavigationService ?? throw new ArgumentNullException(nameof(inventoryNavigationService));
    }

    public void Initialize(StockBalance selectedBalance, bool isPreviewData)
    {
        _activeRequest?.Cancel();
        _activeRequest?.Dispose();
        _activeRequest = null;
        Balance = selectedBalance ?? throw new ArgumentNullException(nameof(selectedBalance));
        IsPreviewData = isPreviewData;
        Direction = StockAdjustmentDirection.Increase;
        QuantityText = string.Empty;
        Reason = string.Empty;
        FeedbackMessage = null;
        IsConflict = false;
        IsSucceeded = false;
        IsClosed = false;
        NotifyCommandState();
    }

    partial void OnBalanceChanged(StockBalance? value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnDirectionChanged(StockAdjustmentDirection value)
    {
        OnPropertyChanged(nameof(DirectionIndex));
        NotifyCommandState();
    }
    partial void OnQuantityTextChanged(string value)
    {
        OnPropertyChanged(nameof(ValidationMessage));
        NotifyCommandState();
    }

    partial void OnReasonChanged(string value)
    {
        OnPropertyChanged(nameof(ValidationMessage));
        NotifyCommandState();
    }
    partial void OnIsSubmittingChanged(bool value) => NotifyCommandState();
    partial void OnIsReloadingChanged(bool value) => NotifyCommandState();

    private void NotifyCommandState()
    {
        OnPropertyChanged(nameof(CanSubmit));
        SubmitCommand.NotifyCanExecuteChanged();
        NavigateToSerialImeiCommand.NotifyCanExecuteChanged();
        ReloadBalanceCommand.NotifyCanExecuteChanged();
    }

    private bool CanSubmitAdjustment() => CanSubmit;
    private bool CanNavigateToSerialImei() => Balance is not null && IsSerialBlocked && !IsSubmitting && !IsReloading;
    private bool CanReloadBalance() => Balance is not null && !IsSubmitting && !IsReloading;

    [RelayCommand(CanExecute = nameof(CanNavigateToSerialImei))]
    private async Task NavigateToSerialImeiAsync()
    {
        if (Balance is null) return;
        await _inventoryNavigationService.NavigateToSerialImeiAsync(new StockBalanceKey(Balance.ProductId, Balance.VariantId));
    }

    [RelayCommand]
    private void Cancel()
    {
        IsClosed = true;
        _activeRequest?.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanSubmitAdjustment))]
    private async Task SubmitAsync()
    {
        if (!CanSubmit || Balance is null || !int.TryParse(QuantityText, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
            return;
        var selectedBalance = Balance;
        var request = new StockAdjustmentRequest(
            new StockBalanceKey(selectedBalance.ProductId, selectedBalance.VariantId), Direction, quantity, Reason);
        _activeRequest?.Cancel();
        _activeRequest?.Dispose();
        var cancellation = new CancellationTokenSource();
        _activeRequest = cancellation;
        IsSubmitting = true;
        FeedbackMessage = null;
        IsConflict = false;
        IsSucceeded = false;
        try
        {
            var result = await _inventoryRepository.AdjustStockAsync(request, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (IsClosed) return;
            IsPreviewData = result.IsPreviewData;
            if (result.Outcome == StockAdjustmentOutcome.Succeeded && result.Balance is not null)
            {
                Balance = result.Balance;
                IsSucceeded = true;
                FeedbackMessage = IsPreviewData
                    ? "Điều chỉnh mẫu thành công · Preview, chưa ghi dữ liệu thật."
                    : "Điều chỉnh tồn kho thành công.";
                _changeNotifier.NotifyStockChanged(new StockBalanceKey(selectedBalance.ProductId, selectedBalance.VariantId));
                IsClosed = true;
            }
            else if (result.Outcome == StockAdjustmentOutcome.Conflict)
            {
                IsConflict = true;
                FeedbackMessage = result.Message ?? "Số tồn đã thay đổi. Hãy tải lại số tồn trước khi quyết định gửi lại.";
            }
            else
            {
                FeedbackMessage = result.Message ?? "Không thể điều chỉnh tồn kho. Dữ liệu nhập vẫn được giữ.";
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Dialog close cancels the request; no success notification is emitted.
        }
        catch (Exception)
        {
            if (!cancellation.IsCancellationRequested && !IsClosed)
            {
                FeedbackMessage = "Không thể điều chỉnh tồn kho. Dữ liệu nhập vẫn được giữ.";
                IsConflict = false;
                IsSucceeded = false;
            }
        }
        finally
        {
            if (ReferenceEquals(_activeRequest, cancellation)) _activeRequest = null;
            IsSubmitting = false;
            cancellation.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(CanReloadBalance))]
    private async Task ReloadBalanceAsync()
    {
        if (Balance is null || IsSubmitting || IsReloading) return;
        var key = new StockBalanceKey(Balance.ProductId, Balance.VariantId);
        _activeRequest?.Cancel();
        _activeRequest?.Dispose();
        var cancellation = new CancellationTokenSource();
        _activeRequest = cancellation;
        IsReloading = true;
        try
        {
            var snapshot = await _inventoryRepository.GetStockDetailAsync(key, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (snapshot is null)
            {
                FeedbackMessage = "Không tìm thấy số dư mới nhất. Bản nháp vẫn được giữ.";
                return;
            }

            Balance = snapshot.Balance;
            IsPreviewData = snapshot.IsPreviewData;
            FeedbackMessage = IsConflict
                ? "Đã tải lại số tồn. Kiểm tra và xác nhận bản nháp trước khi gửi lại."
                : "Đã tải lại số tồn.";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Dialog close cancels the refresh.
        }
        catch (Exception)
        {
            FeedbackMessage = "Không tải lại được số tồn. Bản nháp vẫn được giữ.";
        }
        finally
        {
            if (ReferenceEquals(_activeRequest, cancellation)) _activeRequest = null;
            IsReloading = false;
            cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        _activeRequest?.Cancel();
        _activeRequest?.Dispose();
        _activeRequest = null;
    }
}
