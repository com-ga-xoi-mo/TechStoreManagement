# Tasks

Implementation progress is tracked below. Follow [design.md](design.md) and [the normative delta](specs/category-catalog/spec.md). Red/Green evidence and the user-accepted baseline exception are recorded in Verification notes; no temporary Markdown, helper scripts, or unrelated files are required.

## 1. Red: Required service tests before implementation

- [x] 1.1 Run `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj` to establish the existing baseline; verify and report any pre-existing failure before adding update behavior.
- [x] 1.2 Write `UpdateAsync_Should_Succeed_When_Valid` in `tests/Backend.UnitTests/CategoryServiceTests.cs`; assert all five fields, original ID/CreatedAt, UTC UpdatedAt between captured bounds, and one save/commit.
- [x] 1.3 Write `UpdateAsync_Should_Return_NotFound_When_Id_DoesNotExist`; verify the NotFound result, zero saves, no added category, and transaction cleanup.
- [x] 1.4 Write `UpdateAsync_Should_Fail_Validation_When_Name_Is_Empty`; use null/empty/whitespace test inputs and verify the `name` error, zero saves, and unchanged original fields/timestamp.
- [x] 1.5 Write `UpdateAsync_Should_Fail_Validation_When_ParentId_Equals_Own_Id`; verify the `parentId` error, zero saves, and no mutation.
- [x] 1.6 Write `UpdateAsync_Should_Fail_Validation_When_ParentId_Is_Descendant`; arrange direct and multi-level descendants, including inactive descendants, and verify rejection without saves or mutation.
- [x] 1.7 Write `UpdateAsync_Should_Return_Conflict_When_Slug_Already_Exists_On_Other_Category`; include an inactive owner and verify Conflict, zero saves, and unchanged target.
- [x] 1.8 Write `UpdateAsync_Should_Allow_Keeping_Same_Slug_For_Current_Category`; verify a successful update and one save despite the target already owning the slug.
- [x] 1.9 Extend `FakeCategoryRepository` for tracked retrieval, excluded-ID uniqueness, graph-derived descendants, and lock-call tracking; extend `FakeUnitOfWork` only where ordering/token evidence is needed. Verify fixtures come from Arrange data and generated IDs, with no production test data or mocking dependency.
- [x] 1.10 Run the update filter (`dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~CategoryServiceTests.UpdateAsync"`); observe missing-contract failures first, then introduce only minimal request/result/interface signatures and nonfunctional stubs to obtain runnable Red assertions. Verify update behavior is still failing before business implementation begins.

## 2. Repository foundations and PostgreSQL verification

- [x] 2.1 Implement `GetTrackedByIdAsync` and `ExistsBySlugExceptIdAsync` in the category repository with default/forwarded cancellation tokens; verify in PostgreSQL that the new lookup is tracked, existing `GetByIdAsync` is read-only, own-ID exclusion works, and inactive slug owners are included.
- [x] 2.2 Implement `GetDescendantIdsAsync` using an `AsNoTracking` ID/parent projection and iterative visited-set traversal; verify direct/deep/inactive descendants and unrelated-node exclusion using a seeded PostgreSQL fixture, and document the read-only contract in repository XML comments.
- [x] 2.3 Implement `AcquireHierarchyUpdateLockAsync` using a stable named transaction-level advisory lock inside the repository; document the caller transaction precondition and verify with two database transactions that the second waits until the first commits or rolls back. Verify the repository never saves or manages transaction boundaries.

## 3. Green: Update service and additional Red -> Green cases

- [x] 3.1 Implement `UpdateCategoryRequest`, the mutually exclusive `UpdateCategoryResult` factories, and `CategoryService.UpdateAsync` beyond the Red stubs: transaction, lock-before-read, NotFound, name/active-state validation, parent existence, self/descendant rejection, excluded-ID uniqueness, delayed mutation, one save/commit, DTO mapping, and UTC timestamp. Verify all seven required tests pass.
- [x] 3.2 Before extending edge behavior, add failing tests for trimmed name lengths 200/201, missing/null IsActive, null parent to root, empty string description clearing to null, null/omitted description retaining existing description, missing parent, and an inactive unrelated parent; implement the required behavior and verify these cases pass without altering unrelated fields on rejection.
- [x] 3.3 Add Red tests for explicit slug normalization, invalid slug formats, absent/null/blank slug regeneration after name change, Vietnamese diacritics and `đ`/`Đ`, generated-slug collisions, and a name yielding no slug characters; reuse `GenerateSlug`, validate the generated value, and verify Green without changing create fallback semantics.
- [x] 3.4 Add Red tests with `FakeUnitOfWork.ExceptionToThrowOnSave = new DuplicateKeyException(...)`, including a sensitive exception message; implement rollback and generic Conflict handling and verify no exception/SQL detail appears in the result, no commit occurs, and one save was attempted.
- [x] 3.5 Add Red tests recording token propagation and operation order, plus cancellation during repository/save work and an unrelated save exception; implement safe rollback cleanup and verify original exceptions propagate, cancellation is not a business result, and no commit follows failure. Verify ordering is begin -> lock -> reads -> save -> commit.
- [x] 3.6 Refactor CategoryService dependencies to `_categoryRepository`/`_unitOfWork` and repository dependency to `_dbContext`; share only useful normalization/mapping code, retain file-scoped namespaces and async conventions, and verify the full backend unit suite, including existing product callers and category create/tree tests, passes apart from the pre-existing DataSeederTests failure explicitly accepted by the user.

