# Spec Delta

## ADDED Requirements

### Requirement: Role based menu and landing page
Shell SHALL show only the destinations the signed-in role may use, following the client role table that mirrors `docs/API_PERMISSIONS.md`. Each new session SHALL open the role's landing page, the first allowed destination in the order Overview, Orders, Categories and Inventory, Products. Settings SHALL be shown for every role. The previous session's route SHALL NOT be reused.

#### Scenario: Warehouse login
- **WHEN** `warehouse1` signs in
- **THEN** Shell opens Categories and Inventory and the menu shows Products, Categories and Inventory and Settings only

#### Scenario: Sales login after Admin
- **WHEN** Admin was on Customers, logs out, then `cashier1` signs in
- **THEN** Shell opens Orders, not Customers, and no Customers or Overview item is shown while the server keeps them closed to Sales

#### Scenario: Admin login
- **WHEN** `admin` signs in
- **THEN** Shell opens Overview and shows all six destinations

### Requirement: Restricted routes are blocked before loading
Navigating to a destination or child route that the role may not open SHALL be refused before the page is created or any API call is made; Shell SHALL keep the current page and show a safe error.

#### Scenario: Sales opens product editor directly
- **WHEN** a SalesStaff session navigates to `ProductCreate` or `ProductEdit`
- **THEN** navigation returns unavailable, no editor page is created and no product write request is sent

#### Scenario: Warehouse opens Orders directly
- **WHEN** a WarehouseStaff session navigates to the Orders destination
- **THEN** navigation is refused and the current page stays

### Requirement: Action buttons follow the role
Write actions SHALL be visible only to roles the server allows: category add/edit/delete for Admin; stock import/adjust/delete and serial import for Admin and WarehouseStaff; order create and status change for Admin and SalesStaff; product create/edit/status/delete for Admin and Manager.

#### Scenario: Manager views inventory
- **WHEN** a Manager opens Categories and Inventory
- **THEN** stock and category data are shown without add, import, adjust or delete buttons
