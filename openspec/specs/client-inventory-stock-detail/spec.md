# client-inventory-stock-detail Specification

## Purpose

Lets staff inspect a selected product or variant's current inventory balance and recent repository-provided movements without changing stock or presenting preview history as live records.

## Requirements

### Requirement: Stock detail identifies the selected balance and keeps quantities distinct
The client SHALL show read-only details for the exact product and optional variant selected from the stock list, including physical, reserved, and available quantities.

#### Scenario: Open a product balance
- **WHEN** the user opens a stock detail for a product or variant
- **THEN** the dialog identifies that same product/variant and shows its physical, reserved, and available quantities separately

#### Scenario: Available quantity is consistent
- **WHEN** the detail balance is displayed
- **THEN** the available quantity is consistent with the repository result and is not recomputed from movement history

### Requirement: Recent movements come from the inventory repository
The client SHALL display up to five recent movements in repository-provided newest-first order, including movement time, type, signed quantity change, physical quantity after the movement, and note when supplied.

#### Scenario: Display recent movements
- **WHEN** the repository returns recent movements for the selected balance
- **THEN** the dialog displays those movements in the returned order with their supplied values

#### Scenario: No movement history is available
- **WHEN** the repository returns no movements
- **THEN** the dialog shows an explicit empty-history state and does not synthesize events from the current balance

#### Scenario: Movement query fails
- **WHEN** movement history cannot be loaded
- **THEN** the dialog reports the failure separately from an empty history and offers retry without inventing rows

### Requirement: Stock detail query feedback and retry are explicit
The client SHALL distinguish pending, populated, empty, and failed detail queries and allow the user to retry a failed query.

#### Scenario: Load stock detail
- **WHEN** a detail query is pending
- **THEN** the dialog presents loading feedback and does not present incomplete balance or movement data as a completed result

#### Scenario: Detail query fails
- **WHEN** the detail repository reports an error or unavailable source
- **THEN** the dialog shows a clear error state and retry action, and identifies unavailable real inventory when applicable

#### Scenario: Retry current detail
- **WHEN** the user retries
- **THEN** the client requests the same product and optional variant again and replaces the error only after a successful response

### Requirement: Stock detail refreshes from the repository after inventory changes
The client SHALL reload the selected balance and recent movements from the repository when the user refreshes the detail and after a stock-changing workflow reports success; it SHALL NOT update quantities by locally applying a movement.

#### Scenario: Manually refresh detail
- **WHEN** the user requests a refresh
- **THEN** the dialog reloads balance and recent movements for the same product and optional variant and updates its last-refreshed time only after success

#### Scenario: A stock change succeeds
- **WHEN** an inventory workflow reports that a stock change succeeded while the detail is open
- **THEN** the detail re-queries the repository and the stock list is marked for refresh without locally changing either balance

### Requirement: Serial and IMEI navigation preserves selected product context
The client SHALL provide a navigation action from stock detail to the existing Serial/IMEI destination with the selected product and optional variant context.

#### Scenario: Navigate to Serial/IMEI for the selected balance
- **WHEN** the user follows the Serial/IMEI action
- **THEN** the existing destination receives the selected product and optional variant context

#### Scenario: Serial/IMEI destination is unfinished
- **WHEN** the destination does not yet implement Serial/IMEI records or filters
- **THEN** it remains explicitly marked as not implemented and shows no fabricated records or claim that the filter was applied

### Requirement: Preview detail data is clearly identified
The client SHALL identify synthetic balance and movement data as preview/sample data and SHALL NOT present it as live inventory or server-authorized history.

#### Scenario: Display preview detail
- **WHEN** detail data comes from a fake repository
- **THEN** the dialog visibly labels both balance and movements as preview/sample data

### Requirement: Stock detail dialog remains usable by keyboard and at usable desktop widths
The client SHALL keep the detail summary, movement history, refresh, navigation, retry, and close actions readable and operable at the user's normal and narrower usable desktop widths.

#### Scenario: Narrow usable window
- **WHEN** the owner narrows the application window while the dialog is open
- **THEN** its content reflows or scrolls without clipping the three quantities, movement values, or available actions

#### Scenario: Keyboard dialog operation
- **WHEN** the user opens, operates, refreshes, retries, navigates from, and closes the dialog by keyboard
- **THEN** focus is visible, controls have a sensible Tab order, Enter or Space activates actions, Escape closes the dialog, and focus returns to its opener when the dialog closes
