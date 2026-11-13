using Microsoft.AspNetCore.Authorization;
using TechStore.Api.Identity.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Orders.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Orders;

[ApiController]
[Route("api/orders")]
[Authorize(Roles = "Admin,Manager,SalesStaff")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SalesStaff")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await _orderService.CreateAsync(request, actor.Id, cancellationToken);
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
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await _orderService.SearchAsync(request, actor, cancellationToken);
        if (!result.IsSuccess)
        {
            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await _orderService.GetByIdAsync(id, actor, cancellationToken);
        if (!result.Found)
        {
            return NotFound();
        }

        return Ok(result.Data);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin,SalesStaff")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await _orderService.UpdateStatusAsync(id, request, actor, cancellationToken);
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
