# Tasks

## 1. Server

- [ ] 1.1 Thêm `Identity/Services/FieldAccess.cs` với `CanViewProductCost(RoleType)` (Admin, Manager); unit test đủ bốn role.
- [ ] 1.2 `ProductDto.CostPrice` → `decimal?`; `ProductDtoMapper.ToDto(..., includeCost)`; `IProductService.GetByIdAsync(id, includeCost, ct)`; create/update/status giữ `includeCost: true`.
- [ ] 1.3 `ProductsController.GetById` đọc role bằng `AuthenticatedUser.TryRead`; cập nhật caller/tests ProductService và test HTTP: Manager thấy giá vốn, Sales/Warehouse nhận `null`.

## 2. Client

- [ ] 2.1 `ProductDetail.CostPrice` → `decimal?`; `ProductRepository` chấp nhận `costPrice` null, chỉ báo lỗi khi âm; cập nhật fakes/tests.
- [ ] 2.2 `ProductDetailPage` ẩn dòng giá vốn khi null; `ProductEditorViewModel` nạp chuỗi rỗng khi null.

## 3. Kiểm thử thủ công (AI)

AI tự chạy, ghi lệnh và kết quả thật vào mục 3.x; không tính bước chưa chạy là đạt.

- [ ] 3.1 Chạy API + PostgreSQL thật, đăng nhập `admin`, `manager1`, `cashier1`, `warehouse1` qua `POST /api/auth/login`; gọi `GET /api/products/{id}` bằng từng token. Mong đợi: A/M có `costPrice` số, S/W `costPrice: null`, các field khác giống nhau.
- [ ] 3.2 Gọi `GET /api/products/{id}` không token → 401; token sai chữ ký → 401.
- [ ] 3.3 Chạy `dotnet test` backend và frontend unit; ghi Pass/Fail/Skipped.
