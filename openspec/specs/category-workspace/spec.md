# category-workspace Specification

## Purpose

Provide a desktop category workspace for browsing, searching, filtering, and navigating a category hierarchy using authenticated API data and a consistent Vietnamese dashboard presentation.

## Requirements

### Requirement: Category workspace layout
The workspace SHALL present the header, underline tabs, filter toolbar, tree table, and footer in the order and visual structure of `docs/designs/category-list.html`. Explicit numeric and text values in the change request SHALL take precedence where they differ from the HTML. Category feature UI SHALL introduce no XAML files.

#### Scenario: Open the category workspace
- **WHEN** the user opens the category workspace
- **THEN** it shows “Danh mục và Kho”, a data-bound category total, and “Thêm danh mục” with an add icon
- **AND** “Danh mục” is active with a blue underline and count badge, while “Tồn kho” and “Serial / IMEI” retain inactive styling until selected
- **AND** selecting “Tồn kho” activates the inventory-stock workspace in the same feature shell without adding a seventh Shell destination
- **AND** selecting “Serial / IMEI” retains its inactive/deferred state until its own capability is implemented

#### Scenario: Apply requested visual tokens
- **WHEN** the workspace is rendered
- **THEN** its background is `#F8FAFC`, the white card has a 12-unit radius, `#E2E8F0` border, and subtle shadow, and primary controls use `#0066FF` with `#1D4ED8` hover
- **AND** primary and secondary text use `#0F172A` and `#64748B`, with the HTML typography hierarchy and spacing

### Requirement: Five-column hierarchical table
The table SHALL contain exactly “Tên danh mục”, “Danh mục cha”, “Số sản phẩm”, “Trạng thái”, and “Thao tác”, with respective minimum widths 320, 160, 130, 120, and 150 display units. Counts and status SHALL be centered, actions right-aligned, and Slug SHALL NOT be a column.

#### Scenario: Render parent and child rows
- **WHEN** an expanded category branch is displayed
- **THEN** its root row shows a chevron when expandable, a 36-by-36 tinted icon box, a semibold name, and “Danh mục gốc” in the parent column
- **AND** child rows start at 48 units of left padding with a muted “↳”, show their direct parent name, and deepen indentation for further levels
- **AND** header and body columns remain aligned when the table scrolls horizontally below its minimum width

#### Scenario: Render category status without mutation
- **WHEN** a category is active or hidden
- **THEN** its read-only indicator displays “Bật” or “Đã ẩn” respectively, with hidden-row styling from the design
- **AND** interacting with the indicator does not modify local category state or issue a write request

### Requirement: Authenticated category loading
The workspace SHALL load hierarchy data through the category HTTP API using the current session's Bearer token. It SHALL preserve nested relationships, remain responsive during loading, support cancellation, and expose loading, empty, and failure states without accessing a database directly.

#### Scenario: Load the tree successfully
- **WHEN** a session token is available and tree loading succeeds
- **THEN** the displayed hierarchy and category total reflect the returned data, including hidden nodes if supplied by the management data source
- **AND** repeated loading does not duplicate rows

#### Scenario: Missing session or denied API access
- **WHEN** no token is available or the API returns HTTP 401 or 403
- **THEN** the workspace shows an authentication or permission state and does not present fixture data as a successful load

#### Scenario: Loading fails or is cancelled
- **WHEN** the API fails or the user leaves while loading
- **THEN** failures show a recoverable error with a retry action, cancellation is not shown as a server failure, and a cancelled or older load cannot overwrite a later successful result

### Requirement: Immediate hierarchical filtering
The workspace SHALL filter locally on each search-text or status selection change without requiring submission or another API request. Search SHALL match category names case-insensitively after trimming. Status options SHALL be “Trạng thái: Tất cả”, “Đang kích hoạt”, and “Đã ẩn”, combined with search using AND.

#### Scenario: Find a nested category
- **WHEN** a descendant matches the search and selected status
- **THEN** that descendant and its ancestor path are visible, even if its ancestors do not match
- **AND** unrelated nonmatching rows are excluded, ancestor-only rows are context rather than matches, and required ancestor paths open automatically

