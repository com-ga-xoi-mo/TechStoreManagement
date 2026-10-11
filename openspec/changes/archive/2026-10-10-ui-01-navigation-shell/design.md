# Design

## Context

See [proposal.md](proposal.md) for motivation and [client-navigation-shell/spec.md](specs/client-navigation-shell/spec.md) for behavior. UI-00 is implemented and archived: `TechStoreTheme.xaml` is merged, `ThemePreviewView` is hosted in `MainPage`, and `App.xaml.cs` already has a small DI container. `MainPage` currently exposes seven static `NavigationViewItem`s plus Settings, but no selection handler or route lifecycle. The D01, D23 and D20 image/HTML sources agree on five main items, a branded left sidebar, active-item treatment, Settings and a user footer; D23/D20 differ in font/color details, so D01 structure and D31/UI-00 tokens win. There is no client `Services/Navigation`, `Services/Dialog`, `Services/Session`, `Repositories`, or `Models` implementation yet. No running auth/capability API contract was found in code.

`docs/MVVM_REPOSITORY_BASE_PROJECT.md` §6 and the user's explicit direction put `Models/`, `Views/`, `ViewModels/`, `Repositories/`, and `Services/` beside each other, with interfaces beside implementations. Older `docs/ARCHITECTURE.md`/AGENTS guidance illustrates a `Features/` layout; do not migrate to it in this change. Shell has no business data to retrieve, so adding a fake repository or ApiClient merely to satisfy a diagram would create an unused layer. Later business ViewModels use repository interfaces; repository implementations use ApiClient once an API contract exists.

## Goals / Non-Goals

**Goals:**
- Preserve one Shell instance while swapping route content, maintaining selected item and route history.
- Provide small, replaceable navigation, dialog, and session seams via the existing DI bootstrap, including a way to resolve route Views/ViewModels without requiring `Frame` to constructor-inject Pages.
- Keep UI-00 theme and preview available for development, with no extra business menu entry.
- Make preview/demo status explicit and keep future authenticated capability filtering testable without pretending that role claims or endpoints already exist.

**Non-Goals:**
- Dashboard, catalog, inventory, order, customer, POS, settings, login or account screens; their data, forms and actions belong to later UI tasks.
- Real token storage, role-to-capability mapping, auth API, permission enforcement, or endpoint assumptions. Backend alone enforces authorization.
- A generic navigation framework, modal stack, global event bus, or all future dialog types.

## Decisions

### 1. Native persistent shell and route hosting

Use `Views/Shell/ShellPage.xaml` with `NavigationView` (left pane, D01 brand/menu/footer) and one nested content `Frame` or equivalent content host. `App.OnLaunched` navigates the root Frame to `ShellPage`; destination navigation affects only the nested host. Start on Tổng quan. Use UI-00 keyed brushes/spacing and native SymbolIcons; do not duplicate D23/D20 CSS colors or embed their HTML. Set the pane behavior for compact desktop widths. After agent build/runtime checks, ask the project owner to inspect the normal desktop window and a narrower still-usable window at their current display settings, following plan §6; record their dated confirmation and any observed clipping. Exact dimensions, scale percentages and screenshots are not required. Remove the old `MainPage` from startup or convert it to a thin compatibility redirect only if required by generated Uno code. Keep `ThemePreviewView` reachable by a DEBUG-only developer route or equivalent preview mechanism, not in the five business items.

**Alternative:** keep expanding `MainPage` or make every menu click replace the root Frame. This would retain the seven-item template or destroy persistent sidebar state.

### 2. Explicit route catalog and consistent history

Define stable route IDs (`Dashboard`, `Products`, `CategoriesInventory`, `Orders`, `Customers`) in `Services/Navigation` or a small UI-independent model; one mapping owns labels, icons, route IDs and placeholder destinations. `INavigationService` exposes Navigate/GoBack/CanGoBack/current route without leaking `Frame` into ViewModels. `NavigationService` owns the nested Frame adapter and synchronizes route events to `ShellViewModel` so selected item also changes on Back. Re-selecting the current route is a no-op; unknown/disallowed routes leave the current route and selection intact and surface a readable unavailable message. Prevent the root shell from being pushed into nested history. Use a single reusable placeholder Page/ViewModel parameterized by route/title; later changes replace one route mapping at a time, not the shell.

**Alternative:** one placeholder Page class per destination or tags and string comparisons scattered through XAML code-behind. Those duplicate state and make Back/selection drift likely.

