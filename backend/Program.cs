using TechStore.Api.Assistant;
using TechStore.Api.Assistant.Tools;
using TechStore.Api.Common.Ai;
using TechStore.Api.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Analytics.Repositories;
using TechStore.Api.Analytics.Services;
using TechStore.Api.Common.Configuration;
using TechStore.Api.Common.Data;
using TechStore.Api.Common.Storage;
using TechStore.Api.Customers.Repositories;
using TechStore.Api.Customers.Services;
using TechStore.Api.Identity.Repositories;
using TechStore.Api.Identity.Services;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Repositories;
using TechStore.Api.Orders.Services;
using TechStore.Api.Sales.Repositories;
using TechStore.Api.Sales.Services;
using TechStore.Api.Warranty.Repositories;
using TechStore.Api.Warranty.Services;

// Development only: backend/.env (git-ignored) can hold local settings such as R2 keys.
if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
    DotEnvFile.LoadIfPresent(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Unhandled exceptions become RFC 7807 ProblemDetails without stack traces.
builder.Services.AddProblemDetails();

builder.Services.AddStoreAuthentication(builder.Configuration);
builder.Services.Configure<TechStore.Api.Sales.VietQrOptions>(builder.Configuration.GetSection(TechStore.Api.Sales.VietQrOptions.SectionName));

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

// Object storage (product images)
builder.Services.AddObjectStorage(builder.Configuration);

// Catalog Module repositories
builder.Services.AddScoped<IProductImageRepository, ProductImageRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ISpecDefinitionRepository, SpecDefinitionRepository>();

// Catalog Module services
builder.Services.AddScoped<IProductImageService, ProductImageService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// Inventory Module repositories
builder.Services.AddScoped<IInventoryStockRepository, InventoryStockRepository>();
builder.Services.AddScoped<ISerialImeiRepository, SerialImeiRepository>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();

// Inventory Module services
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ISerialImeiService, SerialImeiService>();
builder.Services.AddScoped<IInventoryFulfillmentService, InventoryFulfillmentService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IPurchaseReceiptService, PurchaseReceiptService>();

// Customers Module repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
builder.Services.AddScoped<IVoucherRepository, VoucherRepository>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<ILoyaltyPointRepository, LoyaltyPointRepository>();
builder.Services.AddScoped<IRefundLoyaltyService, RefundLoyaltyService>();

// Orders Module repositories
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReturnRepository, ReturnRepository>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IReturnExchangeService, ReturnExchangeService>();
builder.Services.AddScoped<IInventoryAfterSaleRepository, InventoryAfterSaleRepository>();
builder.Services.AddScoped<IInventoryAfterSaleService, InventoryAfterSaleService>();
builder.Services.AddScoped<IWarrantyCaseService, WarrantyCaseService>();
builder.Services.AddScoped<IWarrantyCaseRepository, WarrantyCaseRepository>();

// Orders Module services
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddOptions<PricingOptions>()
    .Bind(builder.Configuration.GetSection(PricingOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(o => decimal.Round(o.VatRatePercent, 2) == o.VatRatePercent, "Pricing:VatRatePercent allows at most 2 decimal places.")
    .ValidateOnStart();

// Sales Module (POS shifts and cash checkout)
builder.Services.AddScoped<IPosSessionRepository, PosSessionRepository>();
builder.Services.AddScoped<IPosShiftService, PosShiftService>();
builder.Services.AddScoped<ICashRefundRepository, CashRefundRepository>();
builder.Services.AddScoped<ICashRefundService, CashRefundService>();
builder.Services.AddScoped<IVietQrHoldRepository, VietQrHoldRepository>();
builder.Services.AddScoped<IVietQrHoldService, VietQrHoldService>();
builder.Services.AddScoped<IVietQrPaymentService, VietQrPaymentService>();
builder.Services.AddScoped<IVietQrConfirmationService, VietQrConfirmationService>();
builder.Services.AddHostedService<VietQrExpiryWorker>();
builder.Services.AddScoped<ISaleSettlementService, SaleSettlementService>();
builder.Services.AddScoped<IPosCheckoutService, PosCheckoutService>();

// Analytics Module
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

builder.Services.Configure<AssistantOptions>(builder.Configuration.GetSection("Assistant"));
builder.Services.AddHttpClient<IChatModelClient, OpenAiCompatibleChatClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddScoped<AssistantTools>();
builder.Services.AddScoped<IAssistantService, AssistantService>();

// Identity Module repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IStoreSettingRepository, StoreSettingRepository>();

// Identity Module services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
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

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseObjectStorageFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
