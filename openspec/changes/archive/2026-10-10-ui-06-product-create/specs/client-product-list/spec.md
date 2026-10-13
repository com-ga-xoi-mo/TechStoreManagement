# Spec Delta

## MODIFIED Requirements

### Requirement: Product grid presents a clear catalog preview
The client SHALL show products as responsive cards with identifying, price, variant and stock information, and SHALL identify mock-backed results as preview data.

#### Scenario: Open product list
- **WHEN** an explicit preview session selects Sản phẩm in the Shell
- **THEN** the Shell remains visible and the content shows a product heading, result count, search and filters, sort control, an eight-item grid page when available, and paging controls

#### Scenario: Product card content
- **WHEN** a product card is displayed
- **THEN** it shows the product image or a fallback, name, SKU, brand, variant swatches when supplied, a starting or selected-variant price, and available quantity with readable stock status

#### Scenario: Select a displayed variant
- **WHEN** a card has multiple variants and the user selects a swatch
- **THEN** that card identifies the selected variant and updates its displayed price and availability from that variant's preview data

#### Scenario: Image unavailable
- **WHEN** a product image is missing or fails to load
- **THEN** the card displays a stable placeholder without hiding its text, price, stock information, or controls

#### Scenario: Preview provenance
- **WHEN** product results come from the mock repository
- **THEN** the screen visibly identifies them as sample/preview data and does not claim live catalog, price, inventory, or permission data

#### Scenario: No out-of-scope product workflow
- **WHEN** the product list is displayed
- **THEN** it provides an accessible “Thêm sản phẩm” action that opens the scoped create dialog, but does not expose a working table mode, edit form, product action menu, or any inventory/Serial/IMEI receipt action

## ADDED Requirements

### Requirement: Successful preview creation refreshes the current product list
The client SHALL refresh the current preview result set after a product is successfully created and SHALL retain the current list criteria.

#### Scenario: Refresh after create
- **WHEN** the create dialog reports a successful preview create operation
- **THEN** the list reloads using its current search, filters, sort, and page context, and labels the result as preview data

#### Scenario: Cancel or failed create
- **WHEN** creation is cancelled or fails
- **THEN** the list is not refreshed as if a product had been created and its current criteria remain unchanged
