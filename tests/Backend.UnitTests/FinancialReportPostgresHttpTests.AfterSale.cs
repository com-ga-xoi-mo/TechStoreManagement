using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Analytics;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public sealed partial class FinancialReportPostgresHttpTests
{
    [PostgresFact]
    public async Task Report_LegacyAndUnverifiedEvents_MarkOnlyDependentAmountsUnknownAndExcludeAsOf()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        var asOf = Utc(14, 9);
        Guid shiftId;
        await using (var db = api.Database.CreateContext())
        {
            var actor = await db.Users.SingleAsync(user => user.Username == "cashier1");
            var product = AddProducts(db, 1)[0];
            var stock = new InventoryStock { Product = product, Quantity = 3 };
            var shift = new PosSession
            {
                CashierUserId = actor.Id, OpeningBalance = 120, Status = PosSessionStatus.Closed,
                OpenedAt = Utc(12, 1), ClosedAt = Utc(12, 3), ClosedByUserId = actor.Id
            };
            shiftId = shift.Id;
            db.AddRange(stock, shift);
            var legacy = Refund(Utc(12, 2), null, null, 25, PaymentMethod.Cash, 80);
            legacy.RefundPosSessionId = shift.Id;
            legacy.IsRestocked = true; // no movement
            Restock(Refund(Utc(10, 6), 100, 5, null, PaymentMethod.Card, 105), Utc(13, 2));
            Restock(Refund(Utc(10, 6), 100, 5, 0, PaymentMethod.Card, 105), Utc(14, 2));
            Restock(Refund(asOf, 100, 5, 25, PaymentMethod.Card, 105), asOf);

            Qr(55, Utc(13, 7), Utc(12, 7));
            Qr(30, Utc(13, 8), null);
            Qr(90, asOf, asOf);
            await db.SaveChangesAsync();

            OrderReturn Refund(DateTime time, decimal? amount, decimal? tax, decimal? cost, PaymentMethod method, decimal paid)
            {
                var item = Line(product, 1, 120, cost: cost);
                var sale = Sale(actor, OrderStatus.Refunded, Utc(10, 2), Utc(10, 2), 120, 0, item);
                var returned = new OrderReturn
                {
                    Order = sale, OrderItem = item, ProcessedByUserId = actor.Id, Quantity = 1,
                    Reason = "Fixture", RefundedAt = time, RefundMethod = method,
                    NetAmount = amount, TaxAmount = tax, RefundAmount = paid
                };
                db.Add(returned);
                return returned;
            }

            void Restock(OrderReturn returned, DateTime time) => db.InventoryMovements.Add(new()
            {
                InventoryStock = stock, OrderReturnId = returned.Id, MovementType = InventoryMovementType.CustomerReturn,
                QuantityChange = 1, QuantityAfter = 1, PerformedByUserId = actor.Id, CreatedAt = time
            });

            void Qr(decimal amount, DateTime paidAt, DateTime? confirmedAt)
            {
                var order = Sale(actor, OrderStatus.Processing, null, paidAt, amount, 0, Line(product, 1, amount));
                order.PaymentMethod = PaymentMethod.VietQr;
                db.VietQrTransactions.Add(new()
                {
                    Order = order, TransactionCode = $"FR-{Guid.NewGuid():N}", Amount = amount,
                    BankBin = "970422", BankAccountNumber = "1234567890", QrContent = "test",
                    CreatedAt = Utc(12, 6), ExpiresAt = paidAt.AddHours(1),
                    Status = confirmedAt.HasValue ? VietQrStatus.Confirmed : VietQrStatus.Pending,
                    ConfirmedAt = confirmedAt, ConfirmedByUserId = confirmedAt.HasValue ? actor.Id : null
                });
            }
        }

        using var factory = WithClock(api, new ReportClock(asOf));
        using var client = factory.CreateClient(new() { BaseAddress = new("https://localhost") });
        await IdentityPostgresApi.LoginAsync(client);
        using var response = await client.GetAsync(Route + FixtureRange);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var report = json.RootElement.Deserialize<FinancialReportResponse>(JsonSerializerOptions.Web)!;
        var days = report.Daily.Select(day => day.Totals).ToArray();
        Assert.Equal(asOf, report.AsOf);
        Assert.Equal(80m, days[0].RefundPaidAmount);
        Assert.Null(days[0].RefundTaxAmount);
        Assert.Null(days[0].RefundedSalesExcludingTax);
        Assert.Null(days[0].NetSalesExcludingTax);
        Assert.Null(days[0].CashRefundPaidAmount);
        Assert.Equal(0m, days[0].RestockedCost);
        Assert.Equal(55m, days[0].VietQrReceivedAmount);
        Assert.Equal(0m, days[0].PaymentReceivedAmount);
        Assert.Equal(85m, days[1].PaymentReceivedAmount);
        Assert.Null(days[1].VietQrReceivedAmount);
        Assert.Null(days[1].RestockedCost);
        Assert.Null(days[1].NetCostOfGoodsSold);
        Assert.Null(days[1].NetGrossProfit);
        Assert.Equal(0m, days[2].RefundPaidAmount);
        Assert.Equal(0m, days[2].VietQrReceivedAmount);
        Assert.Equal(0m, days[2].RestockedCost);
        Assert.Equal(0m, days[2].NetGrossProfit);

        var cash = Assert.Single(report.CashByShift);
        Assert.Equal(shiftId, cash.PosSessionId);
        Assert.Equal(0m, cash.CashReceivedAmount);
        Assert.Null(cash.CashRefundPaidAmount);
        Assert.Null(cash.CashNetMovement);
        Assert.Equal(80m, report.Summary.RefundPaidAmount);
        Assert.Equal(0m, report.Summary.VietQrRefundPaidAmount);
        var summary = json.RootElement.GetProperty("summary");
        foreach (var field in new[] { "refundTaxAmount", "cashRefundPaidAmount", "vietQrReceivedAmount",
                     "restockedCost", "netSalesExcludingTax", "netCostOfGoodsSold", "netGrossProfit" })
            Assert.Equal(JsonValueKind.Null, summary.GetProperty(field).ValueKind);
    }
}
