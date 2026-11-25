# frontend-dashboard-preview Specification

## Purpose

Cung cấp Dashboard minh họa có thể nghiệm thu và bàn giao độc lập với backend Analytics, với nguồn dữ liệu, kỳ thống kê, trạng thái và giới hạn tích hợp được thể hiện rõ ràng.

## Requirements

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

### Requirement: Reproducible and coherent preview data
Dashboard demo SHALL dùng dữ liệu xác định, tiền chính xác và metadata kỳ/as-of rõ ràng. Doanh thu và chuỗi ngày SHALL nhất quán; các tổng KPI MUST NOT được suy từ năm dòng preview. Số sản phẩm SHALL được phân biệt với số máy tồn kho.

#### Scenario: Repeat the same period
- **WHEN** chọn cùng kỳ hoặc khởi động lại cùng fixture version
- **THEN** số liệu và ngày tham chiếu giữ nguyên; tổng điểm doanh thu ngày bằng KPI doanh thu của kỳ, ngày không bán có giá trị 0

#### Scenario: Partial lists and unavailable comparisons
- **WHEN** tổng số cảnh báo/đơn lớn hơn số dòng preview hoặc không có kỳ so sánh hợp lệ
- **THEN** KPI giữ tổng fixture, danh sách chỉ có tối đa năm dòng; tỷ lệ thiếu không bị biến thành 0% hoặc vô cực

#### Scenario: Legitimate zero and empty data
- **WHEN** fixture thành công chứa KPI bằng 0, không đơn hoặc không tồn thấp
- **THEN** thể hiện số 0 hợp lệ và empty cho từng danh sách, không báo Analytics chưa tích hợp hoặc lỗi

### Requirement: Meaningful period selection
Dashboard demo SHALL có Hôm nay, 7 ngày qua và Tháng này dựa trên ngày tham chiếu fixture được công bố, mặc định 7 ngày qua. Doanh thu, đơn và trend SHALL theo cùng kỳ; số sản phẩm và tồn thấp SHALL ghi rõ là snapshot tại thời điểm dữ liệu.

#### Scenario: Change period
- **WHEN** chọn kỳ khác
- **THEN** hiển thị loading, bỏ nội dung kỳ cũ và commit toàn bộ kết quả của kỳ mới cùng nhau; không đặt nhãn kỳ mới trên KPI cũ

#### Scenario: Fixed reference date
- **WHEN** mở cùng demo vào một ngày máy khác
- **THEN** kỳ fixture không âm thầm đổi, UI cho biết khoảng ngày cụ thể và ngày tham chiếu minh họa

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

### Requirement: Accessible reference dashboard
Dashboard SHALL giữ hierarchy mẫu với bốn KPI, doanh thu ngày, tồn thấp và đơn gần nhất; dùng tiếng Việt/VND rõ ràng, theme và keyboard phù hợp. Các dòng demo SHALL chỉ đọc và MUST NOT mở bản ghi thật bằng id fixture hoặc cung cấp action nghiệp vụ giả.

#### Scenario: Responsive theme and text scaling
- **WHEN** resize giữa 1440×1000, 1024×768 và 800×600 DIP, đổi Light/Dark/System hoặc text scale 100/150/200%
- **THEN** panels reflow, nội dung và bộ chọn kỳ vẫn truy cập được, dấu tiếng Việt/focus rõ; trình bày không tải lại dữ liệu

#### Scenario: Chart values without hover
- **WHEN** sử dụng keyboard hoặc công nghệ hỗ trợ để đọc trend, kể cả chuỗi zero/rỗng/một điểm
- **THEN** có dữ liệu ngày–VND tương đương đọc được, không phụ thuộc màu hoặc hover và không lỗi chia cho 0 hay hình học sai

