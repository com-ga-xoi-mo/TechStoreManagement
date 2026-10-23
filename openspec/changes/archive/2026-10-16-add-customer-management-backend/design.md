# Design

## Context

See `proposal.md` for motivation and the customer-management delta spec for the behavioral contract. The existing Customers module contains mapped entities, the phone unique index, and a read-only `ICustomerRepository.GetByIdAsync` used by Orders. `IOrderRepository.SearchAsync` already filters by customer ID. JWT authentication and role policies are not yet wired in `Program.cs` on this branch.

## Goals / Non-Goals

**Goals:** Keep customer validation and mapping in a service, database access in repositories, and all writes through `IUnitOfWork`. Provide stable paginated DTOs for customer profiles, existing loyalty data, and purchase history so a later client can consume them.

**Non-Goals:** Loyalty-point calculation or award triggers, point spending, automatic refunds or point reversal, voucher redemption, migration of historical completed orders, frontend implementation, and changing existing order pricing or stock rules.

## Decisions

### 1. Customer API and DTO boundaries

`CustomersController` calls `ICustomerService` only. The service accepts shared request types, validates them, converts them to module-internal search criteria, calls repository interfaces, and maps results to shared DTOs. `ICustomerRepository` gains search, duplicate-phone check, tracked lookup for writes, and `Add`; its existing read-only `GetByIdAsync` remains compatible with Orders. A loyalty-history read repository exposes a paged `AsNoTracking` query and does not stage writes.

The API uses `GET /api/customers`, `GET /api/customers/{id}`, `GET /api/customers/{id}/loyalty-points`, `POST /api/customers`, `PUT /api/customers/{id}`, `GET /api/customers/{id}/orders`, and `DELETE /api/customers/{id}`. `PUT` replaces the five editable contact fields; it never accepts points, tier, or active state. `DELETE` is idempotent deactivation, not physical deletion. This keeps order and ledger references intact. A `PATCH` with ambiguous null-versus-omitted semantics was rejected for this first API.

Shared `CustomerDto` contains ID, full name, phone, optional email/birth date/address, persisted point balance, tier as a string, activity, and timestamps. Search can use a lighter `CustomerSummaryDto`. Point entries use `CustomerLoyaltyPointDto`; purchase history reuses `OrderSummaryDto` inside `PagedResultDto<T>`. A client follows the existing `/api/orders/{id}` route for full order detail.

### 2. Validation, search, and persistence

The service trims text, requires nonblank name and phone, validates email syntax and birth date, and reports all field errors together in a 400 validation response with camelCase keys. It does not impose a Vietnam-mobile-only regex or silently rewrite `+84` numbers without an agreed business rule. Search escapes `%`, `_`, and `\\` before using case-insensitive containment; tier values are parsed from enum names, not numeric values. Paging follows the existing 1/20/100 convention, with stable ID tie-breakers.

Before create/update, the service checks phone availability; the database unique index remains the final concurrency guard. `IUnitOfWork.SaveChangesAsync` maps PostgreSQL `23505` to `DuplicateKeyException`; the service maps only the customer-phone constraint to 409 and does not hide unrelated database failures. No existing entity or migration needs modification for the current contract.

### 3. Purchase history reuses Orders queries

`ICustomerService` verifies the customer exists, then calls the already available `IOrderRepository.SearchAsync` with `CustomerId`, `CreatedAt:desc`, and the normalized page. This avoids a second SQL implementation of order search or a new dependency from the Orders controller into Customers. It includes inactive customers, since their purchase records remain valid.

### 4. Loyalty is a read-only customer view

Customer endpoints read `Customer.LoyaltyPoints`, `Customer.Tier`, and `CustomerLoyaltyPoint` ledger rows with `AsNoTracking`. The service maps the persisted values without recalculating them. It does not open a transaction, lock a customer for an award, append a ledger row, or call `OrderService`.

The Orders workstream may later define and implement how completed orders write loyalty data. That future behavior is intentionally not part of this change, so no point rate, tier threshold, deactivation-after-order rule, or completion hook is invented here.

### 5. Customer authorization uses the Identity foundation on dev

The milestone marks Admin as the actor for UC-CUST-01..06. The current dev branch configures JWT Bearer validation, `UseAuthentication()`, `UseAuthorization()`, and role claims issued by `AuthService` (`ClaimTypes.Role`, exact value `Admin`). Each Customers endpoint must explicitly enforce the Admin role (for example, `[Authorize(Roles = "Admin")]`) and be tested through the real middleware for Admin success, unauthenticated 401, and non-Admin 403. Authentication middleware being registered does not protect a controller unless authorization metadata or a fallback policy applies. Do not copy the fixed acting-user fallback from Orders into Customers.

## Risks / Trade-offs

- **Orders writes are not yet defined** → Keep this change read-only for loyalty and rely on the existing entity/schema contract; let the Orders owner propose a separate change if accrual is approved.
- **Endpoint left without role metadata** → Explicitly protect every Customers route and verify 401/403 behavior through the real middleware before claiming the API is complete.
- **Historical data may contain empty loyalty history** → Return an empty paginated history with the persisted balance/tier rather than inventing entries.
- **Unit fakes cannot prove database query behavior** → Add an opt-in PostgreSQL-backed API check for ordering, inactive-history reads, and duplicate-phone constraints.

## Migration Plan

No schema migration is planned. Land the customer contracts/service/repositories and their tests on the Dev 5 branch, protect the controller routes with the existing Identity authorization, then run targeted tests, API checks, and real PostgreSQL verification before a PR to `dev`. If implementation later requires a schema change, generate its timestamped migration with `dotnet ef migrations add`, review its Up/Down and snapshot, and update this design before applying it. Do not rewrite prior migrations.
