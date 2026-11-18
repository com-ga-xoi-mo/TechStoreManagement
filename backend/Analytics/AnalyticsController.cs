using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Api.Analytics.Services;
using TechStore.Shared.DTOs;

namespace TechStore.Api.Analytics;

/// <summary>Store-wide analytics for Admin and Manager using Vietnam calendar dates.</summary>
[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Admin,Manager")]
public sealed class AnalyticsController(IAnalyticsService analyticsService) : ControllerBase
{
    private static readonly HashSet<string> SupportedPeriods = ["today", "last7Days", "thisMonth", "custom"];

    /// <summary>Reads a coherent, database-backed dashboard snapshot for Admin or Manager.</summary>
    /// <param name="period">today, last7Days (default), thisMonth, or custom.</param>
    /// <param name="from">Inclusive first Vietnam calendar date (yyyy-MM-dd), required for custom.</param>
    /// <param name="to">Inclusive last Vietnam calendar date (yyyy-MM-dd), required for custom.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("dashboard")]
    [EndpointSummary("Read the store-wide dashboard")]
    [EndpointDescription("Admin and Manager can read today, last7Days (default), thisMonth, or custom. For custom, send from and to as inclusive Vietnam calendar dates (yyyy-MM-dd), up to 31 days and no later than today in UTC+07. Other periods do not accept from or to.")]
    [ProducesResponseType(typeof(AdminDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminDashboardDto>> GetDashboard(
        [FromQuery] string? period = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        CancellationToken cancellationToken = default)
    {
        var selectedPeriod = string.IsNullOrWhiteSpace(period) ? "last7Days" : period;
        if (!SupportedPeriods.Contains(selectedPeriod))
        {
            ModelState.AddModelError(nameof(period), "Period must be today, last7Days, thisMonth, or custom.");
            return ValidationProblem(ModelState);
        }

        (DateOnly From, DateOnly To)? range = null;
        var query = ControllerContext.HttpContext?.Request.Query;
        if (selectedPeriod == "custom")
        {
            var start = ParseDate(nameof(from), from, query?[nameof(from)].Count ?? 0);
            var end = ParseDate(nameof(to), to, query?[nameof(to)].Count ?? 0);
            if (start is not null && end is not null)
            {
                var days = end.Value.DayNumber - start.Value.DayNumber + 1;
                if (days < 1)
                    ModelState.AddModelError(nameof(to), "End date must be on or after the start date.");
                else if (days > 31)
                    ModelState.AddModelError(nameof(to), "A custom period can include at most 31 days.");
                else
                    range = (start.Value, end.Value);
            }
        }
        else if (from is not null || query?.ContainsKey(nameof(from)) == true ||
                 to is not null || query?.ContainsKey(nameof(to)) == true)
        {
            ModelState.AddModelError(nameof(period), "Date parameters require period=custom.");
        }

        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var result = await analyticsService.GetDashboardAsync(new(selectedPeriod, range), cancellationToken);
        if (result.Error is { } error)
        {
            ModelState.AddModelError(error.Field, error.Message);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Snapshot);
    }

    private DateOnly? ParseDate(string field, string? input, int valueCount)
    {
        if (valueCount <= 1 && DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            return date;

        ModelState.AddModelError(field, "Supply one valid Vietnam calendar date in yyyy-MM-dd format.");
        return null;
    }
}
