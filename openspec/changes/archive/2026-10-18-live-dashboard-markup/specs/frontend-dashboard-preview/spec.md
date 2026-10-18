# Spec Delta

## ADDED Requirements

### Requirement: Dashboard visual composition matches the reference
The live and preview Dashboard SHALL follow the hierarchy and responsive composition in `docs/sample_dashboard.png`: page header and period controls, four KPI cards, a two-column revenue/low-stock row on wide windows, and a full-width recent-orders table below. The chart SHALL make daily scale and values understandable without hover, and existing KPI/list empty and loading states SHALL remain legible in the same layout. Visual matching MUST NOT hardcode sample values or fabricate fields absent from the Analytics contract.

#### Scenario: Wide Dashboard layout
- **WHEN** Dashboard is shown at a wide desktop size comparable to the 1600×1200 reference
- **THEN** four equal KPI cards appear in one row, revenue chart occupies about 70% of the next row with low-stock beside it at about 30%, and the recent-orders table spans the content width below

#### Scenario: Responsive Dashboard layout
- **WHEN** the available page width is medium or narrow
- **THEN** KPI cards reflow to two or one columns, chart and low-stock stack when needed, and recent-order fields remain usable without clipping or hiding important values

#### Scenario: Chart data is readable without hover
- **WHEN** a live or preview snapshot contains daily revenue points
- **THEN** the chart shows a readable scale, date/weekday labels, daily bars, a connected trend line and markers, with the peak day visually distinguished; an accessible daily-value table remains available without hover

#### Scenario: Recent-order data follows the API contract
- **WHEN** recent orders are rendered
- **THEN** the table uses available order code, customer name, product summary, total, status and time fields, status tones reflect real status, and the client does not invent customer phone or IMEI values missing from `AdminDashboardDto`

#### Scenario: Recent-orders table fills the wide panel
- **WHEN** the recent-orders panel is displayed at a wide desktop width
- **THEN** its header, separators, and data rows stretch across the available panel content width; the five real columns distribute across that width with product summary receiving the largest flexible share, amount aligned right, and status kept readable, without a large unused blank region to the right

#### Scenario: Recent-orders table adapts to narrow widths
- **WHEN** the recent-orders panel is displayed below the wide-layout breakpoint
- **THEN** the table remains usable through deliberate compact reflow or horizontal scrolling, while the wide layout does not inherit a fixed minimum width or unconstrained measure that prevents it from filling its panel

#### Scenario: Sample content does not replace live values
- **WHEN** live Dashboard is loaded
- **THEN** KPI, chart, inventory and order values come from the current API snapshot; numbers in `docs/sample_dashboard.png` are visual examples only

## MODIFIED Requirements

### Requirement: Explicit dashboard demo selection
Dashboard SHALL use the authenticated live Analytics API by default; fixture data SHALL be used only when preview mode is explicitly enabled. Preview MUST remain visibly labeled, MUST NOT create an authentication session or call Analytics API, and MUST NOT be selected as a fallback after any live API failure.

#### Scenario: Default unavailable dashboard
- **WHEN** an authenticated Admin opens Overview with preview disabled
- **THEN** the client requests the selected default period from `GET /api/analytics/dashboard`, renders the returned server snapshot and live order codes, and enables period selection and refresh

#### Scenario: Explicit demo launch
- **WHEN** preview mode is explicitly enabled and the gallery is disabled
- **THEN** Overview displays fixture data with a persistent “Dữ liệu minh họa — chưa kết nối Analytics API” label, the fixture reference date and period, and makes no Analytics API request or claim of Admin authentication

#### Scenario: Invalid flag or simultaneous gallery selection
- **WHEN** the preview flag has a value other than unset/0/1 or both gallery and preview are enabled
- **THEN** invalid configuration is rejected safely before display; when both flags are valid, the gallery retains priority and does not load Dashboard implicitly

#### Scenario: API failure never falls back to fixture
- **WHEN** a live Dashboard request fails because of network, timeout, invalid response, authorization, or server error
- **THEN** the Dashboard enters Error with a safe message and retry for the selected period; it MUST NOT render fixture values or classify the failure as a legitimate zero/empty result

#### Scenario: Catalog remains live
- **WHEN** the user leaves Dashboard preview for Products and the Catalog API fails
- **THEN** Catalog reports the actual failure and does not use fixture or Analytics data instead

### Requirement: Recoverable and cancellable dashboard loading
Dashboard SHALL distinguish Loading, Content, a successful zero/empty result, Error, and explicitly enabled preview. Retry SHALL use the failed period. Only the current request and active session generation may update snapshot, error, or loading state. Leaving the page, closing the app, logout, or a session replacement SHALL cancel outstanding work and prevent stale data from appearing; expected cancellation MUST NOT appear as an error.

#### Scenario: Live zero values and empty lists
- **WHEN** a successful live response contains zero KPIs, an empty recent-order list, or an empty low-stock list
- **THEN** KPI values display zero and each empty list has its own empty presentation; the page MUST NOT show the not-integrated or Error state

#### Scenario: Failure and retry
- **WHEN** the current live or preview request fails and the user retries
- **THEN** the page reports a safe error, does not substitute another source or a false empty result, then clears the error and displays the successful result for the same selected period

#### Scenario: Unauthorized, forbidden and logout
- **WHEN** an anonymous request receives 401, an active session receives 401, an Admin receives 403, or the user logs out during a Dashboard request
- **THEN** anonymous 401 shows sign-in-required Error without changing session; active 401 expires only the matching session generation and shows session-expired Error; 403 shows access-denied Error without clearing session; logout clears the snapshot and cancels the request into sign-in-required Empty state, and no case falls back to preview

#### Scenario: Late response after period change or navigation
- **WHEN** a request started under session A completes after logout or session B becomes current, or request A completes after a newer period request B
- **THEN** A's success, error, and finalization MUST NOT change the active page state or reveal A's data; a current authenticated session may load the current period again

#### Scenario: Late response after navigation or close
- **WHEN** a request completes after the user leaves Dashboard or closes the application, including when the transport ignores cancellation
- **THEN** its result/error/loading-finalization does not update the page; resources remain alive until the request releases them

### Requirement: Explicit analytics and seed readiness
Dashboard handoff SHALL distinguish the completed live Analytics client integration from the separate preview and seeder behaviors. Preview MUST NOT be reported as live API integration or as evidence that UC-SEED-01 is complete; client MUST NOT download all real orders to calculate analytics locally.

#### Scenario: Backend rules remain pending
- **WHEN** the live Dashboard is handed off
- **THEN** its route, period keys, DTO fields, Admin authorization, and UTC+07 snapshot semantics are traced to the current `admin-analytics-api` contract; unresolved backend discrepancies are recorded with an owner and status

#### Scenario: Preview remains separate
- **WHEN** the preview flag is enabled
- **THEN** fixture policy is identified as preview-only, and its values are not described as API response or database verification evidence

#### Scenario: Seed preparation
- **WHEN** UC-SEED-01 readiness is reviewed
- **THEN** documentation compares the existing baseline with the Sprint 2 target of at least 20 products, 5 categories, 10 customers, 20 orders and 50 IMEIs, including idempotency/lifecycle/reconciliation requirements, without claiming the Dashboard implemented the seeder
