
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "SchoolAdmission.Api"
        });
    }

    [HttpGet("db")]
    public async Task<IActionResult> GetDatabaseStatus()
    {
        bool connected = await _db.Database.CanConnectAsync();

        if (!connected)
        {
            return StatusCode(503, new
            {
                status = "Unhealthy",
                database = "Disconnected"
            });
        }

        return Ok(new
        {
            status = "Healthy",
            database = "Connected"
        });
    }
}
