using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Identity.Services;
using TechStore.Api.Warranty.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Warranty;

[ApiController]
[Route("api/warranties")]
public sealed class WarrantyCasesController(IWarrantyCaseService service) : ControllerBase
{
    private const string ReadRoles = "Admin,Manager,SalesStaff,WarehouseStaff";
    private const string IntakeRoles = "Admin,Manager,SalesStaff";
    private const string InspectionRoles = "Admin,WarehouseStaff";
    private const string ReplacementRoles = "Admin,Manager";

    [HttpGet("lookup")]
    [Authorize(Roles = ReadRoles)]
    public async Task<IActionResult> Lookup([FromQuery] string? code, [FromQuery] string? orderCode,
        [FromQuery] Guid? customerId, CancellationToken ct)
    {
        var includePhone = User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("SalesStaff");
        var result = await service.LookupAsync(code, orderCode, customerId, includePhone, ct);
        if (result.Errors != null)
            return Invalid(result.Errors);
        return result.Data == null ? NotFound() : Ok(result.Data);
    }

    [HttpGet]
    [Authorize(Roles = ReadRoles)]
    public async Task<IActionResult> Search(CancellationToken ct, [FromQuery] string? status = null,
        [FromQuery(Name = "q")] string? query = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor))
            return Unauthorized();
        var result = await service.SearchAsync(status, query, page, pageSize,
            WarrantyCaseService.CanViewReplacementCost(actor.Role), ct);
        return result.Errors == null ? Ok(result.Data) : Invalid(result.Errors);
    }

    [HttpGet("{caseId:guid}")]
    [Authorize(Roles = ReadRoles)]
    public async Task<IActionResult> Get(Guid caseId, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor))
            return Unauthorized();
        return Map(await service.GetAsync(caseId, WarrantyCaseService.CanViewReplacementCost(actor.Role), ct));
    }

    [HttpPost("{caseId:guid}/receive")]
    [Authorize(Roles = IntakeRoles)]
    public async Task<IActionResult> Receive(Guid caseId, ReceiveWarrantyRequest request, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor))
            return Unauthorized();
        var result = await service.ReceiveAsync(caseId, request, actor, ct);
        return result.Created ? CreatedAtAction(nameof(Get), new { caseId }, result.Data) : Map(result);
    }

    [HttpPost("{caseId:guid}/inspect")]
    [Authorize(Roles = InspectionRoles)]
    public async Task<IActionResult> Inspect(Guid caseId, InspectWarrantyRequest request, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor))
            return Unauthorized();
        return Map(await service.InspectAsync(caseId, request, actor, ct));
    }

    [HttpPost("{caseId:guid}/repaired")]
    [Authorize(Roles = InspectionRoles)]
    public async Task<IActionResult> Repaired(Guid caseId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MarkWarrantyRepairedRequest? request, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor))
            return Unauthorized();
        return Map(await service.MarkRepairedAsync(caseId, request ?? new(), actor, ct));
    }

    [HttpPost("{caseId:guid}/reject-replacement")]
    [Authorize(Roles = ReplacementRoles)]
    public async Task<IActionResult> RejectReplacement(Guid caseId, RejectWarrantyReplacementRequest request, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor))
            return Unauthorized();
        return Map(await service.RejectReplacementAsync(caseId, request, actor, ct));
    }

    [HttpPost("{caseId:guid}/return")]
    [Authorize(Roles = IntakeRoles)]
    public async Task<IActionResult> ReturnToCustomer(Guid caseId, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor))
            return Unauthorized();
        return Map(await service.ReturnAsync(caseId, actor, ct));
    }

    [HttpPost("{caseId:guid}/replace")]
    [Authorize(Roles = ReplacementRoles)]
    public async Task<IActionResult> Replace(Guid caseId, ReplaceWarrantyDeviceRequest request, CancellationToken ct)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor))
            return Unauthorized();
        return Map(await service.ReplaceAsync(caseId, request.ReplacementDeviceId, actor, ct));
    }

    private IActionResult Map(WarrantyResult result)
    {
        if (result.Errors != null)
            return Invalid(result.Errors);
        if (result.Data != null)
            return Ok(result.Data);
        if (result.Conflict == null)
            return NotFound();

        var details = new ProblemDetails { Status = StatusCodes.Status409Conflict, Detail = result.Conflict };
        if (result.Reasons != null)
            details.Extensions["reasons"] = result.Reasons;
        if (result.CurrentStatus != null)
            details.Extensions["currentStatus"] = result.CurrentStatus;
        return Conflict(details);
    }

    private IActionResult Invalid(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var (field, messages) in errors)
            foreach (var message in messages)
                ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}
