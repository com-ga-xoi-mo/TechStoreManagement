using System.Globalization;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Warranty.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Warranty.Services;

public sealed partial class WarrantyCaseService
{
    private const int MaxCodeAttempts = 5;

    public async Task<WarrantyResult> ReceiveAsync(Guid id, ReceiveWarrantyRequest request, AuthenticatedUser actor, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (id == Guid.Empty)
            errors["caseId"] = ["Mã định danh hồ sơ là bắt buộc."];
        var code = request.Code?.Trim();
        if (string.IsNullOrEmpty(code))
            errors["code"] = ["Nhập IMEI hoặc số sê-ri."];
        var issue = RequiredText(request.IssueDescription, "issueDescription", 1000, errors);
        var name = RequiredText(request.ContactName, "contactName", 100, errors);
        var phone = RequiredText(request.ContactPhone, "contactPhone", 20, errors);
        if (errors.Count > 0)
            return new(null) { Errors = errors };

        for (var attempt = 1; attempt <= MaxCodeAttempts; attempt++)
        {
            var device = await cases.FindDeviceByCodeAsync(code!, ct);
            if (device == null)
                return EligibilityConflict([WarrantyReasonCodes.NoCoveringSale]);

            var sale = await cases.FindSaleAsync(device, ct);
            try
            {
                return await WriteAsync(
                    () => ReceiveLockedAsync(id, request, issue!, name!, phone!, actor, device, sale, ct),
                    ct, CanViewReplacementCost(actor.Role));
            }
            catch (DuplicateKeyException ex) when (ex.ConstraintName == "idx_warranty_cases_code")
            {
                if (attempt == MaxCodeAttempts)
                    return WarrantyResult.Rejected("Chưa cấp được mã bảo hành. Vui lòng thử lại.");
            }
            catch (DuplicateKeyException ex) when (ex.ConstraintName == "uq_warranty_cases_open_device")
            {
                return EligibilityConflict([WarrantyReasonCodes.OpenCase]);
            }
        }

        throw new InvalidOperationException("The last warranty code attempt must return a result.");
    }

    private async Task<WarrantyResult> ReceiveLockedAsync(Guid id, ReceiveWarrantyRequest request,
        string issue, string name, string phone, AuthenticatedUser actor, SerialImei originalDevice, OrderItem? originalSale, CancellationToken ct)
    {
        var order = originalSale == null ? null : await sales.LockOrderAsync(originalSale.OrderId, ct);
        var existing = await cases.LockAsync(id, ct);
        if (existing != null)
            return existing.DeviceId == originalDevice.Id
                ? Map(existing) with { IsReplay = true }
                : WarrantyResult.Rejected("Mã định danh hồ sơ đã được dùng cho thiết bị khác.");

        var now = ToDatabaseTime(clock.GetUtcNow().UtcDateTime);
        var device = await cases.FindDeviceByCodeAsync(request.Code!.Trim(), ct);
        if (device?.Id != originalDevice.Id)
            return EligibilityConflict([WarrantyReasonCodes.NoCoveringSale]);
        var sale = await cases.FindSaleAsync(device, ct);
        var history = await cases.CasesForDeviceAsync(device.Id, ct);
        var reasons = await EligibilityReasonsAsync(device, sale, history, request.OrderCode, request.CustomerId, now, ct);
        if (reasons.Count != 0)
            return EligibilityConflict(reasons);
        if (order == null || sale?.OrderId != order.Id)
            return EligibilityConflict([WarrantyReasonCodes.NoCoveringSale]);

        var receiveId = Guid.NewGuid();
        var received = await inventory.ApplyWarrantyAsync(receiveId, id, order.Id, sale.Id, device.Id,
            WarrantyAction.ReceiveForRepair, null, actor.Id, ct);

        // The inventory change is staged. The untracked read sees the device state protected by the inventory lock.
        var lockedDevice = await cases.FindDeviceByCodeAsync(request.Code.Trim(), ct);
        if (lockedDevice?.Id != originalDevice.Id)
            return EligibilityConflict([WarrantyReasonCodes.NoCoveringSale]);
        var lockedSale = await cases.FindSaleAsync(lockedDevice, ct);
        now = ToDatabaseTime(clock.GetUtcNow().UtcDateTime);
        reasons = await EligibilityReasonsAsync(lockedDevice, lockedSale, await cases.CasesForDeviceAsync(device.Id, ct),
            request.OrderCode, request.CustomerId, now, ct);
        if (reasons.Count != 0)
            return EligibilityConflict(reasons);
        if (lockedSale?.Id != sale.Id || lockedSale.OrderId != order.Id)
            return EligibilityConflict([WarrantyReasonCodes.NoCoveringSale]);
        if (!received.IsSuccess)
            return WarrantyResult.Rejected(received.Conflict!.Message) with { Reasons = [] };

        var prefix = "BH-" + now.ToString("yyyyMM", CultureInfo.InvariantCulture) + "-";
        var lastCode = await cases.GetLastCaseCodeAsync(prefix, ct);
        var sequence = lastCode != null && int.TryParse(lastCode.AsSpan(prefix.Length), out var last) ? last + 1 : 1;
        var claim = new WarrantyCase
        {
            Id = id,
            CaseCode = prefix + sequence.ToString("D4", CultureInfo.InvariantCulture),
            OrderId = order.Id,
            OrderItemId = sale.Id,
            CustomerId = order.CustomerId,
            DeviceId = lockedDevice.Id,
            OpenedAt = now,
            OpenedById = actor.Id,
            WarrantyStartAt = lockedDevice.WarrantyStartAt!.Value,
            WarrantyEndAt = lockedDevice.WarrantyEndAt!.Value,
            Status = WarrantyCaseStatus.Received,
            ReceiveOperationId = receiveId,
            IssueDescription = issue,
            ContactName = name,
            ContactPhone = phone
        };
        cases.Add(claim);
        cases.AddEvent(new WarrantyCaseEvent
        {
            CaseId = id, ActorId = actor.Id, ToStatus = WarrantyCaseStatus.Received, CreatedAt = now
        });
        return Map(claim) with { Created = true };
    }

    private static WarrantyResult EligibilityConflict(IReadOnlyList<string> reasons) =>
        WarrantyResult.Rejected("Thiết bị không đủ điều kiện bảo hành.") with { Reasons = reasons };

    private static string? RequiredText(string? value, string field, int maxLength, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
            errors[field] = [$"Nhập từ 1 đến {maxLength} ký tự."];
        return trimmed;
    }
}
