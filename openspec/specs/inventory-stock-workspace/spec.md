# inventory-stock-workspace Specification

## Purpose

Provide an authenticated desktop workspace for browsing paged inventory balances, understanding available and reserved quantities, and exposing safe hooks for future stock operations without fabricating unsupported backend data.

## Requirements

### Requirement: Authenticated paged stock loading
The workspace SHALL load inventory balances from `GET /api/inventory/stocks` with the current Bearer session and preserve the server page metadata and `InventoryStockDto` quantities.

#### Scenario: Load a page successfully
- **WHEN** an authenticated user opens the Tồn kho tab and the API returns a valid `PagedResultDto<InventoryStockDto>`
- **THEN** the workspace renders the returned rows, total count, current page, page size, and total pages without replacing values with fixtures

#### Scenario: Missing or denied session
- **WHEN** no access token is available or the API returns HTTP 401/403
- **THEN** the workspace shows a recoverable authentication/permission state and does not present stock rows as successfully loaded

### Requirement: Immediate available-field filtering
The workspace SHALL apply search, stock status, category and supported product/variant filters immediately without a submit action. Every filter change SHALL return to page 1 and request the stock list from the API with the current filters, so the rows, total count and page count always come from the server; the client SHALL NOT hide returned rows on its own. The category selector SHALL list categories from the category tree with their full path, SHALL load that tree once per workspace load rather than on every page request, and SHALL filter by category ID, never by category name. Filters the API cannot evaluate SHALL be marked unavailable.

#### Scenario: Search loaded product metadata
- **WHEN** the user types a product name, product SKU, variant name, or variant SKU fragment
- **THEN** the workspace requests page 1 with that `search` value and shows the returned rows, with the total count and visible range taken from the server metadata

#### Scenario: Clearing a filter reloads the full list
- **WHEN** a filtered request returned a single page and the user clears the search text, or sets the status or category filter back to all
- **THEN** the workspace requests page 1 again without that filter and shows the unfiltered rows and total count

#### Scenario: Filter change returns to the first page
- **WHEN** the user is on page 3 and changes any filter
- **THEN** the next request asks for page 1

#### Scenario: Filter by category
- **WHEN** the user picks a category in the selector
- **THEN** the workspace requests stocks with that category's `categoryId` and shows the returned rows

#### Scenario: Two categories with the same name
- **WHEN** two categories in different branches share a name and the user picks one of them
- **THEN** only rows of the picked category's ID are shown, and the selector distinguishes the two by their full path

#### Scenario: Paging keeps the category selection
- **WHEN** a category is selected and the user moves to another page
- **THEN** the category tree is not requested again and the selector keeps the selected category

#### Scenario: Unsupported category or warehouse filter
- **WHEN** the category tree cannot be loaded, or the user opens the warehouse selector while the API has no warehouse field
- **THEN** that selector remains disabled or displays an unavailable state, stock rows still load, and the client does not fabricate category or warehouse matches

### Requirement: Stock status presentation
The workspace SHALL derive a display status from `AvailableQuantity` and the row's `MinStockAlert`, with the same rule as the API: above `MinStockAlert` is “Còn hàng”, from 1 up to `MinStockAlert` is “Sắp hết hàng”, and 0 is “Hết hàng”. A `MinStockAlert` of 0 SHALL be used as is, so such a row is never “Sắp hết hàng”. An out-of-stock row SHALL receive subdued styling, and the status filter SHALL use the same three statuses as the API.

#### Scenario: Render each stock state
- **WHEN** a row has available quantity above its threshold, between one and its threshold, or equal to zero
- **THEN** it displays the corresponding Vietnamese status badge and the zero-availability row uses the reduced-emphasis treatment

#### Scenario: Per-row threshold
- **WHEN** a row has `MinStockAlert` 10 and available quantity 8
- **THEN** its badge is “Sắp hết hàng”

#### Scenario: Zero threshold matches the API
- **WHEN** a row has `MinStockAlert` 0 and available quantity 5
- **THEN** its badge is “Còn hàng”, the same status under which `inStock=true` returns it

#### Scenario: Low-stock filter keeps a full page
- **WHEN** the user picks “Sắp hết hàng” in the status filter
- **THEN** the workspace requests `lowStock=true`, shows every returned row without hiding any client-side, and the total count and page count equal the server's metadata

