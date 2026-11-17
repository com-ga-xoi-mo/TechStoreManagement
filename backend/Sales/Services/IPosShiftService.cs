using TechStore.Api.Identity.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Sales.Services;

// Cash shifts (pos_sessions): open, read and close with reconciliation.
// The role gate lives on the controller; ownership (SalesStaff sees and closes only their own shift) lives here.
public interface IPosShiftService
{
    Task<PosResult<PosShiftDto>> OpenAsync(
        OpenShiftRequest request,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default);

    Task<PosResult<PosShiftDto>> GetCurrentAsync(
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default);

    Task<PosResult<PosShiftDto>> GetByIdAsync(
        Guid id,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default);

    Task<PosResult<PagedResultDto<PosShiftDto>>> SearchAsync(
        PosShiftSearchRequest request,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default);

    Task<PosResult<PosShiftDto>> CloseAsync(
        Guid id,
        CloseShiftRequest request,
        AuthenticatedUser actor,
        CancellationToken cancellationToken = default);
}
