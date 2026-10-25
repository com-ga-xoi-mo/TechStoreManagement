using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory.Services;

public class InventoryService(
    IInventoryStockRepository stockRepository,
    IProductRepository productRepository,
    ISerialImeiRepository serialRepository,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : IInventoryService
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    public async Task<InventoryStockSearchResult> GetStocksAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = request.Page.GetValueOrDefault(1);
        if (page < 1) page = 1;

        var pageSize = request.PageSize.GetValueOrDefault(20);
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var criteria = new InventoryStockSearchCriteria(
            request.Search,
            request.CategoryName,
            request.ProductId,
            request.VariantId,
            request.LowStock,
            request.OutOfStock,
            request.InStock,
            page,
            pageSize);

        var (items, totalCount) = await stockRepository.SearchStocksAsync(criteria, cancellationToken);

        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        var dtos = items.Select(MapToDto).ToList();

        var paged = new PagedResultDto<InventoryStockDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return InventoryStockSearchResult.Success(paged);
    }

    public async Task<InitializeStockResult> InitializeStockAsync(
        InitializeStockRequest request,
        Guid performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (!request.ProductId.HasValue || request.ProductId.Value == Guid.Empty)
        {
            errors["productId"] = ["Product ID is required."];
        }

        if (!request.Quantity.HasValue)
        {
            errors["quantity"] = ["Quantity is required."];
        }
        else if (request.Quantity.Value <= 0)
        {
            errors["quantity"] = ["Opening quantity must be greater than zero."];
        }

        if (request.MinStockAlert.HasValue && request.MinStockAlert.Value < 0)
        {
            errors["minStockAlert"] = ["MinStockAlert must be greater than or equal to zero."];
        }

        Product? validatedProduct = null;
        if (request.ProductId.HasValue && request.ProductId.Value != Guid.Empty)
        {
            validatedProduct = await productRepository.GetWithVariantsAsync(request.ProductId.Value, cancellationToken);
            if (validatedProduct == null)
            {
                errors["productId"] = [$"Product with ID '{request.ProductId.Value}' does not exist."];
            }
            else if (request.VariantId.HasValue && request.VariantId.Value != Guid.Empty)
            {
                if (validatedProduct.Variants == null || validatedProduct.Variants.All(v => v.Id != request.VariantId.Value))
                {
                    errors["variantId"] = [$"Variant with ID '{request.VariantId.Value}' does not belong to product '{request.ProductId.Value}'."];
                }
            }
        }

        if (errors.Count > 0)
        {
            return InitializeStockResult.ValidationFailed(errors);
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await stockRepository.GetByProductAndVariantAsync(
                request.ProductId!.Value,
                request.VariantId,
                cancellationToken);

            if (existing != null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return InitializeStockResult.Conflict(
                    $"Stock balance for product '{request.ProductId.Value}' and variant '{request.VariantId}' has already been initialized.");
            }

            var stock = new InventoryStock
            {
                ProductId = request.ProductId.Value,
                VariantId = request.VariantId,
                Quantity = request.Quantity!.Value,
                ReservedQuantity = 0,
                MinStockAlert = request.MinStockAlert.GetValueOrDefault(5)
            };
            stockRepository.Add(stock);

            var movement = new InventoryMovement
            {
                InventoryStock = stock,
                MovementType = InventoryMovementType.OpeningBalance,
                QuantityChange = request.Quantity.Value,
                QuantityAfter = request.Quantity.Value,
                PerformedByUserId = performedByUserId,
                Reason = request.Reason ?? "Initial stock balance"
            };
            stockRepository.AddMovement(movement);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            stock.Product = validatedProduct!;
            stock.Variant = request.VariantId.HasValue
                ? validatedProduct!.Variants?.FirstOrDefault(v => v.Id == request.VariantId.Value)
                : null;

            return InitializeStockResult.Success(MapToDto(new InventoryStockView(stock, 0)));
        }
        catch (DuplicateKeyException ex)
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            return InitializeStockResult.Conflict($"Stock initialization conflict: {ex.Message}");
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<DeleteInventoryStockResult> DeleteStockAsync(
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        if (stockId == Guid.Empty)
            return DeleteInventoryStockResult.NotFound();

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var stock = await stockRepository.GetByIdForUpdateAsync(stockId, cancellationToken);
            if (stock is null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeleteInventoryStockResult.NotFound();
            }

            if (stock.ReservedQuantity > 0)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeleteInventoryStockResult.Conflict(
                    "Không thể xóa tồn kho vì sản phẩm đang có số lượng được giữ chỗ.");
            }

            var movementCount = await stockRepository.CountMovementsAsync(stock.Id, cancellationToken);
            if (movementCount > 0)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeleteInventoryStockResult.Conflict(
                    "Không thể xóa tồn kho đã có lịch sử nhập, xuất hoặc điều chỉnh.");
            }

            var serials = await serialRepository.GetByStockKeyForUpdateAsync(
                stock.ProductId,
                stock.VariantId,
                cancellationToken);
            if (stock.Quantity > serials.Count)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeleteInventoryStockResult.Conflict(
                    "Không thể xóa vì tồn kho vẫn còn số lượng sản phẩm chưa được đối soát.");
            }

            if (serials.Any(serial => serial.Status != SerialImeiStatus.InStock))
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeleteInventoryStockResult.Conflict(
                    "Không thể xóa vì sản phẩm có Serial/IMEI đã được giữ chỗ, bán hoặc liên kết với giao dịch.");
            }

            serialRepository.RemoveRange(serials);
            stockRepository.Delete(stock);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return DeleteInventoryStockResult.Success();
        }
        catch (ForeignKeyViolationException)
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            return DeleteInventoryStockResult.Conflict(
                "Không thể xóa vì tồn kho vẫn còn dữ liệu liên kết trong hệ thống.");
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<InventoryStockDetailResult> GetStockDetailAsync(
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        if (stockId == Guid.Empty) return InventoryStockDetailResult.NotFound();
        var stock = await stockRepository.GetDetailAsync(stockId, cancellationToken);
        return stock is null
            ? InventoryStockDetailResult.NotFound()
            : InventoryStockDetailResult.Success(MapToDetailDto(stock));
    }

    public async Task<AdjustInventoryStockResult> AdjustStockAsync(
        Guid stockId,
        AdjustStockRequest request,
        Guid performedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, string[]>();
        if (stockId == Guid.Empty) return AdjustInventoryStockResult.NotFound();
        if (request.QuantityChange == 0)
            errors["quantityChange"] = ["QuantityChange must not be zero."];
        if (string.IsNullOrWhiteSpace(request.Reason))
            errors["reason"] = ["A reason is required for a stock adjustment."];
        else if (request.Reason.Trim().Length > 200)
            errors["reason"] = ["Reason must be 200 characters or fewer."];
        if (request.Note?.Length > 1000)
            errors["note"] = ["Note must be 1000 characters or fewer."];
        if (performedByUserId == Guid.Empty)
            errors["performedByUserId"] = ["A valid performing user is required."];
        if (errors.Count > 0) return AdjustInventoryStockResult.ValidationFailed(errors);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var stock = await stockRepository.GetByIdForUpdateAsync(stockId, cancellationToken);
            if (stock is null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return AdjustInventoryStockResult.NotFound();
            }

            var newQuantityLong = (long)stock.Quantity + request.QuantityChange;
            if (newQuantityLong < 0 || newQuantityLong < stock.ReservedQuantity || newQuantityLong > int.MaxValue)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return AdjustInventoryStockResult.Conflict(
                    "Không thể giảm tồn kho thấp hơn số lượng đang giữ chỗ hoặc nhỏ hơn 0.");
            }

            var quantityBefore = stock.Quantity;
            stock.Quantity = checked((int)newQuantityLong);
            var now = _time.GetUtcNow().UtcDateTime;
            stock.UpdatedAt = now;
            var reason = request.Note is { Length: > 0 }
                ? $"{request.Reason.Trim()} — {request.Note.Trim()}"
                : request.Reason.Trim();
            stockRepository.AddMovement(new InventoryMovement
            {
                InventoryStockId = stock.Id,
                MovementType = InventoryMovementType.Adjustment,
                QuantityChange = request.QuantityChange,
                QuantityAfter = stock.Quantity,
                PerformedByUserId = performedByUserId,
                Reason = reason,
                CreatedAt = now
            });

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            var detail = await stockRepository.GetDetailAsync(stock.Id, cancellationToken);
            return detail is null
                ? AdjustInventoryStockResult.NotFound()
                : AdjustInventoryStockResult.Success(MapToDetailDto(detail));
        }
        catch (CheckConstraintViolationException)
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            return AdjustInventoryStockResult.Conflict("Điều chỉnh vi phạm ràng buộc số lượng tồn kho.");
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static InventoryStockDto MapToDto(InventoryStockView view)
    {
        var stock = view.Stock;
        return new InventoryStockDto
        {
            Id = stock.Id,
            ProductId = stock.ProductId,
            ProductName = stock.Product?.Name ?? string.Empty,
            ProductSku = stock.Product?.Sku ?? string.Empty,
            CategoryName = stock.Product?.Category?.Name ?? string.Empty,
            SellingPrice = stock.Variant?.Price ?? stock.Product?.BasePrice,
            VariantId = stock.VariantId,
            VariantName = stock.Variant?.VariantName,
            VariantSku = stock.Variant?.Sku,
            Quantity = stock.Quantity,
            ReservedQuantity = stock.ReservedQuantity,
            MinStockAlert = stock.MinStockAlert,
            CreatedAt = stock.CreatedAt,
            UpdatedAt = stock.UpdatedAt,
            TotalSerialsCount = view.SerialCount
        };
    }

    private static InventoryStockDetailDto MapToDetailDto(InventoryStockView view)
    {
        var stock = view.Stock;
        return new InventoryStockDetailDto
        {
            Id = stock.Id,
            ProductId = stock.ProductId,
            VariantId = stock.VariantId,
            ProductName = stock.Product?.Name ?? string.Empty,
            ProductSku = stock.Product?.Sku ?? string.Empty,
            VariantName = stock.Variant?.VariantName,
            VariantSku = stock.Variant?.Sku,
            CategoryName = stock.Product?.Category?.Name ?? string.Empty,
            SellingPrice = stock.Variant?.Price ?? stock.Product?.BasePrice,
            Quantity = stock.Quantity,
            ReservedQuantity = stock.ReservedQuantity,
            TotalSerialsCount = view.SerialCount,
            CreatedAt = stock.CreatedAt,
            UpdatedAt = stock.UpdatedAt,
            RecentMovements = stock.Movements
                .OrderByDescending(movement => movement.CreatedAt)
                .Take(10)
                .Select(movement => new InventoryMovementDto
                {
                    Id = movement.Id,
                    MovementType = movement.MovementType,
                    QuantityChange = movement.QuantityChange,
                    QuantityBefore = movement.QuantityAfter - movement.QuantityChange,
                    QuantityAfter = movement.QuantityAfter,
                    Note = movement.Reason,
                    CreatedAt = movement.CreatedAt,
                    PerformedByUserName = movement.PerformedByUser?.FullName
                        ?? movement.PerformedByUser?.Username
                        ?? string.Empty
                })
                .ToArray()
        };
    }
}
