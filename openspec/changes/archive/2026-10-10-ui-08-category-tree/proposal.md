# Proposal

## Why

The Shell already exposes “Danh mục & Kho”, but that route still opens a generic placeholder. Staff need a usable category-tree tab to inspect the category hierarchy and its product counts while later stock, Serial/IMEI, and category-edit workflows remain separate.

## What Changes

- Replace the Inventory route placeholder with an Inventory page containing the Danh mục, Tồn kho, and Serial/IMEI tabs; only the category tree is implemented in this change.
- Extend the existing category repository with a complete tree query and explicit fake/unavailable behavior, preserving the flat category-options query used by product forms.
- Add typed category-tree models and a CategoryListViewModel with loading, content, empty, error/retry, expand/collapse, name search, and active-status filtering.
- Render parent/child rows, parent labels, descriptions, product counts, and read-only active/inactive status using native Uno XAML. Do not add category create/edit/delete actions or mutate status.
- Keep stock and Serial/IMEI tab content explicitly unavailable, without invented counts or business data.
- Record the missing category-tree API contract, product-count semantics, and authorization behavior as dependencies for a separate integration change.

## Capabilities

### New Capabilities
- `client-category-tree`: Preview-backed category-tree tab and its loading, filtering, hierarchy, count-display, and unavailable states.

### Modified Capabilities
- `client-navigation-shell`: The existing Inventory destination now opens the category/inventory tab frame; unfinished stock and Serial/IMEI tabs remain explicit placeholders.

## Impact

- Frontend Uno views, view models, category models/repository, preview DI registration, and headless frontend tests.
- Reuses the existing `ICategoryRepository`, `FakeCategoryRepository`, `UnavailableCategoryRepository`, Inventory route, and shell navigation. Existing flat category options used by product forms remain supported.
- No HTTP client, API endpoint, JWT, database, backend changes, category CRUD, inventory mutation, stock-tab workflow, or Serial/IMEI workflow is included. The backend currently has a Category entity but no discovered category-tree API/controller contract or product-count query contract.
