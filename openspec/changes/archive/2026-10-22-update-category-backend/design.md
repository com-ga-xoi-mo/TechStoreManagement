# Design

## Context

See [proposal.md](proposal.md) for motivation and [the category-catalog delta](specs/category-catalog/spec.md) for observable behavior.

Observed implementation:

- `CategoriesController` delegates create/tree requests to `ICategoryService`; no PUT exists.
- `CategoryService` already validates create names and slugs, generates Vietnamese slugs, maps `CategoryDto`, and saves through `IUnitOfWork`. Its primary-constructor dependencies are used directly; the update work will introduce `_categoryRepository` and `_unitOfWork` fields as requested.
- `CategoryRepository.GetByIdAsync` is read-only (`AsNoTracking`) and is also used by product creation. Changing its tracking contract globally would affect existing callers.
- The repository has no descendant traversal, update retrieval, excluded-ID uniqueness query, or hierarchy locking method.
- `Category` already has every editable property and inherits UTC timestamps from `BaseEntity`. Fluent configuration already provides `idx_categories_slug`, `idx_categories_parent_id`, and the nullable self-FK. No migration is necessary.
- `IUnitOfWork` already provides explicit transactions, rollback with tracker clearing, and conversion of PostgreSQL unique violations into `DuplicateKeyException`.
- Existing category tests use xUnit, FluentAssertions, `FakeCategoryRepository`, and `FakeUnitOfWork`, under `TechStore.UnitTests`. Follow these existing paths as explicitly requested.
- `Program.cs` currently has `UseAuthorization` but no JWT authentication registration or `UseAuthentication`; Identity has entities/configurations but no token issuer. The milestone assigns UC-CAT-03 to Admin, whereas the existing creation spec allows Admin or Manager. This change adds Admin-only update access and leaves the creation policy requirement intact. The existing lack of creation authorization is pre-existing spec drift, outside this change.

## Goals / Non-Goals

**Goals:** Make update rules directly testable without EF dependencies; preserve repository read contracts; prevent concurrent reparenting cycles; make HTTP results and authentication executable rather than relying on attributes alone.

**Non-Goals:** Login/refresh-token issuance, frontend edits, new schema constraints, category deletion, product reassignment, cascading active-state changes, unrelated service refactors, and changing existing category creation behavior.

## Decisions

### 1. Explicit update contract and service result

Add `UpdateCategoryRequest` in shared requests with nullable `Name`, `Slug`, `Description`, and `ParentId`, plus nullable `IsActive` so absence can be distinguished from false. The service requires a name and explicit active state. Description is treated safely: non-empty string updates description, empty string `""` or whitespace-only string clears description to null, while null or omitted description preserves the existing description to prevent accidental data loss. Null parent makes the category a root, and null/blank slug regenerates from the new name. This is a recorded contract choice balancing safety and usability.

Add `Task<UpdateCategoryResult> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)` to `ICategoryService`. Define the result alongside existing category service result records, with `IsSuccess`, `IsNotFound`, `Data`, `ValidationErrors`, `IsConflict`, and `ConflictReason`, plus factories `Success`, `NotFound`, `ValidationFailed`, and `Conflict`. Factories produce mutually exclusive outcomes. This follows the existing result pattern while distinguishing missing target from missing parent.

Reuse `CategoryDto`; preserve `Id` and `CreatedAt`. Only successful persistence returns a DTO. A missing target is checked before business validation so a well-formed request for a nonexistent target returns NotFound. Malformed JSON/type binding remains an API-layer 400.

Alternative: throwing business exceptions would move normal result handling away from the existing service contract; partial updates would leave null parent/description ambiguous.

### 2. Repository methods preserve the layer boundary

Extend `ICategoryRepository` and its implementation with:

| Method | Contract |
| --- | --- |
| `GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)` | Return a tracked category for service mutation, including inactive targets; not a row-lock method. |
| `ExistsBySlugExceptIdAsync(string slug, Guid excludedId, CancellationToken cancellationToken = default)` | Return whether another category owns the normalized slug, with no active filter. Preserve existing `ExistsBySlugAsync` unchanged. |
| `GetDescendantIdsAsync(Guid id, CancellationToken cancellationToken = default)` | Return `IReadOnlyList<Guid>` of all direct/indirect descendants, including inactive nodes, excluding the target itself. |
| `AcquireHierarchyUpdateLockAsync(CancellationToken cancellationToken = default)` | Acquire a database transaction-scoped advisory lock; requires an already-open caller-owned transaction. Does not start, save, or close transactions. |

