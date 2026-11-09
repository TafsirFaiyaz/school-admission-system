
using Microsoft.AspNetCore.Mvc;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "SchoolAdmission.Api"
        });
    }
}
