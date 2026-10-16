using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Api.Identity.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Identity;

[ApiController]
[Route("api/settings/[controller]")]
public class StoreController : ControllerBase
{
    private readonly IStoreSettingsService _storeSettingsService;

    public StoreController(IStoreSettingsService storeSettingsService)
    {
        _storeSettingsService = storeSettingsService;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StoreSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _storeSettingsService.GetSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPut]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(StoreSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateStoreSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _storeSettingsService.UpdateSettingsAsync(request, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
