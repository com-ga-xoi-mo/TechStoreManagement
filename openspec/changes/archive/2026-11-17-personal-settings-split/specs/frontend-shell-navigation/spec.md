# Spec Delta

## MODIFIED Requirements

### Requirement: Session local settings
Cài đặt SHALL chia nhóm Cá nhân và nhóm Cửa hàng. Nhóm Cá nhân SHALL cho mọi role chọn System/Light/Dark. Nhóm Cửa hàng SHALL hiển thị thông tin cấu hình cửa hàng TechStore (tên, địa chỉ, hotline, email, mã số thuế); chỉ `Admin` và `Manager` SHALL sửa và lưu được qua API cấu hình, các role khác chỉ đọc và không có nút lưu. Trang dùng Uno C# Markup Fluent API kết hợp CommunityToolkit SettingsControls và declarative bindings. Theme SHALL áp dụng ngay trong Shell/dialog, giữ khi chuyển trang trong phiên và mặc định System ở phiên mới; thao tác theme không cần backend.

#### Scenario: Theme survives navigation
- **WHEN** chọn Dark, chuyển Sản phẩm rồi trở lại Cài đặt
- **THEN** Shell và dialogs dùng Dark, lựa chọn còn Dark; theme không phát request và Catalog chỉ gọi API cho dữ liệu của activation

#### Scenario: New process
- **WHEN** đóng rồi khởi chạy client lần mới
- **THEN** theme bắt đầu System, không tuyên bố đã lưu tùy chọn lâu dài

#### Scenario: Store configuration view and update
- **WHEN** Quản trị viên hoặc Quản lý mở Cài đặt và cập nhật hotline cửa hàng
- **THEN** form thông tin cửa hàng dùng SettingsCard/SettingsExpander, lưu thành công và thông báo kết quả

#### Scenario: Sales changes theme only
- **WHEN** SalesStaff hoặc WarehouseStaff mở Cài đặt
- **THEN** đổi được theme, thấy thông tin cửa hàng ở dạng chỉ đọc, không có nút lưu và không gửi `PUT /api/settings/store`
