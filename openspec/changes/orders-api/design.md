# Design

## Context

See `proposal.md` for motivation, `specs/order-management/spec.md` and `specs/inventory-fulfillment/spec.md` for the required behavior and `specs/data-seeding/spec.md` for the seeder change.

Current state that shapes the approach:

- `Order`, `OrderItem`, `OrderReturn` and their configurations exist (Milestone 1). `orders.order_code` is unique (`idx_orders_code`); `order_items` has composite FKs `(variant_id, product_id)` and `(serial_imei_id, product_id)`, a unique `(order_id, serial_imei_id)`, and `serial_imei_id IS NULL OR quantity = 1`. `OrderStatus` is `Pending, Processing, Completed, Cancelled, PartiallyRefunded, Refunded`.
- Schema notes on `orders` / `order_items` define the lifecycle this design implements: a `Pending` draft may omit its device; payment verification reserves devices and stock; fulfilment releases the reservation, deducts stock, marks the device `Sold`, writes `Sale` movements and activates warranty atomically; paid orders use the refund flow, never cancellation.
- Layering and transactions follow `AGENTS.md` section 9: `Controller → Service → Repository → AppDbContext`; repositories stage, never save; the service owns `IUnitOfWork` (`BeginTransactionAsync`, `SaveChangesAsync`, `CommitAsync`, `RollbackAsync`, no nesting; rollback clears the change tracker; unique violation `23505` becomes `DuplicateKeyException` with `ConstraintName`).
- Catalog (Dev 2) is the template: result records, `[ApiController]` ProblemDetails helpers, fully nullable request contracts, a hand-written validator keyed by camelCase paths, `PagedResultDto<T>`, `ProductPagingHelper`, hand-written fakes.
- Dev 3's work (merged into `dev` as PR #6; the interfaces below were verified after the merge) provides `IInventoryStockRepository` (`GetByProductAndVariantAsync`, `GetForUpdateAsync` using `FromSqlInterpolated ... FOR UPDATE`, `Add`, `AddMovement`), `ISerialImeiRepository` (`SearchAsync`, `FindExistingIdentifiersAsync`, `AddRange`) and `IInventoryService` (`GetStocksAsync`, `InitializeStockAsync`). Its controller resolves the acting user from claims and otherwise falls back to `11111111-1111-1111-1111-111111111111`.
- `DataSeeder` (since commit `26824d7` on `dev`) creates `admin` with the fixed id `11111111-1111-1111-1111-111111111111`, so the fallback actor exists in freshly seeded databases.
- There is no authentication, no exception middleware, no `ICustomerRepository`, and no JSON string-enum converter.

## Goals / Non-Goals

**Goals:**
- Four endpoints (create, list, detail, transition) that keep orders, devices, stock and the movement ledger consistent under concurrent use.
- Orders never write Inventory tables directly; Inventory owns every state change of devices and stock.
- Pricing and validation rules that are unit-testable without PostgreSQL.
- Dev 3's files stay untouched except two additive repository methods; the branch can be rebased onto `dev` once Dev 3 merges.

**Non-Goals:**
- Returns, refunds, `PartiallyRefunded` / `Refunded` transitions, releasing reservations, loyalty, vouchers, tier discounts, tax, POS sessions, VietQR / card verification, order editing, deletion, authentication, RBAC, a global exception middleware, new migrations.

## Decisions

### D1. Module layout
```
backend/Orders/
  Repositories/  IOrderRepository, OrderRepository, OrderSearchCriteria
  Services/      IOrderService, OrderService, OrderPricingHelper, OrderStatusTransitions,
                 OrderCodeGenerator, CreateOrderValidator
  OrdersController.cs
backend/Inventory/Services/   IInventoryFulfillmentService, InventoryFulfillmentService   (new files)
backend/Customers/Repositories/  ICustomerRepository, CustomerRepository                  (minimal)
```
Namespaces follow folders (`TechStore.Api.Orders.Services`, `TechStore.Api.Orders` for the controller, `TechStore.Api.Inventory.Services`). Pure-logic helpers are `public static` so tests call them without `InternalsVisibleTo`.

