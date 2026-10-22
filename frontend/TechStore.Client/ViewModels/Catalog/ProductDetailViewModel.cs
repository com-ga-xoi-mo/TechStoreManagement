using TechStore.Client.Controls.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Catalog;

public sealed partial class ProductDetailViewModel : PageViewModel
{
    private readonly IProductRepository _products;
    private readonly IProductCommandRepository _commands;
    private readonly IDialogService _dialogs;
    private readonly IProductSpecRepository _specs;
    private SpecCatalog _catalog = SpecCatalog.Empty;
    private readonly INavigationService _navigation;
    private readonly ISessionStore _sessionStore;
    private ProductQuery? _returnCriteria;
    private Guid? _focusProductId;

    [ObservableProperty] private Guid productId;
    [ObservableProperty] private ProductDetail? product;
    [ObservableProperty] private bool isNotFound;
    [ObservableProperty] private bool canManage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string actionError = string.Empty;
    [ObservableProperty] private IReadOnlyList<SpecLine> specLines = [];
    [ObservableProperty] private IReadOnlyList<VariantLine> variantLines = [];

    public bool HasSpecs => Product?.Specs.Count > 0;
    public bool HasVariants => Product?.Variants.Count > 0;
    public bool HasNoSpecs => !HasSpecs;
    public bool HasNoVariants => !HasVariants;
    public bool HasActionError => !string.IsNullOrWhiteSpace(ActionError);
    public string ToggleStatusText => Product?.IsActive == true ? "Ngừng kinh doanh" : "Mở bán";

    /// <summary>Reminds the owner what still blocks selling (API design D15, UI design D19).</summary>
    public string SellingHint => Product switch
    {
        null => string.Empty,
        { IsActive: false, Variants.Count: > 0 } => "Sản phẩm đang không bán nên chưa chọn được khi lập đơn. Khi sẵn sàng, bấm Mở bán và bật Đang bán cho các biến thể cần bán.",
        { IsActive: false } => "Sản phẩm đang không bán nên chưa chọn được khi lập đơn. Bấm Mở bán khi sẵn sàng.",
        { Variants.Count: > 0 } p when p.Variants.All(v => !v.IsActive) => "Sản phẩm đang bán nhưng chưa có biến thể nào đang bán. Vào Sửa và tick Đang bán cho biến thể cần bán.",
        _ => string.Empty
    };

    public bool HasSellingHint => SellingHint.Length > 0;

