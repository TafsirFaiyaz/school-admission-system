using System.ComponentModel.DataAnnotations;

namespace SchoolAdmission.Api.DTOs;

public sealed class CreateClassRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;
    [Range(1, int.MaxValue)]
    public int TotalSeats { get; set; }
}

public sealed record ClassResponse(
    Guid Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);

public sealed record CreatedClassResponse(Guid Id, string Name, int TotalSeats);

public sealed record HoldResponse(Guid HoldId, Guid ClassId, string StudentId, string Status, DateTime ExpiresAt);
public sealed record HoldDetailsResponse(Guid HoldId, Guid ClassId, string Status, DateTime ExpiresAt);
public sealed record ConfirmationResponse(Guid HoldId, string Status);
public sealed record MyHoldResponse(Guid HoldId, Guid ClassId, string ClassName, string Status, DateTime ExpiresAt);
public sealed record ApiError(string Code, string Message);
