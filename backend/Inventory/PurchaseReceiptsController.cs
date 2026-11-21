using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory;

[ApiController]
[Route("api/inventory/purchase-receipts")]
[Authorize]
public class PurchaseReceiptsController(IPurchaseReceiptService receiptService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,Manager,WarehouseStaff")]
    [ProducesResponseType(typeof(PagedResultDto<PurchaseReceiptSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReceipts(
        [FromQuery] PurchaseReceiptFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await receiptService.SearchAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Manager,WarehouseStaff")]
    [ProducesResponseType(typeof(PurchaseReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReceiptById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await receiptService.GetDetailAsync(id, cancellationToken);
        if (result.IsNotFound)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Purchase receipt not found",
                detail: "Phiếu nhập không tồn tại.");
        }

        return Ok(result.Data);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,WarehouseStaff")]
    [ProducesResponseType(typeof(PurchaseReceiptDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PurchaseReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateReceipt(
        [FromBody] CreatePurchaseReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await receiptService.CreateAsync(request, userId.Value, cancellationToken);
        return WriteResult(result, creating: true);
    }

    [HttpPost("{id:guid}/deliveries")]
    [Authorize(Roles = "Admin,WarehouseStaff")]
    public async Task<IActionResult> Receive(Guid id, [FromBody] ReceivePurchaseDeliveryRequest request, CancellationToken cancellationToken)
    {
        if (GetUserId() is not { } actor) return Unauthorized();
        return WriteResult(await receiptService.ReceiveAsync(id, request, actor, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel-remainder")]
    [Authorize(Roles = "Admin,WarehouseStaff")]
    public async Task<IActionResult> CancelRemainder(Guid id, CancellationToken cancellationToken)
    {
        if (GetUserId() is not { } actor) return Unauthorized();
        return WriteResult(await receiptService.CancelRemainderAsync(id, actor, cancellationToken));
    }

    private IActionResult WriteResult(PurchaseReceiptResult result, bool creating = false)
    {
        if (result.IsNotFound) return NotFound();
        if (!result.IsSuccess)
        {
            if (result.IsConflict)
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = result.ConflictReason ?? "Xung đột dữ liệu phiếu nhập."
                };
                if (result.DuplicateIdentifiers != null && result.DuplicateIdentifiers.Count > 0)
                {
                    problem.Extensions["duplicateIdentifiers"] = result.DuplicateIdentifiers;
                }
                return StatusCode(StatusCodes.Status409Conflict, problem);
            }

            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        if (!creating || result.IsReplay)
        {
            return Ok(result.Data);
        }

        return Created($"/api/inventory/purchase-receipts/{result.Data!.Id}", result.Data);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var parsedGuid))
        {
            return parsedGuid;
        }

        return null;
    }

    private static ModelStateDictionary BuildModelState(Dictionary<string, string[]>? errors)
    {
        var modelState = new ModelStateDictionary();
        if (errors == null) return modelState;

        foreach (var (key, errList) in errors)
        {
            foreach (var err in errList)
            {
                modelState.AddModelError(key, err);
            }
        }

        return modelState;
    }
}