Keep `GetByIdAsync` read-only. Ordinary validation queries use `AsNoTracking`, `AnyAsync`, and `ToListAsync`, forwarding the token. The descendant method loads only ID/parent-ID pairs with one read-only LINQ query, builds a child lookup, and traverses iteratively with a visited set. This avoids depth-dependent database round trips and infinite recursion on corrupt legacy graphs. It returns IDs, never DTOs or `IQueryable`.

Only repository code refers to EF/Npgsql or raw lock SQL. Services hold no DbContext/provider dependency. Stage edits through the tracked entity; no generic update/CRUD repository is introduced. Use `_dbContext` for the repository field and `_categoryRepository`/`_unitOfWork` for service fields.

Alternative: making the existing lookup tracked changes all readers; recursive SQL is unnecessary for the expected taxonomy size and would make traversal harder to maintain. The explicit lock is the sole SQL-specific hierarchy operation.

### 3. Serialize category updates before validating topology

An unlocked descendant check followed by one atomic save cannot prevent two concurrent root categories from each selecting the other as parent. Locking only the target row also permits that write skew. Therefore each update opens an `IUnitOfWork` transaction and acquires one transaction-scoped PostgreSQL advisory lock shared by all category updates, before reading the target or hierarchy.

Use a stable, documented Catalog hierarchy lock key derived from the semantic namespace `techstore.catalog.category-hierarchy`, identical across application instances. It is infrastructure coordination, never a hardcoded test entity ID. Use a transaction-level lock so commit/rollback releases it automatically. PostgreSQL's default Read Committed isolation makes reads after waiting observe the preceding update's committed hierarchy. Keep lock acquisition separate from the subsequent read queries.

Serialize the entire update path for a simple, enforceable protocol at this low-volume administrative endpoint. Creation can remain unchanged: adding a fresh node under an existing parent cannot close a cycle among existing nodes; the unique constraint still arbitrates concurrent slug claims. Future operations that reparent existing categories must use the same protocol.

Service flow:

1. Begin transaction, await the repository hierarchy lock, then retrieve the tracked target. Roll back and return NotFound if absent.
2. Validate trimmed name length, required active state, and normalized/generated slug into local values without mutating the target.
3. For non-null parent, reject own ID; verify parent existence through the read-only lookup; obtain descendant IDs and reject a descendant parent. No active-state filter is applied.
4. Return field-level validation errors after rollback when invalid. Otherwise check slug uniqueness excluding the target ID; roll back and return Conflict when occupied.
5. Assign the five editable fields and `UpdatedAt = DateTime.UtcNow` only after all checks pass. Call `SaveChangesAsync` exactly once, then `CommitAsync`, then map the saved entity to `CategoryDto`.
6. Catch `DuplicateKeyException`, roll back, and return a generic safe Conflict. Never append `ex.Message`, SQL, or provider detail. Let unrelated exceptions propagate after rollback.
7. Forward the caller token through normal repository, save, and commit calls. On cancellation or an exception, perform rollback cleanup with a non-cancelled cleanup token so the cancelled request cannot prevent cleanup, then propagate the original cancellation/exception. Never convert cancellation into a business failure.

The explicit transaction is required by the hierarchy lock even though there is one save. The service owns every transaction boundary and makes no external calls while holding the lock.

Alternative: one implicit transaction is adequate for the row write and slug constraint but insufficient for the hierarchy invariant; serializable transactions would require expanding Unit of Work isolation/retry contracts beyond this use case.

### 4. Share normalization without changing create semantics

Reuse `CategoryService.GenerateSlug`, which already removes combining marks, handles `đ`/`Đ`, lowercases, removes unsupported characters, and collapses whitespace/hyphens. Update uses the existing regex after either explicit normalization or generation. Unlike the current create flow's random fallback, update returns a slug field error when generation yields an empty value. Keep that distinction explicit and regression-test creation.

Whitespace-only explicit slug counts as absent. Check uniqueness against every other ID, including inactive categories, after normalization. Validation errors use existing camelCase keys (`name`, `slug`, `parentId`, `isActive`). For description, preserve the stored value when the request is null/omitted; trim supplied text and convert empty/whitespace-only text to null, as specified in Decision 1 and the delta.

Alternative: copying transliteration into a second implementation risks diverging Vietnamese handling; broadening this change to alter creation fallback is unnecessary.

