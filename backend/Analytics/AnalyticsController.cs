using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Analytics.Services;
using TechStore.Shared.DTOs;

namespace TechStore.Api.Analytics;

/// <summary>Admin analytics endpoints; response date fields use Vietnam calendar dates and UTC as-of timestamps.</summary>
[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Admin")]
public sealed class AnalyticsController(IAnalyticsService analyticsService) : ControllerBase
{
    private static readonly HashSet<string> SupportedPeriods = ["today", "last7Days", "thisMonth"];

    /// <summary>Reads a coherent, database-backed dashboard snapshot for an Admin.</summary>
    /// <param name="period">today, last7Days, or thisMonth; defaults to last7Days.</param>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AdminDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminDashboardDto>> GetDashboard(
        [FromQuery] string? period = null,
        CancellationToken cancellationToken = default)
    {
        var selectedPeriod = string.IsNullOrWhiteSpace(period) ? "last7Days" : period;
        if (!SupportedPeriods.Contains(selectedPeriod))
        {
            var errors = new ModelStateDictionary();
            errors.AddModelError("period", "Period must be today, last7Days, or thisMonth.");
            return BadRequest(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest });
        }

        return Ok(await analyticsService.GetDashboardAsync(selectedPeriod, cancellationToken));
    }
}
