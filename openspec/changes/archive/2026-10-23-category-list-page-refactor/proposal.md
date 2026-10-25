# Proposal

## Why

The category workspace needs an interactive, data-bound desktop implementation of `docs/designs/category-list.html` so users can browse the hierarchy, search, filter, and expand branches in one consistent screen. The current checkout contains only the Uno starter shell, so this change must introduce the missing category presentation components rather than assume the requested page and ViewModel already exist.

## What Changes

- Build `CategoryManagementPage.cs` entirely in C# Markup, mapping the HTML workspace into its header, underline tabs, filter toolbar, five-column tree table, and pagination footer. Preserve the explicitly requested colors, dimensions, and Vietnamese labels; omit the Slug column.
- Add a headless `ObservableObject` ViewModel owning `SearchText`, `StatusFilter`, `RootCategories`, `VisibleRows`, expansion, pagination, totals, and asynchronous loading/error states. Search and status changes recompute the visible hierarchy immediately.
- Introduce `ICategoryApiClient.GetTreeAsync` and authenticated HTTP transport, including cancellation and safe failure handling. Keep all database access behind backend REST endpoints.
- Add a pure `CategoryIconResolver` for name/slug-derived smartphone, laptop, tablet, accessory, and fallback icons and tint tokens.
- Expose Add/Edit/Delete dialog-opening hooks with category identity, without implementing dialog forms, validation, deletion, or status mutation. Keep inventory and serial tabs as inactive workspace affordances in this step.
- Add the missing headless `Frontend.UnitTests` project and meaningful behavior tests, plus desktop build and visual verification tasks.
- Record explicit integration prerequisites: the existing tree API excludes hidden categories and exposes no product/SKU counts; the starter client has no token/session infrastructure. This frontend change does not silently expand backend scope or replace missing facts with HTML sample values.

## Capabilities

### New Capabilities

- `category-workspace`: Desktop category tree presentation, immediate hierarchical filtering, expansion, pagination, icon resolution, authenticated loading, and deferred action hooks.

### Modified Capabilities

None. The existing `category-catalog` specification remains authoritative for the current active-only tree API. A complete management tree, real product/SKU totals, and server authentication wiring are external integration prerequisites, not changes to that contract in this proposal.

## Impact

- `frontend/TechStore.Client/Features/Categories/`: introduce the requested `Pages/CategoryManagementPage.cs`, ViewModel, row state, icon resolver, and dialog hook contract.
- `frontend/TechStore.Client/Services/Api/` and session/token abstraction: introduce HTTP tree loading and per-request Bearer authentication; reuse existing equivalents if supplied before implementation.
- `frontend/TechStore.Client/App.xaml.cs` and the existing shell: register dependencies and make the workspace reachable through a minimal C# navigation integration.
- `tests/Frontend.UnitTests/`, `TechStore.sln`, and relevant project files: enable standard .NET 10 headless tests without WinUI controls. Keep frontend packages under Central Package Management.
- `docs/designs/category-list.html`: read-only design reference; its sidebar, sample counts, static promotional badge, and JavaScript are not application data or new feature requirements.
- No new `.xaml` files, database migrations, icon columns, backend endpoint changes, full authentication UI, inventory screens, or detailed category dialogs.
- Acceptance requires a desktop build with zero errors and zero warnings, all frontend tests passing, and manual comparison with the local HTML. Full live-data acceptance additionally requires the explicitly documented API/session prerequisites; fixture tests alone cannot establish it.