## 4. Red -> Green: Controller and API documentation

- [x] 4.1 Write failing controller tests with a hand-written `ICategoryService` fake for 200/400/404/409, category DTO contents, field-level errors, safe ProblemDetails, and forwarded cancellation/route ID; verify Red before adding the PUT action.
- [x] 4.2 Implement the thin `PUT /api/categories/{id:guid}` action with `UpdateCategoryRequest` and `CancellationToken cancellationToken = default`, delegating only to `_categoryService.UpdateAsync`; map each result and declare Swagger response types. Verify controller tests pass and no repository/DbContext/provider dependency enters the controller/service layer.
- [x] 4.3 Update the existing `backend/TechStore.Api.http` with a complete PUT example and expected outcomes, and `backend/Catalog/README.md` with replacement/null-field semantics and required IsActive. Verify examples match the route, field types, regenerating-slug behavior, response codes, and Admin requirement.

## 5. Red -> Green: JWT and Admin enforcement

- [x] 5.1 Add an explicit .NET 10-compatible `Microsoft.AspNetCore.TestHost` test dependency and failing in-process pipeline tests using signed test JWTs: missing/invalid/expired/wrong-issuer/wrong-audience tokens yield 401; Manager/SalesStaff/WarehouseStaff yield 403; Admin reaches the service. Verify denied requests never invoke the service.
- [x] 5.2 Add the backend JWT Bearer dependency, configure authentication/authorization with validated external issuer/audience/signing material, use authentication before authorization, and restrict only the new action to Admin. Verify the pipeline tests turn Green and missing JWT configuration fails clearly without fallback credentials or a bypass.
- [x] 5.3 Document `Jwt__Issuer`, `Jwt__Audience`, and `Jwt__SigningKey` deployment configuration in the existing backend module documentation without real secrets; verify instructions acknowledge token issuance is external/out of scope and test keys exist only in test code.

## 6. Integration checks and completion evidence

- [x] 6.1 Run `dotnet build backend/TechStore.Api.csproj` and `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj`; verify the build and tests pass apart from the pre-existing DataSeederTests failure explicitly accepted by the user, and report the executed test counts and Red -> Green evidence.
- [x] 6.2 Against disposable PostgreSQL fixtures, verify a successful PUT persists all fields/UTC UpdatedAt on a fresh context read, and validation failures leave stored fields unchanged; verify runtime Swagger and HTTP responses conform to the delta and request examples.
- [x] 6.3 Exercise a real slug race between create and update after update's uniqueness precheck; verify `IUnitOfWork` translates the unique violation to safe HTTP 409, the attempted update is rolled back, and original persisted fields remain unchanged.
- [x] 6.4 Run concurrent reciprocal reparenting of two roots through independent requests/DbContexts; verify one update succeeds, the other observes the committed hierarchy and rejects the descendant parent with 400, and the stored graph is acyclic. Verify lock-wait cancellation releases/cleans up without an uncommitted write. Record the observed outcomes in the implementation report; do not claim fakes prove this behavior.
- [x] 6.5 Review `git diff --check`, `git diff`, and `git status --short`, then run `openspec validate update-category-backend --strict`; verify changes remain scoped, no frontend/migrations/test-only production data/secrets/unrelated scripts or temporary Markdown exist, and the canonical delta remains discoverable. Report any unavailable PostgreSQL checks explicitly before declaring implementation complete.

## Verification notes

- Verified on 2026-10-22 with .NET SDK 10.0.402 and PostgreSQL 16.
- Baseline before implementation: 91 passed, 1 failed. `DataSeederTests.DataSeeder_Should_Seed_Data_And_Be_Idempotent` reads the existing local database and expects the admin's fixed seed ID; the existing admin has another ID. The user explicitly chose to keep category scope and document this failure. Neither that test, its seeder, nor the existing admin data was changed.
- TDD Red evidence: seven required test names produced 10 failing cases against the update stub; all 35 expanded update cases failed before implementation. Three PostgreSQL repository cases failed against repository stubs. Controller tests failed for the missing action. JWT pipeline tests then showed 12 failures/1 pass with no access enforcement or configuration validation, before the authentication implementation.
- Green evidence: 35 update-service cases, 4 controller cases, 14 authentication/configuration cases, and 7 real PostgreSQL cases pass. Existing category create/tree and product-service cases also pass. Explicit EF Relational 10.0.12 keeps the backend and test runtime aligned, eliminating the pre-existing assembly-version warning encountered during baseline.
- Final backend build (`dotnet build backend/TechStore.Api.csproj --no-restore`): 0 warnings, 0 errors. Final full suite with `TECHSTORE_TEST_CONNECTION` enabled: 151 passed, 1 accepted baseline failure, 0 skipped, 152 total. The suite's exit code remains nonzero because the baseline failure is retained.
- PostgreSQL verification used isolated generated databases: tracked versus read-only queries, inactive descendants, lock serialization, HTTP PUT persistence/validation/Swagger, real create-versus-update unique race returning safe 409 with rollback, concurrent reciprocal reparenting returning one 200 and one 400, and lock-wait cancellation with cleanup all passed. No test databases remained afterward. Timestamp assertions account for PostgreSQL's microsecond storage precision.
- `git diff --check` and `openspec validate update-category-backend --strict` passed. No migrations or frontend changes were needed. Runtime requires externally supplied JWT issuer/audience/signing-key configuration as documented in `backend/Catalog/README.md`.
