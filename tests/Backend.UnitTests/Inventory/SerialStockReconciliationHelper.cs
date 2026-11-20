using Microsoft.EntityFrameworkCore;
using Npgsql;
using TechStore.Api.Common.Data;

namespace TechStore.UnitTests.Inventory;

public static class SerialStockReconciliationHelper
{
    public const string ReconciliationSql = """
        SELECT
            s.id AS stock_id,
            s.product_id,
            s.variant_id,
            s.quantity AS stock_quantity,
            s.reserved_quantity AS stock_reserved_quantity,
            COALESCE(dev.in_stock_count, 0) + COALESCE(dev.reserved_count, 0) AS expected_quantity,
            COALESCE(dev.reserved_count, 0) AS expected_reserved_quantity
        FROM inventory_stocks s
        JOIN products p ON p.id = s.product_id
        LEFT JOIN (
            SELECT
                product_id,
                variant_id,
                COUNT(*) FILTER (WHERE status = 'InStock') AS in_stock_count,
                COUNT(*) FILTER (WHERE status = 'Reserved') AS reserved_count
            FROM serial_imeis
            GROUP BY product_id, variant_id
        ) dev ON dev.product_id = s.product_id AND dev.variant_id IS NOT DISTINCT FROM s.variant_id
        WHERE p.is_serial_tracked = true
          AND (
              s.quantity <> COALESCE(dev.in_stock_count, 0) + COALESCE(dev.reserved_count, 0)
              OR s.reserved_quantity <> COALESCE(dev.reserved_count, 0)
          );
        """;

    public static async Task AssertSerialStockReconciledAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var discrepancies = new List<string>();
        await using var conn = (NpgsqlConnection)context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(cancellationToken);
        }

        await using var cmd = new NpgsqlCommand(ReconciliationSql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var stockId = reader.GetGuid(0);
            var productId = reader.GetGuid(1);
            var variantId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
            var stockQty = reader.GetInt32(3);
            var stockReserved = reader.GetInt32(4);
            var expQty = reader.GetInt64(5);
            var expReserved = reader.GetInt64(6);

            discrepancies.Add(
                $"StockId={stockId}, ProductId={productId}, VariantId={variantId}: " +
                $"Stored(Quantity={stockQty}, Reserved={stockReserved}) vs " +
                $"Expected(Quantity={expQty}, Reserved={expReserved})");
        }

        if (discrepancies.Count > 0)
        {
            throw new InvalidOperationException(
                "Serial stock reconciliation check failed. Discrepancies detected:\n" +
                string.Join("\n", discrepancies));
        }
    }

    public static async Task AssertSerialStockReconciledAsync(PostgresDatabase database, CancellationToken cancellationToken = default)
    {
        await using var context = database.CreateContext();
        await AssertSerialStockReconciledAsync(context, cancellationToken);
    }
}