#### Scenario: Clear a filter or receive no matches
- **WHEN** filters are cleared or produce no matches
- **THEN** clearing restores the user's unfiltered expansion state, while zero matches show an empty-results message and a “0 - 0” footer range
- **AND** each filter change resets the page to the first page

### Requirement: Branch expansion
The workspace SHALL expand and collapse descendants using the branch chevron without fetching data again or changing category totals. Branch state SHALL be independent by category identity, and leaves SHALL NOT offer an active expansion action.

#### Scenario: Toggle one branch
- **WHEN** the user collapses and reopens a branch outside a filter-forced ancestor path
- **THEN** descendants disappear and reappear in hierarchy order while sibling branches retain their state
- **AND** keyboard activation provides the same result, with no blocking work or full-page navigation

#### Scenario: Filter requires an ancestor to stay visible
- **WHEN** a user attempts to collapse a filter-forced ancestor path
- **THEN** the matched descendant remains visible and the path's expansion control indicates that it is forced open until the filter is cleared

### Requirement: Derived category icons
Category icons and badge tints SHALL derive from category name or slug, with smartphone/blue, laptop/indigo, tablet/teal, accessory or audio/emerald, and neutral fallback presentations. Icon presentation SHALL NOT require database fields or migrations.

#### Scenario: Resolve known and unknown categories
- **WHEN** a name or slug identifies a known device family or an unfamiliar category
- **THEN** the matching family presentation or a neutral category fallback is rendered, without exposing icon ligature text to users

### Requirement: Data-derived counts
The workspace SHALL compute category totals from loaded hierarchy data and display product/SKU counts only when provided by a trustworthy API-backed source. Unavailable counts SHALL display an unavailable marker, never a fabricated zero or an HTML sample value.

#### Scenario: Display available product and SKU totals
- **WHEN** authoritative counts exist for the displayed categories
- **THEN** root rows display “{N} sản phẩm” and child rows display “{N} SKU” using the source's defined aggregation rules
- **AND** category totals count unique category identities rather than products, SKUs, or expanded rows

#### Scenario: Existing API lacks management fields
- **WHEN** the source supplies only active categories and no product/SKU counts
- **THEN** filtering operates over the returned tree, missing counts display “—”, and the UI does not claim that unreturned hidden categories are absent from the database
- **AND** complete hidden-category and numeric-count acceptance remains dependent on a management data contract

### Requirement: Hierarchy-aware pagination
The footer SHALL display “Hiển thị A - B trên tổng số Y danh mục” with previous, numbered-page, and next controls derived from the current result. Pages SHALL hold up to 15 matching categories, excluding repeated ancestor context, while keeping each matching row's ancestor path visible.

#### Scenario: Navigate and change the visible hierarchy
- **WHEN** more than 15 matching categories are available after applying filters and expansion
- **THEN** numbered controls switch between deterministic pages, previous is disabled on the first page, and next is disabled on the last
- **AND** ancestors repeated for context are not counted twice in the range or total
- **AND** collapse clamps an out-of-range page, and expanding a branch does not change the header's total category count

#### Scenario: Display one page or an empty result
- **WHEN** at most 15 eligible categories or no eligible categories exist
- **THEN** the footer shows only valid page numbers, an accurate range, and appropriately disabled navigation rather than hardcoded pages “1, 2”

### Requirement: Deferred category action hooks
Add controls SHALL launch the Create Category Dialog. Edit controls SHALL launch the Edit Category Dialog with the selected category identity and a complete editable snapshot. Delete controls SHALL launch the Delete Category Dialog with the selected identity and category snapshot. The category tree SHALL change only after a valid create, edit, or delete operation is confirmed by the API.

#### Scenario: Request an action
- **WHEN** the user activates Add, a row's Edit, or a row's Delete action
- **THEN** Add opens the Create Category Dialog, Edit opens the Edit Category Dialog for that row, and Delete opens the Delete Category Dialog for that row
- **AND** the category tree remains unchanged until the corresponding operation is successfully completed

### Requirement: Delete dialog target context
The delete dialog SHALL present an immutable snapshot of the selected category identity, name, hierarchy level, parent context, derived icon, and available child/product counts.

#### Scenario: Open deletion confirmation
- **WHEN** the user activates Delete for a root or descendant row
- **THEN** the dialog shows that category's name and the correct root or child metadata
- **AND** opening the dialog does not change the tree or issue a delete request

