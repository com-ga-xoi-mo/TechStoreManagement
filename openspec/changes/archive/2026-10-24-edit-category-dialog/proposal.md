# Proposal

## Why

The category workspace exposes edit actions, but those actions currently stop at a placeholder dialog. Operators therefore cannot correct a category name, hierarchy, description, or active state from the desktop client, and the workspace cannot refresh from a confirmed update. This change adds the edit workflow while keeping server-side category mutation as an explicit dependency: the current checkout defines `UpdateCategoryRequest`, but does not yet expose `PUT /api/categories/{id}`.

## What Changes

- Add an edit-category dialog implemented entirely with Uno C# Markup, matching `docs/designs/edit-category.html` at a compact maximum width of 480px.
- Add an `ObservableObject` edit ViewModel with draft fields, parent candidates, anti-cycle filtering, validation, busy/error state, and an `ICategoryApiClient.UpdateAsync` save command.
- Preserve the selected category's description, parent, active state, and slug when initializing the draft; send only the fields in `UpdateCategoryRequest`.
- Exclude the edited category and every descendant from parent selection, including descendants at any depth and any inactive nodes present in the available management snapshot.
- Connect each category-row pencil action to the edit dialog and refresh the category tree exactly once after a successful HTTP 200 update. Keep failed drafts open and preserve user input for 400, 404, and 409 responses.
- Use the existing dialog presenter/lifecycle and theme-token conventions, including keyboard focus, cancellation, light/dark colors, and no new XAML files.
- Add headless ViewModel, API-facade, integration-seam, anti-cycle, request-shape, and refresh-coordination tests to the existing frontend unit-test project.
- Record the backend `PUT /api/categories/{id}` endpoint, authorization, validation, and persistence as a prerequisite for live-data acceptance; this proposal does not implement backend code.

## Capabilities

### New Capabilities

None. The dialog is a new user flow within the existing category workspace capability; it does not introduce a separate durable business capability.

### Modified Capabilities

- `category-workspace`: Edit actions now open a real edit dialog, retain drafts on failure, enforce safe parent selection, and refresh the hierarchy only after a confirmed update.

## Impact

- Frontend view and ViewModel: `frontend/TechStore.Client/Features/Categories/Dialogs/EditCategoryDialog.cs` and `Features/Categories/ViewModels/EditCategoryDialogViewModel.cs`.
- Frontend category API facade and DTO mapping: add update transport and preserve `ParentId`/`Description` needed to seed the draft.
- Category workspace action routing and dialog presentation, plus existing category model snapshots/options.
- Frontend unit tests under `tests/Frontend.UnitTests`.
- Runtime acceptance depends on an authenticated backend `PUT /api/categories/{id}` returning a complete updated category DTO. No database access or backend implementation is added here.
