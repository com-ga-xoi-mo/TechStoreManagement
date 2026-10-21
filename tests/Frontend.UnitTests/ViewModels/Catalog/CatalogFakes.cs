using TechStore.Client.Services.Api;
using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

internal sealed class AllowAllPreparer : IDestinationPreparer
{
    public bool Prepare(AppRoute route) => true;
}

internal sealed class ScriptedDialogService : IDialogService
{
    public DialogResult Result { get; set; } = DialogResult.Confirmed;
    public List<DialogRequest> Requests { get; } = [];

    public Task<DialogResult> ConfirmAsync(DialogRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(Result);
    }

    public Task<DialogResult> InformAsync(DialogRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(Result);
    }
}

internal sealed class RecordingProductCommands : IProductCommandRepository
{
    public List<CreateProductRequest> Created { get; } = [];
    public List<(Guid Id, UpdateProductRequest Request)> Updated { get; } = [];
    public List<(Guid Id, bool IsActive)> StatusChanges { get; } = [];
    public List<Guid> Deleted { get; } = [];

    public Func<CreateProductRequest, Task<ProductDetail>> OnCreate { get; set; } =
        req => Task.FromResult(CatalogSamples.Detail(CatalogSamples.ProductId));

    public Func<Guid, UpdateProductRequest, Task<ProductDetail>> OnUpdate { get; set; } =
        (id, req) => Task.FromResult(CatalogSamples.Detail(id));

    public Func<Guid, bool, Task<ProductDetail>> OnSetStatus { get; set; } =
        (id, active) => Task.FromResult(CatalogSamples.Detail(id, isActive: active));

    public Func<Guid, Task> OnDelete { get; set; } =
        id => Task.CompletedTask;

    public Task<ProductDetail> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        Created.Add(request);
        return OnCreate(request);
    }

    public Task<ProductDetail> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        Updated.Add((id, request));
        return OnUpdate(id, request);
    }

    public Task<ProductDetail> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        StatusChanges.Add((id, isActive));
        return OnSetStatus(id, isActive);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Deleted.Add(id);
        return OnDelete(id);
    }
}

internal sealed class StubProductReader : IProductRepository
{
    public int GetByIdCalls { get; private set; }
    public int SearchCalls { get; private set; }

    public Func<Guid, Task<ProductDetail?>> OnGetById { get; set; } =
        id => Task.FromResult<ProductDetail?>(CatalogSamples.Detail(id));

    public Task<ProductDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdCalls++;
        return OnGetById(id);
    }

    public Task<ProductPage> SearchAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        SearchCalls++;
        return Task.FromResult(new ProductPage([], query.Page, query.PageSize, 0, 0));
    }
}

internal sealed class StubCategoryReader : ICategoryRepository
{
    public bool Fail { get; set; }
    public int RequestCount { get; private set; }

    public Task<IReadOnlyList<CategoryOption>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        RequestCount++;
        if (Fail)
        {
            throw new InvalidOperationException("Category load failed.");
        }

        IReadOnlyList<CategoryOption> options =
        [
            new(CatalogSamples.CategoryId, null, "Điện thoại", "Điện thoại"),
            new(Guid.NewGuid(), null, "Laptop", "Laptop")
        ];
        return Task.FromResult(options);
    }
}

internal static class CatalogSamples
{
    public static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static ProductDetail Detail(
        Guid id,
        bool isActive = true,
        IReadOnlyList<ProductVariant>? variants = null,
        IReadOnlyDictionary<string, JsonElement>? specs = null) => new(
        Id: id,
        CategoryId: CategoryId,
        CategoryName: "Điện thoại",
        Name: "Phone",
        Sku: "P-1",
        Barcode: null,
        Brand: "Tech",
        BasePrice: 10000000m,
        CostPrice: 8000000m,
        Description: "Sample description",
        ImageUrl: null,
        Specs: specs ?? new Dictionary<string, JsonElement>(),
        IsSerialTracked: true,
        IsActive: isActive,
        CreatedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt: null,
        Variants: variants ?? [Variant(id, "P-1-V1", isActive)]);

    public static ProductVariant Variant(Guid productId, string sku, bool isActive = true) => new(
        Id: Guid.NewGuid(),
        ProductId: productId,
        VariantName: "Standard",
        Sku: sku,
        Barcode: null,
        Price: 10000000m,
        Specs: new Dictionary<string, JsonElement>(),
        EffectiveSpecs: new Dictionary<string, JsonElement>(),
        IsActive: isActive,
        CreatedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt: null);

    public static InMemorySessionStore Session(string role)
    {
        var store = new InMemorySessionStore();
        store.SetSession("token", "refresh", "user", "User", role);
        return store;
    }
}

/// <summary>Spec definitions and suggestions for editor and detail tests.</summary>
internal sealed class StubSpecRepository : IProductSpecRepository
{
    public bool FailDefinitions { get; set; }
    public List<SpecDefinition> Definitions { get; } =
    [
        new("ram_gb", "RAM", "GB", SpecValueKind.Number),
        new("storage_gb", "Bộ nhớ trong", "GB", SpecValueKind.Number),
        new("cpu", "Vi xử lý (CPU)", null, SpecValueKind.Text),
        new("wireless", "Không dây", null, SpecValueKind.Boolean)
    ];

    public Task<IReadOnlyList<SpecKeyOption>> GetSpecKeysAsync(Guid? categoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SpecKeyOption>>([]);

    public Task<IReadOnlyList<SpecDefinition>> GetSpecDefinitionsAsync(CancellationToken cancellationToken = default) =>
        FailDefinitions
            ? Task.FromException<IReadOnlyList<SpecDefinition>>(new ApiException(ApiErrorKind.Network, "spec definitions"))
            : Task.FromResult<IReadOnlyList<SpecDefinition>>(Definitions.ToList());
}
