## Why

Milestone 2 Sprint 1 requires the Orders module (Dev 4) to expose order creation, listing, detail and status transitions through the REST API so that the Uno client (`OrderCreatePage`, `OrderListPage`, `OrderDetailDialog`), the customer purchase history and the dashboard can work against real data. Today `backend/Orders/` only contains the `Order`, `OrderItem` and `OrderReturn` entities and their Fluent API configurations from Milestone 1: there is no repository, service, controller or shared DTO.

Selling a device also has to change Inventory state (serial/IMEI status, reserved and on-hand quantities, the `inventory_movements` ledger) atomically. The Inventory module (Dev 3, branch `catalog-inventory-api`, not yet merged) exposes stock initialization and serial import, but nothing for selling: no serial lookup by id with a row lock, and no reserve / confirm-sale operation. Without them an order cannot move a device from `InStock` to `Reserved` to `Sold` without Orders writing Inventory tables directly, which `AGENTS.md` section 9 forbids. The unauthenticated fallback actor used by Dev 3's `InventoryController` is the fixed id `11111111-1111-1111-1111-111111111111`; the foreign keys `orders.cashier_user_id` and `inventory_movements.performed_by_user_id` need that user to exist. (When this change was planned the seeder still created `admin` with a random id; commit `26824d7` on `dev` has since fixed that, so Orders only reuses the same fallback.)

## What Changes

- **UC-ORD-01 – Create order** (`POST /api/orders`): creates a `Pending` retail order (a draft that holds no reservation). The server takes prices from the catalog (variant price, else product base price), snapshots product name, variant name and SKU on each line, validates every line (active product/variant, serial-tracked lines carry one specific `InStock` device with exact product and variant match, non-tracked lines have enough available stock), and calculates totals. The client sends only product / variant / serial / quantity and an optional line discount.
- **UC-ORD-02 – List orders** (`GET /api/orders`): paginated list filtered by status, customer, creation date range and keyword (order code, customer name or phone), with stable sorting.
- **UC-ORD-03 – Order detail** (`GET /api/orders/{id}`): order with its lines, including the sold IMEI/serial and customer summary.
- **UC-ORD-04 – Transition status** (`PATCH /api/orders/{id}/status`): `Pending → Processing` (verify and reserve), `Processing → Completed` (fulfil), `Pending → Completed` (cash shortcut, both steps in one transaction) and `Pending → Cancelled`. All Inventory side effects run inside one transaction with row locks on the order, devices and stock rows.
- **Inventory fulfilment service (new files in `backend/Inventory/`)**: `IInventoryFulfillmentService` / `InventoryFulfillmentService` with check-availability, reserve and confirm-sale operations that only *stage* changes (never save or commit); `OrderService` owns the `IUnitOfWork` transaction. Two additive methods on `ISerialImeiRepository` (`GetByIdsAsync`, `GetForUpdateAsync`) plus the matching test fake. Dev 3's `InventoryService` is not edited.
- **Minimal `ICustomerRepository`** (read: existence/active check by id), following the precedent where Dev 2 created a minimal `ICategoryRepository` for Dev 3 to extend.
- **Shared contracts**: `OrderDto`, `OrderSummaryDto`, `OrderItemDto`, `CreateOrderRequest`, `CreateOrderItemRequest`, `OrderSearchRequest`, `UpdateOrderStatusRequest`.
- **Tests and manual verification**: `OrderServiceTests` and `InventoryFulfillmentServiceTests` with hand-written fakes; sample requests in `backend/TechStore.Api.http`.

Out of scope: returns and refunds (`order_returns`, `PartiallyRefunded`, `Refunded`), releasing reservations (no caller exists while only unpaid `Pending` orders can be cancelled), loyalty points, vouchers, tier discounts, tax rates, POS sessions (`pos_session_id` stays `NULL`), VietQR / card verification (the payment method is recorded and the cashier's confirmation is trusted), order editing after creation, order deletion, authentication/RBAC (the fixed fallback actor is used), frontend, shared exception middleware, migrations.

## Capabilities

### New Capabilities
- `order-management`: Creating retail orders with server-side pricing and validation, listing and filtering orders, order detail, and the order status lifecycle with its Inventory side effects.
- `inventory-fulfillment`: The Inventory-owned operations that reserve devices and stock for an order and confirm a sale (device `Sold` with warranty, stock deduction, `Sale` ledger entry), plus the read-only availability check used when an order is created.

### Modified Capabilities
*(None. The fixed `admin` id is already specified in `openspec/specs/data-seeding/spec.md` and implemented on `dev`.)*

## Impact

- **Backend (`backend/`)**:
  - New: `Orders/Repositories/*` (`IOrderRepository`, `OrderRepository`, `OrderSearchCriteria`), `Orders/Services/*` (`IOrderService`, `OrderService`, pure helpers), `Orders/OrdersController.cs`, `Inventory/Services/IInventoryFulfillmentService.cs`, `Inventory/Services/InventoryFulfillmentService.cs`, `Customers/Repositories/ICustomerRepository.cs`, `CustomerRepository.cs`.
  - Modified: `Inventory/Repositories/ISerialImeiRepository.cs` and `SerialImeiRepository.cs` (two additive methods), `Program.cs` (registrations), `TechStore.Api.http`.
  - Untouched: entities, configurations, migrations, `InventoryService`, other modules.
- **Shared contracts (`shared/`)**: new order DTO and request classes; `PagedResultDto<T>` is reused.
- **API surface**: `POST /api/orders`, `GET /api/orders`, `GET /api/orders/{id}`, `PATCH /api/orders/{id}/status`.
- **Tests**: new `OrderServiceTests.cs`, `InventoryFulfillmentServiceTests.cs`, `FakeOrderRepository.cs`, `FakeCustomerRepository.cs`; `FakeSerialImeiRepository` gains the two new methods.
- **Documentation**: `backend/Orders/README.md` (add `PartiallyRefunded`, repositories, fulfilment dependency).
- **Team coordination**:
  - Dev 3: this change depends on `catalog-inventory-api` being merged into `dev` (`IInventoryStockRepository`, `ISerialImeiRepository`); it adds files in `Inventory/` and two additive methods to `ISerialImeiRepository` and its fake, and must not be developed against a different shape of those interfaces.
  - Dev 1: no change to `Common` is needed; databases seeded before commit `26824d7` still hold a random admin id and must be recreated (`docker compose down -v`) to match the fixed default actor.
  - Dev 5: extends the minimal `ICustomerRepository` for the Customers module.
