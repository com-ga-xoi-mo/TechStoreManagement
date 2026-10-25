# Spec Delta

## MODIFIED Requirements

### Requirement: Deferred stock action hooks
The workspace SHALL expose actions for “Nhập kho mới”, viewing Serial/IMEI, adjusting stock, and deleting a stock row. Delete SHALL open the safe confirmation dialog for the selected row and SHALL mutate inventory only after a supported delete operation succeeds; unsupported destructive operations remain visibly deferred.

#### Scenario: Activate a deferred action
- **WHEN** the user activates an inventory action whose detail workflow is not implemented
- **THEN** the client invokes the registered hook or reports the action as unavailable and issues no stock mutation request

#### Scenario: Request stock deletion
- **WHEN** the user activates the delete action for a stock row
- **THEN** the client opens the delete-stock confirmation dialog with that row's immutable identity and display context
- **AND** the table remains unchanged while the dialog is open or the delete operation is unavailable

#### Scenario: Complete supported deletion
- **WHEN** the user confirms a supported delete operation and the server succeeds
- **THEN** the client closes the dialog, refreshes the inventory list exactly once, and raises the success notification
