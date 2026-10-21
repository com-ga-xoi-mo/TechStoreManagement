# Design

## Context

See `proposal.md` for motivation and `specs/data-seeding/spec.md` for the behavior contract. `DataSeeder` currently owns an `AppDbContext`, returns when any role exists, generates most IDs randomly, derives dates from `DateTime.UtcNow`, and saves directly. The existing data-seeding spec expects only Sprint 1 minimums. Analytics reads Completed revenue by `CompletedAt`, order count/recent orders by `CreatedAt`, and low stock by balance `Quantity <= MinStockAlert`. Order fulfillment reserves inventory before completion; completion consumes stock, marks linked serials Sold, sets warranty dates, and writes Sale movements.

## Goals / Non-Goals

**Goals:**
- Upgrade empty and Sprint 1 databases to the expanded, deterministic demo fixture.
- Preserve existing rows and make missing seed-owned rows safe to add.
- Ensure the cross-module seed graph is atomic and lifecycle-consistent.
- Make expected Analytics output independently checkable against a fixed reference time.

**Non-Goals:**
- Reset, truncate, migrate, or require recreating the developer's current database.
- Reconcile arbitrary user edits by overwriting existing records.
- Add a new public API or change the Analytics contract.
- Implement new order/refund business workflows solely to manufacture demo states.

## Decisions

### Seed identity and legacy matching

Use a fixed seed namespace and deterministic UUID v5 (or an equivalent deterministic UUID function) for newly created seed rows. Before insert, resolve existing rows by domain natural keys, not IDs, because the Sprint 1 seeder generated random IDs. The fixture manifest owns those keys; existing IDs and foreign-key relationships are retained when a match is found.

| Entity | Natural key used to match the Sprint 1 baseline |
| --- | --- |
| Role / user | Role name; normalized username, with email as a consistency check |
| Category / product / variant | Category slug; product SKU; variant SKU |
| Supplier / purchase order / PO item | normalized supplier name; PO number; PO number + product SKU + nullable variant SKU |
| Stock / serial | product SKU + nullable variant SKU; IMEI (or legacy serial number when IMEI is null) |
| Customer / voucher | normalized phone; normalized voucher code |
| POS session / order / order item / VietQR | stable seed session key where available; order code; order code + line key; transaction code |
| Movement / loyalty / return | source business key plus movement/ledger kind and deterministic fixture line key |

Matching a natural key does not grant permission to update that row. Seed-owned immutable identity fields and required relationships must agree with the manifest; user-editable fields are left as found. If a row occupying a seed key is incompatible, fail with entity/key context and roll back the current transaction. Add only absent rows. If the existing business data already satisfies a fixture effect, recognize it through the source natural key and do not append another stock/ledger/payment effect.

### Fixed timestamps and analytics reference

Use a documented constant anchor `2026-10-18T05:00:00Z` (12:00 on 18 October in UTC+07) for the fixture. Every seed-owned timestamp is a deterministic offset from this anchor; it is never recalculated from process start time. Existing timestamps are never changed. This avoids a schema migration or storing fixture metadata in user-facing `store_settings`; the trade-off is that this dataset represents a stable historical snapshot, not a rolling "today" demo. Tests run Analytics with a fixed `TimeProvider` at the reference anchor.

The fixture manifest must explicitly list order status, `CreatedAt`, `PaidAt`, `CompletedAt`, payment method, line keys, quantities and totals. Completed orders contribute gross revenue by `CompletedAt`; all statuses contribute order count/recent-order selection by `CreatedAt`. Include at least one zero-sale day in the last-seven-days reference range. The expected reconciliation table is:

| Analytics output at reference anchor | Expected value / derivation |
| --- | --- |
| Active product count | 20 in an empty test schema |
| Today revenue | 27,980,000 VND: 10,990,000 + 16,990,000 |
| Last 7 days revenue | 136,080,000 VND across local dates 12–18 Oct 2026 |
| This month revenue | 136,080,000 VND across local dates 1–18 Oct 2026 |
| Daily revenue, 12→18 Oct | 14,480,000; 35,980,000; **0**; 14,980,000; 22,980,000; 19,680,000; 27,980,000 VND; sum = 136,080,000 VND |
| Order count | 2 today; 20 in last 7 days and this month at the reference as-of (all 20 fixture orders were created in the last 7 days) |
| Recent order codes/status | `DEMO-202610-0011` Completed; `DEMO-202610-0006` Completed; `DEMO-202610-0001` Completed; `DEMO-202610-0007` Completed; `DEMO-202610-0013` Processing |
| Low-stock count/list | 7 balances; first five by API ordering: Anker 735 Charger 65W, Jabra Evolve2 65 Flex, Logitech Brio 4K Webcam, Logitech MX Master 3S, USB-C 100W Braided Cable. Each has Quantity 0, ReservedQuantity 0, AvailableQuantity 0, MinStockAlert 3. |

