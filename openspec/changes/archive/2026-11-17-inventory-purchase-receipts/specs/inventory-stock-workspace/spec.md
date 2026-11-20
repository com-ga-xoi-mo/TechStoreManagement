# Spec Delta

## MODIFIED Requirements

### Requirement: Deferred stock action hooks
The workspace SHALL expose actions for “Nhập kho mới”, viewing a stock row's detail, adjusting stock, and deleting a stock row. “Nhập kho mới” SHALL open the create purchase receipt form of the “Nhập hàng” tab, and the Tồn kho list SHALL refresh exactly once after that receipt succeeds. Each row SHALL offer its actions through one “•••” button, labeled for assistive technology as “Thao tác cho <product name>”, that opens a menu with “Xem chi tiết”, “Điều chỉnh tồn” and “Xóa tồn” in that order, and the menu SHALL be reachable and operable with the keyboard. “Xem chi tiết” SHALL open the stock detail/history dialog, and “Điều chỉnh tồn” SHALL open the adjustment dialog; delete SHALL continue to mutate inventory only after a supported delete operation succeeds.

#### Scenario: Open the row actions menu
- **WHEN** the user activates the “•••” button of a stock row with the pointer, or focuses it with Tab and presses Enter
- **THEN** a menu opens below the button with “Xem chi tiết”, “Điều chỉnh tồn” and “Xóa tồn”, the last one in the danger color, and Esc closes it without any action

#### Scenario: Open stock detail
- **WHEN** the user picks “Xem chi tiết” in a stock row's actions menu
- **THEN** the client opens the detail/history dialog with that row's immutable identity and display context

#### Scenario: Activate a deferred action
- **WHEN** the user activates an inventory action whose hook is not registered in the current host, such as the workspace shown without the receipt form
- **THEN** the client reports the action as unavailable and issues no stock mutation request

#### Scenario: Receive goods from the stock tab
- **WHEN** the user activates “Nhập kho mới” on the Tồn kho tab
- **THEN** the client opens the create purchase receipt form, issues no stock mutation request until the user confirms it, and after a successful receipt refreshes the Tồn kho list exactly once

#### Scenario: Cancel receiving from the stock tab
- **WHEN** the user closes the create purchase receipt form opened from “Nhập kho mới” without confirming
- **THEN** no request is sent and the Tồn kho list is unchanged

#### Scenario: Open stock adjustment
- **WHEN** the user picks “Điều chỉnh tồn” in a stock row's actions menu
- **THEN** the client opens the adjustment dialog with current quantity and reserved quantity

#### Scenario: Complete supported adjustment
- **WHEN** the user confirms a valid adjustment and the server succeeds
- **THEN** the client closes the dialog, refreshes the inventory list exactly once, and raises the success notification

#### Scenario: Request stock deletion
- **WHEN** the user picks “Xóa tồn” in a stock row's actions menu
- **THEN** the client opens the delete-stock confirmation dialog with that row's immutable identity and display context
- **AND** the table remains unchanged while the dialog is open or the delete operation is unavailable

#### Scenario: Complete supported deletion
- **WHEN** the user confirms a supported delete operation and the server succeeds
- **THEN** the client closes the dialog, refreshes the inventory list exactly once, and raises the success notification

## ADDED Requirements

### Requirement: Serial row status actions
Each row of the Serial/IMEI tab SHALL have one “•••” button, labeled “Thao tác cho <IMEI hoặc serial>”, whose menu offers “Báo lỗi máy” for an `InStock` device and “Sửa xong, đưa về kho” for a `Defective` device; other statuses SHALL show the menu items disabled. Each item SHALL open a dialog, and the list SHALL reload exactly once after the server accepts the change.

#### Scenario: Report a device defective
- **WHEN** the user picks “Báo lỗi máy” on an `InStock` row, chooses or types the reason “Hư màn hình” and confirms
- **THEN** the client sends one report request for that device id, closes the dialog, reloads the current page once and the row shows “Lỗi”

#### Scenario: Suggested reasons
- **WHEN** the defect dialog opens
- **THEN** it offers the reasons “Hư hỏng” and “Mất” and also accepts a typed reason, and the confirm button stays disabled while the reason is blank

#### Scenario: Return a repaired device
- **WHEN** the user picks “Sửa xong, đưa về kho” on a `Defective` row and confirms
- **THEN** the client sends one restore request, reloads once and the row shows “Trong kho”

#### Scenario: Server refuses the change
- **WHEN** the server answers HTTP 400, 403, 404 or 409, or the network fails
- **THEN** the dialog stays open with the entered reason and note, clears its busy state and shows a Vietnamese error
