# Tasks

## 1. Confirm the backend contract

- [x] 1.1 Inspect the existing Customer and CustomerLoyaltyPoint entities/schema; verify the read DTOs expose persisted fields only and introduce no award formula or completion hook.
- [x] 1.2 Inspect the current Identity JWT role claim and seeded Admin role; verify the exact role value and claim mapping that Customers endpoints must use for authorization.

## 2. Customer read APIs (UC-CUST-01, 02, 05)

- [x] 2.1 Add shared Customer summary/detail, persisted point-history, and search request contracts; verify the backend and shared projects compile and public fields match `customer-management/spec.md`.
- [x] 2.2 Write failing `CustomerServiceTests` for name/phone search, tier/activity filters, pagination, stable ordering, profile/persisted point-history lookup, and unknown IDs; verify the tests fail for missing behavior before implementation.
- [x] 2.3 Extend `ICustomerRepository` and repository read queries with module-internal criteria and `AsNoTracking`; implement Customer service mapping and verify the tests from 2.2 pass.
- [x] 2.4 Add paginated purchase history by validating the customer and reusing `IOrderRepository.SearchAsync(CustomerId=...)`; verify tests cover only that customer's orders, inactive customers, unknown IDs, and paging.
- [x] 2.5 Add controller read routes for list, profile, point history, and purchase history; verify endpoint-level tests or live API calls return the specified 200/400/404 shapes and JSON field names.

## 3. Customer write APIs (UC-CUST-03, 04, 06)

- [x] 3.1 Add create/update request contracts and write failing tests for required name/phone, email, future birth date, aggregated field errors, server-owned points/tier/activity, and missing IDs; verify these tests fail before implementation.
- [x] 3.2 Implement tracked customer write repository methods and service create/update behavior; verify 201/200/400/404 tests pass and no repository method calls `SaveChangesAsync`.
- [x] 3.3 Write tests for pre-existing and concurrent duplicate phones; implement precheck plus `DuplicateKeyException` mapping for the phone unique index and verify both paths return 409 without overwriting a profile.
- [x] 3.4 Write tests for first and repeated deactivation with linked orders and point entries; implement soft deactivation and verify 204/404 behavior, preserved history, and rejection of the inactive customer by the existing order-creation rule.
- [x] 3.5 Add controller write routes and update the existing `backend/TechStore.Api.http` requests if useful; verify Swagger or equivalent HTTP calls cover valid and invalid create/update/deactivate flows against a local database.

## 4. Authorization, verification, and handoff

- [x] 4.1 Enforce the Admin role on every Customers route using the existing JWT middleware; verify Admin=success, unauthenticated=401, and non-Admin=403 through HTTP tests without a fixed-user fallback.
- [x] 4.2 Run `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj` and `dotnet build backend/TechStore.Api.csproj`; verify all targeted and regression tests pass and no unrelated project file is changed.
- [x] 4.3 Start local PostgreSQL and the API, execute the six customer use cases through Swagger/HTTP, and verify persisted profile, purchase history, existing point ledger reads, and error responses against the customer delta spec.
- [x] 4.4 Inspect `git diff` and the EF model against the existing schema; verify no migration is needed. If a schema change is required, update design/specs and generate it only with `dotnet ef migrations add`, then verify the generated migration and snapshot.

## Workflow follow-up

- Create a separate OpenSpec change for Customer C# Markup UI after the backend contract is stable, unless the team explicitly folds that work into a later full-stack change.
- Archive this backend change only after every required task and relevant manual check is complete and the implementation matches the customer delta spec.
- Push or open a PR targeting `dev` only if Vy requests publication; Bao An reviews and merges it.