The fixed table applies to an empty isolated test schema. Legacy-baseline verification additionally captures pre-existing rows, preserves their timestamps, and checks that database analytics equals the sum of retained baseline contributions plus these fixture rows; it does not assume a legacy database contains only fixture data.

### Partial seed and conflict policy

Seeding is additive reconciliation, not a destructive reset. Roles, accounts, and every domain entity are checked independently; the presence of one role never suppresses unrelated missing seed data. If a seed row is absent, insert its deterministic fixture row. If it is present and compatible, retain it unchanged. If its natural key collides with incompatible identity or lifecycle data, abort the seed transaction with a clear conflict report. Never reset passwords, overwrite customer/product edits, delete rows, or “repair” a business transaction by appending compensating movements. A later rerun can proceed after the conflicting record is resolved by its owner.

The entire seed graph is one transaction. This keeps newly inserted rows from being left half-linked when a downstream natural-key conflict occurs. Existing rows are read-only to the reconciliation process; rollback leaves them as they were. Serialize concurrent seeder runs using a PostgreSQL transaction-scoped advisory lock so two application instances cannot both insert the same natural keys.

### Repository and Unit of Work boundary

Keep `DataSeeder` as an orchestration service and remove its direct `AppDbContext` dependency. Introduce a seed-specific internal repository contract for natural-key lookups, loading the minimum related state needed to reconcile the fixture, and staging additions. Its PostgreSQL implementation is the only seeder component that uses `AppDbContext`/EF Core. Register it scoped beside `IDataSeeder`; the orchestrator uses `IUnitOfWork` for begin, save, commit and rollback. Use one atomic transaction and call `SaveChangesAsync` through UoW. A dedicated seed repository is preferred over routing every fixture row through domain services: this operation imports a coherent reference dataset, while public order workflows own real-time sales behavior. The seed repository must not fabricate incomplete lifecycle effects or expose `IQueryable`/API DTOs.

### Lifecycle-safe fixture construction

- Seed received purchase orders before their stock, receipt movements, and serials. Each purchase receipt is unique by PO item and contributes to the balance exactly once.
- For serial-tracked balances, `Quantity` equals `InStock + Reserved` serial count; `ReservedQuantity` equals Reserved serial count. All IMEIs are unique, exactly 15 numeric digits, and satisfy the actual database constraint (do not silently claim a Luhn rule that the schema does not enforce).
- Build sale orders with immutable item snapshots. Completed lines with serials point to Sold serials, have warranty dates beginning at `CompletedAt`, and have matching Sale movement rows linked to the order item. Sale movement quantities are negative and `QuantityAfter` is monotonic per stock balance.
- Use the supported status mix `Pending`, `Processing`, `Completed`, and `Cancelled`. Pending rows are unpaid and unreserved; Cancelled rows originate as unpaid Pending and have no reservation/sale effect. Processing rows are paid, have matching Reserved serials/stock reservation quantities, and have no sale movement. Completed rows have payment/session evidence and consumed inventory. Do not seed refunded states because the current order state transition service does not implement returns/refunds; schema-only rows would be misleading.
- Payment records and POS session cached totals must reconcile to the seeded orders. VietQR Confirmed rows have their required confirmation time and provider/reference evidence; cash sales use the appropriate session. Do not create payment evidence for unpaid orders.
- Keep customer loyalty balance equal to its ledger and voucher usage consistent with its associated fixture orders.

### PostgreSQL verification strategy

Replace the current test's implicit return-on-connection-failure behavior. Unit tests cover fixture determinism, natural-key decisions, collision policy, and graph invariants without PostgreSQL. PostgreSQL integration tests require an explicit dedicated test connection and fail/mark the verification unverified when it is absent; they must assert the target database is isolated before destructive setup. Use disposable databases or isolated schemas created for the test run, not the developer's configured application database. Exercise (1) empty database, (2) a Sprint 1 baseline snapshot with its random IDs, (3) immediate rerun, and (4) partial seed with both missing rows and a conflicting row. Do not use `EnsureDeleted` against a generic connection string. Evidence is only verified when these PostgreSQL scenarios actually execute and pass.

## Risks / Trade-offs

- [Natural-key collision with user data] → Preserve the existing record and fail atomically with its key; require an owner to resolve it, never overwrite it.
- [Legacy IDs differ from deterministic IDs] → Resolve all parent rows first and build the fixture relationship graph from their persisted IDs.
- [Fixed timestamp anchor ages] → Treat the dataset as a stable historical snapshot and use fixed-clock acceptance; do not move dates on rerun.
- [Database evidence is environment-dependent] → Keep PostgreSQL verification explicit and report unavailable runs as unverified, never passed.
- [Seeder bypasses domain workflows] → Restrict it to the seed-only repository, encode the same persisted lifecycle invariants, and verify the complete relational graph against schema and existing service transitions.
