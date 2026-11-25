using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Customers.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Customers;

[ApiController]
[Route("api/vouchers")]
// Voucher management is limited to Admin and Manager. See docs/API_PERMISSIONS.md.
[Authorize(Roles = "Admin,Manager")]
public class VouchersController(IVoucherService voucherService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] VoucherSearchRequest request, CancellationToken cancellationToken) =>
        ToAction(await voucherService.SearchAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToAction(await voucherService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVoucherRequest request, CancellationToken cancellationToken)
    {
        var result = await voucherService.CreateAsync(request, cancellationToken);
        if (result.IsSuccess) return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
        return ToAction(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVoucherRequest request, CancellationToken cancellationToken) =>
        ToAction(await voucherService.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        ToAction(await voucherService.SetActiveAsync(id, true, cancellationToken));

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        ToAction(await voucherService.SetActiveAsync(id, false, cancellationToken));

    private IActionResult ToAction<T>(CustomerOperationResult<T> result)
    {
        if (result.IsSuccess) return Ok(result.Data);
        if (!result.Found) return NotFound();
        if (result.IsConflict) return Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "A voucher with this code already exists.");
        return ValidationProblem(BuildModelState(result.ValidationErrors));
    }

    private static ModelStateDictionary BuildModelState(Dictionary<string, string[]>? errors)
    {
        var modelState = new ModelStateDictionary();
        if (errors != null)
            foreach (var (key, messages) in errors)
                foreach (var message in messages) modelState.AddModelError(key, message);
        return modelState;
    }
}
