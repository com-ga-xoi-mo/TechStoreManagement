# Spec Delta

## ADDED Requirements

### Requirement: Cost price only when the server sends it
The product detail page SHALL show the cost price row only when the API returned a `costPrice`; a `null` value SHALL hide the row without showing `0` or an error.

#### Scenario: Sales opens a product
- **WHEN** a SalesStaff opens product detail and the API returns `costPrice: null`
- **THEN** the page loads normally and shows no cost price row

#### Scenario: Manager opens a product
- **WHEN** a Manager opens the same product
- **THEN** the cost price row shows the formatted value
