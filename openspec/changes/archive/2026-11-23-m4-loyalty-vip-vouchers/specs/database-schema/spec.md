## ADDED Requirements

### Requirement: Loyalty and voucher storage
The schema SHALL add `customer_loyalty_points.reference_key` (unique when not null), `vouchers.reserved_count` with a check that `used_count + reserved_count <= usage_limit`, `orders.discount_source`, and a `voucher_redemptions` table with one row per order (unique `order_id`), a voucher FK, a status of `Reserved`, `Redeemed` or `Released`, and the redeem and release timestamps. Every foreign key SHALL have a B-tree index and existing rows SHALL keep their values.

#### Scenario: Duplicate reference key
- **WHEN** two ledger rows with the same `reference_key` are inserted
- **THEN** PostgreSQL rejects the second row with a unique violation

#### Scenario: Holds beyond the limit
- **WHEN** `reserved_count` is raised so that `used_count + reserved_count` exceeds `usage_limit`
- **THEN** PostgreSQL rejects the update

#### Scenario: Two uses for one order
- **WHEN** a second `voucher_redemptions` row is inserted for the same order
- **THEN** PostgreSQL rejects it with a unique violation
