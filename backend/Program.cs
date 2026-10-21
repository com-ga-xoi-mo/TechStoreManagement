using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Analytics.Services;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Repositories;
using TechStore.Api.Identity.Configurations;
using TechStore.Api.Identity.Repositories;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Repositories;
using TechStore.Api.Orders.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// JWT Authentication Configuration
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();

// Database Context (PostgreSQL EF Core)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Data Seeder registration
builder.Services.AddScoped<IDataSeeder, DataSeeder>();
builder.Services.AddScoped<IDemoSeedRepository, DemoSeedRepository>();

// Unit of Work registration
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Catalog Module repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ISpecDefinitionRepository, SpecDefinitionRepository>();

// Catalog Module services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// Inventory Module repositories
builder.Services.AddScoped<IInventoryStockRepository, InventoryStockRepository>();
builder.Services.AddScoped<ISerialImeiRepository, SerialImeiRepository>();

// Inventory Module services
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ISerialImeiService, SerialImeiService>();
builder.Services.AddScoped<IInventoryFulfillmentService, InventoryFulfillmentService>();

// Customers Module repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();

// Orders Module repositories
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Orders Module services
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<IOrderService, OrderService>();

// Analytics Module
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// Identity Module repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IStoreSettingRepository, StoreSettingRepository>();

// Identity Module services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStoreSettingsService, StoreSettingsService>();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
