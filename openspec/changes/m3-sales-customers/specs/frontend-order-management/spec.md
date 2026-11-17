## ADDED Requirements

### Requirement: Choosing the order customer
The order creation screen SHALL include the shared customer picker. The default SHALL be "Khách lẻ" (no customer). When a customer is picked, the create request SHALL send that customer's ID as `customerId`; when the choice is cleared it SHALL send no `customerId`. A server error under `customerId` SHALL be shown in the general error area and the draft lines SHALL be kept.

#### Scenario: Order for a picked customer
- **WHEN** the user picks a customer and saves the draft
- **THEN** `POST /api/orders` carries that `customerId` and the created order shows the customer's name

#### Scenario: Walk-in order
- **WHEN** the user keeps "Khách lẻ" and saves the draft
- **THEN** the request has no `customerId`

#### Scenario: Customer deactivated meanwhile
- **WHEN** the server answers 400 under `customerId` because the customer became inactive
- **THEN** the message is shown in the general error area and the lines stay unchanged