#### Scenario: Preview rows
- **WHEN** xem đơn gần nhất hoặc tồn thấp
- **THEN** dữ liệu có nhãn demo, trạng thái không bị tự chuyển thành Hoàn thành và không có link “Xem tất cả” hay drilldown chưa được triển khai

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
- **THEN** documentation compares the fixture with the M3 targets in `data-seeding`: at least 20 products with images and cost prices on a fresh seed, 5 categories, 10 customers, 20 orders across lifecycle states, and four demo accounts. It identifies 50 IMEIs as the current fixture count and records the seeder's idempotency, lifecycle and Analytics reconciliation evidence separately from Dashboard client verification

### Requirement: Reviewable foundation handoff
Foundation SHALL có hướng dẫn mở rộng theo kiến trúc đã chọn và bằng chứng tách riêng tests, nền tảng, nghiệm thu giao diện và backend readiness. Mọi kết quả SHALL gắn với build/target cụ thể; kiểm tra chưa chạy MUST ghi unverified.

#### Scenario: Another developer extends a feature
- **WHEN** Dev 1–5 đọc bộ bàn giao
- **THEN** xác định được Model/Repository/VM/View, DI, lifecycle/errors/session, navigation/dialog/controls và ví dụ Catalog thật; biết điểm mở rộng cùng phụ thuộc Sprint 2 của mình

#### Scenario: Platform evidence differs
- **WHEN** có Linux Skia evidence nhưng chưa chạy Windows native hoặc chưa xác nhận backend
- **THEN** chỉ ghi Linux verified cho các checks đã chạy; Windows vẫn unverified và Analytics integration chưa hoàn tất

#### Scenario: Owner review
- **WHEN** chủ dự án xác nhận Dashboard của build và target đã xem
- **THEN** record có ngày, build/hash, target và kết quả/ngoại lệ; task owner acceptance mới được tick, không thừa kế acceptance bốn change trước

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

### Requirement: Financial report opens inside the live Overview
The desktop app SHALL place Overview (“Tổng quan”) and Financial Report (“Báo cáo tài chính”) tabs at the top of the existing Overview page for Admin and Manager users with an Active session in live mode. Overview SHALL be the default and preserve the current Dashboard’s data source, date ranges, KPIs, presentation, preview, and navigation.

#### Scenario: Authorized user opens the report
- **WHEN** an Admin or Manager opens Overview and chooses “Báo cáo tài chính”
- **THEN** the client loads the report in place within the existing route and menu entry
- **AND** returning to Overview preserves the Dashboard’s date selection and state for the current activation

#### Scenario: Unavailable role, preview or signed-out session
- **WHEN** the role is SalesStaff or WarehouseStaff, the session lacks authorization, or the Dashboard is in preview mode
- **THEN** the report tab is hidden and the client makes no request to the report endpoint, even if the command to open it is invoked indirectly

### Requirement: Financial report uses an explicit Vietnam calendar range
The report SHALL provide two dates on the Vietnam calendar and an Apply button (“Áp dụng”). The request SHALL send `period=custom` with `from` and `to` in `yyyy-MM-dd` format. The inclusive range is at most 31 days. On first opening the report in an activation, the default SHALL cover today and the preceding six days in UTC+07.

#### Scenario: Unsubmitted or invalid dates
- **WHEN** the user edits draft dates, enters a malformed date, reverses the range, exceeds 31 days, or ends the range after today in UTC+07
- **THEN** the client waits until a valid range is applied before issuing a new query, shows any validation error at the relevant field, and preserves the range label for the current snapshot or in-flight request

#### Scenario: Refresh and retry after editing draft dates
- **WHEN** the user changes draft dates without applying them and presses Refresh (“Làm mới”) or Retry (“Thử lại”)
- **THEN** the client uses the applied start and end dates for the current request or snapshot

### Requirement: Financial report presents server-calculated figures with snapshot metadata
The desktop app SHALL display the period, `asOf` in UTC+07, period totals, and daily figures directly from the report API. Sales, refunds, tax, cash flow, costs, gross profit, and warranty figures SHALL use the corresponding API fields. The app SHALL preserve the server order of rankings and cashier rows.

