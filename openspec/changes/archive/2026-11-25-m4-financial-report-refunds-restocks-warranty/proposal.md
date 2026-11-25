# Proposal

## Why

Bình’s financial report already includes sales figures and fields reserved for after-sales data. M4 needs to include settled refund payouts, items actually restocked, and replacement device costs so net sales, net costs, gross profit, and cash drawer movements can be reconciled against stored records.

## What Changes

- Populate the refund, restock, warranty, and net amount fields in `GET /api/analytics/financial-report`. Date refunds by actual payout. Orders paid and refunded before delivery affect cash flow only.
- Reverse costs on the date items return to sellable stock. Report warranty replacement device costs separately. Preserve original sales, access rules, report periods, and the current Dashboard KPIs.
- Add refunded tax and cash receipts and payouts by shift. Keep QR flows separate from the cash drawer. Preserve existing field names, types, and structure.
- Compare hand-calculated expectations with HTTP results on PostgreSQL. Append results to `docs/M4_FINANCIAL_REPORT.md`, including the owner and rerun conditions for unverified items.

## Capabilities

### New Capabilities

### Modified Capabilities

- `admin-analytics-api` extends financial reporting with after-sales figures and cash flow by channel and shift. It replaces the rule that after-sales fields are always unavailable.

## Impact

- Implementation is limited to `backend/Analytics/`, Bình’s report tests, and `docs/M4_FINANCIAL_REPORT.md`. Read Orders, Inventory, and Sales data from the same snapshot.
- Refund, inventory, warranty, and payment recording workflows, selling prices, points and vouchers, migrations, UI, and Gemini are out of scope. End-to-end QR reconciliation depends on Đạt’s payment confirmation flow.
