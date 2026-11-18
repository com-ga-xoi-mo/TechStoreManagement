# Spec Delta

## MODIFIED Requirements

### Requirement: Explicit dashboard demo selection

Dashboard SHALL use the authenticated live Analytics API by default; fixture data SHALL be used only when preview mode is explicitly enabled. Preview MUST remain visibly labeled, MUST NOT create an authentication session or call Analytics API, and MUST NOT be selected as a fallback after any live API failure.

#### Scenario: Default unavailable dashboard
- **WHEN** an authenticated Admin or Manager opens Overview with preview disabled
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

Dashboard SHALL distinguish Loading, Content, a successful zero/empty result, Error, and explicitly enabled preview. Retry SHALL use the failed period, including both applied dates for a custom range. Only the current request and active session generation may update snapshot, error, or loading state. Leaving the page, closing the app, logout, or a session replacement SHALL cancel outstanding work and prevent stale data from appearing; expected cancellation MUST NOT appear as an error.

#### Scenario: Live zero values and empty lists
- **WHEN** a successful live response contains zero KPIs, an empty recent-order list, or an empty low-stock list
- **THEN** KPI values display zero and each empty list has its own empty presentation; the page MUST NOT show the not-integrated or Error state

#### Scenario: Failure and retry
- **WHEN** the current live or preview request fails and the user retries
- **THEN** the page reports a safe error, does not substitute another source or a false empty result, then clears the error and displays the successful result for the same selected period

#### Scenario: Unauthorized, forbidden and logout
- **WHEN** an anonymous request receives 401, an active session receives 401, an authenticated Dashboard caller receives 403, or the user logs out during a Dashboard request
- **THEN** anonymous 401 shows sign-in-required Error without changing session; active 401 expires only the matching session generation and shows session-expired Error; 403 shows access-denied Error without clearing session; logout clears the snapshot and cancels the request into sign-in-required Empty state, and no case falls back to preview

#### Scenario: Late response after period change or navigation
- **WHEN** a request started under session A completes after logout or session B becomes current, or request A completes after a newer period request B
- **THEN** A's success, error, and finalization MUST NOT change the active page state or reveal A's data; a current authenticated session may load the current period again

#### Scenario: Late response after navigation or close
- **WHEN** a request completes after the user leaves Dashboard or closes the application, including when the transport ignores cancellation
- **THEN** its result/error/loading-finalization does not update the page; resources remain alive until the request releases them

### Requirement: Explicit analytics and seed readiness

Bàn giao SHALL phân biệt tích hợp live Analytics client đã hoàn tất với hành vi preview và seeder độc lập. Preview MUST NOT được báo là tích hợp live API hoặc bằng chứng UC-SEED-01 hoàn tất; client MUST NOT tải toàn bộ đơn thật để tự tính analytics.

#### Scenario: Backend rules remain pending
- **WHEN** the live Dashboard is handed off
- **THEN** its route, period keys, DTO fields, Admin/Manager authorization, and UTC+07 snapshot semantics are traced to the current `admin-analytics-api` contract; unresolved backend discrepancies are recorded with an owner and status

#### Scenario: Preview remains separate
- **WHEN** the preview flag is enabled
- **THEN** fixture policy is identified as preview-only, and its values are not described as API response or database verification evidence

#### Scenario: Seed preparation
- **WHEN** UC-SEED-01 readiness is reviewed
- **THEN** documentation compares the existing baseline with the Sprint 2 target of at least 20 products, 5 categories, 10 customers, 20 orders and 50 IMEIs, including idempotency/lifecycle/reconciliation requirements, without claiming the Dashboard implemented the seeder

## ADDED Requirements

### Requirement: Live Dashboard supports a custom date range

Dashboard live SHALL keep Hôm nay, 7 ngày qua and Tháng này, defaulting to 7 ngày qua on a new activation. It SHALL also let an authorized user select Khoảng ngày, enter a start and end date on the Vietnam calendar, and apply an inclusive range of 1–31 days. It SHALL show the report dates and distinguish the current product/stock snapshot from period totals. Preview SHALL expose only its three existing periods.

#### Scenario: Apply a valid range in live mode

- **WHEN** Admin or Manager enters two valid dates and selects Áp dụng
- **THEN** the client requests `period=custom` with both dates formatted as `yyyy-MM-dd`, renders the whole live snapshot for those dates, and displays the calendar dates and snapshot update time in UTC+07

#### Scenario: Invalid or unsubmitted inputs

- **WHEN** either date is missing, the interval is reversed, exceeds 31 days or ends after today in Vietnam, or the user has edited the dates without applying them
- **THEN** no custom request is sent for the draft. The input indicates any validation problem and the displayed interval continues to describe the applicable request or result

#### Scenario: Refresh and retry the applied range

- **WHEN** a user refreshes a custom result or retries a failed custom request after editing draft dates
- **THEN** the action uses the same two previously applied dates. Switching to a preset sends only that preset, keeps the requested period and displayed snapshot aligned, and preserves existing cancellation/session rules

#### Scenario: Show an accessible historical period

- **WHEN** a historical range spans different months, or the user selects the range with keyboard at a narrow desktop width
- **THEN** the date inputs and apply action are usable, the chart and daily-value table identify the dates unambiguously, and the Dashboard distinguishes historical order/revenue data from current active products and low stock

#### Scenario: Explicit preview keeps its existing choices

- **WHEN** preview mode is enabled
- **THEN** only Hôm nay, 7 ngày qua and Tháng này are offered, using the existing fixture dates and banner without an Analytics API request
