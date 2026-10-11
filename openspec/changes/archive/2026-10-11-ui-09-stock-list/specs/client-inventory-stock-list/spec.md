# Spec Delta

## Purpose

Lets staff inspect product stock balances in the inventory workspace while clearly separating physical, reserved, and available quantities and identifying preview data.

## ADDED Requirements

### Requirement: Stock tab presents read-only balance rows
The client SHALL show a read-only stock table with one row per product and optional variant balance.

#### Scenario: View stock balances
- **WHEN** the user opens Tồn kho
- **THEN** each row identifies the product, optional variant, SKU, category, selling price, and stock status without mutation or future-dialog actions

### Requirement: Stock quantities remain distinct
The client SHALL display physical, reserved, and available quantities as separate values and SHALL calculate available as physical quantity minus reserved quantity for preview rows.

#### Scenario: Compare balance quantities
- **WHEN** a stock row is displayed
- **THEN** its physical quantity, reserved quantity, and available quantity are visibly distinct, and available equals physical minus reserved

#### Scenario: Preview status follows preview balance
- **WHEN** a preview row is displayed with a low-stock status
- **THEN** that status is consistent with the preview row's available quantity and configured preview alert threshold

### Requirement: Stock results support combined filters and paging
The client SHALL combine product-name or SKU search, category filtering, and stock-status filtering and SHALL page results with eight rows per page.

#### Scenario: Combine search and filters
- **WHEN** the user enters a search and selects category and status filters
- **THEN** the displayed rows satisfy all active criteria together

#### Scenario: Change criteria while on a later page
- **WHEN** the user changes search, category, or status while viewing a later page
- **THEN** the result set is refreshed from its first page

#### Scenario: Navigate stock pages
- **WHEN** more than eight rows match the active criteria
- **THEN** the client shows eight rows per page and exposes the current page and available page navigation

### Requirement: Stock query feedback and retries are explicit
The client SHALL distinguish loading, populated, empty, and failed stock queries and provide a retry action after failure.

#### Scenario: Load stock rows
- **WHEN** a stock query is pending
- **THEN** the table shows loading feedback rather than presenting incomplete rows as a completed result

#### Scenario: No stock rows match
- **WHEN** a stock query succeeds with no matching rows
- **THEN** the client shows a readable empty result without sample rows or a loading indicator

#### Scenario: Stock query fails
- **WHEN** a stock query fails
- **THEN** the client shows an error state distinct from empty and exposes retry

#### Scenario: Retry after stock query failure
- **WHEN** the user retries a failed stock query
- **THEN** the client requests the current criteria again and replaces the error only after a successful result

### Requirement: Latest stock query wins
The client SHALL prevent a result from an obsolete search or filter request from replacing results for the latest criteria.

#### Scenario: Earlier request completes last
- **WHEN** an earlier stock request completes after a newer request for changed criteria
- **THEN** only the result matching the latest criteria is displayed

### Requirement: Preview stock is clearly identified
The client SHALL label fake stock results as preview data and SHALL NOT present them as live inventory or server-authorized data.

#### Scenario: Display preview stock
- **WHEN** rows come from the fake inventory source
- **THEN** the stock workspace visibly identifies them as preview/sample data and does not imply that stock changes are persisted

### Requirement: Stock tab remains usable by keyboard at usable desktop widths
The client SHALL keep the stock tab, filters, table, paging controls, and feedback readable and operable by keyboard at the user's normal and narrower usable desktop widths.

#### Scenario: Narrow usable window
- **WHEN** the owner narrows the application window while viewing stock
- **THEN** the table and controls reflow or scroll without horizontally clipping the quantity distinctions or preventing filter and page actions

#### Scenario: Keyboard stock operation
- **WHEN** the user operates stock filters, retry, and paging by keyboard
- **THEN** controls have a sensible Tab order, Enter or Space activates available actions, and focus remains visible