### D2. Inventory changes go through a new `InventoryFulfillmentService`
`OrderService` calls `IInventoryFulfillmentService`; it does not use Inventory repositories for writes.
- Operations (all take a list of `FulfillmentLine { OrderItemId, ProductId, VariantId, SerialImeiId, Quantity }` plus what they need):
  - `CheckAvailabilityAsync(lines)` – read-only, no locks, returns per-line problems (used by create).
  - `ReserveAsync(lines)` – locks, verifies, stages `InStock → Reserved` and `reserved_quantity += q`; no movement.
  - `ConfirmSaleAsync(lines, completedAt, performedByUserId)` – requires reserved state, stages `Reserved → Sold` + warranty, `quantity -= q`, `reserved_quantity -= q`, one `Sale` movement per line, and returns the devices' `unit_cost` by order item id.
- Each mutating operation returns a result record (`Success` / `Conflict(lineIndex/reason)`), never throws for business conflicts, and **never calls `SaveChanges`/transaction methods** so `OrderService` can save order and inventory changes in one commit.
- It reuses Dev 3's `IInventoryStockRepository.GetForUpdateAsync` / `AddMovement` and adds two methods to `ISerialImeiRepository`: `GetByIdsAsync(ids)` (read, `AsNoTracking`) and `GetForUpdateAsync(ids)` (tracked, `SELECT * FROM serial_imeis WHERE id = ANY(@ids) ORDER BY id FOR UPDATE`). The matching `FakeSerialImeiRepository` gains both.
- *Why new files*: Dev 3 is editing `InventoryService` / `ISerialImeiRepository` on their branch; new files do not conflict, and the two additive repository methods are one-line merges.
- *Alternative A (rejected)*: `OrderService` calls `IInventoryStockRepository` / `ISerialImeiRepository` directly. Fewer files, but it breaks the AGENTS.md rule that writes to another module's tables go through that module's service, and duplicates inventory rules (state checks, ledger math, warranty) inside Orders.
- *Alternative B (rejected)*: add the methods to Dev 3's `IInventoryService`. Cleanest ownership, but guaranteed merge conflicts on files Dev 3 is still changing.
- `ReleaseAsync` is **not** built: with cancellation limited to `Pending` (holds nothing) there is no caller. It is deferred to the milestone that adds VietQR expiry / payment failure handling.

### D3. Transitions: one transaction, fixed lock order
```
BeginTransaction
 1. order = GetForUpdateWithItems(orderId)            -- lock order row, load lines + Product + Variant
 2. validate transition against OrderStatusTransitions (409 if not allowed)
 3. Pending→Processing / Pending→Completed:
      re-check product (and variant) are still active → 409 otherwise
      fulfillment.ReserveAsync(lines)                 -- locks devices (id asc), then stock rows (product, variant asc)
 4. →Completed: fulfillment.ConfirmSaleAsync(...)     -- same tracked entities, no second lock round trip needed
      set OrderItem.UnitCost = deviceCost ?? product.CostPrice
 5. set status, paid_at (when first paid), completed_at, updated_at
 6. SaveChanges; Commit        (Rollback on conflict or exception, then return 409 / rethrow)
```
- The shortcut `Pending → Completed` is literally steps 3 and 4 in sequence; `ConfirmSaleAsync` sees the entities already staged as `Reserved` because the unit of work shares one `DbContext`.
- Locking the order first serializes repeated clicks / retries on one order; the second call sees the new status and gets 409 without side effects. Locking devices before stock rows in a fixed order avoids deadlocks between concurrent orders.
- `ReserveAsync` rejects availability based on the locked rows, not on the earlier unlocked check from create, so the create-time check is advisory only.
- *Alternative*: optimistic concurrency with a row version column — rejected, it needs a migration and the project convention is pessimistic `FOR UPDATE`.

### D4. Status machine is a table, not scattered `if`s
`OrderStatusTransitions` holds the allowed pairs `Pending→{Processing, Completed, Cancelled}`, `Processing→{Completed}`; everything else is rejected. Allowed target values accepted from the client are `Processing`, `Completed`, `Cancelled` (400 otherwise). A disallowed pair or repeated status is a state conflict → 409.

### D5. Create flow
1. Validate the request (D8), collecting all errors; DB-dependent checks only after input validation passes.
2. Load each distinct product with variants via `IProductRepository.GetWithVariantsAsync` (cross-module **read**; N small, no new Catalog method); check active, variant rules, derive price / names / SKU.
3. Customer check via `ICustomerRepository` when `customerId` is present.
4. `IInventoryFulfillmentService.CheckAvailabilityAsync` for stock / device availability (unlocked).
5. Build `Order` + `OrderItem`s with `OrderPricingHelper` (pure function: unit price, line total, subtotal, total).
6. Persist inside a retry loop (D9). No inventory entity is modified; creation is therefore a single short transaction.
- Serial-tracked lines must carry the device at creation. The schema allows a device-less draft, but there is no edit-order endpoint in Milestone 2 to attach one later, so allowing it would create orders that can never be fulfilled.
- Two lines may not share a device or a non-tracked product/variant pair; this keeps stock aggregation and the `(order_id, serial_imei_id)` unique index trivial.

