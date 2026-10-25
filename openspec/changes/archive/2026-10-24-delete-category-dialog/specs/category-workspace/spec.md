# Spec Delta

## MODIFIED Requirements

### Requirement: Deferred category action hooks
Add controls SHALL launch the Create Category Dialog. Edit controls SHALL launch the Edit Category Dialog with the selected category identity and a complete editable snapshot. Delete controls SHALL launch the Delete Category Dialog with the selected identity and category snapshot. The category tree SHALL change only after a valid create, edit, or delete operation is confirmed by the API.

#### Scenario: Request an action
- **WHEN** the user activates Add, a row's Edit, or a row's Delete action
- **THEN** Add opens the Create Category Dialog, Edit opens the Edit Category Dialog for that row, and Delete opens the Delete Category Dialog for that row
- **AND** the category tree remains unchanged until the corresponding operation is successfully completed

## ADDED Requirements

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
