# Quyền API — M3 Sprint 1

A = Admin, M = Manager, S = SalesStaff, W = WarehouseStaff.
Mỗi request nghiệp vụ cần JWT hợp lệ, tài khoản đang hoạt động, role và token_version hiện hành.
Thiếu/xác thực sai: 401; đúng identity nhưng thiếu quyền: 403.

| API/action | Allowed roles | Read/write scope |
| --- | --- | --- |
| GET `/api/products`, `/api/products/{id}`, `/api/products/spec-keys`, `/api/products/brands`, `/api/products/spec-definitions` | A M S W | Current catalog DTOs, including current product detail costPrice; field redaction is Sprint 2 |
| POST `/api/products`, PUT `/api/products/{id}`, PATCH `/api/products/{id}/status`, DELETE `/api/products/{id}` | A M | Existing product and price management |
| POST `/api/products/{productId}/images`, DELETE `/api/products/{productId}/images/{imageId}`, PUT `/api/products/{productId}/images/layout` | A M | Existing image management |
| GET `/api/categories/tree` | A M S W | Existing category tree |
| POST `/api/categories`, PUT/DELETE `/api/categories/{id}` | A | Existing category management |
| GET `/api/inventory/stocks`, `/api/inventory/stocks/{stockId}/detail`, `/api/inventory/serials` | A M S W | Existing stock, serial and movement DTOs; cost-field policy reviewed in Sprint 2 |
| POST `/api/inventory/stocks/initialize`, POST `/api/inventory/stocks/{stockId}/adjust`, DELETE `/api/inventory/stocks/{stockId}`, POST `/api/inventory/serials/batch-import` | A W | Existing inventory writes, never catalog price writes |
| GET `/api/orders`, `/api/orders/{id}` | A M S | A/M see all; S sees only orders whose cashier is that user |
| POST `/api/orders`, PATCH `/api/orders/{id}/status` | A S | A writes all; S transitions only own orders; creator comes from token |
| GET `/api/customers`, `/api/customers/{id}`, `/api/customers/{id}/loyalty-points`, `/api/customers/{id}/orders`, `/api/customers/{id}/purchased-items` | A | Existing customer and history DTOs; Sales access is Sprint 2 |
| POST `/api/customers`, PUT `/api/customers/{id}`, PATCH `/api/customers/{id}/deactivate`, `/api/customers/{id}/activate` | A | Existing customer management |
| GET `/api/analytics/dashboard` | A | Store-wide dashboard; Manager enablement is Sprint 2 |
| PUT `/api/settings/store` | A M | Existing store configuration fields |
| GET `/api/auth/me`, POST `/api/auth/logout` | A M S W | Own identity and account-wide logout |
| GET `/api/users`, `/api/users/{id}`, POST `/api/users`, PUT `/api/users/{id}`, PATCH `/api/users/{id}/status`, PUT `/api/users/{id}/role`, POST `/api/users/{id}/reset-password` | A | Account metadata and administrative mutations; never hashes/tokens |

Public: POST /api/auth/login, POST /api/auth/refresh (vẫn kiểm tra refresh token),
GET /api/health và GET /api/settings/store (chỉ store metadata hiện có).
Static product image files public; Swagger chỉ Development. Fallback policy bắt đăng nhập
cho endpoint không khai báo policy; không có quyền mặc định hoặc user fallback.
Account password/hash/token/version và order cost snapshots không xuất trong business DTO.

Sprint 2 tiếp tục: mở dashboard Manager và khách hàng Sales, kiểm tra trường giá vốn
Catalog/Inventory theo role, landing page/menu và tách settings cá nhân.
Hiện Manager gọi dashboard và Sales gọi Customers vẫn nhận 403.
