using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Api.Identity.Services;
using TechStore.Api.Orders.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Orders;

[ApiController]
public abstract class ReturnControllerBase : ControllerBase
{
    protected async Task<IActionResult> ExecuteAsync<T>(Func<AuthenticatedUser, Task<T>> action, bool created = false)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        try
        {
            var result = await action(actor);
            return created && result is Shared.DTOs.ReturnDto r ? Created($"/api/returns/{r.Id}", result) : Ok(result);
        }
        catch (ReturnProblem error)
        {
            if (error.Field != null)
            {
                ModelState.AddModelError(error.Field, error.Message);
                return ValidationProblem(ModelState);
            }
            return Problem(statusCode: error.Status, detail: error.Message);
        }
    }
}

[Route("api/returns")]
[Authorize(Roles = "Admin,Manager,SalesStaff")]
public sealed class ReturnsController(IReturnService service) : ReturnControllerBase
{
    [HttpGet("order/{orderId:guid}")]
    public Task<IActionResult> Order(Guid orderId, CancellationToken ct) =>
        ExecuteAsync(actor => service.OrderAsync(orderId, actor, ct));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        ExecuteAsync(actor => service.GetAsync(id, actor, ct));

    [HttpPost]
    public Task<IActionResult> Create(CreateReturnRequest input, CancellationToken ct) =>
        ExecuteAsync(actor => service.CreateAsync(input, actor, ct), created: true);

    [HttpPost("{id:guid}/settle"), Authorize(Roles = "Admin,Manager")]
    public Task<IActionResult> Settle(Guid id, SettleReturnRequest input, CancellationToken ct) =>
        ExecuteAsync(actor => service.SettleAsync(id, input, actor, ct));

    [HttpPost("{id:guid}/reconcile"), Authorize(Roles = "Admin,Manager")]
    public Task<IActionResult> Reconcile(Guid id, ReconcileReturnRequest input, CancellationToken ct) =>
        ExecuteAsync(actor => service.ReconcileAsync(id, input, actor, ct));

    [HttpPost("{id:guid}/exchange"), Authorize(Roles = "Admin,Manager")]
    public Task<IActionResult> Exchange(Guid id, LinkExchangeRequest input, CancellationToken ct) =>
        ExecuteAsync(actor => service.ExchangeAsync(id, input, actor, ct));
}

[Route("api/inventory/returns")]
[Authorize(Roles = "Admin,WarehouseStaff")]
public sealed class ReturnInspectionsController(IReturnService service) : ReturnControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) =>
        ExecuteAsync(_ => service.InspectionsAsync(ct));

    [HttpPost("{returnLineId:guid}/inspections")]
    public Task<IActionResult> Inspect(Guid returnLineId, InspectReturnRequest input, CancellationToken ct) =>
        ExecuteAsync(actor => service.InspectAsync(returnLineId, input, actor, ct));
}
