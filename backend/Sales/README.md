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
  - Ràng buộc: `opening_balance >= 0`, `cash_sales_total >= 0`, `cash_refund_total >= 0`, `vietqr_sales_total >= 0`.
  - Indexes: `idx_pos_sessions_cashier_id`, `idx_pos_sessions_status`, `idx_pos_sessions_opened_at`.
- **Thanh toán VietQR động (VietQrTransaction):**
  - `transaction_code` là duy nhất trên toàn hệ thống (Unique index `idx_vietqr_tx_code`).
  - `amount > 0` kiểu `numeric(18, 2)` (khớp chính xác số tiền cần thanh toán của đơn hàng).
  - `bank_bin` (Mã BIN ngân hàng, ví dụ 970422 cho MBBank) và `bank_account_number` của cửa hàng.
  - `qr_content` lưu chuỗi EMVCo VietQR tiêu chuẩn để render mã QR hiển thị cho khách quét.
  - M3 chỉ cho chuyển đơn VietQR sang `Processing` hoặc `Completed` khi có giao dịch `Confirmed` đúng đơn, đúng tổng, có thời điểm xác nhận.
  - M4: xác nhận tiền (thủ công hoặc webhook sandbox) cập nhật `status = Confirmed`, `confirmed_at`, đơn sang `Completed` và kích hoạt bảo hành trong cùng một transaction (xem mục 6).
- **Kiểu dữ liệu:** Toàn bộ số tiền dùng `numeric(18, 2)`, thời gian `opened_at`, `closed_at`, `confirmed_at` dùng `timestamptz` (UTC).

## 4. Cấu trúc Thành phần Module
- **`Configurations/`**: `PosSessionConfiguration.cs`, `VietQrTransactionConfiguration.cs`.
- **`Repositories/`**: `IPosSessionRepository` / `PosSessionRepository` (ca Open của người bán, khóa dòng `FOR UPDATE`, tìm kiếm phân trang).
- **`Services/`**: `IPosShiftService` / `PosShiftService` (mở, đọc, đóng ca có đối soát), `IPosCheckoutService` / `PosCheckoutService` (checkout tiền mặt), `PosResult<T>`. `IVietQrPaymentService` (tạo/đọc QR), `IVietQrConfirmationService` (xác nhận thủ công, webhook sandbox, đối soát), `IVietQrHoldService` (hủy/hết hạn), `ISaleSettlementService` (bước hoàn tất bán dùng chung Cash và QR), `VietQrExpiryWorker` (quét QR quá hạn).
- **Controller** (`PosController.cs`, đặt tại thư mục gốc của module): `/api/pos/*`.

## 5. API POS tiền mặt (M3)
|Endpoint|Quyền|Ghi chú|
|:---|:---|:---|
|`POST /api/pos/quote`|Admin, SalesStaff|Định giá giỏ theo giá hiện hành, giảm dòng, `orderDiscountAmount` và VAT. Trả `subtotal`, `discountAmount`, `taxAmount`, `totalAmount`. Không ghi đơn hoặc giữ hàng.|
|`POST /api/pos/shifts`|Admin, Manager, SalesStaff|Mở ca với `openingBalance` (≥ 0, 2 chữ số thập phân). Mỗi người một ca `Open` (index `uq_pos_sessions_cashier_open`); mở lần hai trả 409.|
|`GET /api/pos/shifts/current`|Admin, SalesStaff|Ca `Open` của chính mình hoặc 404.|
|`GET /api/pos/shifts`, `GET /api/pos/shifts/{id}`|Admin, Manager, SalesStaff|Admin/Manager xem mọi ca; SalesStaff chỉ ca của mình (ca người khác trả 404).|
|`POST /api/pos/shifts/{id}/close`|Admin, Manager, SalesStaff|Đóng ca với `closingBalance` (tiền thực đếm) và `note`. Tiền dự kiến = `openingBalance + cashSalesTotal - cashRefundTotal`; `note` bắt buộc khi chênh lệch khác 0. SalesStaff chỉ đóng ca của mình; Manager/Admin đóng được mọi ca và `closedByUserId` ghi người đóng.|
|`POST /api/pos/checkout`|Admin, SalesStaff|Bán tiền mặt vào ca `Open` của chính người gọi. 201 khi tạo mới, 200 khi `checkoutKey` đã được lưu.|

### Luồng checkout (một transaction)
1. Kiểm tra đầu vào (`checkoutKey`, `posSessionId`, `cashTendered`), tra `checkoutKey` đã lưu (trả lại đơn cũ, `Replayed`).
2. `IOrderService.PrepareAsync`: validate và định giá phía server qua `OrderPricingHelper` (giá client không được nhận); `cashTendered` phải ≥ tổng đơn. Quầy gửi `expectedTotal` từ lần định giá cuối. Tổng thay đổi trả 409 để thu ngân xem lại trước khi thu.
3. `BeginTransaction`, khóa dòng ca (`FOR UPDATE`); sai chủ → 404, ca không `Open` → 409; tra lại `checkoutKey` dưới khóa.
4. Sinh mã đơn, `ReserveAsync` rồi `ConfirmSaleAsync` (Inventory, Q. Vinh): giữ hàng, trừ kho, ghi lịch sử, lưu giá vốn, kích hoạt bảo hành.
5. Đơn sang `Completed` (`PaidAt = CompletedAt`), lưu `cashTendered`, `changeGiven = cashTendered - total`, `posSessionId`, `checkoutKey`; `cash_sales_total += total` (không cộng tiền thừa).
6. `SaveChanges` + `Commit`. Bất kỳ lỗi nào: rollback toàn bộ (đơn, giữ hàng, lịch sử, tiền ca, khóa).

