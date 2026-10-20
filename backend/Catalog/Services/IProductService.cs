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

public enum ProductCommandStatus { Success, NotFound, ValidationFailed, Conflict }

public record ProductCommandResult(
    ProductCommandStatus Status,
    ProductDto? Data,
    Dictionary<string, string[]>? ValidationErrors,
    string? ConflictMessage)
{
    public static ProductCommandResult Success(ProductDto? data) => new(ProductCommandStatus.Success, data, null, null);
    public static ProductCommandResult NotFound() => new(ProductCommandStatus.NotFound, null, null, null);
    public static ProductCommandResult ValidationFailed(Dictionary<string, string[]> errors) => new(ProductCommandStatus.ValidationFailed, null, errors, null);
    public static ProductCommandResult Conflict(string message) => new(ProductCommandStatus.Conflict, null, null, message);
}

public interface IProductService
{
    Task<ProductDetailResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductSearchResult> SearchAsync(ProductSearchRequest request, CancellationToken cancellationToken = default);
    Task<CreateProductResult> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductCommandResult> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductCommandResult> SetStatusAsync(Guid id, UpdateProductStatusRequest request, CancellationToken cancellationToken = default);
    Task<ProductCommandResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
