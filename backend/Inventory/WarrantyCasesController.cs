using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory;

[ApiController]
[Route("api/warranties")]
[Authorize(Roles = "Admin,Manager")]
public sealed class WarrantyCasesController(IWarrantyCaseService service) : ControllerBase
{
    [HttpGet("{caseId:guid}")]
    public async Task<IActionResult> Get(Guid caseId, CancellationToken ct) => Map(await service.GetAsync(caseId, ct));

    [HttpPost("{caseId:guid}/receive")]
    public async Task<IActionResult> Receive(Guid caseId, ReceiveWarrantyRequest request, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        if (caseId == Guid.Empty || request.OrderId == Guid.Empty || request.OrderItemId == Guid.Empty || request.DeviceId == Guid.Empty)
            return BadRequest();
        return Map(await service.ReceiveAsync(caseId, request, actor, ct));
    }

    [HttpPost("{caseId:guid}/return")]
    public async Task<IActionResult> ReturnToCustomer(Guid caseId, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return Map(await service.ResolveAsync(caseId, null, actor, ct));
    }

    [HttpPost("{caseId:guid}/replace")]
    public async Task<IActionResult> Replace(Guid caseId, ReplaceWarrantyDeviceRequest request, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        if (request.ReplacementDeviceId == Guid.Empty) return BadRequest();
        return Map(await service.ResolveAsync(caseId, request.ReplacementDeviceId, actor, ct));
    }

    private IActionResult Map(WarrantyResult result) => result.Data != null ? Ok(result.Data) :
        result.Conflict == null ? NotFound() : Problem(statusCode: StatusCodes.Status409Conflict, detail: result.Conflict);
}
