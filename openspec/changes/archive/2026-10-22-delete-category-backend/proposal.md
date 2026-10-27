# Proposal

## Why

The catalog currently has category creation, tree retrieval, and update behavior, but no safe deletion workflow. A direct delete without integrity checks could orphan or invalidate the category hierarchy and leave products associated with a removed category. UC-CAT-04 adds an explicit, authorization-protected deletion path that only removes empty categories and reports actionable failures.

## What Changes

- Add `DELETE /api/categories/{id}` for category deletion.
- Restrict the endpoint to authenticated `Admin` users and preserve standard `401`/`403` behavior.
- Add service and repository contracts for existence, direct-child, and associated-product checks, plus staged deletion.
- Open a unit-of-work transaction, reject missing categories with `404`, reject categories with subcategories with `400`, and reject categories with products with `409`.
- Return `204 No Content` only after an empty category is staged and persisted successfully.
- Add `DeleteCategoryResult` outcome mapping, cancellation-token propagation, and focused unit/controller authorization tests using the existing fake repository and unit of work.

## Capabilities

### New Capabilities

None. Safe deletion extends the existing category-catalog capability.

### Modified Capabilities

- `category-catalog`: add the safe category deletion contract, integrity constraints, HTTP outcomes, and Admin-only authorization for UC-CAT-04.

## Impact

- `backend/Catalog/CategoriesController.cs`: expose the DELETE action and map service outcomes to `204`, `400`, `404`, and `409` responses.
- `backend/Catalog/Services/ICategoryService.cs` and `CategoryService.cs`: add deletion orchestration, transaction handling, and business-rule validation.
- `backend/Catalog/Repositories/ICategoryRepository.cs` and `CategoryRepository.cs`: query direct children and products through `AppDbContext` and stage `Remove` without saving.
- `tests/Backend.UnitTests/CategoryServiceTests.cs`, `FakeCategoryRepository`, and controller/authorization tests: cover success, missing target, child/product blocking, persistence count, and access control.
- No database schema or migration change is expected; existing foreign-key restrictions remain a defense in depth.
