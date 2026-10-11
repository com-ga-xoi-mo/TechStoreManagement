# Spec Delta

## Purpose

Gives the Orders module a single, Inventory-owned way to check, reserve and sell devices and stock so that serial status, stock balances and the movement ledger stay consistent without Orders writing Inventory tables.

## ADDED Requirements

### Requirement: Staging-only operations
Every fulfilment operation that changes data SHALL only stage changes on the shared unit of work; it SHALL NOT save, begin, commit or roll back a transaction. The caller owns the transaction and SHALL open it before calling an operation that locks rows. Operations SHALL work on devices and stock rows locked with `SELECT ... FOR UPDATE`.

#### Scenario: Nothing persisted without the caller's save
- **WHEN** reserve or confirm-sale is called and the caller never saves
- **THEN** the database is unchanged

#### Scenario: Caller rollback undoes everything
- **WHEN** the caller rolls back after a successful reserve
- **THEN** no device status or stock quantity has changed

### Requirement: Read-only availability check
The service SHALL provide an availability check for a list of requested lines (product, optional variant, optional device, quantity) that reads without locking and returns one problem per failing line, keyed by line index, without changing any data. A serial-tracked line is available when its device exists, is `InStock`, has the same product and a variant equal to the line's (null equals null only), and the product / variant has a stock row with at least one available unit. A non-tracked line is available when its stock row exists and `quantity − reserved_quantity` covers the requested quantity.

#### Scenario: Available device
- **WHEN** a line references an `InStock` device of the same product and variant
- **THEN** the check reports no problem for that line

#### Scenario: Several failing lines
- **WHEN** two lines fail for different reasons
- **THEN** the check returns both problems in one call

### Requirement: Reserve devices and stock
Reserving lines SHALL lock the devices in ascending id order and then the stock rows in ascending product / variant order, verify every device is `InStock` with a matching product and variant and every stock row has available quantity, then stage: each device `InStock → Reserved` and each stock row's `reserved_quantity` increased by the total quantity reserved for it. It SHALL NOT change `quantity` and SHALL NOT write an inventory movement. If any verification fails it SHALL stage nothing and return a conflict describing the failing line.

#### Scenario: Reserve a device
- **WHEN** an `InStock` device is reserved
- **THEN** its status becomes `Reserved` and its stock row's `reserved_quantity` increases by 1 while `quantity` is unchanged

#### Scenario: Device already reserved or sold
- **WHEN** a requested device is `Reserved` or `Sold`
- **THEN** the operation reports a conflict and no change is staged for any line

#### Scenario: Insufficient available quantity
- **WHEN** a non-tracked line asks for more than `quantity − reserved_quantity`
- **THEN** the operation reports a conflict and no change is staged

#### Scenario: Several devices sharing one stock row
- **WHEN** three `InStock` devices of one product and variant are reserved together
- **THEN** the shared stock row's `reserved_quantity` increases by 3 and the available-quantity check uses the total, not each device separately

### Requirement: Confirm a sale
Confirming a sale SHALL require each device to be `Reserved` (and each non-tracked quantity to be covered by `reserved_quantity`) and SHALL stage: each device `Reserved → Sold` with `warranty_start_at` = the completion time and `warranty_end_at` = start plus `warranty_months`; each stock row's `quantity` and `reserved_quantity` both reduced by the line quantity; and one `InventoryMovement` per order line of type `Sale` with `quantity_change` = −quantity, `quantity_after` = the stock quantity after that line, the order item id and the acting user. It SHALL return each serial-tracked line's device `unit_cost` so the caller can snapshot costs. Lines SHALL be processed in a stable order so `quantity_after` values are monotonic per stock row.

#### Scenario: Sale of a reserved phone
- **WHEN** a `Reserved` device with `warranty_months` = 12 is sold at 2026-10-11T08:00:00Z
- **THEN** it becomes `Sold` with warranty from that instant to 2027-10-11T08:00:00Z, the stock `quantity` and `reserved_quantity` decrease by 1, and a `Sale` movement of −1 referencing the order line is staged

#### Scenario: Device not reserved
- **WHEN** confirm-sale is called for a device that is `InStock`
- **THEN** the operation reports a conflict and stages nothing

#### Scenario: Ledger consistency
- **WHEN** two lines of one order sell devices from the same stock row with quantity 10
- **THEN** their movements record `quantity_after` 9 and 8

### Requirement: Serial-tracked stock invariant preserved
After reserve and after confirm-sale, for a serial-tracked product the stock row's `quantity` SHALL equal the number of its devices that are `InStock` or `Reserved`, and `reserved_quantity` SHALL equal the number that are `Reserved`.

#### Scenario: Counts match after a sale
- **WHEN** one of 5 `InStock` devices is reserved and then sold
- **THEN** `quantity` is 4, `reserved_quantity` is 0 and 4 devices are `InStock`
