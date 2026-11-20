# Spec Delta

## MODIFIED Requirements

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
| GET `/api/customers`, `/api/customers/{id}`, `/api/customers/{id}/loyalty-points`, `/api/customers/{id}/orders`, `/api/customers/{id}/purchased-items` | A | Existing customer and history DTOs; Sales access is Sprint 2 |
| POST `/api/customers`, PUT `/api/customers/{id}`, PATCH `/api/customers/{id}/deactivate`, `/api/customers/{id}/activate` | A | Existing customer management |
| GET `/api/analytics/dashboard` | A M | Read-only store-wide dashboard |
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
