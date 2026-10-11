# Spec Delta

## ADDED Requirements

### Requirement: Product list opens the showroom for an existing product
The client SHALL provide an accessible activation on a product item that opens the showroom dialog for that product without changing the current list criteria.

#### Scenario: Open a listed product
- **WHEN** the user activates a product card from the Catalog list
- **THEN** the product detail showroom opens for that product and the list's current search, filters, sort, and page remain intact when the dialog closes

#### Scenario: Keyboard activation
- **WHEN** a product card has keyboard focus
- **THEN** Enter or Space opens its showroom and focus returns to the initiating product item after the dialog closes
