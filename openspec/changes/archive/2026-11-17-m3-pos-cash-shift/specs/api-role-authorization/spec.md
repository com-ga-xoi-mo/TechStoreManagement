## ADDED Requirements

### Requirement: POS endpoints authorization
The permission table SHALL list: `POST /api/pos/shifts`, `POST /api/pos/checkout` for Admin and SalesStaff; `POST /api/pos/shifts/{id}/close` for Admin and SalesStaff (SalesStaff own shift only) and Manager; `GET /api/pos/shifts/current` for Admin and SalesStaff; `GET /api/pos/shifts`, `GET /api/pos/shifts/{id}` for Admin, Manager and SalesStaff (own only). WarehouseStaff SHALL receive 403 and a missing or invalid token 401 on every `/api/pos` endpoint. The actor SHALL come from the token.

#### Scenario: Warehouse blocked
- **WHEN** WarehouseStaff calls any `/api/pos` endpoint
- **THEN** the response is 403

#### Scenario: Manager cannot sell
- **WHEN** a Manager posts `/api/pos/checkout`
- **THEN** the response is 403

#### Scenario: Anonymous
- **WHEN** a request without a token calls `/api/pos/shifts/current`
- **THEN** the response is 401
