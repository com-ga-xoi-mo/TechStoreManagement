# Tasks

## 1. Server

- [x] 1.1 Viết test trước: `CustomerServiceTests` cho Sales tìm chỉ ra khách đang hoạt động (kể cả khi gửi `isActive=false`), Sales đọc khách không hoạt động → NotFound, Admin/Manager không đổi.
- [x] 1.2 `ICustomerService.SearchAsync(request, role)` và `GetByIdAsync(id, role)`; cập nhật caller và fake.
- [x] 1.3 `CustomersController`: lớp `Admin,Manager,SalesStaff`; sửa/activity/lịch sử `Admin,Manager`; đọc role bằng `AuthenticatedUser.TryRead`.
- [x] 1.4 Cập nhật baseline `ApiAuthorizationCoverageTests` và `CustomerAuthorizationHttpTests` (S được đọc/tạo, S bị 403 khi sửa/activity/lịch sử, W bị 403 mọi endpoint, M được quản lý).
- [x] 1.5 Cập nhật `docs/API_PERMISSIONS.md` và `backend/Customers/README.md`.

## 2. Client

- [x] 2.1 Test trước rồi sửa `RoleAccess`: Customers mở cho A/M/S; `CanCreateCustomers`, `CanManageCustomers`.
- [x] 2.2 `CustomerListViewModel`/`CustomerDetailViewModel` nhận `ISessionStore?`; Sales luôn tìm `isActive=true`, không tải lịch sử; test headless.
- [x] 2.3 `CustomerListPage`/`CustomerDetailDialogView`: ẩn nút sửa, bộ lọc trạng thái, activity và tab lịch sử theo `CanManageCustomers`.
- [x] 2.4 `CustomerPickerViewModel` (tìm, chọn, bỏ chọn, tạo nhanh, 409, bỏ kết quả cũ) với test headless.
- [x] 2.5 `CustomerPickerView` C# Markup; đăng ký DI; gắn vào `OrderCreatePage` và gửi `customerId`; test `OrderCreateViewModel`.

## 3. Kiểm thử (AI tự chạy, ghi lệnh và kết quả thật)

- [x] 3.1 `dotnet build TechStore.sln`; backend và frontend unit tests; ghi Pass/Fail/Skipped.
- [x] 3.2 API + PostgreSQL thật: đăng nhập admin/manager/sales/warehouse, gọi từng endpoint Customers, ghi mã trả về.
- [ ] 3.3 Chạy UI Skia: Sales mở Khách hàng (không thấy nút sửa/lịch sử), tạo khách; trang tạo đơn chọn khách rồi lưu nháp. Ghi rõ phần nào chưa kiểm tra được.

## Kết quả kiểm thử (AI, 2026-11-17, Windows 11)

- 3.1 `dotnet build frontend/TechStore.Client -f net10.0-desktop`: 0 warning, 0 error. Backend unit: 617 Pass / 0 Fail / 27 Skipped khi chưa có PostgreSQL; chạy lại với `TECHSTORE_TEST_CONNECTION` trỏ PostgreSQL 16 Docker (fixture tự tạo/xóa DB): 645 Pass / 0 Fail / 0 Skipped. Frontend unit: 996 Pass / 0 Fail / 0 Skipped. Build target `net10.0-windows10.0.26100` lỗi sẵn trên `dev` ở `Testing/IdentityThemeProbe.cs` (`ProbeReportItem`), không do change này. **Pass**
- 3.2 API `http://localhost:5287`, PostgreSQL 16 Docker, DB riêng `techstore_vy_verify` (migrate + seed mới), đăng nhập `admin`, `manager1`, `cashier1`, `warehouse1`. **Pass**
  - S tạo khách 201 (Standard, active, 0 điểm); tạo lại cùng số 409.
  - Tìm `q=0977&isActive=false`: S nhận 1 khách đang hoạt động; A nhận 1 khách đã vô hiệu hóa.
  - Hồ sơ khách đã vô hiệu hóa: S 404, M 200. Hồ sơ khách đang hoạt động: S 200.
  - PUT, deactivate, activate, orders, loyalty-points, purchased-items: S 403, W 403, M 200/204, A 200/204.
  - W tìm/tạo 403; không token 401.
  - S tạo đơn `POST /api/orders` có `customerId` của khách vừa tạo: 201, `customerName` đúng; không gửi `customerId`: 201, khách lẻ; khách đã vô hiệu hóa: 400 dưới `customerId`.
- 3.3 **Chưa kiểm tra.** Không điều khiển được cửa sổ Skia từ phiên agent (Windows chặn đưa cửa sổ lên trước), nên chưa xem trang Khách hàng với Sales, form tạo khách từ bộ chọn, binding và đổi theme. Cần Vy chạy tay trước khi archive.
