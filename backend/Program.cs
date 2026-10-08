using Microsoft.EntityFrameworkCore;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Database Context (PostgreSQL EF Core)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Data Seeder registration
builder.Services.AddScoped<IDataSeeder, DataSeeder>();

// Unit of Work registration
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Catalog Module services
builder.Services.AddScoped<IProductService, ProductService>();

// Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TechStore API v1");
        options.RoutePrefix = string.Empty; // Swagger UI at root URL
    });

    // Auto-migrate and seed database in Development
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        logger.LogInformation("Applying pending database migrations...");
        await dbContext.Database.MigrateAsync();

        var seeder = services.GetRequiredService<IDataSeeder>();
        logger.LogInformation("Seeding baseline database records...");
        await seeder.SeedAsync();
        logger.LogInformation("Database migration and seeding completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
