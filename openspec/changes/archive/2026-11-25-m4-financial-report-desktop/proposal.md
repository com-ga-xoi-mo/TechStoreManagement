# Proposal

## Why

Admin and Manager users need the desktop financial report to reconcile sales, refunds, tax, receipts, payouts, and costs from a single API snapshot. The existing Overview provides an entry point that preserves the M3 Dashboard workflow.

## What Changes

- Place the Overview (“Tổng quan”) and Financial Report (“Báo cáo tài chính”) tabs at the top of `DashboardPage` for signed-in Admin and Manager users in live mode. Overview is the default tab.
- Show the report period, update time in UTC+07, period totals, daily figures, product rankings, and cashier performance. Use API values for sales, refunds, tax, cash flow, costs, gross profit, and warranty, with distinct displays for zero, unavailable amounts, empty periods, and load errors.
- Keep draft dates separate from the applied range. Let users replace an in-flight request with a new range, retry the applied range, and discard stale results when changing sessions or leaving the page.
- Run headless tests with the sales DTO and sample JSON, and verify live against a build that includes change 2. Record Skia and native Windows evidence separately in `docs/M4_FINANCIAL_REPORT.md`.

## Capabilities

### New Capabilities

### Modified Capabilities

- `frontend-dashboard-preview` adds a live report view with loading, empty, and error states and display rules within the existing Overview.

## Impact

- Implementation covers the Dashboard view and ViewModel, dedicated Dashboard and Analytics models, repository and DI registration, Bình’s tests, and `docs/M4_FINANCIAL_REPORT.md`.
- Reuse `GET /api/analytics/financial-report`, the sales contract, and the fields added in change 2. The M3 Dashboard, Shell menu and routing, server authorization, API, and shared DTO contracts remain in place.
