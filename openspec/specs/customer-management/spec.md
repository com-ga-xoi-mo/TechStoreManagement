# customer-management Specification

## Purpose

Lets authorized store administrators manage customer profiles and inspect persisted purchase and loyalty histories through stable, paginated REST responses while preserving past transactions.

## Requirements

### Requirement: Customer API authorization
Every `/api/customers` endpoint SHALL require an authenticated Admin. Requests without a valid token SHALL return 401; authenticated callers without the Admin role SHALL return 403. No fixed user or role fallback SHALL grant access.

#### Scenario: Admin can list customers
- **WHEN** an authenticated Admin calls `GET /api/customers`
- **THEN** the endpoint returns 200 with a paginated customer list

#### Scenario: Missing or insufficient role
- **WHEN** an unauthenticated caller or authenticated SalesStaff calls a customer endpoint
- **THEN** the response is 401 or 403 respectively and no customer data is changed or exposed

### Requirement: Paginated customer search
`GET /api/customers` SHALL return paginated items and metadata. It SHALL filter name/phone by case-insensitive literal substring, combine tier/activity with AND, and include both activity states when `isActive` is omitted. Defaults are page 1/size 20, capped at 100; ordering is creation descending then ID. Each item SHALL include all-order count, completed-order spending, and latest completed time; no completed orders means zero spending and null time.

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

### Requirement: Customer profile and persisted loyalty history
`GET /api/customers/{id}` SHALL return the profile, persisted point balance, tier, activity state, and timestamps, or 404 for an unknown ID. `GET /api/customers/{id}/loyalty-points` SHALL return a newest-first paginated audit history with point change, balance after, reason, order ID, and timestamp. This change SHALL NOT calculate, award, reverse, or mutate loyalty data through these read endpoints. An inactive customer remains readable to Admin.

#### Scenario: Existing customer profile
- **WHEN** an Admin requests an existing customer's ID
- **THEN** the response is 200 with contact details, `loyaltyPoints`, `tier`, and `isActive` as persisted values

#### Scenario: Point audit history
- **WHEN** an Admin requests an existing customer's loyalty-point history
- **THEN** the response is 200 with the customer's persisted events in descending timestamp and ID order

#### Scenario: Unknown customer history
- **WHEN** an Admin requests profile or point history for an unknown ID
- **THEN** the response is 404

### Requirement: Create customer
`POST /api/customers` SHALL accept `fullName`, `phone`, optional `email`, `dateOfBirth`, and `address`; it SHALL never accept client-selected points, tier, or activity state. It SHALL trim contact text, require a nonblank name and phone, reject malformed email or future birth dates with 400 field errors, and return 201 with the profile and `Location` on success. New customers SHALL start active with zero points and Standard tier.

#### Scenario: Valid new customer
- **WHEN** an Admin posts a valid name and unused phone
- **THEN** the response is 201 with `Location: /api/customers/{id}`, `loyaltyPoints=0`, `tier=Standard`, and `isActive=true`

#### Scenario: Several validation errors
- **WHEN** name and phone are blank and the birth date is in the future
- **THEN** one 400 response reports errors under `fullName`, `phone`, and `dateOfBirth`, and stores nothing

#### Scenario: Existing phone or concurrent duplicate
- **WHEN** the submitted phone already belongs to another customer, including a concurrent creation race
- **THEN** the response is 409 and no duplicate customer is stored

### Requirement: Update customer contact details
`PUT /api/customers/{id}` SHALL replace the editable contact fields using the same validation as creation and return the updated profile. Points, tier, and activity state SHALL remain server-controlled. An unknown ID SHALL return 404; a phone already used by another customer SHALL return 409.

#### Scenario: Valid contact update
- **WHEN** an Admin updates an existing customer's name, phone, email, birth date, or address with valid values
- **THEN** the response is 200 with the new contact data and unchanged persisted point balance and tier

#### Scenario: Duplicate phone update
- **WHEN** an Admin assigns a phone belonging to another customer
- **THEN** the response is 409 and the original profile remains unchanged

### Requirement: Customer purchase history
`GET /api/customers/{id}/orders` SHALL return the customer's newest-first paginated order summaries, including code, status, amount, timestamps, and the first stable item's product, optional variant, and category names. Those names SHALL use the existing product relationship without a schema change, and be null when an order has no items. An unknown customer returns 404; inactive customers retain their order history.

#### Scenario: Customer with completed and pending orders
- **WHEN** an Admin requests orders for a customer who owns both kinds
- **THEN** the response is 200 with only that customer's orders and correct pagination totals

#### Scenario: Inactive customer still has history
- **WHEN** an Admin requests orders for an inactive customer
- **THEN** the response still includes that customer's historical orders

#### Scenario: Order has a primary product
- **WHEN** an Admin requests order history for a customer whose order contains product items
- **THEN** the order summary includes the first stable primary item's product, variant when present, and product category names

#### Scenario: Order has no product items
- **WHEN** an Admin requests order history for an order without an item
- **THEN** the primary product, variant, and category fields are null without fabricating display data

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

#### Scenario: Unknown customer purchased-item history
- **WHEN** an Admin requests purchased items for an unknown customer ID
- **THEN** the response is 404

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

### Requirement: Reactivate customer
`PATCH /api/customers/{id}/activate` SHALL set `isActive=true` without changing the customer's profile, points, orders, or other history. It SHALL return 204 for an existing customer, including one already active, and 404 for an unknown ID.

#### Scenario: Reactivate an inactive customer
- **WHEN** an Admin activates an inactive customer
- **THEN** the response is 204, the customer becomes active, and all historical data remains unchanged

#### Scenario: Repeat activation
- **WHEN** an Admin activates an already active customer
- **THEN** the response is 204 and no unrelated customer data is changed
