using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me/holds")]
public class MeController(HoldService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var studentId = HoldsController.StudentId(Request);
        if (studentId is null) return BadRequest(new ApiError("INVALID_STUDENT_ID", "Supply a 17-digit X-Student-Id header."));
        return Ok(await service.GetForStudentAsync(studentId));
    }
}
