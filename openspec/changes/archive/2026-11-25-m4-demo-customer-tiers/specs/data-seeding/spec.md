## MODIFIED Requirements

### Requirement: Customer Profiles and Promotional Vouchers Seeding
The data seeder SHALL ensure at least 10 active customer profiles with unique phone numbers, each carrying the loyalty tier its points earn, and valid promotional voucher data covering every voucher state: usable percentage, usable fixed amount, expired, used up and switched off.

#### Scenario: Customer profiles with loyalty balance
- **WHEN** the customer dataset is seeded
- **THEN** at least 10 active customer profiles with unique phone numbers, coherent loyalty balances and tier classifications, and at least one active voucher with a valid code and future expiration date MUST be present.

#### Scenario: Tier matches points
- **WHEN** the customer dataset is seeded into an empty database
- **THEN** every customer's tier MUST equal the tier the loyalty rules give for that customer's points (Standard below 500, Silver from 500, Gold from 1,500, Platinum from 5,000), and the starting balances MUST be the fixture values.

#### Scenario: Vouchers in every state
- **WHEN** the voucher dataset is seeded into an empty database
- **THEN** one voucher each MUST exist that is active and usable as a percentage with a cap, active and usable as a fixed amount, active but expired, active with every use consumed, and switched off, every row satisfying the schema checks (expiry after creation, used count within the limit).
