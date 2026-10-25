# Design

## Context

The category module already follows `CategoriesController -> ICategoryService -> ICategoryRepository -> AppDbContext`. Category updates use an explicit `IUnitOfWork` transaction and cancellation-token propagation; repository methods stage changes and never save. The database already protects category relationships with foreign-key restrictions, while products reference their category through `Product.CategoryId`.

## Goals / Non-Goals

**Goals:**

- Add one transactional deletion workflow that checks the target, direct children, and product associations before staging removal.
- Keep all EF Core queries inside `CategoryRepository`, with read checks using no tracking and a tracked target only when it will be removed.
- Preserve consistent HTTP outcomes and Admin-only authorization at the controller boundary.
- Make unit tests deterministic with the existing hand-written fakes and add API-level authorization/mapping coverage.

**Non-Goals:**

- No cascade deletion of subcategories, products, variants, or other aggregates.
- No soft-delete flag, archival state, schema migration, or change to existing create/update/tree behavior.
- No direct repository or `AppDbContext` access from controllers or services outside the defined repository contract.

## Decisions

### Transaction owned by the service

`CategoryService.DeleteAsync` will begin an `IUnitOfWork` transaction, load the category for mutation, run both integrity checks, call repository `Delete`, save once, and commit. Missing targets and blocked checks roll back before returning their result. This keeps validation and removal atomic and follows the existing update transaction pattern. A non-transactional delete was rejected because a concurrent child/product association could race the checks.

### Repository checks express intent

`ICategoryRepository` will expose `HasSubCategoriesAsync(Guid, CancellationToken)`, `HasProductsAsync(Guid, CancellationToken)`, and `Delete(Category)`. The implementation will use `AsNoTracking().AnyAsync` over `Categories` and `Products`, and `DbSet.Remove` only to stage the tracked entity. Generic queries or service-level EF access were rejected because they violate the repository boundary.

### Explicit result contract

`DeleteCategoryResult` will distinguish success, not found, subcategory validation failure, and associated-product conflict. The controller will map these to `NoContent`, `NotFound`, `BadRequest`, and `Conflict` while keeping the exact business messages in the response detail. A result contract is preferred over exceptions for expected business conditions; exceptions remain available for unexpected persistence failures and cancellation.

### Authorization remains declarative

The action will use `[Authorize(Roles = nameof(RoleType.Admin))]`, matching the existing update endpoint and shared JWT pipeline. Authorization is evaluated before the service is called, so unauthorized requests cannot acquire a transaction or mutate state. Tests will cover absent/invalid tokens, non-admin roles, and an Admin success path through `CategoryApiHost`.

### Race protection relies on transaction plus database constraints

The service will perform checks and stage removal in one transaction. Existing restrictive foreign keys remain the final guard if a product or child is associated concurrently; the implementation will translate the resulting expected deletion conflict into the safe `409` outcome without exposing provider or SQL details. No schema change is needed.

## Risks / Trade-offs

- [Risk] A concurrent insert can occur after the pre-check and before commit. → Keep the transaction short, retain restrictive foreign keys, and map the expected persistence conflict to the safe conflict result.
- [Risk] A blocked deletion may leave a transaction open if an early return bypasses cleanup. → Use a `committed` flag and unconditional rollback in `finally`, passing `CancellationToken.None` during cleanup.
- [Risk] Controller tests can verify status mapping without proving database authorization middleware. → Use the existing `CategoryApiHost` for JWT integration cases and direct controller tests for service-result and cancellation forwarding.

## Migration Plan

1. Deploy the API changes and tests; no database migration is required.
2. Verify the DELETE endpoint with an empty category, a category with children, and a category with products before enabling client UI actions.
3. Roll back by deploying the previous API version; existing data and foreign-key restrictions remain intact.
