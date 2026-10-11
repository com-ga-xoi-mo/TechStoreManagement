# Spec Delta

## MODIFIED Requirements

### Requirement: Role and Administrative Identity Seeding
The data seeder SHALL seed system roles and an initial administrative user account with a deterministic fixed identifier into PostgreSQL 16.

#### Scenario: Administrative roles and admin user created
- **WHEN** the seeder executes on an empty identity database
- **THEN** exactly 4 roles MUST be created (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`), and an active user with username `admin`, email `admin@techstore.vn`, deterministic identifier `11111111-1111-1111-1111-111111111111`, and valid password hash corresponding to `Admin@123` MUST be assigned the `Admin` role.