#### Scenario: Sales and after-sale numbers appear separately
- **WHEN** the API returns original sales and after-sales figures
- **THEN** the report shows sales and refunded sales, sales and refunded tax, receipts and payouts, cash and QR when available, original, restocked and net costs, gross profit before and after returns, and warranty costs as separate figures
- **AND** it shows separate product rankings by quantity and revenue, cashier performance, and cash-by-shift rows when supplied by the API
- **AND** the client uses the server’s net figures, costs, profits, cash drawer figures, and ranking order without calculating them from the rows or reordering them

#### Scenario: Financial values are readable without relying on color
- **WHEN** a report contains zero, negative, or valid fractional amounts
- **THEN** the tables and period totals retain Vietnamese labels, VND formatting, negative signs, and meaningful fractional digits

### Requirement: Financial report distinguishes emptiness, unavailable amounts and failure
The report SHALL have loading, content, empty, and error states independent of the Dashboard. The client SHALL show zero as “0 ₫”, unavailable amounts as “Chưa tính được”, a separate message for empty lists, and a safe load error with recovery available to authorized users.

#### Scenario: Successful empty range or legacy response
- **WHEN** a successful range has all available activity metrics at zero and empty lists
- **THEN** the report says no nonzero activity was recorded and keeps the period, `asOf`, and zero-value daily rows visible
- **AND** when a field is unavailable, the report retains known values and marks the unavailable field without inferring that the period had no transactions

#### Scenario: Current request fails
- **WHEN** the API returns an authorization or date range error, the payload has an invalid structure, or a network error or timeout occurs
- **THEN** the report shows the appropriate error in the report view without loading fixture data, relabeling old figures with the new period, or presenting the error as an empty period
- **AND** 403 preserves the current session and 401 follows the existing authentication flow

#### Scenario: Required JSON amount is invalid
- **WHEN** `summary` or any `daily[].totals` is missing a required field or contains `null` for a required non-nullable numeric field
- **THEN** the client reports an invalid response

#### Scenario: Valid zero or unavailable JSON amount
- **WHEN** a valid payload contains zero, a nullable field set to `null`, or an omitted optional amount
- **THEN** the client displays “0 ₫” for zero and “Chưa tính được” for unavailable amounts

### Requirement: Financial requests respect the selected period and session lifetime
The report SHALL let users apply a new valid range while loading and cancel the previous request. Only the latest request for the current activation, open report view, and valid session generation SHALL update data, errors, or loading state. Expected cancellation SHALL produce no error.

#### Scenario: Older success or failure arrives after a newer request
- **WHEN** a request for range A succeeds or fails after the user submits range B
- **THEN** the range label, snapshot, errors, and loading state reflect only B, even if the transport ignores cancellation

#### Scenario: Logout, account replacement, route exit or app close
- **WHEN** the session generation changes or the user leaves the report, leaves the page, or closes the app during loading
- **THEN** the report request is canceled and late results are discarded. Signing out or switching accounts clears the old range and figures. The new account opens Overview in its default state
- **AND** switching views, retrying, and loading the report leave independent Dashboard requests running as before

### Requirement: Financial report remains accessible across desktop targets
The report SHALL support keyboard navigation, visible focus, and readable layouts after resizing or switching themes on Skia desktop and native Windows. Tab labels SHALL also serve as accessible names. The selected tab SHALL have a cue other than color and expose its state to assistive technology. Presentation changes SHALL preserve the displayed range and SHALL NOT reload data.

#### Scenario: Keyboard, narrow window and live theme switch
- **WHEN** the user selects the report, enters dates, retries, and reads figures with the keyboard at 800×600, 1024×768, or 1440×1000 DIP, then switches between light and dark while the page is open
- **THEN** focus stays out of hidden panels, all required rows and columns remain readable, and colors update immediately on the same snapshot
