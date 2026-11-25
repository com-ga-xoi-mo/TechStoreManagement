# Design

## Context

`DemoSeedRepository` reconciles rows by natural keys under an advisory lock, and `DataSeeder` commits everything in a single transaction. The manifest currently defines 5 categories, 20 products with cost prices, 3 variants (`IP15PM-256-NT`, `S24U-256-GR`, `MBP14-M3-512-SG`), 10 customers, 20 orders (12 `Completed`, 2 `Processing`, 3 `Pending`, 3 `Cancelled`), and 50 IMEIs. The repository creates four demo accounts. Manifest SKUs, order codes, timestamps, and positional indices are already relied upon by fixtures. The identified data gap is missing `ProductImage` rows. `backend/samples/pixel.png` is a tiny test asset suited for upload tests, not for illustrating products during demos.

`AssertAnalyticsReferenceAsync` in the PostgreSQL seed integration suite currently calls the repository directly and zero-fills days itself. Following the Manager Dashboard change, the real endpoint supports `custom`. It reads current database time to compute the three relative periods and `asOf`.

## Goals / Non-Goals

- Allow Development seeding to generate covers on a fresh DB and backfill missing covers by SKU on existing DBs while preserving existing images, cost prices, and user data.
- Run fixed reference verification through the real HTTP pipeline against PostgreSQL data. Record live demo runs using the historical custom range.
- Out of scope: schema migrations, migrating images between providers for existing DBs, changing pricing formulas or Analytics logic, and reconciling new closed-shift POS orders.

## Decisions

### D1. Append only missing items to the existing seed

Keep `AnchorUtc=2026-10-18T05:00:00Z` and all existing data intact. Any new image mappings attach as new data by SKU or category slug, preserving existing rows, ordering, and manifest indices. Do not add orders or alter amounts. If valid seed rows are added later, update fixed-count assertions in `DemoSeedManifestTests`, `DemoSeedRepositoryTests`, and `DataSeederPostgresIntegrationTests` together with related Expected tables. Assert exact count 50 IMEIs in regression tests for the current fixture and verify uniqueness, format, and links; M3 acceptance criteria do not mandate a minimum IMEI count. On legacy databases, verify that modified cost prices and metadata are preserved even when they differ from the manifest.

### D2. Bundled sample images, provider-supplied URLs

Generate placeholder PNG illustrations per category (no real product photos, logos, or brand marks), one sample per category group so every SKU receives a cover image. Place files under `backend/samples/demo-products/` and embed them as embedded resources so seeding works from the repo, test runners, or published artifacts in another directory. Use Catalog's existing content-signature validation and image limits. Give each product its own object so deleting one image does not affect other products.

`DataSeeder` orchestrates within the existing seeding transaction. After `DemoSeedRepository.ReconcileAsync` succeeds, `DataSeeder` calls the repository to find demo SKUs missing images. The repository checks newly staged products as well, acquires Catalog's product lock for existing products, and skips products that already have a gallery or cover URL. For each SKU still missing an image, `DataSeeder` calls `IObjectStorage.PutAsync` directly, passes the returned URL back to the repository to stage `ProductImage` and cover, then calls `IUnitOfWork.SaveChangesAsync` and `CommitAsync`. The repository sets null variant, sort order 0, the seed anchor timestamp, and `ProductCoverImage` rules for the cover URL.

Image IDs are deterministic per SKU; object keys follow the existing `products/{productId:N}/{guid:N}.png` format. Local storage uses `CreateNew`, so idempotency relies on DB references and the advisory lock rather than overwriting keys.

Before commit, on conflicts, upload errors, or save failures, `DataSeeder` rolls back and performs best-effort cleanup of newly created keys for that attempt via `IObjectStorage`. Cleanup uses an independent token if the request was canceled. When the commit outcome is ambiguous, preserve objects until verified unreferenced in DB to avoid breaking committed images. Document troubleshooting steps when DB holds a URL whose file was deleted externally rather than silently replacing existing galleries. Use the same provider and storage across reruns.

### D3. Expand existing PostgreSQL fixtures

Keep the existing fresh/rerun, Sprint 1 baseline, and partial/conflict test cases in `DataSeederPostgresIntegrationTests` and its isolated schema fixture. Add small sentinel checks before/after reruns: IDs, timestamps, credential hashes, modified user fields, and existing galleries/covers. When testing passwords/roles of the four seeded accounts, distinguish a fresh seed from an already-modified DB. Keep the fixed admin ID rule and natural SKU/slug keys for legacy baselines.

