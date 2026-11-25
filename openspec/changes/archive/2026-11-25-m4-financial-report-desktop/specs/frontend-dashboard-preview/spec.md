# Spec Delta

## ADDED Requirements

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
