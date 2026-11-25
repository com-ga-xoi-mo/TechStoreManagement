# api-role-authorization Specification

## Purpose

Đảm bảo mọi API nghiệp vụ TechStore xác thực người dùng và kiểm tra quyền theo hành động tại server, với danh sách ngoại lệ public và lỗi truy cập thống nhất.

## Requirements

### Requirement: Authentication is required by default
Every business API SHALL require a valid authenticated user even when the endpoint has no explicit authorization attribute. Public exceptions SHALL be limited to `POST /api/auth/login`, `POST /api/auth/refresh`, `GET /api/health` and `GET /api/settings/store`. Health SHALL expose only service health; public store settings SHALL expose only the existing non-secret store DTO fields (id, storeName, address, phone, email, taxId, description, isActive and updatedAt). Static product image files remain public resources; development Swagger remains development-only and SHALL NOT grant anonymous access to its business endpoints.

#### Scenario: Endpoint without an explicit policy
- **WHEN** a caller sends a request without a token to a business endpoint without its own authorization declaration
- **THEN** the server returns 401 without invoking business logic

#### Scenario: Public endpoints remain reachable
- **WHEN** login, refresh, health or public store settings is requested without an access token
- **THEN** missing access authentication does not prevent the endpoint from evaluating its own input; invalid credentials or refresh tokens can still return 401

### Requirement: Action permissions cover every business endpoint
The server SHALL enforce the following authorization matrix. `A`, `M`, `S`, `W` mean Admin, Manager, SalesStaff, WarehouseStaff. Roles absent from a row SHALL receive 403. All listed reads return existing DTO fields unless a narrower scope is stated; account secrets and order cost snapshots SHALL never be returned. No user-management DELETE endpoint SHALL exist.

| API/action | Allowed roles | Read/write scope |
| --- | --- | --- |
| GET `/api/products`, `/api/products/{id}`, `/api/products/spec-keys`, `/api/products/brands`, `/api/products/spec-definitions` | A M S W | Current catalog DTOs, including current product detail costPrice; field redaction is Sprint 2 |
| POST `/api/products`, PUT `/api/products/{id}`, PATCH `/api/products/{id}/status`, DELETE `/api/products/{id}` | A M | Existing product and price management |
| POST `/api/products/{productId}/images`, DELETE `/api/products/{productId}/images/{imageId}`, PUT `/api/products/{productId}/images/layout` | A M | Existing image management |
| GET `/api/categories/tree` | A M S W | Existing category tree |
| POST `/api/categories`, PUT/DELETE `/api/categories/{id}` | A | Existing category management |
| GET `/api/inventory/stocks`, `/api/inventory/stocks/{stockId}/detail`, `/api/inventory/serials` | A M S W | Existing stock, serial and movement DTOs; cost-field policy reviewed in Sprint 2 |
| POST `/api/inventory/stocks/initialize`, POST `/api/inventory/stocks/{stockId}/adjust`, DELETE `/api/inventory/stocks/{stockId}`, POST `/api/inventory/serials/batch-import` | A W | Existing inventory writes, never catalog price writes |
| GET `/api/orders`, `/api/orders/{id}` | A M S | A/M see all; S sees only orders whose cashier is that user |
| POST `/api/orders`, PATCH `/api/orders/{id}/status` | A S | A writes all; S transitions only own orders; creator comes from token |
| GET `/api/customers`, `/api/customers/{id}` | A M S | S reads only active customers; an inactive customer is 404 for S |
| POST `/api/customers` | A M S | Create a customer at the counter; points, tier and activity stay server-controlled |
| GET `/api/customers/{id}/loyalty-points`, `/api/customers/{id}/orders`, `/api/customers/{id}/purchased-items` | A M | Existing customer history DTOs |
| PUT `/api/customers/{id}`, PATCH `/api/customers/{id}/deactivate`, `/api/customers/{id}/activate` | A M | Existing customer management |
| GET `/api/analytics/dashboard` | A M | Read-only store-wide dashboard |
| GET `/api/analytics/financial-report` | A M | Read-only store-wide financial aggregates |
| PUT `/api/settings/store` | A M | Existing store configuration fields |
| GET `/api/auth/me`, POST `/api/auth/logout` | A M S W | Own identity and account-wide logout |
| GET `/api/users`, `/api/users/{id}`, POST `/api/users`, PUT `/api/users/{id}`, PATCH `/api/users/{id}/status`, PUT `/api/users/{id}/role`, POST `/api/users/{id}/reset-password` | A | Account metadata and administrative mutations; never hashes/tokens |

#### Scenario: Warehouse cannot change selling prices or create users
- **WHEN** WarehouseStaff calls a product write endpoint or any account administration endpoint
- **THEN** the response is 403 and no changes are persisted

#### Scenario: Sales cannot read store-wide revenue
- **WHEN** SalesStaff calls analytics or lists orders
- **THEN** analytics returns 403 and the order list, its totalCount and totalPages include only that user's own orders

#### Scenario: Manager retains product editing rights
- **WHEN** Manager sends a valid product update
- **THEN** the product update is allowed

#### Scenario: Manager can read store-wide Dashboard

- **WHEN** Manager with a current valid JWT requests the Dashboard with valid inputs
- **THEN** the API returns 200 and provides the same store-wide read scope as for Admin

#### Scenario: Warehouse cannot read store-wide Dashboard

- **WHEN** WarehouseStaff with a current valid JWT requests the Dashboard
- **THEN** the API returns 403 without analytics data

#### Scenario: Sales serves customers without managing them
- **WHEN** SalesStaff searches, opens and creates a customer, then calls customer update, deactivation or history endpoints
- **THEN** search, profile and creation succeed for active customers, and every update, activity and history call returns 403

#### Scenario: Warehouse cannot read customers
- **WHEN** WarehouseStaff calls any customer endpoint
- **THEN** the response is 403 and no customer data is returned

#### Scenario: Financial report enforces store-wide read access
- **WHEN** callers request the financial report with a valid period
- **THEN** Admin and Manager receive 200 with the same store-wide scope, SalesStaff and WarehouseStaff receive 403, and a missing or invalid JWT receives 401

### Requirement: Identity and authorization failures have safe semantics
A missing, expired, forged or revoked token, missing/malformed/empty user identifier, unknown/inactive user, missing token version, obsolete token version or inconsistent role SHALL result in 401. A currently valid identity without the required action role SHALL receive 403. Failure bodies SHALL use safe ProblemDetails with the actual HTTP status and SHALL NOT contain stack traces, password/hash/token values, connection strings or signing keys. Challenges SHALL retain the Bearer authentication header.

#### Scenario: Invalid identity on Orders
- **WHEN** an otherwise signed JWT has no subject, an invalid GUID, the empty GUID or an unknown user ID and calls Orders
- **THEN** the response is 401 and no order service mutation occurs

#### Scenario: Valid but disallowed role
- **WHEN** an active WarehouseStaff with a current valid token requests Orders
- **THEN** the response is 403 without exposing order data

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

### Requirement: Warranty case authorization
The API SHALL restrict warranty case intake, lookup, repair handback and replacement to Admin and Manager.

#### Scenario: Sales and Warehouse cannot authorize warranty operations
- **WHEN** SalesStaff or WarehouseStaff calls a warranty case endpoint
- **THEN** the API returns 403.
