using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Services;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Repositories;
using TechStore.Api.Sales.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Orders.Services;

public sealed partial class ReturnService(
    IReturnRepository returns, IOrderRepository orders, IInventoryAfterSaleService inventory,
    IUnitOfWork unit, TimeProvider clock, ICashRefundService cash,
    IRefundLoyaltyService loyalty) : IReturnService
{
    public async Task<OrderReturnsDto> OrderAsync(Guid orderId, AuthenticatedUser actor, CancellationToken ct)
    {
        var order = await VisibleOrderAsync(orderId, actor, false, ct);
        var history = await returns.HistoryAsync(orderId, ct);
        order.Returns = await returns.SettledAsync(orderId, ct);
        var returned = order.Returns.ToLookup(x => x.OrderItemId);
        var quantities = order.Items.ToDictionary(i => i.Id,
            i => Math.Max(0, i.Quantity - returned[i.Id].Sum(r => r.Quantity)));
        var remaining = quantities.Where(p => p.Value > 0).ToDictionary();
        var quote = RefundAllocation.Calculate(order, remaining);
        var amounts = quote.Lines.ToDictionary(l => l.OrderItemId, l => l.RefundAmount);
        var review = EligibleStatus(order.Status) ? quote.Error : null;
        if (history.Any(r => r.Status == ReturnStatus.AwaitingBank))
            review = "Đơn đang chờ đối soát ngân hàng.";
        var available = EligibleStatus(order.Status) && review == null;
        var balances = order.Items.OrderBy(i => i.Id)
            .Select(i => new ReturnBalanceDto(i.Id, i.ProductName, i.SerialImeiId, i.SerialImei?.Imei ?? i.SerialImei?.SerialNumber,
                available ? quantities[i.Id] : 0, available ? amounts.GetValueOrDefault(i.Id) : 0, i.Sku, i.VariantName)).ToList();
        var legacy = order.Returns.Where(r => r.CaseLineId == null).OrderByDescending(r => r.RefundedAt)
            .Select(r => new LegacyReturnDto(r.Id, r.OrderItemId, r.Quantity, r.RefundAmount, r.RefundMethod.ToString(),
                r.RefundedAt, r.Reason, r.IsRestocked)).ToList();
        return new(order.Id, order.OrderCode, order.Status.ToString(), balances, history.Select(Map).ToList(), legacy, review);
    }

    public async Task<ReturnDto> GetAsync(Guid id, AuthenticatedUser actor, CancellationToken ct)
    {
        var request = await returns.FindAsync(id, false, ct) ?? throw ReturnProblem.Missing();
        await VisibleOrderAsync(request.OrderId, actor, false, ct);
        return Map(request);
    }

    public Task<ReturnDto> CreateAsync(CreateReturnRequest input, AuthenticatedUser actor, CancellationToken ct) =>
        WriteAsync(async () =>
        {
            var order = await VisibleOrderAsync(input.OrderId, actor, true, ct);
            if (input.RequestId == Guid.Empty) throw ReturnProblem.Invalid("requestId", "Thiếu mã yêu cầu.");
            var reason = input.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length > 500)
                throw ReturnProblem.Invalid("reason", "Nhập lý do từ 1 đến 500 ký tự.");
            if (input.Lines is not { Count: > 0 }) throw ReturnProblem.Invalid("lines", "Chọn ít nhất một dòng hàng.");
            var quantities = new Dictionary<Guid, int>();
            for (int i = 0; i < input.Lines.Count; i++)
            {
                var line = input.Lines[i];
                var item = line == null ? null : order.Items.FirstOrDefault(x => x.Id == line.OrderItemId);
                if (line == null || item == null || !quantities.TryAdd(item.Id, line.Quantity))
                    throw ReturnProblem.Invalid($"lines[{i}].orderItemId", "Dòng hàng phải thuộc đơn và chỉ xuất hiện một lần.");
                if (item.SerialImeiId != line.SerialImeiId)
                    throw ReturnProblem.Invalid($"lines[{i}].serialImeiId", "IMEI không khớp với dòng bán.");
                if (line.Quantity <= 0 || line.Quantity > item.Quantity)
                    throw ReturnProblem.Invalid($"lines[{i}].quantity", "Số lượng trả không hợp lệ.");
            }

            var existing = await returns.FindAsync(input.RequestId, false, ct);
            if (existing != null)
            {
                if (existing.OrderId != order.Id || existing.RequestedById != actor.Id || existing.Reason != reason ||
                    existing.Lines.Count != quantities.Count || existing.Lines.Any(l => quantities.GetValueOrDefault(l.OrderItemId) != l.Quantity))
                    throw ReturnProblem.Conflict("Mã yêu cầu đã dùng cho nội dung khác.");
                return Map(existing);
            }

            var allocation = await QuoteAsync(order, quantities, Guid.Empty, ct);
            var request = new ReturnCase
            {
                Id = input.RequestId, OrderId = order.Id, RequestedById = actor.Id, Reason = reason,
                CreatedAt = clock.GetUtcNow().UtcDateTime, WasUndelivered = order.Status == OrderStatus.Processing,
                Lines = allocation.Lines.Select(l => new ReturnCaseLine
                {
                    OrderItemId = l.OrderItemId, Quantity = l.Quantity, NetAmount = l.NetAmount,
                    DiscountAmount = l.DiscountAmount, TaxAmount = l.TaxAmount
                }).ToList()
            };
            returns.Add(request);
            return Map(request);
        }, ct);

    private async Task<RefundAllocationResult> QuoteAsync(Order order, IReadOnlyDictionary<Guid, int> quantities, Guid except, CancellationToken ct)
    {
        if (!EligibleStatus(order.Status)) throw ReturnProblem.Conflict("Trạng thái đơn không cho phép hoàn.");
        if (await returns.HasBankHoldAsync(order.Id, except, ct))
            throw ReturnProblem.Conflict("Đơn đang chờ đối soát ngân hàng.");
        // Use a detached snapshot. Assigning detached historical rows to a tracked collection
        // would stage inserts or relationship changes when the UnitOfWork saves.
        var snapshot = await orders.GetWithItemsAsync(order.Id, ct) ?? throw ReturnProblem.Missing();
        snapshot.Returns = await returns.SettledAsync(order.Id, ct);
        var quote = RefundAllocation.Calculate(snapshot, quantities);
        if (quote.Error != null) throw ReturnProblem.Conflict(quote.Error);
        if (order.Status == OrderStatus.Processing && !quote.IsFullyReturned)
            throw ReturnProblem.Conflict("Đơn đã thanh toán chưa giao chỉ được hoàn toàn bộ.");
        return quote;
    }

    private static bool EligibleStatus(OrderStatus status) =>
        status is OrderStatus.Processing or OrderStatus.Completed or OrderStatus.PartiallyRefunded;

    private async Task<Order> VisibleOrderAsync(Guid id, AuthenticatedUser actor, bool forUpdate, CancellationToken ct)
    {
        var order = forUpdate ? await orders.GetForUpdateWithItemsAsync(id, ct) : await orders.GetWithItemsAsync(id, ct);
        if (order == null || actor.Role == RoleType.SalesStaff && order.CashierUserId != actor.Id)
            throw ReturnProblem.Missing();
        return order;
    }

    private async Task<T> WriteAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await unit.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            await unit.SaveChangesAsync(ct);
            await unit.CommitAsync(ct);
            return result;
        }
        catch (CashRefundConflictException ex)
        {
            await unit.RollbackAsync(CancellationToken.None);
            throw ReturnProblem.Conflict(ex.Message);
        }
        catch (DuplicateKeyException)
        {
            await unit.RollbackAsync(CancellationToken.None);
            throw ReturnProblem.Conflict("Mã thao tác hoặc mã giao dịch đã được sử dụng. Hãy tra cứu kết quả.");
        }
        catch
        {
            await unit.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static ReturnDto Map(ReturnCase r) => new(r.Id, r.OrderId, r.Status, r.Reason, r.Method,
        r.BankReference, r.ReplacementOrderId, r.CreatedAt, r.RefundedAt, r.Lines.OrderBy(l => l.OrderItemId)
            .Select(l => new ReturnLineDto(l.OrderItemId, l.Quantity, l.NetAmount, l.TaxAmount, l.DiscountAmount)).ToList(), r.BankApprovedAt);

    private static FulfillmentLine StockLine(OrderItem item, int quantity) =>
        new(item.Id, item.ProductId, item.VariantId, item.SerialImeiId, quantity);

    private static void RequireStock(InventoryOperationResult result)
    {
        if (!result.IsSuccess) throw ReturnProblem.Conflict(result.Conflict?.Message ?? "Tình trạng hàng đã thay đổi.");
    }
}
