# Design

## Context

The Manager Dashboard and `custom` date range were verified in `m3-manager-dashboard-access`. `m3-demo-data-reconciliation` reconciled KPIs using a fixed fixture. POS Cash and shifts already exist in `backend/Sales/PosController.cs`. `PosCheckoutPostgresHttpTests` already runs shift open, Cash sale, shift close, and inventory checks over real HTTP on PostgreSQL. That fixture uses `IdentityPostgresApi` to create an isolated test database and perform real logins.

The baseline for apply is Binh's branch after committing the two previous changes (`m3-manager-dashboard-access` and `m3-demo-data-reconciliation`) and merging the latest `origin/dev`. The table below describes `origin/dev` at survey time. This design does not pin a fixed SHA because branches are still evolving; when applying, record the SHA of the actual build under test and re-verify each dependency on that build.

| Step | Status on `origin/dev` at survey time | Handoff / handling |
| --- | --- | --- |
| Manager reads Dashboard | Available once Binh's branch (the two prior changes) is merged with `dev` | Binh preserves the `admin-analytics-api` contract and integrates onto the same build with POS. Reuse authorization evidence from the prior change. |
| Suppliers, purchase receipts, IMEI intake | Blocking the end-to-end flow. `dev` does not yet have the receiving controller. Owner branch `origin/feat/inventory-purchase-receipts` has `SuppliersController` and `PurchaseReceiptsController`; `POST /api/inventory/purchase-receipts` creates a `Received` receipt and records stock intake in the same transaction. | Q. Vinh merges receiving into `dev` along with migrations and the intake contract, including document metadata, IMEI, unit cost, and ledger source linkage. Binh only uses the contract on the merged build and does not pull code from unmerged branches. |
| SKU/IMEI lookup, open shift, checkout, close shift | Product search API, serial lookup, and POS routes exist. Checkout accepts an optional `customerId`. | T. Vinh owns product lookup / search UI. Dat owns POS/shifts, wiring lookup results, customer selection, and order creation. Q. Vinh owns availability, stock, serials, ledger history, and warranty. |
| Search/select customer as Sales | GET Customers for Sales from Vy exists. POS desktop does not yet have a customer picker step. | Use Vy's API for Sales to look up and select a customer during HTTP runs. Dat hands off integrating Vy's picker into POS to verify desktop customer selection. |
| Tax and discounts | Vy's formula exists in `OrderService`. `PosCheckoutRequest` and the POS wiring do not yet accept/pass order-level discounts. | Verify configured VAT and stored totals on the merged build. Non-zero order-level discount cases at POS wait for Dat to wire checkout to Vy's contract. Vy confirms formulas and expected figures for the test configuration. |

## Goals / Non-Goals

**Goals**

- Verify over real HTTP every state change from pre-transaction to post-commit or rollback, with independent reconciliation values derived from stored data.
- Support running POS and Dashboard cases from the fixture on the baseline. End-to-end completion still requires real intake records and selling the exact received IMEI on the same build.

**Non-Goals**

- Changing APIs, authorization, pricing, receiving logic, POS, shifts, inventory, or owners' UI features. Do not repeat the four-role matrix, concurrency race tests, or full reconciliation of the fixed fixture.
- Modifying another owner's files, including test files and test helpers. When a test exposes a bug in another owner's area, record the issue and hand it off per D4; do not edit application code or relax assertions to make the test pass.

## Decisions

### D1. One isolated database per verification run

Use a migrated test PostgreSQL database on the exact build under acceptance. Create Binh's dedicated test class `DashboardLiveFlowPostgresHttpTests` in `tests/Backend.UnitTests/`, without modifying Dat's `PosCheckoutPostgresHttpTests`. Reuse `IdentityPostgresApi`, the opt-in `TECHSTORE_TEST_CONNECTION` configuration, and per-test generated databases without modifying those helpers. Any test-specific additions, such as overriding the shift/checkout clock or wrapping the Analytics snapshot timestamp in test DI, live in Binh's new test class or new helpers. Verify real HTTP through the production API host/DI. Use Kestrel and a dedicated demo database for the desktop run. Only query the test DB with a fresh context for assertions on committed rows, rollbacks, and fields absent from DTOs. Drive business operations through public endpoints.

