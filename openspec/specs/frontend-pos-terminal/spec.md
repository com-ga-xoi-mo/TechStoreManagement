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
The POS page SHALL accept a barcode, SKU or Serial/IMEI scanned or typed, add a line, let the user remove a line and change quantity of non-serial lines. Sales are walk-in until the Customers API is opened to SalesStaff (Vy); the request already accepts `customerId`. An IMEI already in the cart or already sold SHALL be refused with a message. Totals shown SHALL come from server values; the client SHALL NOT send prices.

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
