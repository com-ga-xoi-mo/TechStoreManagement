## Why

Phần M4 của Đạt cần bán bằng VietQR và đối soát ca. Hiện trong `dev` chỉ có nửa cuối: An đã làm hủy/hết hạn QR (nhả giữ hàng). Chưa có tạo QR, giữ hàng/voucher, xác nhận tiền, webhook, chặn đóng ca còn QR chờ, lưu tiền đến muộn. Rà soát còn thấy checkout Cash áp giảm giá voucher nhưng không giữ/tiêu lượt dùng, vì `IVoucherService.ReserveAsync/RedeemAsync/ReleaseAsync` của Vy chưa ai gọi.

## What Changes

- Tạo QR: `POST /api/pos/vietqr` giữ hàng/IMEI và lượt voucher 15 phút, tạo đơn `Pending` phương thức `VietQr` gắn ca của người bán và một dòng `vietqr_transactions` đang chờ; trả nội dung EMVCo VietQR, ảnh PNG, mã chuyển khoản, ngân hàng và hạn. `GET /api/pos/vietqr/{id}` trả trạng thái chờ/thành công/thất bại/hết hạn.
- Xác nhận tiền một nơi duy nhất: `POST /api/pos/vietqr/{id}/confirm` (Admin/Manager, mã tham chiếu ngân hàng bắt buộc) và `POST /api/payments/vietqr/webhook` (sandbox ký HMAC-SHA256, chống phát lại) dùng chung service. Kiểm số tiền, mã chuyển khoản, QR còn chờ và chưa quá hạn, chống lặp theo `(provider, provider_transaction_id)`.
- Bước "hoàn tất bán" dùng chung cho Cash và QR: tiêu lượt voucher, trừ kho/giá vốn/bảo hành qua `IInventoryFulfillmentService`, cộng điểm, cộng tiền ca theo phương thức. QR xác nhận đưa đơn thẳng `Pending → Completed` (giao tại quầy). **Checkout Cash bắt đầu giữ và tiêu lượt voucher.**
- Hủy/hết hạn QR nhả hàng và lượt voucher đúng một lần (bỏ ràng buộc 409 khi có voucher). Hết hạn tự động bằng `BackgroundService` cùng logic với `/expire`.
- Tiền đến muộn, sai số tiền, lặp, hoặc sau hủy/hết hạn/đóng ca: không hoàn tất đơn, không sửa ca cũ; lưu vào bảng mới `vietqr_bank_events` để Admin/Manager đối soát.
- Đóng ca bị chặn khi còn QR chờ. Tiền két = đầu ca + Cash − hoàn Cash; QR cộng vào `vietqr_sales_total` và hiện riêng.
- UI C# Markup: POS chọn Tiền mặt/VietQR, hiện QR, đếm ngược, polling 3 giây, hủy; màn hình Admin/Manager xác nhận thủ công và danh sách tiền cần đối soát.
- Migration mới (`vietqr_bank_events`, chỉ mục một QR chờ mỗi đơn), `schema.dbml`, bảng quyền, baseline test quyền, README `Sales`, script PowerShell ký webhook sandbox.
- Thêm gói NuGet `QRCoder` vào backend (mã hóa QR không tự viết).

## Capabilities

### New Capabilities

- `vietqr-payment`: tạo QR có giữ hàng/voucher, trạng thái, hủy/hết hạn, xác nhận thủ công và webhook sandbox.
- `vietqr-reconciliation`: lưu và xử lý tiền ngân hàng không khớp, đến muộn hoặc lặp.
- `pos-sale-settlement`: bước hoàn tất bán dùng chung cho Cash và QR, gồm tiêu lượt voucher và cộng tiền ca theo phương thức.

### Modified Capabilities (thêm requirement mới bằng delta ADDED, không đổi requirement cũ)

- `pos-shift-management`: chặn đóng ca khi còn QR chờ; tiền QR tách khỏi tiền két.
- `pos-cash-checkout`: lượt voucher được giữ và tiêu cùng lúc nhận tiền.
- `frontend-pos-terminal`: thanh toán VietQR trên POS và màn hình xác nhận/đối soát.
- `api-role-authorization`: các endpoint `/api/pos/vietqr` và `/api/payments/vietqr`.

## Impact

- Backend: `backend/Sales/` (service, repository, controller, hosted service, options), `backend/Orders/` (dùng chung bước hoàn tất), `backend/Customers/` (chỉ gọi service của Vy), `backend/Common/Data/` (migration), `Program.cs`, `appsettings.example.json`.
- Shared: DTO/request cho QR, xác nhận, sự kiện ngân hàng.
- Frontend: `Repositories/Sales`, `ViewModels/Sales`, `Views/Sales`, route cho màn hình xác nhận/đối soát.
- Tests: `tests/Backend.UnitTests` (fake, PostgreSQL HTTP đồng thời), `tests/Frontend.UnitTests`.
- Docs: `schema.dbml`, `API_PERMISSIONS.md`, `backend/Sales/README.md`, `MILESTONE_4_PLAN.md` (chỉ tích mục có bằng chứng).
- Không làm: nhà cung cấp ngân hàng thật, hoàn tiền tự động (An), công thức tiền/điểm/voucher (Vy), thao tác kho trực tiếp (An), Store settings, QR cho đơn đổi hàng.
