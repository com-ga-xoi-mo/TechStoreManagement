# Module Sales (Bán hàng tại quầy POS & Thanh toán)

## 1. Danh sách Thực thể (Entities - Kế thừa BaseEntity)
- `PosSession.cs` (`pos_sessions`): Quản lý ca thu ngân, đối soát tiền mặt đầu/cuối ca và tổng doanh thu ca.
- `VietQrTransaction.cs` (`vietqr_transactions`): Giao dịch thanh toán chuyển khoản ngân hàng động VietQR và đối soát webhook.

## 2. Các Enum Nghiệp vụ
- **`PosSessionStatus`**: `Open` (Ca đang hoạt động), `Closed` (Đã chốt ca, đối soát két tiền).
- **`VietQrStatus`**: `Pending` (Mã QR đã tạo, chờ chuyển khoản), `Confirmed` (Xác nhận thành công), `Failed` (Thất bại), `Expired` (Hết hạn giao dịch).

## 3. Quy ước Thiết kế & Ràng buộc Schema (Theo schema.dbml)
- **100% Khóa ngoại được đánh Index B-tree:**
  - `pos_sessions`: `cashier_user_id` (FK -> users.id).
  - `vietqr_transactions`: `order_id` (FK -> orders.id), `pos_session_id` (FK -> pos_sessions.id).
- **Ca làm việc (PosSession):**
  - Ràng buộc: `opening_balance >= 0`, `cash_sales_total >= 0`, `vietqr_sales_total >= 0`.
  - Indexes: `idx_pos_sessions_cashier_id`, `idx_pos_sessions_status`, `idx_pos_sessions_opened_at`.
- **Thanh toán VietQR động (VietQrTransaction):**
  - `transaction_code` là duy nhất trên toàn hệ thống (Unique index `idx_vietqr_tx_code`).
  - `amount > 0` kiểu `numeric(18, 2)` (khớp chính xác số tiền cần thanh toán của đơn hàng).
  - `bank_bin` (Mã BIN ngân hàng, ví dụ 970422 cho MBBank) và `bank_account_number` của cửa hàng.
  - `qr_content` lưu chuỗi EMVCo VietQR tiêu chuẩn để render mã QR hiển thị cho khách quét.
  - Khi ngân hàng callback webhook xác nhận tiền về: cập nhật `status = Confirmed`, ghi nhận `confirmed_at`, đồng thời cập nhật đơn hàng sang `Completed` và kích hoạt bảo hành.
- **Kiểu dữ liệu:** Toàn bộ số tiền dùng `numeric(18, 2)`, thời gian `opened_at`, `closed_at`, `confirmed_at` dùng `timestamptz` (UTC).

## 4. Cấu trúc Thành phần Module
- **`Configurations/`**: `PosSessionConfiguration.cs`, `VietQrTransactionConfiguration.cs`.
- **`Services/`**: `IPosService.cs`, `VietQrService.cs` (Sinh chuỗi EMVCo VietQR, đối soát webhook).
- **`Controllers/`**: `SalesController.cs` (Mở/đóng ca thu ngân, sinh mã thanh toán, kiểm tra trạng thái thanh toán đơn hàng).
