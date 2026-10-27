# Tasks

## 1. Feature and Headless Test Foundations

- [x] 1.1 Capture the desktop build baseline and current workspace integration limitations in `docs/category-workspace-verification.md`; verify the record identifies active-only tree data, missing counts/session wiring, and the exact `docs/designs/category-list.html` reference without altering unrelated working-tree changes.
- [x] 1.2 Introduce UI-free category row/snapshot state, typed status filter, icon descriptor, API/session interfaces, and dialog-request contract under the paths in `design.md`; verify the types reference neither WinUI controls nor database libraries and count fields distinguish unavailable values from zero.
- [x] 1.3 Add `tests/Frontend.UnitTests/TechStore.Frontend.UnitTests.csproj` targeting `net10.0`, with explicit links to relevant production UI-free files, shared contracts, CommunityToolkit.Mvvm, xUnit, and FluentAssertions; register it in `TechStore.sln` and verify restore/build without initializing a native UI thread.

## 2. Authenticated Tree Transport and Loading

- [x] 2.1 Implement `ICategoryApiClient.GetTreeAsync` over `HttpClient`, mapping shared nested DTOs to the client snapshot and leaving unavailable product/SKU totals null; verify nested deserialization, empty trees, missing count fields, cancellation propagation, and Bearer headers through a fake HTTP handler.
- [x] 2.2 Implement an injected session-token provider seam and map missing token, HTTP 401, HTTP 403, server failure, and malformed response outcomes; verify no request is sent without a session token, tokens are applied per request, and errors contain no token or provider details.
- [x] 2.3 Implement ViewModel loading, busy/error/empty states, retry, cancellation, and stale-response protection; verify repeated successful loads do not duplicate rows, busy state clears on all paths, retry recovers, and an older/cancelled response cannot replace a newer result.
- [x] 2.4 Document the current tree adapter's active-only/count limitations and session setup seam in the verification document; verify the documentation distinguishes fixture coverage from live API acceptance and does not prescribe backend implementation within this change.

## 3. Hierarchy Projection, Filters, Expansion, and Pagination

- [x] 3.1 Implement `CategoryManagementViewModel : ObservableObject`, generated properties/commands, `RootCategories`, `VisibleRows`, unique category totals, stable row identities, direct-parent labels, and depth; verify empty, single-root, multi-root, and three-level fixtures preserve API sibling order and initially expanded branches.
- [x] 3.2 Implement immediate name search and All/Active/Hidden filtering with AND composition, ancestor context, and forced-open paths; verify trimmed mixed-case search, hidden children under active parents, active children under hidden parents, nonmatching siblings, matching parents without nonmatching children, and zero matches without extra API calls.
- [x] 3.3 Implement branch expansion by category identity, disabled leaf/forced-path chevrons, and restoration after clearing filters; verify collapsing one branch preserves sibling and nested branch state, category totals remain unchanged, and no refresh or network request occurs.
- [x] 3.4 Implement page size 15, eligible-match pagination followed by ancestor context, page-number commands, footer range/total, reset on filter changes, and clamp on collapse; verify 0/1/15/16-result boundaries, one root with more than 15 descendants, context deduplication across pages, and previous/next availability.
- [x] 3.5 Add `CategoryIconResolver` with smartphone/blue, laptop/indigo, tablet/teal, accessory/audio/emerald, and neutral fallback mappings; verify Vietnamese names, English slugs, mixed case, ambiguous vendor names, and unknown/empty input without native UI dependencies.
- [x] 3.6 Document the meanings of header total, filtered total, footer total, context rows, expansion, and pagination in the verification document; verify its worked nested-tree example agrees with the corresponding behavioral tests.

## 4. C# Workspace Layout and Bindings

- [x] 4.1 Implement the C# page's tokens, typography, local font/icon resources and attribution, responsive header, Add button, and underline tabs using the HTML mapping and explicit overrides in `design.md`; verify visual correspondence, correct hover/focus behavior, offline icon rendering, dynamic category badges, and absence of new XAML or embedded XAML markup.
- [x] 4.2 Implement the 40-unit search and status controls with programmatic two-way binding and immediate text-source updates; verify each keystroke and selection updates the bound ViewModel and the page contains no filtering or API logic.
- [x] 4.3 Build the white tree-table card, aligned five-column header/body layout, shared horizontal scrolling, parent/child row visuals, branch glyphs/chevrons, depth indentation, and read-only status indicators; verify column minima 320/160/130/120/150, 36-unit icon tiles, row hover/hidden styling, correct direct-parent names, and no Slug column.
- [x] 4.4 Bind product/SKU presentation, unavailable markers, loading/error/retry/empty views, and footer commands; verify authoritative fake counts format as “{N} sản phẩm”/“{N} SKU”, missing counts render “—”, empty range is 0–0, and pages are derived from results rather than the HTML's sample values.
- [x] 4.5 Review the page against the local HTML at a normal desktop width and a narrow width; record screenshots or equivalent visual evidence for workspace ordering, padding, spacing, font weights, colors, table alignment, footer controls, and the documented token overrides in the verification document.

## 5. Deferred Actions and Application Integration

- [x] 5.1 Wire Add/Edit/Delete commands to the dialog-opening contract, including the exact selected category ID and a minimal host placeholder only where needed; verify hand-written fake dialog tests observe one correct request per action and no tree mutation or write HTTP requests.
- [x] 5.2 Register feature/API/session/dialog dependencies in `App.xaml.cs` and add the minimal C# shell navigation and page lifecycle forwarding; verify the workspace is reachable, navigation-away cancels loading, subscriptions do not accumulate after revisiting, and no form implementation or new XAML file is introduced.
- [x] 5.3 Record how the external session provider and future management tree source connect to these seams; verify missing credentials show the session-required state and normal application execution never falls back to hardcoded tokens or design fixtures.

## 6. Integration Verification and Acceptance Evidence

- [x] 6.1 Run `dotnet test tests/Frontend.UnitTests/TechStore.Frontend.UnitTests.csproj` for the full frontend suite; verify every test passes and record the actual total and outcome in the verification document.
- [x] 6.2 Run `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`; verify zero errors and zero warnings, resolve relevant warnings without broad suppression, and record the actual build summary.
- [ ] 6.3 Exercise the running desktop page using design-like mixed-status fixtures, empty results, load failure/retry, deep-tree search, repeated expand/collapse, keyboard actions, and cross-page context; verify responsive interaction and visual alignment, and keep fixture mode isolated from normal runtime composition.
- [x] 6.4 Review the final diff for no added `.xaml`, backend endpoint/schema changes, database dependencies, persisted icons, or detailed dialogs; run `openspec validate category-list-page-refactor --strict` and verify only authorized frontend/test/documentation changes are included.
- [ ] 6.5 Verify live integration when the external management API and session prerequisites are delivered: confirm secured Bearer access, hidden categories in management data, and authoritative product/SKU counts with defined aggregation semantics; record actual evidence and leave this task unchecked if those prerequisites remain unavailable. Fixture tests do not satisfy this task and this task does not authorize implementing the backend prerequisite.

## Workflow follow-up

- Review the completed implementation and verification evidence before archiving.
- Archive only after required acceptance work is complete; unresolved live integration prerequisites remain explicit outstanding work.
