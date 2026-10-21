using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Catalog;

public sealed partial class ProductEditorViewModel : PageViewModel
{
    private static readonly Regex VariantErrorRegex = new(@"^variants\[(\d+)\]\.(\w+)$", RegexOptions.Compiled);

    private readonly IProductRepository _products;
    private readonly IProductCommandRepository _commands;
    private readonly ICategoryRepository _categories;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ISessionStore _sessionStore;
    private readonly IProductSpecRepository _specs;
    private readonly IProductBrandRepository _brands;
    private IReadOnlyList<BrandOption> _brandOptions = [];
    private SpecCatalog _catalog = SpecCatalog.Empty;

    private ProductQuery? _returnQuery;
    private ProductDraft? _baseline;

    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private bool isNotFound;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private string generalError = string.Empty;
    [ObservableProperty] private string pageTitle = "Thêm sản phẩm mới";
    [ObservableProperty] private bool canManage;
    [ObservableProperty] private string specCatalogMessage = string.Empty;
    [ObservableProperty] private IReadOnlyList<SpecChoice> brandSuggestions = [];
    [ObservableProperty] private string brandHint = string.Empty;

    [ObservableProperty] private Guid? productId;
    [ObservableProperty] private Guid? selectedCategoryId;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string sku = string.Empty;
    [ObservableProperty] private string brand = string.Empty;
    [ObservableProperty] private string basePriceText = string.Empty;
    [ObservableProperty] private string costPriceText = string.Empty;
    [ObservableProperty] private string barcode = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private string imageUrl = string.Empty;
    [ObservableProperty] private bool isSerialTracked = true;
    [ObservableProperty] private IReadOnlyList<CategoryOption> categories = [];

    // Margin properties
    [ObservableProperty] private string marginText = string.Empty;
    [ObservableProperty] private bool hasMargin;
    [ObservableProperty] private bool isMarginLoss;

    // Field errors
    [ObservableProperty] private string? nameError;
    [ObservableProperty] private string? skuError;
    [ObservableProperty] private string? brandError;
    [ObservableProperty] private string? categoryError;
    [ObservableProperty] private string? basePriceError;
    [ObservableProperty] private string? costPriceError;
    [ObservableProperty] private string? barcodeError;
    [ObservableProperty] private string? descriptionError;
    [ObservableProperty] private string? imageUrlError;
    [ObservableProperty] private string? specsError;

    public bool HasNameError => !string.IsNullOrWhiteSpace(NameError);
    public bool HasSkuError => !string.IsNullOrWhiteSpace(SkuError);
    public bool HasBrandError => !string.IsNullOrWhiteSpace(BrandError);
    public bool HasCategoryError => !string.IsNullOrWhiteSpace(CategoryError);
    public bool HasBasePriceError => !string.IsNullOrWhiteSpace(BasePriceError);
    public bool HasCostPriceError => !string.IsNullOrWhiteSpace(CostPriceError);
    public bool HasBarcodeError => !string.IsNullOrWhiteSpace(BarcodeError);
    public bool HasDescriptionError => !string.IsNullOrWhiteSpace(DescriptionError);
    public bool HasImageUrlError => !string.IsNullOrWhiteSpace(ImageUrlError);
    public bool HasSpecsError => !string.IsNullOrWhiteSpace(SpecsError);
    public bool HasGeneralError => !string.IsNullOrWhiteSpace(GeneralError);
    public bool HasSpecCatalogMessage => !string.IsNullOrWhiteSpace(SpecCatalogMessage);
    public bool HasBrandSuggestions => BrandSuggestions.Count > 0;
    public bool HasBrandHint => !string.IsNullOrWhiteSpace(BrandHint);

    public bool HasVariants => Variants.Count > 0;
    public bool IsBarcodeEnabled => !HasVariants;

    public ObservableCollection<SpecRowItem> Specs { get; } = [];
    public ObservableCollection<VariantRowItem> Variants { get; } = [];

    public bool CanSave => !IsSaving && ContentState == PageContentState.Content && CanManage;

    public ProductEditorViewModel(
        IProductRepository products,
        IProductCommandRepository commands,
        ICategoryRepository categories,
        INavigationService navigation,
        IDialogService dialogs,
        ISessionStore sessionStore,
        IProductSpecRepository specs,
        IProductBrandRepository brands)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _categories = categories ?? throw new ArgumentNullException(nameof(categories));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _specs = specs ?? throw new ArgumentNullException(nameof(specs));
        _brands = brands ?? throw new ArgumentNullException(nameof(brands));

