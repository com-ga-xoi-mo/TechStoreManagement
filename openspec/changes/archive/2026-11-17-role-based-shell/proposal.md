# Proposal

## Why

M3 Sprint 2 (T. Vinh): "Chọn trang mở đầu, menu và nút thao tác theo role. Kiểm tra cả trường hợp mở thẳng một trang bị hạn chế." Hiện Shell hiện cùng sáu mục cho mọi role, luôn mở Tổng quan (Sales/Warehouse nhận 403), route giữ nguyên từ phiên trước, và nút thêm/sửa/xóa kho, danh mục, tạo đơn hiện cho cả role không có quyền ghi.

## What Changes

- Thêm bảng quyền client thuần `RoleAccess` (một nơi, unit test được) khớp `docs/API_PERMISSIONS.md`: mục menu, route con và nhóm nút thao tác cho từng role; trang mở đầu là mục đầu tiên role được vào theo thứ tự Tổng quan → Đơn hàng → Danh mục và Kho → Sản phẩm.
- Shell chỉ dựng mục menu role được vào; mỗi lần đăng nhập mở trang mở đầu của role (không giữ route của phiên trước).
- `ShellRoutePageFactory` chặn mọi route trái quyền (gồm route con tạo/sửa sản phẩm, tạo đơn, quản lý tài khoản) trước khi tạo trang hay gọi API.
- Ẩn nút ghi theo role: danh mục (Admin), kho (Admin, WarehouseStaff), tạo đơn/đổi trạng thái (Admin, SalesStaff). Sản phẩm đã có `CatalogPermissions`.
- Tổng quan cho Manager và Khách hàng cho Sales đang chờ Bình/Vy mở ở server; bảng client ghi một dòng để chủ phần đổi khi merge.

## Capabilities

### Modified Capabilities

- `frontend-shell-navigation`: menu, landing và route guard theo role.

## Impact

- Client: `Services/Navigation/RoleAccess.cs` (mới), `INavigationService.Reset`, `NavigationService`, `NavigationState`, `ShellViewModel`, `ShellPage`, `ShellRoutePageFactory`, các ViewModel/Page Danh mục, Kho, Serial, Đơn hàng; `CatalogPermissions` dùng `RoleAccess`.
- Tests: headless tests cho `RoleAccess`, Shell landing/menu, guard; cập nhật fake `INavigationService`.
- Server không đổi: quyền vẫn kiểm ở API; client chỉ tránh gọi API sẽ bị 403.
