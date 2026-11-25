## ADDED Requirements

### Requirement: VietQR endpoints authorization
The permission table SHALL list: `POST /api/pos/vietqr` for Admin and SalesStaff; `GET /api/pos/vietqr` and `GET /api/pos/vietqr/{id}` for Admin, Manager and SalesStaff (SalesStaff own QR only); `POST /api/pos/vietqr/{id}/cancel` and `/expire` for Admin and SalesStaff (SalesStaff own QR only); `POST /api/pos/vietqr/{id}/confirm` for Admin and Manager; `GET /api/payments/vietqr/events` and `POST /api/payments/vietqr/events/{id}/resolve` for Admin and Manager. `POST /api/payments/vietqr/webhook` SHALL be the only anonymous endpoint added and SHALL require a valid HMAC signature. WarehouseStaff SHALL receive 403 on every other route and a missing or invalid token 401.

#### Scenario: Warehouse blocked
- **WHEN** WarehouseStaff calls any `/api/pos/vietqr` route
- **THEN** the response is 403

#### Scenario: Manager cannot create a QR
- **WHEN** a Manager posts `/api/pos/vietqr`
- **THEN** the response is 403

#### Scenario: Webhook without signature
- **WHEN** a request without `X-Signature` calls the webhook
- **THEN** the response is 401 and the fallback policy still protects every other endpoint
