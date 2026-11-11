using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public class ClassesController(ClassService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClassRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new ApiError("INVALID_NAME", "Class name is required."));
        try
        {
            var created = await service.CreateAsync(name, request.TotalSeats);
            return CreatedAtAction(nameof(GetById), new { classId = created.Id },
                new CreatedClassResponse(created.Id, created.Name, created.TotalSeats));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new ApiError("DUPLICATE_CLASS", "A class with this name already exists."));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await service.GetAllAsync());

    [HttpGet("{classId}")]
    public async Task<IActionResult> GetById(string classId)
    {
        if (!Guid.TryParse(classId, out var id))
            return BadRequest(new ApiError("INVALID_ID", "Invalid class ID."));
        var result = await service.GetByIdAsync(id);
        return result is null ? NotFound(new ApiError("CLASS_NOT_FOUND", "Class not found.")) : Ok(result);
    }
}
