## ADDED Requirements

### Requirement: Voucher route under Customers
Shell SHALL host a voucher list route under the Customers destination without adding a menu entry. The route SHALL be openable only by Admin and Manager and SHALL be refused for other roles before the page loads. Leaving it SHALL return to the Customers list.

#### Scenario: Route keeps the menu
- **WHEN** a Manager navigates to the voucher route
- **THEN** the menu still shows six entries and Customers is selected

#### Scenario: Warehouse direct route
- **WHEN** a WarehouseStaff session navigates to the voucher route
- **THEN** navigation is refused and the previous page stays

#### Scenario: Back to customers
- **WHEN** the user presses "← Khách hàng" on the voucher page
- **THEN** the Customers list opens
