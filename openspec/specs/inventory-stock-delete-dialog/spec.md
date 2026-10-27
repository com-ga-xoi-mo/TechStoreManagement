# inventory-stock-delete-dialog Specification

## Purpose

Provide a safe, accessible confirmation flow for removing an inventory stock item while preserving the selected product context, protecting related Serial/IMEI data, and keeping the inventory workspace authoritative after a successful operation.

## Requirements

### Requirement: Stock deletion target context
The dialog SHALL receive an immutable snapshot containing stock identity, product identity, display name, SKU, and an authoritative related Serial/IMEI count when one is available.

#### Scenario: Open for a stock row
- **WHEN** the user activates the delete action for a visible stock row
- **THEN** the dialog shows the product name, SKU, and related Serial/IMEI count from that row or its trusted API-backed metadata
- **AND** opening the dialog does not remove or mutate the row

#### Scenario: Missing count metadata
- **WHEN** the current API contract cannot provide a related Serial/IMEI count
- **THEN** the dialog displays an unavailable marker or safe explanatory text and never invents a count

### Requirement: Delete stock dialog visual contract
The dialog SHALL be a C# Markup-only modal matching `docs/designs/delete-stock-item.html`: a centered 460px maximum-width white card with 16px corners, danger icon, Vietnamese title/subtitle, stock context card, warning copy, two-column footer, and accessible close action.

#### Scenario: Render the confirmation modal
- **WHEN** the modal is shown in the supported desktop theme
- **THEN** it displays `delete_forever`, “Xác nhận xóa sản phẩm”, the irreversible-action subtitle, product/SKU context, the Serial/IMEI warning, “Hủy bỏ”, and “Xác nhận xóa”
- **AND** no XAML file is introduced

### Requirement: Cancel and busy safety
The dialog SHALL close without a delete request when cancelled or closed, and SHALL prevent repeated confirmation while a delete operation is busy.

#### Scenario: Cancel or close
- **WHEN** the user clicks “Hủy bỏ”, the close button, or Escape
- **THEN** the dialog closes and no destructive API request is sent

#### Scenario: Confirm while busy
- **WHEN** a delete operation is in progress
- **THEN** confirm and cancel interactions are disabled or gated, a progress indicator is shown, and a second delete request cannot be submitted

### Requirement: Supported deletion and workspace refresh
When a supported authorized delete operation succeeds, the dialog SHALL close, the inventory workspace SHALL refresh exactly once, the removed row SHALL disappear from authoritative results, and a green success notification SHALL be raised.

#### Scenario: Delete succeeds
- **WHEN** the configured delete seam returns success
- **THEN** the dialog closes, one inventory reload is requested, and the success notification says “Đã xóa sản phẩm thành công khỏi danh mục tồn kho.”

#### Scenario: Refresh fails after deletion
- **WHEN** deletion succeeds but the subsequent inventory reload fails
- **THEN** the client reports a recoverable refresh error without submitting the delete again

### Requirement: Safe deferred deletion without backend support
The client SHALL not call an invented or unsupported destructive route. When no supported delete endpoint is configured, confirmation SHALL remain safe/deferred and report that server support is unavailable.

#### Scenario: Endpoint unavailable
- **WHEN** the delete seam has no supported backend operation
- **THEN** the dialog remains open with an actionable unavailable message, preserves its target context, and sends no destructive request

### Requirement: Delete failures preserve context
The dialog SHALL keep its target and remain open for cancellation or correction when deletion returns a normalized authorization, validation, conflict, not-found, transport, timeout, or cancellation outcome.

#### Scenario: Server or transport failure
- **WHEN** the delete operation fails with HTTP 400, 401, 403, 404, 409, a transport error, or a timeout
- **THEN** a friendly error is displayed, the product/SKU/count context is preserved, and the inventory row is not optimistically removed

#### Scenario: Caller cancellation
- **WHEN** the caller cancels the delete request
- **THEN** cancellation remains distinguishable from a successful user cancel, the dialog does not report success, and no refresh is requested

### Requirement: Delete dialog verification
The frontend unit-test suite SHALL cover target initialization, unavailable count/endpoint behavior, cancel/close, busy gating, success refresh/toast coordination, cancellation, and normalized failure retention.

#### Scenario: Run dialog tests
- **WHEN** the headless frontend unit-test project is executed
- **THEN** delete dialog ViewModel and inventory workspace coordination tests pass without requiring a live database
