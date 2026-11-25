using TechStore.Client.Models.Vouchers;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Vouchers;

/// <summary>Voucher management (Admin and Manager). HTTP errors surface as <c>ApiException</c>.</summary>
public interface IVoucherRepository
{
    Task<VoucherPage> SearchAsync(VoucherQuery query, CancellationToken cancellationToken = default);
    Task<VoucherItem> CreateAsync(CreateVoucherRequest request, CancellationToken cancellationToken = default);
    Task<VoucherItem> UpdateAsync(Guid voucherId, UpdateVoucherRequest request, CancellationToken cancellationToken = default);
    Task<VoucherItem> SetActiveAsync(Guid voucherId, bool isActive, CancellationToken cancellationToken = default);
}
