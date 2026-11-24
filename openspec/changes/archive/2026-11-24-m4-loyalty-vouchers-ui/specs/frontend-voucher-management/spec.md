## ADDED Requirements

### Requirement: Voucher list
The voucher page SHALL list vouchers from `GET /api/vouchers` with search by code or description, a status filter (all, active, inactive) and pagination. Each row SHALL show the code, description, discount (percentage with its cap, or fixed amount), minimum order, uses as used / held / limit, expiry and a status of Inactive, Expired, Exhausted or Active.

#### Scenario: Open the list
- **WHEN** an Admin opens the voucher page
- **THEN** the first page loads, the count is shown, and each row shows its discount, uses and status

#### Scenario: Search by code
- **WHEN** the user types `sale` in the search box
- **THEN** one request is sent after the typing pause with `q=sale` and the list shows the answer

#### Scenario: No match
- **WHEN** the search returns no voucher
- **THEN** an empty state is shown instead of an empty table

### Requirement: Voucher status display
A voucher SHALL be shown as Inactive when it is switched off, otherwise Expired when its expiry is not in the future, otherwise Exhausted when `usedCount + reservedCount` has reached the usage limit, otherwise Active. Expiry SHALL be read against an injected clock.

#### Scenario: Last use held
- **WHEN** a voucher has limit 5, 4 used and 1 held
- **THEN** the row shows Exhausted

#### Scenario: Switched off and expired
- **WHEN** a voucher is switched off and its expiry has passed
- **THEN** the row shows Inactive

### Requirement: Create and edit a voucher
The page SHALL open a dialog to create a voucher (code, description, type, value, minimum order, maximum discount, usage limit, expiry) and to edit one. In edit, the code and the discount type SHALL be read-only. A saved voucher SHALL appear in the list without a manual reload. Server errors under a field SHALL show beside that field, a 409 SHALL show that the code is already used, and the dialog SHALL stay open.

#### Scenario: Create succeeds
- **WHEN** a Manager saves a valid 10% voucher
- **THEN** the dialog closes and the list shows the code in upper case

#### Scenario: Duplicate code
- **WHEN** the server answers 409 to a create
- **THEN** the dialog stays open and shows that the code already exists

#### Scenario: Value out of range
- **WHEN** the server answers 400 with an error under `discountValue`
- **THEN** that message is shown beside the value field

#### Scenario: Edit keeps the code
- **WHEN** a voucher is opened for edit
- **THEN** the code and discount type cannot be changed

### Requirement: Switch a voucher on or off
Each row SHALL offer Activate or Deactivate according to its state. The action SHALL call the matching endpoint, update the row and show a safe message on failure. A voucher switched off SHALL remain in the list.

#### Scenario: Deactivate
- **WHEN** the user chooses Deactivate on an active voucher
- **THEN** the row shows Inactive after the answer and the offered action becomes Activate

#### Scenario: Not allowed
- **WHEN** the server answers 403
- **THEN** a message says the user has no permission and the row is unchanged

### Requirement: Voucher page follows the role
The voucher page SHALL open only for Admin and Manager. Other roles SHALL NOT see the entry button, and a direct route to the page SHALL be refused before any request. The server still enforces 403.

#### Scenario: Sales has no entry
- **WHEN** a SalesStaff session opens Customers
- **THEN** no Voucher button is shown

#### Scenario: Direct route as Sales
- **WHEN** a SalesStaff session navigates to the voucher route
- **THEN** navigation is refused and no voucher request is sent

### Requirement: Safe loading and error states
The page SHALL show loading, empty, content and error states without leaving stale rows after a failed search, SHALL cancel its requests when left, and SHALL show a retry action on error. Messages SHALL be safe text for 401, 403, timeout, network and invalid responses.

#### Scenario: Server down
- **WHEN** the list request fails with a network error
- **THEN** an error message and a retry action are shown

#### Scenario: Leave while loading
- **WHEN** the user leaves the page during a request
- **THEN** the request is cancelled and no later answer changes the page
