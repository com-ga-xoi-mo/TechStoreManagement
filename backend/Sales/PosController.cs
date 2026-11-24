using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Identity.Services;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Sales;

[ApiController]
[Route("api/pos")]
[Authorize(Roles = "Admin,Manager,SalesStaff")]
public class PosController(IPosShiftService shiftService, IPosCheckoutService checkoutService) : ControllerBase
{
    [HttpPost("shifts")]
    public async Task<IActionResult> OpenShift([FromBody] OpenShiftRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await shiftService.OpenAsync(request, actor, cancellationToken);
        return Map(result, shift => CreatedAtAction(nameof(GetShift), new { id = shift.Id }, shift));
    }

    [HttpGet("shifts/current")]
    public async Task<IActionResult> GetCurrentShift(CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await shiftService.GetCurrentAsync(actor, cancellationToken);
        return Map(result, shift => Ok(shift));
    }

    [HttpGet("shifts")]
    public async Task<IActionResult> SearchShifts([FromQuery] PosShiftSearchRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await shiftService.SearchAsync(request, actor, cancellationToken);
        return Map(result, page => Ok(page));
    }

    [HttpGet("shifts/{id:guid}")]
    public async Task<IActionResult> GetShift(Guid id, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await shiftService.GetByIdAsync(id, actor, cancellationToken);
        return Map(result, shift => Ok(shift));
    }

    [HttpPost("shifts/{id:guid}/close")]
    public async Task<IActionResult> CloseShift(
        Guid id,
        [FromBody] CloseShiftRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await shiftService.CloseAsync(id, request, actor, cancellationToken);
        return Map(result, shift => Ok(shift));
    }

    [HttpPost("quote")]
    [Authorize(Roles = "Admin,SalesStaff")]
    public async Task<IActionResult> Quote([FromBody] PosCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return Map(await checkoutService.QuoteAsync(request, actor, cancellationToken), quote => Ok(quote));
    }

    [HttpPost("checkout")]
    [Authorize(Roles = "Admin,SalesStaff")]
    public async Task<IActionResult> Checkout([FromBody] PosCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await checkoutService.CheckoutAsync(request, actor, cancellationToken);
        return Map(result, outcome => outcome.Replayed
            ? Ok(outcome.Order)
            : Created($"/api/orders/{outcome.Order.Id}", outcome.Order));
    }

    private IActionResult Map<T>(PosResult<T> result, Func<T, IActionResult> onSuccess) => result.Kind switch
    {
        PosResultKind.Success => onSuccess(result.Data!),
        PosResultKind.NotFound => NotFound(),
        PosResultKind.Conflict => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            detail: result.ConflictReason),
        _ => ValidationProblem(BuildModelState(result.ValidationErrors))
    };

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
