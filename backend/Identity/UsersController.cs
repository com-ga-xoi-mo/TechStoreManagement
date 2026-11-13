using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Api.Identity.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Identity;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController(IUserManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] UserSearchRequest request, CancellationToken cancellationToken) =>
        Respond(await service.SearchAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Respond(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await service.CreateAsync(request, actor, cancellationToken);
        return result.StatusCode == 201 ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) : Respond(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return Respond(await service.UpdateAsync(id, request, actor, cancellationToken));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return Respond(await service.SetStatusAsync(id, request, actor, cancellationToken));
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> SetRole(Guid id, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        return Respond(await service.SetRoleAsync(id, request, actor, cancellationToken));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetUserPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await service.ResetPasswordAsync(id, request, actor, cancellationToken);
        return result.StatusCode == 200 ? NoContent() : Respond(result);
    }

    private IActionResult Respond<T>(UserOperationResult<T> result)
    {
        if (result.Errors is not null) return ValidationProblem(new ValidationProblemDetails(result.Errors) { Status = 400 });
        return result.StatusCode < 400 ? Ok(result.Data)
            : Problem(statusCode: result.StatusCode, title: result.StatusCode switch
            {
                401 => "Unauthorized", 403 => "Forbidden", 404 => "Not Found", 409 => "Conflict", _ => "Request failed"
            }, detail: result.Detail);
    }
}