        _sessionStore.SessionChanged += OnSessionChanged;
        CanManage = CatalogPermissions.CanManage(_sessionStore.Current);

        Variants.CollectionChanged += OnVariantsCollectionChanged;
    }

    protected override bool ManagesLoadState => true;

    private void OnVariantsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (HasVariants)
        {
            Barcode = string.Empty;
        }
        OnPropertyChanged(nameof(HasVariants));
        OnPropertyChanged(nameof(IsBarcodeEnabled));
    }

    partial void OnNameErrorChanged(string? value) => OnPropertyChanged(nameof(HasNameError));
    partial void OnSkuErrorChanged(string? value) => OnPropertyChanged(nameof(HasSkuError));
    partial void OnBrandErrorChanged(string? value) => OnPropertyChanged(nameof(HasBrandError));
    partial void OnCategoryErrorChanged(string? value) => OnPropertyChanged(nameof(HasCategoryError));
    partial void OnBasePriceErrorChanged(string? value) => OnPropertyChanged(nameof(HasBasePriceError));
    partial void OnCostPriceErrorChanged(string? value) => OnPropertyChanged(nameof(HasCostPriceError));
    partial void OnBarcodeErrorChanged(string? value) => OnPropertyChanged(nameof(HasBarcodeError));
    partial void OnDescriptionErrorChanged(string? value) => OnPropertyChanged(nameof(HasDescriptionError));
    partial void OnImageUrlErrorChanged(string? value) => OnPropertyChanged(nameof(HasImageUrlError));
    partial void OnSpecsErrorChanged(string? value) => OnPropertyChanged(nameof(HasSpecsError));
    partial void OnGeneralErrorChanged(string value) => OnPropertyChanged(nameof(HasGeneralError));
    partial void OnSpecCatalogMessageChanged(string value) => OnPropertyChanged(nameof(HasSpecCatalogMessage));
    partial void OnBrandSuggestionsChanged(IReadOnlyList<SpecChoice> value) => OnPropertyChanged(nameof(HasBrandSuggestions));
    partial void OnBrandHintChanged(string value) => OnPropertyChanged(nameof(HasBrandHint));
    partial void OnBrandChanged(string value) => UpdateBrandSuggestions();

    // Suggestions are help only: the server stores the existing spelling anyway (API design D14).
    private void UpdateBrandSuggestions()
    {
        var text = Brand.Trim();
        var exact = _brandOptions.FirstOrDefault(b => string.Equals(b.Name, text, StringComparison.OrdinalIgnoreCase));
        BrandHint = exact is not null && !string.Equals(exact.Name, text, StringComparison.Ordinal)
            ? $"Đã có thương hiệu “{exact.Name}”; sản phẩm sẽ dùng cách viết này."
            : string.Empty;
        var slug = SpecKeyGenerator.Slug(text);
        BrandSuggestions = exact is not null || slug.Length == 0
            ? []
            : _brandOptions
                .Where(b => SpecKeyGenerator.Slug(b.Name).Contains(slug, StringComparison.Ordinal))
                .OrderByDescending(b => b.ProductCount)
                .Take(5)
                .Select(b => new SpecChoice(b.Name, SpecPresentation.WithCount(b.Name, b.ProductCount)))
                .ToList();
    }

    private async Task LoadBrandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _brandOptions = await _brands.GetBrandsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _brandOptions = [];
        }
    }

    [RelayCommand]
    private void PickBrand(object? parameter)
    {
        if (parameter is SpecChoice choice) Brand = choice.Value;
    }

    partial void OnIsSavingChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnCanManageChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    protected override void OnContentStateChanged()
    {
        base.OnContentStateChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnBasePriceTextChanged(string value) => UpdateMargin();
    partial void OnCostPriceTextChanged(string value) => UpdateMargin();

    private void UpdateMargin()
    {
        if (ProductInputParser.TryParseMoney(BasePriceText, out var basePrice)
            && ProductInputParser.TryParseMoney(CostPriceText, out var costPrice))
        {
            var info = CatalogPresentation.MarginText(basePrice, costPrice);
            if (info is not null)
            {
                MarginText = info.Text;
                IsMarginLoss = info.IsLoss;
                HasMargin = true;
                return;
            }
        }

        MarginText = string.Empty;
        HasMargin = false;
        IsMarginLoss = false;
    }

    public void SetRoute(Guid? productId, ProductQuery? returnQuery)
    {
        if (ContentState != PageContentState.Idle)
            throw new InvalidOperationException("An editor route must be configured before activation.");

        ProductId = productId;
        _returnQuery = returnQuery;
        IsEditMode = productId.HasValue;
        PageTitle = IsEditMode ? "Chỉnh sửa sản phẩm" : "Thêm sản phẩm mới";
    }

    protected override async Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        Specs.Clear();
        Variants.Clear();
        GeneralError = string.Empty;
        ClearAllErrors();
        ContentState = PageContentState.Loading;

        try
        {
            Categories = await _categories.GetTreeAsync(cancellationToken);
            await LoadSpecCatalogAsync(cancellationToken);
            await LoadBrandsAsync(cancellationToken);

            if (IsEditMode && ProductId is { } id)
            {
                var product = await _products.GetByIdAsync(id, cancellationToken);
                if (product is null)
                {
                    IsNotFound = true;
                    ContentState = PageContentState.Empty;
                    return false;
                }

                IsNotFound = false;
                SelectedCategoryId = product.CategoryId;
                Name = product.Name;
                PageTitle = $"Chỉnh sửa: {product.Name}";
                Sku = product.Sku;
                Brand = product.Brand;
                BasePriceText = product.BasePrice.ToString("0.##", CultureInfo.InvariantCulture);
                CostPriceText = product.CostPrice.ToString("0.##", CultureInfo.InvariantCulture);
                Barcode = product.Barcode ?? string.Empty;
                Description = product.Description ?? string.Empty;
                ImageUrl = product.ImageUrl ?? string.Empty;
                IsSerialTracked = product.IsSerialTracked;

                foreach (var spec in ProductInputParser.SpecsFromJson(product.Specs))
                {
                    Specs.Add(WithCatalog(SpecRowItem.From(spec)));
                }

                foreach (var v in product.Variants)
                {
                    var item = new VariantRowItem(v.Id)
                    {
                        VariantName = v.VariantName,
                        Sku = v.Sku,
                        PriceText = v.Price.ToString("0.##", CultureInfo.InvariantCulture),
                        Barcode = v.Barcode ?? string.Empty,
                        IsActive = v.IsActive
                    };

                    foreach (var s in ProductInputParser.SpecsFromJson(v.Specs))
                    {
                        item.Specs.Add(WithCatalog(SpecRowItem.From(s)));
                    }

                    Variants.Add(item);
                }
            }
            else
            {
                IsNotFound = false;
                SelectedCategoryId = null;
                Name = string.Empty;
                PageTitle = "Thêm sản phẩm mới";
                Sku = string.Empty;
                Brand = string.Empty;
                BasePriceText = string.Empty;
                CostPriceText = string.Empty;
                Barcode = string.Empty;
                Description = string.Empty;
                ImageUrl = string.Empty;
                IsSerialTracked = true;
            }

            _baseline = BuildDraft();
            UpdateMargin();
            ContentState = PageContentState.Content;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = GetSafeErrorMessage(ex);
            ContentState = PageContentState.Error;
            return false;
        }
    }

    // Spec names are optional help: without them the editor still works with generated keys.
    private async Task LoadSpecCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            _catalog = new SpecCatalog(await _specs.GetSpecDefinitionsAsync(cancellationToken));
            SpecCatalogMessage = string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _catalog = SpecCatalog.Empty;
            SpecCatalogMessage = "Không tải được danh sách tên thông số. Bạn vẫn nhập được thông số; tên mới sẽ được lưu khi lưu sản phẩm.";
        }
    }

    private SpecRowItem WithCatalog(SpecRowItem row)
    {
        row.AttachCatalog(_catalog);
        return row;
    }

    private IEnumerable<SpecRowItem> AllSpecRows() => Specs.Concat(Variants.SelectMany(v => v.Specs));

    public ProductDraft BuildDraft() => new(
        CategoryId: SelectedCategoryId,
        Name: Name,
        Sku: Sku,
        Brand: Brand,
        BasePrice: BasePriceText,
        CostPrice: CostPriceText,
        Barcode: Barcode,
        Description: Description,
        ImageUrl: ImageUrl,
        IsSerialTracked: IsSerialTracked,
        Specs: Specs.Select(s => s.ToDraft()).ToList(),
        Variants: Variants.Select(v => v.ToDraft()).ToList(),
        SpecLabels: AllSpecRows().Select(row => row.ToLabelDraft()).OfType<SpecLabelDraft>().ToList());

    public bool HasUnsavedChanges
    {
        get
        {
            if (_baseline is null) return false;
            var current = BuildDraft();

            if (current.CategoryId != _baseline.CategoryId
                || current.Name != _baseline.Name
                || current.Sku != _baseline.Sku
                || current.Brand != _baseline.Brand
                || current.BasePrice != _baseline.BasePrice
                || current.CostPrice != _baseline.CostPrice
                || current.Barcode != _baseline.Barcode
                || current.Description != _baseline.Description
                || current.ImageUrl != _baseline.ImageUrl
                || current.IsSerialTracked != _baseline.IsSerialTracked)
            {
                return true;
            }

            if (!current.Specs.SequenceEqual(_baseline.Specs)
                || !(current.SpecLabels ?? []).SequenceEqual(_baseline.SpecLabels ?? []))
            {
                return true;
            }

            if (current.Variants.Count != _baseline.Variants.Count)
            {
                return true;
            }

            for (var i = 0; i < current.Variants.Count; i++)
            {
                var curV = current.Variants[i];
                var baseV = _baseline.Variants[i];
                if (curV.Id != baseV.Id
                    || curV.VariantName != baseV.VariantName
                    || curV.Sku != baseV.Sku
                    || curV.Price != baseV.Price
                    || curV.Barcode != baseV.Barcode
                    || curV.IsActive != baseV.IsActive
                    || !curV.Specs.SequenceEqual(baseV.Specs))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [RelayCommand]
    private void AddSpec() => Specs.Add(WithCatalog(new SpecRowItem()));

    // Template-bound commands take object (see RemoveSpec).
    [RelayCommand]
    private void PickSpecSuggestion(object? parameter)
    {
        if (parameter is SpecSuggestion suggestion) suggestion.Row.Pick(suggestion.Definition);
    }

    [RelayCommand]
    private void ToggleSpecKeyEditor(object? parameter)
    {
        if (parameter is SpecRowItem row) row.IsKeyEditorOpen = !row.IsKeyEditorOpen;
    }

    // CommandParameter comes from a binding inside an item template; while a row is removed it can briefly be
    // the page DataContext, so the command takes object and ignores anything else.
    [RelayCommand]
    private void RemoveSpec(object? parameter)
    {
        if (parameter is SpecRowItem row) Specs.Remove(row);
    }

    [RelayCommand]
    private void AddVariant() => Variants.Add(new VariantRowItem(null));

    [RelayCommand]
    private void RemoveVariant(object? parameter)
    {
        if (parameter is VariantRowItem { IsSaved: false } row)
        {
            Variants.Remove(row);
        }
    }

    [RelayCommand]
    private void AddVariantSpec(object? parameter)
    {
        if (parameter is VariantRowItem variant) variant.Specs.Add(WithCatalog(new SpecRowItem()));
    }

    [RelayCommand]
    private void RemoveVariantSpec(object? parameter)
    {
        if (parameter is not SpecRowItem spec) return;
        var owner = Variants.FirstOrDefault(v => v.Specs.Contains(spec));
        owner?.Specs.Remove(spec);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanSave) return;
        ClearAllErrors();
        GeneralError = string.Empty;

        var draft = BuildDraft();
        var validation = ProductDraftValidator.Validate(draft, isCreate: !IsEditMode);

        ApplyFieldErrors(validation.FieldErrors);

        for (var i = 0; i < validation.ProductSpecRowErrors.Count && i < Specs.Count; i++)
        {
            Specs[i].Error = validation.ProductSpecRowErrors[i];
        }

        for (var v = 0; v < validation.VariantSpecRowErrors.Count && v < Variants.Count; v++)
        {
            for (var s = 0; s < validation.VariantSpecRowErrors[v].Count && s < Variants[v].Specs.Count; s++)
            {
                Variants[v].Specs[s].Error = validation.VariantSpecRowErrors[v][s];
            }
        }

        foreach (var row in AllSpecRows().Where(r => r.Key.Length == 0 && r.HasError))
        {
            row.Error = "Hãy nhập tên thông số có chữ hoặc số.";
        }

        if (!validation.IsValid) return;

        IsSaving = true;
        try
        {
            if (!IsEditMode)
            {
                var created = await _commands.CreateAsync(validation.Create!, ActivationToken);
                _navigation.Navigate(AppRoute.ProductDetail(created.Id, _returnQuery, created.Id));
            }
            else
            {
                var updated = await _commands.UpdateAsync(ProductId!.Value, validation.Update!, ActivationToken);
                _navigation.Navigate(AppRoute.ProductDetail(updated.Id, _returnQuery, updated.Id));
            }
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
            // Ignore cancelled operation
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 400 && ex.FieldErrors.Count > 0)
            {
                var joinedErrors = ex.FieldErrors.ToDictionary(
                    kvp => kvp.Key,
                    kvp => string.Join(" ", kvp.Value),
                    StringComparer.OrdinalIgnoreCase);
                ApplyFieldErrors(joinedErrors);
            }
            else
            {
                GeneralError = CatalogApiErrors.Describe(ex, "Sản phẩm");
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ApplyFieldErrors(IReadOnlyDictionary<string, string> errors)
    {
        foreach (var (key, error) in errors)
        {
            var match = VariantErrorRegex.Match(key);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out var index) && index >= 0 && index < Variants.Count)
                {
                    var field = match.Groups[2].Value;
                    switch (field)
                    {
                        case "variantName":
                            Variants[index].NameError = error;
                            break;
                        case "sku":
                            Variants[index].SkuError = error;
                            break;
                        case "price":
                            Variants[index].PriceError = error;
                            break;
                        case "barcode":
                            Variants[index].BarcodeError = error;
                            break;
                        case "specs":
                            Variants[index].SpecsError = error;
                            break;
                        default:
                            AppendGeneralError(error);
                            break;
                    }
                }
                else
                {
                    AppendGeneralError(error);
                }
                continue;
            }

            if (key.StartsWith("specLabels.", StringComparison.OrdinalIgnoreCase))
            {
                var specKey = key["specLabels.".Length..];
                var rows = AllSpecRows().Where(r => r.Key == specKey).ToList();
                if (rows.Count == 0) AppendGeneralError(error);
                foreach (var row in rows) row.Error = error;
                continue;
            }

            switch (key.ToLowerInvariant())
            {
                case "categoryid":
                    CategoryError = error;
                    break;
                case "name":
                    NameError = error;
                    break;
                case "sku":
                    SkuError = error;
                    break;
                case "brand":
                    BrandError = error;
                    break;
                case "baseprice":
                    BasePriceError = error;
                    break;
                case "costprice":
                    CostPriceError = error;
                    break;
                case "barcode":
                    BarcodeError = error;
                    break;
                case "description":
                    DescriptionError = error;
                    break;
                case "imageurl":
                    ImageUrlError = error;
                    break;
                case "specs":
                    SpecsError = error;
                    break;
                default:
                    AppendGeneralError(error);
                    break;
            }
        }
    }

    private void AppendGeneralError(string error)
    {
        if (string.IsNullOrWhiteSpace(GeneralError))
        {
            GeneralError = error;
        }
        else
        {
            GeneralError += " " + error;
        }
    }

    private void ClearAllErrors()
    {
        NameError = null;
        SkuError = null;
        BrandError = null;
        CategoryError = null;
        BasePriceError = null;
        CostPriceError = null;
        BarcodeError = null;
        DescriptionError = null;
        ImageUrlError = null;
        SpecsError = null;

        foreach (var spec in Specs)
        {
            spec.Error = null;
        }

        foreach (var variant in Variants)
        {
            variant.ClearErrors();
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (HasUnsavedChanges)
        {
            var request = new DialogRequest(
                "Bỏ thay đổi?",
                "Các thay đổi chưa lưu sẽ bị mất.",
                "Bỏ thay đổi",
                "Tiếp tục chỉnh sửa");

            var result = await _dialogs.ConfirmAsync(request, ActivationToken);
            if (result != DialogResult.Confirmed) return;
        }

        if (IsEditMode && ProductId is { } id)
        {
            _navigation.Navigate(AppRoute.ProductDetail(id, _returnQuery, id));
        }
        else
        {
            _navigation.Navigate(AppRoute.Products(_returnQuery));
        }
    }

    [RelayCommand]
    private void BackToList() => _navigation.Navigate(AppRoute.Products(_returnQuery));

    protected override void OnDeactivated()
    {
        base.OnDeactivated();
        _sessionStore.SessionChanged -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, SessionChangedEventArgs args)
    {
        CanManage = CatalogPermissions.CanManage(_sessionStore.Current);
        if (!CanManage)
        {
            GeneralError = "Bạn không có quyền thực hiện thao tác này.";
        }
        SaveCommand.NotifyCanExecuteChanged();
    }

    protected override string GetSafeErrorMessage(Exception exception) => exception switch
    {
        ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.",
        ApiException { StatusCode: 403 } => "Bạn không có quyền thực hiện thao tác này.",
        ApiException { Kind: ApiErrorKind.Timeout } => "Yêu cầu Catalog quá thời gian chờ. Hãy thử lại.",
        ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối Catalog API. Hãy kiểm tra kết nối rồi thử lại.",
        ApiException { Kind: ApiErrorKind.InvalidResponse } => "Catalog API trả dữ liệu không đúng định dạng.",
        _ => "Không thể tải dữ liệu sản phẩm. Hãy thử lại."
    };
}
