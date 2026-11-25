using Microsoft.EntityFrameworkCore;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Api.Warranty.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Common.Data;

internal sealed partial class DemoSeedRepository
{
    internal const string AfterSaleOrderCode = "M4-202610-0001";

    private static Guid AfterSaleId(string key) => DemoSeedManifest.Id($"m4-october:{key}");

    private async Task EnsureAfterSaleDemoAsync(Product cable, Supplier supplier,
        (User Admin, User Cashier, User Warehouse) users, CancellationToken ct)
    {
        // The complete graph is inserted atomically under the seed lock. A later inspection or sale
        // is legitimate activity: never replace an existing document or reset this variant's stock.
        var prior = await dbContext.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.OrderCode == AfterSaleOrderCode, ct);
        if (prior != null)
        {
            if (prior.Id != AfterSaleId("sale"))
                throw Conflict("order", AfterSaleOrderCode, "order code is already in use");
            await EnsureDemoWarrantyCaseAsync(users.Admin, ct);
            return;
        }

        var sold = new DateTime(2026, 10, 25, 3, 0, 0, DateTimeKind.Utc);
        var received = sold.AddDays(-1);
        var refunded = sold.AddDays(1);
        var inspected = refunded.AddDays(1);
        var variant = new ProductVariant { Id = AfterSaleId("variant"), ProductId = cable.Id, Sku = "M4-CABLE-PACK",
            VariantName = "Bộ cáp USB-C 100W", Price = 1_000_000m, CreatedAt = received.AddDays(-1) };
        var stock = new InventoryStock { Id = AfterSaleId("stock"), ProductId = cable.Id, VariantId = variant.Id,
            Quantity = 3, MinStockAlert = 1, CreatedAt = received, UpdatedAt = inspected };
        var purchase = new PurchaseOrder { Id = AfterSaleId("purchase"), PoNumber = "PO-M4-202610-0001", SupplierId = supplier.Id,
            CreatedByUserId = users.Warehouse.Id, ReceivedByUserId = users.Warehouse.Id, Status = PurchaseOrderStatus.Received,
            TotalCost = 2_400_000m, CreatedAt = received.AddHours(-1), OrderedAt = received.AddHours(-1), ReceivedAt = received };
        var purchaseLine = new PurchaseOrderItem { Id = AfterSaleId("purchase-line"), PurchaseOrderId = purchase.Id,
            ProductId = cable.Id, VariantId = variant.Id, UnitCost = 600_000m, OrderedQuantity = 4, ReceivedQuantity = 4, CreatedAt = received };
        var shift = new PosSession { Id = AfterSaleId("shift"), CashierUserId = users.Cashier.Id, ClosedByUserId = users.Cashier.Id,
            OpenedAt = sold.AddHours(-1), ClosedAt = sold.AddHours(7), CreatedAt = sold.AddHours(-1), Status = PosSessionStatus.Closed,
            OpeningBalance = 100_000m, CashSalesTotal = 1_980_000m, ExpectedClosingBalance = 2_080_000m, ClosingBalance = 2_080_000m };
        var order = new Order { Id = AfterSaleId("sale"), OrderCode = AfterSaleOrderCode, CashierUserId = users.Cashier.Id,
            PosSessionId = shift.Id, Status = OrderStatus.Refunded, PaymentMethod = PaymentMethod.Cash, CheckoutKey = AfterSaleId("checkout"),
            Subtotal = 2_000_000m, DiscountAmount = 200_000m, TaxRate = 10m, TaxAmount = 180_000m, TotalAmount = 1_980_000m,
            CashTendered = 2_000_000m, ChangeGiven = 20_000m, PaidAt = sold, CompletedAt = sold, CreatedAt = sold, UpdatedAt = refunded };
        var item = new OrderItem { Id = AfterSaleId("sale-line"), OrderId = order.Id, ProductId = cable.Id, VariantId = variant.Id,
            ProductName = cable.Name, VariantName = variant.VariantName, Sku = variant.Sku, UnitPrice = variant.Price,
            UnitCost = purchaseLine.UnitCost, Quantity = 2, TotalPrice = 2_000_000m, OrderDiscountAmount = order.DiscountAmount,
            TaxAmount = order.TaxAmount, CreatedAt = sold };
        dbContext.AddRange(variant, stock, purchase, purchaseLine, shift, order, item,
            new InventoryReservation { OrderItemId = item.Id, Quantity = item.Quantity, State = ReservationState.Sold });

        var refund = new ReturnCase { Id = AfterSaleId("refund"), OrderId = order.Id, RequestedById = users.Cashier.Id,
            PayoutActorId = users.Admin.Id, Reason = "Khách trả hai bộ cáp", Status = ReturnStatus.Refunded,
            Method = "VietQr", BankReference = "DEMO-M4-20261026-01", RefundedAt = refunded, CreatedAt = refunded.AddHours(-1) };
        var claim = new ReturnCaseLine { Id = AfterSaleId("claim-line"), CaseId = refund.Id, OrderItemId = item.Id,
            Quantity = 2, NetAmount = 1_800_000m, DiscountAmount = 200_000m, TaxAmount = 180_000m, CreatedAt = refund.CreatedAt };
        var inspectionId = AfterSaleId("inspection");
        var returned = new OrderReturn[2];
        for (var i = 0; i < returned.Length; i++)
        {
            returned[i] = new OrderReturn { Id = AfterSaleId($"settled-slice-{i}"), OrderId = order.Id, OrderItemId = item.Id,
                CaseLineId = claim.Id, ProcessedByUserId = users.Admin.Id, Reason = refund.Reason, Quantity = 1,
                NetAmount = 900_000m, DiscountAmount = 100_000m, TaxAmount = 90_000m, RefundAmount = 990_000m,
                RefundMethod = PaymentMethod.VietQr, RefundedAt = refunded, CreatedAt = refunded,
                IsRestocked = i == 0, Disposition = i == 0 ? "InStock" : "Pending",
                InspectionId = i == 0 ? inspectionId : null, InspectedAt = i == 0 ? inspected : null };
        }
        dbContext.AddRange(refund, claim);
        dbContext.OrderReturns.AddRange(returned);

