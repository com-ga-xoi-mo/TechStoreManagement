# Design

## Context

- `ShellPage` thêm cứng sáu `NavigationViewItem`; `NavigationService` là singleton nên `CurrentRoute` còn từ phiên trước.
- `App` tạo `ShellPage` mới (transient) mỗi khi session chuyển Active; `ShellViewModel` cũng transient.
- `ShellRoutePageFactory.Create` đã chặn `UserManagement` cho non-Admin bằng `UnauthorizedAccessException` → `ShellDestinationPreparer` trả false → `NavigationOutcome.DestinationUnavailable` → Shell báo lỗi, giữ trang cũ.
- `VerifyAllRoutesResolve` (DEBUG) tạo mọi route lúc khởi động để kiểm DI.

## Decisions

### D1. `RoleAccess` thuần, string role như `SessionSnapshot.Role`
Bảng tĩnh `Destinations(role)`, `CanOpen(route, role)`, `Landing(role)`, `CanManageCategories/Stock/Orders/Catalog(role)`. Role lạ hoặc chưa đăng nhập → không mục nào ngoài Cài đặt. Không dựng permission engine.

| Mục | Admin | Manager | SalesStaff | WarehouseStaff |
|---|---|---|---|---|
| Tổng quan | ✓ | chờ Bình | | |
| Sản phẩm | ✓ | ✓ | ✓ | ✓ |
| Danh mục và Kho | ✓ | ✓ (xem) | | ✓ |
| Đơn hàng | ✓ | ✓ (xem) | ✓ | |
| Khách hàng | ✓ | | chờ Vy | |
| Cài đặt | ✓ | ✓ | ✓ | ✓ |

Route con: tạo/sửa sản phẩm A/M; tạo đơn A/S; quản lý tài khoản A.

### D2. Reset route khi Shell mới được tạo
Thêm `INavigationService.Reset(AppRoute)` đặt route không chuẩn bị trang, không phát event. `ShellViewModel` gọi `Reset(AppRoute.Root(RoleAccess.Landing(role)))` trong constructor, trước khi `ShellNavigationAdapter.Attach` chuẩn bị trang đầu. Lý do: ShellPage/ShellViewModel đã tạo lại theo phiên; không cần lắng nghe thêm event.

### D3. Guard trong factory, verify DI bỏ qua guard
`Create(route)` ném `UnauthorizedAccessException` khi `!RoleAccess.CanOpen`. `VerifyAllRoutesResolve` chỉ resolve page qua DI (không qua guard) để DEBUG startup không phụ thuộc role đang lưu.

### D4. Nút ghi ẩn bằng bool trên ViewModel
ViewModel nhận `ISessionStore?` tùy chọn như `SettingsViewModel`; `null` (test cũ, gallery) coi như được phép để không đổi hành vi cũ. Page bind `Visibility`. Không dùng event handler.

## Non-Goals
Không đổi quyền server; không mở Tổng quan Manager/Khách hàng Sales (Bình/Vy); không cập nhật menu khi role đổi giữa phiên (server tăng token_version → 401 → đăng nhập lại).
