# Proposal

## Why

Binh needs to complete the Sprint 5 regression portion and consolidate M3 evidence for An to assemble the submission package. Because results currently span multiple builds and test suites, every conclusion must be traceable to its test case, owner, and executed build.

## What Changes

- Before testing, fetch and merge latest `origin/dev` into `feat/m3-manager-dashboard-access`, rebuild, and record the actual executed SHA. Reference the three archived Dashboard, seeding, and live-flow changes.
- Update Binh's sections that already have evidence for the Manager Dashboard, demo dataset, and idempotent seeding. Run M2 Admin regressions for products, categories, and customers, reusing existing automated tests and adding HTTP/PostgreSQL coverage for gaps.
- Reconcile post-checkout data for successful checkouts, 409 conflicts, and 400 input validation errors. Reuse live-flow evidence for the covered scope and explicitly mark sequential conflict checks.
- Consolidate test results by group, build, and owner in `docs/M3_TEST_SUMMARY.md`. Items waiting for receiving, POS integration, or Windows native UI record their status and rerun conditions for An to follow up.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. This change covers only verification and handoff under existing contracts and sets `skip_specs: true`.

## Impact

- Within apply scope, editable files include `docs/M3_TEST_SUMMARY.md` covering [Dashboard Manager](../../../../docs/M3_TEST_SUMMARY.md#dashboard-manager), [Demo data and KPI reconciliation](../../../../docs/M3_TEST_SUMMARY.md#dữ-liệu-demo-và-đối-soát-kpi), and [Live POS transaction reconciliation](../../../../docs/M3_TEST_SUMMARY.md#đối-soát-giao-dịch-pos-live), Binh's checkboxes in `docs/MILESTONE_3_PLAN.md`, and a new test class owned by Binh if needed.
- Keep all other owners' application code, tests, helpers, and documents untouched. When discrepancies are found, document the defect, evidence, and responsible owner; rerun results must tie to the fixed build.