### Requirement: Seven-column inventory table
The table SHALL present exactly seven columns: product/SKU, category, sale price, available quantity, reserved quantity, status, and actions, with numeric values aligned as specified by the design. To keep scrolling smooth, a row SHALL NOT draw a decorative icon box, SHALL show the category as plain secondary text rather than a badge, and SHALL keep the colored stock status badge and the row separator.

#### Scenario: Render stock row content
- **WHEN** a stock DTO is displayed
- **THEN** the product cell shows the name and SKU without an icon box, the category cell shows the category name as plain text, quantities show `Quantity`, `ReservedQuantity`, and `AvailableQuantity`, and unavailable fields display an em dash rather than fabricated values

### Requirement: Inventory workspace visual contract
The workspace SHALL be implemented without XAML and SHALL match the structure and tokens in `docs/designs/inventory-stock.html`, including the header actions, underline tabs, filter toolbar, seven-column card, and pagination footer. Where the design differs from the lighter row described in "Seven-column inventory table" and the single row actions menu described in "Deferred stock action hooks", those requirements SHALL take precedence.

#### Scenario: Render the Tồn kho tab
- **WHEN** the workspace is shown in the supported desktop theme
- **THEN** it displays “Danh mục và Kho”, stock total text, “Xuất báo cáo”, “Nhập kho mới”, an active Tồn kho tab, the three filter controls, and the designed typography, spacing, borders, colors, and focus states

### Requirement: Pagination and result states
The workspace SHALL expose deterministic previous, numbered-page, and next controls based on server metadata and SHALL distinguish loading, empty, error, and successful states.

#### Scenario: Navigate pages
- **WHEN** the user selects a valid page or previous/next control
- **THEN** one authenticated page request is made with the selected page and page size, and invalid controls are disabled at the boundaries

#### Scenario: Empty or failed page
- **WHEN** the API returns zero rows or a normalized HTTP/transport error
- **THEN** the workspace shows an empty-result or recoverable error state with retry, without retaining stale rows as current data

### Requirement: Deferred stock action hooks
The workspace SHALL expose actions for “Nhập kho mới”, viewing a stock row's detail, adjusting stock, and deleting a stock row. Each row SHALL offer its actions through one “•••” button, labeled for assistive technology as “Thao tác cho <product name>”, that opens a menu with “Xem chi tiết”, “Điều chỉnh tồn” and “Xóa tồn” in that order, and the menu SHALL be reachable and operable with the keyboard. “Xem chi tiết” SHALL open the stock detail/history dialog, and “Điều chỉnh tồn” SHALL open the adjustment dialog; delete SHALL continue to mutate inventory only after a supported delete operation succeeds.

#### Scenario: Open the row actions menu
- **WHEN** the user activates the “•••” button of a stock row with the pointer, or focuses it with Tab and presses Enter
- **THEN** a menu opens below the button with “Xem chi tiết”, “Điều chỉnh tồn” and “Xóa tồn”, the last one in the danger color, and Esc closes it without any action

#### Scenario: Open stock detail
- **WHEN** the user picks “Xem chi tiết” in a stock row's actions menu
- **THEN** the client opens the detail/history dialog with that row's immutable identity and display context

#### Scenario: Activate a deferred action
- **WHEN** the user activates an inventory action that remains outside this workflow, such as “Nhập kho mới”
- **THEN** the client invokes the registered hook or reports the action as unavailable and issues no stock mutation request

#### Scenario: Open stock adjustment
- **WHEN** the user picks “Điều chỉnh tồn” in a stock row's actions menu
- **THEN** the client opens the adjustment dialog with current quantity and reserved quantity

#### Scenario: Complete supported adjustment
- **WHEN** the user confirms a valid adjustment and the server succeeds
- **THEN** the client closes the dialog, refreshes the inventory list exactly once, and raises the success notification

#### Scenario: Request stock deletion
- **WHEN** the user picks “Xóa tồn” in a stock row's actions menu
- **THEN** the client opens the delete-stock confirmation dialog with that row's immutable identity and display context
- **AND** the table remains unchanged while the dialog is open or the delete operation is unavailable

#### Scenario: Complete supported deletion
- **WHEN** the user confirms a supported delete operation and the server succeeds
- **THEN** the client closes the dialog, refreshes the inventory list exactly once, and raises the success notification

### Requirement: API error and cancellation behavior
The inventory client SHALL use the shared API transport rules for bearer tokens, cancellation, normalized errors, and paged JSON responses.

#### Scenario: Cancel or fail a request
- **WHEN** a load is cancelled, times out, loses network connectivity, or receives a non-success response
- **THEN** cancellation remains cancellation, transport errors retain their type, HTTP status is preserved, and the ViewModel exposes a recoverable error without retrying writes

