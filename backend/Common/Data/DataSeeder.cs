using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Common.Data;

public class DataSeeder : IDataSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly PasswordHasher<User> _passwordHasher;

    public DataSeeder(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // 1. Idempotency check: if roles already exist, skip seeding
        if (await _dbContext.Roles.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;

        // 2. Roles
        var adminRole = new Role { Id = Guid.NewGuid(), Name = RoleType.Admin, Description = "Full system administration and settings", CreatedAt = now };
        var managerRole = new Role { Id = Guid.NewGuid(), Name = RoleType.Manager, Description = "Store management and business analytics", CreatedAt = now };
        var salesRole = new Role { Id = Guid.NewGuid(), Name = RoleType.SalesStaff, Description = "POS register checkout and sales", CreatedAt = now };
        var warehouseRole = new Role { Id = Guid.NewGuid(), Name = RoleType.WarehouseStaff, Description = "Warehouse intake and inventory management", CreatedAt = now };

        _dbContext.Roles.AddRange(adminRole, managerRole, salesRole, warehouseRole);

        // 3. Users
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@techstore.vn",
            FullName = "System Administrator",
            IsActive = true,
            CreatedAt = now
        };
        adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, "Admin@123");

        var cashierUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "cashier1",
            Email = "cashier1@techstore.vn",
            FullName = "Nguyen Thu Ngan",
            IsActive = true,
            CreatedAt = now
        };
        cashierUser.PasswordHash = _passwordHasher.HashPassword(cashierUser, "Cashier@123");

        var warehouseUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "warehouse1",
            Email = "warehouse1@techstore.vn",
            FullName = "Tran Van Kho",
            IsActive = true,
            CreatedAt = now
        };
        warehouseUser.PasswordHash = _passwordHasher.HashPassword(warehouseUser, "Warehouse@123");

        _dbContext.Users.AddRange(adminUser, cashierUser, warehouseUser);

        // User Roles
        _dbContext.UserRoles.AddRange(
            new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id, AssignedAt = now },
            new UserRole { UserId = cashierUser.Id, RoleId = salesRole.Id, AssignedAt = now },
            new UserRole { UserId = warehouseUser.Id, RoleId = warehouseRole.Id, AssignedAt = now }
        );

        // 4. Categories
        var catSmartphones = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Smartphones",
            Slug = "smartphones",
            Description = "Dien thoai thong minh chinh hang tu cac thuong hieu hang dau",
            IsActive = true,
            CreatedAt = now
        };

        var catLaptops = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Laptops",
            Slug = "laptops",
            Description = "May tinh xach tay cao cap, mong nhe va chuyen nghiep",
            IsActive = true,
            CreatedAt = now
        };

        _dbContext.Categories.AddRange(catSmartphones, catLaptops);

        // 5. Products (~5 products with JSONB specs and variants)
        // Product 1: iPhone 15 Pro Max
        var prodIPhone = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = catSmartphones.Id,
            Name = "iPhone 15 Pro Max 256GB",
            Sku = "IP15PM-256",
            Brand = "Apple",
            BasePrice = 29990000m,
            CostPrice = 26000000m,
            Description = "Sieu pham Apple chip A17 Pro, vo Titan chuan hang khong vu tru",
            Specs = "{\"cpu\": \"Apple A17 Pro\", \"ram_gb\": 8, \"storage_gb\": 256, \"battery_mah\": 4422, \"color\": \"Natural Titanium\"}",
            IsSerialTracked = true,
            IsActive = true,
            CreatedAt = now
        };
        var varIPhoneNatural = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = prodIPhone.Id,
            VariantName = "Titan Tu Nhien 256GB",
            Sku = "IP15PM-256-NT",
            Price = 29990000m,
            Specs = "{\"color\": \"Natural Titanium\"}",
            IsActive = true,
            CreatedAt = now
        };

        // Product 2: Galaxy S24 Ultra
        var prodS24 = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = catSmartphones.Id,
            Name = "Samsung Galaxy S24 Ultra 256GB",
            Sku = "S24U-256",
            Brand = "Samsung",
            BasePrice = 27990000m,
            CostPrice = 24000000m,
            Description = "Flagship Samsung tich hop Galaxy AI quyen nang",
            Specs = "{\"cpu\": \"Snapdragon 8 Gen 3 for Galaxy\", \"ram_gb\": 12, \"storage_gb\": 256, \"battery_mah\": 5000, \"color\": \"Titanium Gray\"}",
            IsSerialTracked = true,
            IsActive = true,
            CreatedAt = now
        };
        var varS24Gray = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = prodS24.Id,
            VariantName = "Xam Titan 256GB",
            Sku = "S24U-256-GR",
            Price = 27990000m,
            Specs = "{\"color\": \"Titanium Gray\"}",
            IsActive = true,
            CreatedAt = now
        };

        // Product 3: MacBook Pro 14 M3
        var prodMacBook = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = catLaptops.Id,
            Name = "MacBook Pro 14 inch M3 512GB",
            Sku = "MBP14-M3-512",
            Brand = "Apple",
            BasePrice = 39990000m,
            CostPrice = 35000000m,
            Description = "MacBook Pro trang bi chip M3 vuot troi cho lap trinh va sang tao",
            Specs = "{\"cpu\": \"Apple M3\", \"ram_gb\": 16, \"storage_gb\": 512, \"battery_mah\": 7000, \"color\": \"Space Gray\"}",
            IsSerialTracked = true,
            IsActive = true,
            CreatedAt = now
        };
        var varMacBookGray = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = prodMacBook.Id,
            VariantName = "Xam Khong Gian 512GB",
            Sku = "MBP14-M3-512-SG",
            Price = 39990000m,
            Specs = "{\"color\": \"Space Gray\"}",
            IsActive = true,
            CreatedAt = now
        };

        // Product 4: Dell XPS 15 (standalone, no variants)
        var prodDell = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = catLaptops.Id,
            Name = "Dell XPS 15 9530 Core i7",
            Sku = "DELL-XPS15-9530",
            Brand = "Dell",
            BasePrice = 45000000m,
            CostPrice = 40000000m,
            Description = "Laptop doanh nhan Dell XPS man hinh OLED 3.5K",
            Specs = "{\"cpu\": \"Intel Core i7-13700H\", \"ram_gb\": 32, \"storage_gb\": 1024, \"battery_mah\": 8600, \"color\": \"Platinum Silver\"}",
            IsSerialTracked = true,
            IsActive = true,
            CreatedAt = now
        };

        // Product 5: Sony WH-1000XM5 (accessory, non-serial tracked)
        var prodSony = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = catSmartphones.Id,
            Name = "Sony WH-1000XM5 Chong On",
            Sku = "SONY-WH1000XM5",
            Brand = "Sony",
            BasePrice = 7990000m,
            CostPrice = 6500000m,
            Description = "Tai nghe chup tai chong on hang dau the gioi",
            Specs = "{\"bluetooth\": \"5.2\", \"battery_mah\": 1200, \"color\": \"Black\"}",
            IsSerialTracked = false,
            IsActive = true,
            CreatedAt = now
        };

        _dbContext.Products.AddRange(prodIPhone, prodS24, prodMacBook, prodDell, prodSony);
        _dbContext.ProductVariants.AddRange(varIPhoneNatural, varS24Gray, varMacBookGray);

        // 6. Supplier & Purchase Order
        var supplierFpt = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "FPT Synnex Distribution",
            ContactName = "Nguyen Minh Tuan",
            Phone = "02873006666",
            Email = "contact@synnexfpt.com.vn",
            Address = "Toa nha FPT, Quan Tan Binh, TP. Ho Chi Minh",
            IsActive = true,
            CreatedAt = now
        };
        _dbContext.Suppliers.Add(supplierFpt);

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierFpt.Id,
            CreatedByUserId = warehouseUser.Id,
            ReceivedByUserId = warehouseUser.Id,
            PoNumber = "PO-202610-001",
            Status = PurchaseOrderStatus.Received,
            TotalCost = 217000000m,
            Notes = "Lo hang cong nghe nhap kho dau thang 10",
            OrderedAt = now.AddDays(-5),
            ReceivedAt = now.AddDays(-4),
            CreatedAt = now.AddDays(-5)
        };
        _dbContext.PurchaseOrders.Add(po);

        var poItemIPhone = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = prodIPhone.Id,
            VariantId = varIPhoneNatural.Id,
            OrderedQuantity = 5,
            ReceivedQuantity = 5,
            UnitCost = 26000000m,
            CreatedAt = now.AddDays(-5)
        };
        var poItemSony = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = prodSony.Id,
            VariantId = null,
            OrderedQuantity = 10,
            ReceivedQuantity = 10,
            UnitCost = 6500000m,
            CreatedAt = now.AddDays(-5)
        };
        _dbContext.PurchaseOrderItems.AddRange(poItemIPhone, poItemSony);

        // 7. Inventory Stocks, Movements & Serial/IMEIs
        // Stock for iPhone Variant 1
        var stockIPhone = new InventoryStock
        {
            Id = Guid.NewGuid(),
            ProductId = prodIPhone.Id,
            VariantId = varIPhoneNatural.Id,
            Quantity = 4, // 5 received, 1 sold in seed order
            ReservedQuantity = 0,
            MinStockAlert = 2,
            CreatedAt = now.AddDays(-4)
        };
        // Stock for Sony
        var stockSony = new InventoryStock
        {
            Id = Guid.NewGuid(),
            ProductId = prodSony.Id,
            VariantId = null,
            Quantity = 9, // 10 received, 1 sold in seed order
            ReservedQuantity = 0,
            MinStockAlert = 3,
            CreatedAt = now.AddDays(-4)
        };
        // Stock for Samsung
        var stockS24 = new InventoryStock
        {
            Id = Guid.NewGuid(),
            ProductId = prodS24.Id,
            VariantId = varS24Gray.Id,
            Quantity = 5,
            ReservedQuantity = 0,
            MinStockAlert = 2,
            CreatedAt = now.AddDays(-4)
        };
        // Stock for MacBook
        var stockMacBook = new InventoryStock
        {
            Id = Guid.NewGuid(),
            ProductId = prodMacBook.Id,
            VariantId = varMacBookGray.Id,
            Quantity = 3,
            ReservedQuantity = 0,
            MinStockAlert = 1,
            CreatedAt = now.AddDays(-4)
        };
        // Stock for Dell
        var stockDell = new InventoryStock
        {
            Id = Guid.NewGuid(),
            ProductId = prodDell.Id,
            VariantId = null,
            Quantity = 2,
            ReservedQuantity = 0,
            MinStockAlert = 1,
            CreatedAt = now.AddDays(-4)
        };

        _dbContext.InventoryStocks.AddRange(stockIPhone, stockSony, stockS24, stockMacBook, stockDell);

        // Inventory Movements (Opening balance / intake)
        _dbContext.InventoryMovements.AddRange(
            new InventoryMovement
            {
                Id = Guid.NewGuid(),
                InventoryStockId = stockIPhone.Id,
                MovementType = InventoryMovementType.PurchaseReceipt,
                QuantityChange = 5,
                QuantityAfter = 5,
                PerformedByUserId = warehouseUser.Id,
                PurchaseOrderItemId = poItemIPhone.Id,
                Reason = "Nhap kho tu PO-202610-001",
                CreatedAt = now.AddDays(-4)
            },
            new InventoryMovement
            {
                Id = Guid.NewGuid(),
                InventoryStockId = stockSony.Id,
                MovementType = InventoryMovementType.PurchaseReceipt,
                QuantityChange = 10,
                QuantityAfter = 10,
                PerformedByUserId = warehouseUser.Id,
                PurchaseOrderItemId = poItemSony.Id,
                Reason = "Nhap kho phu kien tu PO-202610-001",
                CreatedAt = now.AddDays(-4)
            }
        );

        // Serial / IMEIs (5 total for iPhone: 4 in stock, 1 sold)
        var serialSoldIPhone = new SerialImei
        {
            Id = Guid.NewGuid(),
            ProductId = prodIPhone.Id,
            VariantId = varIPhoneNatural.Id,
            Imei = "358912345678901",
            Status = SerialImeiStatus.Sold,
            UnitCost = 26000000m,
            PurchaseOrderId = po.Id,
            WarrantyMonths = 12,
            WarrantyStartAt = now.AddDays(-1),
            WarrantyEndAt = now.AddDays(-1).AddMonths(12),
            CreatedAt = now.AddDays(-4)
        };

        var serialInStockIPhone1 = new SerialImei
        {
            Id = Guid.NewGuid(),
            ProductId = prodIPhone.Id,
            VariantId = varIPhoneNatural.Id,
            Imei = "358912345678902",
            Status = SerialImeiStatus.InStock,
            UnitCost = 26000000m,
            PurchaseOrderId = po.Id,
            WarrantyMonths = 12,
            CreatedAt = now.AddDays(-4)
        };
        var serialInStockIPhone2 = new SerialImei
        {
            Id = Guid.NewGuid(),
            ProductId = prodIPhone.Id,
            VariantId = varIPhoneNatural.Id,
            Imei = "358912345678903",
            Status = SerialImeiStatus.InStock,
            UnitCost = 26000000m,
            PurchaseOrderId = po.Id,
            WarrantyMonths = 12,
            CreatedAt = now.AddDays(-4)
        };
        var serialInStockIPhone3 = new SerialImei
        {
            Id = Guid.NewGuid(),
            ProductId = prodIPhone.Id,
            VariantId = varIPhoneNatural.Id,
            Imei = "358912345678904",
            Status = SerialImeiStatus.InStock,
            UnitCost = 26000000m,
            PurchaseOrderId = po.Id,
            WarrantyMonths = 12,
            CreatedAt = now.AddDays(-4)
        };
        var serialInStockIPhone4 = new SerialImei
        {
            Id = Guid.NewGuid(),
            ProductId = prodIPhone.Id,
            VariantId = varIPhoneNatural.Id,
            Imei = "358912345678905",
            Status = SerialImeiStatus.InStock,
            UnitCost = 26000000m,
            PurchaseOrderId = po.Id,
            WarrantyMonths = 12,
            CreatedAt = now.AddDays(-4)
        };

        _dbContext.SerialImeis.AddRange(serialSoldIPhone, serialInStockIPhone1, serialInStockIPhone2, serialInStockIPhone3, serialInStockIPhone4);

        // 8. Customers & Vouchers
        var custAn = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Nguyen Van An",
            Phone = "0901234567",
            Email = "nguyenvanan@gmail.com",
            DateOfBirth = new DateOnly(1995, 5, 20),
            Address = "123 Le Loi, Quan 1, TP.HCM",
            LoyaltyPoints = 150,
            Tier = CustomerTier.Standard,
            IsActive = true,
            CreatedAt = now.AddMonths(-1)
        };

        var custBich = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Tran Thi Bich",
            Phone = "0912345678",
            Email = "tranthibich@gmail.com",
            DateOfBirth = new DateOnly(1998, 8, 15),
            Address = "456 Nguyen Hue, Quan 1, TP.HCM",
            LoyaltyPoints = 600,
            Tier = CustomerTier.Silver,
            IsActive = true,
            CreatedAt = now.AddMonths(-2)
        };

        _dbContext.Customers.AddRange(custAn, custBich);

        _dbContext.CustomerLoyaltyPoints.AddRange(
            new CustomerLoyaltyPoint
            {
                Id = Guid.NewGuid(),
                CustomerId = custAn.Id,
                PointsChange = 150,
                BalanceAfter = 150,
                Reason = "Diem thuong tich luy mua sam ban dau",
                CreatedAt = now.AddDays(-2)
            },
            new CustomerLoyaltyPoint
            {
                Id = Guid.NewGuid(),
                CustomerId = custBich.Id,
                PointsChange = 600,
                BalanceAfter = 600,
                Reason = "Diem thuong thanh vien bac Silver",
                CreatedAt = now.AddDays(-1)
            }
        );

        var voucher100k = new Voucher
        {
            Id = Guid.NewGuid(),
            Code = "TECHSTORE100K",
            Description = "Giam 100.000d cho don hang tu 1.000.000d",
            DiscountType = VoucherType.FixedAmount,
            DiscountValue = 100000m,
            MinOrderAmount = 1000000m,
            UsageLimit = 100,
            UsedCount = 1,
            ExpiresAt = now.AddMonths(3),
            IsActive = true,
            CreatedAt = now.AddDays(-10)
        };

        var voucherVip = new Voucher
        {
            Id = Guid.NewGuid(),
            Code = "VIPTECHSTORE",
            Description = "Giam 5% cho don hang cong nghe VIP",
            DiscountType = VoucherType.Percentage,
            DiscountValue = 5m,
            MinOrderAmount = 5000000m,
            MaxDiscountAmount = 1000000m,
            UsageLimit = 50,
            UsedCount = 0,
            ExpiresAt = now.AddMonths(6),
            IsActive = true,
            CreatedAt = now.AddDays(-10)
        };

        _dbContext.Vouchers.AddRange(voucher100k, voucherVip);

        // 9. POS Session & Orders
        var posSession = new PosSession
        {
            Id = Guid.NewGuid(),
            CashierUserId = cashierUser.Id,
            OpeningBalance = 2000000m,
            ClosingBalance = 9990000m,
            CashSalesTotal = 7990000m,
            VietQrSalesTotal = 29890000m,
            CardSalesTotal = 0m,
            Status = PosSessionStatus.Closed,
            OpenedAt = now.AddHours(-10),
            ClosedAt = now.AddHours(-1),
            CreatedAt = now.AddHours(-10)
        };
        _dbContext.PosSessions.Add(posSession);

        // Order 1: Walk-in cash purchase of Sony headphone
        var order1 = new Order
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-202610-0001",
            CustomerId = custAn.Id,
            CashierUserId = cashierUser.Id,
            PosSessionId = posSession.Id,
            Status = OrderStatus.Completed,
            PaymentMethod = PaymentMethod.Cash,
            Subtotal = 7990000m,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            TotalAmount = 7990000m,
            PaidAt = now.AddHours(-5),
            CompletedAt = now.AddHours(-5),
            CreatedAt = now.AddHours(-5)
        };
        var orderItem1 = new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order1.Id,
            ProductId = prodSony.Id,
            VariantId = null,
            SerialImeiId = null,
            ProductName = prodSony.Name,
            VariantName = null,
            Sku = prodSony.Sku,
            UnitPrice = 7990000m,
            UnitCost = 6500000m,
            Quantity = 1,
            DiscountAmount = 0m,
            TotalPrice = 7990000m,
            CreatedAt = now.AddHours(-5)
        };
        _dbContext.Orders.Add(order1);
        _dbContext.OrderItems.Add(orderItem1);

        // Order 2: VietQR purchase of iPhone 15 Pro Max with voucher
        var order2 = new Order
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-202610-0002",
            CustomerId = custBich.Id,
            CashierUserId = cashierUser.Id,
            PosSessionId = posSession.Id,
            Status = OrderStatus.Completed,
            PaymentMethod = PaymentMethod.VietQr,
            Subtotal = 29990000m,
            DiscountAmount = 100000m,
            TaxAmount = 0m,
            TotalAmount = 29890000m,
            VoucherId = voucher100k.Id,
            PaidAt = now.AddHours(-2),
            CompletedAt = now.AddHours(-2),
            CreatedAt = now.AddHours(-2)
        };
        var orderItem2 = new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order2.Id,
            ProductId = prodIPhone.Id,
            VariantId = varIPhoneNatural.Id,
            SerialImeiId = serialSoldIPhone.Id,
            ProductName = prodIPhone.Name,
            VariantName = varIPhoneNatural.VariantName,
            Sku = varIPhoneNatural.Sku,
            UnitPrice = 29990000m,
            UnitCost = 26000000m,
            Quantity = 1,
            DiscountAmount = 100000m,
            TotalPrice = 29890000m,
            CreatedAt = now.AddHours(-2)
        };
        _dbContext.Orders.Add(order2);
        _dbContext.OrderItems.Add(orderItem2);

        // VietQR Transaction for Order 2
        var vietQrTx = new VietQrTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = order2.Id,
            PosSessionId = posSession.Id,
            TransactionCode = "TS20261002",
            Provider = "MBBank",
            ProviderTransactionId = "MB20261002001",
            BankBin = "970422",
            BankAccountNumber = "1234567890",
            Amount = 29890000m,
            QrContent = "00020101021238540010A00000072701240006970422011012345678900208QRIBFTTA53037045408298900005802VN62140810TS202610026304A1B2",
            Status = VietQrStatus.Confirmed,
            ExpiresAt = now.AddHours(-1),
            ConfirmedAt = now.AddHours(-2),
            ConfirmedByUserId = adminUser.Id,
            CreatedAt = now.AddHours(-2)
        };
        _dbContext.VietQrTransactions.Add(vietQrTx);

        // Save all seeded records
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
