using System.Text.RegularExpressions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory.Services;

public class SerialImeiService(
    ISerialImeiRepository serialImeiRepository,
    IInventoryStockRepository stockRepository,
    IProductRepository productRepository,
    IUnitOfWork unitOfWork) : ISerialImeiService
{
    private static readonly Regex ImeiRegex = new(@"^[0-9]{15}$", RegexOptions.Compiled);

    public async Task<SerialImeiSearchResult> GetSerialsAsync(
        SerialImeiFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = request.Page.GetValueOrDefault(1);
        if (page < 1) page = 1;

        var pageSize = request.PageSize.GetValueOrDefault(20);
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var criteria = new SerialImeiSearchCriteria(
            request.ProductId,
            request.VariantId,
            request.Status,
            page,
            pageSize);

        var (items, totalCount) = await serialImeiRepository.SearchAsync(criteria, cancellationToken);
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        var dtos = items.Select(MapToDto).ToList();

        var paged = new PagedResultDto<SerialImeiDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return SerialImeiSearchResult.Success(paged);
    }

    public async Task<BatchImportSerialImeiResult> BatchImportAsync(
        BatchImportSerialImeiRequest request,
        Guid performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (!request.ProductId.HasValue || request.ProductId.Value == Guid.Empty)
        {
            errors["productId"] = ["Product ID is required."];
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            errors["items"] = ["Items list cannot be empty."];
        }
        else if (request.Items.Count > 500)
        {
            errors["items"] = ["Batch import size cannot exceed 500 items per request."];
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
            return BatchImportSerialImeiResult.ValidationFailed(errors);
        }

        var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenImeis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dupePayloadSerials = new List<string>();
        var dupePayloadImeis = new List<string>();

        var batchSerials = new List<string>();
        var batchImeis = new List<string>();

        for (var i = 0; i < request.Items!.Count; i++)
        {
            var item = request.Items[i];
            var serial = string.IsNullOrWhiteSpace(item.SerialNumber) ? null : item.SerialNumber.Trim();
            var imei = string.IsNullOrWhiteSpace(item.Imei) ? null : item.Imei.Trim();

            if (serial == null && imei == null)
            {
                errors[$"items[{i}]"] = ["Either SerialNumber or IMEI must be provided."];
                continue;
            }

            if (imei != null && !ImeiRegex.IsMatch(imei))
            {
                errors[$"items[{i}].imei"] = ["IMEI must be exactly 15 digits."];
            }

            if (serial != null)
            {
                if (!seenSerials.Add(serial))
                {
                    dupePayloadSerials.Add(serial);
                }
                batchSerials.Add(serial);
            }

            if (imei != null)
            {
                if (!seenImeis.Add(imei))
                {
                    dupePayloadImeis.Add(imei);
                }
                batchImeis.Add(imei);
            }
        }

        if (dupePayloadSerials.Count > 0 || dupePayloadImeis.Count > 0)
        {
            return BatchImportSerialImeiResult.Conflict(
                "Duplicate Serial or IMEI entries found within the import payload.",
                dupePayloadSerials,
                dupePayloadImeis);
        }

        if (errors.Count > 0)
        {
            return BatchImportSerialImeiResult.ValidationFailed(errors);
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Kiểm tra trùng lặp với Database
            var existing = await serialImeiRepository.FindExistingIdentifiersAsync(batchSerials, batchImeis, cancellationToken);
            if (existing.HasAnyDuplicates)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                var message = $"Serial/IMEI duplicate conflict: Serials [{string.Join(", ", existing.ExistingSerials)}], IMEIs [{string.Join(", ", existing.ExistingImeis)}] already exist in the system.";
                return BatchImportSerialImeiResult.Conflict(
                    message,
                    existing.ExistingSerials,
                    existing.ExistingImeis);
            }

            // =========================================================================
            // TUÂN THỦ NGHIÊM NGẶT THỨ TỰ 3 BƯỚC THỰC THI LOGIC
            // =========================================================================

            // BƯỚC 1: Kiểm tra tồn kho -> Nếu chưa có stock row -> tạo mới InventoryStock với Quantity = 0.
            var stock = await stockRepository.GetForUpdateAsync(request.ProductId!.Value, request.VariantId, cancellationToken);
            if (stock == null)
            {
                stock = new InventoryStock
                {
                    ProductId = request.ProductId.Value,
                    VariantId = request.VariantId,
                    Quantity = 0,
                    ReservedQuantity = 0,
                    MinStockAlert = 5
                };
                stockRepository.Add(stock);
            }

            // BƯỚC 2: Cập nhật số lượng -> Cộng dồn số lượng IMEI hợp lệ vừa import vào kho (Quantity += importedCount).
            var importedCount = request.Items.Count;
            stock.Quantity += importedCount;

            // BƯỚC 3: Tạo lịch sử -> CHỈ khởi tạo bản ghi InventoryMovement (stage movement) SAU KHI biến Quantity đã được cộng xong.
            // Đảm bảo thuộc tính QuantityAfter của movement phải lấy đúng giá trị đã được cập nhật ở bước 2.
            var movement = new InventoryMovement
            {
                InventoryStock = stock,
                MovementType = InventoryMovementType.OpeningBalance,
                QuantityChange = importedCount,
                QuantityAfter = stock.Quantity, // Strictly assigned after stock.Quantity += importedCount
                PerformedByUserId = performedByUserId,
                Reason = request.Reason ?? "Initial batch serial/IMEI import"
            };
            stockRepository.AddMovement(movement);

            // Khởi tạo các thực thể SerialImei
            var serialEntities = new List<SerialImei>(importedCount);
            foreach (var item in request.Items)
            {
                serialEntities.Add(new SerialImei
                {
                    ProductId = request.ProductId.Value,
                    VariantId = request.VariantId,
                    SerialNumber = string.IsNullOrWhiteSpace(item.SerialNumber) ? null : item.SerialNumber.Trim(),
                    Imei = string.IsNullOrWhiteSpace(item.Imei) ? null : item.Imei.Trim(),
                    Status = SerialImeiStatus.InStock,
                    UnitCost = item.UnitCost ?? request.DefaultUnitCost,
                    PurchaseOrderId = request.PurchaseOrderId,
                    WarrantyMonths = item.WarrantyMonths ?? request.DefaultWarrantyMonths ?? 12
                });
            }
            serialImeiRepository.AddRange(serialEntities);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return BatchImportSerialImeiResult.Success(importedCount);
        }
        catch (DuplicateKeyException ex)
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            return BatchImportSerialImeiResult.Conflict($"A duplicate serial or IMEI was detected in the database: {ex.Message}");
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static SerialImeiDto MapToDto(SerialImei item)
    {
        return new SerialImeiDto
        {
            Id = item.Id,
            ProductId = item.ProductId,
            ProductName = item.Product?.Name ?? string.Empty,
            VariantId = item.VariantId,
            VariantName = item.Variant?.VariantName,
            SerialNumber = item.SerialNumber,
            Imei = item.Imei,
            Status = item.Status,
            UnitCost = item.UnitCost,
            PurchaseOrderId = item.PurchaseOrderId,
            WarrantyMonths = item.WarrantyMonths,
            WarrantyStartAt = item.WarrantyStartAt,
            WarrantyEndAt = item.WarrantyEndAt,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}
