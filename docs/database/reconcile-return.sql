-- Run with psql -v order_code='M4-202610-0001' -f docs/database/reconcile-return.sql
-- Internal report. The execution role needs read access to the sale, refunds and stock movements.
WITH sale AS (
    SELECT id, completed_at, subtotal, discount_amount, tax_amount FROM orders WHERE order_code = :'order_code'
), events AS (
    SELECT s.completed_at AS event_at,
           s.subtotal - s.discount_amount AS net_merchandise, s.tax_amount AS tax,
           CASE WHEN count(i.id) > 0 AND count(i.unit_cost) = count(i.id)
                THEN sum(i.quantity * i.unit_cost) END AS cost
    FROM sale s LEFT JOIN order_items i ON i.order_id = s.id
    WHERE s.completed_at IS NOT NULL
    GROUP BY s.id, s.completed_at, s.subtotal, s.discount_amount, s.tax_amount
    UNION ALL
    SELECT r.refunded_at, -r.net_amount, -r.tax_amount, 0
    FROM sale s JOIN order_returns r ON r.order_id = s.id
    WHERE s.completed_at IS NOT NULL
    UNION ALL
    SELECT m.created_at, 0, 0, -m.quantity_change * i.unit_cost
    FROM sale s
    JOIN order_returns r ON r.order_id = s.id
    JOIN inventory_movements m ON m.order_return_id = r.id AND m.movement_type = 'CustomerReturn'
    JOIN order_items i ON i.id = r.order_item_id
), daily AS (
    SELECT (event_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS day,
           sum(net_merchandise) AS net, sum(tax) AS tax, sum(cost) AS cost,
           count(*) FILTER (WHERE net_merchandise IS NULL OR tax IS NULL) AS missing_allocation,
           count(*) FILTER (WHERE cost IS NULL) AS missing_cost
    FROM events GROUP BY 1
), cumulative AS (
    SELECT day, sum(net) OVER w AS net, sum(tax) OVER w AS tax, sum(cost) OVER w AS cost,
           sum(missing_allocation) OVER w AS missing_allocation, sum(missing_cost) OVER w AS missing_cost
    FROM daily WINDOW w AS (ORDER BY day ROWS UNBOUNDED PRECEDING)
)
SELECT day,
       CASE WHEN missing_allocation = 0 THEN net END AS net_merchandise,
       CASE WHEN missing_allocation = 0 THEN tax END AS net_tax,
       CASE WHEN missing_cost = 0 THEN cost END AS net_cost,
       CASE WHEN missing_allocation + missing_cost = 0 THEN net - cost END AS gross_profit,
       missing_allocation, missing_cost
FROM cumulative ORDER BY day;
