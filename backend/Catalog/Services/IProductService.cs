using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog.Services;

public record ProductDetailResult(bool Found, ProductDto? Data)
{
    public static ProductDetailResult NotFound() => new(false, null);
    public static ProductDetailResult Success(ProductDto data) => new(true, data);
}

public record ProductSearchResult(
    bool IsSuccess,
    PagedResultDto<ProductSummaryDto>? Data,
    Dictionary<string, string[]>? ValidationErrors)
{
    public static ProductSearchResult Success(PagedResultDto<ProductSummaryDto> data) =>
        new(true, data, null);

    public static ProductSearchResult Failed(Dictionary<string, string[]> errors) =>
        new(false, null, errors);
}

public record CreateProductResult(
    bool IsSuccess,
    ProductDto? Data,
    Dictionary<string, string[]>? ValidationErrors,
    IReadOnlyList<string>? ConflictingSkus,
    bool IsConflict)
{
    public static CreateProductResult Success(ProductDto data) =>
        new(true, data, null, null, false);

    public static CreateProductResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, null, errors, null, false);

    public static CreateProductResult Conflict(IReadOnlyList<string> conflictingSkus) =>
        new(false, null, null, conflictingSkus, true);
}

public interface IProductService
{
    Task<ProductDetailResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductSearchResult> SearchAsync(ProductSearchRequest request, CancellationToken cancellationToken = default);
    Task<CreateProductResult> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
}
