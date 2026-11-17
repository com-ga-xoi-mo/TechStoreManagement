## Why

M3 cần luồng bán tiền mặt tại quầy: mở ca → quét hàng → thu tiền → trừ kho/bảo hành → đóng ca đối soát. Hiện `Sales` chỉ có entity `PosSession`/`VietQrTransaction`, chưa có service, controller hay UI; Orders mới tạo đơn `Pending` rồi chuyển trạng thái bằng hai request, chưa gắn ca và chưa chống ghi nhận thanh toán lặp. Phần này thuộc Đạt, Sprint 4 của M3.

## What Changes

- Thêm module `Sales` vận hành: `IPosShiftService` (mở/đóng ca, xem ca hiện tại, danh sách ca) và `IPosCheckoutService` (checkout tiền mặt) cùng repository, controller `POST /api/pos/shifts`, `POST /api/pos/shifts/{id}/close`, `GET /api/pos/shifts/current`, `GET /api/pos/shifts`, `POST /api/pos/checkout`.
- `POST /api/pos/checkout` tạo đơn, thu tiền mặt, tính tiền thừa, giữ rồi trừ kho, ghi giá vốn, kích hoạt bảo hành, cộng `cash_sales_total` của ca và hoàn tất đơn trong **một transaction**; đi thẳng `Pending → Completed` với `paid_at` và `completed_at` ghi một lần. Có khóa idempotency do client sinh: gửi lại cùng khóa trả lại đúng đơn cũ, không ghi tiền hay trừ kho lần hai.
- Mỗi người bán chỉ có một ca `Open` (partial unique index); đơn POS gắn `pos_session_id` của chính người bán. Chặn bán vào ca đã đóng hoặc ca của người khác. Đóng ca ghi tiền dự kiến, tiền thực đếm, chênh lệch và lý do (bắt buộc khi chênh lệch khác 0). Manager/Admin xem mọi ca và đóng cưỡng bức.
- Thêm cột: `orders.cash_tendered`, `orders.change_given`, `orders.checkout_key`; `pos_sessions.expected_closing_balance`, `closing_note`, `closed_by_user_id`. Cập nhật `schema.dbml`, migration, DTO/request/enum dùng chung.
- Giữ nguyên `POST /api/orders` và `PATCH /api/orders/{id}/status` cho đơn nháp: hủy đơn `Pending` và các bước chuyển hợp lệ vẫn như cũ; đơn đã trả tiền không bị sửa hay hủy, không cho đánh dấu QR đã trả khi chưa có xác nhận.
- UI C# Markup: trang POS (quét barcode/Serial/IMEI, thêm/xóa dòng, đổi số lượng, chọn khách hoặc khách lẻ, nhận tiền, hiển thị tiền thừa), dialog mở ca và đóng ca, chi tiết đơn hiển thị đủ mã, ngày, khách, mặt hàng, đơn giá, giảm giá, thuế, tổng, phương thức, trạng thái, tiền khách đưa và tiền thừa.
- Chỉ gọi `OrderPricingHelper` (phần của Vy; thuế và giảm giá cấp đơn hiện bằng 0) và `IInventoryFulfillmentService` (phần của Q. Vinh); không viết lại công thức tiền hay logic kho.
- Bảng quyền `docs/API_PERMISSIONS.md`, baseline guard `ApiAuthorizationCoverageTests`, README `Sales`, test đồng thời trên PostgreSQL.

## Capabilities

### New Capabilities

- `pos-shift-management`: mở/đóng ca thu ngân, một ca `Open` mỗi người bán, đối soát tiền đầu ca, tiền dự kiến, tiền thực đếm và chênh lệch, quyền xem/đóng ca.
- `pos-cash-checkout`: checkout tiền mặt nguyên tử gắn ca, tiền khách đưa/tiền thừa, idempotency, ghi tiền ca một lần, rollback toàn bộ khi lỗi.
- `frontend-pos-terminal`: trang POS và dialog mở/đóng ca trong client, trạng thái loading/rỗng/lỗi, theo token theme.

### Modified Capabilities

- `order-management`: đơn POS gắn ca và người bán, đơn đã thanh toán bị đóng băng (không sửa/xóa/hủy), DTO đơn bổ sung phiên ca, tiền khách đưa và tiền thừa.
- `api-role-authorization`: thêm endpoint `/api/pos/*` vào bảng quyền (`SalesStaff` bán và quản ca của mình, `Manager`/`Admin` xem mọi ca và đóng cưỡng bức, `WarehouseStaff` bị chặn).
- `frontend-order-management`: chi tiết đơn hiển thị tiền khách đưa và tiền thừa.

## Impact

- Backend: `backend/Sales/` (Services, Repositories, Controller, Configurations), `backend/Orders/` (tách bước dựng đơn và hoàn tất để dùng chung trong một transaction, thêm cột), `backend/Common/Data/` (migration, `Program.cs` đăng ký DI), seed ca mẫu.
- Shared: `shared/DTOs`, `shared/Requests`, `shared/Enums` (DTO ca, request mở/đóng ca và checkout).
- Frontend: `frontend/TechStore.Client/` (Repositories/Sales, ViewModels/Sales, Views/Sales, route và menu cho `SalesStaff`/`Admin`/`Manager`).
- Tests: `tests/Backend.UnitTests` (service, validator, PostgreSQL đồng thời, guard quyền), `tests/Frontend.UnitTests` (ViewModel, repository).
- Docs: `docs/database/schema.dbml`, `docs/API_PERMISSIONS.md`, `docs/MILESTONE_3_PLAN.md`, `backend/Sales/README.md`.
- Không thuộc change này: VietQR, hoàn tiền/đổi trả, điểm thưởng/voucher (M4); công thức thuế/giảm giá cấp đơn (Vy); nhập hàng và logic kho (Q. Vinh); tìm kiếm sản phẩm/IMEI phía Catalog (T. Vinh). Migration cần ghép theo thứ tự với An để tránh xung đột snapshot.
