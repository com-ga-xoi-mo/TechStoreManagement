# Proposal

## Why

The category workspace exposes a delete action, but it currently stops at a placeholder instead of providing a safe confirmation flow. A destructive operation needs an explicit, design-matched modal that explains category constraints, prevents accidental requests, preserves the dialog on business errors, and refreshes the tree only after the API confirms deletion.

## What Changes

- Add a C# Markup-only `DeleteCategoryDialog` matching `docs/designs/delete-category.html`, including the warning icon, category context card, constraint warnings, dynamic error area, and destructive footer actions.
- Add `DeleteCategoryDialogViewModel` with target snapshot state, busy/error state, explicit cancel behavior, and a relay command that sends one authenticated delete request.
- Extend the category client contract and preview fakes with a category-specific delete operation and HTTP failure mapping for the safe-delete workflow.
- Replace the category table's delete placeholder with dialog coordination; close and refresh exactly once only after HTTP 204.
- Preserve the tree and the dialog draft on cancel, dismissal, HTTP 400/409 business failures, and other failed requests.
- Add headless frontend tests for cancellation, confirmation, busy de-duplication, 204 refresh coordination, and 400/409 error messaging.
- Keep the change frontend-only. The live acceptance path depends on a backend `DELETE /api/categories/{id}` contract that returns 204, 400 for child-category constraints, and 409 for linked-product constraints; backend implementation is outside this change.
- Do not add XAML, automatic child deletion, product reassignment, or bulk-delete behavior.

## Capabilities

### New Capabilities

None. The delete workflow extends the existing category workspace capability.

### Modified Capabilities

- `category-workspace`: Replace the deferred delete action hook with a safe delete dialog, explicit cancel/error semantics, and authoritative refresh after a successful 204 response.

## Impact

- **Frontend UI:** `CategoryManagementPage`, the category row action coordinator, and new `Features/Categories/Dialogs/DeleteCategoryDialog.cs`.
- **Frontend MVVM:** new `DeleteCategoryDialogViewModel` plus observable state and command tests in `tests/Frontend.UnitTests`.
- **Frontend API:** `ICategoryApiClient`, `CategoryApiClient`, preview clients/fakes, and delete-specific failure mapping using the existing bearer-token transport.
- **Backend dependency:** an authenticated `DELETE /api/categories/{id}` endpoint with stable 204/400/409 semantics and useful ProblemDetails messages is required before live-data acceptance can be claimed.
- **Build/test verification:** desktop and Windows targets, no new `.xaml` files, and the complete headless frontend test project.
