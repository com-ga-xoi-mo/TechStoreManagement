## MODIFIED Requirements

### Requirement: Server-side pricing and snapshots
The server SHALL determine each line's `unitPrice` from the catalog: the variant's `price` when a variant is selected, otherwise the product's `basePrice`. Catalog prices exclude VAT. The client SHALL NOT supply prices or totals. Each line SHALL snapshot the product name, variant name (null without a variant) and SKU (the variant SKU when a variant is selected, otherwise the product SKU). A line's `totalPrice` SHALL equal `unitPrice × quantity − discountAmount`. The optional line `discountAmount` defaults to 0, SHALL NOT be negative and SHALL NOT exceed `unitPrice × quantity`. Order totals SHALL follow the order pricing formula.

#### Scenario: Price taken from the variant
- **WHEN** a line selects a variant priced 29,990,000 with quantity 1 and the VAT rate is 0%
- **THEN** the stored line has `unitPrice` = 29,990,000 and `totalPrice` = 29,990,000, and the order `totalAmount` = 29,990,000

#### Scenario: Price taken from the product when no variants exist
- **WHEN** a line selects a product without variants whose `basePrice` is 350,000 with quantity 3 and `discountAmount` 50,000
- **THEN** the stored line has `unitPrice` = 350,000 and `totalPrice` = 1,000,000

#### Scenario: Client-supplied prices are ignored
- **WHEN** a request body contains a `unitPrice`, `taxAmount` or `totalAmount` property
- **THEN** the stored values are still calculated from the catalog and the formula

#### Scenario: Line discount larger than the line
- **WHEN** a line's `discountAmount` exceeds `unitPrice × quantity`
- **THEN** the response is 400 with an error under that line's `discountAmount`

### Requirement: Order detail
`GET /api/orders/{id}` SHALL return the order with its customer summary, amounts including `taxRate`, and every line ordered by product name, SKU then id. Each line SHALL include ids, snapshot names, SKU, `unitPrice`, `quantity`, `discountAmount`, `totalPrice`, its shares `orderDiscountAmount` and `taxAmount`, and for serial-tracked lines `serialImeiId`, `imei` and `serialNumber`. Line cost snapshots (`unit_cost`) SHALL NOT be returned. An unknown id SHALL return 404.

#### Scenario: Detail with sold device
- **WHEN** a client fetches a completed order containing a phone line
- **THEN** the line shows the IMEI of the sold device

#### Scenario: Detail shows the stored allocation
- **WHEN** a client fetches an order created with an order discount and 10% VAT
- **THEN** the order shows `taxRate` 10 and each line shows the `orderDiscountAmount` and `taxAmount` stored at creation

#### Scenario: Unknown order
- **WHEN** a client fetches an id that does not exist
- **THEN** the response is 404

## ADDED Requirements

### Requirement: Order pricing formula
The server SHALL apply line discounts, then the order discount, then VAT. `subtotal` is the sum of line `totalPrice`; the order `discountAmount` (request `orderDiscountAmount`, default 0) is applied once to it, never the line discounts again. `taxAmount` SHALL be `(subtotal − discountAmount) × taxRate / 100` rounded to a whole đồng, half away from zero; `totalAmount` = `subtotal − discountAmount + taxAmount`. The configured VAT rate is stored as `taxRate`.

#### Scenario: Order discount and VAT
- **WHEN** an order has two lines of 1,000,000 each, `orderDiscountAmount` 200,000 and VAT 10%
- **THEN** `subtotal` = 2,000,000, `discountAmount` = 200,000, `taxAmount` = 180,000 and `totalAmount` = 1,980,000

#### Scenario: Line discount is not subtracted twice
- **WHEN** an order has one line 3 × 350,000 with line `discountAmount` 50,000, `orderDiscountAmount` 100,000 and VAT 10%
- **THEN** the line `totalPrice` = 1,000,000, `subtotal` = 1,000,000, `discountAmount` = 100,000, `taxAmount` = 90,000 and `totalAmount` = 990,000

#### Scenario: Tax rounded to a whole đồng
- **WHEN** an order has one line of 1,234,565 with no discounts and VAT 10%
- **THEN** `taxAmount` = 123,457 and `totalAmount` = 1,358,022

#### Scenario: Rate stays with the order
- **WHEN** the configured VAT rate changes after an order was created
- **THEN** the existing order keeps its stored `taxRate`, `taxAmount` and `totalAmount`

#### Scenario: Invalid order discount
- **WHEN** `orderDiscountAmount` is negative, has a fraction of a đồng, or exceeds `subtotal`
- **THEN** the response is 400 with an error under `orderDiscountAmount` and nothing is stored

### Requirement: Order amounts allocated to lines
Every line SHALL store `orderDiscountAmount`, its share of the order discount in proportion to line `totalPrice`, and `taxAmount`, its share of the order tax in proportion to `totalPrice − orderDiscountAmount`. Shares are whole đồng: each line gets the rounded-down share, then the remaining đồng go one by one to the largest remainders, ties to the earlier line. Line shares SHALL sum exactly to the order `discountAmount` and `taxAmount`.

#### Scenario: Even allocation
- **WHEN** two lines of 1,000,000 each share an order discount of 200,000 and VAT 10%
- **THEN** each line has `orderDiscountAmount` = 100,000 and `taxAmount` = 90,000

#### Scenario: Remainder goes to the largest remainder
- **WHEN** three lines of 100,000 each share an order discount of 100,000
- **THEN** the lines receive 33,334, 33,333 and 33,333 and the shares sum to 100,000

#### Scenario: Free line gets no share
- **WHEN** a line has `totalPrice` 0 because its whole amount was discounted
- **THEN** its `orderDiscountAmount` and `taxAmount` are 0

### Requirement: Discount permission
The total discount of an order (line discounts plus the order discount) SHALL be checked against the caller's role. A SalesStaff caller SHALL NOT exceed `Pricing:SalesStaffMaxDiscountPercent` (default 10%) of `Σ unitPrice × quantity`. An Admin caller is limited only by the amount rules. A refused discount SHALL return 400 with an error under `discount` and store nothing.

#### Scenario: Sales within the limit
- **WHEN** a SalesStaff creates an order worth 10,000,000 before discounts with 500,000 line discount and 500,000 order discount
- **THEN** the order is created

#### Scenario: Sales above the limit
- **WHEN** a SalesStaff creates an order worth 10,000,000 before discounts with 600,000 line discount and 500,000 order discount
- **THEN** the response is 400 with an error under `discount` and no order is stored

#### Scenario: Admin above the Sales limit
- **WHEN** an Admin creates the same order with 1,100,000 total discount
- **THEN** the order is created
