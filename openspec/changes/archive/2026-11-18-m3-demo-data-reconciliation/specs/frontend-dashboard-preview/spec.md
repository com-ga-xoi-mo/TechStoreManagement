# Spec Delta

## MODIFIED Requirements

### Requirement: Explicit analytics and seed readiness

Handoff documentation SHALL distinguish completed live Analytics client integration from preview behavior and standalone seeder readiness. Preview MUST NOT be reported as live API integration or completed UC-SEED-01 evidence; the client MUST NOT download all real orders to compute analytics locally.

#### Scenario: Backend rules remain pending
- **WHEN** the live Dashboard is handed off
- **THEN** its route, period keys, DTO fields, Admin/Manager authorization, and UTC+07 snapshot semantics are traced to the current `admin-analytics-api` contract; unresolved backend discrepancies are recorded with an owner and status

#### Scenario: Preview remains separate
- **WHEN** the preview flag is enabled
- **THEN** fixture policy is identified as preview-only, and its values are not described as API response or database verification evidence

#### Scenario: Seed preparation
- **WHEN** UC-SEED-01 readiness is reviewed
- **THEN** documentation compares the fixture with the M3 targets in `data-seeding`: at least 20 products with images and cost prices on a fresh seed, 5 categories, 10 customers, 20 orders across lifecycle states, and four demo accounts. It identifies 50 IMEIs as the current fixture count and records the seeder's idempotency, lifecycle and Analytics reconciliation evidence separately from Dashboard client verification