The baseline run uses an `InStock` fixture device with matching product/variant and available stock. The customer is looked up via GET Customers using a Sales account on the `dev`-merged build. The end-to-end run requires looking up/creating a supplier per the handoff contract, recording a receipt, reconciling the receipt/IMEI, and looking up the customer as Sales on the same build. Fixture-based rows and API-received rows are recorded separately in the handoff so it is clear which inputs were verified.

### D2. Success case and read-back sequence

1. Manager logs in as `manager1` and calls `GET /api/analytics/dashboard` before receiving stock. Record `period`, date bounds, `timeZone`, `asOf`, summary, daily series, recent orders, and low stock. Use the same `custom` range covering the transaction's Vietnam date and the immediately preceding day. Run sequentially on an isolated database so each delta maps cleanly to a single transaction.
2. When receiving is present in the build, use a warehouse account to receive the test IMEI. Check receipt code, receiver, IMEI linkage to the purchase receipt, unit cost, stock increment, and `PurchaseReceipt` ledger entry. Record stock and reservation counts after receiving and before checkout, because receiving can change the low-stock baseline.
3. `cashier1` opens a shift via `POST /api/pos/shifts`. Use `GET /api/products?q=...` and `GET /api/inventory/serials?search=...` to identify the exact SKU/product/variant–IMEI. Look up the test customer and retrieve `customerId` via GET Customers on a build granting customer lookup. Send `POST /api/pos/checkout` with `posSessionId`, a fresh `checkoutKey`, `customerId`, sufficient `cashTendered` for the configured total, and one quantity-1 line with the exact `serialImeiId`. Record HTTP 201, order ID, order code, line item, and read the order back via `GET /api/orders/{id}`. Read stored `TotalAmount` and timestamps via a fresh context to verify the response.
4. Verify the order is `Completed`, `Cash`, with `PaidAt = CompletedAt` populated and matching customer, cashier, shift, and IMEI. Compared against the pre-sale stock snapshot, post-commit `Quantity` decreases by 1 and `ReservedQuantity` returns to its prior value. Reconcile the IMEI as `Sold`, warranty period per `WarrantyMonths`, snapshot unit cost, and exactly one `Sale` movement with quantity `-1`, post-sale quantity, actor, and `OrderItemId` foreign key. Read unit cost and movement history from the test DB if omitted by DTOs.
5. Close the shift via `POST /api/pos/shifts/{id}/close` with counted cash for a shift containing only this single Cash transaction. Verify `CashSalesTotal` increases by the order total, change given equals cash tendered minus order total, expected closing balance equals opening balance plus recorded Cash sales, counted balance matches, and discrepancy is 0. Verify shift status `Closed`, actor, and close timestamp.
6. Manager re-reads the same period after commit. `asOf` must be later than `CreatedAt` and `CompletedAt`. Relative to pre-transaction reads, `OrderCount` increases by 1 on the UTC+07 date of `CreatedAt`, revenue increases by `TotalAmount` on the date of `CompletedAt`, only that day bucket increases, and the daily series sums to summary revenue. The new order appears once in the up to 5 recent orders, matching ID, code, status, and total. If low stock changes from intake/sale, verify against actual stock balances whose threshold depends on `Quantity`.

In regression tests, choose a checkout timestamp after 17:00 UTC so the UTC date differs from the UTC+07 date. Use test-only DI for the shift/checkout clock and Analytics snapshot clock while preserving real auth/JWT and all production query and write logic. Keep two snapshot timestamps—before the transaction and after commit—so the sale does not fall on the exclusive `asOf` boundary. The Kestrel/desktop run uses real wall-clock time. If a live run crosses a date rollover, recreate the demo DB and rerun from the baseline with a period covering the target Vietnam date.

