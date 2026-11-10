
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<AdmissionClass> AdmissionClasses { get; set; }

    public DbSet<AdmissionHold> AdmissionHolds { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdmissionClass>(entity =>
        {
            entity.ToTable("admission_classes");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(c => c.TotalSeats)
                .IsRequired();

            entity.HasIndex(c => c.Name)
                .IsUnique();
        });

        modelBuilder.Entity<AdmissionHold>(entity =>
        {
            entity.ToTable("admission_holds");

            entity.HasKey(h => h.Id);

            entity.Property(h => h.StudentId)
                .IsRequired()
                .HasMaxLength(17);

            entity.Property(h => h.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.HasOne(h => h.AdmissionClass)
                .WithMany(c => c.Holds)
                .HasForeignKey(h => h.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(h => h.ClassId);
            entity.HasIndex(h => h.StudentId);
            entity.HasIndex(h => h.ExpiresAt);
        });
    }
}
