# Design

## Context

See `proposal.md` for motivation and scope. Inspection found:

- Uno SDK 6.7.30, .NET 10 desktop/Windows targets, Central Package Management, and the `Mvvm` Uno feature already exist. The installed .NET SDK is 10.0.402.
- The client currently contains `App.xaml`, `MainPage.xaml`, and starter code; no category feature, API client, token/session provider, dialog service, or frontend test project exists. The IDE's `category-management-frontend` proposal is absent from this checkout.
- The local design is now available at `docs/designs/category-list.html`. Its main workspace contains header, tabs, toolbar, table, and footer. The outer sidebar belongs to the application shell.
- `CategoryTreeNodeDto` contains identity, name, slug, parent, status, description, and nested `SubCategories`; it has no product/SKU counts. `CategoryService.GetTreeAsync` uses an active-only repository query. The main `category-catalog` spec explicitly excludes hidden branches.
- `Program.cs` does not currently register the authentication extension present in untracked work. Neither archived proposals nor unrelated working-tree changes establish an implemented runtime dependency.

## Goals / Non-Goals

**Goals:** Keep the feature view declarative, make all hierarchy transformations testable without WinUI, faithfully translate the HTML workspace to native controls, and keep integration limitations visible in acceptance evidence.

**Non-Goals:** Implementing backend management reads, inventing count semantics, creating a login flow, rebuilding the entire sidebar, implementing category mutation dialogs, or upgrading Uno/.NET. Existing application XAML is outside the feature rewrite; no new XAML is introduced.

## Decisions

### 1. Native C# layout with an explicit HTML mapping

Create `Features/Categories/Pages/CategoryManagementPage.cs` using WinUI control object initializers and programmatic bindings; Uno fluent extensions can be used if already resolved. Object initializers are explicitly allowed by the request and avoid requiring another markup package solely for syntax. Do not use `XamlReader`, embedded XAML strings, WebView, or HTML rendering as an implementation shortcut.

The page composes the following mapping; dimensions are logical display units, corresponding to CSS pixels at 100% scale.

| HTML element/classes | Native presentation |
| --- | --- |
| `main px-gutter-desktop py-space-lg`, inner `gap-space-lg` | Workspace padding 24 and vertical gap 24; exclude sidebar's `pl-60` offset from page content |
| Header `sm:flex-row`, `text-xl font-bold`, count `ml-2.5` | Reflowing header; 20-unit bold title, 14-unit count, gap 10 |
| Primary button `px-4 py-2 gap-1.5`, add symbol | Padding 16/8, spacing 6, 14-unit medium label, 18-unit add icon; requested 8-unit corner radius |
| Tabs `gap-6 -mt-2`, `py-3 px-1`, `border-b-2` | Gap 24, effective previous gap 16, padding 4/12, active 2-unit underline; badges 12-unit text and padding 8/2 |
| Toolbar `gap-3 -mt-2`, search `max-w-lg h-10 pl-10 pr-3` | Gap 12, effective previous gap 16, search maximum width 512, height 40, left/right insets 40/12; search icon at left 12 |
| Status `min-w-[200px] h-10 px-3 pr-8` | Minimum width 200, height 40, padding 12 with 32 reserved for native chevron; selected value bound two-way |
| Card `rounded-xl shadow-sm border ... overflow-hidden` | White Border with clipping, requested 12-unit radius and opaque Slate 200 border; subtle supported native shadow |
| `thead bg-slate-50 text-xs uppercase tracking-wider py-3.5 px-4` | Slate 50 header; 12-unit semibold uppercase labels, horizontal padding 16, vertical padding 14, letter spacing matching 0.05em |
| Root `td py-3.5 px-4`, `gap-2`, `w-6 h-6`, `w-9 h-9` | Cell padding 16/14; gap 8; 24-unit chevron hit presentation; 36-unit icon tile; root name semibold 14 |
| Child `bg-slate-50/50 py-3 px-4 pl-12` | Half-opacity Slate 50 background, horizontal/vertical padding 16/12, first-column left padding 48, muted branch glyph; name medium 14; parent label 12 |
| Status `w-9 h-5` / `w-7 h-4` | Read-only switch-shaped status indicator, root 36×20, child 28×16, plus “Bật”/“Đã ẩn”; hidden rows opacity 0.75 |
| Actions parent `p-1.5 text-[18px]`, child `p-1 text-[16px]` | Right-aligned icon buttons with root padding 6/icon 18 and child padding 4/icon 16, tooltips and keyboard focus |
| Footer `p-4 bg-slate-50 gap-3`, buttons `h-8 gap-1.5` | Padding 16, gap 12, height 32 controls with 6-unit spacing; active page blue |

