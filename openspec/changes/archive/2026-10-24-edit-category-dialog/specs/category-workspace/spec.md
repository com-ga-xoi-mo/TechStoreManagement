# Spec Delta

## MODIFIED Requirements

### Requirement: Deferred category action hooks
Add controls SHALL launch the Create Category Dialog. Edit controls SHALL launch the Edit Category Dialog with the selected category identity and a complete editable snapshot. Delete controls SHALL continue to expose a dialog-opening request with the selected identity. The category tree SHALL change only after a valid create or edit operation is confirmed by the API.

#### Scenario: Request an action
- **WHEN** the user activates Add, a row's Edit, or a row's Delete action
- **THEN** Add opens the Create Category Dialog, Edit opens the Edit Category Dialog for that row, and Delete invokes its existing hook with the target identity
- **AND** the category tree remains unchanged until the corresponding operation is successfully completed

## ADDED Requirements

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
