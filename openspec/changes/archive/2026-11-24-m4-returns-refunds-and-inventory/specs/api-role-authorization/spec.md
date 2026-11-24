## MODIFIED Requirements

### Requirement: POS endpoints authorization
Admin, Manager and SalesStaff SHALL open and read their own current shift. Admin and SalesStaff SHALL quote and check out sales and cancel or expire a pending QR hold. SalesStaff SHALL access only their own QR holds and shifts. Admin/Manager SHALL list and close any shift. Missing or invalid authentication SHALL yield 401. WarehouseStaff SHALL receive 403 on POS routes. The actor SHALL come from the token.

#### Scenario: Warehouse blocked
- **WHEN** WarehouseStaff calls any `/api/pos` endpoint
- **THEN** the response is 403

#### Scenario: Manager cannot sell
- **WHEN** a Manager posts `/api/pos/checkout`
- **THEN** the response is 403

#### Scenario: Anonymous
- **WHEN** a request without a token calls `/api/pos/shifts/current`
- **THEN** the response is 401

#### Scenario: Sales ends a pending QR hold
- **WHEN** the cashier cancels an existing unpaid QR hold before expiry or marks it expired when due
- **THEN** the order is cancelled and its reservation is released once. A different cashier receives 404. An unresolved payment or voucher hold returns 409 and keeps the reservation.

## ADDED Requirements

### Requirement: Warranty case authorization
The API SHALL restrict warranty case intake, lookup, repair handback and replacement to Admin and Manager.

#### Scenario: Sales and Warehouse cannot authorize warranty operations
- **WHEN** SalesStaff or WarehouseStaff calls a warranty case endpoint
- **THEN** the API returns 403.
