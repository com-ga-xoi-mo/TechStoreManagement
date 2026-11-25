## ADDED Requirements

### Requirement: Cash checkout consumes the voucher use
When a cash checkout prices an order with a voucher, the checkout SHALL hold one use of that voucher and consume it in the same transaction as the payment, through the shared settlement. If the voucher is no longer usable, has no use left or was taken by a concurrent checkout, the checkout SHALL return 409 and store nothing.

#### Scenario: Voucher use consumed
- **WHEN** a cash checkout uses a voucher with one use left
- **THEN** the sale completes and the voucher has no use left

#### Scenario: Last use taken by another sale
- **WHEN** two cash checkouts with the last use of a voucher run at the same time
- **THEN** exactly one completes and the other returns 409 with no stock, shift or points change

#### Scenario: Retry with the same key
- **WHEN** the completed checkout is posted again with the same `checkoutKey`
- **THEN** the stored order is returned and no second use is consumed
