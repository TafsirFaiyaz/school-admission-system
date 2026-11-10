
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Register API controllers.
builder.Services.AddControllers();

// Register OpenAPI documentation.
builder.Services.AddOpenApi();

// Read the PostgreSQL connection string.
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Database connection string is missing.");

// Register Entity Framework Core with PostgreSQL.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Enable OpenAPI in development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Map controller endpoints.
app.MapControllers();

// Start the application.
app.Run();