Reconcile orders, items, shifts and actors, shift totals against recorded paid orders, VietQR records, serial/warranty state, stock/reserved counts, and receipt/sale ledger rows by source keys. Extend the existing conflict case to verify rollback via a fresh context and before/after snapshots. Add a storage failure or post-upload save failure case to prove no dangling DB references remain and only objects created by the failed attempt are cleaned up. Reuse `DemoSeed*Tests` and `DataSeederTests` for idempotency, locking, and save/rollback ordering.

### D4. HTTP reconciliation at two clear points in time

Build a manual table from the fixture's order and item rows with order codes, UTC timestamps, Vietnam dates, statuses, and amounts. Compute values independently before checking API output or existing assertions. Compute revenue from `CompletedAt` on `Completed` orders and order count from `CreatedAt` across all statuses. List zero days and up to 5 order codes ordered by `CreatedAt DESC, id DESC`, plus low-stock rows in contract order. Active products and low stock reflect the seeded DB state at read time. `2026-10-18T05:00:00Z` corresponds to 12:00 on 18/10 UTC+07.

| Request at fixed anchor | Vietnam dates | Reference revenue | Reference order count |
| --- | --- | ---: | ---: |
| `today` | 18/10/2026 | 27,980,000 ₫ | 2 |
| `last7Days` | 12–18/10/2026 | 136,080,000 ₫ | 20 |
| `thisMonth` | 01–18/10/2026 | 136,080,000 ₫ | 20 |
| `custom&from=2026-10-12&to=2026-10-18` | 12–18/10/2026 | 136,080,000 ₫ | 20 |

For example, the 12–18/10 daily series sums to 14,480,000, 35,980,000, 0, 14,980,000, 22,980,000, 19,680,000, 27,980,000 ₫. Numbers in this design are reference targets; record Actual only after running against an isolated DB and resolving any differences.

Add `Microsoft.AspNetCore.Mvc.Testing` version `10.0.12` to `tests/Backend.IntegrationTests/TechStore.Backend.IntegrationTests.csproj`, matching `Backend.UnitTests`. Extend the existing reconciliation test to host `WebApplicationFactory<Program>` on the fixture's isolated schema and Local storage folder, using real login and real PostgreSQL. Test host DI wraps the Analytics repository only to pin `GetSnapshotTimeUtcAsync` to `AnchorUtc`; all queries, controllers, mappings, authentication, and serialization use production code paths. Do not add an API parameter to override the clock. Verify `AdminDashboardDto` over HTTP across all four periods, zero days, totals, and top orders. This uses the contract to verify KPIs rather than duplicating repository code in Expected calculations. Clarify that `asOf` in anchored mode is a test timestamp, not a historical snapshot of a modified DB.

During live demos, run the standard API using the database clock with Dashboard preview disabled, log in as `manager1`, and select 12–18/10/2026. `custom` includes all of 18/10 if the demo runs on a later date, whereas the fixed test cuts off at 12:00 that day. Compute Expected per the exact bounds of each run and record results separately. The three relative periods always follow demo wall-clock time and may be empty outside the seed month.

### D5. Pricing formula boundary

`EnsureOrdersAsync` persists `TotalAmount` from product/variant prices and sets tax and discount to 0 without invoking checkout pricing. Fixture reconciliation verifies stored amounts, payment relations, and aggregations. Official pricing and allocations belong to Vy; tax/discount calculations during real checkout and POS orders are verified in `m3-dashboard-live-flow`.

## Risks / Trade-offs

- [Object storage sits outside DB transactions] -> Clean up newly uploaded keys only after confirmed rollback; preserve references and log cleanup failures for demo storage maintenance.
- [Wrong period from machine clock or run date] -> Keep an isolated clock anchor for the test host, record real `asOf` for live demos, and document UTC+07 bounds in the report.
- [Switching providers breaks old URLs] -> Pair each demo run with a matching demo DB and storage. For R2, use a dedicated demo bucket and public base URL, never logging or documenting real secret values.

## Migration Plan

Use existing migrations. Provide Local demo instructions with `ObjectStorage__Provider=Local`, `ObjectStorage__Local__RootPath` pointing to a writable demo directory, `ObjectStorage__Local__RequestPath=/uploads`, and `dotnet run --project backend/TechStore.Api.csproj --launch-profile http` with the demo DB connection string. Verify seed logs, read cover URLs from the API, and open URLs or browse Catalog in the client.

For R2, use a fresh demo DB/bucket and configure `ObjectStorage__Provider=R2`, `ObjectStorage__R2__AccountId`, `ObjectStorage__R2__AccessKeyId`, `ObjectStorage__R2__SecretAccessKey`, `ObjectStorage__R2__BucketName`, and `ObjectStorage__R2__PublicBaseUrl` via environment variables, user-secrets, or git-ignored `backend/.env`. Check seed logs, read absolute URLs from product details, and verify public access. Record R2 URL verification as not tested if no test bucket is available.
