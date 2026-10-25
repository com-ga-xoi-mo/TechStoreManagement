# Design

## Context

See `proposal.md` for the motivation and scope. The category page already routes row actions through `CategoryManagementViewModel` and presents the existing create/edit dialogs, but its delete action still falls back to a placeholder. `ICategoryApiClient` has authenticated tree/create/update calls but no category delete operation, and the current backend checkout does not expose the required delete endpoint. The delete workflow therefore needs a frontend contract that can be tested with preview fakes while making the live endpoint dependency explicit.

## Goals / Non-Goals

**Goals:**

- Keep deletion behind an explicit, accessible confirmation modal that mirrors `docs/designs/delete-category.html`.
- Keep all state and error handling headless in the ViewModel, with a view limited to C# Markup, bindings, styling, and dialog button wiring.
- Preserve the selected category context and workspace tree until the API returns 204.
- Make 400/409 business constraints understandable and testable, with exactly one refresh after success.
- Reuse the existing bearer-token transport, `CategoryIconResolver`, `ContentDialogPresenter`, design tokens, and category row action identity.

**Non-Goals:**

- Implementing the backend controller, service, repository, or database rules.
- Automatically deleting or reparenting child categories, removing linked products, or providing bulk deletion.
- Adding category icon fields, XAML, optimistic tree mutation, or a second dialog framework.

## Decisions

### 1. Add a category-specific delete client operation

Extend `ICategoryApiClient` with a `DeleteAsync` operation accepting the category identity and cancellation token. `CategoryApiClient` sends `DELETE /api/categories/{id}` through the existing `ApiClient`/Bearer handler and maps status codes into the existing `CategoryApiFailure` model. Preview clients and test fakes implement the same operation so ViewModel and page tests do not require a live server.

**Why:** A typed category contract keeps HTTP details out of the ViewModel and preserves the repository/client boundary. Reusing the existing transport avoids a second token path.

**Alternative rejected:** Calling `IApiClient` directly from the dialog ViewModel would duplicate URI/error handling and violate the feature-first client boundary.

The live API prerequisite is an authenticated `DELETE /api/categories/{id}` that returns 204 on success, 400 when children block deletion, 409 when products block deletion, and structured error details suitable for normalization. The frontend must not silently emulate this endpoint.

### 2. Use an immutable target snapshot plus observable operation state

`DeleteCategoryDialogViewModel` receives the selected category snapshot at construction. It exposes target ID, name, level/meta text, parent name, derived icon descriptor, child/product counts, `IsBusy`, `ErrorMessage`, and a computed error-visible state. `ConfirmDeleteAsync` validates that a target exists, gates duplicate submissions, clears only the prior operation error, awaits one delete call, and exposes a success result/event for the page coordinator. Cancel and close are presentation actions and never call the client.

**Why:** The dialog cannot accidentally switch targets while a destructive request is running, and a snapshot makes cancellation and error-retention tests deterministic.

**Alternative rejected:** Mutating `CategoryNode` or removing the row before the response would make 400/409 recovery and refresh failure ambiguous.

### 3. Normalize failures without losing context

The ViewModel maps `BadRequest` to a message telling the user to move or delete child categories first and `Conflict` to a message explaining that linked inventory products prevent deletion. Unauthorized/forbidden, not-found, unavailable, and unknown failures use actionable generic messages. `ErrorMessage` remains visible in the warning box, `IsBusy` returns false in `finally`, and no failure closes the dialog or changes the tree.

**Why:** Business constraints are expected user decisions, while transport/authentication failures remain recoverable without exposing raw HTTP internals.

### 4. Compose the modal in C# Markup from design tokens

`DeleteCategoryDialog` uses a `ContentDialog`/presenter-compatible root with a 480px maximum-width white card, 12px radius, 24px padding, and the spacing/colors/typography from the HTML design. The body is a centered warning icon and title/subtitle, followed by a category info card, a constraint warning card with two conditional bullets, and a bound error area. The footer aligns “Hủy bỏ” and the red “Xác nhận xóa” action to the right; the destructive button swaps its icon content for busy feedback through bindings.

The view binds to the ViewModel and commands, uses `CategoryIconResolver` for the category icon/tint, and does not subscribe to `PropertyChanged` or mutate text/visibility from event handlers. Escape/close maps to dialog cancellation; focus is returned by the page coordinator after dismissal where the platform supports it.

**Why:** This keeps theme-aware colors and accessibility state in the existing Markup conventions while preserving a single visual source of truth in the HTML design.

**Alternative rejected:** Reusing the generic placeholder `ContentDialog` would omit the safety context and cannot satisfy the specified modal structure.

### 5. Refresh only after a confirmed delete

The page coordinator opens the dialog with the row's identity/snapshot. A successful 204 result closes it and invokes the existing tree-load path exactly once using the current activation/session token. No local removal occurs before that load. Cancellation and failures return without refresh; a refresh failure is reported through the existing page content error/retry state and never resubmits the delete.

**Why:** The server's refreshed hierarchy remains authoritative for parent relationships, counts, hidden state, and pagination.

### 6. Test through headless fakes and coordinator seams

Add ViewModel tests for target initialization, cancel/close, one-request busy gating, 204 success, 400/409 messages, and preserved context. Add page/coordinator tests that assert the dialog receives the selected row, successful deletion causes one refresh, and failed/cancelled deletion causes none. Add client transport tests for DELETE URI, bearer propagation, 204 handling, and status mapping. Keep live endpoint verification separate and marked as a prerequisite.

## Risks / Trade-offs

- **[Backend endpoint is absent or returns different status semantics]** → Keep the contract and dependency explicit, cover the frontend with deterministic fakes, and block live acceptance until the backend returns the documented 204/400/409 responses.
- **[A dialog is dismissed while a request is in flight]** → Disable destructive and cancel actions while `IsBusy` where the platform allows, cancel the operation with the dialog lifetime token, and ignore stale completion events.
- **[Design tokens or Material icon fonts differ across Uno targets]** → Use existing token/accessor and icon resolver conventions, verify both desktop and Windows builds, and visually inspect the modal on the target desktop runtime.
- **[Refresh after 204 fails]** → Surface the existing recoverable page error and retry path; never issue the destructive request again or fabricate a deleted tree locally.

## Migration Plan

1. Implement the client delete contract, failure mapping, ViewModel, C# Markup dialog, page coordinator, preview fake, and tests behind the existing category action flow.
2. Provide or deploy the backend `DELETE /api/categories/{id}` contract separately, then run live acceptance for 204, 400, and 409 cases with an authenticated session.
3. Verify no `.xaml` files were added, build desktop and Windows targets with zero warnings/errors, and run the complete frontend unit-test project.
4. Roll back by restoring the prior delete placeholder and leaving the backend endpoint untouched; no database migration is part of this change.
