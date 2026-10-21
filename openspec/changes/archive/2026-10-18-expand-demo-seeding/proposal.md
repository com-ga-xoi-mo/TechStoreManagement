# Proposal

## Why

`DataSeeder` returns as soon as any role exists, so a Sprint 1 database never receives the expanded Sprint 2 demo catalog and transactional history. Its generated IDs and `DateTime.UtcNow` values also make the dataset hard to reconcile, while the current database test silently returns when PostgreSQL is unavailable.

## What Changes

- Expand the demo dataset to at least 20 products, 5 categories, 10 customers, 20 orders across multiple lifecycle states, and 50 schema-valid unique 15-digit IMEIs.
- Make seeding additive and repeatable on empty, Sprint 1, and partially populated databases; preserve existing account and user-owned data, and report conflicting seed-owned records instead of overwriting them.
- Establish stable seed identity and a fixed, documented UTC timestamp anchor; reconcile legacy rows by natural keys before inserting missing rows.
- Keep stock, reservations, serial states, append-only movements, order lifecycle, and payment records consistent, and document expected Analytics aggregates for the seeded snapshot.
- Replace the role-existence early return with per-entity reconciliation and align seeder persistence with repository and Unit of Work boundaries.
- Require explicit PostgreSQL evidence for new database, legacy baseline, rerun, and partial-seed scenarios; unavailable PostgreSQL remains unverified.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `data-seeding`: expand dataset thresholds and require safe additive reconciliation, deterministic identity/time, lifecycle-consistent records, and verifiable PostgreSQL behavior.

## Impact

- Backend seeding and data-access registration in `backend/Common/Data/` and `backend/Program.cs`.
- PostgreSQL integration/unit tests for seeding; no API route or database schema change is intended.
- Existing Sprint 1 identity/catalog/customer/inventory/order/payment rows are inputs to natural-key reconciliation and must not be reset or rewritten.
- Analytics reconciliation follows the existing `admin-analytics-api` contract.
