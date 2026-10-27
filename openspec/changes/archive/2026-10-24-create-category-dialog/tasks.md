# Tasks: Create Category Dialog (UC-CAT-02)

## 1. Category API Client Enhancement

- [x] 1.1 Add `CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)` to `ICategoryApiClient` and implement it in `CategoryApiClient` to post to `api/categories` with Bearer authentication and handle `409 Conflict`, `400 BadRequest`, and network errors. Verify with unit tests in `tests/Frontend.UnitTests/Services/CategoryApiClientTests.cs`.

## 2. Create Category ViewModel & Unit Tests

- [x] 2.1 Implement `CreateCategoryDialogViewModel.cs` in `frontend/TechStore.Client/Features/Categories/ViewModels/` managing form state (`Name`, `AutoSlug`, `SelectedParentId`, `Description`, `IsActive`, `KeepCreating`, `IsBusy`, `ErrorMessage`), real-time Vietnamese diacritic stripping for slug generation, and parent category options (`ParentCategoryOption`). Verify by writing unit tests for slug conversion, input validation, and parent hierarchy in `tests/Frontend.UnitTests/Features/Categories/CreateCategoryDialogTests.cs`.
- [x] 2.2 Implement `SaveCommand` in `CreateCategoryDialogViewModel` to call `ICategoryApiClient.CreateAsync` with strict DTO contract (`Name`, `Slug`, `ParentId`, `Description`, `IsActive`). Handle success with `CategoryCreated` event and `KeepCreating` logic (clear form if true, close if false); handle API failures without losing user input. Verify with unit tests in `CreateCategoryDialogTests.cs`.

## 3. 100% C# Markup Dialog View

- [x] 3.1 Implement `CreateCategoryDialog.cs` in `frontend/TechStore.Client/Features/Categories/Dialogs/` using 100% C# Markup (no XAML) faithfully adhering to `docs/designs/create-category.html` (640px max width, 12px rounded container, folder icon header, subtitle, close button, name input with real-time slug preview badge, parent category dropdown, description textarea, POS toggle switch card, system tip box, keep-creating checkbox, cancel button, and primary add button with ProgressRing). Verify compilation with 0 errors.

## 4. Workspace Integration & Verification

- [x] 4.1 Update `CategoryManagementPage.cs` to open `CreateCategoryDialog` when "+ Thêm danh mục" is activated, populating parent category options from the current tree and executing `ViewModel.LoadCommand` to refresh the category tree upon successful creation. Verify manually and via integration checks.
- [x] 4.2 Verify complete solution build on `net10.0-desktop` and `net10.0-windows10.0.26100` with 0 errors and 0 warnings, and verify 100% passing tests across `TechStore.Frontend.UnitTests`.
