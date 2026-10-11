using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.ViewModels.Inventory;

public enum InventoryTab
{
    Categories,
    Stock,
    SerialImei
}

public sealed partial class InventoryViewModel : ObservableObject
{
    public CategoryListViewModel CategoryListViewModel { get; }
    public StockListViewModel StockListViewModel { get; }
    private readonly IInventoryNavigationContext? _navigationContext;
    public StockBalanceKey? SelectedSerialImeiContext { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCategoryTabSelected))]
    [NotifyPropertyChangedFor(nameof(IsStockTabSelected))]
    [NotifyPropertyChangedFor(nameof(IsSerialImeiTabSelected))]
    [NotifyPropertyChangedFor(nameof(PlaceholderTitle))]
    [NotifyPropertyChangedFor(nameof(PlaceholderMessage))]
    private InventoryTab selectedTab = InventoryTab.Categories;

    public bool IsCategoryTabSelected => SelectedTab == InventoryTab.Categories;
    public bool IsStockTabSelected => SelectedTab == InventoryTab.Stock;
    public bool IsSerialImeiTabSelected => SelectedTab == InventoryTab.SerialImei;

    public string PlaceholderTitle => SelectedTab switch
    {
        InventoryTab.SerialImei => "Serial / IMEI chưa được triển khai",
        _ => string.Empty
    };

    public string PlaceholderMessage => SelectedTab switch
    {
        InventoryTab.SerialImei => "Nội dung Serial / IMEI sẽ được bổ sung trong UI-12. Không có dữ liệu mã máy mẫu trong tab này.",
        _ => string.Empty
    };

    public InventoryViewModel(
        CategoryListViewModel categoryListViewModel,
        StockListViewModel stockListViewModel,
        IInventoryNavigationContext? navigationContext = null)
    {
        CategoryListViewModel = categoryListViewModel;
        StockListViewModel = stockListViewModel;
        _navigationContext = navigationContext;
    }

    public async Task InitializeAsync()
    {
        if (_navigationContext is not null)
        {
            _navigationContext.SerialImeiRequested -= OnSerialImeiRequested;
            _navigationContext.SerialImeiRequested += OnSerialImeiRequested;
            var pendingKey = _navigationContext.ConsumePendingSerialImeiKey();
            if (pendingKey is not null)
            {
                SelectSerialImeiContext(pendingKey);
                return;
            }
        }

        await SelectCategoryTabAsync();
    }

    public void OnNavigatedFrom()
    {
        if (_navigationContext is not null)
        {
            _navigationContext.SerialImeiRequested -= OnSerialImeiRequested;
        }
        CategoryListViewModel.CancelPendingLoad();
        StockListViewModel.OnNavigatedFrom();
    }

    private void OnSerialImeiRequested(object? sender, StockBalanceKey key) => SelectSerialImeiContext(key);

    private void SelectSerialImeiContext(StockBalanceKey key)
    {
        SelectedSerialImeiContext = key;
        OnPropertyChanged(nameof(SelectedSerialImeiContext));
        SelectedTab = InventoryTab.SerialImei;
    }

    [RelayCommand]
    private async Task SelectCategoryTabAsync()
    {
        SelectedTab = InventoryTab.Categories;
        await CategoryListViewModel.LoadIfNeededAsync();
    }

    [RelayCommand]
    private async Task SelectStockTabAsync()
    {
        SelectedTab = InventoryTab.Stock;
        await StockListViewModel.LoadIfNeededAsync();
    }

    [RelayCommand]
    private void SelectSerialImeiTab() => SelectedTab = InventoryTab.SerialImei;
}