### 5. Thin HTTP mapping with working JWT/RBAC

Add an async PUT action using `{id:guid}`, `UpdateCategoryRequest`, and `CancellationToken cancellationToken = default`. The action calls only `_categoryService.UpdateAsync` and maps results:

| Outcome | HTTP response |
| --- | --- |
| Success | 200 with `CategoryDto` |
| Target missing | 404 ProblemDetails |
| Invalid input or parent | 400 ValidationProblemDetails with field errors |
| Duplicate slug / unique race | 409 ProblemDetails with safe detail |
| Missing, invalid, expired JWT | 401 from authentication |
| Valid non-Admin identity | 403 from authorization |

Declare response metadata for Swagger, including 401/403. Apply Admin authorization to the new action, not the entire controller, to avoid changing tree/create access in this change.

Register JWT Bearer authentication and authorization in `Program.cs`; order `UseAuthentication` before `UseAuthorization`. Add the matching .NET 10 JWT Bearer package as an explicit backend dependency. Bind issuer, audience, and signing-key configuration from external configuration (`Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`); validate issuer, audience, signature, lifetime, and role claims. Missing/invalid configuration must fail startup clearly; no secret/default signing key or authentication bypass. Configure claim mapping so role `Admin` is recognized consistently with `RoleType.Admin`.

The token issuer itself remains outside scope. Authorization tests generate temporary test JWTs with test-only configuration; production does not acquire a fake login route or synthetic identity.

Alternative: adding only an authorization attribute would leave requests without a working authentication handler in the current application.

### 6. TDD and verification boundaries

Write the seven explicitly requested tests in `CategoryServiceTests.cs` first, extend hand-written fakes, observe Red, then add minimal contracts/stubs if needed to turn missing symbols into runnable failing assertions. Only then implement the business behavior and reach Green. Repeat Red -> Green for additional edge cases; refactor with tests kept green.

Successful tests assert all fields, UTC `UpdatedAt` bounded by before/after timestamps, preserved creation timestamp, and one save/commit. Validation/not-found/precheck-conflict tests assert zero saves, rollback, and no mutation. Save-time duplicate tests inject `DuplicateKeyException` through the existing fake hook and assert safe Conflict, rollback, and no commit. Token-recording/cancellation-aware fakes verify propagation and exception cleanup, without pretending to prove database isolation.

Keep fake data in test Arrange sections and use generated IDs. Extend descendant traversal in the fake using its seeded graph, never test-specific canned descendant IDs. Fakes verify business decisions and orchestration; only PostgreSQL checks prove EF tracking, durable writes, unique-constraint races, lock serialization, and rollback.

For HTTP behavior, add focused controller tests with a hand-written service fake and a minimal in-process authentication pipeline test using `Microsoft.AspNetCore.TestHost` in the existing unit-test project. Resolve the real JWT validation and update action, replace only the domain service, and submit generated signed tokens. This verifies 401/403/200 without a database or real token issuer. Controller calls alone cannot verify middleware.

## Risks / Trade-offs

- Global category-update serialization increases contention -> Keep transactions short, honor cancellation, and avoid external calls. Administrative update frequency is low; monitor before considering finer-grained locking.
- Descendant traversal loads the hierarchy's ID links -> One query, minimal projection, O(n) processing; use a recursive repository query later if taxonomy scale justifies it.
- Advisory locks require a common writer protocol -> Document the key and precondition; future reparenting paths must acquire it. Direct database edits bypass application invariants.
- Authentication configuration is currently absent -> Supply external JWT settings before startup, validate explicitly, and use dedicated test configuration. Login implementation is not a hidden dependency of service tests.
- Existing create error handling includes exception text and create authorization does not meet its spec -> Avoid copying these patterns into update; leave unrelated remediation separate.
- Fakes do not emulate EF tracking or transaction isolation -> Require real PostgreSQL verification before calling the implementation complete.

## Migration Plan

1. Implement test-first in the apply phase; no production code is changed by this proposal.
2. Configure external JWT issuer/audience/signing material, restore the backend dependency, and deploy the API. No schema/data migration or frontend deployment is required.
3. Verify Admin success, authentication/role failures, persistence on a fresh read, safe duplicate conflict, and reciprocal reparenting under concurrent requests.
4. Roll back by reverting the application change. Existing category rows and unique/FK indexes remain compatible. Removing the endpoint does not undo already accepted category edits.
