# Spec Delta

## MODIFIED Requirements

### Requirement: Acting user
Every Orders request SHALL require a current valid user token. The acting user SHALL be the token's validated nonempty GUID subject/name identifier, never a fixed fallback or client-supplied cashier ID. The acting user SHALL be stored as cashier_user_id on creation and performed_by_user_id on inventory movements written by a transition. Missing, malformed or unknown identity SHALL return 401 without mutation. Admin and SalesStaff SHALL create/transition orders; Admin, Manager and SalesStaff SHALL read orders; other roles SHALL receive 403.

#### Scenario: Unauthenticated request
- **WHEN** a request has no valid authentication or identity claim
- **THEN** the response is 401 and no order or inventory change is written

#### Scenario: Audited caller
- **WHEN** an authorized user creates an order or completes a permitted transition
- **THEN** the persisted cashier or movement actor is that user's ID from the validated token

#### Scenario: Warehouse request rejected
- **WHEN** WarehouseStaff requests any Orders endpoint
- **THEN** the response is 403 before order data or business validation is exposed

## ADDED Requirements

### Requirement: Order access is scoped to the caller
Admin and Manager SHALL read all orders; SalesStaff SHALL list/read only orders with cashier_user_id equal to the caller. Search filters SHALL combine with this mandatory scope and pagination totals SHALL count only visible orders. SalesStaff SHALL transition only their own orders; Admin SHALL transition any order and Manager SHALL NOT write orders. A SalesStaff request to another user's order detail or status route SHALL return 404 and mutate nothing. Existing order pricing, lifecycle and atomicity rules SHALL remain in effect for authorized requests.

#### Scenario: Sales listing is scoped
- **WHEN** SalesStaff lists or searches orders including a customer/date filter
- **THEN** items, totalCount and totalPages describe only matching orders created by that user

#### Scenario: Foreign order ID
- **WHEN** SalesStaff reads or transitions an order created by another user
- **THEN** the response is 404 and no order, stock or audit data changes

#### Scenario: Manager read-only access
- **WHEN** Manager reads an existing order then attempts an order creation or transition
- **THEN** read succeeds and both write actions return 403
