using Microsoft.EntityFrameworkCore;

namespace TechStore.Api.Analytics.Repositories;

public sealed partial class AnalyticsRepository
{
    private async Task<FinancialAfterSaleReadModel> ReadFinancialAfterSaleAsync(
        FinancialReportCriteria criteria, CancellationToken cancellationToken)
    {
        var from = criteria.FromUtc;
        var to = criteria.ToExclusiveUtc;
        var refunds = await dbContext.Database.SqlQuery<FinancialRefundDayRow>($"""
            WITH settled AS (
                SELECT r.refunded_at, r.refund_amount, r.tax_amount, r.net_amount,
                       o.completed_at <= r.refunded_at
                           AND NOT COALESCE(c.was_undelivered, false)
                           AND r.disposition IS DISTINCT FROM 'Undelivered' AS delivered,
                       r.refund_method = 'VietQr' AS is_qr,
                       c.method IS NOT DISTINCT FROM 'VietQr' AND c.refunded_at IS NOT DISTINCT FROM r.refunded_at
                           AND NULLIF(BTRIM(c.bank_reference), '') IS NOT NULL AS qr_verified
                FROM order_returns r
                JOIN orders o ON o.id = r.order_id
                LEFT JOIN return_case_lines l ON l.id = r.case_line_id
                LEFT JOIN return_cases c ON c.id = l.case_id
                WHERE r.refunded_at >= {from} AND r.refunded_at < {to}
                  AND (r.case_line_id IS NULL OR c.status = 'Refunded')
            )
            SELECT ((refunded_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date AS "Date",
                   SUM(refund_amount) AS "RefundPaidAmount",
                   CASE WHEN BOOL_OR(tax_amount IS NULL) THEN NULL ELSE SUM(tax_amount) END AS "RefundTaxAmount",
                   CASE WHEN BOOL_OR(delivered AND net_amount IS NULL) THEN NULL
                        ELSE COALESCE(SUM(net_amount) FILTER (WHERE delivered), 0) END AS "RefundedSalesExcludingTax",
                   CASE WHEN BOOL_OR(is_qr AND NOT qr_verified) THEN NULL
                        ELSE COALESCE(SUM(refund_amount) FILTER (WHERE is_qr), 0) END AS "VietQrRefundPaidAmount"
            FROM settled
            GROUP BY 1
            """).ToListAsync(cancellationToken);

        var restocks = await dbContext.Database.SqlQuery<FinancialRestockDayRow>($"""
            SELECT ((m.created_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date AS "Date",
                   CASE WHEN BOOL_OR(i.unit_cost IS NULL) THEN NULL
                        ELSE SUM(m.quantity_change * i.unit_cost) END AS "RestockedCost"
            FROM inventory_movements m
            JOIN order_returns r ON r.id = m.order_return_id
            JOIN order_items i ON i.id = r.order_item_id AND i.order_id = r.order_id
            JOIN orders o ON o.id = i.order_id
            LEFT JOIN return_case_lines l ON l.id = r.case_line_id
            LEFT JOIN return_cases c ON c.id = l.case_id
            WHERE m.created_at >= {from} AND m.created_at < {to}
              AND m.movement_type = 'CustomerReturn' AND m.quantity_change > 0
              AND o.completed_at <= r.refunded_at AND NOT COALESCE(c.was_undelivered, false)
              AND r.disposition IS DISTINCT FROM 'Undelivered'
              AND (r.case_line_id IS NULL OR c.status = 'Refunded')
            GROUP BY 1
            """).ToListAsync(cancellationToken);

        var warranty = await dbContext.Database.SqlQuery<FinancialWarrantyDayRow>($"""
            SELECT ((m.created_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date AS "Date",
                   CASE WHEN BOOL_OR(w.replacement_unit_cost IS NULL) THEN NULL
                        ELSE SUM(ABS(m.quantity_change::bigint) * w.replacement_unit_cost) END AS "WarrantyCost"
            FROM inventory_movements m
            JOIN warranty_cases w ON w.movement_id = m.id AND w.order_item_id = m.order_item_id
            WHERE m.created_at >= {from} AND m.created_at < {to}
              AND m.movement_type = 'WarrantyReplacement' AND m.quantity_change < 0
              AND w.status = 'Replaced'
            GROUP BY 1
            """).ToListAsync(cancellationToken);

        var cash = await ReadFinancialCashAsync(criteria, cancellationToken);
        var qrReceipts = await dbContext.Database.SqlQuery<FinancialQrReceiptDayRow>($"""
            WITH verified AS NOT MATERIALIZED (
                SELECT q.order_id, q.confirmed_at, q.amount
                FROM vietqr_transactions q
                JOIN orders o ON o.id = q.order_id
                WHERE q.status = 'Confirmed' AND q.confirmed_at IS NOT NULL
                  AND o.payment_method = 'VietQr' AND o.total_amount = q.amount
                  AND q.pos_session_id IS NOT DISTINCT FROM o.pos_session_id
                  AND (q.confirmed_by_user_id IS NOT NULL OR
                       (NULLIF(BTRIM(q.provider), '') IS NOT NULL AND NULLIF(BTRIM(q.provider_transaction_id), '') IS NOT NULL))
            ), events AS (
                SELECT confirmed_at AS occurred_at, amount
                FROM verified
                WHERE confirmed_at >= {from} AND confirmed_at < {to}
                UNION ALL
                SELECT o.paid_at, NULL::numeric
                FROM orders o
                WHERE o.payment_method = 'VietQr' AND o.paid_at >= {from} AND o.paid_at < {to}
                  AND NOT EXISTS (SELECT 1 FROM verified q WHERE q.order_id = o.id)
            )
            SELECT ((occurred_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date AS "Date",
                   CASE WHEN BOOL_OR(amount IS NULL) THEN NULL ELSE SUM(amount) END AS "VietQrReceivedAmount"
            FROM events
            GROUP BY 1
            """).ToListAsync(cancellationToken);

        return new(refunds, restocks, warranty, cash, qrReceipts);
    }

