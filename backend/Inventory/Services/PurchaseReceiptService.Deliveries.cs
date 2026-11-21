using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory.Services;

public partial class PurchaseReceiptService
{
    public Task<PurchaseReceiptResult> ReceiveAsync(Guid id, ReceivePurchaseDeliveryRequest delivery,
        Guid actorUserId, CancellationToken cancellationToken = default) =>
        UpdateReceiptAsync(id, delivery, actorUserId, cancellationToken);

    public Task<PurchaseReceiptResult> CancelRemainderAsync(Guid id, Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        UpdateReceiptAsync(id, null, actorUserId, cancellationToken);

    private async Task<PurchaseReceiptResult> UpdateReceiptAsync(Guid id, ReceivePurchaseDeliveryRequest? delivery,
        Guid actorUserId, CancellationToken cancellationToken)
    {
        if (delivery is not null && delivery.ClientRequestId == Guid.Empty)
            return InvalidDelivery("clientRequestId", "Cần mã yêu cầu cho lần nhận hàng.");

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await purchaseOrderRepository.LockReceiptAsync(id, cancellationToken);
            var now = _time.GetUtcNow().UtcDateTime;
            var replay = false;
            PurchaseReceiptResult? error = null;
            if (order == null || order.Status == PurchaseOrderStatus.Draft)
                error = PurchaseReceiptResult.NotFound();
            else if (delivery != null && await purchaseOrderRepository.HasDeliveryAsync(id, delivery.ClientRequestId, cancellationToken))
                replay = true;
            else if (delivery == null && order.Status == PurchaseOrderStatus.Cancelled)
                replay = true;
            else if (order.Status is not (PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived))
                error = PurchaseReceiptResult.Conflict("Phiếu đã đóng, không còn hàng chờ nhận.");
            else if (delivery == null)
            {
                order.Status = PurchaseOrderStatus.Cancelled;
                order.CancelledByUserId = actorUserId;
                order.CancelledAt = now;
                order.UpdatedAt = now;
            }
            else
                error = await ApplyDeliveryAsync(order, delivery, actorUserId, now, cancellationToken);

            if (error != null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return error;
            }
            if (!replay) await unitOfWork.SaveChangesAsync(cancellationToken);
            var (current, devices) = await purchaseOrderRepository.GetReceiptDetailAsync(id, cancellationToken);
            var detail = MapToDetailDto(current!, devices);
            await unitOfWork.CommitAsync(cancellationToken);
            return replay ? PurchaseReceiptResult.Replayed(detail) : PurchaseReceiptResult.Updated(detail);
        }
        catch (DuplicateKeyException ex)
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            return PurchaseReceiptResult.Conflict(ex.ConstraintName is "idx_serial_imeis_imei" or "idx_serial_imeis_serial"
                ? "Serial hoặc IMEI đã tồn tại trong hệ thống." : "Dữ liệu kho vừa thay đổi. Vui lòng thử lại.");
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<PurchaseReceiptResult?> ApplyDeliveryAsync(PurchaseOrder order, ReceivePurchaseDeliveryRequest delivery,
        Guid actorUserId, DateTime now, CancellationToken cancellationToken)
    {
        if (delivery.Lines is not { Count: > 0 and <= 100 })
            return InvalidDelivery("lines", "Mỗi lần nhận cần từ 1 đến 100 dòng hàng.");

        var document = string.IsNullOrWhiteSpace(delivery.SupplierDocumentNumber) ? order.SupplierDocumentNumber : delivery.SupplierDocumentNumber.Trim();
        if (document?.Length > 100) return InvalidDelivery("supplierDocumentNumber", "Số chứng từ không được vượt quá 100 ký tự.");

        var items = order.Items.ToDictionary(i => i.Id);
        var lines = new Dictionary<Guid, PurchaseDeliveryLine>();
        var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imeis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deviceCount = 0;
        foreach (var line in delivery.Lines)
        {
            if (line == null || !items.TryGetValue(line.LineId, out var item) || !lines.TryAdd(line.LineId, line))
                return InvalidDelivery("lines", "Dòng hàng không thuộc phiếu hoặc bị lặp.");
            if (line.Quantity <= 0)
                return InvalidDelivery("quantity", "Số lượng nhận phải lớn hơn 0.");
            if (line.ExpectedReceivedQuantity != item.ReceivedQuantity || line.Quantity > item.OrderedQuantity - item.ReceivedQuantity)
                return PurchaseReceiptResult.Conflict("Số lượng còn lại đã thay đổi hoặc không đủ. Hãy tải lại phiếu.");
            var count = line.Devices?.Count ?? 0;
            if (count != (item.Product.IsSerialTracked ? line.Quantity : 0) || count > 500 - deviceCount)
                return InvalidDelivery("devices", "Số máy phải khớp số lượng thực nhận, tối đa 500 máy mỗi đợt.");
            deviceCount += count;
            foreach (var device in line.Devices ?? [])
            {
                if (device == null) return InvalidDelivery("devices", "Mã máy không hợp lệ.");
                if (!SerialImeiValidationHelper.ValidateDeviceIdentifiers(device.SerialNumber, device.Imei,
                        out var serial, out var imei, out _, out var message))
                    return InvalidDelivery("devices", message!);
                if ((serial != null && !serials.Add(serial)) || (imei != null && !imeis.Add(imei)))
                    return PurchaseReceiptResult.Conflict("Trùng Serial hoặc IMEI trong đợt nhận.");
            }
        }

        if (deviceCount > 0)
        {
            var existing = await serialImeiRepository.FindExistingIdentifiersAsync(serials.ToList(), imeis.ToList(), cancellationToken);
            if (existing.HasAnyDuplicates)
                return PurchaseReceiptResult.Conflict("Serial hoặc IMEI đã tồn tại trong kho.",
                    existing.ExistingSerials.Concat(existing.ExistingImeis).ToList());
        }

        var devices = new List<SerialImei>();
        foreach (var item in lines.Keys.Select(id => items[id]).OrderByStockLockKey(i => i.ProductId, i => i.VariantId))
        {
            var line = lines[item.Id];
            await ReceiveLineAsync(order, item, line.Quantity, line.Devices, actorUserId, now, delivery.ClientRequestId, document, devices, cancellationToken);
            order.TotalCost += item.UnitCost * line.Quantity;
        }
        if (devices.Count > 0) serialImeiRepository.AddRange(devices);
        order.ReceivedAt = order.UpdatedAt = now;
        order.ReceivedByUserId = actorUserId;
        order.Status = order.Items.All(i => i.ReceivedQuantity == i.OrderedQuantity)
            ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        return null;
    }

    private static PurchaseReceiptResult InvalidDelivery(string field, string message) =>
        PurchaseReceiptResult.ValidationFailed(new() { [field] = [message] });
}
