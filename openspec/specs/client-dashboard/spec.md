# client-dashboard Specification

## Purpose

Gives TechStore staff a readable, time-filtered overview of sales and inventory in the desktop Shell, with explicit preview provenance until authoritative analytics data is integrated.

## Requirements

### Requirement: Dashboard overview composition
The client SHALL show a Dashboard page with four KPI cards, a revenue trend, low-stock items and recent orders using the established Shell and theme.

#### Scenario: Dashboard opens after preview login
- **WHEN** an explicit preview session enters the Tổng quan route
- **THEN** the Dashboard page shows revenue, order count, total products and low-stock count cards, a revenue chart, low-stock items and up to five recent orders

#### Scenario: Preview provenance
- **WHEN** Dashboard figures come from the mock source
- **THEN** the page visibly identifies the figures as sample/preview data and does not claim a verified administrator, live revenue, inventory or backend permission

#### Scenario: Narrow desktop window
- **WHEN** the window is narrowed while Dashboard is visible
- **THEN** cards, chart, alerts, table/list and feedback remain readable and reachable by reflow or scrolling without horizontal clipping

### Requirement: Consistent period filtering
The client SHALL offer Hôm nay, 7 ngày qua and Tháng này and load period-dependent sections from one consistent result for the selected range.

#### Scenario: Change range
- **WHEN** the user selects a different date range
- **THEN** the selected state, displayed range, revenue, order count, comparison, trend and period-filtered recent orders update together from the newly selected result

#### Scenario: Current-state inventory metrics
- **WHEN** the selected sales period changes
- **THEN** total products and low-stock alerts remain marked as an as-of inventory snapshot rather than being presented as sales-period totals

#### Scenario: Rapid selection
- **WHEN** a slower response for an earlier range finishes after a newer range was selected
- **THEN** the earlier result does not overwrite the newer selection or its displayed figures

#### Scenario: Comparison unavailable
- **WHEN** the source has no valid comparable prior period
- **THEN** the page shows an unavailable comparison instead of a fabricated percentage or division-by-zero result

### Requirement: Revenue trend is understandable
The client SHALL present revenue points for the selected period with readable dates, currency values and a nonvisual equivalent of the chart data.

#### Scenario: Populated trend
- **WHEN** the selected result contains dated revenue points
- **THEN** each point is placed in chronological order and its date and amount can be read without relying on color or a mouse hover

#### Scenario: Zero or sparse trend
- **WHEN** the selected result contains zero-value days or too few points for a curve
- **THEN** the chart still represents those values accurately without inventing missing revenue or a misleading interpolated peak

### Requirement: Low-stock and recent-order summaries
The client SHALL show source-provided low-stock alerts and at most five recent orders without treating their visible subset as the basis of whole-store KPIs.

#### Scenario: Low-stock alert
- **WHEN** an item is reported below its effective safety threshold
- **THEN** the page shows its product/variant identity, available quantity, unit and low-stock status without claiming a warehouse detail page exists

#### Scenario: Recent orders
- **WHEN** the selected period contains orders
- **THEN** up to five newest orders show identifying code, customer/product summary, amount and status, with dates/statuses consistent with the selected result

#### Scenario: Unimplemented links
- **WHEN** a user activates an order, product or “Xem tất cả” affordance before its destination screen is implemented
- **THEN** the client keeps the user on Dashboard and clearly reports that the destination is unavailable

### Requirement: Dashboard data states
The client SHALL distinguish loading, populated, empty and failed results and offer a retry after failure.

#### Scenario: Loading
- **WHEN** a range request is pending
- **THEN** loading feedback is visible and stale figures are not presented as the newly selected range

#### Scenario: Empty range
- **WHEN** the selected range has no sales data
- **THEN** revenue/order sections show an explicit empty or zero state while available current-state inventory information remains identifiable

#### Scenario: Failure and retry
- **WHEN** the mock source fails
- **THEN** the page shows a nontechnical error and retry action, and retry requests the current selected range without silently switching sources

#### Scenario: Leave page during load
- **WHEN** the user leaves Dashboard while a request is pending
- **THEN** that request cannot later replace the state shown for a newer visit or another selected range
