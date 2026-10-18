# Spec Delta

## MODIFIED Requirements

### Requirement: Offline development shell
Client SHALL open the Shell with valid configuration without requiring the backend/database to be available for shell construction. Shell MUST show the development unauthenticated state when there is no session and MUST NOT create a fake authentication session. When Dashboard is activated, the default live Analytics request may fail visibly; demo fixture data is shown only when preview is explicitly enabled. The Shell itself SHALL remain navigable after that request failure.

#### Scenario: Default offline startup
- **WHEN** the application starts with backend unavailable, valid configuration, and gallery disabled
- **THEN** Shell opens at Overview with “Bản phát triển — chưa đăng nhập”, no fake token/session is created, and the Dashboard reports the actual unauthenticated/network failure without fixture data

#### Scenario: Explicit dashboard preview
- **WHEN** Dashboard preview is enabled and gallery is disabled
- **THEN** Shell opens offline at Overview with a persistent fixture banner and no authentication session or Analytics request

#### Scenario: Shell remains usable after Dashboard API failure
- **WHEN** the Dashboard API request fails during shell startup or navigation
- **THEN** the error is contained in Dashboard state and the user can navigate to the other supported Shell destinations

### Requirement: Six coherent admin destinations
Shell SHALL have exactly six entry points: Overview, Products, Categories and Inventory, Orders, Customers, and Settings. They SHALL be structured with C# Markup Fluent API (`Uno.Extensions.Markup.WinUI` on `NavigationView`). Selected menu, title, and content SHALL reflect the successfully navigated route. Shell Pane Footer SHALL show the active account (FullName, Role) and Logout action through declarative C# Markup bindings. Products SHALL open live Catalog data and provide productId detail navigation; Overview SHALL show live Analytics by default or labeled preview only when enabled; unimplemented business pages SHALL clearly remain placeholders without fabricated data. At expanded desktop widths, the navigation pane SHALL visually follow the left-sidebar hierarchy in `docs/sample_dashboard.png`: brand and subtitle, menu section label, highlighted current destination, compact icon/label rows, Settings separated near the bottom, and account footer. Visual changes MUST preserve existing route, selection, session and logout behavior.

#### Scenario: Visit every destination
- **WHEN** the user visits all six destinations with mouse or keyboard
- **THEN** corresponding content appears, Products loads live Catalog, selected item/title are correct, Settings appears only once in the footer, and no POS/Identity menu entry is added

#### Scenario: Same destination selected again
- **WHEN** the user selects the current route with identical parameters
- **THEN** content/state is preserved and the page is not recreated or loaded again

#### Scenario: Unknown or unavailable destination
- **WHEN** an unsupported route, empty productId, or failed destination creation is requested
- **THEN** Shell retains the prior page/title/selection and reports a safe error instead of leaving Shell blank or selection incorrect

#### Scenario: Read-only detail navigation
- **WHEN** the user opens product card A then navigates to detail B
- **THEN** the correct IDs are passed, the prior activation is cancelled, and Products remains selected; detail does not add a seventh menu entry

#### Scenario: Preview stays within dashboard
- **WHEN** the user views Dashboard live or preview mode and then selects Products or another destination
- **THEN** the six Shell entries remain unchanged, Catalog continues using its live API, and unimplemented destinations remain placeholders

#### Scenario: Shell reflects active user session
- **WHEN** an Admin successfully signs in as `admin`
- **THEN** Shell footer updates the user's name and “Quản trị viên” role and shows “Đã đăng nhập”; after logout it reflects the anonymous state

#### Scenario: Expanded sidebar matches the reference hierarchy
- **WHEN** Shell is shown with its navigation pane expanded at a wide desktop size
- **THEN** brand, menu caption, selected Overview row, other destinations, separated Settings entry and account footer follow the reference's visual hierarchy without adding/removing destinations or changing navigation behavior

### Requirement: Predictable presentation state
Pages SHALL present Loading, Empty, Content, or Error from the current result and provide retry for the current failure; Catalog detail also supports NotFound. Dashboard SHALL distinguish live API success with zero/empty values, live API errors, and preview results; only an explicit preview flag may select fixture data. A new activation resets page data. Back from Catalog detail preserves query/page as values and reloads. Resize, theme, or dialogs SHALL NOT reload or clear current page state. A Dashboard session change SHALL clear data from the previous session and prevent late responses from committing.

#### Scenario: Failure and retry
- **WHEN** the current load fails and the user retries
- **THEN** Error transitions to Loading and then the new result; retries for the same page are not run concurrently and no fake data masks failure

#### Scenario: Placeholder and revisit
- **WHEN** an unimplemented placeholder page is opened, left, and revisited
- **THEN** its new activation starts fresh and ends in Empty with an explanation, without carrying prior input

#### Scenario: Presentation-only change
- **WHEN** a page has current state and the user resizes, changes theme, or opens/closes a dialog
- **THEN** that state remains and no load starts solely because of presentation changes

#### Scenario: Back from product detail
- **WHEN** the user returns from product detail to the list
- **THEN** a new list activation uses the previously applied query/page, reloads API data, and restores focus to the card if present or the list heading

#### Scenario: Leave catalog branch
- **WHEN** the user leaves Products for another Shell destination and later returns
- **THEN** default query/page is used and data/input from the prior Catalog activation is not restored automatically

#### Scenario: Dashboard revisit
- **WHEN** the user revisits Dashboard or invokes refresh after completing an order
- **THEN** a new activation starts at the default period, and refresh requests the selected period from the configured source; live mode reflects the latest API snapshot, preview mode returns fixture data, and prior activation data is not restored

#### Scenario: Dashboard logout and stale request
- **WHEN** logout or session replacement occurs while Dashboard data is present or loading
- **THEN** the old snapshot is cleared, outstanding work is cancelled, and a late response for the former session cannot update the page

#### Scenario: Live zero values and independent empty lists
- **WHEN** a successful live Dashboard response contains zero KPIs or an empty recent-order or low-stock list
- **THEN** zero values and each list's own empty state are shown as successful data, without preview or not-integrated messaging

#### Scenario: Dashboard session and authorization changes
- **WHEN** an anonymous request receives 401, an active session receives 401, an Admin receives 403, or the user logs out during a Dashboard request
- **THEN** anonymous 401 shows sign-in-required Error without changing session; active 401 expires only the matching session; 403 preserves the session and shows access denied; logout clears the snapshot and cancels the request into sign-in-required Empty state

#### Scenario: Dashboard late response after a session change
- **WHEN** a Dashboard request completes after logout, session replacement, or a newer period request
- **THEN** its data, error, or loading finalization does not modify the current page or expose data from the prior session
