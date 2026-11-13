# Spec Delta

## MODIFIED Requirements

### Requirement: Role and Administrative Identity Seeding
The data seeder SHALL ensure the four system roles and demo identities for all four roles exist without resetting existing credentials or user data.

#### Scenario: Administrative roles and admin user created
- **WHEN** the seeder runs against an empty identity database
- **THEN** exactly four roles (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`) and an active `admin@techstore.vn` user with fixed identifier `11111111-1111-1111-1111-111111111111` and Admin role MUST be created.

#### Scenario: Existing identity records are preserved
- **WHEN** one or more roles or users already exist, including a Sprint 1 baseline
- **THEN** missing seed roles and identities MUST be added, while existing passwords, profile values, role assignments, and unrelated user records remain unchanged.

#### Scenario: Manager demo is added
- **WHEN** the seeder runs on an empty or existing M2 database without the manager1 demo identity
- **THEN** one active manager1 account with Manager role is created with deterministic identity and documented demo credentials, while existing Admin, cashier and warehouse passwords remain unchanged

#### Scenario: Administratively changed seed user is preserved
- **WHEN** a previously seeded employee has a changed profile, password, role or inactive status and seeding runs again
- **THEN** those changes and their token version remain unchanged; the old role is not silently re-added and the account is not reactivated
