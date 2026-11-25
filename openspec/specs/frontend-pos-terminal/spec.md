# frontend-pos-terminal Specification

## Purpose

Màn hình bán hàng tại quầy trên client desktop: mở và đóng ca, quét IMEI/SKU, giỏ hàng, nhận tiền mặt và tiền thừa, với trạng thái tải/lỗi/rỗng và theme Sáng/Tối.
## Requirements
### Requirement: Shift gate
The POS page SHALL load the current shift. Without an open shift it SHALL show an empty state with an "open shift" action that asks for the opening balance and calls the API; the sale controls SHALL be disabled until a shift is open.

#### Scenario: No open shift
- **WHEN** a SalesStaff opens the POS page with no open shift
- **THEN** the sale controls are disabled and the open-shift action is shown

#### Scenario: Open a shift
- **WHEN** the user submits a valid opening balance
- **THEN** the page shows the open shift and enables the sale controls

### Requirement: Scan and cart
The POS page SHALL accept a barcode, SKU or Serial/IMEI scanned or typed, add a line, let the user remove a line and change quantity of non-serial lines. SalesStaff SHALL be able to choose or create a customer or sell to a walk-in guest. Scanning SHALL require an exact product, variant or device identifier. An IMEI already in the cart or already sold SHALL be refused with a message. Totals shown SHALL come from server values; the client SHALL NOT send prices.

#### Scenario: Duplicate IMEI
- **WHEN** the same IMEI is scanned twice
- **THEN** the second scan is refused with a message and the cart is unchanged

#### Scenario: Quantity change
- **WHEN** the user raises the quantity of a non-serial line
- **THEN** the line total and order total update

### Requirement: Cash payment and change
The page SHALL take the amount received, show the change from the server-calculated total, keep Pay disabled while the amount is lower than the total or a request is running, and send one `checkoutKey` per sale that is kept across retries. After success it SHALL show the order code and change and clear the cart.

#### Scenario: Pay disabled while processing
- **WHEN** the user presses Pay
- **THEN** Pay is disabled until the response, and a retry after an error reuses the same key

#### Scenario: Success
- **WHEN** the API returns 201 or 200
- **THEN** the order code and change are shown and the cart is empty

### Requirement: Close shift panel
The close-shift panel (shown in place of the sale area, not a modal) SHALL show opening balance, cash sales and expected cash, take the counted cash and a note, show the discrepancy, and require the note when it is not 0.

#### Scenario: Discrepancy needs a note
- **WHEN** counted cash differs from expected and the note is empty
- **THEN** Confirm is disabled and a field error is shown

### Requirement: Page states and navigation
The POS page SHALL show loading, error with retry and empty states through `PageViewModel`, be reached from the "Bán hàng tại quầy" button of the Orders page (role-based hiding of that button belongs to the shell role work; the server returns 403 for other roles and the page shows its error state), and follow Light/Dark theme tokens with colors set directly on elements.

#### Scenario: Warehouse route
- **WHEN** WarehouseStaff reaches the POS page
- **THEN** the shift request is refused with 403 and the page shows its error state with a retry action, not a crash

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

