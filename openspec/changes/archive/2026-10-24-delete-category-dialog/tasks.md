# Tasks

## 1. Category delete API contract

- [x] 1.1 Extend `ICategoryApiClient` and `CategoryApiClient` with authenticated `DELETE /api/categories/{id}` returning a no-content success and mapping 400, 409, auth, not-found, and transport failures; verify with client transport tests for URI, bearer propagation, status mapping, and cancellation.
- [x] 1.2 Update `CategoryPreviewApiClient` and frontend test fakes with deterministic delete responses; verify existing category workspace tests still compile and the fake can model 204, 400, and 409 outcomes.

## 2. Delete dialog ViewModel

- [x] 2.1 Add `DeleteCategoryDialogViewModel` with immutable target snapshot data, derived icon/context metadata, `IsBusy`, `ErrorMessage`, cancel state, and a single-submit `ConfirmDeleteAsync` command; verify target initialization and duplicate-submit gating with headless tests.
- [x] 2.2 Implement friendly 400/409 and recoverable generic error mapping while retaining target context and keeping the dialog open; verify tests cover child-category and linked-product messages plus authorization/not-found/unavailable failures.
- [x] 2.3 Expose a success/close result that carries the deleted identity without mutating the workspace snapshot; verify a 204 test completes once and clears busy state even when the caller observes completion.

## 3. C# Markup delete modal

- [x] 3.1 Create `DeleteCategoryDialog.cs` as a C# Markup-only Fluent modal matching `docs/designs/delete-category.html`: 480px card, 12px radius, centered warning treatment, Vietnamese copy, category info card, constraint bullets, dynamic error area, and footer actions; verify no `.xaml` file is added and the project compiles.
- [x] 3.2 Bind all visual state and commands to the ViewModel, reuse `CategoryIconResolver` and theme tokens, provide busy feedback and keyboard/Escape/cancel behavior, and avoid view-side property-change/event synchronization; verify markup review and dialog interaction tests.

## 4. Workspace delete coordination

- [x] 4.1 Replace the category table delete placeholder with dialog presentation using the selected row's complete snapshot and restore focus to the invoking row after dismissal where supported; verify cancel/close sends no request and leaves the tree unchanged.
- [x] 4.2 On a 204 result, close the dialog and invoke exactly one authenticated tree refresh; keep the existing tree for failures and route refresh errors through the recoverable page state; verify coordinator tests for 204, failed refresh, 400, 409, and repeated activation.

## 5. Integration verification and delivery

- [x] 5.1 Run `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` and verify all existing and delete workflow tests pass.
- [x] 5.2 Build `frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` and `-f net10.0-windows10.0.26100` with zero errors and warnings; verify no new XAML files and record the results.
- [x] 5.3 Compare the dialog implementation with `docs/designs/delete-category.html`, including spacing, font hierarchy, icon visibility, focus/hover states, and 400/409 error presentation; record any target-specific limitation separately from automated test results.

## Workflow follow-up

- After implementation, provide the backend `DELETE /api/categories/{id}` contract with 204/400/409 semantics before claiming live-data acceptance.
- Archive the change only after all tracked tasks and the backend dependency review are complete.
