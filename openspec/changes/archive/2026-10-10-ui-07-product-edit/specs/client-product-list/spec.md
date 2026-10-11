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
- **WHEN** the product list is displayed to a preview user with the relevant capability
- **THEN** it provides accessible “Thêm sản phẩm” and “Chỉnh sửa” actions that open their scoped dialogs, but does not expose a working table mode, product deletion action, or any inventory/Serial/IMEI receipt action

## ADDED Requirements

### Requirement: Successful preview edit refreshes the current product list
The client SHALL refresh the current preview result set after a product edit succeeds and SHALL retain the current list criteria.

#### Scenario: Refresh after edit
- **WHEN** the edit dialog reports a successful preview save
- **THEN** the list reloads using its current search, filters, sort, and page context and reflects the updated product values when they still match the criteria

#### Scenario: Cancel or failed edit
- **WHEN** editing is cancelled or the repository rejects/fails the save
- **THEN** the list does not refresh as if a save succeeded and its current criteria and saved product values remain unchanged
