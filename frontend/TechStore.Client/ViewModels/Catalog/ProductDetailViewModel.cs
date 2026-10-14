using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Catalog;

public sealed partial class ProductDetailViewModel : PageViewModel
{
    private readonly IProductRepository _products;
    private readonly INavigationService _navigation;
    private readonly ISessionStore _sessionStore;
    private ProductQuery? _returnCriteria;
    private Guid? _focusProductId;

    [ObservableProperty] private Guid productId;
    [ObservableProperty] private ProductDetail? product;
    [ObservableProperty] private bool isNotFound;
    public bool HasSpecs => Product?.Specs.Count > 0;
    public bool HasVariants => Product?.Variants.Count > 0;
    public bool HasNoSpecs => !HasSpecs;
    public bool HasNoVariants => !HasVariants;

    public ProductDetailViewModel(
        IProductRepository products,
        INavigationService navigation,
        ISessionStore sessionStore)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _sessionStore.SessionChanged += OnSessionChanged;
    }

    protected override bool ManagesLoadState => true;

    /// <summary>Called before activation; it performs no I/O and clears any previously visible product.</summary>
    public void SetRoute(Guid id, ProductQuery? returnCriteria, Guid? focusProductId)
    {
        if (id == Guid.Empty) throw new ArgumentException("A non-empty product id is required.", nameof(id));
        if (ContentState != PageContentState.Idle)
            throw new InvalidOperationException("A product detail route must be configured before activation.");
        ProductId = id;
        Product = null;
        IsNotFound = false;
        _returnCriteria = returnCriteria;
        _focusProductId = focusProductId;
    }

    protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        var requestedId = ProductId;
        Product = null;
        IsNotFound = false;
        ErrorMessage = string.Empty;
        ContentState = PageContentState.Loading;
        return RunLatestRequestAsync(
            token => _products.GetByIdAsync(requestedId, token),
            product =>
            {
                if (product is null)
                {
                    IsNotFound = true;
                    Product = null;
                    ContentState = PageContentState.Empty;
                    return;
                }
                if (product.Id != requestedId)
                    throw new ApiException(ApiErrorKind.InvalidResponse, "Product detail id did not match the requested id.");
                Product = product;
                IsNotFound = false;
                ContentState = PageContentState.Content;
            },
            product => product is not null,
            () => false);
    }

    protected override void OnLatestRequestFailed(Exception exception)
    {
        Product = null;
        IsNotFound = false;
        ContentState = PageContentState.Error;
    }

    partial void OnProductChanged(ProductDetail? value)
    {
        OnPropertyChanged(nameof(HasSpecs));
        OnPropertyChanged(nameof(HasVariants));
        OnPropertyChanged(nameof(HasNoSpecs));
        OnPropertyChanged(nameof(HasNoVariants));
    }

    protected override void OnDeactivated() => _sessionStore.SessionChanged -= OnSessionChanged;

    private void OnSessionChanged(object? sender, SessionChangedEventArgs args)
    {
        CancelTrackedRequests();
        Product = null;
        IsNotFound = false;
        _returnCriteria = null;
        _focusProductId = null;
        ContentState = args.Status == SessionStatus.Expired ? PageContentState.Error : PageContentState.Idle;
        ErrorMessage = args.Status == SessionStatus.Expired
            ? "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục."
            : string.Empty;
    }

    [RelayCommand]
    private void Back() => _navigation.Navigate(AppRoute.Products(_returnCriteria, _focusProductId ?? ProductId));

    protected override string GetSafeErrorMessage(Exception exception) => exception switch
    {
        ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.",
        ApiException { StatusCode: 403 } => "Bạn không có quyền xem sản phẩm này.",
        ApiException { Kind: ApiErrorKind.Timeout } => "Yêu cầu Catalog quá thời gian chờ. Hãy thử lại.",
        ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối Catalog API. Hãy kiểm tra kết nối rồi thử lại.",
        ApiException { Kind: ApiErrorKind.InvalidResponse } => "Catalog API trả dữ liệu không đúng định dạng.",
        _ => "Không thể tải chi tiết sản phẩm. Hãy thử lại."
    };
}
