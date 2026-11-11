using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public class ClassService(AppDbContext db)
{
    public async Task<AdmissionClass> CreateAsync(string name, int capacity)
    {
        var entity = new AdmissionClass { Name = name, TotalSeats = capacity };
        db.AdmissionClasses.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public Task<List<ClassResponse>> GetAllAsync()
    {
        var now = DateTime.UtcNow;
        return db.AdmissionClasses.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new ClassResponse(
                c.Id, c.Name, c.TotalSeats,
                c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now),
                c.Holds.Count(h => h.Status == HoldStatus.Confirmed),
                c.TotalSeats - c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now)
                    - c.Holds.Count(h => h.Status == HoldStatus.Confirmed)))
            .ToListAsync();
    }

    public Task<ClassResponse?> GetByIdAsync(Guid classId)
    {
        var now = DateTime.UtcNow;
        return db.AdmissionClasses.AsNoTracking().Where(c => c.Id == classId)
            .Select(c => new ClassResponse(
                c.Id, c.Name, c.TotalSeats,
                c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now),
                c.Holds.Count(h => h.Status == HoldStatus.Confirmed),
                c.TotalSeats - c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now)
                    - c.Holds.Count(h => h.Status == HoldStatus.Confirmed)))
            .FirstOrDefaultAsync();
    }
}
