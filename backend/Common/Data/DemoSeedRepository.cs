using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Common.Data;

internal sealed class DemoSeedRepository(AppDbContext dbContext) : IDemoSeedRepository
{
    private const string SupplierName = "FPT Synnex Distribution";
    private const string PurchaseOrderNumber = "PO-DEMO-202610-001";
    private const string VoucherCode = "DEMO-OCT26";

    public async Task AcquireSeedLockAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(731204620261018)", cancellationToken);
        }
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var roles = await EnsureRolesAsync(cancellationToken);
        var users = await EnsureUsersAsync(roles, cancellationToken);
        var categories = await EnsureCategoriesAsync(cancellationToken);
        var products = await EnsureProductsAsync(categories, cancellationToken);
        var supplier = await EnsureSupplierAsync(cancellationToken);
        var purchaseOrder = await EnsurePurchaseOrderAsync(supplier, users.Warehouse, products, cancellationToken);
        var customers = await EnsureCustomersAsync(cancellationToken);
        await EnsureVoucherAsync(cancellationToken);
        var session = await EnsureSessionAsync(users.Cashier, cancellationToken);
        var serials = await EnsureInventoryAsync(purchaseOrder, users.Warehouse, users.Cashier, products, cancellationToken);
        await EnsureLoyaltyAsync(customers, cancellationToken);
        await EnsureOrdersAsync(users, customers, session, products, serials, cancellationToken);
        await EnsureSessionTotalsAsync(session, cancellationToken);
    }

    private async Task<Dictionary<RoleType, Role>> EnsureRolesAsync(CancellationToken ct)
    {
        var existing = await dbContext.Roles.ToListAsync(ct);
        var result = new Dictionary<RoleType, Role>();
        foreach (var roleType in Enum.GetValues<RoleType>())
        {
            var matches = existing.Where(x => x.Name == roleType).ToList();
            if (matches.Count > 1) throw Conflict("role", roleType.ToString(), "duplicate natural key");
            var role = matches.SingleOrDefault();
            if (role is null)
            {
                role = new Role { Id = DemoSeedManifest.Id($"role:{roleType}"), Name = roleType, CreatedAt = DemoSeedManifest.AnchorUtc };
                dbContext.Roles.Add(role);
            }
            result.Add(roleType, role);
        }
        return result;
    }

    private async Task<(User Admin, User Cashier, User Warehouse)> EnsureUsersAsync(
        IReadOnlyDictionary<RoleType, Role> roles, CancellationToken ct)
    {
        var rows = await dbContext.Users.ToListAsync(ct);
        var admin = await EnsureUserAsync(rows, "admin", "admin@techstore.vn", "System Administrator", "Admin@123", ct);
        var cashier = await EnsureUserAsync(rows, "cashier1", "cashier1@techstore.vn", "Nguyen Thu Ngan", "Cashier@123", ct);
        var warehouse = await EnsureUserAsync(rows, "warehouse1", "warehouse1@techstore.vn", "Tran Van Kho", "Warehouse@123", ct);

        await EnsureUserRoleAsync(admin, roles[RoleType.Admin], ct);
        await EnsureUserRoleAsync(cashier, roles[RoleType.SalesStaff], ct);
        await EnsureUserRoleAsync(warehouse, roles[RoleType.WarehouseStaff], ct);
        return (admin, cashier, warehouse);
    }

    private async Task<User> EnsureUserAsync(List<User> rows, string username, string email, string fullName, string password, CancellationToken ct)
    {
        var matches = rows.Where(x => string.Equals(x.Username.Trim(), username, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count > 1) throw Conflict("user", username, "duplicate username");
        var user = matches.SingleOrDefault();
        if (user is not null)
        {
            if (!string.Equals(user.Email.Trim(), email, StringComparison.OrdinalIgnoreCase))
                throw Conflict("user", username, $"email is {user.Email}, expected {email}");
            if (!user.IsActive) throw Conflict("user", username, "seed account is inactive; refusing to reactivate a user record");
            if (username == "admin" && user.Id != Guid.Parse("11111111-1111-1111-1111-111111111111"))
                throw Conflict("user", username, "identifier differs from the fixed administrative ID");
            return user;
        }

        if (rows.Any(x => string.Equals(x.Email.Trim(), email, StringComparison.OrdinalIgnoreCase)))
            throw Conflict("user", email, "email belongs to a different username");

        user = new User
        {
            Id = username == "admin" ? Guid.Parse("11111111-1111-1111-1111-111111111111") : DemoSeedManifest.Id($"user:{username}"),
            Username = username,
            Email = email,
            FullName = fullName,
            IsActive = true,
            CreatedAt = DemoSeedManifest.AnchorUtc
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        dbContext.Users.Add(user);
        rows.Add(user);
        await Task.CompletedTask;
        return user;
    }

    private async Task EnsureUserRoleAsync(User user, Role role, CancellationToken ct)
    {
        var exists = await dbContext.UserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id, ct);
        if (!exists) dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = DemoSeedManifest.AnchorUtc });
    }

    private async Task<Dictionary<string, Category>> EnsureCategoriesAsync(CancellationToken ct)
    {
        var rows = await dbContext.Categories.ToListAsync(ct);
        var result = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in DemoSeedManifest.Categories)
        {
            var match = OneByKey(rows, x => x.Slug, seed.Slug, "category");
            if (match is null)
            {
                match = new Category
                {
                    Id = DemoSeedManifest.Id($"category:{seed.Slug}"), Name = seed.Name, Slug = seed.Slug,
                    Description = seed.Description, IsActive = true, CreatedAt = DemoSeedManifest.AnchorUtc
                };
                dbContext.Categories.Add(match);
                rows.Add(match);
            }
            result.Add(seed.Slug, match);
        }
        return result;
    }

    private async Task<Dictionary<string, Product>> EnsureProductsAsync(IReadOnlyDictionary<string, Category> categories, CancellationToken ct)
    {
        var rows = await dbContext.Products.ToListAsync(ct);
        var result = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in DemoSeedManifest.Products)
        {
            var category = categories[seed.CategorySlug];
            var product = OneByKey(rows, x => x.Sku, seed.Sku, "product");
            if (product is null)
            {
                product = new Product
                {
                    Id = DemoSeedManifest.Id($"product:{seed.Sku}"), CategoryId = category.Id,
                    Name = seed.Name, Sku = seed.Sku, Brand = seed.Brand, BasePrice = seed.Price,
                    CostPrice = seed.Cost, Description = seed.Name, Specs = seed.Specs,
                    IsSerialTracked = seed.SerialTracked, IsActive = true, CreatedAt = seed.CreatedAt
                };
                dbContext.Products.Add(product);
                rows.Add(product);
            }
            else if (product.CategoryId != category.Id || product.IsSerialTracked != seed.SerialTracked)
            {
                throw Conflict("product", seed.Sku, "category relationship or serial-tracking contract does not match the seed fixture");
            }
            result.Add(seed.Sku, product);

            if (seed.VariantSku is not null)
            {
                var variants = await dbContext.ProductVariants.ToListAsync(ct);
                var variant = OneByKey(variants, x => x.Sku, seed.VariantSku, "product variant");
                if (variant is null)
                {
                    variant = new ProductVariant
                    {
                        Id = DemoSeedManifest.Id($"variant:{seed.VariantSku}"), ProductId = product.Id,
                        VariantName = seed.Name, Sku = seed.VariantSku, Price = seed.Price,
                        Specs = "{}", IsActive = true, CreatedAt = seed.CreatedAt
                    };
                    dbContext.ProductVariants.Add(variant);
                    variants.Add(variant);
                }
                else if (variant.ProductId != product.Id)
                {
                    throw Conflict("product variant", seed.VariantSku, "parent product relationship does not match");
                }
            }
        }
        return result;
    }

    private async Task<Supplier> EnsureSupplierAsync(CancellationToken ct)
    {
        var rows = await dbContext.Suppliers.ToListAsync(ct);
        var supplier = OneByKey(rows, x => x.Name, SupplierName, "supplier");
        if (supplier is not null) return supplier;
        supplier = new Supplier
        {
            Id = DemoSeedManifest.Id($"supplier:{SupplierName.ToUpperInvariant()}"), Name = SupplierName,
            ContactName = "Nguyen Minh Tuan", Phone = "02873006666", Email = "contact@synnexfpt.com.vn",
            Address = "Ho Chi Minh City", IsActive = true, CreatedAt = DemoSeedManifest.AnchorUtc.AddDays(-10)
        };
        dbContext.Suppliers.Add(supplier);
        return supplier;
    }

    private async Task<PurchaseOrder> EnsurePurchaseOrderAsync(
        Supplier supplier, User warehouse, IReadOnlyDictionary<string, Product> products, CancellationToken ct)
    {
        var orders = await dbContext.PurchaseOrders.ToListAsync(ct);
        var po = OneByKey(orders, x => x.PoNumber, PurchaseOrderNumber, "purchase order");
        var receivedAt = DemoSeedManifest.AnchorUtc.AddDays(-8);
        var tracked = DemoSeedManifest.Products.Where(x => x.SerialTracked && x.Sku.StartsWith("DEMO-", StringComparison.Ordinal)).ToArray();
        if (po is null)
        {
            po = new PurchaseOrder
            {
                Id = DemoSeedManifest.Id($"purchase-order:{PurchaseOrderNumber}"), SupplierId = supplier.Id,
                CreatedByUserId = warehouse.Id, ReceivedByUserId = warehouse.Id, PoNumber = PurchaseOrderNumber,
                Status = PurchaseOrderStatus.Received,
                TotalCost = tracked.Sum(x => x.Cost * 5), Notes = "Deterministic Sprint 2 demo receipt",
                OrderedAt = receivedAt.AddDays(-1), ReceivedAt = receivedAt, CreatedAt = receivedAt.AddDays(-1)
            };
            dbContext.PurchaseOrders.Add(po);
            orders.Add(po);
        }
        else if (po.Status != PurchaseOrderStatus.Received || po.SupplierId != supplier.Id)
        {
            throw Conflict("purchase order", PurchaseOrderNumber, "status or supplier conflicts with fixture");
        }

        var items = await dbContext.PurchaseOrderItems.ToListAsync(ct);
        var stocksBySku = new Dictionary<string, PurchaseOrderItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in tracked)
        {
            var product = products[seed.Sku];
            var item = items.SingleOrDefault(x => x.PurchaseOrderId == po.Id && x.ProductId == product.Id && x.VariantId == null);
            if (item is null)
            {
                item = new PurchaseOrderItem
                {
                    Id = DemoSeedManifest.Id($"purchase-order-item:{PurchaseOrderNumber}:{seed.Sku}"),
                    PurchaseOrderId = po.Id, ProductId = product.Id, OrderedQuantity = 5, ReceivedQuantity = 5,
                    UnitCost = seed.Cost, CreatedAt = receivedAt.AddDays(-1)
                };
                dbContext.PurchaseOrderItems.Add(item);
                items.Add(item);
            }
            else if (item.ReceivedQuantity != 5 || item.OrderedQuantity < item.ReceivedQuantity)
            {
                throw Conflict("purchase order item", $"{PurchaseOrderNumber}/{seed.Sku}", "received quantity conflicts with fixture");
            }
            stocksBySku.Add(seed.Sku, item);
        }
        _purchaseItemsBySku = stocksBySku;
        return po;
    }

    private Dictionary<string, PurchaseOrderItem> _purchaseItemsBySku = new(StringComparer.OrdinalIgnoreCase);

    private async Task<Dictionary<string, Customer>> EnsureCustomersAsync(CancellationToken ct)
    {
        var rows = await dbContext.Customers.ToListAsync(ct);
        var result = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in DemoSeedManifest.Customers)
        {
            var customer = OneByKey(rows, x => x.Phone, seed.Phone, "customer");
            if (customer is null)
            {
                customer = new Customer
                {
                    Id = DemoSeedManifest.Id($"customer:{seed.Phone}"), FullName = seed.Name,
                    Phone = seed.Phone, Email = seed.Email, DateOfBirth = seed.DateOfBirth,
                    LoyaltyPoints = seed.LoyaltyPoints, Tier = seed.Tier, IsActive = true, CreatedAt = seed.CreatedAt
                };
                dbContext.Customers.Add(customer);
                rows.Add(customer);
            }
            result.Add(seed.Phone, customer);
        }
        return result;
    }

    private async Task<Voucher> EnsureVoucherAsync(CancellationToken ct)
    {
        var rows = await dbContext.Vouchers.ToListAsync(ct);
        var voucher = OneByKey(rows, x => x.Code, VoucherCode, "voucher");
        if (voucher is not null)
        {
            if (!voucher.IsActive || voucher.DiscountType != VoucherType.FixedAmount || voucher.DiscountValue != 100000m || voucher.UsedCount != 0)
                throw Conflict("voucher", VoucherCode, "active state, discount, or usage conflicts with fixture");
            return voucher;
        }
        voucher = new Voucher
        {
            Id = DemoSeedManifest.Id($"voucher:{VoucherCode}"), Code = VoucherCode,
            Description = "Demo fixture voucher", DiscountType = VoucherType.FixedAmount,
            DiscountValue = 100000m, MinOrderAmount = 1000000m, UsageLimit = 100, UsedCount = 0,
            ExpiresAt = DemoSeedManifest.AnchorUtc.AddYears(2), IsActive = true,
            CreatedAt = DemoSeedManifest.AnchorUtc.AddDays(-10)
        };
        dbContext.Vouchers.Add(voucher);
        return voucher;
    }

    private async Task<PosSession> EnsureSessionAsync(User cashier, CancellationToken ct)
    {
        var id = DemoSeedManifest.Id("pos-session:demo-october-2026");
        var session = await dbContext.PosSessions.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (session is not null)
        {
            if (session.CashierUserId != cashier.Id) throw Conflict("POS session", id.ToString(), "cashier mismatch");
            return session;
        }
        session = new PosSession
        {
            Id = id, CashierUserId = cashier.Id, OpeningBalance = 1000000m,
            Status = PosSessionStatus.Closed, OpenedAt = DemoSeedManifest.AnchorUtc.AddDays(-10),
            ClosedAt = DemoSeedManifest.AnchorUtc.AddMinutes(1), CreatedAt = DemoSeedManifest.AnchorUtc.AddDays(-10)
        };
        dbContext.PosSessions.Add(session);
        return session;
    }

    private async Task<Dictionary<(string Sku, int Unit), SerialImei>> EnsureInventoryAsync(
        PurchaseOrder po, User warehouse, User cashier, IReadOnlyDictionary<string, Product> products, CancellationToken ct)
    {
        var variants = await dbContext.ProductVariants.ToListAsync(ct);
        var stocks = await dbContext.InventoryStocks.ToListAsync(ct);
        var serialRows = await dbContext.SerialImeis.ToListAsync(ct);
        var serials = new Dictionary<(string Sku, int Unit), SerialImei>();
        var seedOrdersBySerial = DemoSeedManifest.Orders
            .Where(x => x.Status is OrderStatus.Completed or OrderStatus.Processing)
            .ToDictionary(x => (DemoSeedManifest.Products[x.ProductIndex].Sku, x.SerialIndex));
        var orderItemIds = DemoSeedManifest.Orders.ToDictionary(x => x.OrderCode, x => DemoSeedManifest.Id($"order-item:{x.OrderCode}:line-1"));

        foreach (var seed in DemoSeedManifest.Serials)
        {
            var product = products[seed.ProductSku];
            var variant = variants.SingleOrDefault(x => x.ProductId == product.Id);
            var productSerials = DemoSeedManifest.Serials.Where(x => x.ProductSku == seed.ProductSku).ToList();
            var unit = productSerials.IndexOf(seed);
            var matchingOrder = seedOrdersBySerial.GetValueOrDefault((seed.ProductSku, unit));
            var status = matchingOrder?.Status == OrderStatus.Completed
                ? SerialImeiStatus.Sold
                : matchingOrder?.Status == OrderStatus.Processing ? SerialImeiStatus.Reserved : SerialImeiStatus.InStock;
            var serial = OneByKey(serialRows, x => x.Imei ?? string.Empty, seed.Imei, "IMEI");
            if (serial is null)
            {
                serial = new SerialImei
                {
                    Id = DemoSeedManifest.Id($"imei:{seed.Imei}"), ProductId = product.Id,
                    VariantId = variant?.Id, Imei = seed.Imei, Status = status,
                    UnitCost = DemoSeedManifest.Products.Single(x => x.Sku == seed.ProductSku).Cost,
                    PurchaseOrderId = po.Id, WarrantyMonths = 12,
                    WarrantyStartAt = status == SerialImeiStatus.Sold ? matchingOrder!.CompletedAt : null,
                    WarrantyEndAt = status == SerialImeiStatus.Sold ? matchingOrder!.CompletedAt!.Value.AddMonths(12) : null,
                    CreatedAt = seed.CreatedAt
                };
                dbContext.SerialImeis.Add(serial);
                serialRows.Add(serial);
            }
            else if (serial.ProductId != product.Id || serial.Status != status || serial.VariantId != variant?.Id)
            {
                throw Conflict("IMEI", seed.Imei, "product, variant, or lifecycle status conflicts with fixture");
            }
            serials[(seed.ProductSku, unit)] = serial;
        }

        var movements = await dbContext.InventoryMovements.ToListAsync(ct);
        var newTrackedProducts = DemoSeedManifest.Products.Where(x => x.SerialTracked && x.Sku.StartsWith("DEMO-", StringComparison.Ordinal)).ToArray();
        foreach (var seed in newTrackedProducts)
        {
            var product = products[seed.Sku];
            var variant = variants.SingleOrDefault(x => x.ProductId == product.Id);
            var stock = stocks.SingleOrDefault(x => x.ProductId == product.Id && x.VariantId == variant?.Id);
            var completedSales = DemoSeedManifest.Orders
                .Where(x => x.Status == OrderStatus.Completed && DemoSeedManifest.Products[x.ProductIndex].Sku == seed.Sku)
                .OrderBy(x => x.CompletedAt)
                .ToArray();
            var reservations = DemoSeedManifest.Orders.Count(x => x.Status == OrderStatus.Processing && DemoSeedManifest.Products[x.ProductIndex].Sku == seed.Sku);
            var finalQuantity = 5 - completedSales.Length;
            if (stock is null)
            {
                stock = new InventoryStock
                {
                    Id = DemoSeedManifest.Id($"stock:{seed.Sku}"), ProductId = product.Id, VariantId = variant?.Id,
                    Quantity = finalQuantity, ReservedQuantity = reservations, MinStockAlert = 3,
                    CreatedAt = DemoSeedManifest.AnchorUtc.AddDays(-8)
                };
                dbContext.InventoryStocks.Add(stock);
                stocks.Add(stock);
            }
            else if (stock.Quantity != finalQuantity || stock.ReservedQuantity != reservations)
            {
                throw Conflict("inventory stock", seed.Sku, $"expected quantity/reserved {finalQuantity}/{reservations}, found {stock.Quantity}/{stock.ReservedQuantity}");
            }

            var purchaseItem = _purchaseItemsBySku[seed.Sku];
            var receiptMatches = movements.Where(x => x.PurchaseOrderItemId == purchaseItem.Id).Take(2).ToList();
            if (receiptMatches.Count > 1)
                throw Conflict("purchase receipt movement", seed.Sku, "duplicate source ledger rows");
            if (receiptMatches.Count == 1)
            {
                var receipt = receiptMatches[0];
                if (receipt.InventoryStockId != stock.Id || receipt.MovementType != InventoryMovementType.PurchaseReceipt ||
                    receipt.QuantityChange != 5 || receipt.QuantityAfter != 5)
                    throw Conflict("purchase receipt movement", seed.Sku, "source ledger values conflict with fixture");
            }
            else
            {
                movements.Add(new InventoryMovement
                {
                    Id = DemoSeedManifest.Id($"movement:receipt:{PurchaseOrderNumber}:{seed.Sku}"),
                    InventoryStockId = stock.Id, MovementType = InventoryMovementType.PurchaseReceipt,
                    QuantityChange = 5, QuantityAfter = 5, PerformedByUserId = warehouse.Id,
                    PurchaseOrderItemId = purchaseItem.Id, Reason = $"Received from {PurchaseOrderNumber}",
                    CreatedAt = DemoSeedManifest.AnchorUtc.AddDays(-8)
                });
            }

            var quantityAfter = 5;
            foreach (var sale in completedSales)
            {
                var orderItemId = orderItemIds[sale.OrderCode];
                var saleMatches = movements.Where(x => x.OrderItemId == orderItemId).Take(2).ToList();
                if (saleMatches.Count > 1)
                    throw Conflict("sale movement", sale.OrderCode, "duplicate order-item ledger rows");
                if (saleMatches.Count == 1)
                {
                    quantityAfter--;
                    var existing = saleMatches[0];
                    if (existing.InventoryStockId != stock.Id || existing.MovementType != InventoryMovementType.Sale ||
                        existing.QuantityChange != -1 || existing.QuantityAfter != quantityAfter)
                        throw Conflict("sale movement", sale.OrderCode, "order-item ledger values conflict with fixture");
                    continue;
                }
                quantityAfter--;
                movements.Add(new InventoryMovement
                {
                    Id = DemoSeedManifest.Id($"movement:sale:{sale.OrderCode}:line-1"),
                    InventoryStockId = stock.Id, MovementType = InventoryMovementType.Sale,
                    QuantityChange = -1, QuantityAfter = quantityAfter,
                    PerformedByUserId = cashier.Id, OrderItemId = orderItemId,
                    Reason = $"Completed {sale.OrderCode}", CreatedAt = sale.CompletedAt!.Value
                });
            }
        }

        // Non-serialized products used by pending and cancelled orders need a balance row, but no reservation or movement.
        foreach (var seed in DemoSeedManifest.Products.Where(x => !x.SerialTracked && x.Sku.StartsWith("DEMO-", StringComparison.Ordinal)))
        {
            var product = products[seed.Sku];
            var stock = stocks.SingleOrDefault(x => x.ProductId == product.Id && x.VariantId == null);
            if (stock is null)
            {
                stock = new InventoryStock
                {
                    Id = DemoSeedManifest.Id($"stock:{seed.Sku}"), ProductId = product.Id,
                    Quantity = 0, ReservedQuantity = 0, MinStockAlert = 3,
                    CreatedAt = DemoSeedManifest.AnchorUtc.AddDays(-8)
                };
                dbContext.InventoryStocks.Add(stock);
                stocks.Add(stock);
            }
        }

        foreach (var movement in movements.Where(x => x.Id != Guid.Empty && dbContext.Entry(x).State == EntityState.Detached))
            dbContext.InventoryMovements.Add(movement);
        return serials;
    }

    private async Task EnsureLoyaltyAsync(IReadOnlyDictionary<string, Customer> customers, CancellationToken ct)
    {
        var rows = await dbContext.CustomerLoyaltyPoints.ToListAsync(ct);
        for (var i = 2; i < DemoSeedManifest.Customers.Count; i++)
        {
            var seed = DemoSeedManifest.Customers[i];
            var customer = customers[seed.Phone];
            var reason = "Initial Sprint 2 demo loyalty balance";
            if (customer.LoyaltyPoints != seed.LoyaltyPoints)
                throw Conflict("customer loyalty", seed.Phone, $"balance {customer.LoyaltyPoints} differs from fixture {seed.LoyaltyPoints}");
            var matches = rows.Where(x => x.CustomerId == customer.Id && x.Reason == reason).Take(2).ToList();
            if (matches.Count > 1) throw Conflict("customer loyalty", seed.Phone, "duplicate seed ledger key");
            var match = matches.SingleOrDefault();
            if (match is null)
            {
                dbContext.CustomerLoyaltyPoints.Add(new CustomerLoyaltyPoint
                {
                    Id = DemoSeedManifest.Id($"loyalty:{seed.Phone}:initial"), CustomerId = customer.Id,
                    PointsChange = seed.LoyaltyPoints, BalanceAfter = seed.LoyaltyPoints, Reason = reason,
                    CreatedAt = seed.CreatedAt
                });
            }
        }
    }

    private async Task EnsureOrdersAsync(
        (User Admin, User Cashier, User Warehouse) users,
        IReadOnlyDictionary<string, Customer> customers,
        PosSession session,
        IReadOnlyDictionary<string, Product> products,
        IReadOnlyDictionary<(string Sku, int Unit), SerialImei> serials,
        CancellationToken ct)
    {
        var orders = await dbContext.Orders.ToListAsync(ct);
        var items = await dbContext.OrderItems.ToListAsync(ct);
        var variants = await dbContext.ProductVariants.ToListAsync(ct);
        var qrRows = await dbContext.VietQrTransactions.ToListAsync(ct);
        foreach (var seed in DemoSeedManifest.Orders)
        {
            var productSeed = DemoSeedManifest.Products[seed.ProductIndex];
            var product = products[productSeed.Sku];
            var variant = productSeed.VariantSku is null ? null : variants.Single(x => x.Sku == productSeed.VariantSku);
            var amount = variant?.Price ?? productSeed.Price;
            var order = OneByKey(orders, x => x.OrderCode, seed.OrderCode, "order");
            if (order is null)
            {
                order = new Order
                {
                    Id = DemoSeedManifest.Id($"order:{seed.OrderCode}"), OrderCode = seed.OrderCode,
                    CustomerId = customers[DemoSeedManifest.Customers[seed.CustomerIndex].Phone].Id,
                    CashierUserId = users.Cashier.Id, PosSessionId = session.Id,
                    Status = seed.Status, PaymentMethod = seed.PaymentMethod,
                    Subtotal = amount, TotalAmount = amount, DiscountAmount = 0, TaxAmount = 0,
                    PaidAt = seed.PaidAt, CompletedAt = seed.CompletedAt, CreatedAt = seed.CreatedAt
                };
                dbContext.Orders.Add(order);
                orders.Add(order);
            }
            else if (order.Status != seed.Status || order.TotalAmount != amount || order.CreatedAt != seed.CreatedAt ||
                     order.PaymentMethod != seed.PaymentMethod || order.PaidAt != seed.PaidAt || order.CompletedAt != seed.CompletedAt ||
                     order.CustomerId != customers[DemoSeedManifest.Customers[seed.CustomerIndex].Phone].Id ||
                     order.CashierUserId != users.Cashier.Id || order.PosSessionId != session.Id)
            {
                throw Conflict("order", seed.OrderCode, "identity, status, payment, amount, or lifecycle timestamps conflict with fixture");
            }

            var serial = seed.Status is OrderStatus.Completed or OrderStatus.Processing
                ? serials[(productSeed.Sku, seed.SerialIndex)]
                : null;
            var item = items.SingleOrDefault(x => x.OrderId == order.Id);
            if (item is null)
            {
                item = new OrderItem
                {
                    Id = DemoSeedManifest.Id($"order-item:{seed.OrderCode}:line-1"), OrderId = order.Id,
                    ProductId = product.Id, VariantId = variant?.Id, SerialImeiId = serial?.Id,
                    ProductName = product.Name, VariantName = variant?.VariantName,
                    Sku = variant?.Sku ?? product.Sku, UnitPrice = amount, UnitCost = productSeed.Cost,
                    Quantity = 1, DiscountAmount = 0, TotalPrice = amount, CreatedAt = seed.CreatedAt
                };
                dbContext.OrderItems.Add(item);
                items.Add(item);
            }
            else if (item.ProductId != product.Id || item.VariantId != variant?.Id || item.SerialImeiId != serial?.Id ||
                     item.TotalPrice != amount || item.Quantity != 1 || item.Sku != (variant?.Sku ?? product.Sku))
            {
                throw Conflict("order item", seed.OrderCode, "product, serial, SKU, quantity, or total conflicts with fixture");
            }

            if (seed.PaymentMethod == PaymentMethod.VietQr && seed.Status == OrderStatus.Completed)
            {
                var code = $"DQR-{seed.OrderCode}";
                var transaction = OneByKey(qrRows, x => x.TransactionCode, code, "VietQR transaction");
                if (transaction is null)
                {
                    transaction = new VietQrTransaction
                    {
                        Id = DemoSeedManifest.Id($"vietqr:{code}"), OrderId = order.Id, PosSessionId = session.Id,
                        TransactionCode = code, Provider = "MBBank", ProviderTransactionId = $"REF-{seed.OrderCode}",
                        BankBin = "970422", BankAccountNumber = "1234567890", Amount = amount,
                        QrContent = $"TECHSTORE:{code}:{amount:0.00}", Status = VietQrStatus.Confirmed,
                        ExpiresAt = seed.CreatedAt.AddMinutes(30), ConfirmedAt = seed.PaidAt,
                        ConfirmedByUserId = users.Admin.Id, CreatedAt = seed.CreatedAt
                    };
                    dbContext.VietQrTransactions.Add(transaction);
                    qrRows.Add(transaction);
                }
                else if (transaction.OrderId != order.Id || transaction.Amount != amount || transaction.Status != VietQrStatus.Confirmed)
                {
                    throw Conflict("VietQR transaction", code, "order, amount, or status conflicts with fixture");
                }
            }
        }

    }

    private async Task EnsureSessionTotalsAsync(PosSession session, CancellationToken ct)
    {
        var sessionOrders = dbContext.Orders.Local.Where(x => x.PosSessionId == session.Id && x.PaidAt != null).ToList();
        var cash = sessionOrders.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.TotalAmount);
        var qr = sessionOrders.Where(x => x.PaymentMethod == PaymentMethod.VietQr).Sum(x => x.TotalAmount);
        if (session.CashSalesTotal == cash && session.VietQrSalesTotal == qr) return;
        if (session.CashSalesTotal != 0 || session.VietQrSalesTotal != 0)
            throw Conflict("POS session", session.Id.ToString(), "cached totals conflict with the paid order set");
        session.CashSalesTotal = cash;
        session.VietQrSalesTotal = qr;
        session.ClosingBalance = session.OpeningBalance + cash;
        await Task.CompletedTask;
    }

    private static T? OneByKey<T>(IEnumerable<T> rows, Func<T, string> keySelector, string key, string entity)
        => DemoSeedNaturalKey.FindSingle(rows, keySelector, key, entity);

    private static InvalidOperationException Conflict(string entity, string key, string reason) =>
        new($"Demo seed conflict for {entity} '{key}': {reason}.");
}
