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
| GET `/api/inventory/stocks`, `/api/inventory/stocks/{stockId}/detail`, `/api/inventory/serials` | A M S W | Existing stock, serial and movement DTOs; trường `unitCost` của thiết bị trong `/api/inventory/serials` chỉ hiển thị cho A M W, ẩn thành `null` với SalesStaff |
| POST `/api/inventory/stocks/initialize`, POST `/api/inventory/stocks/{stockId}/adjust`, DELETE `/api/inventory/stocks/{stockId}`, POST `/api/inventory/serials/batch-import` | A W | Existing inventory writes, never catalog price writes; `initialize` từ chối hàng serial với 400 Bad Request; `adjust` từ chối hàng serial với 409 Conflict |
| GET `/api/inventory/suppliers`, `/api/inventory/suppliers/{id}` | A M W | Danh bạ nhà cung cấp, loại trừ nhà cung cấp hệ thống |
| POST `/api/inventory/suppliers`, PUT `/api/inventory/suppliers/{id}`, PATCH `/api/inventory/suppliers/{id}/status` | A W | Quản lý nhà cung cấp (tạo, sửa, ngừng hoạt động/sử dụng lại) |
| GET `/api/inventory/purchase-receipts`, `/api/inventory/purchase-receipts/{id}` | A M W | Tra cứu danh sách và chi tiết phiếu nhập kho |
| POST `/api/inventory/purchase-receipts` | A W | Nhận đủ ngay hoặc lập phiếu chờ giao, chống lặp theo `clientRequestId` |
| POST `/api/inventory/purchase-receipts/{id}/deliveries`, `/api/inventory/purchase-receipts/{id}/cancel-remainder` | A W | Nhận từng đợt hoặc đóng phần chưa nhận. Lưu người thao tác từ token. |
| POST `/api/inventory/serials/{id}/defective`, POST `/api/inventory/serials/{id}/restore` | A W | Báo lỗi thiết bị (tồn -1, movement `Defective`) và đưa thiết bị đã sửa về kho (tồn +1, movement `Adjustment`) |
| GET `/api/orders`, `/api/orders/{id}` | A M S | A/M see all; S sees only orders whose cashier is that user |
| POST `/api/orders`, PATCH `/api/orders/{id}/status` | A S | A writes all; S transitions only own orders; creator comes from token |
| GET `/api/customers`, `/api/customers/{id}` | A M S | S chỉ thấy khách đang hoạt động (server ép `isActive=true`); khách đã vô hiệu hóa trả 404 với S |
| POST `/api/customers` | A M S | Tạo khách tại quầy; điểm, hạng và trạng thái do server quyết định |
| GET `/api/customers/{id}/loyalty-points`, `/api/customers/{id}/orders`, `/api/customers/{id}/purchased-items` | A M | Lịch sử khách gồm đơn của thu ngân khác nên không mở cho S |
| PUT `/api/customers/{id}`, PATCH `/api/customers/{id}/deactivate`, `/api/customers/{id}/activate` | A M | Sửa liên hệ và trạng thái khách |
| POST `/api/pos/shifts`, GET `/api/pos/shifts/current`, POST `/api/pos/quote`, POST `/api/pos/checkout` | A S | Open own shift, read own open shift, quote a cart without writing an order, cash checkout into own open shift; actor from token |
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

Sprint 2 tiếp tục: kiểm tra trường giá vốn
Inventory theo role, landing page/menu và tách settings cá nhân. Giá vốn sản phẩm đã được che tại server theo `FieldAccess`.
Dashboard mở cho Admin/Manager (change `m3-manager-dashboard-access`). Customers theo bảng trên (change `m3-sales-customers`).

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
