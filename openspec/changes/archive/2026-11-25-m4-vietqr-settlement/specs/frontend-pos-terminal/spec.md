## ADDED Requirements

### Requirement: Choose the payment method
The POS page SHALL let SalesStaff and Admin choose Tiền mặt or VietQR for the current cart. Cash keeps the existing cash flow. VietQR replaces the cash box with a "Tạo mã QR" action; the cart, discount and customer SHALL be locked while a QR is pending.

#### Scenario: Switch to QR
- **WHEN** the cashier selects VietQR with a non-empty cart
- **THEN** the cash received box is hidden and "Tạo mã QR" is shown enabled

### Requirement: Show and follow the QR
After creating a QR the page SHALL show the QR image, the amount, the transfer content, the receiving bank and account, a countdown to `expiresAt`, and the status (chờ, thành công, thất bại, hết hạn), refreshed by polling the server every 3 seconds while the page is open and not after it is closed. A "Hủy" action SHALL cancel the QR. On `Confirmed` the page SHALL show the receipt and clear the cart; on `Expired` or `Failed` it SHALL unlock the cart and say why. Leaving the page and coming back SHALL resume the pending QR from the server.

#### Scenario: Payment arrives
- **WHEN** the QR is confirmed on the server while the page polls
- **THEN** within one polling interval the receipt shows and the cart is empty

#### Scenario: Countdown reaches zero
- **WHEN** the deadline passes
- **THEN** the page shows "Hết hạn", unlocks the cart and offers to create a new QR

#### Scenario: Network error while polling
- **WHEN** a poll fails
- **THEN** the page keeps the last known state, shows a short notice and keeps polling

#### Scenario: Return to the page
- **WHEN** the cashier leaves for Settings and returns with a pending QR
- **THEN** the same QR is shown with the remaining time

### Requirement: Close shift shows pending QR
When the server refuses to close the shift because of a pending QR, the close panel SHALL name the QR and keep the shift open.

#### Scenario: Close refused
- **WHEN** the close returns 409 for a pending QR
- **THEN** the panel shows which order is waiting and the shift stays open

### Requirement: Confirm and reconcile screen
Admin and Manager SHALL have a screen listing `Pending` QRs with order code, amount, cashier and time left, and a confirm action asking for the bank reference and the received amount, and a second list of bank events needing reconciliation with a resolve action requiring a note. SalesStaff and WarehouseStaff SHALL NOT see or open it.

#### Scenario: Manager confirms from the list
- **WHEN** a Manager enters a matching reference and amount
- **THEN** the QR leaves the pending list and the sale shows as completed

#### Scenario: Role hidden
- **WHEN** a SalesStaff is signed in
- **THEN** the entry to the screen is not shown and opening its route directly is denied
