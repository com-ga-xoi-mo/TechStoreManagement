using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Api.Identity.Services;
using TechStore.Api.Sales.Services;

namespace TechStore.Api.Sales;

[ApiController]
[Route("api/pos/vietqr")]
[Authorize(Roles = "Admin,SalesStaff")]
public sealed class VietQrHoldsController(IVietQrHoldService holds) : ControllerBase
{
    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) => CloseAsync(id, VietQrCloseReason.Cancel, ct);

    [HttpPost("{id:guid}/expire")]
    public Task<IActionResult> Expire(Guid id, CancellationToken ct) => CloseAsync(id, VietQrCloseReason.Expire, ct);

    private async Task<IActionResult> CloseAsync(Guid id, VietQrCloseReason reason, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await holds.CloseAsync(id, reason, actor, ct);
        return result.Kind switch
        {
            PosResultKind.Success => Ok(result.Data),
            PosResultKind.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, detail: result.ConflictReason)
        };
    }
}
