# Tasks

## 1. TDD Red: Tests and Test Doubles

- [x] 1.1 Extend `FakeCategoryRepository` with configurable direct-child and product-association checks, deletion tracking, and cancellation-aware call recording; verify the fake compiles against the current repository contract without adding production-only data.
- [x] 1.2 Add `DeleteAsync_Should_Succeed_When_Category_Is_Empty` to `CategoryServiceTests`, asserting the category is marked for deletion, `SaveChangesAsync` is called exactly once, and the transaction commits; verify the new test fails before the deletion contract exists.
- [x] 1.3 Add service tests for a missing ID, a category with subcategories, and a category with associated products; verify expected `404`, `400`, and `409` outcomes respectively, no deletion on blocked paths, and no save on missing or blocked paths.
- [x] 1.4 Add direct controller tests for DELETE result mapping and cancellation-token forwarding; verify success maps to `204 No Content`, validation to `400`, missing target to `404`, and product conflict to `409`.
- [x] 1.5 Add API-host authorization tests for DELETE with no token, invalid/expired token, non-Admin roles, and a valid Admin token; verify `401`, `403`, and service invocation behavior through the existing JWT test host.

## 2. Repository Contract and Data Access

- [x] 2.1 Add `HasSubCategoriesAsync`, `HasProductsAsync`, and `Delete` to `ICategoryRepository` with default cancellation-token parameters; verify all existing repository fakes and decorators compile after the contract change.
- [x] 2.2 Implement direct-child and product checks in `CategoryRepository` with `AsNoTracking` and `AnyAsync`, and stage removal with `DbSet.Remove` without calling `SaveChanges`; verify repository code contains no service or controller dependencies and the repository-focused tests pass.
- [x] 2.3 Preserve existing restrictive foreign-key behavior and document no migration is required; verify `dotnet ef migrations has-pending-model-changes` reports no model change attributable to deletion.

## 3. Service Implementation

- [x] 3.1 Add `DeleteCategoryResult` and `DeleteAsync` to the category service contract with `CancellationToken cancellationToken = default`; verify the service and all test doubles compile with the new result states.
- [x] 3.2 Implement `CategoryService.DeleteAsync` using a service-owned transaction, tracked target lookup, subcategory check, product check, staged delete, one `SaveChangesAsync`, and commit; verify the TDD service tests pass and every repository/unit-of-work call receives the supplied token.
- [x] 3.3 Add rollback cleanup for not-found, blocked, persistence-failure, and cancellation paths, preserving cancellation propagation while using a non-cancelled token for cleanup; verify cancellation tests observe rollback and never report success.
- [x] 3.4 Translate expected concurrent deletion integrity conflicts into the safe product-conflict result without exposing provider or SQL details; verify a simulated persistence conflict returns `409` and leaves no partial mutation.

## 4. Controller and Authorization Implementation

- [x] 4.1 Add `DELETE /api/categories/{id:guid}` to `CategoriesController` with `[Authorize(Roles = nameof(RoleType.Admin))]` and response metadata for `204`, `400`, `404`, `409`, `401`, and `403`; verify the action only calls `ICategoryService.DeleteAsync`.
- [x] 4.2 Map `DeleteCategoryResult` to `NoContent`, `ValidationProblem`, and safe `ProblemDetails` responses with the specified subcategory and product messages; verify direct controller tests pass with no repository or DbContext access.
- [x] 4.3 Update the controller test fake service and API-host registrations for the new interface method; verify the full unit-test project builds with nullable and analyzer checks enabled.

## 5. Integration Verification and Handoff

- [x] 5.1 Run the focused category service and controller tests, then the complete backend unit-test project; verify all UC-CAT-04 tests pass and record any pre-existing baseline failure separately.
- [x] 5.2 Run the PostgreSQL-backed category API tests for empty deletion, child/product rejection, and authorization; verify persisted rows remain for rejected deletes and are absent only after a successful `204`.
- [x] 5.3 Build the backend and run `openspec validate delete-category-backend --strict`; verify there are no compile errors, no spec validation errors, and no unrequested migration or temporary files.

## Workflow follow-up

- Archive the change after implementation, review, and validation are complete.
- Verify the archived artifacts and, if desired, sync the category-catalog delta into the main specification before archiving.
