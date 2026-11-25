# pos-shift-management Specification

## Purpose

Cho SalesStaff mở và đóng ca thu ngân, mỗi người một ca đang mở, đối soát tiền đầu ca, tiền dự kiến và tiền thực đếm qua API, và cho Manager/Admin xem và đóng cưỡng bức mọi ca.
## Requirements
### Requirement: Open a shift
`POST /api/pos/shifts` SHALL open a shift for the acting user with `openingBalance` (≥ 0, at most 2 decimals) and return 201 with the shift (`id`, `cashierUserId`, `openingBalance`, `status` = `Open`, `openedAt`, totals 0). A user SHALL have at most one `Open` shift; the rule SHALL also be enforced by the partial unique index `uq_pos_sessions_cashier_open`. `SalesStaff`, `Admin` and `Manager` SHALL open their own shifts.

#### Scenario: Open the first shift
- **WHEN** a SalesStaff with no open shift posts `openingBalance` = 2,000,000
- **THEN** the response is 201 with `status` = `Open`, `openingBalance` = 2,000,000 and `cashSalesTotal` = 0

#### Scenario: Second open shift
- **WHEN** a user who already has an `Open` shift posts another open request, including two simultaneous requests
- **THEN** exactly one shift exists and the other request returns 409

#### Scenario: Negative opening balance
- **WHEN** `openingBalance` is negative or has more than 2 decimals
- **THEN** the response is 400 with an error under `openingBalance` and nothing is stored

#### Scenario: Manager opens a refund drawer
- **WHEN** a Manager opens their own drawer and reads the current shift
- **THEN** both requests succeed and the Manager remains unable to check out a sale.

### Requirement: Read shifts
`GET /api/pos/shifts/current` SHALL return the acting user's `Open` shift or 404. `GET /api/pos/shifts` SHALL return a page of shifts newest first, filterable by `status` and `cashierUserId`; SalesStaff SHALL see only their own shifts, Manager and Admin SHALL see all. `GET /api/pos/shifts/{id}` of another user's shift SHALL return 404 to SalesStaff.

#### Scenario: Sales sees only own shifts
- **WHEN** SalesStaff A lists shifts while shifts of A and B exist
- **THEN** only shifts of A are returned and `totalCount` counts only them

#### Scenario: Manager sees all shifts
- **WHEN** a Manager lists shifts
- **THEN** shifts of every cashier are returned

### Requirement: Close a shift with reconciliation
`POST /api/pos/shifts/{id}/close` SHALL close an `Open` shift with the counted `closingBalance` (≥ 0) and an optional `note` (at most 500 characters). The server SHALL lock the shift, compute `expectedClosingBalance` = `openingBalance` + `cashSalesTotal` − `cashRefundTotal`, store `closingBalance`, `closedAt`, `closedByUserId` and the note, and return `discrepancy` = `closingBalance` − `expectedClosingBalance`. A non-empty `note` SHALL be required when `discrepancy` is not 0. The owner SHALL close their own shift; Manager and Admin MAY close any shift. A closed shift SHALL NOT change again.

#### Scenario: Close with exact cash
- **WHEN** opening 2,000,000, cash sales 5,000,000 and counted cash 7,000,000
- **THEN** the shift is `Closed`, expected = 7,000,000, discrepancy = 0

#### Scenario: Shortage without reason
- **WHEN** counted cash differs from expected and no `note` is sent
- **THEN** the response is 400 with an error under `note` and the shift stays `Open`

#### Scenario: Close another user's shift
- **WHEN** SalesStaff B closes the shift of A
- **THEN** the response is 404; and a Manager closing it succeeds

#### Scenario: Close twice
- **WHEN** a closed shift is closed again
- **THEN** the response is 409 and no value changes

#### Scenario: Cash refund competes with close
- **WHEN** the operator pays a refund while the drawer is closing
- **THEN** either its amount is deducted once in the closed balance, or the refund fails with 409 and no payout is recorded.

### Requirement: Closing races with selling
Closing a shift and checkout into the same shift SHALL be serialized by a row lock on the shift. A checkout that loses the race SHALL fail with 409 and change nothing; a sale committed before the close SHALL be included in `cashSalesTotal` used for `expectedClosingBalance`.

#### Scenario: Sale during close
- **WHEN** a checkout and a close of the same shift run concurrently
- **THEN** either the sale is counted in the expected cash and the shift closes, or the checkout returns 409 and no order exists

### Requirement: Close is blocked while a QR is pending
`POST /api/pos/shifts/{id}/close` SHALL, after locking the shift, refuse with 409 and the list of pending QR ids and order codes when any QR of that shift is `Pending`. After the QR is confirmed, cancelled or expired the close SHALL be allowed. The check and the close SHALL be serialized by the shift lock so a QR created, confirmed or expired during the close either appears in the check or is rejected.

#### Scenario: Pending QR blocks the close
- **WHEN** the shift has one `Pending` QR and the cashier closes it
- **THEN** the response is 409 naming the QR and the shift stays `Open`

#### Scenario: Close after the QR ends
- **WHEN** the same QR is cancelled and the cashier closes the shift
- **THEN** the shift closes normally

#### Scenario: QR created while closing
- **WHEN** a QR creation and a close of the same shift run at the same time
- **THEN** either the close succeeds and the creation returns 409, or the creation wins and the close returns 409

### Requirement: Shift DTO shows QR money apart from cash
The shift DTO SHALL expose `vietQrSalesTotal` next to `cashSalesTotal`. `expectedClosingBalance` SHALL stay opening balance + cash sales − cash refunds and SHALL NOT include `vietQrSalesTotal`.

#### Scenario: Shift totals
- **WHEN** a QR of 3,000,000 was confirmed in the shift
- **THEN** `vietQrSalesTotal` is 3,000,000 and `expectedClosingBalance` is unchanged

