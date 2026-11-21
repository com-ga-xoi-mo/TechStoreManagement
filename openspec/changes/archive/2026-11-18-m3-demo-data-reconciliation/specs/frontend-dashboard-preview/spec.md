# Spec Delta

## MODIFIED Requirements

### Requirement: Explicit analytics and seed readiness

Bàn giao SHALL phân biệt tích hợp live Analytics client đã hoàn tất với hành vi preview và seeder độc lập. Preview MUST NOT được báo là tích hợp live API hoặc bằng chứng UC-SEED-01 hoàn tất; client MUST NOT tải toàn bộ đơn thật để tự tính analytics.

#### Scenario: Backend rules remain pending
- **WHEN** the live Dashboard is handed off
- **THEN** its route, period keys, DTO fields, Admin/Manager authorization, and UTC+07 snapshot semantics are traced to the current `admin-analytics-api` contract; unresolved backend discrepancies are recorded with an owner and status

#### Scenario: Preview remains separate
- **WHEN** the preview flag is enabled
- **THEN** fixture policy is identified as preview-only, and its values are not described as API response or database verification evidence

#### Scenario: Seed preparation
- **WHEN** UC-SEED-01 readiness is reviewed
- **THEN** documentation compares the fixture with the M3 targets in `data-seeding`: at least 20 products with images and cost prices on a fresh seed, 5 categories, 10 customers, 20 orders across lifecycle states, and four demo accounts. It identifies 50 IMEIs as the current fixture count and records the seeder's idempotency, lifecycle and Analytics reconciliation evidence separately from Dashboard client verification