    private Task<List<FinancialCashDayRow>> ReadFinancialCashAsync(
        FinancialReportCriteria criteria, CancellationToken cancellationToken)
    {
        var from = criteria.FromUtc;
        var to = criteria.ToExclusiveUtc;
        // A payout is a case-level event; inspections can split its settlement rows later.
        // Evaluate all rows of each candidate case before producing a single cash event.
        return dbContext.Database.SqlQuery<FinancialCashDayRow>($"""
            WITH payouts AS (
                SELECT c.id, c.refunded_at, p.shift_id, p.amount,
                       s.amount IS NOT DISTINCT FROM p.amount AND s.matches AS verified
                FROM return_cases c
                JOIN cash_refund_payouts p ON p.refund_id = c.id
                CROSS JOIN LATERAL (
                    SELECT SUM(r.refund_amount) AS amount,
                           COALESCE(BOOL_AND(r.refund_method = 'Cash'
                               AND r.refund_pos_session_id IS NOT DISTINCT FROM p.shift_id
                               AND r.refunded_at = c.refunded_at), false) AS matches
                    FROM return_case_lines l
                    JOIN order_returns r ON r.case_line_id = l.id
                    WHERE l.case_id = c.id
                ) s
                WHERE c.status = 'Refunded' AND c.method = 'Cash'
                  AND c.refunded_at >= {from} AND c.refunded_at < {to}
            ), events AS (
                SELECT o.paid_at AS occurred_at, o.pos_session_id AS shift_id,
                       o.total_amount AS received, 0::numeric AS paid
                FROM orders o
                WHERE o.payment_method = 'Cash' AND o.pos_session_id IS NOT NULL
                  AND o.paid_at >= {from} AND o.paid_at < {to}
                UNION ALL
                SELECT refunded_at, shift_id, 0::numeric, CASE WHEN verified THEN amount ELSE NULL END
                FROM payouts
                UNION ALL
                SELECT r.refunded_at, r.refund_pos_session_id, 0::numeric, NULL::numeric
                FROM order_returns r
                LEFT JOIN return_case_lines l ON l.id = r.case_line_id
                LEFT JOIN return_cases c ON c.id = l.case_id
                WHERE r.refund_method = 'Cash' AND r.refunded_at >= {from} AND r.refunded_at < {to}
                  AND (r.case_line_id IS NULL OR c.status = 'Refunded')
                  AND NOT EXISTS (SELECT 1 FROM payouts p WHERE p.id = c.id AND p.verified)
            )
            SELECT ((occurred_at AT TIME ZONE 'UTC') + INTERVAL '7 hours')::date AS "Date",
                   shift_id AS "PosSessionId", SUM(received) AS "CashReceivedAmount",
                   CASE WHEN BOOL_OR(paid IS NULL) THEN NULL ELSE SUM(paid) END AS "CashRefundPaidAmount"
            FROM events
            GROUP BY 1, 2
            """).ToListAsync(cancellationToken);
    }
}
