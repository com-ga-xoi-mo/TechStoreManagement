using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
using Microsoft.AspNetCore.Authorization;

namespace TechStore.Api.Inventory;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ISerialImeiService _serialImeiService;

    public InventoryController(
        IInventoryService inventoryService,
        ISerialImeiService serialImeiService)
    {
        _inventoryService = inventoryService;
        _serialImeiService = serialImeiService;
    }

    [HttpGet("stocks")]
    [ProducesResponseType(typeof(PagedResultDto<InventoryStockDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStocks(
        [FromQuery] InventoryStockFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetStocksAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Ok(result.Data);
    }

    [HttpPost("stocks/initialize")]
    [Authorize(Roles = "Admin,WarehouseStaff")]
    [ProducesResponseType(typeof(InventoryStockDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitializeStock(
        [FromBody] InitializeStockRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _inventoryService.InitializeStockAsync(request, userId.Value, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.IsConflict)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.ConflictReason ?? "Stock balance conflict.");
            }

            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Created($"/api/inventory/stocks?productId={result.Data!.ProductId}", result.Data);
    }

    [HttpDelete("stocks/{stockId:guid}")]
    [Authorize(Roles = "Admin,Manager,WarehouseStaff")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteStock(
        Guid stockId,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.DeleteStockAsync(stockId, cancellationToken);
        if (result.IsSuccess) return NoContent();
        if (result.IsNotFound)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Inventory stock not found",
                detail: "The requested inventory stock does not exist.");
        }

        return Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Inventory stock cannot be deleted",
            detail: result.ConflictReason);
    }

    [HttpGet("stocks/{stockId:guid}/detail")]
    [ProducesResponseType(typeof(InventoryStockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockDetail(
        Guid stockId,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetStockDetailAsync(stockId, cancellationToken);
        if (result.IsNotFound)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Inventory stock not found",
                detail: "The requested inventory stock does not exist.");
        }

        return Ok(result.Data);
    }

    [HttpPost("stocks/{stockId:guid}/adjust")]
    [Authorize(Roles = "Admin,WarehouseStaff")]
    [ProducesResponseType(typeof(InventoryStockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdjustStock(
        Guid stockId,
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var result = await _inventoryService.AdjustStockAsync(
            stockId,
            request,
            userId.Value,
            cancellationToken);

        if (result.IsSuccess) return Ok(result.Data);
        if (result.IsNotFound)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Inventory stock not found",
                detail: "The requested inventory stock does not exist.");
        }
        if (result.IsConflict)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Inventory adjustment conflict",
                detail: result.ConflictReason);
        }

        return ValidationProblem(BuildModelState(result.ValidationErrors));
    }

    [HttpGet("serials")]
    [ProducesResponseType(typeof(PagedResultDto<SerialImeiDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSerials(
        [FromQuery] SerialImeiFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _serialImeiService.GetSerialsAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Ok(result.Data);
    }

    [HttpPost("serials/batch-import")]
    [Authorize(Roles = "Admin,WarehouseStaff")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BatchImportSerials(
        [FromBody] BatchImportSerialImeiRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await _serialImeiService.BatchImportAsync(request, userId.Value, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.IsConflict)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.ConflictReason ?? "Duplicate serial or IMEI collision.");
            }

            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Created(
            $"/api/inventory/serials?productId={request.ProductId}",
            new { importedCount = result.ImportedCount });
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
