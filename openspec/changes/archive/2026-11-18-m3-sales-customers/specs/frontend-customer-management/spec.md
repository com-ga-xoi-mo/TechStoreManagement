## ADDED Requirements

### Requirement: Customer screens follow the role
The Customers destination SHALL open for Admin, Manager and SalesStaff. Admin and Manager keep every customer action. SalesStaff SHALL see only search, tier filter, profile detail and "Thêm khách hàng"; the activity filter, edit, activity actions and history tabs SHALL be hidden and no history request SHALL be sent. The server still enforces 403.

#### Scenario: Sales opens the customer list
- **WHEN** a SalesStaff session opens Customers
- **THEN** the list shows active customers with search, tier filter and an add button, and no row shows an edit action

#### Scenario: Sales opens a customer profile
- **WHEN** a SalesStaff session opens a customer's detail
- **THEN** the dialog shows contact details, tier and points, shows no history tabs or activity actions, and sends no history request

#### Scenario: Manager opens the customer list
- **WHEN** a Manager session opens Customers
- **THEN** the same actions as Admin are available

### Requirement: Shared customer picker
The client SHALL provide one customer picker for sales screens: search active customers by name or phone showing name, phone and tier; pick one or clear back to a walk-in guest; create a customer that is then selected. "Khách mới" sits at the top right beside the heading with search beneath; selected-customer details show below search only while one is selected. Loading, no-result and error states SHALL show, and a stale response SHALL NOT replace a newer one.

#### Scenario: Pick an existing customer
- **WHEN** the user types `0901` and picks "Nguyễn Văn A" from the results
- **THEN** the picker shows the selected name and phone and exposes that customer's ID to the screen

#### Scenario: Walk-in guest
- **WHEN** the user clears the selection
- **THEN** the picker hides the selected customer information, shows no "Khách lẻ" or "Không tích điểm" label, and exposes no customer ID

#### Scenario: Create and select a new customer
- **WHEN** the user creates a customer with a new phone from the picker
- **THEN** the new customer is selected without another search

#### Scenario: Phone already used
- **WHEN** creation returns 409
- **THEN** the picker says the phone already belongs to a customer and shows the search results for that phone

## MODIFIED Requirements

### Requirement: Manage customer activity safely
The customer UI SHALL let an Admin or a Manager deactivate an active customer only after explicit confirmation, reactivate an inactive customer, and communicate success or failure without implying permanent deletion. It SHALL expose no permanent-delete action, and SHALL NOT show activity actions to SalesStaff.

#### Scenario: Confirm deactivation
- **WHEN** an Admin confirms the “Vô hiệu hóa” action
- **THEN** the client calls the API deactivation operation, marks the profile inactive, and refreshes the list while retaining customer and history data

#### Scenario: Cancel deactivation
- **WHEN** an Admin dismisses or cancels the confirmation
- **THEN** no deactivation request is sent and the profile remains unchanged

#### Scenario: Reactivate an inactive customer
- **WHEN** an Admin chooses `Kích hoạt lại` for an inactive customer
- **THEN** the client calls the activation API, marks the profile active, and refreshes the list without changing profile or history data

#### Scenario: Activity-specific actions
- **WHEN** an Admin views an active or inactive customer
- **THEN** an active customer offers deactivation, an inactive customer offers reactivation, and neither state offers permanent deletion
