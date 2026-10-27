# Design: Create Category Dialog (UC-CAT-02)

## Context
See `proposal.md` for motivation. The desktop client's Category Workspace (`CategoryManagementPage.cs`) currently shows a placeholder `ContentDialog` upon clicking "+ Thêm danh mục". The category service backend (`POST /api/categories`) is fully functional and enforces RBAC (`Admin`, `Manager`), validation, unique slug uniqueness (`409 Conflict`), and hierarchical parent linking.

The visual design is specified in `docs/designs/create-category.html`. Per `AGENTS.md`, all UI components in the Categories feature must be written in 100% C# Markup (no XAML files).

## Goals / Non-Goals

**Goals:**
- Provide a pixel-accurate 640px Fluent dialog using 100% C# Markup in `CreateCategoryDialog.cs`.
- Implement `CreateCategoryDialogViewModel.cs` following Feature-First MVVM and CommunityToolkit.Mvvm with testable state management.
- Real-time Vietnamese diacritic stripping for slug generation (e.g., "Tai nghe & Âm thanh" $\rightarrow$ `tai-nghe-am-thanh`).
- Populate parent category selection from the existing category tree, with `-- Danh mục gốc (Cấp 1) --` as the default root option.
- Strictly adhere to `CreateCategoryRequest` DTO contract (`Name`, `Slug`, `ParentId`, `Description`, `IsActive`), omitting client-only fields (`Code`, `DisplayOrder`).
- Support persistent state on error (no input loss on 409 Conflict or 400 Bad Request) and sequential entry with `KeepCreating`.
- Trigger automatic tree refresh in `CategoryManagementViewModel` upon successful creation.
- 100% passing unit tests for the ViewModel in `TechStore.Frontend.UnitTests`.

**Non-Goals:**
- Category editing (UC-CAT-03) or category deletion (UC-CAT-04), which remain deferred to subsequent changes.
- Backend schema changes or database migrations (backend already supports `POST /api/categories`).
- Export to Excel or drag-and-drop hierarchy reorganization.

## Decisions

### Decision 1: 100% C# Markup via Custom `ContentDialog`
- **Choice**: Implement `CreateCategoryDialog` as a subclass of `ContentDialog` (or wrapping a custom container inside `ContentDialog`) with `DefaultButton = ContentDialogButton.None` and custom header, body, and footer styled purely via C# markup controls (`Grid`, `StackPanel`, `Border`, `TextBox`, `ComboBox`, `ToggleSwitch`, `CheckBox`, `Button`, `ProgressRing`).
- **Rationale**: Keeps full control over the 640px max width, 12px rounded corner radius, subtle borders, custom colors matching `CategoryWorkspaceStyles.cs`, and eliminates WinUI's default rigid dialog button bar. Strictly satisfies the project rule: no XAML files.
- **Alternatives considered**: Standard WinUI ContentDialog with `PrimaryButtonText`. Rejected because WinUI's native button bar does not support the left-aligned `KeepCreating` checkbox alongside right-aligned action buttons matching `create-category.html`.

### Decision 2: Vietnamese Slug Auto-Generation in ViewModel
- **Choice**: Real-time diacritic stripping using `String.Normalize(NormalizationForm.FormD)`, explicit mapping for Vietnamese characters (đ/Đ $\rightarrow$ d), regex stripping of combining marks `\p{IsCombiningDiacriticalMarks}`, converting non-alphanumeric sequences to single hyphens `-`, and lowercasing.
- **Rationale**: Runs instantly in memory on every keystroke in `Name` without blocking or API calls, generating clean, standard slugs matching the mockup preview (`tai-nghe-am-thanh`).
- **Alternatives considered**: Server-side slug generation. Rejected because `create-category.html` explicitly requires real-time slug preview badge while typing.

### Decision 3: Strict Backend DTO Contract Conformance
- **Choice**: The API request only serializes `CreateCategoryRequest`:
  ```csharp
  public class CreateCategoryRequest
  {
      public string? Name { get; set; }
      public string? Slug { get; set; }
      public string? Description { get; set; }
      public Guid? ParentId { get; set; }
      public bool? IsActive { get; set; }
  }
  ```
  The UI displays decorative/client preview fields for SKU code and icon representation per `create-category.html`, but does NOT send `Code` or `DisplayOrder` to the backend.
- **Rationale**: The backend `CategoriesController` and `ICategoryService` accept only `CreateCategoryRequest`. Modifying or sending unmapped properties violates contract integrity.

### Decision 4: Separation of UI Hosting and ViewModel Logic
- **Choice**: `CreateCategoryDialogViewModel` depends on `ICategoryApiClient`. It exposes `SaveCommand`, observable properties for all form fields, `IsBusy`, `ErrorMessage`, and events or callbacks for success (`CategoryCreated`).
- **Rationale**: Enables comprehensive headless unit testing in `TechStore.Frontend.UnitTests` without requiring a WinUI runtime or XAML dispatcher.

### Decision 5: Parent Category Flattening for ComboBox
- **Choice**: Create a flat lookup list `ParentCategoryOption` with `Id`, `DisplayName` (indented with dashes or arrows, e.g., `Điện thoại thông minh`, `— iPhone`), and `Depth`. The root item is always `-- Danh mục gốc (Cấp 1) --` with `Id = null`.
- **Rationale**: WinUI ComboBox works best with a flat items source while providing clear hierarchical visual depth to cashier/admin users.

## Risks / Trade-offs

- **[Risk]** Modal dimensions exceeding small screen resolutions $\rightarrow$ **Mitigation**: Constrain dialog max-width to 640px, set max-height on the form content with a `ScrollViewer` (`max-h-[calc(100vh-180px)]` equivalent), ensuring accessibility on all resolutions.
- **[Risk]** Backend returns 409 Conflict (duplicate slug) causing input loss $\rightarrow$ **Mitigation**: Catch `CategoryApiException` or HTTP 409 in `SaveAsync`, set `ErrorMessage`, and do not reset input properties or close the dialog.
- **[Risk]** Parent category tree modified concurrently $\rightarrow$ **Mitigation**: Backend validates `ParentId` existence and returns 400 Bad Request if invalid; dialog surfaces message to user.

## Migration Plan
No database migration or API contract changes required. Rollback is a simple git revert of frontend files.
