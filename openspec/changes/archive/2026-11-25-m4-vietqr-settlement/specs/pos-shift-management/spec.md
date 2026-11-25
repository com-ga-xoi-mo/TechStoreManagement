## ADDED Requirements

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