### Requirement: Explicit safe-delete confirmation
The delete flow SHALL require an explicit destructive confirmation and SHALL provide a cancel or close action that performs no delete request.

#### Scenario: Cancel deletion
- **WHEN** the user clicks “Hủy bỏ”, closes the modal, or presses Escape
- **THEN** the dialog closes without calling the delete API
- **AND** the category tree remains unchanged

#### Scenario: Confirm deletion once
- **WHEN** the user activates “Xác nhận xóa” for a valid target
- **THEN** the client sends exactly one delete request for that target while the operation is busy
- **AND** repeated activation while busy cannot submit another request

### Requirement: Delete request and business failures
The client SHALL send the delete request with the current Bearer session and SHALL keep the dialog open with a friendly error for HTTP 400, 409, authorization, not-found, or transport failures.

#### Scenario: Category has child categories
- **WHEN** the delete API returns HTTP 400 for a child-category constraint
- **THEN** the dialog explains that child categories must be moved or deleted first
- **AND** the target context and any entered state remain intact

#### Scenario: Category has linked products
- **WHEN** the delete API returns HTTP 409 for a linked-product constraint
- **THEN** the dialog explains that linked inventory products prevent deletion
- **AND** the user can cancel or correct the data without losing the context

#### Scenario: Other delete failure
- **WHEN** the API returns an authorization, not-found, or transport failure
- **THEN** the dialog displays a recoverable error and remains open without mutating the tree

### Requirement: Successful deletion refreshes the workspace
After an HTTP 204 delete response, the workspace SHALL close the dialog and request exactly one fresh tree load using the current session. The refreshed hierarchy SHALL be authoritative.

#### Scenario: Delete an empty category
- **WHEN** the backend returns HTTP 204
- **THEN** the dialog closes and the workspace performs one tree refresh
- **AND** the deleted category is absent from the refreshed hierarchy

#### Scenario: Refresh fails after deletion
- **WHEN** deletion returns HTTP 204 but the subsequent tree load fails
- **THEN** the workspace reports a recoverable refresh error without resubmitting the delete request

### Requirement: Delete dialog visual and accessibility contract
The delete dialog SHALL be a C# Markup-only Fluent modal matching `docs/designs/delete-category.html`, with a 480px white rounded card, centered red warning icon, Vietnamese title and subtitle, category info card, constraint warning box, dynamic error area, and destructive footer action.

#### Scenario: Render and operate the modal
- **WHEN** the delete dialog is rendered in the supported theme
- **THEN** it presents the `delete_forever` warning treatment, category metadata, constraint bullets, “Hủy bỏ”, and “Xác nhận xóa” with busy feedback
- **AND** labels, focus states, Escape, and keyboard activation remain usable without introducing a XAML file

### Requirement: Delete workflow verification
The frontend test suite SHALL cover target initialization, cancel/close behavior, one-request busy gating, 204 refresh coordination, and friendly 400/409 error retention.

#### Scenario: Run delete workflow tests
- **WHEN** the complete frontend unit-test project is executed
- **THEN** delete ViewModel and workspace coordination tests pass for cancel, confirm, 400, 409, 204, refresh failure, and repeated-submit cases
- **AND** live-data acceptance remains dependent on the backend delete endpoint contract

### Requirement: Edit category draft initialization
The edit flow SHALL initialize a draft from the selected category's current name, description, parent, active state, and slug without mutating the workspace snapshot.

#### Scenario: Open an existing category
- **WHEN** the user activates Edit for a root or descendant row
- **THEN** the dialog shows the matching values, the correct root/child badge, and a context card derived from the current hierarchy
- **AND** cancelling or dismissing the dialog leaves the tree and the selected snapshot unchanged

### Requirement: Safe parent candidates
The edit flow SHALL offer the null-root option first and SHALL exclude the category being edited and every descendant at any depth from parent candidates, including inactive descendants present in the available hierarchy snapshot.

#### Scenario: Prevent a cycle
- **WHEN** the user opens the parent picker for a category with nested descendants
- **THEN** the picker contains “-- Danh mục gốc (Không có danh mục cha) --” followed only by valid candidates
- **AND** the edited category and all of its descendants cannot be selected as the new parent

