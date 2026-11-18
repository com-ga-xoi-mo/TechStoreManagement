# Quyền API — M3

A = Admin, M = Manager, S = SalesStaff, W = WarehouseStaff.
Mỗi request nghiệp vụ cần JWT hợp lệ, tài khoản đang hoạt động, role và token_version hiện hành.
Thiếu/xác thực sai: 401; đúng identity nhưng thiếu quyền: 403.

| API/action | Allowed roles | Read/write scope |
| --- | --- | --- |
| GET `/api/products`, `/api/products/{id}`, `/api/products/spec-keys`, `/api/products/brands`, `/api/products/spec-definitions` | A M S W | Product detail `costPrice` is populated only for A/M; S/W receive `null`. Other catalog fields unchanged |
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
| POST `/api/pos/shifts`, GET `/api/pos/shifts/current`, POST `/api/pos/checkout` | A S | Open own shift, read own open shift, cash checkout into own open shift; actor from token |
| GET `/api/pos/shifts`, `/api/pos/shifts/{id}` | A M S | A/M see all shifts; S sees only own (other users' shifts return 404) |
| POST `/api/pos/shifts/{id}/close` | A M S | S closes own shift only; A/M close any shift; closing records `closedByUserId` |
| GET `/api/analytics/dashboard` | A M | Read-only store-wide dashboard |
| PUT `/api/settings/store` | A M | Existing store configuration fields |
| GET `/api/auth/me`, POST `/api/auth/logout` | A M S W | Own identity and account-wide logout |
| GET `/api/users`, `/api/users/{id}`, POST `/api/users`, PUT `/api/users/{id}`, PATCH `/api/users/{id}/status`, PUT `/api/users/{id}/role`, POST `/api/users/{id}/reset-password` | A | Account metadata and administrative mutations; never hashes/tokens |

Public: POST /api/auth/login, POST /api/auth/refresh (vẫn kiểm tra refresh token),
GET /api/health và GET /api/settings/store (chỉ store metadata hiện có).
Static product image files public; Swagger chỉ Development. Fallback policy bắt đăng nhập
cho endpoint không khai báo policy; không có quyền mặc định hoặc user fallback.
Account password/hash/token/version và order cost snapshots không xuất trong business DTO.

Sprint 2 tiếp tục: mở khách hàng Sales, kiểm tra trường giá vốn
Inventory theo role, landing page/menu và tách settings cá nhân. Giá vốn sản phẩm đã được che tại server theo `FieldAccess`.
Sales gọi Customers vẫn nhận 403.

## Kiểm chứng và cập nhật khi tích hợp

`tests/Backend.UnitTests/ApiAuthorizationCoverageTests.cs` giữ baseline method/route và
public/roles độc lập với controller metadata. Test đối chiếu hai chiều toàn bộ controller
endpoints đang đăng ký, kết hợp policy controller/action/default/fallback và kiểm cả bốn
role. HTTP + PostgreSQL tests yêu cầu đúng 401 khi thiếu token, đúng 403 cho role bị cấm;
400/404/500 không được tính là quyền đã đạt. Positive reads và test actor/ownership được
ghi riêng; độ phủ policy không chứng nhận mọi nghiệp vụ đã hoạt động.

Khi chủ module merge endpoint mới hoặc thay quyền, cập nhật bảng này, spec của change
nghiệp vụ tương ứng, baseline và test/caller trong cùng thay đổi. Không tự sinh expected
roles từ attributes, không đưa API tương lai chưa đăng ký vào baseline rồi kết luận lỗi.
Dashboard Manager thuộc Bình; Customers cho Sales thuộc Vy; trường giá vốn và UI theo
phân công Q. Vinh/T. Vinh.

Lệnh chạy và giới hạn TestServer/Kestrel/UI: [Identity README](../backend/Identity/README.md#repeatable-http--postgresql-regression).
Kết quả theo build và các điểm chờ tích hợp: [M3 handoff](M3_IDENTITY_INTEGRATION_HANDOFF.md).