Use Plus Jakarta Sans with the HTML's 400/500/600/700 weights as a packaged local asset for visual parity, with a system sans-serif fallback. Icons must render offline through local glyph/vector resources; do not depend on Google Fonts CSS or display Material ligature strings as plain text. Implement hover and focus states programmatically, including primary hover, row hover, neutral edit hover, and red delete hover.

Explicit request values override design conflicts: `bg-surface` resolves to `#F7F9FB` in HTML but the requested page background is `#F8FAFC`; Tailwind overrides `rounded-lg` to 4 and `rounded-xl` to 8, but requested controls/card use 8/12. The HTML card border uses 60% opacity and root badges use `rounded`; use the requested opaque `#E2E8F0` border and capsule badges. Keep remaining HTML spacing, text hierarchy, slate colors, and blue/indigo/teal/emerald tints. Unsupported/nonstandard `py-0.2` on the sample promotional badge is not a valid spacing token.

Do not hardcode sample counts `24`, `1.420`, `1.385`, or product/SKU numbers. Inventory/serial badges can show “—” until their real sources exist. The static “Bán chạy” badge is not inferred from an iPhone name: omit it without authoritative data rather than introduce sales analytics in this step.

### 2. ViewModel owns the complete visible-row projection

Place `CategoryManagementViewModel` under `Features/Categories/ViewModels/`, inheriting `ObservableObject`. Use generated observable properties and relay commands. Keep `RootCategories`, stable IDs, expansion state, `SearchText`, a typed `StatusFilter` (All/Active/Hidden), `VisibleRows`, loading/error state, and pagination state independent of WinUI.

Row state includes category identity, direct parent name, depth, status, children/expansion flags, `IsContextOnly`, nullable product/SKU totals, and icon descriptor. Colors are strings/tokens and icons are descriptors; brushes, glyph control instances, and native enums stay in the presentation layer. The page only builds controls, binds values/commands, and forwards lifecycle or dialog-display events. No tree traversal, filtering, counting, API calls, or state mutations belong in view handlers.

Projection algorithm:

1. Traverse the loaded tree in API sibling order, index by identity, and count unique nodes for `TotalCount`. Initially expand branches to match the open HTML tree.
2. Normalize search by trimming and compare names case-insensitively; combine name and status predicates with AND. Do not introduce slug search or accent-insensitive matching beyond the request.
3. With no filters, eligible rows follow stored expansion state. With filters, compute matching nodes across the entire tree, include their ancestor closure, and force required paths open without overwriting stored expansion state. Matching a parent alone does not include nonmatching children.
4. Disable collapsing a forced-open filter path; all other branch toggles remain local. Clearing filters restores pre-filter expansion. Descendants deeper than one level add 24 units per level beyond the initial child indentation of 48.
5. Page eligible matching nodes in preorder at page size 15, then attach each page's ancestor context. Deduplicate ancestors within that page. Repeated context is not a new match and cannot consume page slots.
6. `TotalCount` and the active tab count describe all loaded unique nodes; `FilteredCount` describes all matches before expansion; footer `Y` describes eligible matches after expansion, excluding context. `A/B` use page indices into that eligible sequence. Empty range is 0–0. Filters reset page 1; collapse clamps the current page; page navigation preserves expansion.
7. Update a stable observable row collection with bounded changes rather than reconstructing the whole page. Do not perform network work on search, filter, expansion, or page navigation.

This explicit flat projection is preferred over a native TreeView template because five aligned columns, filter context, and deterministic pagination otherwise become hard to coordinate. Pagination over roots was rejected because the footer counts categories and a single root could contain most of the result.

### 3. API and session seams with honest missing data

Add `Services/Api/ICategoryApiClient` and its HttpClient implementation. `GetTreeAsync(CancellationToken)` returns a client-owned tree snapshot wrapping shared DTO data plus optional, explicitly unavailable management statistics. The current adapter maps `SubCategories` recursively and leaves count values null; it does not change the shared DTO or infer counts from child category counts. A future compatible management adapter can supply real statistics after their API contract is defined.

Obtain tokens from an injected session/access-token abstraction per request, assign `Authorization: Bearer ...` to the request message, and propagate cancellation through HTTP and deserialization. Do not store a token literal in the page, a shared default header, source code, or logs. Missing token gives a session-required result; 401 and 403 map to distinct user-facing states. Network/server errors offer retry. Use cancellation plus a load generation guard so navigation cancellation or older responses cannot replace current state. Always clear busy state after success, failure, or cancellation.

