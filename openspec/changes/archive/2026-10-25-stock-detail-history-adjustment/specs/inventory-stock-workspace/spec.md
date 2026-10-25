# Spec Delta

## MODIFIED Requirements

### Requirement: Deferred stock action hooks
The workspace SHALL expose actions for “Nhập kho mới”, viewing Serial/IMEI, adjusting stock, and deleting a stock row. Viewing Serial/IMEI SHALL open the stock detail/history dialog, and adjusting stock SHALL open the adjustment dialog; delete SHALL continue to mutate inventory only after a supported delete operation succeeds.

#### Scenario: Open stock detail
- **WHEN** the user activates the visibility action for a stock row
- **THEN** the client opens the detail/history dialog with that row's immutable identity and display context

#### Scenario: Activate a deferred action
- **WHEN** the user activates an inventory action that remains outside this workflow, such as “Nhập kho mới”
- **THEN** the client invokes the registered hook or reports the action as unavailable and issues no stock mutation request

#### Scenario: Open stock adjustment
- **WHEN** the user activates the edit action for a stock row
- **THEN** the client opens the adjustment dialog with current quantity and reserved quantity

#### Scenario: Complete supported adjustment
- **WHEN** the user confirms a valid adjustment and the server succeeds
- **THEN** the client closes the dialog, refreshes the inventory list exactly once, and raises the success notification

#### Scenario: Request stock deletion
- **WHEN** the user activates the delete action for a stock row
- **THEN** the client opens the delete-stock confirmation dialog with that row's immutable identity and display context
- **AND** the table remains unchanged while the dialog is open or the delete operation is unavailable

#### Scenario: Complete supported deletion
- **WHEN** the user confirms a supported delete operation and the server succeeds
- **THEN** the client closes the dialog, refreshes the inventory list exactly once, and raises the success notification

## ADDED Requirements

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
