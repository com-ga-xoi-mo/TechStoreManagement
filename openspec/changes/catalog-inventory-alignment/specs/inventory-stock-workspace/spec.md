## MODIFIED Requirements

### Requirement: Immediate available-field filtering
The workspace SHALL apply search, stock status, category and supported product/variant filters immediately without a submit action. The category selector SHALL list categories from the category tree with their full path and SHALL filter by category ID, never by category name. Filters the API cannot evaluate SHALL be marked unavailable.

#### Scenario: Search loaded product metadata
- **WHEN** the user types a product name, product SKU, variant name, or variant SKU fragment
- **THEN** matching loaded rows are shown case-insensitively and the visible range is recalculated from the filtered result

#### Scenario: Filter by category
- **WHEN** the user picks a category in the selector
- **THEN** the workspace requests stocks with that category's `categoryId` and shows only rows whose `categoryId` equals it

#### Scenario: Two categories with the same name
- **WHEN** two categories in different branches share a name and the user picks one of them
- **THEN** only rows of the picked category's ID are shown, and the selector distinguishes the two by their full path

#### Scenario: Unsupported category or warehouse filter
- **WHEN** the category tree cannot be loaded, or the user opens the warehouse selector while the API has no warehouse field
- **THEN** that selector remains disabled or displays an unavailable state, stock rows still load, and the client does not fabricate category or warehouse matches

### Requirement: Stock status presentation
The workspace SHALL derive a display status from `AvailableQuantity` and the row's `MinStockAlert` (5 when the API sends a value of 0 or less): above the threshold is “Còn hàng”, from 1 up to the threshold is “Sắp hết hàng”, and 0 is “Hết hàng”. An out-of-stock row SHALL receive subdued styling, and the status filter SHALL use the same three statuses as the API.

#### Scenario: Render each stock state
- **WHEN** a row has available quantity above its threshold, between one and its threshold, or equal to zero
- **THEN** it displays the corresponding Vietnamese status badge and the zero-availability row uses the reduced-emphasis treatment

#### Scenario: Per-row threshold
- **WHEN** a row has `MinStockAlert` 10 and available quantity 8
- **THEN** its badge is “Sắp hết hàng”

#### Scenario: Low-stock filter keeps a full page
- **WHEN** the user picks “Sắp hết hàng” in the status filter
- **THEN** the workspace requests `lowStock=true`, shows every returned row without hiding any client-side, and the total count and page count equal the server's metadata
