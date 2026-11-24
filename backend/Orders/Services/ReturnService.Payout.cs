using TechStore.Api.Identity.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Orders.Services;

public sealed partial class ReturnService
{
    public async Task<ReturnDto> SettleAsync(Guid id, SettleReturnRequest input, AuthenticatedUser actor, CancellationToken ct)
    {
        if (input.Method is not ("Cash" or "VietQr"))
            throw ReturnProblem.Invalid("method", "Chọn Cash hoặc VietQr.");
        var previous = await GetAsync(id, actor, ct);
        if (previous.Status != ReturnStatus.Pending)
            return ExistingPayout(previous, input.Method);

        return await WriteAsync(async () =>
        {
            Guid? shiftId = null;
            if (input.Method == "Cash")
            {
                shiftId = await cash.LockOpenShiftAsync(actor.Id, ct);
                if (shiftId == null) throw ReturnProblem.Conflict("Cần ca đang mở của chính người hoàn tiền.");
            }
            var order = await VisibleOrderAsync(previous.OrderId, actor, true, ct);
            var request = await returns.FindAsync(id, true, ct) ?? throw ReturnProblem.Missing();
            if (request.Status != ReturnStatus.Pending)
                return ExistingPayout(Map(request), input.Method);

            var allocation = await RequoteAsync(order, request, ct);
            request.Method = input.Method;
            request.PayoutActorId = actor.Id;
            if (input.Method == "VietQr")
            {
                var stock = StockLines(order, request);
                RequireStock(order.Status == OrderStatus.Processing
                    ? await inventory.CheckReleaseAsync(order.Id, stock, ct)
                    : await inventory.CheckReturnAsync(order.Id, stock, ct));
                request.BankApprovedAt = ToDatabaseTime(clock.GetUtcNow().UtcDateTime);
                request.Status = ReturnStatus.AwaitingBank;
            }
            else
            {
                await cash.StagePayoutAsync(id, shiftId!.Value, actor.Id, allocation.Lines.Sum(l => l.RefundAmount), ct);
                await CompleteAsync(order, request, allocation, shiftId, ct);
            }
            return Map(request);
        }, ct);
    }