### Requirement: Inventory workspace verification
The frontend unit-test suite SHALL cover API request construction, paged deserialization, status derivation, immediate filtering, unsupported-filter handling, pagination boundaries, loading/empty/error states, cancellation, and deferred action hooks.

#### Scenario: Run inventory workspace tests
- **WHEN** `tests/Frontend.UnitTests` is executed
- **THEN** all inventory ViewModel and API-client tests pass without requiring a live backend or database

### Requirement: Adjustment form validation and projection
The adjustment dialog SHALL show the signed quantity projection immediately and SHALL prevent submission when amount is non-positive, reason is blank, or the resulting quantity violates reservation constraints.

#### Scenario: Live projection
- **WHEN** the user switches between increase and decrease or changes the amount
- **THEN** the dialog immediately displays the calculated new quantity

#### Scenario: Invalid adjustment form
- **WHEN** the amount or reason is invalid, or a decrease exceeds available quantity
- **THEN** the confirm action is disabled and no adjustment request is sent

### Requirement: Detail and adjustment error recovery
The dialogs SHALL preserve user input and remain open for HTTP 400, 403, 404, 409, timeout, or network failures, while exposing a localized recoverable error.

#### Scenario: Server rejects adjustment
- **WHEN** the adjustment API returns HTTP 400, 403, 404, or 409
- **THEN** the dialog remains open, clears its busy state, and displays the error without discarding the draft

### Requirement: Stock detail opens the row's serials
The stock detail dialog SHALL link to the Serial/IMEI records of its stock row. Activating the link SHALL close the dialog, select the Serial/IMEI tab and request page 1 filtered by the row's `productId` and, when present, `variantId`, never by product name. While this filter is active the tab SHALL name the product and variant and offer "Bỏ lọc" to remove it.

#### Scenario: Open serials of a stock row
- **WHEN** the user opens the detail of the stock row "iPhone 15 - 256GB" and activates the link "Xem N Serial / IMEI của sản phẩm này"
- **THEN** the dialog closes, the Serial/IMEI tab is selected, the client requests page 1 with that row's `productId` and `variantId`, serials of other products such as "iPhone 15 Pro" are not shown, and the tab shows "Đang xem serial của: iPhone 15 - 256GB (<SKU>)"

#### Scenario: Stock row without a variant
- **WHEN** the stock row has no variant and the user activates the serial link
- **THEN** the request carries only the row's `productId`

#### Scenario: Clear the product filter
- **WHEN** the product filter is active and the user activates "Bỏ lọc"
- **THEN** the notice disappears and the client requests page 1 of serials without `productId` or `variantId`, keeping the search text and status filter the user has set

### Requirement: Stock report export
"Xuất báo cáo" SHALL export every stock row matching the current search, category and status filters, not only the visible page, as a UTF-8 CSV with a byte order mark. It SHALL ask where to save through the system save dialog with a suggested name `bao-cao-ton-kho-<yyyyMMdd-HHmmss>.csv`. A failed export SHALL NOT replace the stock table with an error state, and every notice SHALL say whether it succeeded.

#### Scenario: Export all filtered rows
- **WHEN** 250 rows match the “Sắp hết hàng” filter, the user is on page 1 and activates “Xuất báo cáo”
- **THEN** the client requests pages 1 to 3 with `pageSize=100` and `lowStock=true`, and the saved file contains a header and 250 data rows

#### Scenario: Save where the user chooses
- **WHEN** the user picks a folder and name in the save dialog
- **THEN** the file is written there and a success notice shows the row count and the full file path

#### Scenario: Cancel the save dialog
- **WHEN** the user closes the save dialog without saving
- **THEN** no file is written and no notice is shown

#### Scenario: Save dialog unavailable
- **WHEN** the system cannot show a save dialog
- **THEN** the file is written to the Downloads folder without overwriting an existing file, and the notice says the dialog could not be opened and shows the full path

#### Scenario: Nothing to export
- **WHEN** no row matches the current filters
- **THEN** no dialog opens and a notice titled “Không thành công” says there is no data to export

#### Scenario: Export fails
- **WHEN** a stock page request fails during export
- **THEN** no file is written, the stock table stays visible, and a notice titled “Không thành công” asks the user to try again

#### Scenario: Busy while exporting
- **WHEN** an export is running
- **THEN** the button reads “Đang xuất...” and cannot be activated again until the export finishes
