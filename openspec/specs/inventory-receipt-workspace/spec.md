# inventory-receipt-workspace Specification

## Purpose

Gives warehouse staff desktop screens to record deliveries as purchase receipts, review past receipts and keep the supplier list, inside the existing "Danh mục và Kho" workspace.

## Requirements

### Requirement: Receipt and supplier tabs
The "Danh mục và Kho" workspace SHALL add the tabs "Nhập hàng" and "Nhà cung cấp" after "Serial/IMEI", built in C# Markup without XAML and with theme-aware tokens. Tabs SHALL be reachable and operable with the keyboard, and opening a tab SHALL load its first page through the authenticated API.

#### Scenario: Open the receipts tab
- **WHEN** the user selects "Nhập hàng"
- **THEN** the tab is shown as active and one authenticated request loads the first page of receipts

#### Scenario: Readable in both themes
- **WHEN** the user switches between Sáng và Tối while either new tab or one of its dialogs is open
- **THEN** text, table, inputs and buttons stay readable and use the theme colors

### Requirement: Receipt list
The "Nhập hàng" tab SHALL show receipts newest first with columns Số phiếu, Ngày nhận (local time), Nhà cung cấp, Số chứng từ, Số mặt hàng and Tổng tiền (VND, vi-VN format), a search box, a supplier filter, pagination and distinct loading, empty, error-with-retry and content states. Each row SHALL have one "•••" menu with "Xem chi tiết".

#### Scenario: Search receipts
- **WHEN** the user types "HD-0912" and submits the search
- **THEN** the list reloads from page 1 with that search and shows the matching receipt

#### Scenario: No permission
- **WHEN** the API answers HTTP 403 for the list
- **THEN** the tab shows "Bạn không có quyền xem phiếu nhập." without rows and without a retry loop

### Requirement: Receipt detail dialog
"Xem chi tiết" SHALL open a dialog showing the receipt number, supplier, document number, receiver, received time, note, total and every line with product, variant, SKU, quantity, unit cost and line total; serial-tracked lines SHALL list their serial numbers or IMEIs with current status.

#### Scenario: View receipt detail
- **WHEN** the user opens the detail of a receipt with one IMEI line of 5 devices
- **THEN** the dialog shows the line with quantity 5 and the 5 IMEIs with their status labels in Vietnamese

### Requirement: Create receipt form
"Tạo phiếu nhập" SHALL open a form with an active-supplier picker, document number, note and one or more lines. Each line SHALL pick a product and, when it has variants, a variant, and take quantity and unit cost; serial-tracked lines SHALL also take warranty months and a Serial/IMEI box (one per line, or separated by comma, semicolon or tab) with a live count. Lines SHALL be addable and removable.

#### Scenario: Line total preview
- **WHEN** the user enters quantity 20 and unit cost 45.000 on a line
- **THEN** the line shows 900.000 ₫ and the form total updates, marked as a preview that the server recomputes

#### Scenario: Serial-tracked line
- **WHEN** the user picks a serial-tracked product on a line
- **THEN** the line shows warranty months (default 12) and the Serial/IMEI box, and hides them again when a product without serial is picked

### Requirement: Create receipt form validation
The form SHALL disable "Xác nhận nhập" while it is busy or invalid and SHALL show the first problem: no supplier, no line, a line without product, quantity below 1, missing or negative unit cost, the same product/variant on two lines, a device count different from quantity, or a duplicated code within the form. Server validation errors SHALL be shown next to the form.

#### Scenario: Device count mismatch
- **WHEN** a serial-tracked line has quantity 5 and 4 codes
- **THEN** the form shows "Số mã Serial/IMEI (4) phải bằng số lượng nhập (5)." and the confirm button is disabled

#### Scenario: Duplicate code rejected by server
- **WHEN** the server answers HTTP 409 with duplicate identifiers
- **THEN** the form stays open with every entered value and lists the duplicate codes

### Requirement: Receipt submission is safe to retry
The form SHALL generate one `clientRequestId` when it opens and reuse it for every submit attempt from that form. A network error or timeout SHALL keep the form and its values so the user can submit again; a successful submit SHALL close the form, notify the user with the receipt number and refresh the receipts list, the Tồn kho tab and the Serial/IMEI tab once each. Both “Nhập kho mới” buttons (Tồn kho and Serial/IMEI tabs) and “Tạo phiếu nhập” SHALL open this form.

#### Scenario: Retry after network failure
- **WHEN** the first submit fails with a network error and the user presses "Xác nhận nhập" again
- **THEN** the second request carries the same `clientRequestId`, and after success only one receipt exists

#### Scenario: Successful receipt
- **WHEN** the server accepts the receipt
- **THEN** the form closes, a success message shows the receipt number, and the receipts, stock and Serial/IMEI lists each reload exactly once

### Requirement: Supplier tab
The "Nhà cung cấp" tab SHALL list suppliers with name, contact, phone, email, status and receipt count, with search, an active/inactive/all filter and pagination. "Thêm nhà cung cấp" and the row menu items "Sửa" and "Ngừng sử dụng" or "Sử dụng lại" SHALL open dialogs or confirmations that call the supplier API and refresh the list once on success.

#### Scenario: Create a supplier
- **WHEN** the user fills name and phone and confirms "Thêm nhà cung cấp"
- **THEN** the dialog closes, the list reloads once and the new supplier can be chosen on a new receipt

#### Scenario: Duplicate name
- **WHEN** the server answers HTTP 409 for the supplier name
- **THEN** the dialog stays open and shows "Tên nhà cung cấp đã tồn tại."

#### Scenario: Deactivate a supplier
- **WHEN** the user confirms "Ngừng sử dụng" for a supplier
- **THEN** its status shows "Ngừng sử dụng" and it is no longer offered in the receipt form

### Requirement: Receipt workspace verification
ViewModels and client repositories of both tabs and their dialogs SHALL be covered by headless unit tests for loading states, validation, request mapping, `clientRequestId` reuse and error mapping (400, 401, 403, 404, 409, network). Bindings and theme switching SHALL be checked by running the app.

#### Scenario: Headless tests
- **WHEN** the frontend unit tests run
- **THEN** they exercise the receipt and supplier ViewModels and repositories without a UI thread and pass

### Requirement: Receive deliveries against an open receipt

The form SHALL offer immediate receipt and creation of an ordered receipt. The detail dialog SHALL show status and received versus ordered quantities. Admin and WarehouseStaff SHALL be able to enter the delivered quantities and identifiers, retry the same batch after a network error and explicitly confirm cancellation of the remainder.

#### Scenario: Partial delivery and cancellation from the workspace
- **WHEN** WarehouseStaff opens a five-unit ordered receipt, records two units and confirms cancellation
- **THEN** the detail shows two received out of five, the list shows a cancelled receipt, and further delivery actions are unavailable
