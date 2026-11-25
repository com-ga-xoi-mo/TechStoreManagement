# Proposal

## Why

Binh needs to prove that the Manager Dashboard reflects a newly created POS Cash order from a real intake-and-sale flow on PostgreSQL. The two preceding changes verified Dashboard access and reconciled the fixed fixture, so this change focuses on metric updates after commit and after a failed checkout.

## What Changes

- Run the end-to-end flow: receive IMEI-tracked stock -> open shift -> look up product, select customer -> Cash checkout -> close shift -> Manager reads the Dashboard again. Verify that the order count increases by exactly 1, revenue increases by the exact stored `TotalAmount`, the order appears among the 5 most recent orders, and revenue lands on the correct UTC+07 calendar date.
- Run a checkout attempt for an already `Sold` IMEI using a new idempotency key (`CheckoutKey`) and an open shift. Confirm HTTP 409 and reconcile inventory, shift, order, movement ledger, and Dashboard before and after the attempt.
- Reconcile purchase receipt, order, IMEI, reservation, inventory movement, warranty, unit cost, and shift cash figures against the existing contracts. Record any discrepancies by owner so Dat, Q. Vinh, or Vy can resolve their respective areas.
- Prepare a POS/Dashboard flow that can run with an `InStock` device already present in the fixture to verify behavior while waiting for receiving. End-to-end receiving evidence waits for Q. Vinh's receiving API and handoff contract to be present in the tested build.
- Record inputs, Expected/Actual, status, build SHA, and logs for each case. Record HTTP and desktop verification evidence separately. Reuse the role-authorization evidence from `m3-manager-dashboard-access`.

## Capabilities

### New Capabilities

None. This is a verification and handoff change.

### Modified Capabilities

No contract changes. Verification criteria are taken from `admin-analytics-api`, `pos-cash-checkout`, `pos-shift-management`, `inventory-fulfillment`, and Q. Vinh's receiving contract once handed off. This change sets `skip_specs: true`.

## Impact

- Add Binh's dedicated test class `tests/Backend.UnitTests/DashboardLiveFlowPostgresHttpTests.cs` to reconcile the Dashboard after a POS transaction, using a dedicated database and JWTs from real logins. Consolidate evidence in [`docs/M3_TEST_SUMMARY.md`](../../../../docs/M3_TEST_SUMMARY.md#đối-soát-giao-dịch-pos-live), linked from [`docs/M3_TEST_SUMMARY.md`](../../../../docs/M3_TEST_SUMMARY.md#dashboard-manager).
- Only add or edit files within Binh's scope: the new test class, dedicated test helpers if needed, and the two documentation sections above. Do not modify files belonging to other owners, including `PosCheckoutPostgresHttpTests` and Dat's POS/shift code, Q. Vinh's receiving/inventory code, Vy's Customers/pricing formulas, T. Vinh's Catalog search, and An's Identity/test helpers such as `IdentityPostgresApi`. Those files may only be called.
- An end-to-end build requires combining the applied Manager Dashboard, Dat's POS/shift, Q. Vinh's receiving, and Vy's customer lookup API. Verifying order-level discounts at POS also depends on Dat wiring checkout to Vy's contract.
- Binh's scope is verification and handoff. Receiving, inventory operations, checkout, pricing formulas, and owners' desktop features are verified through existing paths.
