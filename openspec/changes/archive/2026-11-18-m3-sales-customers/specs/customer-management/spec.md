## MODIFIED Requirements

### Requirement: Customer API authorization
Every `/api/customers` endpoint SHALL require an authenticated, active account; a missing or invalid token SHALL return 401. Admin, Manager and SalesStaff MAY search, read and create customers. Only Admin and Manager MAY update, deactivate, reactivate, or read the order, purchased-item and loyalty history. Any other authenticated role SHALL receive 403 with no customer data changed or exposed. No fixed user or role fallback SHALL grant access.

#### Scenario: Admin can list customers
- **WHEN** an authenticated Admin calls `GET /api/customers`
- **THEN** the endpoint returns 200 with a paginated customer list

#### Scenario: Sales can search and create customers
- **WHEN** an authenticated SalesStaff calls `GET /api/customers?q=090` and then `POST /api/customers` with a valid, unused phone
- **THEN** the search returns 200 and the creation returns 201 with a Standard, active customer

#### Scenario: Sales cannot edit, deactivate or read history
- **WHEN** an authenticated SalesStaff calls `PUT /api/customers/{id}`, `PATCH /api/customers/{id}/deactivate`, `PATCH /api/customers/{id}/activate`, or any customer history endpoint
- **THEN** each response is 403 and the customer is unchanged

#### Scenario: Manager manages customers
- **WHEN** an authenticated Manager updates, deactivates, reactivates, or reads the history of a customer
- **THEN** the requests are allowed like for Admin

#### Scenario: Missing or insufficient role
- **WHEN** an unauthenticated caller or an authenticated WarehouseStaff calls a customer endpoint
- **THEN** the response is 401 or 403 respectively and no customer data is changed or exposed

### Requirement: Paginated customer search
`GET /api/customers` SHALL return paginated items and metadata, matching name/phone by case-insensitive literal substring and combining tier/activity with AND; omitting `isActive` includes both states. Defaults: page 1, size 20, capped at 100, newest first then ID. Each item SHALL include all-order count, completed-order spending and latest completed time (zero and null without completed orders). For SalesStaff the server SHALL return only active customers whatever `isActive` is sent.

#### Scenario: Search by Vietnamese name or phone
- **WHEN** an Admin searches with a name fragment or a phone fragment
- **THEN** only matching customers are returned, with a correct filtered `totalCount`

#### Scenario: Tier and activity filters
- **WHEN** an Admin requests `tier=Silver&isActive=false`
- **THEN** every returned customer has Silver tier and is inactive

#### Scenario: Sales search is limited to active customers
- **WHEN** a SalesStaff requests `isActive=false` or omits `isActive`
- **THEN** the response contains only active customers and `totalCount` counts only active matches

#### Scenario: Invalid tier
- **WHEN** an Admin requests `tier=Unknown`
- **THEN** the response is 400 with an error under `tier`

#### Scenario: Page beyond the end
- **WHEN** the requested page exceeds the number of filtered pages
- **THEN** the response is 200 with empty `items` and accurate totals

#### Scenario: Customer order aggregates
- **WHEN** an Admin lists a customer with completed, pending, or cancelled orders
- **THEN** `orderCount` counts all of those orders, `totalSpent` sums only completed-order totals, and `lastPurchaseAt` is the latest completed timestamp

#### Scenario: Customer with no completed orders
- **WHEN** an Admin lists a customer who has no completed orders
- **THEN** `totalSpent` is zero and `lastPurchaseAt` is null

### Requirement: Customer profile and persisted loyalty history
`GET /api/customers/{id}` SHALL return the profile, persisted points, tier, activity state and timestamps, or 404 for an unknown ID. `GET /api/customers/{id}/loyalty-points` SHALL return newest-first paginated history with point change, balance after, reason, order ID and timestamp. These reads SHALL NOT calculate or change loyalty data. Admin and Manager can read an inactive customer; for SalesStaff an inactive customer SHALL return 404 like an unknown ID.

#### Scenario: Existing customer profile
- **WHEN** an Admin requests an existing customer's ID
- **THEN** the response is 200 with contact details, `loyaltyPoints`, `tier`, and `isActive` as persisted values

#### Scenario: Sales reads an inactive customer
- **WHEN** a SalesStaff requests the profile of an inactive customer
- **THEN** the response is 404 and no profile data is returned

#### Scenario: Point audit history
- **WHEN** an Admin requests an existing customer's loyalty-point history
- **THEN** the response is 200 with the customer's persisted events in descending timestamp and ID order

#### Scenario: Unknown customer history
- **WHEN** an Admin requests profile or point history for an unknown ID
- **THEN** the response is 404
