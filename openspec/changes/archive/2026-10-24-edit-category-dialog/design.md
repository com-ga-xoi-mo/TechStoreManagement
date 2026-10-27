# Design

## Context

The category page already owns the loaded hierarchy, row action commands, and a serialized `ContentDialogPresenter`, while the category API facade currently supports tree retrieval and creation only. `CategoryNode` and its mapping do not retain every field required to seed an edit draft, even though the shared contract already defines `Description` and `ParentId`. The current edit action is a placeholder. See `proposal.md` and the category-workspace delta for the observable behavior.

The user explicitly selected the existing feature convention of an `ObservableObject` ViewModel and `ICategoryApiClient.UpdateAsync`. That narrow choice is retained for this dialog; the implementation must still keep UI controls out of the ViewModel and use the repository's current session/token boundary. The backend update endpoint is not present in this checkout and remains an external prerequisite.

## Goals / Non-Goals

**Goals:**

- Provide a compact, accessible, theme-aware C# Markup edit dialog with a draft that can be cancelled safely.
- Preserve enough hierarchy and DTO data to initialize and validate a draft without losing description, parent, active-state, or slug values.
- Enforce client-side anti-cycle selection over the complete available hierarchy and revalidate the selected parent before sending.
- Normalize update errors, retain failed drafts, and coordinate one authoritative tree refresh after HTTP 200.
- Keep transport cancellation, bearer-token behavior, focus recovery, and tests compatible with existing frontend infrastructure.

**Non-Goals:**

- Implementing the server `PUT /api/categories/{id}` endpoint, authorization, validation, transactions, or database changes.
- Adding inventory/serial functionality, delete behavior, slug editing UI, or a new dialog framework.
- Claiming hidden-category/count acceptance when the current tree endpoint does not return those management fields.

## Decisions

### 1. Use a headless edit ViewModel with a presenter-owned dialog lifecycle

`EditCategoryDialogViewModel` will own only draft state, candidate projection, validation, and the save command. It will expose a result/close signal consumed by the UI presenter; it will not reference `ContentDialog`, controls, or window events. The page/coordinator will pass an immutable category snapshot, await the presenter result, and invoke one `LoadCommand` after a successful update. This keeps a failed draft open and avoids data/event handlers in the view.

The alternative was to make the page subscribe directly to `PropertyChanged` or button events as the existing legacy create dialog does. That would violate the current C# Markup guidance and makes cancellation/focus ownership difficult to test.

### 2. Build candidates from the full hierarchy snapshot with identity-based DFS

Candidate generation starts with the null-root option, then performs deterministic preorder traversal over all supplied roots and descendants. A visited set protects against malformed duplicate references; a descendant set rooted at the edited ID excludes the edited node and every reachable child, regardless of depth or active state. Labels include hierarchy depth/path. The save command checks the selected ID against the same excluded set and available IDs, so programmatic binding cannot bypass the picker.

The source is the full loaded management snapshot, not `VisibleRows`, filtered results, expanded rows, or a paged slice. If the current parent is missing from that snapshot, the dialog reports it as unavailable and requires a valid explicit selection or reload instead of silently converting it to a root.

### 3. Extend presentation data additively and preserve the existing slug

Add optional `Description` and `ParentId` values to the category presentation snapshot without breaking existing positional construction. Update DTO mapping to retain those fields. The dialog has no slug editor; it carries the original slug when available and sends `null` only when no slug is known, allowing the server contract to regenerate it. Description text is sent unchanged when populated and as an empty value when the user intentionally clears it; it is not normalized to null in the client.

### 4. Keep the API facade explicit and non-retrying

Add `UpdateAsync(id, UpdateCategoryRequest, cancellationToken)` to `ICategoryApiClient` and its existing authenticated implementation. It sends one relative `PUT` request with the current bearer token and parses the complete success DTO. HTTP 400/404/409 map to the existing typed category exception/error surface; caller cancellation propagates as cancellation and is not converted to a validation error. No automatic write retry or optimistic local mutation is used.

The generic `IApiClient`/repository route was considered, but the user's explicit Choice A and the existing category facade are the smallest compatible seam for this feature. The implementation should share the configured origin/session behavior rather than introduce a second token store.

### 5. Render with element-level theme tokens and binding-only view code

`EditCategoryDialog.cs` will construct the card, header, fields, context card, and footer with Uno fluent C# Markup. Theme colors are applied directly with `AppColors` accessors; non-color metrics use Markup styles. The root context card uses the specified blue fill/stroke and the child card uses the slate fill/stroke through semantic tokens or direct theme resources. Two-way fields use binding expressions, and busy/error/visibility are binding conversions. No `PropertyChanged`, `TextChanged`, `Checked`, or `Click` handlers will synchronize draft state.

The dialog presenter supplies Escape/cancel, focus restoration, root serialization, and lifetime cancellation. A small UI-side adapter may connect the ViewModel result to `ContentDialog.Hide`; this is lifecycle plumbing, not form data logic.

### 6. Refresh only after a confirmed update

The workspace opens the dialog with the row's identity and a complete snapshot. A successful presenter result carries the 200 response (or an equivalent saved marker), after which the page requests exactly one tree reload. The response is not injected into rows because counts and hidden branches may be incomplete; the refreshed API hierarchy remains authoritative. If reload fails, the page reports a retryable refresh error and never resubmits the PUT.

## Risks / Trade-offs

- **[Backend endpoint absent]** Live save cannot succeed in the current checkout. → Keep the dependency explicit in tasks and verification; use a deterministic fake API for headless behavior tests and do not mark live acceptance complete.
- **[Management tree may omit inactive descendants]** Client filtering can only exclude descendants present in the supplied snapshot. → Traverse every node returned, preserve backend anti-cycle validation as authoritative, and report data completeness limits.
- **[DTO shape changes affect existing fakes]** Adding `UpdateAsync` and optional snapshot fields requires all category API fakes and constructors to be updated. → Make fields additive, search all implementations before coding, and compile the complete frontend test project.
- **[Theme/style behavior differs by target]** Markup style color setters can be resolved once on Skia. → Put colors on elements, run the light/dark dialog check, and build both desktop and Windows targets.
- **[Commit succeeds but refresh fails]** A saved category may not be immediately visible in an active-only tree. → Report refresh failure separately, offer retry, and never issue a second write.

## Migration Plan

1. Add the additive model/API seams, dialog ViewModel, C# Markup view, presenter coordination, and tests behind the existing category route.
2. Build `net10.0-desktop` and `net10.0-windows10.0.26100`, then run the complete `tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` suite and the dialog visual/theme checks.
3. When the backend supplies the authenticated `PUT /api/categories/{id}` contract, run the live edit flow for successful update, validation, missing-target, conflict, and post-save refresh cases.
4. Rollback is a client deployment rollback; no database migration is introduced by this change.