**Thứ tự khóa:** dòng ca → thiết bị (id tăng dần) → tồn kho (product, variant). Đóng ca chỉ khóa dòng ca nên bán và đóng ca không thể chờ vòng nhau; sau khi đóng, checkout vào ca đó trả 409, bán trước đó được tính vào tiền dự kiến.

**Idempotency:** `checkoutKey` (GUID do client sinh khi bấm Thanh toán, giữ khi thử lại) + unique index một phần `uq_orders_cashier_checkout_key (cashier_user_id, checkout_key)`. Server không so khớp nội dung với lần gửi trước; client đổi giỏ phải sinh khóa mới.

Đơn đã thanh toán (`Completed`) không thể hủy hay sửa qua `/api/orders`; hoàn tiền đi theo quy trình M4. Đơn nháp `POST /api/orders` vẫn dùng như cũ cho Admin.

## 6. VietQR và đối soát (M4)
|Endpoint|Quyền|Ghi chú|
|:---|:---|:---|
|`POST /api/pos/vietqr`|Admin, SalesStaff|Tạo yêu cầu QR trong ca `Open` của chính người gọi: tạo đơn `Pending`, giữ hàng/IMEI và lượt voucher 15 phút. 201 khi tạo mới, 200 khi `checkoutKey` đã có. 503 khi thiếu `VietQr:BankBin/AccountNumber/AccountName`.|
|`GET /api/pos/vietqr`, `GET /api/pos/vietqr/{id}`|Admin, Manager, SalesStaff|SalesStaff chỉ thấy của mình (404 với của người khác). Đọc QR quá hạn sẽ hết hạn nó (hết hạn lười). Danh sách không kèm ảnh QR.|
|`POST /api/pos/vietqr/{id}/cancel`, `/expire`|Admin, SalesStaff|Kết thúc QR chờ, nhả hàng và lượt voucher đúng một lần.|
|`POST /api/pos/vietqr/{id}/confirm`|Admin, Manager|Xác nhận thủ công: `referenceCode` (mã tham chiếu sao kê) và `receivedAmount` phải khớp tuyệt đối. `provider = manual`.|
|`POST /api/payments/vietqr/webhook`|Công khai, bắt buộc chữ ký|**Sandbox**, chưa nối ngân hàng thật. Header `X-Timestamp` (giây Unix, lệch tối đa 5 phút) và `X-Signature` = hex HMAC-SHA256 của `"<timestamp>.<thân thô>"` bằng `VietQr:WebhookSecret`. Thân tối đa 16 KB. Thiếu secret trả 503, chữ ký sai/quá hạn trả 401. `provider = sandbox`.|
|`GET /api/payments/vietqr/events`, `POST .../events/{id}/resolve`|Admin, Manager|Tiền không hoàn tất được đơn (đến muộn, sai số tiền/tài khoản, không khớp mã, lặp, lỗi hoàn tất) để đối soát; ghi chú xử lý bắt buộc, giải quyết hai lần giữ lần đầu.|

**Trạng thái QR:** `Pending` → `Confirmed` | `Failed` (hủy) | `Expired`. Mỗi đơn tối đa một QR `Pending` (`uq_vietqr_pending_order`) và một `Confirmed` (`uq_vietqr_confirmed_order`).

**Xác nhận:** trong một transaction, khóa ca → đơn → các QR của đơn, kiểm QR còn `Pending`, chưa quá hạn, ca còn `Open`, tiền khớp, tham chiếu chưa dùng `(provider, provider_transaction_id)`. Sau đó `ISaleSettlementService` hoàn tất bán (đơn thẳng `Completed`, `vietqr_sales_total += total`, không động vào tiền két) rồi QR sang `Confirmed`. Tiền không hoàn tất được đơn **không** sửa ca/đơn cũ mà lưu vào `vietqr_bank_events` (`NeedsReconciliation`).

**Thứ tự khóa chung:** ca → đơn → QR của đơn → thiết bị → tồn kho → voucher → khách. Xác nhận, hủy, hết hạn và đóng ca cùng đi theo thứ tự này nên chỉ một nhánh thắng.

**Đóng ca:** bị chặn 409 và nêu mã đơn khi còn QR `Pending` của ca. Tiền dự kiến = đầu ca + Cash − hoàn Cash; QR hiện riêng ở `vietQrSalesTotal`.

**Cấu hình:** `VietQr__BankBin`, `VietQr__AccountNumber`, `VietQr__AccountName`, `VietQr__WebhookSecret` (bí mật, không commit), `VietQr__HoldMinutes` (15), `VietQr__ExpirySweepSeconds` (30, `0` tắt worker). Mô phỏng ngân hàng: `scripts/simulate-vietqr-webhook.ps1`.
