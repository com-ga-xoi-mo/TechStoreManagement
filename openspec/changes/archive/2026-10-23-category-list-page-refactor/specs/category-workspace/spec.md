# Spec Delta

## Purpose

Provide a desktop category workspace for browsing, searching, filtering, and navigating a category hierarchy using authenticated API data and a consistent Vietnamese dashboard presentation.

## ADDED Requirements

### Requirement: Category workspace layout
The workspace SHALL present the header, underline tabs, filter toolbar, tree table, and footer in the order and visual structure of `docs/designs/category-list.html`. Explicit numeric and text values in the change request SHALL take precedence where they differ from the HTML. Category feature UI SHALL introduce no XAML files.

#### Scenario: Open the category workspace
- **WHEN** the user opens the category workspace
- **THEN** it shows “Danh mục và Kho”, a data-bound category total, and “Thêm danh mục” with an add icon
- **AND** “Danh mục” is active with a blue underline and count badge, while “Tồn kho” and “Serial / IMEI” retain inactive styling
- **AND** inventory/serial navigation and their feature content are not implemented in this step

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
Add, Edit, and Delete controls SHALL expose dialog-opening requests, with the selected category identity for Edit/Delete. This step SHALL NOT implement detailed dialog forms, validate submissions, delete categories, or persist category changes.

#### Scenario: Request an action
- **WHEN** the user activates Add, a row's Edit, or a row's Delete action
- **THEN** the corresponding hook is invoked once, with the correct category identity when applicable
- **AND** the tree remains unchanged and no mutation HTTP request is issued

### Requirement: Workspace verification
Delivery SHALL include a successful `net10.0-desktop` build with zero errors and zero warnings, a passing complete headless frontend unit-test suite, and visual comparison against the local HTML. Build and test results SHALL be reported separately from live API integration prerequisites.

#### Scenario: Verify the completed workspace
- **WHEN** implementation is submitted for acceptance
- **THEN** verification covers combined filtering, deep trees, expansion state, pagination boundaries, authentication failures, cancellation, icon fallback, and action identity
- **AND** visual review checks the five columns, spacing, font hierarchy, hover/focus states, and narrow-window scrolling against the design without adding XAML files
- **AND** live hidden-category/count acceptance is not declared complete solely because fixture tests pass