#### Scenario: Select a valid parent
- **WHEN** the user chooses a different valid parent or the root option
- **THEN** the draft reflects that choice and the context badge, breadcrumb, and explanatory card update without changing the workspace tree

### Requirement: Edit form validation and request contract
The dialog SHALL require a trimmed category name of at most 200 characters and SHALL send only `Name`, `Slug`, `ParentId`, `Description`, and `IsActive` in an update request. An unchanged slug SHALL be preserved when available; a missing slug MAY be sent as null to request server regeneration.

#### Scenario: Reject invalid input locally
- **WHEN** the name is empty or exceeds the allowed length
- **THEN** the dialog remains open, does not call the API, and displays a clear validation message while preserving the draft

#### Scenario: Save an edited category
- **WHEN** the user submits a valid draft
- **THEN** the client sends one update request for the selected identity with the exact supported fields, including explicit parent and active-state values
- **AND** description clearing is represented as an empty value rather than silently discarding the user's intent

### Requirement: Edit failure preserves the draft
The edit dialog SHALL keep all user-entered values and remain open when the update API returns HTTP 400, 404, or 409, with an actionable error message.

#### Scenario: Backend rejects the update
- **WHEN** the API returns a validation, missing-category, or conflict response
- **THEN** the dialog displays the normalized error, keeps the name, parent, description, and active-state draft, and allows correction or cancellation

### Requirement: Successful edit refreshes the workspace
After an HTTP 200 update response, the edit flow SHALL close the dialog and request exactly one fresh category-tree load using the current session. The workspace SHALL use the refreshed hierarchy as authoritative.

#### Scenario: Commit an update
- **WHEN** the backend returns HTTP 200 with the updated category DTO
- **THEN** the dialog closes and the workspace refreshes once, reflecting the new name, hierarchy, description-derived state, and active state when present
- **AND** the client does not optimistically fabricate counts or retry the write

#### Scenario: Refresh fails after commit
- **WHEN** the update succeeds but the subsequent tree load fails
- **THEN** the workspace reports a recoverable refresh error without resubmitting the update

### Requirement: Edit dialog visual and accessibility contract
The edit dialog SHALL be a C# Markup-only Fluent modal matching the edit-category design: compact 480px maximum width, 12px rounded white card, edit icon header, root/child badge, required name field, parent picker and hint, context card, description field, active toggle, cancel action, and primary save action with busy feedback.

#### Scenario: Render and operate the modal
- **WHEN** the dialog is shown in light or dark theme
- **THEN** its labels, focus states, spacing, colors, and controls remain readable and no XAML file is introduced
- **AND** Escape/cancel dismisses without saving, Enter activates the designated save action when valid, and focus returns to the invoking row or current page content

### Requirement: Edit workflow verification
The frontend test suite SHALL cover draft initialization, root and deep descendant anti-cycle filtering, request shape, validation, error retention, cancellation, single-save busy behavior, successful refresh coordination, and failed-refresh behavior.

#### Scenario: Run edit workflow tests
- **WHEN** the complete frontend unit-test project is executed
- **THEN** the edit ViewModel and workspace coordination tests pass for root, child, inactive, deep-tree, invalid-input, 400/404/409, cancellation, and 200-refresh cases
- **AND** live-data acceptance is reported separately until the backend update endpoint exists

### Requirement: Workspace verification
Delivery SHALL include a successful `net10.0-desktop` build with zero errors and zero warnings, a passing complete headless frontend unit-test suite, and visual comparison against the local HTML. Build and test results SHALL be reported separately from live API integration prerequisites.

#### Scenario: Verify the completed workspace
- **WHEN** implementation is submitted for acceptance
- **THEN** verification covers combined filtering, deep trees, expansion state, pagination boundaries, authentication failures, cancellation, icon fallback, and action identity
- **AND** visual review checks the five columns, spacing, font hierarchy, hover/focus states, and narrow-window scrolling against the design without adding XAML files
- **AND** live hidden-category/count acceptance is not declared complete solely because fixture tests pass

### Requirement: Create Category Dialog interaction
The Create Category Dialog SHALL validate category creation inputs, automatically generate URL-safe slugs from Vietnamese names in real time, submit valid data to the category API, and refresh the workspace tree upon successful creation.

