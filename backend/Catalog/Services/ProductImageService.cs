using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Common.Data;
using TechStore.Api.Common.Storage;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog.Services;

public sealed class ProductImageService(IProductRepository productRepository, IProductImageRepository imageRepository,
    IUnitOfWork unitOfWork, IObjectStorage storage, TimeProvider? timeProvider = null) : IProductImageService
{
    private const string LimitMessage = "A product can have at most 20 images.";
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<ProductImageCommandResult<ProductImageDto>> UploadAsync(Guid productId, ProductImageUpload upload, CancellationToken cancellationToken = default)
    {
        var info = ProductImageFileInspector.Inspect(upload.Content, out var error);
        if (info == null) return ProductImageCommandResult<ProductImageDto>.ValidationFailed(new() { ["file"] = [error!] });
        var product = await productRepository.GetDetailAsync(productId, cancellationToken);
        var rejected = CheckUpload(product, upload.VariantId);
        if (rejected != null) return rejected;
        var key = $"products/{productId:N}/{Guid.NewGuid():N}{info.Extension}";
        string url;
        try { url = await storage.PutAsync(key, upload.Content, info.ContentType, cancellationToken); }
        catch (ObjectStorageUnavailableException)
        {
            return ProductImageCommandResult<ProductImageDto>.StorageUnavailable("Image storage is unavailable. Try again later.");
        }
        var committed = false;
        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            product = await productRepository.GetForUpdateAsync(productId, cancellationToken);
            rejected = CheckUpload(product, upload.VariantId);
            if (rejected != null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return rejected;
            }
            var image = new ProductImage
            {
                ProductId = productId, VariantId = upload.VariantId, ObjectKey = key, Url = url,
                ContentType = info.ContentType, SizeBytes = upload.Content.Length, SortOrder = product!.Images.Count
            };
            imageRepository.Add(image);
            product.Images.Add(image);
            product.ImageUrl = ProductCoverImage.UrlOf(product.Images);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            committed = true;
            return ProductImageCommandResult<ProductImageDto>.Success(ProductDtoMapper.ToImageDto(image));
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (!committed) await storage.TryDeleteAsync(key, CancellationToken.None);
        }
    }

    private static ProductImageCommandResult<ProductImageDto>? CheckUpload(Product? product, Guid? variantId)
    {
        if (product == null) return ProductImageCommandResult<ProductImageDto>.NotFound();
        if (variantId.HasValue && !product.Variants.Any(v => v.Id == variantId))
            return ProductImageCommandResult<ProductImageDto>.ValidationFailed(new()
                { ["variantId"] = [$"Variant '{variantId}' does not belong to this product."] });
        return product.Images.Count >= 20 ? ProductImageCommandResult<ProductImageDto>.Conflict(LimitMessage) : null;
    }

    public async Task<ProductImageCommandResult<bool>> DeleteAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default)
    {
        string key;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var product = await productRepository.GetForUpdateAsync(productId, cancellationToken);
            var image = product?.Images.FirstOrDefault(i => i.Id == imageId);
            if (product == null || image == null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return ProductImageCommandResult<bool>.NotFound();
            }
            key = image.ObjectKey;
            imageRepository.Remove(image);
            product.Images.Remove(image);
            ProductCoverImage.Renumber(product.Images.OrderBy(i => i.SortOrder).ToList());
            product.ImageUrl = ProductCoverImage.UrlOf(product.Images);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
        await storage.TryDeleteAsync(key, cancellationToken);
        return ProductImageCommandResult<bool>.Success(true);
    }

    public async Task<ProductImageCommandResult<IReadOnlyList<ProductImageDto>>> SaveLayoutAsync(Guid productId, UpdateProductImagesLayoutRequest request, CancellationToken cancellationToken = default)
    {
        var errors = ProductImageLayoutValidator.Validate(request);
        if (errors.Count > 0) return ProductImageCommandResult<IReadOnlyList<ProductImageDto>>.ValidationFailed(errors);
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var product = await productRepository.GetForUpdateAsync(productId, cancellationToken);
            if (product == null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return ProductImageCommandResult<IReadOnlyList<ProductImageDto>>.NotFound();
            }
            var items = request.Items!;
            if (!product.Images.Select(i => i.Id).ToHashSet().SetEquals(items.Select(i => i.Id!.Value)))
                errors["items"] = ["The layout must list every image of the product exactly once."];
            for (var i = 0; i < items.Count; i++)
                if (items[i].VariantId is { } variant && !product.Variants.Any(v => v.Id == variant))
                    errors[$"items[{i}].variantId"] = [$"Variant '{variant}' does not belong to this product."];
            if (errors.Count > 0)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return ProductImageCommandResult<IReadOnlyList<ProductImageDto>>.ValidationFailed(errors);
            }
            var byId = product.Images.ToDictionary(i => i.Id);
            var ordered = items.Select(item => byId[item.Id!.Value]).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].VariantId = items[i].VariantId;
                ordered[i].UpdatedAt = _time.GetUtcNow().UtcDateTime;
            }
            ProductCoverImage.Renumber(ordered);
            product.ImageUrl = ProductCoverImage.UrlOf(ordered);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return ProductImageCommandResult<IReadOnlyList<ProductImageDto>>.Success(ordered.Select(ProductDtoMapper.ToImageDto).ToList());
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
