using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddScoped<ClassService>();
builder.Services.AddScoped<HoldService>();
builder.Services.AddHostedService<HoldExpirationService>();
builder.Services.AddCors(options => options.AddPolicy("local-dev", policy =>
    policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
          .AllowAnyHeader().AllowAnyMethod()));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();
app.UseCors("local-dev");
app.MapControllers();

// For this take-home project, bootstrap a fresh database automatically so that
// 'docker compose up' requires no manual EF command. Do not combine this approach
// with production migrations; see README.md for this trade-off.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("""
        CREATE UNIQUE INDEX IF NOT EXISTS "UX_admission_holds_student_active"
        ON admission_holds ("ClassId", "StudentId")
        WHERE "Status" IN ('Held', 'Confirmed');
        """);
}

app.Run();
