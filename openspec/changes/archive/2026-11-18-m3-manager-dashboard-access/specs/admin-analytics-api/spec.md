# Spec Delta

## RENAMED Requirements

- FROM: `### Requirement: Admin-only dashboard analytics endpoint`
- TO: `### Requirement: Authorized dashboard analytics endpoint`

## MODIFIED Requirements

### Requirement: Authorized dashboard analytics endpoint

The API SHALL expose `GET /api/analytics/dashboard` supporting periods `today`, `last7Days`, `thisMonth`, and `custom`. When `period` is omitted and no date parameters are provided, the default SHALL be `last7Days`. The endpoint SHALL require a valid JWT and grant the same store-wide read scope to Admin and Manager.

#### Scenario: Admin reads dashboard

- **WHEN** Admin sends a request with a valid period
- **THEN** the API returns HTTP 200 with summary, daily revenue, recent orders, low-stock balances, and snapshot metadata

#### Scenario: Manager reads dashboard

- **WHEN** Manager sends a request with a valid period
- **THEN** the API returns HTTP 200 with the same store-wide data scope and response contract as Admin

#### Scenario: Authentication and role enforcement

- **WHEN** a request lacks a JWT or carries an invalid JWT, or SalesStaff or WarehouseStaff calls the endpoint with a valid JWT
- **THEN** the API returns HTTP 401 for authentication failures or HTTP 403 for disallowed roles without returning analytics data

#### Scenario: Invalid period

- **WHEN** an authorized caller sends an unsupported period
- **THEN** the API returns HTTP 400 with a validation error on the `period` field instead of falling back to a default period

### Requirement: Vietnam period boundaries and snapshot metadata

Every response SHALL specify `period`, `startDate`, `endDate`, `timeZone` as `UTC+07:00`, and `asOf` in UTC at read time. Report dates are Vietnam calendar dates. Period queries SHALL use `[fromUtc, endExclusiveUtc)`, where the start is 00:00 UTC+07 of `startDate` and the end is the earlier of 00:00 UTC+07 of the day after `endDate` and `asOf`. All parts of the response SHALL read from the same database snapshot. Active products and low-stock balances SHALL reflect snapshot read time.

#### Scenario: Period boundaries at UTC rollover

- **WHEN** a request is read near UTC or UTC+07 midnight
- **THEN** start/end dates and events are evaluated in UTC+07, with an inclusive start bound and an exclusive end bound

#### Scenario: Partial current day

- **WHEN** a period ends on the current Vietnam calendar date and is read before midnight
- **THEN** period totals include only data before `asOf`, and the daily series has one point per day through `endDate`, including zero-revenue days

#### Scenario: Coherent snapshot under concurrent writes

- **WHEN** orders, products, or stock change while the dashboard is being read
- **THEN** every field in the response reflects one coherent database snapshot and `asOf` is the read timestamp of that snapshot

#### Scenario: Existing relative periods still include the current day

- **WHEN** a caller selects `today`, `last7Days`, or `thisMonth`
- **THEN** `endDate` is the Vietnam date at `asOf`, and `startDate` is the same day, six days earlier, or the first day of the current month respectively

#### Scenario: Historical range keeps its report dates

- **WHEN** a caller selects a custom period ending before the current Vietnam calendar date
- **THEN** the response has `period=custom`, start and end dates matching `from` and `to`, and `asOf` remains the current read timestamp. Revenue and order queries include `to` up to the next Vietnam midnight and exclude out-of-range orders per each KPI's timestamp rule

## ADDED Requirements

### Requirement: Validated custom dashboard period

With `period=custom`, the API SHALL require exactly one value for each of `from` and `to` on the Vietnam calendar, formatted as `yyyy-MM-dd`. The inclusive range SHALL span 1 to 31 days and end no later than the Vietnam calendar date at `asOf`. Dates MUST convert to valid UTC boundaries. Other periods SHALL accept only the `period` parameter.

#### Scenario: One day and maximum length are allowed

- **WHEN** an authorized caller selects the same date for `from` and `to`, or enters a 31-day span with valid bounds and an end date not in the future
- **THEN** the API returns HTTP 200 and one daily revenue point for each day in the period

#### Scenario: Missing, malformed or unsupported dates

- **WHEN** an authorized caller sends missing dates, empty dates, repeated date parameters, invalid format, nonexistent dates or dates that cannot convert to UTC boundaries, `from > to`, a range over 31 days, or an end date in the future in UTC+07
- **THEN** the API returns HTTP 400 with `ValidationProblemDetails` keyed to the relevant field(s) without running aggregate queries

#### Scenario: Date parameters require the custom period

- **WHEN** an authorized caller sends `from` or `to` without selecting exactly `period=custom`
- **THEN** the API returns HTTP 400 identifying the parameter-combination error