    // Manual bank mode: a privileged operator attests a verified bank outcome using
    // its unique transaction reference. This endpoint never issues a transfer.
    public async Task<ReturnDto> ReconcileAsync(Guid id, ReconcileReturnRequest input, AuthenticatedUser actor, CancellationToken ct)
    {
        var reference = input.BankReference?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(reference) || reference.Length > 128)
            throw ReturnProblem.Invalid("bankReference", "Nhập mã giao dịch ngân hàng đã xác minh (tối đa 128 ký tự).");
        var previous = await GetAsync(id, actor, ct);
        return await WriteAsync(async () =>
        {
            var order = await VisibleOrderAsync(previous.OrderId, actor, true, ct);
            var request = await returns.FindAsync(id, true, ct) ?? throw ReturnProblem.Missing();
            var expectedPaid = input.Paid ? request.Lines.Sum(l => l.NetAmount + l.TaxAmount) : 0m;
            if (request.Method != "VietQr" || input.Amount != expectedPaid)
                throw ReturnProblem.Conflict("Kênh hoàn hoặc số tiền không khớp phiếu.");
            DateTime? paidAt = input.PaidAt is { } paid ? ToDatabaseTime(paid.UtcDateTime) : null;
            var target = input.Paid ? ReturnStatus.Refunded : ReturnStatus.Failed;
            if (request.Status == target && request.BankReference == reference && (!input.Paid || request.RefundedAt == paidAt))
                return Map(request);
            if (request.Status != ReturnStatus.AwaitingBank)
                throw ReturnProblem.Conflict("Phiếu không còn chờ xác nhận ngân hàng.");
            if (input.Paid)
            {
                // Older pending cases have no recorded approval time. The operator must verify it
                // against the original approval before applying the bank receipt.
                DateTime? approvedAt = request.BankApprovedAt;
                if (approvedAt is null && input.VerifiedLegacyApprovalAt is { } legacyApproval)
                    approvedAt = ToDatabaseTime(legacyApproval.UtcDateTime);
                if (approvedAt is null || approvedAt < request.CreatedAt)
                    throw ReturnProblem.Invalid("verifiedLegacyApprovalAt", "Cần xác minh ngày giờ duyệt chi gốc của phiếu cũ.");
                if (paidAt is null || paidAt < approvedAt || paidAt > clock.GetUtcNow().UtcDateTime)
                    throw ReturnProblem.Invalid("paidAt", "Thời điểm thực chi phải từ lúc duyệt chi đến hiện tại. Kiểm tra cả giây trên chứng từ.");
                request.BankApprovedAt = approvedAt;
            }
            request.BankReference = reference;
            request.PayoutActorId = actor.Id;
            if (input.Paid)
                await CompleteAsync(order, request, await RequoteAsync(order, request, ct), null, ct, paidAt);
            else
                request.Status = ReturnStatus.Failed;
            return Map(request);
        }, ct);
    }

    private static DateTime ToDatabaseTime(DateTime utc) => utc.AddTicks(-(utc.Ticks % 10));

    private static ReturnDto ExistingPayout(ReturnDto value, string method)
    {
        if (value.Method == method && value.Status is ReturnStatus.Refunded or ReturnStatus.AwaitingBank) return value;
        throw ReturnProblem.Conflict("Phiếu đã được xử lý với kết quả hoặc phương thức khác.");
    }


    private async Task<RefundAllocationResult> RequoteAsync(Order order, ReturnCase request, CancellationToken ct)
    {
        if (request.WasUndelivered != (order.Status == OrderStatus.Processing))
            throw ReturnProblem.Conflict("Tình trạng giao hàng đã thay đổi. Hãy kiểm tra hàng thực nhận và lập yêu cầu mới.");
        var allocation = await QuoteAsync(order, request.Lines.ToDictionary(x => x.OrderItemId, x => x.Quantity), request.Id, ct);
        var quotes = allocation.Lines.ToDictionary(x => x.OrderItemId);
        if (request.Lines.Any(x => quotes[x.OrderItemId].NetAmount != x.NetAmount ||
                                   quotes[x.OrderItemId].TaxAmount != x.TaxAmount ||
                                   quotes[x.OrderItemId].DiscountAmount != x.DiscountAmount))
            throw ReturnProblem.Conflict("Phân bổ tiền đã thay đổi do phiếu khác. Lập yêu cầu mới để kiểm tra lại.");
        return allocation;
    }

    private static List<Inventory.Services.FulfillmentLine> StockLines(Order order, ReturnCase request)
    {
        var items = order.Items.ToDictionary(x => x.Id);
        return request.Lines.OrderBy(l => l.OrderItemId).Select(l => StockLine(items[l.OrderItemId], l.Quantity)).ToList();
    }

    private async Task CompleteAsync(Order order, ReturnCase request, RefundAllocationResult allocation, Guid? shift, CancellationToken ct, DateTime? bankPaidAt = null)
    {
        var notDelivered = order.Status == OrderStatus.Processing;
        var stock = StockLines(order, request);
        RequireStock(notDelivered ? await inventory.ReleaseAsync(request.Id, order.Id, stock, ct)
                                  : await inventory.ReceiveReturnAsync(request.Id, order.Id, stock, ct));
        if (!notDelivered && order.CustomerId is Guid customerId)
        {
            await loyalty.StageReversalAsync(request.Id, order.Id, customerId, allocation.RemainingNetAmount, ct);
        }
        var now = clock.GetUtcNow().UtcDateTime;
        var refundedAt = bankPaidAt ?? now.AddTicks(-(now.Ticks % 10));
        foreach (var l in request.Lines)
        {
            returns.AddSettlement(new OrderReturn
            {
                CaseLineId = l.Id, OrderId = order.Id, OrderItemId = l.OrderItemId, ProcessedByUserId = request.PayoutActorId!.Value,
                Quantity = l.Quantity, Reason = request.Reason, NetAmount = l.NetAmount, TaxAmount = l.TaxAmount,
                DiscountAmount = l.DiscountAmount, RefundAmount = l.NetAmount + l.TaxAmount,
                RefundMethod = shift.HasValue ? PaymentMethod.Cash : PaymentMethod.VietQr,
                RefundPosSessionId = shift, RefundedAt = refundedAt, Disposition = notDelivered ? "Undelivered" : "Pending"
            });
        }
        request.Status = ReturnStatus.Refunded;
        request.RefundedAt = refundedAt;
        order.Status = allocation.IsFullyReturned ? OrderStatus.Refunded : OrderStatus.PartiallyRefunded;
        order.UpdatedAt = now;
    }

    public Task<ReturnDto> ExchangeAsync(Guid id, LinkExchangeRequest input, AuthenticatedUser actor, CancellationToken ct) =>
        WriteAsync(async () =>
        {
            var request = await returns.FindAsync(id, true, ct) ?? throw ReturnProblem.Missing();
            var source = await VisibleOrderAsync(request.OrderId, actor, false, ct);
            var replacement = await orders.GetWithItemsAsync(input.ReplacementOrderId, ct) ?? throw ReturnProblem.Missing();
            if (request.Status != ReturnStatus.Refunded || replacement.Id == source.Id || replacement.CheckoutKey != id ||
                replacement.CustomerId != source.CustomerId || replacement.Status != OrderStatus.Completed ||
                replacement.CreatedAt < request.RefundedAt ||
                request.ReplacementOrderId.HasValue && request.ReplacementOrderId != replacement.Id)
                throw ReturnProblem.Conflict("Đơn thay thế không khớp giao dịch đổi hàng của phiếu.");
            request.ReplacementOrderId = replacement.Id;
            return Map(request);
        }, ct);
}
