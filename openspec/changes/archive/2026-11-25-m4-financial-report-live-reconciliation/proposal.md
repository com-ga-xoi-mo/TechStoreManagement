# Proposal

## Why

Sprint 2 needs a single reconciliation of the financial report against transactions committed by the integrated API. A hand-calculated baseline and independent reads of the recorded data will help each owner locate discrepancies in amounts, timing, stock, and access.

## What Changes

- Add an integration test class owned by Bình. Use real HTTP routes, JWT roles, and a disposable PostgreSQL database to capture a report before activity and reconcile it after receiving stock, selling, partially and fully refunding, and inspecting returned items.
- Verify the Sprint 2 two-item example by Vietnam calendar day and for the full period. Cover a warranty replacement, an order refunded before delivery, replayed and failed requests, and competing transactions with evidence of database overlap.
- Read the committed money, tax, inventory, IMEI, shift, loyalty, and voucher records owned by the contributing modules. Record the actual QR payment mode and identify missing integrations by owner and rerun condition.
- Record API/PostgreSQL, desktop UI, and AI results separately in `docs/M4_FINANCIAL_REPORT.md`, with build identifiers, commands, test inputs, expected and observed results, and status.

## Capabilities

### New Capabilities

### Modified Capabilities

## Impact

- The change adds Bình’s integration tests and updates the existing financial report evidence document. It verifies the existing contracts through the three M4 reporting changes and the Sprint 2 plan. The metadata opts out of delta specs with `skip_specs: true`.
- Testing depends on the integrated inventory, payment, returns, warranty, pricing and loyalty workflows and the report consumers. Existing owner test classes and the October 2026 M3 fixture provide regression coverage and reference data.