#### Scenario: Real-time Vietnamese slug generation
- **WHEN** the user types or modifies the category name (e.g., "Tai nghe & Âm thanh")
- **THEN** the dialog displays an auto-generated lowercase, unaccented, hyphen-separated slug in the preview badge (e.g., "tai-nghe-am-thanh") in real time

#### Scenario: Parent category hierarchy selection
- **WHEN** the user opens the parent category dropdown
- **THEN** the options include "-- Danh mục gốc (Cấp 1) --" (value null) and all existing categories with hierarchy indication
- **AND** selecting a parent links the new category under that parent upon creation

#### Scenario: Successful category creation without keep-creating
- **WHEN** the user submits valid category data and "Tiếp tục thêm danh mục khác sau khi lưu" is unchecked
- **THEN** the system sends a CreateCategoryRequest containing only Name, Slug, ParentId, Description, and IsActive to the backend
- **AND** upon HTTP 201 response, the dialog closes and the category workspace tree automatically refreshes to display the new category

#### Scenario: Successful category creation with keep-creating
- **WHEN** the user submits valid category data and "Tiếp tục thêm danh mục khác sau khi lưu" is checked
- **THEN** upon HTTP 201 response, the category workspace tree refreshes in the background, the form fields reset to clean defaults, and the dialog remains open for subsequent entries

#### Scenario: Backend validation or conflict failure
- **WHEN** the backend returns an error (such as HTTP 409 Conflict for duplicate slug or HTTP 400 Bad Request)
- **THEN** the dialog remains open, existing user input is preserved without loss, and an informative error message is displayed

### Requirement: Create Category Dialog visual structure
The Create Category Dialog SHALL be implemented entirely in C# Markup without XAML, styled as a 640px Fluent modal with rounded corners, custom header, system tip box, and active POS status switch.

#### Scenario: Modal visual elements presentation
- **WHEN** the Create Category Dialog is rendered
- **THEN** it displays a 12px rounded container with maximum width 640px, a header with folder icon, title, subtitle, close button, required name field with slug badge, parent combobox with hint text, multi-line description field, POS active toggle switch, system tip box with info icon, keep-creating checkbox, cancel button, and primary add button
- **AND** no `.xaml` file is introduced for the dialog

### Requirement: Workspace follows the app theme
The "Danh mục và Kho" workspace (Danh mục, Tồn kho and Serial/IMEI tabs) and every dialog it opens SHALL follow the app's Light/Dark theme setting, including a change made while the page is open. Text SHALL keep at least 4.5:1 contrast against its background in both themes, and status meaning SHALL also be given in words.

#### Scenario: Switch theme while the workspace is open
- **WHEN** the user opens the workspace, then switches Settings from Sáng to Tối and back
- **THEN** page background, table rows, badges, buttons, inputs and icons change with the theme and all text stays readable, without reopening the page

#### Scenario: Dialog in dark theme
- **WHEN** the app is in Tối and the user opens the create, edit or delete category dialog, or the import, adjust, detail or delete stock dialog
- **THEN** the dialog uses dark surfaces and light text, with readable labels, field errors and focus states

#### Scenario: Danger text contrast
- **WHEN** a destructive warning or field error is shown on a light surface
- **THEN** its text color has at least 4.5:1 contrast with that surface

### Requirement: Category row actions open from one menu
Each category row SHALL offer Edit and Delete through one “•••” button, labeled for assistive technology as “Thao tác cho <category name>”, that opens a menu with “Sửa danh mục” and “Xóa danh mục”, the latter in the danger color. The expand chevron, the status switch and the row hover background SHALL stay as they are.

#### Scenario: Edit or delete from the row menu
- **WHEN** the user opens the “•••” menu of the category row “Accessories” and picks “Sửa danh mục” or “Xóa danh mục”
- **THEN** the Edit Category Dialog or the Delete Category Dialog opens for “Accessories”, and the tree is unchanged until that operation succeeds

#### Scenario: Keyboard access
- **WHEN** the user moves focus to a row's “•••” button with Tab and presses Enter
- **THEN** the menu opens, the arrow keys move between its items, and Esc closes it without any action
