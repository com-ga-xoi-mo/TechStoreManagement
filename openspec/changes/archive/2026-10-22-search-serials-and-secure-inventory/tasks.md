# Tasks

## 1. TDD Red: Search and Security Tests

- [x] 1.1 Extend `FakeSerialImeiRepository` to apply case-insensitive keyword matching across SerialNumber and Imei, while retaining ProductId, VariantId, Status, and pagination behavior; verify the fake compiles with the planned criteria contract and contains no production fallback IDs.
- [x] 1.2 Add `SerialImeiService` tests for an exact 15-digit IMEI keyword, a serial-number keyword with different casing, and a keyword combined with Status; verify filtered items and `TotalCount` are correct before production search changes are implemented.
- [x] 1.3 Add a service test for keyword plus ProductId/VariantId and paging; verify predicates are applied before page slicing and page metadata describes the filtered result.
- [x] 1.4 Add inventory API authorization tests for unauthenticated reads/writes, disallowed roles, and allowed roles; verify `401`/`403` responses occur before the fake service records a call.
- [x] 1.5 Add write-claim tests with a generated GUID in `NameIdentifier`, a generated GUID in `sub`, missing claims, and malformed claims; verify the exact claimed GUID reaches the service and no fixed GUID is used.

## 2. Search Contract and Service Mapping

- [x] 2.1 Add nullable `Q` to `SerialImeiFilterRequest` and nullable keyword storage to `SerialImeiSearchCriteria`; verify request binding and all existing criteria call sites compile.
- [x] 2.2 Normalize blank or whitespace-only `Q` to no filter in `SerialImeiService.GetSerialsAsync`, and pass nonblank keywords into the criteria; verify the new service search tests pass and cancellation tokens remain propagated.
- [x] 2.3 Preserve existing page/page-size clamping and DTO mapping while adding the keyword path; verify existing serial listing and batch-import service tests remain green.

## 3. Repository Search Implementation

- [x] 3.1 Add a database-side case-insensitive contains predicate over nullable SerialNumber or Imei, combined with ProductId, VariantId, and Status filters; verify the predicate is applied before `CountAsync`, `Skip`, and `Take`.
- [x] 3.2 Add PostgreSQL-backed search coverage for IMEI, serial, case-insensitive matching, combined status filtering, and pagination; verify returned totals and pages match the filtered rows.
- [x] 3.3 Confirm repository queries remain the only AppDbContext access and no schema/migration changes are introduced; verify `git diff` contains no migration or unrelated inventory files.

## 4. Inventory Authorization and Actor Identity

- [x] 4.1 Add `[Authorize(Roles = "Admin,Manager,SalesStaff,WarehouseStaff")]` to GET stocks and GET serials, and `[Authorize(Roles = "Admin,WarehouseStaff")]` to stock initialization and serial batch import; verify endpoint metadata exposes the intended role policies.
- [x] 4.2 Replace the fixed GUID fallback in `InventoryController` with claim extraction that checks `NameIdentifier` then `sub`, parses a GUID, and returns HTTP 401 when neither is valid; verify no hardcoded user ID remains in the controller or new tests.
- [x] 4.3 Ensure write actions reject missing or malformed claims before calling inventory services and pass the parsed actor ID to initialization/import; verify generated-claim authorization tests pass.
- [x] 4.4 Update inventory controller test doubles/hosts and add allowed Admin/WarehouseStaff plus rejected Manager/SalesStaff cases for both write endpoints; verify service call counts and actor IDs.

## 5. Verification and Handoff

- [x] 5.1 Run focused `SerialImeiService` and inventory controller authorization tests; verify all UC-IMEI-01 search and security tests pass.
- [x] 5.2 Run PostgreSQL-backed serial search and inventory authorization tests with a disposable database; verify case-insensitive results, filters, pagination, and persisted behavior.
- [x] 5.3 Build the backend and full unit-test project, recording any pre-existing baseline failures separately; verify no compile errors or analyzer regressions.
- [x] 5.4 Run `openspec validate search-serials-and-secure-inventory --strict` and `git diff --check`; verify all artifacts are valid and no temporary scripts, hardcoded identifiers, or migration files are introduced.

## Workflow follow-up

- Archive the change after implementation, review, and validation are complete.
- Sync the inventory-management delta into the main spec during archive when approved.
