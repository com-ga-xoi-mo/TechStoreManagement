# Design

## Context

See `proposal.md` for motivation and source/spec reconciliation. Current persistence is EF Core/Npgsql in `AppDbContext`; Orders already has `CreatedAt`, `CompletedAt`, `Status`, `TotalAmount`, and `OrderItem` product/variant snapshots. Inventory stores one balance per product/variant, exposes the exact existing low-stock predicate `Quantity <= MinStockAlert`, and reports `AvailableQuantity = Quantity - ReservedQuantity`. Auth emits role claims matching `RoleType` names. No Analytics module/API DTO exists. Dashboard preview types are client/demo models and must not cross into the API or repository contract.

## Goals / Non-Goals

**Goals:**
- Establish one Admin-only dashboard read endpoint for UC-STAT-01/02/03.
- Keep all database access and aggregation in an Analytics repository; service owns period semantics, orchestration, response mapping, and transaction boundary.
- Return a bounded, deterministic API response with a verifiable as-of snapshot.
- Provide PostgreSQL reconciliation evidence before downstream Dashboard integration.

**Non-Goals:**
- Dashboard view/repository/DI integration, fixture policy changes, or Dashboard model migration.
- Seeder changes, order/refund lifecycle changes, inventory balance changes, or new business mutations.
- Net revenue accounting. Version one reports completed gross order total and excludes refunded statuses rather than subtracting refund rows.

## Decisions

### Endpoint, period vocabulary, and DTO

Use `GET /api/analytics/dashboard?period=today|last7Days|thisMonth`, with omitted period defaulting to `last7Days`; reject any other value with 400. Add a dedicated API DTO in `shared/DTOs`, not the frontend preview records. The top-level response contains:

- `period`, `startDate`, `endDate`, `timeZone: "UTC+07:00"`, and `asOf` serialized as UTC (`Z`). Dates are local Vietnam calendar dates; `endDate` is inclusive.
- `summary`: `revenue`, `orderCount`, `activeProductCount`, `lowStockCount`, `revenueChangePercent`, and `orderCountChangePercent`.
- `dailyRevenue`: `{ date, revenue }` for every calendar date in the requested period.
- `recentOrders`: up to five real order rows with `id`, `orderCode`, nullable `customerName` (null means walk-in), `productSummary` from persisted OrderItem snapshots, `totalAmount`, actual `status`, and UTC `createdAt`.
- `lowStockItems`: up to five balance rows with product/variant identity and name, `quantity`, `reservedQuantity`, separately calculated `availableQuantity`, and `minStockAlert`.

Return both comparisons as JSON null in v1. Do not serialize comparison as zero or omit it. The API DTO describes the wire contract; frontend `DashboardSnapshot`, `RecentOrder.DemoDisplayId`, and `DashboardSourceKind.Demo` remain preview-owned and are not reused.

### Time windows, KPI rules, and list semantics

Capture a single UTC `asOf` timestamp from PostgreSQL `statement_timestamp()` in the first read statement after beginning the snapshot transaction. This aligns the boundary with acquisition of the RepeatableRead snapshot more closely than `transaction_timestamp()` (which is fixed at transaction start). Convert that timestamp to fixed UTC+07 to determine local dates. Use `startDate` at 00:00 UTC+07 converted to UTC as inclusive lower bound and `asOf` as exclusive upper bound for all time-filtered queries. Thus today is a partial day and last7Days is today plus six preceding local dates; this matches the existing three Dashboard period names and default last-seven-days selection.

- Revenue: database `SUM(TotalAmount)` for `Status=Completed` with `CompletedAt >= fromUtc AND CompletedAt < asOfUtc`. `PartiallyRefunded` and `Refunded` are excluded. Refund rows are not subtracted; this is explicitly gross completed revenue.
- Order count: database count of all statuses with `CreatedAt` in the same half-open interval. A yesterday-created order completed today contributes revenue today, not today's order count.
- Active product count: database count of `products.IsActive = true` at snapshot.
- Low stock: count balance rows, not distinct product ids, using only `Quantity <= MinStockAlert`. `ReservedQuantity` does not alter this threshold. `AvailableQuantity = Quantity - ReservedQuantity` is informational and returned separately. The summary count covers all matching rows; return the first five rows ordered by quantity ascending, product name, variant name (nulls first), then stock id.
- Recent orders: all statuses, filtered by `CreatedAt` in the requested interval, ordered by `CreatedAt DESC, Id DESC`, maximum five. Use OrderItem snapshots; do not join current product state to rewrite historical names. Repository projection returns first item snapshot by item id and total line count; service formats the first product/variant plus `+ N mặt hàng` when more lines exist. This bounds transferred data and avoids materializing full orders.
- Daily revenue: group the same Completed/CompletedAt set by UTC+07 local date in PostgreSQL. Fill absent dates with zero in service. Revenue total comes from the database aggregate over the same criteria/snapshot; integration tests assert `SUM(dailyRevenue) == summary.revenue` exactly.

