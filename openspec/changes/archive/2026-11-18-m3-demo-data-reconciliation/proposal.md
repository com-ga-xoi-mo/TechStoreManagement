# Proposal

## Why

The M3 demo seed already populates categories, orders, and unit costs, but lacks openable product images. Binh needs a hand-calculated reference dataset verified via the Analytics API to distinguish fixed fixture results from live data on the demo date.

## What Changes

- Confirm at least 20 products with cost prices, 5 categories, 10 customers, 20 orders across lifecycle states, and four demo accounts (`admin`, `manager1`, `cashier1`, `warehouse1`). Keep 50 IMEIs in the current fixture as a regression benchmark.
- Add cover images for seeded SKUs missing images. The team generates placeholder category images and bundles sample assets in the repository. `DataSeeder` uploads images via the configured `IObjectStorage` provider and stores references in `product_images`. Local storage is the default demo path; document R2 setup separately alongside matching demo database and storage configuration.
- Expand existing PostgreSQL seeding tests to verify fresh DBs, reruns, partial seeds, and legacy DBs. Verify user records, natural keys, timestamps, rollbacks, and linkage across orders, VietQR, stock, IMEIs, and shifts.
- Reconcile four Analytics periods, including the existing `custom` date range, against an independently calculated Expected/Actual table anchored at `2026-10-18T05:00:00Z`. Add demo instructions for reading the live snapshot for the historical period on the seeded DB.
- Explicitly note that zero tax/discount values are properties of this fixture. Verify Vy's pricing formulas on real POS orders in `m3-dashboard-live-flow`.

## Capabilities

### New Capabilities

No new capabilities.

### Modified Capabilities

- `data-seeding`: Add seeded demo images via storage, verify cost prices and seeded data, update the role of the 50-IMEI benchmark, and reconcile Analytics against the existing contract.
- `frontend-dashboard-preview`: Align seed target descriptions in the Dashboard handoff with the M3 plan and clarify that 50 IMEIs is the current fixture count.

## Impact

- Application changes focus on seed images in `backend/Common/Data`, existing Catalog/storage ports, and sample assets in `backend`. Keep the existing manifest append-only.
- Expand `DemoSeed*Tests`, `DataSeederTests`, and `DataSeederPostgresIntegrationTests`. Acceptance uses isolated PostgreSQL and Local storage, with instructions for R2 testing when a test bucket is available.
- Handoff outputs include the reconciliation table, rerun commands, and demo instructions. Existing Analytics KPI calculations and API/DTO contracts remain unchanged.
