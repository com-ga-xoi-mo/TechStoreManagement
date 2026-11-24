using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Repositories;
using TechStore.Api.Sales.Repositories;
using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Services;

public enum VietQrCloseReason { Cancel, Expire }
public record ClosedVietQrHold(Guid Id, Guid OrderId, string Status, string OrderStatus);

public interface IVietQrHoldService
{
    Task<PosResult<ClosedVietQrHold>> CloseAsync(Guid id, VietQrCloseReason reason, AuthenticatedUser actor, CancellationToken ct);
}

public sealed class VietQrHoldService(IVietQrHoldRepository payments, IPosSessionRepository shifts, IOrderRepository orders,
    IInventoryAfterSaleService inventory, IUnitOfWork work, TimeProvider clock) : IVietQrHoldService
{
    public async Task<PosResult<ClosedVietQrHold>> CloseAsync(Guid id, VietQrCloseReason reason, AuthenticatedUser actor, CancellationToken ct)
    {
        var owner = await payments.FindOwnerAsync(id, ct);
        if (owner == null || actor.Role is not (RoleType.SalesStaff or RoleType.Admin))
            return PosResult<ClosedVietQrHold>.NotFound();

        await work.BeginTransactionAsync(ct);
        try
        {
            var shift = owner.ShiftId.HasValue ? await shifts.GetForUpdateAsync(owner.ShiftId.Value, ct) : null;
            var order = await orders.GetForUpdateWithItemsAsync(owner.OrderId, ct);
            if (order == null || actor.Role == RoleType.SalesStaff && order.CashierUserId != actor.Id)
            {
                await work.RollbackAsync(CancellationToken.None);
                return PosResult<ClosedVietQrHold>.NotFound();
            }

            var attempts = await payments.LockPaymentsAsync(order.Id, ct);
            var qr = attempts.SingleOrDefault(q => q.Id == id);
            if (shift == null || shift.CashierUserId != order.CashierUserId || qr == null || qr.PosSessionId != shift.Id ||
                order.PaymentMethod != PaymentMethod.VietQr || order.PaidAt != null || order.CompletedAt != null ||
                qr.Amount != order.TotalAmount || qr.PosSessionId != order.PosSessionId || qr.ConfirmedAt != null ||
                qr.ProviderTransactionId != null || attempts.Any(q => q.Id != id && q.Status is VietQrStatus.Pending or VietQrStatus.Confirmed))
                return await ConflictAsync("Không thể giải phóng lượt giữ của giao dịch này. Vui lòng đối soát đơn và chứng từ thanh toán.");

            var target = reason == VietQrCloseReason.Cancel ? VietQrStatus.Failed : VietQrStatus.Expired;
            bool replay = order.Status == OrderStatus.Cancelled && qr.Status == target;
            var now = clock.GetUtcNow().UtcDateTime;
            if (!replay)
            {
                if (order.Status != OrderStatus.Pending || qr.Status != VietQrStatus.Pending)
                    return await ConflictAsync("Đơn hoặc giao dịch VietQR không còn chờ thanh toán.");
                if (reason == VietQrCloseReason.Expire && now < qr.ExpiresAt)
                    return await ConflictAsync("Mã VietQR chưa hết hạn.");
                if (reason == VietQrCloseReason.Cancel && now >= qr.ExpiresAt)
                    return await ConflictAsync("Mã VietQR đã hết hạn. Chọn xử lý hết hạn.");
                if (order.VoucherId.HasValue)
                    return await ConflictAsync("Cần giải phóng lượt voucher cùng giao dịch. Lượt giữ hàng và voucher vẫn còn nguyên.");
            }

            // The QR UUID is the durable release key. Inventory compares the payload even on a terminal-state retry.
            var lines = order.Items.OrderBy(i => i.Id)
                .Select(i => new FulfillmentLine(i.Id, i.ProductId, i.VariantId, i.SerialImeiId, i.Quantity)).ToList();
            var release = await inventory.ReleaseAsync(id, order.Id, lines, ct);
            if (!release.IsSuccess) return await ConflictAsync(release.Conflict!.Message);

            if (!replay)
            {
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = now;
                qr.Status = target;
                qr.UpdatedAt = now;
                await work.SaveChangesAsync(ct);
            }
            await work.CommitAsync(ct);
            return PosResult<ClosedVietQrHold>.Success(new(qr.Id, order.Id, qr.Status.ToString(), order.Status.ToString()));
        }
        catch
        {
            await work.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<PosResult<ClosedVietQrHold>> ConflictAsync(string detail)
    {
        await work.RollbackAsync(CancellationToken.None);
        return PosResult<ClosedVietQrHold>.Conflict(detail);
    }
}