No comparison range is queried in v1. A supplied/default period determines both summary and the two period-bound lists; product/low-stock counts are point-in-time snapshot values, not historical period values.

### Repository contract, service, and snapshot consistency

Add an Analytics module rather than placing reporting queries in Orders or Inventory repositories. Controller depends only on `IAnalyticsService`; service depends on `IAnalyticsRepository` and `IUnitOfWork`, never `AppDbContext` or EF. Repository accepts validated internal criteria such as `DashboardAnalyticsCriteria(FromUtc, AsOfUtc)` and returns internal aggregate/read records such as `DashboardAnalyticsReadModel`, daily rows, low-stock rows, and recent-order rows. It MUST NOT return shared API DTOs, EF entities, or `IQueryable`.

The service opens one `RepeatableRead` transaction through `IUnitOfWork`, asks the repository for a snapshot anchor using `statement_timestamp()` as its first read, computes the UTC+07 date window, then calls the repository read method(s), maps the internal read model to the shared response DTO and commits. On error/cancellation it rolls back and rethrows so middleware retains existing error behavior. All repository methods are read-only `AsNoTracking` projections and execute their filters, `COUNT`, `SUM`, `GROUP BY`, sort, and `LIMIT` in PostgreSQL; no full order list is loaded for in-memory aggregation. The RepeatableRead transaction ensures all query parts see the same PostgreSQL snapshot. Extend `IUnitOfWork` with an overload accepting `System.Data.IsolationLevel`, preserving the existing default overload for current write flows; update `UnitOfWork` and test fake without exposing EF types to services.

To avoid duplicating KPI logic, centralize the completed revenue predicate and UTC half-open boundaries in the repository query path; both the revenue total and date grouping use it. Keep `asOf` and computed dates in one service result mapping. Apply no writes, locks, or `SaveChanges` for this read endpoint.

### Authorization and HTTP behavior

Create `AnalyticsController` at `api/analytics` with `[Authorize(Roles = "Admin")]` on dashboard action/controller. Existing JWT role claim uses `RoleType.ToString()`, so this matches seeded `Admin`; do not inherit the broader `Admin,Manager` policy of `StoreController`. Existing authentication middleware supplies 401/403. Validate period before opening the database transaction and return standard `ValidationProblemDetails` with a `period` field for invalid input.

Register repository and service as scoped in `backend/Program.cs`. Add endpoint response metadata for 200/400/401/403. No client credential or alternate unauthenticated preview endpoint is added.

### PostgreSQL implementation and indexes

Use EF Core translated aggregate projections in the Analytics repository (or parameterized PostgreSQL SQL only if a LINQ translation cannot preserve the specified grouping/limit); keep all date predicates sargable by comparing raw `timestamptz` columns to UTC bounds, never applying local-date conversion to a filtered column. Group revenue by local date using the database timezone conversion after filtering. Existing indexes cover `orders.created_at`, `orders.status`, stock product/variant identity and catalog product id. There is currently no `completed_at` index; inspect `EXPLAIN (ANALYZE, BUFFERS)` against representative seeded data during PostgreSQL verification. Add a migration and schema DBML index update only if measurements show the completed revenue query needs it; do not add an unconditional schema migration in advance.

## Risks / Trade-offs

- [CompletedAt may be null for a malformed Completed row] → Such a row cannot be assigned to a revenue period and is excluded by the range predicate; reconciliation tests verify normal transitions set it.
- [Completed gross amount can differ from cash retained after a refund] → Name/report it as completed gross revenue, exclude refund statuses, and document that net revenue is a later contract.
- [RepeatableRead can hold a database snapshot during several aggregate queries] → Keep the query set bounded/read-only, pass cancellation through every call, and avoid loading order entities or collections.
- [OrderItem snapshots can make a recent order's product summary longer] → Select a compact bounded summary in the database projection; keep the parent order limit at five.
- [Current completed revenue query lacks a dedicated CompletedAt index] → Measure query plan on PostgreSQL; add an index migration only when evidence warrants it.
- [Dashboard preview model uses demo-specific fields and lacks all response fields] → This change adds API contracts only; the separate live Dashboard change owns DTO-to-UI mapping.

## Migration Plan

No data migration is expected. Deploy the API/DTO additions alongside existing endpoints. The endpoint is immediately protected by JWT Admin role. PostgreSQL tests should run against the current schema; any index migration requires a separately reviewed migration and DBML update based on measured query plans. Rollback by removing the route and DI registrations; existing orders, stocks, and Dashboard preview remain untouched.

## Open Questions

None. Business semantics, API shape, transaction snapshot, role, bounds, and list limits are resolved for this change.
