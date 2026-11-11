using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdmissionClass> AdmissionClasses => Set<AdmissionClass>();
    public DbSet<AdmissionHold> AdmissionHolds => Set<AdmissionHold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdmissionClass>(entity =>
        {
            entity.ToTable("admission_classes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(150);
            entity.Property(x => x.TotalSeats).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<AdmissionHold>(entity =>
        {
            entity.ToTable("admission_holds");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StudentId).IsRequired().HasMaxLength(17);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasOne(x => x.AdmissionClass).WithMany(x => x.Holds)
                  .HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ClassId);
            entity.HasIndex(x => x.StudentId);
            entity.HasIndex(x => x.ExpiresAt);
            // Database-level backup protection against two active applications
            entity.HasIndex(x => new { x.ClassId, x.StudentId })
                  .IsUnique()
                  .HasDatabaseName("UX_admission_holds_student_active")
                  .HasFilter("\"Status\" IN ('Held', 'Confirmed')");
        });
    }
}
