# Spec Delta

## MODIFIED Requirements

### Requirement: Paginated customer search
`GET /api/customers` SHALL return `items`, `page`, `pageSize`, `totalCount`, and `totalPages`. `q` SHALL search name or phone case-insensitively as a literal substring; `tier` and `isActive` SHALL filter with AND. Omitted `isActive` includes both states. Page defaults to 1, page size to 20, and page size is clamped to 1..100. Results SHALL sort by creation time descending with ID as tie-breaker. Each customer item SHALL include the total count of all orders, the sum of `TotalAmount` for `Completed` orders, and the latest completion time among `Completed` orders; customers without completed orders SHALL have zero completed-order spending and no last completed purchase time.

#### Scenario: Search by Vietnamese name or phone
- **WHEN** an Admin searches with a name fragment or a phone fragment
- **THEN** only matching customers are returned, with a correct filtered `totalCount`

#### Scenario: Tier and activity filters
- **WHEN** an Admin requests `tier=Silver&isActive=false`
- **THEN** every returned customer has Silver tier and is inactive

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

### Requirement: Deactivate customer without data loss
`PATCH /api/customers/{id}/deactivate` SHALL set `isActive=false` rather than remove the row. It SHALL return 204 for an existing customer, including one already inactive, and 404 for an unknown ID. The customer, point ledger, and linked orders SHALL remain stored; new orders SHALL reject an inactive customer. The API SHALL NOT expose `DELETE /api/customers/{id}`.

#### Scenario: Deactivate customer with purchases
- **WHEN** an Admin deactivates a customer who has orders
- **THEN** the response is 204, their orders and point history remain readable, and a new order using their ID is rejected

#### Scenario: Repeat deactivation
- **WHEN** an Admin deactivates an already inactive customer
- **THEN** the response is 204 and no additional data is removed

#### Scenario: Hard-delete route is unavailable
- **WHEN** an Admin sends `DELETE /api/customers/{id}`
- **THEN** the API responds with 405 Method Not Allowed and no customer data is changed

## ADDED Requirements

### Requirement: Customer purchased-device history
`GET /api/customers/{id}/purchased-items` SHALL return a newest-first paginated history of completed-order items for that customer, including product and variant names, order reference and completion time, and linked serial/IMEI details when present. Unknown customers SHALL return 404, and this query SHALL NOT change order, inventory, or customer data.

#### Scenario: Customer has tracked completed purchases
- **WHEN** an Admin requests purchased items for a customer with completed orders containing tracked devices
- **THEN** the response is 200 with paginated item details including the linked serial number and/or IMEI

#### Scenario: Customer has completed purchases without a serial or IMEI
- **WHEN** an Admin requests purchased items for a customer whose completed order includes non-serialized items
- **THEN** the response includes those order items with serial and IMEI fields null

#### Scenario: Customer has no completed purchased items
- **WHEN** an Admin requests purchased items for a customer with no qualifying completed order items
- **THEN** the response is 200 with an empty `items` page and accurate pagination totals

#### Scenario: Unknown customer
- **WHEN** an Admin requests purchased items for an unknown customer ID
- **THEN** the response is 404

### Requirement: Customer order-history product context
`GET /api/customers/{id}/orders` SHALL include the primary order item's product name, variant name when present, and category name in each order summary. The category SHALL be read through the existing product relationship and SHALL NOT require a database schema change.

#### Scenario: Order has a primary product
- **WHEN** an Admin requests order history for a customer whose order contains product items
- **THEN** the order summary includes the first stable primary item's product, variant when present, and product category names

#### Scenario: Order has no product items
- **WHEN** an Admin requests order history for an order without an item
- **THEN** the primary product, variant, and category fields are null without fabricating display data

### Requirement: Reactivate customer
`PATCH /api/customers/{id}/activate` SHALL set `isActive=true` without changing the customer's profile, points, orders, or other history. It SHALL return 204 for an existing customer, including one already active, and 404 for an unknown ID.

#### Scenario: Reactivate an inactive customer
- **WHEN** an Admin activates an inactive customer
- **THEN** the response is 204, the customer becomes active, and all historical data remains unchanged

#### Scenario: Repeat activation
- **WHEN** an Admin activates an already active customer
- **THEN** the response is 204 and no unrelated customer data is changed
