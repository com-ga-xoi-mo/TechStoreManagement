## ADDED Requirements

### Requirement: One order-level promotion
An order SHALL use at most one order-level promotion: the VIP discount (`applyVipDiscount`), a voucher (`voucherCode`) or a manual discount (`orderDiscountAmount` above 0). Sending more than one SHALL return 400 under `orderDiscount`. The chosen amount is the order `discountAmount` of the pricing formula, so line discounts stay and the discount is allocated by line `totalPrice` before VAT. The order stores `discountSource` (`None`, `Manual`, `Vip`, `Voucher`) and, for a voucher, its `voucherId`.

#### Scenario: VIP discount
- **WHEN** a Gold customer's order has goods worth 2,000,000 after line discounts and `applyVipDiscount` is true
- **THEN** `discountAmount` is 100,000, `discountSource` is `Vip` and VAT is charged on 1,900,000

#### Scenario: Voucher discount
- **WHEN** an order has goods worth 2,000,000 and a valid 10% voucher code
- **THEN** `discountAmount` is 200,000, `discountSource` is `Voucher` and `voucherId` is stored

#### Scenario: Two promotions in one order
- **WHEN** a request has both `voucherCode` and `applyVipDiscount`
- **THEN** the response is 400 under `orderDiscount` and nothing is stored

#### Scenario: VIP discount without a VIP tier
- **WHEN** `applyVipDiscount` is true for a Standard customer or for an order without a customer
- **THEN** the response is 400 under `applyVipDiscount`

## MODIFIED Requirements

### Requirement: Discount permission
The discount chosen by staff SHALL be checked against the caller's role: line discounts plus a manual order discount. A SalesStaff caller SHALL NOT exceed `Pricing:SalesStaffMaxDiscountPercent` (default 10%) of `Σ unitPrice × quantity`. A VIP or voucher discount is store policy and SHALL NOT count toward that limit, but line discounts are still checked on their own. An Admin caller is limited only by the amount rules. A refused discount SHALL return 400 with an error under `discount` and store nothing.

#### Scenario: Sales within the limit
- **WHEN** a SalesStaff creates an order worth 10,000,000 before discounts with 500,000 line discount and 500,000 manual order discount
- **THEN** the order is created

#### Scenario: Sales above the limit
- **WHEN** a SalesStaff creates an order worth 10,000,000 before discounts with 600,000 line discount and 500,000 manual order discount
- **THEN** the response is 400 with an error under `discount` and no order is stored

#### Scenario: Admin above the Sales limit
- **WHEN** an Admin creates the same order with 1,100,000 total discount
- **THEN** the order is created

#### Scenario: VIP discount does not use the Sales limit
- **WHEN** a SalesStaff creates an order worth 10,000,000 for a Platinum customer with `applyVipDiscount` and 500,000 line discount
- **THEN** the order is created with 1,000,000 VIP discount

#### Scenario: Line discount alone above the limit with a voucher
- **WHEN** a SalesStaff applies a valid voucher and a line discount of 1,100,000 on an order worth 10,000,000
- **THEN** the response is 400 with an error under `discount`
