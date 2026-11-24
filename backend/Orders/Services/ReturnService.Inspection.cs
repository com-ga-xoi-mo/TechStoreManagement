using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Orders.Services;

public sealed partial class ReturnService
{
    public async Task<List<ReturnInspectionDto>> InspectionsAsync(CancellationToken ct) =>
        (await returns.InspectionsAsync(ct)).Select(Inspection).ToList();

    public Task<ReturnInspectionDto> InspectAsync(Guid lineId, InspectReturnRequest input, AuthenticatedUser actor, CancellationToken ct) =>
        WriteAsync(async () =>
        {
            if (input.OperationId == Guid.Empty || input.Quantity <= 0)
                throw ReturnProblem.Invalid("quantity", "Cần mã lần kiểm định và số lượng dương.");
            var disposition = input.Outcome switch
            {
                "InStock" => ReturnDisposition.Restock,
                "UnderRepair" => ReturnDisposition.UnderRepair,
                "Defective" => ReturnDisposition.Defective,
                _ => throw ReturnProblem.Invalid("outcome", "Kết quả kiểm định không hợp lệ.")
            };
            var line = await returns.LockInspectionAsync(lineId, ct) ?? throw ReturnProblem.Missing();
            var previous = line.Settlements.SingleOrDefault(s => s.InspectionId == input.OperationId);
            if (previous != null)
            {
                if (previous.Quantity != input.Quantity || previous.Disposition != input.Outcome)
                    throw ReturnProblem.Conflict("Mã kiểm định đã dùng cho kết quả khác.");
                return Inspection(line);
            }
            var pending = line.Settlements.SingleOrDefault(s => s.Disposition == "Pending");
            if (pending == null || input.Quantity > pending.Quantity)
                throw ReturnProblem.Conflict("Số lượng kiểm định vượt phần còn chờ.");
            var slice = input.Quantity == pending.Quantity ? pending : Split(pending, input.Quantity, line);
            RequireStock(await inventory.InspectAsync(input.OperationId, slice.Id, StockLine(line.Item, input.Quantity), disposition, actor.Id, ct));
            slice.InspectionId = input.OperationId;
            slice.Disposition = input.Outcome;
            slice.InspectedAt = clock.GetUtcNow().UtcDateTime;
            slice.IsRestocked = disposition == ReturnDisposition.Restock;
            return Inspection(line);
        }, ct);

    private OrderReturn Split(OrderReturn pending, int quantity, ReturnCaseLine line)
    {
        decimal Part(decimal? amount) => decimal.Round(amount!.Value * quantity / pending.Quantity, 2, MidpointRounding.AwayFromZero);
        var slice = new OrderReturn
        {
            CaseLineId = line.Id, Quantity = quantity, OrderId = pending.OrderId, OrderItemId = pending.OrderItemId,
            ProcessedByUserId = pending.ProcessedByUserId, RefundPosSessionId = pending.RefundPosSessionId,
            RefundMethod = pending.RefundMethod, RefundedAt = pending.RefundedAt, Reason = pending.Reason,
            NetAmount = Part(pending.NetAmount), TaxAmount = Part(pending.TaxAmount), DiscountAmount = Part(pending.DiscountAmount)
        };
        slice.RefundAmount = slice.NetAmount.Value + slice.TaxAmount.Value;
        pending.NetAmount -= slice.NetAmount;
        pending.TaxAmount -= slice.TaxAmount;
        pending.DiscountAmount -= slice.DiscountAmount;
        pending.RefundAmount -= slice.RefundAmount;
        pending.Quantity -= quantity;
        returns.AddSettlement(slice);
        return slice;
    }

    private static ReturnInspectionDto Inspection(ReturnCaseLine l)
    {
        int Quantity(string disposition) => l.Settlements.Where(x => x.Disposition == disposition).Sum(x => x.Quantity);
        return new(l.Id, l.CaseId, l.Item.ProductName, l.Item.SerialImeiId,
            l.Item.SerialImei?.Imei ?? l.Item.SerialImei?.SerialNumber, l.Quantity,
            Quantity("Pending"), Quantity("InStock"), Quantity("Defective"), Quantity("UnderRepair"), l.Item.Sku);
    }
}
