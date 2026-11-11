namespace SchoolAdmission.Api.Models;
public class AdmissionClass
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<AdmissionHold> Holds { get; set; } = new List<AdmissionHold>();
}
