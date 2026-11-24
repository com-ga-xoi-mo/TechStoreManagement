# Proposal

## Why

M4 Sprint 1 (Vy): "Làm UI/API quản lý voucher, điểm, hạng và lịch sử khách hàng". Phần API/service đã xong (change `m4-loyalty-vip-vouchers`). Client chưa có màn hình nào cho voucher, và hồ sơ khách hàng mới chỉ hiện số điểm và hạng, chưa cho thấy mức giảm của hạng, hạng kế tiếp và số điểm còn thiếu.

## What Changes

- **Quản lý voucher (Admin/Manager)**: route mới `AppRoute.Vouchers()` nằm dưới mục Khách hàng (như Quản lý tài khoản nằm dưới Cài đặt, nên menu vẫn có sáu mục). Danh sách có tìm theo mã/mô tả, lọc trạng thái, phân trang, cột lượt dùng (đã dùng / đang giữ / giới hạn), tạo, sửa, bật/tắt. Nút "Voucher" ở đầu danh sách khách hàng chỉ hiện cho Admin/Manager.
- **Hồ sơ khách hàng**: thêm thẻ tóm tắt điểm gọi `GET /api/customers/{id}/loyalty-summary`: điểm, hạng, mức giảm của hạng, hạng kế tiếp, điểm còn thiếu và thanh tiến độ. Hiện cho mọi vai trò mở được hồ sơ (Admin, Manager, SalesStaff), vì server cho phép.
- **Client**: `IVoucherRepository`, `ILoyaltyRepository`, models, ViewModels, views C# Markup theo token giao diện, DI.
- **Quyền client**: `RoleAccess.CanManageVouchers`, `CanOpen(Vouchers)` chỉ Admin/Manager, route bị chặn trước khi tải.
- **Không làm**: chọn voucher/VIP trong màn tạo đơn hay POS (Đạt/An nối vào checkout), sửa luồng thanh toán/hoàn tiền, thay đổi API.

## Capabilities

### New Capabilities

- `frontend-voucher-management`: màn hình quản lý voucher.

### Modified Capabilities

- `frontend-customer-management`: thẻ tóm tắt điểm và hạng, nút vào Voucher.
- `frontend-shell-navigation`: route Voucher dưới Khách hàng, quyền mở route.

## Impact

- Client: `Models/Vouchers`, `Models/Customers` (tóm tắt điểm), `Repositories/Vouchers`, `Repositories/Loyalty`, `ViewModels/Vouchers`, `ViewModels/Customers/CustomerDetailViewModel`, `Views/Vouchers`, `Views/Customers` (nút vào, thẻ tóm tắt), `Services/Navigation` (`AppRoute`, `RoleAccess`), `Views/Shell/ShellRoutePageFactory`, `UiServiceCollectionExtensions`.
- Tests: `tests/Frontend.UnitTests` (repository, ViewModel, điều hướng, quyền).
- Ảnh hưởng người khác: thêm một giá trị `AppRouteKind` và một nhánh trong `ShellRoutePageFactory`/`RoleAccess`; không đổi sáu mục menu hay route cũ. Không sửa backend.
