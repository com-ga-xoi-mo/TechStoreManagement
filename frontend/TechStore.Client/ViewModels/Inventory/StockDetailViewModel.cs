using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.Services.Navigation;

namespace TechStore.Client.ViewModels.Inventory;

public sealed partial class StockDetailViewModel : ObservableObject, IDisposable
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryChangeNotifier? _changeNotifier;
    private readonly IInventoryNavigationService? _inventoryNavigationService;
    private CancellationTokenSource? _activeRequest;
    private Task _activeLoadTask = Task.CompletedTask;
    private int _requestGeneration;

    public ObservableCollection<StockMovement> RecentMovements { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBalance))]
    private StockBalance? balance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyHistory))]
    [NotifyPropertyChangedFor(nameof(HasBalance))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyHistory))]
    [NotifyPropertyChangedFor(nameof(HasBalance))]
    private bool hasLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyHistory))]
    [NotifyPropertyChangedFor(nameof(HasBalance))]
    private bool hasError;

    [ObservableProperty]
    private bool isNotFound;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNavigationError))]
    private string? navigationErrorMessage;

    [ObservableProperty]
    private bool isPreviewData;

    [ObservableProperty]
    private DateTimeOffset? lastRefreshedAt;

    public StockBalanceKey? Key { get; private set; }
    public bool HasBalance => HasLoaded && !IsLoading && !HasError && Balance is not null;
    public bool IsEmptyHistory => HasLoaded && !IsLoading && !HasError && !IsNotFound && RecentMovements.Count == 0;
    public bool HasMovements => HasBalance && RecentMovements.Count > 0;
    public bool HasNavigationError => !string.IsNullOrWhiteSpace(NavigationErrorMessage);
    public string ProductName => Balance?.ProductName ?? string.Empty;
    public string VariantName => Balance?.VariantName ?? string.Empty;
    public string ProductIdentityText => Balance is null ? string.Empty : $"SKU: {Balance.Sku}";
    public string CategoryName => Balance?.CategoryName ?? string.Empty;
    public int PhysicalQuantity => Balance?.PhysicalQuantity ?? 0;
    public int ReservedQuantity => Balance?.ReservedQuantity ?? 0;
    public int AvailableQuantity => Balance?.AvailableQuantity ?? 0;

    public StockDetailViewModel(
        IInventoryRepository inventoryRepository,
        IInventoryChangeNotifier? changeNotifier = null,
        IInventoryNavigationService? inventoryNavigationService = null)
    {
        _inventoryRepository = inventoryRepository;
        _changeNotifier = changeNotifier;
        _inventoryNavigationService = inventoryNavigationService;
        if (_changeNotifier is not null)
        {
            _changeNotifier.StockChanged += OnStockChanged;
        }
    }

    public Task LoadAsync(StockBalanceKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Key = key;
        OnPropertyChanged(nameof(Key));
        NavigateToSerialImeiCommand.NotifyCanExecuteChanged();
        _activeLoadTask = LoadSnapshotAsync(key);
        return _activeLoadTask;
    }

    public Task WaitForCurrentQueryAsync() => _activeLoadTask;

    public void CancelLoad()
    {
        _activeRequest?.Cancel();
        _activeRequest = null;
        _requestGeneration++;
        IsLoading = false;
    }

    public void Dispose()
    {
        CancelLoad();
        if (_changeNotifier is not null)
        {
            _changeNotifier.StockChanged -= OnStockChanged;
        }
    }

    private void OnStockChanged(object? sender, StockBalanceKey changedKey)
    {
        if (Key == changedKey)
        {
            _activeLoadTask = RefreshAsync();
        }
    }

    [RelayCommand]
    private Task RetryAsync() => Key is null ? Task.CompletedTask : LoadAsync(Key);

    [RelayCommand]
    private Task RefreshAsync() => Key is null ? Task.CompletedTask : LoadAsync(Key);

    [RelayCommand(CanExecute = nameof(CanNavigateToSerialImei))]
    private async Task NavigateToSerialImeiAsync()
    {
        if (Key is null || _inventoryNavigationService is null)
        {
            NavigationErrorMessage = "Điều hướng Serial / IMEI chưa được cấu hình.";
            return;
        }

        NavigationErrorMessage = null;
        if (!await _inventoryNavigationService.NavigateToSerialImeiAsync(Key))
        {
            NavigationErrorMessage = "Không thể mở tab Serial / IMEI.";
        }
    }

    private bool CanNavigateToSerialImei() => Key is not null;

    private async Task LoadSnapshotAsync(StockBalanceKey key)
    {
        _activeRequest?.Cancel();
        _activeRequest?.Dispose();
        var request = new CancellationTokenSource();
        _activeRequest = request;
        var generation = ++_requestGeneration;

        IsLoading = true;
        HasLoaded = false;
        HasError = false;
        IsNotFound = false;
        ErrorMessage = null;
        Balance = null;
        RecentMovements.Clear();
        OnPropertyChanged(nameof(HasMovements));

        try
        {
            var result = await _inventoryRepository.GetStockDetailAsync(key, request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (generation != _requestGeneration)
            {
                return;
            }

            if (result is null)
            {
                IsNotFound = true;
                HasLoaded = true;
                IsPreviewData = false;
                LastRefreshedAt = null;
                return;
            }

            Balance = result.Balance;
            foreach (var movement in result.RecentMovements)
            {
                RecentMovements.Add(movement);
            }
            OnPropertyChanged(nameof(HasMovements));

            IsPreviewData = result.IsPreviewData;
            LastRefreshedAt = result.LastRefreshedAt;
            HasLoaded = true;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            // Closed or superseded detail queries are expected.
        }
        catch (InventoryRepositoryUnavailableException)
        {
            if (generation == _requestGeneration)
            {
                HasError = true;
                ErrorMessage = "API chi tiết tồn kho chưa được cấu hình. Không có dữ liệu thật; bật chế độ Preview để xem dữ liệu mẫu.";
            }
        }
        catch (Exception)
        {
            if (generation == _requestGeneration)
            {
                HasError = true;
                ErrorMessage = "Không tải được chi tiết tồn kho. Vui lòng thử lại.";
            }
        }
        finally
        {
            if (generation == _requestGeneration)
            {
                IsLoading = false;
                _activeRequest = null;
            }

            request.Dispose();
            OnPropertyChanged(nameof(IsEmptyHistory));
            OnPropertyChanged(nameof(HasBalance));
            OnPropertyChanged(nameof(HasMovements));
        }
    }

    partial void OnBalanceChanged(StockBalance? value)
    {
        OnPropertyChanged(nameof(ProductName));
        OnPropertyChanged(nameof(VariantName));
        OnPropertyChanged(nameof(ProductIdentityText));
        OnPropertyChanged(nameof(CategoryName));
        OnPropertyChanged(nameof(PhysicalQuantity));
        OnPropertyChanged(nameof(ReservedQuantity));
        OnPropertyChanged(nameof(AvailableQuantity));
    }
}
