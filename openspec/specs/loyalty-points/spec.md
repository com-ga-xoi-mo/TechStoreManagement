# loyalty-points Specification

## Purpose
Rewards customers with points for completed sales, moves them through VIP tiers and takes points back when goods are returned, so checkout and refunds call one definition of points, tiers and tier discounts.

## Requirements

### Requirement: Points earned on a completed sale
A completed order with a customer SHALL earn `floor((subtotal − discountAmount) / 100,000)` points, where the amount is the goods value after line and order discounts and before VAT. An order without a customer, an order that is not `Completed`, or an amount that earns 0 points SHALL add nothing. The ledger row stores the point change, the balance after and the order ID, and the customer's points increase by the same amount.

#### Scenario: M4 reference sale
- **WHEN** an order of two 1,000,000 items with a 200,000 order discount and 10% VAT is completed for a customer
- **THEN** the customer earns 18 points

#### Scenario: Amount below one point
- **WHEN** a completed order has goods value 99,999 after discounts
- **THEN** no ledger row is added and the balance is unchanged

#### Scenario: Walk-in guest
- **WHEN** an order without a customer is completed
- **THEN** no points are recorded

### Requirement: Earning is idempotent per order
Earning points for an order SHALL be applied at most once. A repeated call for the same order SHALL return the existing result and SHALL NOT add a ledger row or change the balance.

#### Scenario: Repeated earn call
- **WHEN** the earn operation is called twice for the same completed order
- **THEN** one ledger row exists and the balance rose once

### Requirement: Membership tier follows points
The tier SHALL be Standard below 500 points, Silver from 500, Gold from 1,500 and Platinum from 5,000. After every change of points the tier SHALL be recalculated from the new balance. The tier discount is Standard 0%, Silver 2%, Gold 5% and Platinum 10%, and it is read from the tier stored before the sale.

#### Scenario: Reaching Silver
- **WHEN** a Standard customer with 490 points earns 10 points
- **THEN** the balance is 500 and the tier is Silver

#### Scenario: Dropping a tier after a reversal
- **WHEN** a Silver customer with 505 points has 10 points reversed
- **THEN** the balance is 495 and the tier is Standard

### Requirement: Points reversed on a refund
When goods are returned, the points of the order SHALL be recalculated on the goods value the customer still keeps, and the difference SHALL be deducted. The deduction SHALL be recorded with the return ID as its reference, SHALL NOT make the balance negative, and an order that never earned points SHALL lose none. Warranty handling SHALL NOT reverse points.

#### Scenario: Partial then final return
- **WHEN** the 18-point reference sale has one item returned (900,000 goods value kept) and then the other (0 kept)
- **THEN** 9 points are deducted for the first return and 9 for the second

#### Scenario: Repeated return call
- **WHEN** the reversal is called twice with the same return ID
- **THEN** the second call changes nothing

#### Scenario: Not enough balance
- **WHEN** the calculated deduction is larger than the customer's current balance
- **THEN** only the available balance is deducted and the balance is 0, never negative

### Requirement: Loyalty summary
`GET /api/customers/{id}/loyalty-summary` SHALL return the points, tier, tier discount percent, next tier and points missing for it (null at Platinum). It SHALL NOT change data. An unknown customer SHALL return 404, and an inactive customer SHALL return 404 for SalesStaff.

#### Scenario: Summary of a Silver customer
- **WHEN** a client requests the summary of a customer with 700 points
- **THEN** the response shows tier Silver, discount 2%, next tier Gold and 800 points missing
