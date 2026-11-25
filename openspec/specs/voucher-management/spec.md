# voucher-management Specification

## Purpose
Lets Admin and Manager manage discount vouchers and gives sales a safe way to hold, consume and return one use per order, so the last use of a voucher is never oversold.

## Requirements

### Requirement: Voucher management API
`/api/vouchers` SHALL let Admin and Manager list (search by code or description, filter by active state, paginated), read, create and update vouchers, and activate or deactivate them. Codes are trimmed, upper-cased and unique (409 on duplicate).

#### Scenario: Create a percentage voucher
- **WHEN** a Manager creates `sale10` with 10% and usage limit 50
- **THEN** the response is 201 and the stored code is `SALE10`

#### Scenario: Duplicate code
- **WHEN** a voucher with an existing code is created
- **THEN** the response is 409 and nothing is stored

### Requirement: Voucher input validation
A percentage voucher SHALL have a value in (0, 100] and a fixed voucher a positive whole-đồng value. The minimum order and maximum discount SHALL NOT be negative. The usage limit SHALL be at least 1 and not below the uses already consumed or held. A new voucher SHALL expire in the future. Invalid input returns 400 with an error under the field.

#### Scenario: Percentage above 100
- **WHEN** a voucher is created with type percentage and value 120
- **THEN** the response is 400 with an error under `discountValue`

#### Scenario: Limit below the uses in place
- **WHEN** a voucher with 3 uses and 2 holds is updated to limit 4
- **THEN** the response is 400 with an error under `usageLimit`

### Requirement: Voucher eligibility and amount
A voucher SHALL apply only when it is active, not expired, the goods value after line discounts reaches its minimum order amount, and `usedCount + reservedCount` is below the usage limit. A percentage voucher gives `round(goods × value / 100)` capped by its maximum discount; a fixed voucher gives its value. The amount never exceeds the goods value. An unknown or ineligible code SHALL return 400 under `voucherCode`.

#### Scenario: Percentage voucher with a cap
- **WHEN** a 20% voucher with a 150,000 maximum is applied to goods worth 1,000,000
- **THEN** the discount is 150,000

#### Scenario: Fixed voucher larger than the goods
- **WHEN** a 500,000 fixed voucher is applied to goods worth 300,000
- **THEN** the discount is 300,000

#### Scenario: Below the minimum order
- **WHEN** a voucher with minimum 2,000,000 is applied to goods worth 1,500,000
- **THEN** the response is 400 under `voucherCode` and no discount is applied

### Requirement: Voucher use lifecycle
A voucher use SHALL be held when payment starts, consumed when money is received and returned when the sale is cancelled or the hold expires before payment. Each order has at most one use row. Holding twice for one order, redeeming twice or releasing twice SHALL NOT change the counts again. A consumed use SHALL NOT be returned when the order is later refunded.

#### Scenario: Hold, then redeem
- **WHEN** a use is held and then redeemed for an order
- **THEN** `reservedCount` returns to its start and `usedCount` rises by 1

#### Scenario: Hold, then release
- **WHEN** a held use is released before payment
- **THEN** `reservedCount` returns to its start and `usedCount` is unchanged

#### Scenario: Refund after payment
- **WHEN** a redeemed use is released after the order was refunded
- **THEN** the counts are unchanged

### Requirement: Last use is not oversold
Concurrent holds on the same voucher SHALL be serialized by a row lock so that `usedCount + reservedCount` never exceeds the usage limit. When only one use is left, exactly one concurrent caller SHALL succeed and the others SHALL receive a refusal.

#### Scenario: Two callers, one use left
- **WHEN** two orders hold the last use of a voucher at the same time
- **THEN** one hold succeeds and the other is refused
