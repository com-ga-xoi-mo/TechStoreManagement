# Proposal

## Why

M3 Sprint 2 (Vy): "Cho Sales tìm, chọn và tạo khách hàng phục vụ bán hàng. Quyền sửa, vô hiệu hóa và xem lịch sử phải theo bảng quyền đã chốt." Hiện mọi endpoint `/api/customers` chỉ cho `Admin`, nên SalesStaff không gắn được khách vào đơn, Manager không xem được khách. Đạt cần một bộ chọn/tạo khách dùng chung cho màn tạo đơn và POS.

## What Changes

- Chốt bảng quyền Customers theo hành động:
  - Tìm và xem hồ sơ: `Admin`, `Manager`, `SalesStaff`. SalesStaff chỉ thấy khách đang hoạt động (server ép, không chỉ lọc trên UI); khách đã vô hiệu hóa trả 404 với SalesStaff.
  - Tạo khách: `Admin`, `Manager`, `SalesStaff`.
  - Sửa, vô hiệu hóa/kích hoạt lại, xem lịch sử đơn/thiết bị/điểm: `Admin`, `Manager`. SalesStaff bị 403.
  - `WarehouseStaff` không có quyền nào với Customers (403).
- Client: route Khách hàng mở cho Admin/Manager/SalesStaff; SalesStaff chỉ thấy tìm, xem hồ sơ và thêm khách, không thấy nút sửa, bộ lọc trạng thái, vô hiệu hóa/kích hoạt và các tab lịch sử.
- Client: bộ chọn khách dùng chung (tìm theo tên/số điện thoại, chọn, bỏ chọn về khách lẻ, tạo nhanh khách mới rồi chọn luôn), gắn vào trang tạo đơn để gửi `customerId`. Đạt dùng lại cho POS.

## Capabilities

### Modified Capabilities

- `customer-management`: quyền theo hành động, SalesStaff chỉ đọc khách đang hoạt động.
- `api-role-authorization`: bảng quyền Customers mới.
- `frontend-customer-management`: trang khách hàng theo role; bộ chọn khách.
- `frontend-order-management`: trang tạo đơn chọn hoặc tạo khách.

## Impact

- Backend: `CustomersController` (policy theo action, đọc actor), `ICustomerService`/`CustomerService` (`SearchAsync`, `GetByIdAsync` nhận role), `Identity/Services/FieldAccess.cs` không đổi.
- Tests: `CustomerServiceTests`, `CustomerAuthorizationHttpTests`, `ApiAuthorizationCoverageTests` (baseline), frontend tests cho RoleAccess, ViewModel khách hàng và bộ chọn.
- Docs: `docs/API_PERMISSIONS.md`, `backend/Customers/README.md`.
- Không đổi schema, không migration. Không đổi contract JSON của Customers.
- Đạt: POS gọi bộ chọn khách (`CustomerPickerViewModel` + `CustomerPickerView`) thay vì tự viết.
