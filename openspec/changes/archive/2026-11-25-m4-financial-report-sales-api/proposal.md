# Proposal

## Why

M4 Sprint 1 needs a management reporting API so Bình can reconcile delivered sales, cost of goods sold, and cashier performance against recorded transactions. The API contract and sample JSON let T. Vinh start integrating before change 2 adds refunds, restocks, and warranty costs.

## What Changes

- Add `GET /api/analytics/financial-report` for Admin and Manager. It accepts a `custom` range of Vietnam calendar dates and returns period totals, daily totals, two product rankings, and cashier performance.
- Recognize original sales by `CompletedAt` and retain them when an order becomes `PartiallyRefunded` or `Refunded`. Report sales excluding tax, sales tax, and receipts based on `PaidAt` separately. Missing costs propagate to dependent metrics.
- Publish a DTO with nullable fields for refunds, restocked costs, post-refund profit, and warranty costs. Preserve the M3 Dashboard contract and KPIs.
- Verify against hand-calculated expectations on PostgreSQL and test HTTP authorization with JWTs. Record the results, build SHA, and data limitations in `docs/M4_FINANCIAL_REPORT.md`.

## Capabilities

### New Capabilities

### Modified Capabilities

- `admin-analytics-api` adds the financial report contract and sales calculation rules.
- `api-role-authorization` grants Admin and Manager store-wide financial report access.

## Impact

- Implementation covers `backend/Analytics/`, the report tests, `ApiAuthorizationCoverageTests`, the new route entry in `docs/API_PERMISSIONS.md`, and Bình’s verification document. The new `shared/DTOs/FinancialReportDto.cs` defines the shared desktop contract.
- Use persisted order and line-item snapshots. Pricing formulas, UI, Gemini, sales, refund and warranty workflows, shift ledger reconciliation, and database schema changes are out of scope.
