using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog.Services;

public sealed record ProductImageUpload(string? FileName, byte[] Content, Guid? VariantId);

public enum ProductImageCommandStatus { Success, NotFound, ValidationFailed, Conflict, StorageUnavailable }

public sealed record ProductImageCommandResult<T>(ProductImageCommandStatus Status, T? Data, Dictionary<string, string[]>? ValidationErrors, string? Message)
{
    public static ProductImageCommandResult<T> Success(T data) => new(ProductImageCommandStatus.Success, data, null, null);
    public static ProductImageCommandResult<T> NotFound() => new(ProductImageCommandStatus.NotFound, default, null, null);
    public static ProductImageCommandResult<T> ValidationFailed(Dictionary<string, string[]> errors) => new(ProductImageCommandStatus.ValidationFailed, default, errors, null);
    public static ProductImageCommandResult<T> Conflict(string message) => new(ProductImageCommandStatus.Conflict, default, null, message);
    public static ProductImageCommandResult<T> StorageUnavailable(string message) => new(ProductImageCommandStatus.StorageUnavailable, default, null, message);
}

public interface IProductImageService
{
    Task<ProductImageCommandResult<ProductImageDto>> UploadAsync(Guid productId, ProductImageUpload upload, CancellationToken cancellationToken = default);
    Task<ProductImageCommandResult<bool>> DeleteAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default);
    Task<ProductImageCommandResult<IReadOnlyList<ProductImageDto>>> SaveLayoutAsync(Guid productId, UpdateProductImagesLayoutRequest request, CancellationToken cancellationToken = default);
}
