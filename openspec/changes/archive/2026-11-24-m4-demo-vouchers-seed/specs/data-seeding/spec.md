## MODIFIED Requirements

### Requirement: Customer Profiles and Promotional Vouchers Seeding
The data seeder SHALL ensure at least 10 active customer profiles with unique phone numbers and valid promotional voucher data covering every voucher state: usable percentage, usable fixed amount, expired, used up and switched off.

#### Scenario: Customer profiles with loyalty balance
- **WHEN** the customer dataset is seeded
- **THEN** at least 10 active customer profiles with unique phone numbers, coherent loyalty balances and tier classifications, and at least one active voucher with a valid code and future expiration date MUST be present.

#### Scenario: Vouchers in every state
- **WHEN** the voucher dataset is seeded into an empty database
- **THEN** one voucher each MUST exist that is active and usable as a percentage with a cap, active and usable as a fixed amount, active but expired, active with every use consumed, and switched off, every row satisfying the schema checks (expiry after creation, used count within the limit).

## ADDED Requirements

### Requirement: Seeded Vouchers Are Managed By Administrators
The data seeder SHALL identify a demo voucher by its code only and SHALL add the demo vouchers that are missing without reading or changing the values, active state or use counts of those that exist, because Administrators and Managers can switch off, edit and spend a seeded voucher.

#### Scenario: Administrator edit is kept
- **WHEN** an Administrator has switched off a seeded voucher, changed its discount and limit, and its used and held counts are not zero, and the seeder runs again
- **THEN** the seeder MUST succeed and MUST leave those values unchanged.

#### Scenario: Deleted demo voucher is restored
- **WHEN** one demo voucher was deleted and another was edited, and the seeder runs again
- **THEN** only the deleted voucher MUST be added with its fixture values, and the edited one MUST keep its edit.

#### Scenario: Duplicate code is a conflict
- **WHEN** two voucher rows match one demo code, ignoring case and surrounding spaces
- **THEN** the seeder MUST fail with a message naming the voucher and its code and MUST roll back the seed transaction.