### D3. Repeatable Sold IMEI conflict case

Use an independent run with an open Cash shift. Sell a device via a successful checkout to establish a deterministic `Sold` state. Capture shift, stock, serial/warranty, ledger, related orders, and Dashboard state. Submit a checkout for the same IMEI with a fresh `checkoutKey`, sufficient cash, and the same open shift. With valid preconditions, expect HTTP 409 because the IMEI is already sold. Use a new key so execution reaches the availability check under the checkout contract.

After the 409 response, query the DB with a fresh context and re-read the Dashboard for the same period. Assert that no order or line exists for the failed key, no new movement was written, stock balances and warranty state/dates are unchanged, and shift totals and status are unchanged. Assert that KPIs, daily series, recent orders, and low stock are unchanged across all data fields. `asOf` reflects the new read timestamp. Document clearly that this is a sequential conflict check.

### D4. Evidence and issue handoff

Create [`docs/M3_TEST_SUMMARY.md`](../../../../docs/M3_TEST_SUMMARY.md#đối-soát-giao-dịch-pos-live) and link it from the M3 handoff. Each case records API/client build SHA (or base SHA plus local diff/artifact hash if built with uncommitted changes), UTC/UTC+07 timestamps, fixture or document codes, test SKU/IMEI, order/shift IDs, endpoint, inputs, Expected/Actual, HTTP status, and path to credential-scrubbed logs. Record Pass/Fail/Skipped/Not tested ("Chưa kiểm tra") per item. Pending handoff items specify exact steps, owner, dependency, and rerun criteria.

API runs record HTTP codes and read-back reconciliation data. Desktop runs with preview disabled on the integrated build record platform/target, receiving steps if UI is handed off, opening shift, scanning device, selecting customer if integrated, Cash checkout, closing shift, and switching to Manager to open the exact period. Capture screenshots or action logs and displayed figures, tied to the order code for API/DB cross-checking. Missing UI steps are noted by owner while keeping API evidence for those steps. The deterministic 409 case is executed over HTTP because UI scan may block `Sold` IMEIs upfront.

Route order/shift lifecycle and checkout discrepancies to Dat; receiving/IMEI/stock/ledger/warranty/cost issues to Q. Vinh; customer/pricing issues to Vy; Catalog lookup/search UI issues to T. Vinh. Binh records Dashboard delta evidence, summarizes defects, and reruns against fixed SHAs. Only reconcile pricing/order discounts against contracts present on the build under test. Unconnected parts document the exact pending scope and responsible owner.

## Risks / Trade-offs

- [Receiving code lives on a separate branch] -> Mark receiving complete only when the same build executes POST and links the purchase receipt to the sold IMEI. Fixture data keeps POS/Dashboard verification unblocked meanwhile.
- [Receiving not merged before Sprint 5 deadline] -> Do not leave the change open indefinitely because `m3-test-handoff-and-acceptance` depends on its output. If `dev` still lacks receiving by the deadline, mark receiving items (task 2.1 and the receiving step in 3.1) as "Not tested – waiting for Q. Vinh" ("Chưa kiểm tra – chờ Q. Vinh") with rerun conditions, then archive. Run the supplement once receiving merges and update evidence in the same document.
- [Reads crossing a date boundary] -> Compare identical period bounds, record `asOf`, verify timestamps lie inside the range, and commit only one checkout per run. Use an anchored test clock for UTC+07 boundary regression.
- [Different API and desktop build SHAs] -> Record the SHA for each evidence type. If customer picker UI, receiving UI, or order discount wiring is missing, note each pending item explicitly even when basic API checkout passes.

## Migration Plan

Use the migrations from the integrated owner build when initializing test/demo databases. Use the test fixture's isolated database cleanup per run. This change only requires rerunning tests and updating handoff records after integration.
