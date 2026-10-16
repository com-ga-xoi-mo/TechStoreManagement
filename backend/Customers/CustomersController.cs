using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Customers.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Customers;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "Admin")]
public class CustomersController(ICustomerService customerService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] CustomerSearchRequest request, CancellationToken cancellationToken) =>
        ToAction(await customerService.SearchAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToAction(await customerService.GetByIdAsync(id, cancellationToken));

    [HttpGet("{id:guid}/loyalty-points")]
    public async Task<IActionResult> GetLoyaltyPoints(Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ToAction(await customerService.GetLoyaltyPointsAsync(id, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}/orders")]
    public async Task<IActionResult> GetOrders(Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ToAction(await customerService.GetOrdersAsync(id, page, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await customerService.CreateAsync(request, cancellationToken);
        if (result.IsSuccess) return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
        return ToAction(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken) =>
        ToAction(await customerService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await customerService.DeactivateAsync(id, cancellationToken);
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
