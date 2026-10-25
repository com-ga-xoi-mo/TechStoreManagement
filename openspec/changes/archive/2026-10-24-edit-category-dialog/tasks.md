# Tasks

## 1. Preserve the edit data contract and API boundary

- [x] 1.1 Extend the category presentation snapshot and DTO mapping with additive `ParentId` and `Description` values, preserving existing constructors and verify existing category model tests still pass.
- [x] 1.2 Add `UpdateAsync(Guid, UpdateCategoryRequest, CancellationToken)` to `ICategoryApiClient` and its implementation, using the configured API origin, current bearer token, one relative PUT, complete DTO parsing, typed 400/404/409 errors, and propagated cancellation; verify with request/response and transport-failure tests.
- [x] 1.3 Update every category API fake, preview client, and DI registration that implements the interface; verify the complete frontend test project compiles without a second session/token store.

## 2. Implement the headless edit draft and anti-cycle behavior

- [x] 2.1 Add `EditCategoryDialogViewModel` as an `ObservableObject` with draft fields, parent candidates, context projections, busy/error state, close/save result signaling, and explicit cancellation semantics; verify it has no Uno or WinUI references.
- [x] 2.2 Build deterministic parent options from the full hierarchy snapshot with a root-first option, depth/path labels, visited-node protection, and exclusion of the edited node plus every descendant including inactive nodes; verify root, direct-child, deep-child, duplicate-reference, and inactive-descendant tests.
- [x] 2.3 Seed drafts without mutating source nodes, preserve the existing slug, retain description and parent values, report unavailable current parents, and revalidate candidate membership before save; verify initialization, cancel, missing-parent, and programmatic invalid-selection tests.
- [x] 2.4 Implement local name validation, exact `UpdateCategoryRequest` construction, description clearing, explicit `IsActive`, one-save busy gating, and error retention for 400/404/409; verify boundary-length, request-field, concurrent-save, cancellation, and failure-preservation tests.

## 3. Build the C# Markup dialog

- [x] 3.1 Implement `EditCategoryDialog.cs` as a binding-only C# Markup `ContentDialog` with the 480px maximum card, 12px radius, header badge/close affordance, required name field, parent picker/hint, context card, description field, active toggle, cancel action, and primary save/progress state; verify no `.xaml` file is added and no forbidden view event handlers are present.
- [x] 3.2 Apply `AppColors`, typography, metrics, and element-level theme brushes for the HTML tokens, including mutually exclusive root/child context cards and accessible labels/focus visuals; verify light/dark rendering and markup binding paths in the UI gallery or equivalent visual harness.
- [x] 3.3 Connect presenter-owned Escape, cancel, focus restoration, serialized modal lifetime, and ViewModel close/save results without putting controls in the ViewModel; verify keyboard and caller-cancellation tests for confirmed, cancelled, dismissed, and detached-source cases.

## 4. Connect row actions and authoritative refresh

- [x] 4.1 Replace the category-row edit placeholder with an edit-dialog coordinator that passes the selected identity and complete snapshot while preserving existing Add/Delete hooks; verify root and child pencil actions target the correct category and leave the tree unchanged before commit.
- [x] 4.2 After one HTTP 200 result, close the dialog and invoke exactly one category-tree reload; use the reload as authoritative and surface a retryable refresh error without resubmitting the PUT; verify success, hidden/renamed/reparented result, and failed-refresh coordination tests.
- [x] 4.3 Register the dialog/coordinator through the existing frontend UI services and retain current route activation/cancellation behavior; verify DI resolution and page-leave cancellation with the headless frontend suite.

## 5. Integration verification and live prerequisite

- [x] 5.1 Run `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` with zero errors and warnings, and verify the complete `tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` suite passes.
- [x] 5.2 Run the Windows target build `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-windows10.0.26100` with zero errors and warnings when the Windows SDK workload is available; record any environment limitation separately from code failures.
- [x] 5.3 Compare the dialog against `docs/designs/edit-category.html` at compact, root, and child states, including keyboard focus and theme switching; verify the repository contains no new XAML files.
- [x] 5.4 Document and verify the external authenticated `PUT /api/categories/{id}` backend prerequisite before claiming live 200/400/404/409 acceptance; do not implement or silently substitute backend behavior in this frontend change.

## Workflow follow-up

- When the backend update endpoint is delivered, run live authenticated edit, validation, missing-target, conflict, and post-save refresh checks.
- Archive this change only after implementation review and verification satisfy the repository workflow.
