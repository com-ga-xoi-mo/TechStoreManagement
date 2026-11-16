# Tasks

## 1. Server

- [x] 1.1 Thêm `Identity/Services/FieldAccess.cs` với `CanViewProductCost(RoleType)` (Admin, Manager); unit test đủ bốn role.
- [x] 1.2 `ProductDto.CostPrice` → `decimal?`; `ProductDtoMapper.ToDto(..., includeCost)`; `IProductService.GetByIdAsync(id, includeCost, ct)`; create/update/status giữ `includeCost: true`.
- [x] 1.3 `ProductsController.GetById` đọc role bằng `AuthenticatedUser.TryRead`; cập nhật caller/tests ProductService và test HTTP: Manager thấy giá vốn, Sales/Warehouse nhận `null`.

## 2. Client

- [x] 2.1 `ProductDetail.CostPrice` → `decimal?`; `ProductRepository` chấp nhận `costPrice` null, chỉ báo lỗi khi âm; cập nhật fakes/tests.
- [x] 2.2 `ProductDetailPage` ẩn dòng giá vốn khi null; `ProductEditorViewModel` nạp chuỗi rỗng khi null.

## 3. Kiểm thử thủ công (AI)

AI tự chạy, ghi lệnh và kết quả thật vào mục 3.x; không tính bước chưa chạy là đạt.

- [x] 3.1 Chạy API + PostgreSQL thật, đăng nhập `admin`, `manager1`, `cashier1`, `warehouse1` qua `POST /api/auth/login`; gọi `GET /api/products/{id}` bằng từng token. Mong đợi: A/M có `costPrice` số, S/W `costPrice: null`, các field khác giống nhau.
- [x] 3.2 Gọi `GET /api/products/{id}` không token → 401; token sai chữ ký → 401.
- [x] 3.3 Chạy `dotnet test` backend và frontend unit; ghi Pass/Fail/Skipped.

## Kết quả kiểm thử (AI, 2026-11-16)

- 3.1 API `http://localhost:5199`, DB riêng `techstore_m3_verify`, sản phẩm `DEMO-CABLE-USB-C`: admin 200 `costPrice=120000`; manager1 200 `120000`; cashier1 200 `null`; warehouse1 200 `null`; các field khác giống hệt giữa admin và cashier1. **Pass**
- 3.2 Không token → 401; token sửa chữ ký → 401. **Pass**
- 3.3 Backend unit: 581 Pass / 0 Fail / 26 Skipped (PostgreSQL tests cần biến môi trường, chưa chạy). Frontend unit: 938 Pass / 0 Fail. `dotnet build TechStore.sln` thành công. **Pass** (26 Skipped không tính là đạt)
- Chưa kiểm tra: trang chi tiết sản phẩm trên UI thật (ẩn dòng giá vốn khi null).
- Kiểm chứng lại trước commit: `dotnet build TechStore.sln` 0 warning/error; backend 581 Pass / 0 Fail / 26 Skipped; frontend 938 Pass / 0 Fail / 0 Skipped. Log `/tmp/m3-build.log`, `/tmp/m3-backend.log`, `/tmp/m3-frontend.log`.
- Đã thử khởi động Skia trỏ API thật qua `TECHSTORE_API_BASE_URL=http://localhost:5199`; log `/tmp/m3-skia.log` có cảnh báo Uno dev-server và X11/EWMH. Chưa kiểm tra trực quan trang chi tiết, binding và ẩn giá vốn: không có công cụ quan sát/điều khiển Wayland; không tính startup log là đạt UI.
