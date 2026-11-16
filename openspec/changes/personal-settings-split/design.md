# Design

## Decisions

### D1. Một bool `CanEditStore`
`SettingsViewModel.CanEditStore = RoleAccess.CanEditStoreSettings(session.Role)` (A, M; không có session → true như hành vi cũ của test/gallery). TextBox bind `IsReadOnly`, nút lưu và banner bind `Visibility`. `SaveStoreSettingsAsync` trả về sớm khi không được phép, không gửi request.

### D2. Thứ tự nhóm
Cá nhân (theme) đặt trước, sau đó Cửa hàng, rồi Thông tin ứng dụng; nút quản lý tài khoản giữ cho Admin.

## Non-Goals
Không lưu theme theo tài khoản hay qua lần chạy sau (spec hiện tại: phiên mới mặc định System).
