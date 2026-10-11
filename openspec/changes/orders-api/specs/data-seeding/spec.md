# Spec Delta

## MODIFIED Requirements

### Requirement: Role and Administrative Identity Seeding
The data seeder SHALL seed system roles and an initial administrative user account into PostgreSQL 16. The administrative user SHALL have the fixed id `11111111-1111-1111-1111-111111111111`, which is the default acting user used by API endpoints while authentication is not yet implemented.

#### Scenario: Administrative roles and admin user created
- **WHEN** the seeder executes on an empty identity database
- **THEN** exactly 4 roles MUST be created (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`), and an active user with username `admin`, email `admin@techstore.vn`, and valid password hash corresponding to `Admin@123` MUST be assigned the `Admin` role.

#### Scenario: Admin user has the fixed default id
- **WHEN** the seeder executes on an empty database
- **THEN** the `admin` user's id MUST equal `11111111-1111-1111-1111-111111111111`, so an order or inventory movement attributed to the default acting user satisfies its foreign key to `users`, and every seeded row that references the administrative user MUST use that same id
