## ADDED Requirements

### Requirement: Voucher and loyalty summary roles
Every `/api/vouchers` endpoint SHALL require Admin or Manager; a missing token returns 401 and any other role returns 403 with nothing read or changed. `GET /api/customers/{id}/loyalty-summary` SHALL allow Admin, Manager and SalesStaff and return 403 to WarehouseStaff.

#### Scenario: Sales lists vouchers
- **WHEN** a SalesStaff calls `GET /api/vouchers`
- **THEN** the response is 403

#### Scenario: Warehouse reads a loyalty summary
- **WHEN** a WarehouseStaff calls `GET /api/customers/{id}/loyalty-summary`
- **THEN** the response is 403

#### Scenario: Sales reads a loyalty summary
- **WHEN** a SalesStaff calls the summary of an active customer
- **THEN** the response is 200
