## ADDED Requirements

### Requirement: POS order attributes
An order created through `POST /api/pos/checkout` SHALL carry `posSessionId`, `cashTendered` and `changeGiven`, exposed in the order detail DTO; orders created through `POST /api/orders` SHALL leave these empty. Order detail SHALL show code, dates, customer, lines (name, SKU, quantity, unit price, line discount), order discount, tax, total, payment method and status.

#### Scenario: POS order detail
- **WHEN** a client reads an order made by checkout
- **THEN** the DTO contains `posSessionId`, `cashTendered`, `changeGiven`, `paidAt` and `completedAt`

#### Scenario: Draft order
- **WHEN** a client reads an order made through `POST /api/orders`
- **THEN** `posSessionId`, `cashTendered` and `changeGiven` are null
