# Spec Delta

## Purpose

Lets authorized store administrators manage customer profiles and inspect persisted purchase and loyalty histories through stable, paginated REST responses while preserving past transactions.

## ADDED Requirements

### Requirement: Customer API authorization
Every `/api/customers` endpoint SHALL require an authenticated Admin. Requests without a valid token SHALL return 401; authenticated callers without the Admin role SHALL return 403. No fixed user or role fallback SHALL grant access.

#### Scenario: Admin can list customers
- **WHEN** an authenticated Admin calls `GET /api/customers`
- **THEN** the endpoint returns 200 with a paginated customer list

#### Scenario: Missing or insufficient role
- **WHEN** an unauthenticated caller or authenticated SalesStaff calls a customer endpoint
- **THEN** the response is 401 or 403 respectively and no customer data is changed or exposed

### Requirement: Paginated customer search
`GET /api/customers` SHALL return `items`, `page`, `pageSize`, `totalCount`, and `totalPages`. `q` SHALL search name or phone case-insensitively as a literal substring; `tier` and `isActive` SHALL filter with AND. Omitted `isActive` includes both states. Page defaults to 1, page size to 20, and page size is clamped to 1..100. Results SHALL sort by creation time descending with ID as tie-breaker.

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
`GET /api/customers/{id}/orders` SHALL return a newest-first paginated list of that customer's orders using order summaries, including code, status, total amount, and timestamps. The customer SHALL be checked first; an unknown ID returns 404. An inactive customer retains their order history.

#### Scenario: Customer with completed and pending orders
- **WHEN** an Admin requests orders for a customer who owns both kinds
- **THEN** the response is 200 with only that customer's orders and correct pagination totals

#### Scenario: Inactive customer still has history
- **WHEN** an Admin requests orders for an inactive customer
- **THEN** the response still includes that customer's historical orders

### Requirement: Deactivate customer without data loss
`DELETE /api/customers/{id}` SHALL set `isActive=false` rather than remove the row. It SHALL return 204 for an existing customer, including one already inactive, and 404 for an unknown ID. The customer, point ledger, and linked orders SHALL remain stored; new orders SHALL reject an inactive customer.

#### Scenario: Deactivate customer with purchases
- **WHEN** an Admin deactivates a customer who has orders
- **THEN** the response is 204, their orders and point history remain readable, and a new order using their ID is rejected

#### Scenario: Repeat deactivation
- **WHEN** an Admin deactivates an already inactive customer
- **THEN** the response is 204 and no additional data is removed
