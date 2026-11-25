## MODIFIED Requirements

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
