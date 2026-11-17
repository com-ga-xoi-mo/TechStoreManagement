## ADDED Requirements

### Requirement: Cash details in order detail
The order detail dialog SHALL show `cashTendered` and `changeGiven` for a cash POS order and the shift it belongs to, and SHALL show `—` for orders without them.

#### Scenario: POS order shown
- **WHEN** the detail of a checkout order is opened
- **THEN** cash tendered and change are displayed with currency formatting

#### Scenario: Draft order shown
- **WHEN** the detail of a draft order is opened
- **THEN** the cash fields show `—`