    public ProductDetailViewModel(
        IProductRepository products,
        INavigationService navigation,
        ISessionStore sessionStore,
        IProductCommandRepository commands,
        IDialogService dialogs,
        IProductSpecRepository specs)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _specs = specs ?? throw new ArgumentNullException(nameof(specs));
        _sessionStore.SessionChanged += OnSessionChanged;
        CanManage = CatalogPermissions.CanManage(_sessionStore.Current);
    }

    protected override bool ManagesLoadState => true;

    private bool CanRunAction() => CanManage && !IsBusy && Product is not null;

    partial void OnCanManageChanged(bool value) => NotifyActionCommands();

    partial void OnIsBusyChanged(bool value) => NotifyActionCommands();

    partial void OnActionErrorChanged(string value) => OnPropertyChanged(nameof(HasActionError));

    private void NotifyActionCommands()
    {
        EditCommand.NotifyCanExecuteChanged();
        ToggleStatusCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Called before activation; it performs no I/O and clears any previously visible product.</summary>
    public void SetRoute(Guid id, ProductQuery? returnCriteria, Guid? focusProductId)
    {
        if (id == Guid.Empty) throw new ArgumentException("A non-empty product id is required.", nameof(id));
        if (ContentState != PageContentState.Idle)
            throw new InvalidOperationException("A product detail route must be configured before activation.");
        ProductId = id;
        Product = null;
        IsNotFound = false;
        ActionError = string.Empty;
        _returnCriteria = returnCriteria;
        _focusProductId = focusProductId;
    }

    protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        var requestedId = ProductId;
        Product = null;
        IsNotFound = false;
        ErrorMessage = string.Empty;
        ActionError = string.Empty;
        ContentState = PageContentState.Loading;
        return RunLatestRequestAsync(
            async token =>
            {
                var product = await _products.GetByIdAsync(requestedId, token);
                var catalog = product is null ? _catalog : await LoadSpecCatalogAsync(token);
                return (Product: product, Catalog: catalog);
            },
            loaded =>
            {
                _catalog = loaded.Catalog;
                var product = loaded.Product;
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
            loaded => loaded.Product is not null,
            () => false);
    }

    protected override void OnLatestRequestFailed(Exception exception)
    {
        Product = null;
        IsNotFound = false;
        ContentState = PageContentState.Error;
    }

    // Names are optional: when definitions cannot be loaded the page shows keys (UI design D16).
    private async Task<SpecCatalog> LoadSpecCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new SpecCatalog(await _specs.GetSpecDefinitionsAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return _catalog;
        }
    }

    private string SpecText(KeyValuePair<string, System.Text.Json.JsonElement> spec)
    {
        var definition = _catalog.FindByKey(spec.Key);
        return $"{SpecPresentation.KeyName(spec.Key, definition?.Label)}: {SpecPresentation.ValueText(spec.Value, definition?.Unit)}";
    }

    partial void OnProductChanged(ProductDetail? value)
    {
        SpecLines = value?.Specs
            .Select(spec =>
            {
                var definition = _catalog.FindByKey(spec.Key);
                return new SpecLine(SpecPresentation.KeyName(spec.Key, definition?.Label), SpecPresentation.ValueText(spec.Value, definition?.Unit));
            })
            .ToList() ?? [];
        VariantLines = value?.Variants
            .Select(variant => new VariantLine(variant, string.Join("   ·   ", variant.EffectiveSpecs.Select(SpecText))))
            .ToList() ?? [];
        OnPropertyChanged(nameof(HasSpecs));
        OnPropertyChanged(nameof(HasVariants));
        OnPropertyChanged(nameof(HasNoSpecs));
        OnPropertyChanged(nameof(HasNoVariants));
        OnPropertyChanged(nameof(ToggleStatusText));
        OnPropertyChanged(nameof(SellingHint));
        OnPropertyChanged(nameof(HasSellingHint));
        NotifyActionCommands();
    }

    protected override void OnDeactivated() => _sessionStore.SessionChanged -= OnSessionChanged;

    private void OnSessionChanged(object? sender, SessionChangedEventArgs args)
    {
        CanManage = CatalogPermissions.CanManage(_sessionStore.Current);
        CancelTrackedRequests();
        Product = null;
        IsNotFound = false;
        ActionError = string.Empty;
        _returnCriteria = null;
        _focusProductId = null;
        ContentState = args.Status == SessionStatus.Expired ? PageContentState.Error : PageContentState.Idle;
        ErrorMessage = args.Status == SessionStatus.Expired
            ? "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục."
            : string.Empty;
    }

    [RelayCommand]
    private void Back() => _navigation.Navigate(AppRoute.Products(_returnCriteria, _focusProductId ?? ProductId));

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private void Edit()
    {
        if (!CanRunAction()) return;
        _navigation.Navigate(AppRoute.ProductEdit(ProductId, _returnCriteria));
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task ToggleStatusAsync()
    {
        if (!CanRunAction() || Product is null) return;
        ActionError = string.Empty;

        var isActive = Product.IsActive;
        var request = isActive
            ? new DialogRequest(
                "Ngừng kinh doanh?",
                $"Sản phẩm {Product.Name} sẽ không còn được chọn khi lập đơn. Bạn có thể mở bán lại sau.",
                "Ngừng kinh doanh",
                "Hủy")
            : new DialogRequest(
                "Mở bán?",
                $"Sản phẩm {Product.Name} sẽ được chọn khi lập đơn, với các biến thể đang bán.",
                "Mở bán",
                "Hủy");

        var confirmation = await _dialogs.ConfirmAsync(request, ActivationToken);
        if (confirmation != DialogResult.Confirmed) return;

        IsBusy = true;
        try
        {
            var result = await _commands.SetStatusAsync(ProductId, !isActive, ActivationToken);
            if (result.Id == ProductId)
            {
                Product = result;
            }
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
            // Ignore cancelled activation
        }
        catch (ApiException ex)
        {
            ActionError = CatalogApiErrors.Describe(ex, "Sản phẩm");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task DeleteAsync()
    {
        if (!CanRunAction() || Product is null) return;
        ActionError = string.Empty;

        var request = new DialogRequest(
            "Xóa sản phẩm?",
            "Chỉ xóa được sản phẩm chưa phát sinh tồn kho, nhập hàng hay đơn hàng. Thao tác này không hoàn tác được. Nếu sản phẩm đã có giao dịch, hãy dùng Ngừng kinh doanh.",
            "Xóa vĩnh viễn",
            "Hủy");

        var confirmation = await _dialogs.ConfirmAsync(request, ActivationToken);
        if (confirmation != DialogResult.Confirmed) return;

        IsBusy = true;
        try
        {
            await _commands.DeleteAsync(ProductId, ActivationToken);
            _navigation.Navigate(AppRoute.Products(_returnCriteria));
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
            // Ignore cancelled activation
        }
        catch (ApiException ex)
        {
            var msg = CatalogApiErrors.Describe(ex, "Sản phẩm");
            if (ex.StatusCode == 409)
            {
                msg += " Bạn có thể dùng Ngừng kinh doanh để ẩn sản phẩm khỏi bán hàng.";
            }
            ActionError = msg;
        }
        finally
        {
            IsBusy = false;
        }
    }

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
