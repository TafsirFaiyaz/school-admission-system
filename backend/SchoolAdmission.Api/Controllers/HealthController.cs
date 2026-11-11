using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "Healthy", service = "SchoolAdmission.Api" });

    [HttpGet("db")]
    public async Task<IActionResult> DatabaseStatus()
    {
        bool connected = await db.Database.CanConnectAsync();
        return connected ? Ok(new { status = "Healthy", database = "Connected" })
            : StatusCode(503, new { status = "Unhealthy", database = "Disconnected" });
    }
}
