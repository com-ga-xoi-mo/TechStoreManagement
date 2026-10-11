using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Orders.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Orders;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    // Default administrator seeded by DataSeeder; used while authentication is not implemented.
    private static readonly Guid DefaultActingUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.CreateAsync(request, GetUserId(), cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.IsConflict)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.ConflictReason);
            }

            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] OrderSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.SearchAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _orderService.GetByIdAsync(id, cancellationToken);
        if (!result.Found)
        {
            return NotFound();
        }

        return Ok(result.Data);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.UpdateStatusAsync(id, request, GetUserId(), cancellationToken);
        if (!result.Found)
        {
            return NotFound();
        }

        if (!result.IsSuccess)
        {
            if (result.IsConflict)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.ConflictReason);
            }

            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Ok(result.Data);
    }

    // Acting user from the token when present. TODO: remove the fixed fallback once authentication is added.
    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        return !string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var userId)
            ? userId
            : DefaultActingUserId;
    }

    private static ModelStateDictionary BuildModelState(Dictionary<string, string[]>? errors)
    {
        var modelState = new ModelStateDictionary();
        if (errors != null)
        {
            foreach (var (key, messages) in errors)
            {
                foreach (var message in messages)
                {
                    modelState.AddModelError(key, message);
                }
            }
        }

        return modelState;
    }
}
