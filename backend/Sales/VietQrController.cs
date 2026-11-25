using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Identity.Services;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Sales;

[ApiController]
[Route("api/pos/vietqr")]
[Authorize(Roles = "Admin,Manager,SalesStaff")]
public sealed class VietQrController(IVietQrPaymentService payments, IVietQrConfirmationService confirmation) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin,SalesStaff")]
    public async Task<IActionResult> Create([FromBody] CreateVietQrRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await payments.CreateAsync(request, actor, cancellationToken);
        return Map(result, outcome => outcome.Replayed
            ? Ok(outcome.Payment)
            : Created($"/api/pos/vietqr/{outcome.Payment.Id}", outcome.Payment));
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] VietQrSearchRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return Map(await payments.SearchAsync(request, actor, cancellationToken), page => Ok(page));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return Map(await payments.GetAsync(id, actor, cancellationToken), payment => Ok(payment));
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] ConfirmVietQrRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return Map(await confirmation.ConfirmManualAsync(id, request, actor, cancellationToken), applied => Ok(applied.Payment));
    }

    private IActionResult Map<T>(PosResult<T> result, Func<T, IActionResult> onSuccess) => result.Kind switch
    {
        PosResultKind.Success => onSuccess(result.Data!),
        PosResultKind.NotFound => NotFound(),
        PosResultKind.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: result.ConflictReason),
        PosResultKind.Unavailable => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service unavailable", detail: result.ConflictReason),
        _ => ValidationProblem(BuildModelState(result.ValidationErrors))
    };

    private static ModelStateDictionary BuildModelState(Dictionary<string, string[]>? errors)
    {
        var modelState = new ModelStateDictionary();
        if (errors != null)
        {
            foreach (var (key, messages) in errors)
            {
                foreach (var message in messages) modelState.AddModelError(key, message);
            }
        }

        return modelState;
    }
}
