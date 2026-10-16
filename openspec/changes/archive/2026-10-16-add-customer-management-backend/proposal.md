# Proposal

## Why

Sprint 2 assigns Dev 5 customer management (UC-CUST-01 through UC-CUST-06), but the API currently has only a `Customer` entity and a repository lookup used by Orders. Staff cannot manage customer profiles or inspect purchase and existing loyalty histories through a Customers API. This change starts with the backend contract and implementation; the C# Markup client will follow in a separate step.

## What Changes

- Add backend customer search, profile and existing loyalty-history reads, create, update, purchase-history, and deactivation endpoints with documented validation, pagination, and HTTP responses.
- Expose the persisted customer loyalty balance, tier, and point ledger as read-only customer data. This change does not calculate, award, reverse, or otherwise mutate loyalty points.
- Preserve customers and their order history by deactivating them (`is_active = false`); do not expose permanent deletion.
- Add backend service/repository contracts, shared request/response DTOs, dependency injection, unit tests, and API/database verification. Reuse the existing Orders customer filter for purchase history where suitable.
- Keep Customer C# Markup UI, loyalty accrual rules, point redemption, vouchers, and refund reversal outside this backend-first change. UI can be delivered by a later change after the API is stable.

## Capabilities

### New Capabilities

- `customer-management`: Customer profile CRUD, search, purchase history, read-only loyalty balance/history, and deactivation.

### Modified Capabilities

None. Order-completion and loyalty-award behavior remains outside this change and is owned by the Orders workstream if the team later approves it.

## Impact

- Backend: `backend/Customers/`, shared DTOs/requests, and service registration in `backend/Program.cs`.
- Cross-module integration: this change reads the existing customer and loyalty tables. It does not edit `OrderService`, define an award formula, or require an Orders completion hook. Any future writer of loyalty entries must preserve the existing entity/schema contract.
- The current dev branch now includes Dev 1's JWT Bearer middleware and role claims. Customer endpoints must enforce Admin access with that real Identity foundation and verify 401/403 behavior; do not rely on the fact that authentication middleware exists alone.
- Existing schema already has `customers`, `customer_loyalty_points`, and an `orders.customer_id` link. No migration is expected; any newly identified schema change must use EF CLI and be reviewed separately.
- Tests: `CustomerServiceTests` and live API/PostgreSQL checks for the six customer use cases. No frontend implementation or remote publication is authorized by this proposal.
