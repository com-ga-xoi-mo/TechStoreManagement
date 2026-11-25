using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using TechStore.Shared.Enums;

namespace TechStore.Api.Analytics.Repositories;

public sealed partial class AnalyticsRepository
{
    private const string DeliveredSalesSql = """
        WITH delivered_orders AS (
            SELECT id, completed_at, cashier_user_id, total_amount, tax_amount
            FROM orders
            WHERE status IN ({2}, {3}, {4})
              AND completed_at >= {0}
              AND completed_at < {1}
        ),
        sale_lines AS (
            SELECT i.id, i.order_id, i.product_id, i.product_name, i.quantity, i.tax_amount,
                   i.total_price - i.order_discount_amount AS sales_excluding_tax,
                   i.unit_cost * i.quantity AS line_cost
            FROM order_items i
            INNER JOIN delivered_orders o ON o.id = i.order_id
        )
        """;

    private const string SalesPerOrderSql = """
        ,
        sales_per_order AS (
            SELECT order_id, SUM(quantity) AS quantity, SUM(sales_excluding_tax) AS sales_excluding_tax,
                   SUM(tax_amount) AS tax, SUM(line_cost) AS known_cost,
                   BOOL_OR(line_cost IS NULL) AS missing_cost
            FROM sale_lines
            GROUP BY order_id
        )
        """;

    public async Task<FinancialReportReadModel> GetFinancialReportAsync(
        FinancialReportCriteria criteria, CancellationToken cancellationToken = default)
    {
        var sales = await QueryDeliveredSales<FinancialSalesDayRow>(criteria, SalesPerOrderSql + """

            SELECT ((o.completed_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date AS "Date",
                   COUNT(*) AS "CompletedOrderCount",
                   COALESCE(SUM(s.quantity), 0)::bigint AS "Quantity",
                   COUNT(*) FILTER (WHERE COALESCE(s.tax, 0) <> o.tax_amount
                        OR COALESCE(s.sales_excluding_tax, 0) <> o.total_amount - o.tax_amount)
                        AS "AllocationMismatchOrderCount",
                   COALESCE(SUM(s.sales_excluding_tax), 0) AS "SalesExcludingTax",
                   COALESCE(SUM(s.tax), 0) AS "SalesTax",
                   CASE WHEN BOOL_OR(s.missing_cost) THEN NULL ELSE COALESCE(SUM(s.known_cost), 0) END
                        AS "CostOfGoodsSold"
            FROM delivered_orders o
            LEFT JOIN sales_per_order s ON s.order_id = o.id
            GROUP BY 1
            """).ToListAsync(cancellationToken);

        var receipts = await dbContext.Database.SqlQuery<FinancialReceiptsDayRow>($"""
            SELECT ((paid_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date AS "Date",
                   SUM(total_amount) AS "PaymentReceivedAmount"
            FROM orders
            WHERE paid_at >= {criteria.FromUtc} AND paid_at < {criteria.ToExclusiveUtc}
            GROUP BY 1
            """).ToListAsync(cancellationToken);

        var products = await QueryDeliveredSales<FinancialProductRow>(criteria, """
            ,
            product_totals AS (
                SELECT product_id, SUM(quantity) AS quantity, SUM(sales_excluding_tax) AS revenue
                FROM sale_lines
                GROUP BY product_id
            ),
            ranked_products AS (
                SELECT product_id, quantity, revenue,
                       ROW_NUMBER() OVER (ORDER BY quantity DESC, revenue DESC, product_id) AS quantity_rank,
                       ROW_NUMBER() OVER (ORDER BY revenue DESC, quantity DESC, product_id) AS revenue_rank
                FROM product_totals
            )
            SELECT r.product_id AS "ProductId", latest.product_name AS "ProductName",
                   r.quantity AS "Quantity", r.revenue AS "SalesExcludingTax",
                   r.quantity_rank AS "QuantityRank", r.revenue_rank AS "RevenueRank"
            FROM ranked_products r
            CROSS JOIN LATERAL (
                SELECT l.product_name
                FROM sale_lines l
                INNER JOIN delivered_orders o ON o.id = l.order_id
                WHERE l.product_id = r.product_id
                ORDER BY o.completed_at DESC, l.id DESC
                LIMIT 1
            ) latest
            WHERE r.quantity_rank <= {5} OR r.revenue_rank <= {5}
            """, FinancialReportReadModel.ProductRankingLimit).ToListAsync(cancellationToken);

        var cashiers = await QueryDeliveredSales<FinancialCashierRow>(criteria, SalesPerOrderSql + """

            SELECT o.cashier_user_id AS "CashierUserId",
                   CASE WHEN u.full_name IS NULL OR BTRIM(u.full_name) = ''
                        THEN o.cashier_user_id::text ELSE u.full_name END AS "CashierName",
                   COUNT(*) AS "CompletedOrderCount",
                   COALESCE(SUM(s.quantity), 0)::bigint AS "Quantity",
                   COALESCE(SUM(s.sales_excluding_tax), 0) AS "SalesExcludingTax",
                   CASE WHEN BOOL_OR(s.missing_cost) THEN NULL ELSE COALESCE(SUM(s.known_cost), 0) END
                        AS "CostOfGoodsSold"
            FROM delivered_orders o
            LEFT JOIN sales_per_order s ON s.order_id = o.id
            LEFT JOIN users u ON u.id = o.cashier_user_id
            GROUP BY o.cashier_user_id, u.full_name
            ORDER BY "SalesExcludingTax" DESC, "CompletedOrderCount" DESC, "CashierUserId"
            """).ToListAsync(cancellationToken);

        var afterSale = await ReadFinancialAfterSaleAsync(criteria, cancellationToken);
        return new(sales, receipts, products, cashiers, afterSale);
    }

    // SQL fragments are fixed repository statements. Dates, statuses and the optional rank limit are parameters.
    private IQueryable<T> QueryDeliveredSales<T>(FinancialReportCriteria criteria, string sql, params object[] extraParameters)
        => dbContext.Database.SqlQuery<T>(FormattableStringFactory.Create(DeliveredSalesSql + "\n" + sql,
            [criteria.FromUtc, criteria.ToExclusiveUtc, OrderStatus.Completed.ToString(),
             OrderStatus.PartiallyRefunded.ToString(), OrderStatus.Refunded.ToString(), .. extraParameters]));
}
