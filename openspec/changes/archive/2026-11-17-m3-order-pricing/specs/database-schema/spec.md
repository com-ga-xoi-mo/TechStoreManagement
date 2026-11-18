## ADDED Requirements

### Requirement: Order pricing allocation columns
The schema SHALL store the VAT rate of each order and each line's share of the order discount and tax. `orders.tax_rate` SHALL be `numeric(5, 2)` not null default 0 with check `tax_rate >= 0 AND tax_rate <= 100`. `order_items.order_discount_amount` and `order_items.tax_amount` SHALL be `numeric(18, 2)` not null default 0, each with a check `>= 0`.

#### Scenario: Migration on existing data
- **WHEN** the migration runs on a database with orders created before this change
- **THEN** those orders get `tax_rate` 0 and their lines get `order_discount_amount` 0 and `tax_amount` 0, and no other value changes

#### Scenario: Invalid values rejected
- **WHEN** a write stores a negative `order_items.tax_amount` or an `orders.tax_rate` above 100
- **THEN** PostgreSQL rejects it with a check constraint violation
