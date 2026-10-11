using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory;

[ApiController]
[Route("api/v1/inventory")]
[Route("api/inventory")]
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
    [ProducesResponseType(typeof(InventoryStockDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitializeStock(
        [FromBody] InitializeStockRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _inventoryService.InitializeStockAsync(request, userId, cancellationToken);

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

        return Created($"/api/v1/inventory/stocks?productId={result.Data!.ProductId}", result.Data);
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
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BatchImportSerials(
        [FromBody] BatchImportSerialImeiRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _serialImeiService.BatchImportAsync(request, userId, cancellationToken);

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
            $"/api/v1/inventory/serials?productId={request.ProductId}",
            new { importedCount = result.ImportedCount });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var parsedGuid))
        {
            return parsedGuid;
        }

        // Fallback to baseline Admin user ID if running unauthenticated
        return Guid.Parse("11111111-1111-1111-1111-111111111111");
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
