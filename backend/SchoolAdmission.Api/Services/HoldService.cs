using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public sealed class AdmissionException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public class HoldService(AppDbContext db, IConfiguration config)
{
    private static string PublicStatus(AdmissionHold h, DateTime now) =>
        h.Status == HoldStatus.Held && h.ExpiresAt <= now
            ? "EXPIRED" : h.Status.ToString().ToUpperInvariant();

    // PostgreSQL row locks coordinate independent backend processes.
    // The command executes on the DbContext's existing connection/transaction.
    private async Task<bool> LockRowAsync(string table, Guid id)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText = $"SELECT 1 FROM {table} WHERE \"Id\" = @id FOR UPDATE";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = id;
        cmd.Parameters.Add(parameter);
        return await cmd.ExecuteScalarAsync() != null;
    }

    public async Task<HoldResponse> CreateAsync(Guid classId, string studentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        if (!await LockRowAsync("admission_classes", classId))
            throw new AdmissionException(404, "CLASS_NOT_FOUND", "Class not found.");

        var admissionClass = await db.AdmissionClasses.AsNoTracking()
            .FirstAsync(x => x.Id == classId);
        var now = DateTime.UtcNow;

        // Release expired holds for this class before checking availability/uniqueness.
        await db.AdmissionHolds
            .Where(x => x.ClassId == classId && x.Status == HoldStatus.Held && x.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, HoldStatus.Expired));

        bool alreadyApplied = await db.AdmissionHolds.AnyAsync(h =>
            h.ClassId == classId && h.StudentId == studentId &&
            (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed));
        if (alreadyApplied)
            throw new AdmissionException(409, "DUPLICATE_APPLICATION", "You already have an active or confirmed application in this class.");

        int occupied = await db.AdmissionHolds.CountAsync(h =>
            h.ClassId == classId &&
            (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed));
        if (occupied >= admissionClass.TotalSeats)
            throw new AdmissionException(409, "NO_SEATS", "No seats are currently available.");

        int seconds = config.GetValue<int?>("HOLD_DURATION_SECONDS") ?? 600;
        if (seconds <= 0) throw new InvalidOperationException("HOLD_DURATION_SECONDS must be positive.");
        var hold = new AdmissionHold
        {
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.Held,
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(seconds)
        };
        db.AdmissionHolds.Add(hold);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new HoldResponse(hold.Id, classId, studentId, "HELD", hold.ExpiresAt);
    }

    public async Task<HoldDetailsResponse?> GetAsync(Guid holdId)
    {
        var hold = await db.AdmissionHolds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == holdId);
        return hold is null ? null : new HoldDetailsResponse(hold.Id, hold.ClassId, PublicStatus(hold, DateTime.UtcNow), hold.ExpiresAt);
    }

    public async Task<ConfirmationResponse> ConfirmAsync(Guid holdId, string studentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        if (!await LockRowAsync("admission_holds", holdId))
            throw new AdmissionException(404, "HOLD_NOT_FOUND", "Application not found.");

        var hold = await db.AdmissionHolds.FirstAsync(h => h.Id == holdId);
        if (hold.StudentId != studentId)
            throw new AdmissionException(403, "NOT_OWNER", "This application does not belong to the selected student.");
        if (hold.Status == HoldStatus.Confirmed)
        {
            await tx.CommitAsync();
            return new ConfirmationResponse(hold.Id, "CONFIRMED");
        }
        if (hold.Status != HoldStatus.Held)
            throw new AdmissionException(409, "NOT_ACTIVE", "This application cannot be confirmed.");
        var now = DateTime.UtcNow;
        if (hold.ExpiresAt <= now)
        {
            hold.Status = HoldStatus.Expired;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            throw new AdmissionException(409, "HOLD_EXPIRED", "The reservation has expired.");
        }
        hold.Status = HoldStatus.Confirmed;
        hold.ConfirmedAt = now;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new ConfirmationResponse(hold.Id, "CONFIRMED");
    }

    public async Task CancelAsync(Guid holdId, string studentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        if (!await LockRowAsync("admission_holds", holdId))
            throw new AdmissionException(404, "HOLD_NOT_FOUND", "Application not found.");
        var hold = await db.AdmissionHolds.FirstAsync(h => h.Id == holdId);
        if (hold.StudentId != studentId)
            throw new AdmissionException(403, "NOT_OWNER", "This application does not belong to the selected student.");
        if (hold.Status == HoldStatus.Cancelled)
        {
            await tx.CommitAsync();
            return;
        }
        if (hold.Status == HoldStatus.Confirmed)
            throw new AdmissionException(409, "ALREADY_CONFIRMED", "Confirmed admissions cannot be cancelled.");
        if (hold.Status != HoldStatus.Held)
            throw new AdmissionException(409, "NOT_ACTIVE", "This application is no longer active.");
        var now = DateTime.UtcNow;
        if (hold.ExpiresAt <= now)
        {
            hold.Status = HoldStatus.Expired;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            throw new AdmissionException(409, "HOLD_EXPIRED", "The reservation has expired.");
        }
        hold.Status = HoldStatus.Cancelled;
        hold.CancelledAt = now;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<List<MyHoldResponse>> GetForStudentAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        var holds = await db.AdmissionHolds.AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new { h.Id, h.ClassId, h.AdmissionClass.Name, h.Status, h.ExpiresAt })
            .ToListAsync();
        return holds.Select(h => new MyHoldResponse(h.Id, h.ClassId, h.Name,
            h.Status == HoldStatus.Held && h.ExpiresAt <= now ? "EXPIRED" : h.Status.ToString().ToUpperInvariant(),
            h.ExpiresAt)).ToList();
    }
}
