using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Customers.Services;
using TechStore.Api.Identity.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Customers;

[ApiController]
[Route("api/customers")]
// Search, profile and create serve the counter (A M S); edit, activity and history manage customers (A M). See docs/API_PERMISSIONS.md.
[Authorize(Roles = "Admin,Manager,SalesStaff")]
public class CustomersController(ICustomerService customerService, ILoyaltyService loyaltyService) : ControllerBase
{
    private const string ManageRoles = "Admin,Manager";

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] CustomerSearchRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return ToAction(await customerService.SearchAsync(request, actor.Role, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return ToAction(await customerService.GetByIdAsync(id, actor.Role, cancellationToken));
    }

    // Points, tier and the next tier: the counter needs it to offer the VIP discount, so it follows the profile roles.
    [HttpGet("{id:guid}/loyalty-summary")]
    public async Task<IActionResult> GetLoyaltySummary(Guid id, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return ToAction(await loyaltyService.GetSummaryAsync(id, actor.Role, cancellationToken));
    }

    [HttpGet("{id:guid}/loyalty-points")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> GetLoyaltyPoints(Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ToAction(await customerService.GetLoyaltyPointsAsync(id, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}/orders")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> GetOrders(Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ToAction(await customerService.GetOrdersAsync(id, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}/purchased-items")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> GetPurchasedItems(Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ToAction(await customerService.GetPurchasedItemsAsync(id, page, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await customerService.CreateAsync(request, cancellationToken);
        if (result.IsSuccess) return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
        return ToAction(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken) =>
        ToAction(await customerService.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await customerService.DeactivateAsync(id, cancellationToken);
        if (result.IsSuccess) return NoContent();
        return ToAction(result);
    }

    [HttpPatch("{id:guid}/activate")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await customerService.ActivateAsync(id, cancellationToken);
        if (result.IsSuccess) return NoContent();
        return ToAction(result);
    }

    private IActionResult ToAction<T>(CustomerOperationResult<T> result)
    {
        if (result.IsSuccess) return Ok(result.Data);
        if (!result.Found) return NotFound();
        if (result.IsConflict) return Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "A customer with this phone number already exists.");
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
