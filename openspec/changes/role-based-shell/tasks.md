# Tasks

## 1. Bảng quyền client

- [ ] 1.1 Thêm `Services/Navigation/RoleAccess.cs` theo design D1; `CatalogPermissions.CanManage` dùng lại; link vào headless tests; test bốn role + role lạ + chưa đăng nhập cho `Destinations`, `Landing`, `CanOpen` mọi route và các nhóm nút.

## 2. Shell và route

- [ ] 2.1 Thêm `Reset(AppRoute)` vào `INavigationService`/`NavigationService`/`NavigationState`; cập nhật mọi fake.
- [ ] 2.2 `ShellViewModel` reset về landing của role và lọc `MainDestinations`; `ShellPage` dựng menu từ `MainDestinations`; tests landing/menu từng role và Admin → logout → Sales không giữ route cũ.
- [ ] 2.3 `ShellRoutePageFactory.Create` chặn route trái quyền; `VerifyAllRoutesResolve` resolve không qua guard; test guard bằng navigation state/preparer fake.

## 3. Nút thao tác

- [ ] 3.1 Danh mục (A), kho/serial (A, W), đơn hàng (A, S): thêm bool trên ViewModel đọc `ISessionStore?` và bind Visibility; tests ViewModel cho từng role.

## 4. Kiểm thử thủ công (AI)

AI tự chạy, ghi lệnh và kết quả thật; bước không chạy được ghi "Chưa kiểm tra" và lý do.

- [ ] 4.1 Chạy headless tests Shell/RoleAccess; mô phỏng lần lượt bốn tài khoản demo đăng nhập qua `ShellViewModel` thật với session store thật, ghi landing + menu thực tế so với bảng D1.
- [ ] 4.2 Build `net10.0-desktop`, chạy app với API thật; đăng nhập từng role, ghi log Shell/route và ảnh chụp nếu môi trường cho phép.
- [ ] 4.3 Với token Sales/Warehouse gọi trực tiếp API ghi tương ứng nút đã ẩn (`POST /api/categories`, `POST /api/inventory/stocks/initialize`, `POST /api/products`, `POST /api/orders` với Warehouse) → 403, xác nhận server vẫn chặn khi bỏ qua UI.
