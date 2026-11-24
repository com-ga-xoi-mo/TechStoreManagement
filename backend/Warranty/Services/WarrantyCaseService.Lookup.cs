using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Warranty.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.Api.Warranty.Services;

public sealed record WarrantyLookupResult(WarrantyLookupDto? Data, IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed partial class WarrantyCaseService
{
    public async Task<WarrantyLookupResult> LookupAsync(string? code, string? orderCode, Guid? customerId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new(null, new Dictionary<string, string[]> { ["code"] = ["Nhập IMEI hoặc số sê-ri."] });

        var device = await cases.FindDeviceByCodeAsync(code.Trim(), ct);
        if (device == null)
            return new(null);

        var sale = await cases.FindSaleAsync(device, ct);
        var history = await cases.CasesForDeviceAsync(device.Id, ct);
        var reasons = await EligibilityReasonsAsync(device, sale, history, orderCode, customerId, clock.GetUtcNow().UtcDateTime, ct);
        var provenSale = IsCoveringSale(sale) && ProofFailure(sale!.Order, orderCode, customerId) == null ? sale : null;
        var order = provenSale?.Order;
        return new(new WarrantyLookupDto
        {
            DeviceId = device.Id,
            Imei = device.Imei,
            SerialNumber = device.SerialNumber,
            DeviceStatus = device.Status.ToString(),
            ProductId = device.ProductId,
            VariantId = device.VariantId,
            ProductName = device.Product.Name,
            VariantName = device.Variant?.VariantName,
            WarrantyStartAt = device.WarrantyStartAt,
            WarrantyEndAt = device.WarrantyEndAt,
            Eligible = reasons.Count == 0,
            Reasons = reasons,
            OrderCode = order?.OrderCode,
            PurchasedAt = order?.CompletedAt,
            CustomerId = order?.CustomerId,
            CustomerName = order?.Customer?.FullName,
            CustomerPhone = order?.Customer?.Phone,
            IsWalkIn = order == null ? null : order.CustomerId == null,
            History = provenSale == null ? [] : history.Where(c => c.OrderItemId == provenSale.Id)
                .Select(c => new WarrantyCaseSummaryDto(c.Id, c.CaseCode, c.Status.ToString(), c.OpenedAt,
                    provenSale.ProductName, device.Imei ?? device.SerialNumber, provenSale.Order.OrderCode,
                    provenSale.Order.Customer?.FullName, null, null)).ToArray()
        });
    }

    private async Task<IReadOnlyList<string>> EligibilityReasonsAsync(SerialImei device, OrderItem? sale,
        IReadOnlyList<WarrantyCase> history, string? orderCode, Guid? customerId, DateTime now, CancellationToken ct)
    {
        var reasons = new List<string>();
        if (device.Status != SerialImeiStatus.Sold)
            reasons.Add(WarrantyReasonCodes.NotSold);
        if (!IsCoveringSale(sale))
            reasons.Add(WarrantyReasonCodes.NoCoveringSale);
        if (device.WarrantyStartAt is not DateTime start || device.WarrantyEndAt is not DateTime end || now < start || now >= end)
            reasons.Add(WarrantyReasonCodes.Expired);
        if (sale != null)
        {
            if (await cases.ReturnedQuantityAsync(sale.Id, ct) >= sale.Quantity)
                reasons.Add(WarrantyReasonCodes.LineReturned);
            if (await cases.HasOpenReturnRequestAsync(sale.Id, device.Id, ct))
                reasons.Add(WarrantyReasonCodes.PendingReturn);
            if (IsCoveringSale(sale) && ProofFailure(sale.Order, orderCode, customerId) is { } proofFailure)
                reasons.Add(proofFailure);
        }
        if (device.WarrantyCaseId != null || history.Any(c => WarrantyCaseTransitions.IsOpen(c.Status)))
            reasons.Add(WarrantyReasonCodes.OpenCase);
        return reasons;
    }

    private static bool IsCoveringSale(OrderItem? sale) =>
        sale?.Order is { CompletedAt: not null, Status: OrderStatus.Completed or OrderStatus.PartiallyRefunded };

    private static string? ProofFailure(Order order, string? orderCode, Guid? customerId)
    {
        if (order.CustomerId.HasValue && order.CustomerId == customerId)
            return null;
        if (!string.IsNullOrWhiteSpace(orderCode))
            return string.Equals(orderCode.Trim(), order.OrderCode, StringComparison.OrdinalIgnoreCase)
                ? null : WarrantyReasonCodes.OrderMismatch;
        return order.CustomerId.HasValue ? WarrantyReasonCodes.CustomerMismatch : WarrantyReasonCodes.OrderCodeRequired;
    }
}
