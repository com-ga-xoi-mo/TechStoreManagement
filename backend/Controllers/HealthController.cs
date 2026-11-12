using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TechStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Check()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "TechStore Management API",
            Timestamp = DateTime.UtcNow
        });
    }
}