        var receipt = new InventoryMovement { Id = AfterSaleId("receipt"), InventoryStockId = stock.Id,
            PerformedByUserId = users.Warehouse.Id, MovementType = InventoryMovementType.PurchaseReceipt, QuantityChange = 4,
            QuantityAfter = 4, PurchaseOrderItemId = purchaseLine.Id, CreatedAt = received };
        var sale = new InventoryMovement { Id = AfterSaleId("sale-movement"), InventoryStockId = stock.Id,
            PerformedByUserId = users.Cashier.Id, MovementType = InventoryMovementType.Sale, QuantityChange = -2,
            QuantityAfter = 2, OrderItemId = item.Id, CreatedAt = sold };
        var putBack = new InventoryMovement { Id = AfterSaleId("restock"), InventoryStockId = stock.Id,
            PerformedByUserId = users.Warehouse.Id, MovementType = InventoryMovementType.CustomerReturn, QuantityChange = 1,
            QuantityAfter = 3, OrderReturnId = returned[0].Id, CreatedAt = inspected };
        dbContext.InventoryMovements.AddRange(receipt, sale, putBack);
        var units = new FulfillmentLine(item.Id, item.ProductId, item.VariantId, null, item.Quantity);
        var receiveLog = InventoryOperationLog.OrderLines(refund.Id, false, order.Id, [units], refunded);
        var inspectionLog = InventoryOperationLog.Inspection(inspectionId, returned[0].Id, units with { Quantity = 1 },
            ReturnDisposition.Restock, users.Warehouse.Id, inspected);
        inspectionLog.MovementId = putBack.Id;
        inspectionLog.InspectionReturnId = returned[0].Id;
        dbContext.AddRange(receiveLog, inspectionLog);

        await EnsureDemoWarrantyCaseAsync(users.Admin, ct);
    }

    private async Task EnsureDemoWarrantyCaseAsync(User handler, CancellationToken ct)
    {
        // Old M4 databases already contain the operation pair. Attach the typed case without
        // re-performing either action or touching current ownership of a subsequently resold device.
        var caseId = AfterSaleId("warranty-case");
        var opened = new DateTime(2026, 10, 26, 3, 0, 0, DateTimeKind.Utc);
        var closed = opened.AddDays(1);
        var oldSale = dbContext.Orders.Local.Single(o => o.OrderCode == "DEMO-202610-0001");
        var contact = dbContext.Customers.Local.Single(c => c.Id == oldSale.CustomerId);
        const string issue = "Thiết bị hoạt động không ổn định";
        var existing = await dbContext.Set<WarrantyCase>().SingleOrDefaultAsync(c => c.Id == caseId, ct);
        if (existing != null)
        {
            existing.IssueDescription ??= issue;
            existing.ContactName ??= contact.FullName;
            existing.ContactPhone ??= contact.Phone;
            return;
        }
        var oldItem = dbContext.OrderItems.Local.Single(i => i.OrderId == oldSale.Id);
        var oldDevice = oldItem.SerialImeiId!.Value;
        var receiveId = AfterSaleId("repair");
        var returnId = AfterSaleId("return-to-owner");
        var operations = dbContext.Set<InventoryOperation>();
        async Task<Guid> EnsureOperation(Guid id, WarrantyAction action, DateTime at)
        {
            var prior = await operations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, ct);
            if (prior != null)
            {
                using var payload = System.Text.Json.JsonDocument.Parse(prior.Payload);
                return payload.RootElement.GetProperty("actorId").GetGuid();
            }
            dbContext.Add(InventoryOperationLog.Warranty(id, caseId, oldSale.Id, oldItem.Id, oldDevice, action, null, handler.Id, at));
            return handler.Id;
        }
        var openedBy = await EnsureOperation(receiveId, WarrantyAction.ReceiveForRepair, opened);
        var closedBy = await EnsureOperation(returnId, WarrantyAction.ReturnToCustomer, closed);
        var originalWarranty = oldSale.CompletedAt!.Value;
        var claim = new WarrantyCase
        {
            Id = caseId, CaseCode = "BH-202610-0001", OrderId = oldSale.Id, OrderItemId = oldItem.Id, CustomerId = oldSale.CustomerId,
            DeviceId = oldDevice, OpenedById = openedBy, OpenedAt = opened, WarrantyStartAt = originalWarranty,
            WarrantyEndAt = originalWarranty.AddMonths(12), ClosedById = closedBy, ClosedAt = closed,
            IssueDescription = issue, ContactName = contact.FullName, ContactPhone = contact.Phone,
            Status = WarrantyCaseStatus.ReturnedToCustomer, ReceiveOperationId = receiveId, ResolutionOperationId = returnId
        };
        claim.Events.Add(new WarrantyCaseEvent
        {
            CaseId = caseId, ActorId = openedBy, CreatedAt = opened, ToStatus = WarrantyCaseStatus.Received
        });
        claim.Events.Add(new WarrantyCaseEvent
        {
            CaseId = caseId, ActorId = closedBy, CreatedAt = closed,
            FromStatus = WarrantyCaseStatus.Received, ToStatus = WarrantyCaseStatus.ReturnedToCustomer
        });
        dbContext.Add(claim);
    }
}
