# client-product-list Specification

## Purpose

Lets staff browse and narrow a product catalog in the TechStore desktop client through a responsive grid, with preview data clearly distinguished from authoritative catalog and inventory data.

## Requirements

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
- **THEN** it provides accessible “Thêm sản phẩm” and “Chỉnh sửa” actions that open their scoped dialogs, but does not expose a working table mode, product deletion action, or any inventory/Serial/IMEI receipt action

### Requirement: Successful preview creation refreshes the current product list
The client SHALL refresh the current preview result set after a product is successfully created and SHALL retain the current list criteria.

#### Scenario: Refresh after create
- **WHEN** the create dialog reports a successful preview create operation
- **THEN** the list reloads using its current search, filters, sort, and page context, and labels the result as preview data

#### Scenario: Cancel or failed create
- **WHEN** creation is cancelled or fails
- **THEN** the list is not refreshed as if a product had been created and its current criteria remain unchanged

### Requirement: Search and filters narrow the product result set
The client SHALL apply product search and selected catalog filters together and show the resulting total count.

#### Scenario: Search by name or SKU
- **WHEN** the user enters a search term
- **THEN** matching is case-insensitive against product name or SKU, and the visible cards and total count reflect the matching result set

#### Scenario: Combine filters
- **WHEN** the user selects a category, brand, price range, and/or stock status
- **THEN** all selected criteria are applied together, the active selections are visible, and the displayed count represents products matching every selected criterion

#### Scenario: Price range boundaries
- **WHEN** the price filter is set to Dưới 10 triệu, 10–25 triệu, or Trên 25 triệu
- **THEN** a product priced exactly 10 million belongs to 10–25 million, a product priced exactly 25 million remains in that range, and only greater prices belong to Trên 25 triệu

#### Scenario: Clear search and filters
- **WHEN** the user clears the search and returns filters to Tất cả
- **THEN** the unfiltered product result set is requested and its count and first page are shown

### Requirement: Product results can be sorted and paged
The client SHALL provide the D23 sort choices and page navigation over the filtered result set.

#### Scenario: Sort results
- **WHEN** the user chooses newest, price low-to-high, price high-to-low, or stock high-to-low
- **THEN** cards are ordered by that choice while the selected filters remain applied

#### Scenario: Navigate pages
- **WHEN** a filtered result set contains more than eight products
- **THEN** the user can move between pages, see the current range and total count, and previous/next controls are disabled at their respective boundaries

#### Scenario: Criteria change resets page
- **WHEN** the user changes search, category, brand, price, stock, or sort while on a later page
- **THEN** the query returns to the first page before showing the new result set

#### Scenario: Page beyond current result set
- **WHEN** refreshed criteria produce fewer pages than the current page index
- **THEN** the client returns to a valid first page and does not show an empty page as if it were the matching result

### Requirement: Product loading states and request ordering are explicit
The client SHALL distinguish loading, populated, empty, and failed product queries and prevent obsolete requests from publishing results.

#### Scenario: Loading a query
- **WHEN** a product query is pending
- **THEN** loading feedback is visible, repeated submission is prevented, and results are not presented as belonging to the new criteria before they arrive

#### Scenario: No matching products
- **WHEN** a successful query returns no products
- **THEN** an empty/no-match message is shown with the current criteria retained and without a fake product card

#### Scenario: Query failure and retry
- **WHEN** the mock repository fails
- **THEN** a readable error and retry action are shown, and retry uses the current criteria and page without silently switching data sources

#### Scenario: Older search finishes last
- **WHEN** a slower request for an earlier search or filter finishes after a newer request
- **THEN** the earlier response cannot replace the newer criteria, count, page, or cards

#### Scenario: Leave product list during query
- **WHEN** the user leaves Sản phẩm while a query is pending
- **THEN** the abandoned request cannot overwrite the state of a later visit or another route

### Requirement: Product list remains usable at the user's desktop size
The client SHALL keep product controls and information reachable by keyboard and at normal or narrower usable desktop window widths.

#### Scenario: Narrow desktop window
- **WHEN** the user narrows the window while viewing products
- **THEN** cards reflow and the page can scroll vertically without horizontal clipping of search, filters, cards, feedback, or paging

#### Scenario: Keyboard operation
- **WHEN** the user operates search, filters, variant swatches, retry, and paging with keyboard input
- **THEN** each available control is reachable in a sensible Tab order, Enter/Space activates it, and focus remains visible

#### Scenario: Stock status is readable without color
- **WHEN** a product availability status is shown
- **THEN** its text communicates available, low, out-of-stock, or discontinued state without relying on color alone

### Requirement: Product list opens the showroom for an existing product
The client SHALL provide an accessible activation on a product item that opens the showroom dialog for that product without changing the current list criteria.

#### Scenario: Open a listed product
- **WHEN** the user activates a product card from the Catalog list
- **THEN** the product detail showroom opens for that product and the list's current search, filters, sort, and page remain intact when the dialog closes

#### Scenario: Keyboard activation
- **WHEN** a product card has keyboard focus
- **THEN** Enter or Space opens its showroom and focus returns to the initiating product item after the dialog closes

### Requirement: Successful preview edit refreshes the current product list
The client SHALL refresh the current preview result set after a product edit succeeds and SHALL retain the current list criteria.

#### Scenario: Refresh after edit
- **WHEN** the edit dialog reports a successful preview save
- **THEN** the list reloads using its current search, filters, sort, and page context and reflects the updated product values when they still match the criteria

#### Scenario: Cancel or failed edit
- **WHEN** editing is cancelled or the repository rejects or fails the save
- **THEN** the list does not refresh as if a save succeeded and its current criteria and saved product values remain unchanged
