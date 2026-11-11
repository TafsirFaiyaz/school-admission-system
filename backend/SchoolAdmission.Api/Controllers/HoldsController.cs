using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
public class HoldsController(HoldService service) : ControllerBase
{
    internal static string? StudentId(HttpRequest request)
    {
        string? value = request.Headers["X-Student-Id"].FirstOrDefault();
        return value != null && Regex.IsMatch(value, "^[0-9]{17}$") ? value : null;
    }

    [HttpPost("classes/{classId}/holds")]
    public async Task<IActionResult> Create(string classId)
    {
        var studentId = StudentId(Request);
        if (studentId is null) return BadRequest(new ApiError("INVALID_STUDENT_ID", "Supply a 17-digit X-Student-Id header."));
        if (!Guid.TryParse(classId, out var id)) return BadRequest(new ApiError("INVALID_ID", "Invalid class ID."));
        try
        {
            var hold = await service.CreateAsync(id, studentId);
            return CreatedAtAction(nameof(Get), new { holdId = hold.HoldId }, hold);
        }
        catch (AdmissionException ex) { return StatusCode(ex.StatusCode, new ApiError(ex.Code, ex.Message)); }
    }

    [HttpGet("holds/{holdId}")]
    public async Task<IActionResult> Get(string holdId)
    {
        if (!Guid.TryParse(holdId, out var id)) return BadRequest(new ApiError("INVALID_ID", "Invalid hold ID."));
        var hold = await service.GetAsync(id);
        return hold is null ? NotFound(new ApiError("HOLD_NOT_FOUND", "Application not found.")) : Ok(hold);
    }

    [HttpPost("holds/{holdId}/confirm")]
    public async Task<IActionResult> Confirm(string holdId)
    {
        var studentId = StudentId(Request);
        if (studentId is null) return BadRequest(new ApiError("INVALID_STUDENT_ID", "Supply a 17-digit X-Student-Id header."));
        if (!Guid.TryParse(holdId, out var id)) return BadRequest(new ApiError("INVALID_ID", "Invalid hold ID."));
        try { return Ok(await service.ConfirmAsync(id, studentId)); }
        catch (AdmissionException ex) { return StatusCode(ex.StatusCode, new ApiError(ex.Code, ex.Message)); }
    }

    [HttpDelete("holds/{holdId}")]
    public async Task<IActionResult> Cancel(string holdId)
    {
        var studentId = StudentId(Request);
        if (studentId is null) return BadRequest(new ApiError("INVALID_STUDENT_ID", "Supply a 17-digit X-Student-Id header."));
        if (!Guid.TryParse(holdId, out var id)) return BadRequest(new ApiError("INVALID_ID", "Invalid hold ID."));
        try { await service.CancelAsync(id, studentId); return NoContent(); }
        catch (AdmissionException ex) { return StatusCode(ex.StatusCode, new ApiError(ex.Code, ex.Message)); }
    }
}