Register feature dependencies in the application composition root and add a minimal C# shell navigation hook to reach the workspace. Do not replace the application shell solely to reproduce the HTML sidebar. Supply session tokens through the session abstraction; implementing login/refresh is a separate dependency, not a hardcoded development credential fallback.

### 4. Explicit API prerequisites preserve existing contracts

The frontend cannot recover hidden categories from the current active-only endpoint. It also cannot establish product versus SKU counts, category subtree aggregation, or inventory/serial badge totals from `CategoryTreeNodeDto`. Keep `category-catalog` unchanged and document a prerequisite for an authorized management tree source including hidden categories and authoritative, defined count semantics. Authentication must also be registered by the owning backend work before claiming secured live integration.

Do not change `/api/categories/tree` to return hidden nodes by default, fan out product requests per row, add database access, or label absent values as zero. Fixtures can validate complete mixed-status behavior through the API abstraction, but the release evidence must distinguish those tests from live acceptance. Product/SKU aggregation rules remain the responsibility of the upstream contract; this change only formats authoritative values.

### 5. Pure icon resolver and deferred dialog actions

`CategoryIconResolver` normalizes name/slug and returns a device-family descriptor plus tint tokens. Prefer specific tablet and laptop matches before general phone/vendor terms, and use a neutral folder/category fallback. Test Vietnamese names, English slugs, mixed case, ambiguous vendor names, and unknown/empty input. Do not infer category family from vendor alone when the vendor sells multiple device classes.

Commands invoke an injected category dialog-request contract: Add has no target, Edit/Delete carry the exact selected ID. The host can subscribe to open a placeholder dialog through a UI service, with lifecycle-safe unsubscription. This is the extension point for later form implementation; no API mutation or state toggle is triggered now. A read-only native-looking status indicator preserves HTML presentation while avoiding an unsupported write flow.

### 6. Headless tests without a new application architecture

Create `tests/Frontend.UnitTests/TechStore.Frontend.UnitTests.csproj` targeting standard `net10.0` and register it in the solution. Explicitly link the production ViewModel, row state, resolver, API client, and their UI-free interfaces as compile inputs; reference shared contracts and the same compatible CommunityToolkit.Mvvm dependency. This small initial test project avoids referencing a desktop executable or loading WinUI and keeps source ownership under the requested feature paths. A dedicated presentation assembly can replace source links if later features justify it.

Use xUnit and FluentAssertions in the repository's AAA style, with hand-written fake API/session/dialog services and an in-memory HttpMessageHandler. Test hierarchy/filter/page interactions, identity routing, non-fabricated counts, authentication headers/errors, retries, and stale/cancelled loads. Do not write snapshot tests that merely mirror the control constructors. Native layout, fonts, scrolling, and interaction styling require visual verification.

## Risks / Trade-offs

- [Missing management API/session contract] → Complete frontend behavior using deterministic fakes and current adapter; record live hidden/count/authentication checks as outstanding prerequisites. Do not mark full acceptance complete without them.
- [HTML/request discrepancies] → Apply the explicit override list above and retain a visual review checklist mapping all workspace regions to HTML. The outer sidebar remains shell-owned.
- [Fonts and glyph resources absent] → Package local assets with appropriate attribution during implementation; use native vector resources if symbol glyph support is unreliable and verify visually on Skia.
- [Context ancestors exceed 15 rendered rows] → Page size limits matching categories, not repeated context rows; footer and tests use this definition consistently.
- [Filtered ancestor indicators could mislead] → Preserve the ancestor's actual status, mark it as context in row state, and never count it as a matching result.
- [Headless source links can drift] → List explicit production files in the test project and run both client build and frontend tests as acceptance checks.
- [Existing build warnings] → Capture baseline results and resolve relevant warnings rather than adding broad suppressions or claiming a zero-warning build without evidence.

## Migration Plan

1. Add the UI-free contracts, feature classes, tests, and composition/navigation integration without backend schema changes.
2. Build the desktop target, run all frontend tests, and compare the workspace to the local HTML using both design-like fixtures and empty/error/narrow-window cases.
3. Verify the live authorized management source and session integration separately when their owning work is available; record blocked prerequisites accurately.
4. Roll back by reverting feature and navigation registrations. No persisted category data or schema migration needs reversal.
