# client-inventory-stock-adjustment Specification

## Purpose

Lets staff submit a reasoned stock correction for a regular product or variant while protecting serial-tracked inventory from direct quantity edits and distinguishing preview behavior from a real server mutation.

## Requirements

### Requirement: Adjustment applies only to the selected non-serial balance
The client SHALL open an adjustment interaction for the exact product and optional variant selected from the stock list and SHALL expose quantity changes only when repository data identifies that balance as non-serial-tracked.

#### Scenario: Open a regular stock balance
- **WHEN** a staff member opens adjustment for a non-serial-tracked product or variant
- **THEN** the interaction identifies that same balance and shows its current physical and reserved quantities

#### Scenario: Block a serial-tracked balance
- **WHEN** a staff member opens adjustment for a serial-tracked product or variant
- **THEN** the interaction shows the D05 explanation, provides no quantity input or submit action, and offers navigation to the existing Serial/IMEI destination with the selected context

### Requirement: A regular stock adjustment has a valid direction, amount, and reason
The client SHALL require one increase/decrease direction, a positive whole-number quantity, and a non-empty reason before enabling submission.

#### Scenario: Invalid adjustment draft
- **WHEN** the direction is unset, the amount is missing, non-integer, zero or negative, or the reason is blank
- **THEN** submission is disabled or validation is shown, and the draft values remain available for correction

#### Scenario: Show projected physical quantity
- **WHEN** a valid draft amount or direction changes
- **THEN** the dialog shows an explicitly estimated resulting physical quantity based on the current repository balance and does not change the displayed balance or history before repository success

### Requirement: A confirmed adjustment is reported only after repository success
The client SHALL show a pending state during submission, prevent duplicate submissions, and report success only after the repository returns a successful result.

#### Scenario: Adjustment succeeds
- **WHEN** the repository confirms an adjustment
- **THEN** the dialog reports success, the inventory-change notification is published for the selected balance, and the stock list reloads repository data rather than applying a local quantity delta

#### Scenario: Cancel an adjustment
- **WHEN** the user closes or cancels before repository success
- **THEN** no inventory-change notification or success state is produced

### Requirement: A stock conflict preserves the adjustment draft and allows balance reload
The client SHALL treat a 409 or insufficient-stock response as a conflict, keep the adjustment open, preserve direction, quantity, and reason, and offer an explicit balance reload without automatically resubmitting.

#### Scenario: Conflict after another stock change
- **WHEN** the repository reports a 409 conflict
- **THEN** the dialog explains that the stock changed, retains the draft, and does not publish a success notification

#### Scenario: Reload after conflict
- **WHEN** the user requests the current balance after a conflict
- **THEN** the client reloads the selected balance from the repository while preserving the direction, quantity, and reason draft

### Requirement: Other adjustment failures preserve the draft without claiming success
The client SHALL show a recoverable failure state for non-conflict repository errors and SHALL retain the user's input.

#### Scenario: Repository error
- **WHEN** an adjustment request fails for a reason other than a stock conflict
- **THEN** the dialog reports failure, keeps the draft, and does not change balance, movement history, or stock-list state

### Requirement: Preview adjustments are clearly identified as non-persistent
The client SHALL label preview adjustment data and responses as sample behavior and SHALL NOT represent them as persisted stock, a real authorization decision, or a server-written movement.

#### Scenario: Preview success
- **WHEN** the fake inventory repository reports a successful adjustment
- **THEN** the UI identifies the interaction as Preview and presents the updated fixture only as mock repository data

### Requirement: The adjustment interaction is usable by keyboard and at usable desktop widths
The client SHALL keep adjustment fields, validation, conflict recovery, and available actions operable at the user's normal and narrower usable desktop widths.

#### Scenario: Keyboard adjustment
- **WHEN** the user operates the adjustment interaction by keyboard
- **THEN** focus remains visible, Tab order is sensible, Enter or Space activates enabled actions, Escape cancels, and focus returns to the invoking stock-row action

#### Scenario: Narrow usable window
- **WHEN** the owner narrows the window while the adjustment interaction is open
- **THEN** content scrolls or reflows without clipping the balance summary, inputs, validation, or actions
