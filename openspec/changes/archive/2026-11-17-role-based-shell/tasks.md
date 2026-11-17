# Tasks

## 1. Bảng quyền client

- [x] 1.1 Thêm `Services/Navigation/RoleAccess.cs` theo design D1; `CatalogPermissions.CanManage` dùng lại; link vào headless tests; test bốn role + role lạ + chưa đăng nhập cho `Destinations`, `Landing`, `CanOpen` mọi route và các nhóm nút.

## 2. Shell và route

- [x] 2.1 Thêm `Reset(AppRoute)` vào `INavigationService`/`NavigationService`/`NavigationState`; cập nhật mọi fake.
- [x] 2.2 `ShellViewModel` reset về landing của role và lọc `MainDestinations`; `ShellPage` dựng menu từ `MainDestinations`; tests landing/menu từng role và Admin → logout → Sales không giữ route cũ.
- [x] 2.3 `ShellRoutePageFactory.Create` chặn route trái quyền; `VerifyAllRoutesResolve` resolve không qua guard; test guard bằng navigation state/preparer fake.

## 3. Nút thao tác

- [x] 3.1 Danh mục (A), kho/serial (A, W), đơn hàng (A, S): thêm bool trên ViewModel đọc `ISessionStore?` và bind Visibility; tests ViewModel cho từng role.

## 4. Kiểm thử thủ công (AI)

AI tự chạy, ghi lệnh và kết quả thật; bước không chạy được ghi "Chưa kiểm tra" và lý do.

- [x] 4.1 Chạy headless tests Shell/RoleAccess; mô phỏng lần lượt bốn tài khoản demo đăng nhập qua `ShellViewModel` thật với session store thật, ghi landing + menu thực tế so với bảng D1.
- [x] 4.2 Build `net10.0-desktop`, chạy app với API thật; đăng nhập từng role, ghi log Shell/route và ảnh chụp nếu môi trường cho phép.
- [x] 4.3 Với token Sales/Warehouse gọi trực tiếp API ghi tương ứng nút đã ẩn (`POST /api/categories`, `POST /api/inventory/stocks/initialize`, `POST /api/products`, `POST /api/orders` với Warehouse) → 403, xác nhận server vẫn chặn khi bỏ qua UI.

## Kết quả kiểm thử (AI, 2026-11-16)

- 4.3 API thật `http://localhost:5199`, DB `techstore_m3_verify`; đăng nhập Sales/Warehouse thành công 200. Sales: POST categories, inventory/stocks/initialize, products đều 403; Warehouse: POST categories, products, orders đều 403, `application/problem+json`. **Pass**. Log `/tmp/m3-role-api.log`.
- Lần thử đầu chưa đọc được token, không tính là đạt; lần sau đọc đúng `accessToken` từ response thật và xác nhận token không rỗng trước khi gọi nghiệp vụ. Admin đăng nhập lại 200, không đổi credentials.
- 4.1 `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj --logger "console;verbosity=detailed"`: **962 Pass / 0 Fail / 0 Skipped**. Log `/tmp/m3-role-frontend.log` ghi output thực tế từ ShellViewModel + InMemorySessionStore (token mô phỏng, không phải login UI): Admin landing Dashboard, menu Dashboard/Products/Inventory/Orders/Customers/Settings; Manager Orders, Products/Inventory/Orders/Settings; Sales Orders, Products/Orders/Settings; Warehouse Inventory, Products/Inventory/Settings. **Pass headless**, không thay thế kiểm tra binding UI.
- 4.2 Build desktop **Pass**, 0 warning/error (`/tmp/m3-role-build.log`). App chạy với API thật qua `TECHSTORE_API_BASE_URL=http://localhost:5199`, dừng bởi timeout 15 giây; log `/tmp/m3-role-skia.log` có cảnh báo Uno dev-server/X11. **Chưa kiểm tra** đăng nhập/binding/menu bằng UI bốn role vì không quan sát/điều khiển Wayland; task giữ mở.

- 4.2 (17/11): T. Vinh chạy app Skia với API thật, đăng nhập bốn role, landing/menu/nút đúng (kịch bản A–B). **Pass**