### D6. Pricing
Money is `decimal` with 2 places (matches `numeric(18,2)`). `OrderPricingHelper.Calculate(lines)` returns line totals, `subtotal`, `discountAmount = 0`, `taxAmount = 0`, `totalAmount = subtotal`. The server never reads prices from the request. Prices are snapshotted at creation; later catalog price changes do not affect an existing order.

### D7. Enums travel as strings
`status`, `targetStatus`, `paymentMethod` in DTOs/requests are `string` properties holding the enum name. Parsing uses `Enum.TryParse(ignoreCase: true)` and rejects numeric text and undefined values. This avoids adding a global `JsonStringEnumConverter` to `Program.cs` (cross-cutting, owned by the lead) while giving the Uno client readable values.

### D8. Request contracts and validator
Like Catalog D3, every input property of `CreateOrderRequest`, `CreateOrderItemRequest`, `OrderSearchRequest` and `UpdateOrderStatusRequest` is nullable with no initializers; otherwise `[ApiController]` produces its own PascalCase 400 before the validator runs and turns a missing number into 0. `CreateOrderValidator` returns `Dictionary<string, string[]>` keyed by camelCase path (`customerId`, `paymentMethod`, `notes`, `items`, `items[2].serialImeiId`, ...). Limits: 1..100 lines, `notes` ≤ 500, `quantity` ≥ 1 and ≤ 10,000.

### D9. Order code
`OrderCodeGenerator` formats `ORD-{yyyyMM}-{NNNN:D4}` from UTC now and the highest suffix returned by `IOrderRepository.GetLastOrderCodeAsync(prefix)` (max of existing codes with that prefix, not a count, so deletions never reuse numbers). Persist loop: up to 5 attempts; each attempt begins a transaction, builds a fresh `Order` graph, saves; on `DuplicateKeyException` whose `ConstraintName` is `idx_orders_code`, roll back (this clears the tracker) and retry; other duplicate keys, such as `uq_order_items_order_serial`, are mapped to the validation / conflict result; exhausting attempts returns 409.

