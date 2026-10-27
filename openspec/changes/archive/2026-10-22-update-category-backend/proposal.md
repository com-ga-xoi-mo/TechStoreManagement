# Proposal

## Why

UC-CAT-03 requires administrators to correct category information and reorganize the product taxonomy, but the backend currently exposes only category creation and tree retrieval. An API-first update flow must validate slugs and parent relationships, persist timestamps, and prevent invalid hierarchy changes with behavior specified before implementation through TDD.

## What Changes

- Add `PUT /api/categories/{id}` for `Name`, `Slug`, `Description`, `ParentId`, and `IsActive`, returning the updated `CategoryDto` and setting `UpdatedAt = DateTime.UtcNow`.
- Introduce `UpdateCategoryRequest`, `UpdateCategoryResult`, and `ICategoryService.UpdateAsync`, with explicit success, validation, missing-category, and conflict outcomes.
- Reject blank or overlong names, invalid slugs, duplicate slugs owned by other categories, missing parents, self-parenting, and descendant-parent cycles, including inactive descendants.
- Regenerate omitted or blank slugs from the submitted name using existing Vietnamese transliteration; allow retaining the current category's own slug.
- Extend category repositories with tracked update retrieval, slug uniqueness excluding the current ID, and `GetDescendantIdsAsync`; keep all business decisions and persistence orchestration in the service.
- Protect the new endpoint with JWT Bearer authentication and the Admin role specified for UC-CAT-03. Supply the missing authentication configuration without expanding into login or refresh-token implementation.
- Plan failing service tests first, then minimal implementation and refactoring, using the existing hand-written fakes. Validate HTTP mapping and persistence separately from service fakes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `category-catalog`: Add administrative category updates, validation, slug regeneration and uniqueness, hierarchy protection, and safe error responses. The requested change-local detail path is `specs/catalog/update-category.md`; retain `category-catalog` as the existing capability identity rather than introducing a duplicate taxonomy capability.

## Impact

- Shared contracts: add `shared/Requests/UpdateCategoryRequest.cs`; reuse `shared/DTOs/CategoryDto.cs`.
- Catalog: extend `CategoriesController`, `ICategoryService`, `CategoryService`, `ICategoryRepository`, and `CategoryRepository` while retaining existing create and tree behavior.
- Common infrastructure: reuse `IUnitOfWork` and `DuplicateKeyException`; any hierarchy lock remains repository-owned and its transaction service-owned.
- Security: configure JWT Bearer validation and authorization middleware in `backend/Program.cs`, with the necessary backend package/configuration and no committed secrets. Identity currently contains entities/configurations only, so an existing token issuer cannot be assumed.
- Tests: extend `CategoryServiceTests.cs`, `FakeCategoryRepository.cs`, and `FakeUnitOfWork.cs` as needed; add focused controller/authorization verification.
- No category schema migration, frontend work, deletion API, product changes, or unrelated cleanup is planned. Planning creates only standard OpenSpec artifacts; production changes and the TDD runs belong to the apply phase.
