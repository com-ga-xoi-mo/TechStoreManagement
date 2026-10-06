using Microsoft.EntityFrameworkCore;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;

namespace TechStore.Api.Common.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Catalog Module (3 tables)
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    // Inventory Module (6 tables)
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<SerialImei> SerialImeis => Set<SerialImei>();

    // Orders Module (3 tables)
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderReturn> OrderReturns => Set<OrderReturn>();

    // Customers Module (3 tables)
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerLoyaltyPoint> CustomerLoyaltyPoints => Set<CustomerLoyaltyPoint>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();

    // Sales Module (2 tables)
    public DbSet<PosSession> PosSessions => Set<PosSession>();
    public DbSet<VietQrTransaction> VietQrTransactions => Set<VietQrTransaction>();

    // Identity Module (4 tables)
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply Fluent API configurations from current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
