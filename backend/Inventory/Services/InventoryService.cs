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
    IUnitOfWork unitOfWork) : IInventoryService
{
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
            request.ProductId,
            request.VariantId,
            request.LowStock,
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

        if (request.ProductId.HasValue && request.ProductId.Value != Guid.Empty)
        {
            var product = await productRepository.GetWithVariantsAsync(request.ProductId.Value, cancellationToken);
            if (product == null)
            {
                errors["productId"] = [$"Product with ID '{request.ProductId.Value}' does not exist."];
            }
            else if (request.VariantId.HasValue && request.VariantId.Value != Guid.Empty)
            {
                if (product.Variants == null || product.Variants.All(v => v.Id != request.VariantId.Value))
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

            return InitializeStockResult.Success(MapToDto(stock));
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

    private static InventoryStockDto MapToDto(InventoryStock stock)
    {
        return new InventoryStockDto
        {
            Id = stock.Id,
            ProductId = stock.ProductId,
            ProductName = stock.Product?.Name ?? string.Empty,
            ProductSku = stock.Product?.Sku ?? string.Empty,
            VariantId = stock.VariantId,
            VariantName = stock.Variant?.VariantName,
            VariantSku = stock.Variant?.Sku,
            Quantity = stock.Quantity,
            ReservedQuantity = stock.ReservedQuantity,
            MinStockAlert = stock.MinStockAlert,
            CreatedAt = stock.CreatedAt,
            UpdatedAt = stock.UpdatedAt
        };
    }
}