### 3. DI and ViewModel boundary

Extend the existing `App.Services` registration minimally: singleton navigation/session/dialog services for the one desktop window, and transient Shell/placeholder ViewModels. Since native `Frame.Navigate` creates Pages, keep parameterless View constructors and resolve their ViewModels at the View boundary through the DI provider; use code-behind only for `InitializeComponent`, `DataContext`/`x:Bind` setup, and necessary `Frame`, `XamlRoot` and navigation lifecycle attachment. Commands and state stay in `ShellViewModel` using CommunityToolkit.Mvvm. No ViewModel references `Page`, `Frame`, `ContentDialog`, HTTP or JSON. On Shell unload/window teardown detach event handlers and dialog anchor to avoid duplicate subscriptions. If multiple windows are introduced later, revisit service lifetime rather than pretending this singleton design covers them.

**Alternative:** injecting a Page directly into a ViewModel or building a custom framework-wide ViewModel factory. Both add unnecessary coupling for five shell routes.

### 4. Session and capability seam without fabricated authorization

Use an `ICurrentSession` (name can follow local convention) and a small snapshot containing display status plus an allowed-route set. Wire an explicit preview implementation via DI for UI-01; its footer says “Chế độ xem trước” and it permits five placeholder routes only for UI review. Never display the reference image's “Admin TechStore” as an authenticated identity. A separately injected known-capability snapshot can drive menu filtering in headless tests; if preview mode is off and capability is unknown, do not show protected routes as granted or infer permissions from `RoleType`. When a capability snapshot changes, choose an allowed fallback and synchronize route/selection. UI-02 and a confirmed backend/session contract will replace the preview provider; `401/403` handling and real claims mapping are deferred. Client menu filtering is UX only; every protected API action remains server-authorized.

**Alternative:** hard-code Admin role or map the existing four `RoleType` values to menu access now. No endpoint/capability contract or login flow exists to justify that behavior.

### 5. Minimal informational dialog seam

Place `IDialogService` and `DialogService` together under `Services/Dialog`. The service can show one shell-level informational `ContentDialog` with the Shell's `XamlRoot`, supports Escape/close, and serializes or rejects a second open request clearly. Attach the `XamlRoot` from the View boundary; do not let ViewModels create controls. A small placeholder-only “Thông tin” action may exercise it in development; there are no business dialogs or nested dialogs in UI-01.

**Alternative:** construct `ContentDialog` inside each ViewModel or implement the full future POS/dialog workflow now. Both break the scope and test seam.

## Risks / Trade-offs

- **Reference images show Admin data but there is no authenticated session** → Footer clearly says preview; no fake identity or authorization claim.
- **Unknown capability contract** → Allow preview-only route exercise; define a testable snapshot seam, list real integration as pending UI-02/backend agreement, and do not invent endpoints.
- **Frame navigation and native Back can desynchronize active state** → Central route mapping, navigated/back event synchronization, no-op repeat navigation, and headless history tests plus runtime smoke check.
- **NavigationView may render differently in Uno Skia and Windows** → Reuse UI-00 native theme, test on Linux desktop now, record Windows native as unverified until a Windows host is available.
- **Narrow pane/footer may clip Vietnamese labels** → Test compact pane, tooltip/accessibility labels, keyboard focus, vertical reachability and readable placeholder text at the owner's usable window sizes.
- **`ContentDialog` requires a live `XamlRoot` and permits one active dialog** → Attach/detach with Shell lifecycle, guard re-entry, and smoke-test actual open/close at runtime.

## Migration Plan

1. Keep UI-00 resources and preview source. Add the three service seams and route catalog, register them in the existing DI composition root, then add Shell and placeholder Views/ViewModels.
2. Redirect app startup from template `MainPage` to `ShellPage`, remove the seven-item starter menu, and smoke-test all five placeholder routes, Back, active state, compact pane, footer, and one informational dialog.
3. Record UI-only/preview status and Windows limitation in a UI-01 handoff. Roll back startup navigation and service registrations to restore UI-00 preview if Shell startup fails; no data migration or backend rollback is involved.

## Open Questions

- The authoritative session/capability response shape and route-to-capability mapping remain for UI-02/backend contract work. UI-01's preview and test snapshot behavior is fully defined without them.
- The Settings screen and any user-menu actions lack designs; leave them unavailable until separately specified.