### D10. List and detail queries
Repository methods return entities (`AsNoTracking`), never DTOs or `IQueryable`:
- `SearchAsync(OrderSearchCriteria)` → `(IReadOnlyList<Order> Items, int TotalCount)`; includes `Customer`, loads line count via a projection-friendly `Include`/`Count`. Keyword uses `EF.Functions.ILike` over order code, customer name and phone with `%`, `_`, `\` escaped by a small private helper (Catalog's escaper is private to its repository). Order: chosen sort key then `Id`.
- `GetWithItemsAsync(id)` → order + customer + items (with `SerialImei`) for detail.
- `GetForUpdateWithItemsAsync(id)` → tracked order locked with `SELECT * FROM orders WHERE id = {id} FOR UPDATE`, then lines with `Product`, `Variant`, `SerialImei` loaded (requires the open transaction).
- `GetLastOrderCodeAsync(prefix)`, `Add(order)`.
Paging reuses `ProductPagingHelper` (public static, same 20 / 1..100 rules) and `PagedResultDto<T>`; promoting the helper to `Common/` is a later cleanup.

### D11. DTOs
`OrderSummaryDto`, `OrderDto`, `OrderItemDto` in `shared/DTOs/`. Dates are UTC `DateTime`. `OrderItemDto` carries `imei`, `serialNumber` but **not** `unit_cost`: cost is margin data that Milestone 3 will hide from non-managers, so it is kept out of the contract from the start.

### D12. Minimal Customers repository
`ICustomerRepository` with `GetByIdAsync(id)` (`AsNoTracking`) and a `CustomerRepository`, registered Scoped. Same precedent as Catalog (Dev 2 created a minimal `ICategoryRepository`, Dev 3 extended it). Dev 5 owns and extends it; they must not create a second one.

### D13. Acting user
`OrdersController` resolves the user like Dev 3's controller (claim `NameIdentifier` / `sub`, else the fixed id `11111111-1111-1111-1111-111111111111`). The method is private and duplicated for now with a comment; it is replaced by the shared mechanism when authentication (Dev 1) lands. No seeder change is required: `DataSeeder` already gives `admin` that id. Databases seeded before that fix keep a random admin id (the seeder skips a non-empty database) and must be recreated with `docker compose down -v`.

### D14. Routes and action names
`[Route("api/orders")]`; actions `Create` (POST, `CreatedAtAction(nameof(GetById), ...)`), `Search` (GET), `GetById` (GET `{id:guid}`), `UpdateStatus` (PATCH `{id:guid}/status`). Action names carry no `Async` suffix (ASP.NET Core strips it, which breaks `CreatedAtAction`); service methods keep it. Error mapping: validation → `ValidationProblem`, not found → `NotFound`, state / stock / device conflict → `Problem(409)`.

### D15. Branch and commits
Branch `feat/orders-api` from `dev` (already created). Because Inventory work needs Dev 3's interfaces on `dev`, tasks are ordered so everything independent of those interfaces comes first; the branch is rebased onto `dev` after Dev 3's pull request merges. Commits: (1) OpenSpec artifacts; (2) shared contracts + Orders helpers; (3) Inventory fulfilment; (4) Orders repository / service / controller; (5) docs and task progress. No commit touches unrelated files such as `.gitignore`.

## Risks / Trade-offs

- [Dev 3's branch is unmerged, interfaces may change] → Inventory fulfilment tasks are gated on the merge; rebase and adjust method names if needed; keep new Inventory code in new files.
- [Databases seeded before commit `26824d7` keep a random admin id] → Recreate the local database; the symptom is a foreign-key error (`23503`) on `cashier_user_id` or `performed_by_user_id`.
- [Unauthenticated fallback acts as admin] → Acceptable only until authentication; the fallback must be removed when `[Authorize]` is introduced (tracked in Open Questions).
- [Create-time availability check is unlocked and can go stale] → Transitions re-verify under locks and return 409; a stale draft is harmless because it reserves nothing.
- [Only the locking SQL and unique-code retry depend on PostgreSQL and cannot be unit tested] → Fakes cover service logic; manual requests in `TechStore.Api.http` cover `FOR UPDATE`, the race on one device and the 23505 retry; no integration test project exists yet.
- [Cash / VietQR / card payments are trusted, not verified] → Documented non-goal; real verification belongs to the payment milestone.
- [Per-product `GetWithVariantsAsync` calls are N+1] → Orders have at most 100 lines and typically a few; revisit with a batch method if profiling shows a problem.
- [Product deactivated between create and payment] → Re-checked at the payment step (`Pending → Processing/Completed`) with 409; not re-checked at `Processing → Completed`, because the sale is already paid.
- [Serial-tracked stock invariant (`quantity = InStock + Reserved`) depends on Dev 3's import keeping it] → Fulfilment preserves it and has tests; seeded data already satisfies it.
- [Reusing `ProductPagingHelper` couples Orders to a Catalog class] → Trivial static helper; promote to `Common/` later.

## Migration Plan

- No database migration. Branch `feat/orders-api` from `dev`; commits as in D15; merge by pull request after Dev 3's Inventory pull request.
- Developers whose local database was seeded before the admin-id fix recreate it (`docker compose down -v && docker compose up -d`).
- Rollback: revert the commits; no schema or data change to undo (orders created during manual testing are disposable development data).

## Open Questions

- When authentication lands, who removes the fixed-id fallback in `OrdersController` and `InventoryController`? (Proposal: Dev 1 with the `[Authorize]` rollout.)
- Should `Processing → Cancelled` (unpaid payment attempt failed) exist once VietQR is implemented? That milestone would add `ReleaseAsync`.
- Non-tracked goods costing policy is still unagreed with Dev 3; this change uses the product's `cost_price`.
- Tax rate, voucher and tier discount rules are undefined; `taxAmount` and order-level `discountAmount` stay 0 until the team decides.
- Whether to adopt a global `JsonStringEnumConverter` later, which would let DTO properties become real enums.
- Seed data gap found during live verification: `DELL-XPS15-9530`, `MBP14-M3-512` and `S24U-256` are serial-tracked and have `inventory_stocks` rows (quantity 2, 3 and 5) but no `serial_imeis` rows, so they violate `quantity = InStock + Reserved` and cannot be sold through Orders (a device is required). Only the iPhone has devices. The seeder owner should add devices (or lower the stock) so the demo can sell more than one product.
