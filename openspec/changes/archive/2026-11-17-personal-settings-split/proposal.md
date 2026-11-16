# Proposal

## Why

M3 Sprint 2 (T. Vinh): "Tách cài đặt cá nhân như theme khỏi quyền sửa cấu hình cửa hàng." Server chỉ cho `Admin`/`Manager` gọi `PUT /api/settings/store`, nhưng trang Cài đặt hiện form sửa và nút "Lưu thay đổi" cho mọi role; Sales/Warehouse bấm lưu sẽ nhận 403.

## What Changes

- Cài đặt chia hai nhóm: **Cá nhân** (giao diện Sáng/Tối/Hệ thống, mọi role) và **Cửa hàng**.
- `Admin`/`Manager`: nhóm Cửa hàng sửa được và có nút lưu. `SalesStaff`/`WarehouseStaff`: thông tin cửa hàng chỉ đọc, không có nút lưu; lệnh lưu bị chặn trong ViewModel.
- Quyền sửa đọc từ `RoleAccess` (change `role-based-shell`).

## Capabilities

### Modified Capabilities

- `frontend-shell-navigation`: Session local settings.

## Impact

- Client: `SettingsViewModel`, `SettingsPage`; tests SettingsViewModel.
- Server không đổi.
