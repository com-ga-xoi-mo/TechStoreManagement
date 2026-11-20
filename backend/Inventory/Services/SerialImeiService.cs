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
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : ISerialImeiService
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    public async Task<SerialImeiSearchResult> GetSerialsAsync(
        SerialImeiFilterRequest request,
        bool includeCost = false,
        CancellationToken cancellationToken = default)
    {
        var page = request.Page.GetValueOrDefault(1);
        if (page < 1) page = 1;

        var pageSize = request.PageSize.GetValueOrDefault(20);
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var criteria = new SerialImeiSearchCriteria(
            request.Search,
            request.ProductId,
            request.VariantId,
            request.Status,
            page,
            pageSize);

        var (items, totalCount) = await serialImeiRepository.SearchAsync(criteria, cancellationToken);
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        var dtos = items.Select(item => MapToDto(item, includeCost)).ToList();

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

            // Each device carries its own cost, taken from the item or else from defaultUnitCost.
            var costError = UnitCostRules.Check(item.UnitCost ?? request.DefaultUnitCost);
            if (costError != null)
            {
                errors[$"items[{i}].unitCost"] = [$"{costError} Set it on the item or send defaultUnitCost."];
            }

            if (serial == null && imei == null)
            {
                errors[$"items[{i}]"] = ["Either SerialNumber or IMEI must be provided."];
                continue;
            }

            if (imei != null && !SerialImeiValidationHelper.IsValidImei(imei))
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

    public async Task<DeviceStatusResult> ReportDefectiveAsync(
        Guid deviceId,
        ReportDefectiveDeviceRequest request,
        Guid actorUserId,
        bool includeCost = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (deviceId == Guid.Empty)
        {
            return DeviceStatusResult.NotFound();
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors["reason"] = ["Lý do báo lỗi là bắt buộc."];
        }
        else if (request.Reason.Trim().Length > 200)
        {
            errors["reason"] = ["Lý do không được vượt quá 200 ký tự."];
        }

        if (request.Note != null && request.Note.Length > 1000)
        {
            errors["note"] = ["Ghi chú không được vượt quá 1000 ký tự."];
        }

        if (errors.Count > 0)
        {
            return DeviceStatusResult.ValidationFailed(errors);
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var devices = await serialImeiRepository.GetForUpdateAsync([deviceId], cancellationToken);
            var device = devices.FirstOrDefault();
            if (device == null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeviceStatusResult.NotFound();
            }

            if (device.Status != SerialImeiStatus.InStock)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeviceStatusResult.Conflict($"Thiết bị không ở trạng thái sẵn sàng trong kho (trạng thái hiện tại: {StatusLabel(device.Status)}).");
            }

            var stock = await stockRepository.GetForUpdateAsync(device.ProductId, device.VariantId, cancellationToken);
            if (stock == null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeviceStatusResult.Conflict("Không tìm thấy dòng tồn kho tương ứng với thiết bị.");
            }

            if (stock.Quantity - 1 < stock.ReservedQuantity || stock.Quantity - 1 < 0)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeviceStatusResult.Conflict("Không thể giảm tồn kho thấp hơn số lượng đang giữ chỗ hoặc nhỏ hơn 0.");
            }

            var now = _time.GetUtcNow().UtcDateTime;
            device.Status = SerialImeiStatus.Defective;
            device.UpdatedAt = now;

            stock.Quantity -= 1;
            stock.UpdatedAt = now;

            var devCode = !string.IsNullOrWhiteSpace(device.Imei) ? $"IMEI {device.Imei}" : $"Serial {device.SerialNumber}";
            var trimmedReason = request.Reason!.Trim();
            var trimmedNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            var reasonText = trimmedNote != null
                ? $"Báo lỗi máy {devCode}: {trimmedReason} — {trimmedNote}"
                : $"Báo lỗi máy {devCode}: {trimmedReason}";

            stockRepository.AddMovement(new InventoryMovement
            {
                InventoryStockId = stock.Id,
                MovementType = InventoryMovementType.Defective,
                QuantityChange = -1,
                QuantityAfter = stock.Quantity,
                PerformedByUserId = actorUserId,
                Reason = reasonText,
                CreatedAt = now
            });

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return DeviceStatusResult.Success(await MapChangedDeviceAsync(device, includeCost, cancellationToken));
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<DeviceStatusResult> RestoreAsync(
        Guid deviceId,
        RestoreDeviceRequest request,
        Guid actorUserId,
        bool includeCost = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (deviceId == Guid.Empty)
        {
            return DeviceStatusResult.NotFound();
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Note != null && request.Note.Length > 1000)
        {
            errors["note"] = ["Ghi chú không được vượt quá 1000 ký tự."];
        }

        if (errors.Count > 0)
        {
            return DeviceStatusResult.ValidationFailed(errors);
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var devices = await serialImeiRepository.GetForUpdateAsync([deviceId], cancellationToken);
            var device = devices.FirstOrDefault();
            if (device == null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeviceStatusResult.NotFound();
            }

            if (device.Status != SerialImeiStatus.Defective)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeviceStatusResult.Conflict($"Thiết bị không ở trạng thái báo lỗi (trạng thái hiện tại: {StatusLabel(device.Status)}).");
            }

            var stock = await stockRepository.GetForUpdateAsync(device.ProductId, device.VariantId, cancellationToken);
            if (stock == null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return DeviceStatusResult.Conflict("Không tìm thấy dòng tồn kho tương ứng với thiết bị.");
            }

            var now = _time.GetUtcNow().UtcDateTime;
            device.Status = SerialImeiStatus.InStock;
            device.UpdatedAt = now;

            stock.Quantity += 1;
            stock.UpdatedAt = now;

            var devCode = !string.IsNullOrWhiteSpace(device.Imei) ? $"IMEI {device.Imei}" : $"Serial {device.SerialNumber}";
            var trimmedNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            var reasonText = trimmedNote != null
                ? $"Sửa xong máy {devCode}: {trimmedNote}"
                : $"Sửa xong máy {devCode}";

            stockRepository.AddMovement(new InventoryMovement
            {
                InventoryStockId = stock.Id,
                MovementType = InventoryMovementType.Adjustment,
                QuantityChange = 1,
                QuantityAfter = stock.Quantity,
                PerformedByUserId = actorUserId,
                Reason = reasonText,
                CreatedAt = now
            });

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return DeviceStatusResult.Success(await MapChangedDeviceAsync(device, includeCost, cancellationToken));
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // The locked device row carries no product navigation, so names are read after commit.
    private async Task<SerialImeiDto> MapChangedDeviceAsync(
        SerialImei device,
        bool includeCost,
        CancellationToken cancellationToken)
    {
        var dto = MapToDto(device, includeCost);
        var product = await productRepository.GetDetailAsync(device.ProductId, cancellationToken);
        if (product == null)
        {
            return dto;
        }

        dto.ProductName = product.Name;
        if (device.VariantId.HasValue)
        {
            dto.VariantName = product.Variants.FirstOrDefault(v => v.Id == device.VariantId.Value)?.VariantName;
        }

        return dto;
    }

    private static string StatusLabel(SerialImeiStatus status) => status switch
    {
        SerialImeiStatus.InStock => "Trong kho",
        SerialImeiStatus.Reserved => "Đang giữ",
        SerialImeiStatus.Sold => "Đã bán",
        SerialImeiStatus.Returned => "Đã trả",
        SerialImeiStatus.UnderRepair => "Đang sửa chữa",
        SerialImeiStatus.Defective => "Lỗi",
        _ => status.ToString()
    };

    private static SerialImeiDto MapToDto(SerialImei item, bool includeCost)
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
            UnitCost = includeCost ? item.UnitCost : null,
            PurchaseOrderId = item.PurchaseOrderId,
            WarrantyMonths = item.WarrantyMonths,
            WarrantyStartAt = item.WarrantyStartAt,
            WarrantyEndAt = item.WarrantyEndAt,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}
