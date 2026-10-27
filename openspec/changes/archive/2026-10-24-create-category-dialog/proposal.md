# Proposal: Create Category Dialog (UC-CAT-02)

## Why
Category creation in the desktop category workspace currently opens a placeholder ContentDialog with no form or persistence capabilities. To complete the category authoring workflow (UC-CAT-02), administrators and managers need a dedicated, production-ready Create Category Dialog faithfully following `docs/designs/create-category.html` using 100% C# Markup that creates categories through the backend API and immediately refreshes the category tree.

## What Changes
- Implement `CreateCategoryDialog.cs` using 100% C# Markup (WinUI 3 / Uno Platform, strictly no XAML) matching the 640px Fluent design in `docs/designs/create-category.html`.
- Implement `CreateCategoryDialogViewModel.cs` using CommunityToolkit.Mvvm to manage form state (`Name`, `Slug`, `SelectedParentId`, `Description`, `IsActive`, `KeepCreating`, `IsBusy`, `ErrorMessage`) and real-time Vietnamese accent removal for slug auto-generation.
- Extend `ICategoryApiClient` and `CategoryApiClient` to support `CreateAsync(CreateCategoryRequest, CancellationToken)` mapping to backend `POST /api/categories`, properly handling `409 Conflict`, `400 BadRequest`, and network failures.
- Enforce strict backend DTO contract compliance: only send `Name`, `Slug`, `ParentId`, `Description`, and `IsActive` in `CreateCategoryRequest`. Exclude non-persisted client-side UI fields (`Code`, `DisplayOrder`).
- Connect the `+ Thêm danh mục` action in `CategoryManagementPage.cs` to launch `CreateCategoryDialog` with the current category tree for parent selection.
- Support "Tiếp tục thêm danh mục khác sau khi lưu" (`KeepCreating`): when checked, successful creation clears form fields for sequential entry and refreshes the tree without closing the dialog; when unchecked, closes the dialog and refreshes the tree.
- Retain form inputs and display clear error messages if the backend returns validation errors or conflict (HTTP 409).
- Add unit tests in `TechStore.Frontend.UnitTests` covering `CreateCategoryDialogViewModel` validation, slug generation, parent category loading, create workflow, keep-creating behavior, and error handling.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `category-workspace`: Replace the deferred Add category dialog stub with the full Create Category Dialog interaction (UC-CAT-02), including C# Markup visual layout, form validation, real-time Vietnamese slug generation, parent category hierarchy selection, API creation, conflict/error preservation, and post-creation tree refresh.

## Impact
- **Frontend UI**: Adds `CreateCategoryDialog.cs` in `frontend/TechStore.Client/Features/Categories/Dialogs/`. Modifies `CategoryManagementPage.cs` to show the dialog and reload data on success.
- **Frontend ViewModels**: Adds `CreateCategoryDialogViewModel.cs` in `frontend/TechStore.Client/Features/Categories/ViewModels/`.
- **Frontend API Client**: Adds `CreateAsync` method to `ICategoryApiClient` and `CategoryApiClient`.
- **Contracts**: Uses existing `CreateCategoryRequest` in `TechStore.Shared.Requests` and `CategoryDto` in `TechStore.Shared.DTOs`.
- **Dependencies**: No new external NuGet packages. Uses existing `CommunityToolkit.Mvvm` and Uno Platform WinUI 3 controls.
- **Tests**: Adds unit tests in `tests/Frontend.UnitTests/Features/Categories/CreateCategoryDialogTests.cs`.
