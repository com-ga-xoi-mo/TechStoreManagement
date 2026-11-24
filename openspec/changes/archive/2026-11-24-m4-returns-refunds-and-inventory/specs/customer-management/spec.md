## MODIFIED Requirements

### Requirement: Paginated customer search
`GET /api/customers` SHALL return paginated items and metadata, matching name/phone by case-insensitive literal substring and combining tier/activity with AND; omitting `isActive` includes both states. Defaults: page 1, size 20, capped at 100, newest first then ID. Each item SHALL include all-order count, gross delivered spending including tax and the latest delivery time (zero and null without delivered orders). For SalesStaff the server SHALL return only active customers whatever `isActive` is sent.

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
- **THEN** `orderCount` counts all of those orders, `totalSpent` sums original delivered order totals, and `lastPurchaseAt` is the latest delivery timestamp

#### Scenario: Customer with no completed orders
- **WHEN** an Admin lists a customer who has no completed orders
- **THEN** `totalSpent` is zero and `lastPurchaseAt` is null

#### Scenario: Order later returned

- **WHEN** the customer has a delivered order with status `PartiallyRefunded` or `Refunded` and a separate order refunded before delivery
- **THEN** the list and profile preserve the delivered order's original tax-inclusive `TotalAmount` in `totalSpent` and its `CompletedAt` in `lastPurchaseAt`. Both orders count toward `orderCount` and only the delivered order counts toward the profile's `completedOrderCount`.

### Requirement: Customer purchased-device history
`GET /api/customers/{id}/purchased-items` SHALL return a newest-first paginated history of historically delivered order items for that customer, including items from orders later partially or wholly refunded, including product and variant names, order reference and completion time, and linked serial/IMEI details when present. Unknown customers SHALL return 404, and this query SHALL NOT change order, inventory, or customer data.

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

#### Scenario: A refund preserves historical device purchases

- **WHEN** a customer returns part or all of a delivered order
- **THEN** every original delivered item remains in purchase history with its sale reference and linked serial/IMEI. Items from an order refunded before delivery are excluded.
