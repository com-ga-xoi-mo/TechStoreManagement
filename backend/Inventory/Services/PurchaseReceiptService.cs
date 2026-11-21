using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory.Services;

public partial class PurchaseReceiptService(
    IPurchaseOrderRepository purchaseOrderRepository,
    IInventoryStockRepository stockRepository,
    ISerialImeiRepository serialImeiRepository,
    IProductRepository productRepository,
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : IPurchaseReceiptService
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<PurchaseReceiptSearchResult> SearchAsync(
        PurchaseReceiptFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = request.Page.GetValueOrDefault(1);
        if (page < 1) page = 1;

        var pageSize = request.PageSize.GetValueOrDefault(20);
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var criteria = new PurchaseReceiptSearchCriteria(
            request.Search,
            request.SupplierId,
            page,
            pageSize);

        var (items, totalCount) = await purchaseOrderRepository.SearchReceiptsAsync(criteria, cancellationToken);
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        return PurchaseReceiptSearchResult.Success(
            new PagedResultDto<PurchaseReceiptSummaryDto>(items, page, pageSize, totalCount, totalPages));
    }

    public async Task<PurchaseReceiptDetailResult> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // The header/lines and device list are separate queries; a delivery must appear in both or neither.
        await unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            var (order, devices) = await purchaseOrderRepository.GetReceiptDetailAsync(id, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return order == null ? PurchaseReceiptDetailResult.NotFound()
                : PurchaseReceiptDetailResult.Success(MapToDetailDto(order, devices));
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PurchaseReceiptResult> CreateAsync(
        CreatePurchaseReceiptRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Check clientRequestId first before any other validation
        if (!request.ClientRequestId.HasValue || request.ClientRequestId.Value == Guid.Empty)
        {
            return PurchaseReceiptResult.ValidationFailed(new()
            {
                ["clientRequestId"] = ["Client request ID is required."]
            });
        }

        var replay = await FindReplayAsync(request.ClientRequestId.Value, cancellationToken);
        if (replay != null) return replay;

        // 2. Validate request (in-memory)
        var errors = new Dictionary<string, string[]>();

        // A JSON null inside lines or devices would otherwise reach the checks below as a null reference.
        if (request.Lines != null)
        {
            for (var i = 0; i < request.Lines.Count; i++)
            {
                if (request.Lines[i] == null)
                {
                    errors[$"lines[{i}]"] = ["Dòng hàng không hợp lệ."];
                    continue;
                }

                var lineDevices = request.Lines[i].Devices;
                for (var m = 0; lineDevices != null && m < lineDevices.Count; m++)
                {
                    if (lineDevices[m] == null)
                    {
                        errors[$"lines[{i}].devices[{m}]"] = ["Mã Serial/IMEI không hợp lệ."];
                    }
                }
            }

            if (errors.Count > 0)
            {
                return PurchaseReceiptResult.ValidationFailed(errors);
            }
        }

        var trimmedDoc = string.IsNullOrWhiteSpace(request.SupplierDocumentNumber) ? null : request.SupplierDocumentNumber.Trim();
        var trimmedNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (!request.SupplierId.HasValue || request.SupplierId.Value == Guid.Empty)
        {
            errors["supplierId"] = ["Nhà cung cấp là bắt buộc."];
        }

        if (trimmedDoc != null && trimmedDoc.Length > 100)
        {
            errors["supplierDocumentNumber"] = ["Số chứng từ không được vượt quá 100 ký tự."];
        }

        if (trimmedNote != null && trimmedNote.Length > 1000)
        {
            errors["note"] = ["Ghi chú không được vượt quá 1000 ký tự."];
        }

        if (request.Lines == null || request.Lines.Count == 0 || request.Lines.Count > 100)
        {
            errors["lines"] = ["Phiếu nhập phải có từ 1 đến 100 dòng."];
        }

        var seenProducts = new HashSet<StockLockKey>();
        var seenPayloadSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPayloadImeis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dupePayloadCodes = new List<string>();
        var allBatchSerials = new List<string>();
        var allBatchImeis = new List<string>();

        if (request.Lines != null)
        {
            for (var i = 0; i < request.Lines.Count; i++)
            {
                var line = request.Lines[i];
                if (line.ProductId.HasValue)
                {
                    var lockKey = new StockLockKey(line.ProductId.Value, line.VariantId);
                    if (!seenProducts.Add(lockKey))
                    {
                        errors[$"lines[{i}]"] = ["Không được trùng sản phẩm hoặc biến thể trong cùng phiếu nhập."];
                    }
                }
                else
                {
                    errors[$"lines[{i}].productId"] = ["Sản phẩm là bắt buộc."];
                }

                if (!line.Quantity.HasValue || line.Quantity.Value <= 0 || line.Quantity.Value > 100_000)
                {
                    errors[$"lines[{i}].quantity"] = ["Số lượng phải từ 1 đến 100.000."];
                }

                var costErr = UnitCostRules.Check(line.UnitCost);
                if (costErr != null)
                {
                    errors[$"lines[{i}].unitCost"] = [costErr];
                }
                else if (decimal.Round(line.UnitCost!.Value, 2) != line.UnitCost.Value)
                {
                    errors[$"lines[{i}].unitCost"] = ["Giá nhập có tối đa hai chữ số thập phân."];
                }
                else if (line.Quantity.HasValue && line.Quantity.Value > 0 && line.Quantity.Value * line.UnitCost!.Value > UnitCostRules.MaxLineTotal)
                {
                    errors[$"lines[{i}].unitCost"] = ["Tổng tiền dòng vượt quá giới hạn cho phép."];
                }

                var wm = line.WarrantyMonths.GetValueOrDefault(12);
                if (wm < 0 || wm > 120)
                {
                    errors[$"lines[{i}].warrantyMonths"] = ["Thời gian bảo hành phải từ 0 đến 120 tháng."];
                }

                if (line.Devices != null)
                {
                    for (var m = 0; m < line.Devices.Count; m++)
                    {
                        var dev = line.Devices[m];
                        if (!SerialImeiValidationHelper.ValidateDeviceIdentifiers(
                            dev.SerialNumber,
                            dev.Imei,
                            out var s,
                            out var imei,
                            out var errField,
                            out var errMsg,
                            100))
                        {
                            var fieldKey = errField != null ? $"lines[{i}].devices[{m}].{errField}" : $"lines[{i}].devices[{m}]";
                            errors[fieldKey] = [errMsg!];
                        }

                        if (s != null)
                        {
                            if (!seenPayloadSerials.Add(s))
                            {
                                dupePayloadCodes.Add(s);
                            }
                            allBatchSerials.Add(s);
                        }

                        if (imei != null)
                        {
                            if (!seenPayloadImeis.Add(imei))
                            {
                                dupePayloadCodes.Add(imei);
                            }
                            allBatchImeis.Add(imei);
                        }
                    }
                }
            }
        }

        // 3. Database lookups (AsNoTracking)
        Supplier? supplier = null;
        if (request.SupplierId.HasValue && request.SupplierId.Value != Guid.Empty)
        {
            supplier = await supplierRepository.GetByIdAsync(request.SupplierId.Value, cancellationToken);
            if (supplier == null)
            {
                errors["supplierId"] = ["Nhà cung cấp không tồn tại."];
            }
            else if (supplier.Name == SystemSuppliers.OpeningBalanceName)
            {
                errors["supplierId"] = ["Không thể nhập hàng từ nhà cung cấp hệ thống."];
            }
            else if (!supplier.IsActive)
            {
                errors["supplierId"] = ["Nhà cung cấp đã ngừng hoạt động."];
            }
        }

        Dictionary<Guid, Product> products = [];
        if (request.Lines != null)
        {
            var productIds = request.Lines
                .Where(l => l.ProductId.HasValue && l.ProductId.Value != Guid.Empty)
                .Select(l => l.ProductId!.Value)
                .Distinct()
                .ToList();

            products = (await productRepository.GetManyWithVariantsAsync(productIds, cancellationToken))
                .ToDictionary(p => p.Id);

            var totalDevices = 0;

            for (var i = 0; i < request.Lines.Count; i++)
            {
                var line = request.Lines[i];
                if (!line.ProductId.HasValue) continue;

                if (!products.TryGetValue(line.ProductId.Value, out var product))
                {
                    errors[$"lines[{i}].productId"] = ["Sản phẩm không tồn tại."];
                    continue;
                }

                if (product.Variants.Count > 0)
                {
                    if (!line.VariantId.HasValue)
                    {
                        errors[$"lines[{i}].variantId"] = ["Sản phẩm có biến thể bắt buộc chọn biến thể."];
                    }
                    else
                    {
                        var variant = product.Variants.FirstOrDefault(v => v.Id == line.VariantId.Value);
                        if (variant == null)
                        {
                            errors[$"lines[{i}].variantId"] = ["Biến thể không thuộc sản phẩm này."];
                        }
                        else if (!variant.IsActive)
                        {
                            errors[$"lines[{i}].variantId"] = ["Biến thể đã ngừng kinh doanh."];
                        }
                    }
                }
                else
                {
                    if (line.VariantId.HasValue)
                    {
                        errors[$"lines[{i}].variantId"] = ["Sản phẩm không có biến thể."];
                    }
                }

                if (product.IsSerialTracked)
                {
                    var devCount = line.Devices?.Count ?? 0;
                    totalDevices += devCount;
                    if (request.ReceiveLater && devCount != 0)
                        errors[$"lines[{i}].devices"] = ["Chỉ gửi mã máy khi hàng về kho."];
                    else if (!request.ReceiveLater && devCount != line.Quantity)
                    {
                        errors[$"lines[{i}].devices"] = [$"Số lượng máy ({devCount}) phải bằng số lượng nhập ({line.Quantity ?? 0})."];
                    }
                }
                else
                {
                    if (line.Devices != null && line.Devices.Count > 0)
                    {
                        errors[$"lines[{i}].devices"] = ["Hàng không quản lý Serial/IMEI không được nhập máy."];
                    }
                }
            }

            if (totalDevices > 500)
            {
                errors["lines"] = ["Một phiếu nhập tối đa 500 máy."];
            }

            // Each line fits numeric(18,2), but their sum is stored in total_cost too.
            var receiptTotal = request.Lines
                .Where(l => l.Quantity is > 0 and <= 100_000 && l.UnitCost is >= 0 and <= UnitCostRules.Max)
                .Sum(l => l.Quantity!.Value * l.UnitCost!.Value);
            if (receiptTotal > UnitCostRules.MaxLineTotal)
            {
                errors["lines"] = ["Tổng tiền phiếu nhập vượt quá giới hạn cho phép."];
            }
        }

        if (errors.Count > 0)
        {
            return PurchaseReceiptResult.ValidationFailed(errors);
        }

        if (dupePayloadCodes.Count > 0)
        {
            return PurchaseReceiptResult.Conflict("Trùng Serial hoặc IMEI trong phiếu nhập.", dupePayloadCodes.Distinct().ToList());
        }

        // 4. Begin Transaction
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 4a. Check existing DB identifiers
            if (allBatchSerials.Count > 0 || allBatchImeis.Count > 0)
            {
                var existing = await serialImeiRepository.FindExistingIdentifiersAsync(allBatchSerials, allBatchImeis, cancellationToken);
                if (existing.HasAnyDuplicates)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    var raced = await FindReplayAsync(request.ClientRequestId.Value, cancellationToken);
                    if (raced != null) return raced;
                    var dupeList = existing.ExistingSerials.Concat(existing.ExistingImeis).Distinct().ToList();
                    return PurchaseReceiptResult.Conflict("Serial hoặc IMEI đã tồn tại trong hệ thống.", dupeList);
                }
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var poNumber = $"PN-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            var totalCost = request.Lines!.Sum(l => l.Quantity!.Value * l.UnitCost!.Value);

            var po = new PurchaseOrder
            {
                SupplierId = request.SupplierId!.Value,
                CreatedByUserId = actorUserId,
                ReceivedByUserId = request.ReceiveLater ? null : actorUserId,
                PoNumber = poNumber,
                SupplierDocumentNumber = trimmedDoc,
                ClientRequestId = request.ClientRequestId.Value,
                Status = request.ReceiveLater ? PurchaseOrderStatus.Ordered : PurchaseOrderStatus.Received,
                TotalCost = request.ReceiveLater ? 0m : totalCost,
                Notes = trimmedNote,
                OrderedAt = now,
                ReceivedAt = request.ReceiveLater ? null : now,
                CreatedAt = now
            };

            // 4b. Lock stocks in standard (ProductId, VariantId) order
            var orderedLines = request.Lines!
                .OrderByStockLockKey(l => l.ProductId!.Value, l => l.VariantId)
                .ToList();

            var devicesToAdd = new List<SerialImei>();

            foreach (var line in orderedLines)
            {
                var productId = line.ProductId!.Value;
                var variantId = line.VariantId;
                var qty = line.Quantity!.Value;
                var unitCost = line.UnitCost!.Value;
                var warrantyMonths = line.WarrantyMonths.GetValueOrDefault(12);

                var poItem = new PurchaseOrderItem
                {
                    PurchaseOrder = po,
                    ProductId = productId,
                    VariantId = variantId,
                    OrderedQuantity = qty,
                    WarrantyMonths = products[productId].IsSerialTracked ? warrantyMonths : 0,
                    UnitCost = unitCost,
                    CreatedAt = now
                };
                po.Items.Add(poItem);

                if (!request.ReceiveLater)
                    await ReceiveLineAsync(po, poItem, qty, line.Devices, actorUserId, now, null, trimmedDoc, devicesToAdd, cancellationToken);
            }

            purchaseOrderRepository.Add(po);
            if (devicesToAdd.Count > 0)
            {
                serialImeiRepository.AddRange(devicesToAdd);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            var (createdOrder, createdDevices) = await purchaseOrderRepository.GetReceiptDetailAsync(po.Id, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return PurchaseReceiptResult.Created(MapToDetailDto(createdOrder ?? po, createdDevices.Count > 0 ? createdDevices : devicesToAdd));
        }
        catch (DuplicateKeyException ex)
        {
            await unitOfWork.RollbackAsync(cancellationToken);

            // Re-read by clientRequestId before handling constraint
            var raced = await FindReplayAsync(request.ClientRequestId.Value, cancellationToken);
            if (raced != null) return raced;

            if (ex.ConstraintName == "idx_inventory_stocks_product_variant")
            {
                return PurchaseReceiptResult.Conflict("Tồn kho vừa được tạo bởi thao tác khác, vui lòng thử lại.");
            }
            if (ex.ConstraintName is "idx_serial_imeis_imei" or "idx_serial_imeis_serial")
            {
                return PurchaseReceiptResult.Conflict("Serial hoặc IMEI đã tồn tại trong hệ thống.");
            }
            if (ex.ConstraintName == "idx_purchase_orders_po_number")
            {
                return PurchaseReceiptResult.Conflict("Số phiếu bị trùng, vui lòng thử lại.");
            }

            return PurchaseReceiptResult.Conflict("Xung đột dữ liệu, vui lòng thử lại.");
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<PurchaseReceiptResult?> FindReplayAsync(Guid key, CancellationToken cancellationToken)
    {
        var existingId = await purchaseOrderRepository.FindIdByClientRequestIdAsync(key, cancellationToken);
        if (existingId == null) return null;
        var detail = await GetDetailAsync(existingId.Value, cancellationToken);
        return detail.Data == null ? null : PurchaseReceiptResult.Replayed(detail.Data);
    }

    private async Task ReceiveLineAsync(PurchaseOrder po, PurchaseOrderItem poItem, int qty,
        List<PurchaseReceiptDeviceRequest>? devices, Guid actorUserId, DateTime now, Guid? requestId, string? documentNumber,
        List<SerialImei> devicesToAdd, CancellationToken cancellationToken)
    {
        var productId = poItem.ProductId;
        var variantId = poItem.VariantId;
        var unitCost = poItem.UnitCost;
        var warrantyMonths = poItem.WarrantyMonths ?? 12;
        poItem.ReceivedQuantity += qty;
        var stock = await stockRepository.GetForUpdateAsync(productId, variantId, cancellationToken);
        if (stock == null)
        {
            stock = new InventoryStock
            {
                ProductId = productId,
                VariantId = variantId,
                Quantity = 0,
                ReservedQuantity = 0,
                MinStockAlert = 5,
                CreatedAt = now
            };
            stockRepository.Add(stock);
        }

        stock.Quantity += qty;
        stock.UpdatedAt = now;

        stockRepository.AddMovement(new InventoryMovement
        {
            InventoryStock = stock,
            MovementType = InventoryMovementType.PurchaseReceipt,
            QuantityChange = qty,
            ReceiptRequestId = requestId,
            QuantityAfter = stock.Quantity,
            PerformedByUserId = actorUserId,
            PurchaseOrderItem = poItem,
            Reason = documentNumber == null ? $"Phiếu nhập {po.PoNumber}" : $"Phiếu nhập {po.PoNumber} · Chứng từ {documentNumber}",
            CreatedAt = now
        });

        if (devices != null && devices.Count > 0)
        {
            foreach (var dev in devices)
            {
                var normalizedSerial = SerialImeiValidationHelper.Normalize(dev.SerialNumber);
                var normalizedImei = SerialImeiValidationHelper.Normalize(dev.Imei);

                var serialEntity = new SerialImei
                {
                    ProductId = productId,
                    VariantId = variantId,
                    SerialNumber = normalizedSerial,
                    Imei = normalizedImei,
                    Status = SerialImeiStatus.InStock,
                    UnitCost = unitCost,
                    WarrantyMonths = warrantyMonths,
                    PurchaseOrder = po,
                    CreatedAt = now
                };
                devicesToAdd.Add(serialEntity);
            }
        }
    }

    private static PurchaseReceiptDto MapToDetailDto(PurchaseOrder order, IReadOnlyList<SerialImei> devices)
    {
        var devicesByItem = devices.ToLookup(d => (d.ProductId, d.VariantId));

        var lines = order.Items.Select(item =>
        {
            var itemDevices = devicesByItem[(item.ProductId, item.VariantId)]
                .Select(d => new PurchaseReceiptDeviceDto
                {
                    Id = d.Id,
                    SerialNumber = d.SerialNumber,
                    Imei = d.Imei,
                    Status = d.Status
                })
                .ToList();

            return new PurchaseReceiptLineDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? string.Empty,
                ProductSku = item.Product?.Sku ?? string.Empty,
                VariantId = item.VariantId,
                VariantName = item.Variant?.VariantName,
                VariantSku = item.Variant?.Sku,
                OrderedQuantity = item.OrderedQuantity,
                Quantity = item.ReceivedQuantity,
                UnitCost = item.UnitCost,
                LineTotal = item.ReceivedQuantity * item.UnitCost,
                IsSerialTracked = item.Product?.IsSerialTracked ?? (itemDevices.Count > 0),
                WarrantyMonths = item.WarrantyMonths ?? (itemDevices.Count > 0 ? (devices.FirstOrDefault(d => d.ProductId == item.ProductId && d.VariantId == item.VariantId)?.WarrantyMonths ?? 12) : 0),
                Devices = itemDevices
            };
        }).ToList();

        return new PurchaseReceiptDto
        {
            Status = order.Status,
            CancelledAt = order.CancelledAt,
            CancelledByUserId = order.CancelledByUserId,
            Id = order.Id,
            PoNumber = order.PoNumber,
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier?.Name ?? string.Empty,
            SupplierDocumentNumber = order.SupplierDocumentNumber,
            ClientRequestId = order.ClientRequestId,
            Note = order.Notes,
            ReceivedAt = order.ReceivedAt,
            ReceivedByUserId = order.ReceivedByUserId,
            ReceivedByName = order.ReceivedByUser?.FullName,
            TotalCost = order.TotalCost,
            Lines = lines
        };
    }
}
