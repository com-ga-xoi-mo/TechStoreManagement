## Context

Có sẵn trong `dev`: checkout Cash (`PosCheckoutService`) tự làm đủ các bước bán; An đã viết `VietQrHoldService.CloseAsync` (hủy/hết hạn QR, nhả hàng qua `IInventoryAfterSaleService.ReleaseAsync` khóa theo id QR, từ chối khi đơn có voucher); `OrderService.UpdateStatus` chặn đơn VietQr khi chưa có xác nhận; bảng `vietqr_transactions` đã có khung (check xác nhận, `uq_vietqr_provider_transaction`, `uq_vietqr_confirmed_order`); `pos_sessions.vietqr_sales_total` đã có. Vy có `IVoucherService.ReserveAsync/RedeemAsync/ReleaseAsync` và `IRefundLoyaltyService.StageSaleAsync` nhưng voucher chưa ai gọi. Không có cấu hình ngân hàng, không có QR/webhook.

Ràng buộc: `Controller → Service → Repository`, repository không save, một nơi mở/commit transaction cho mỗi luồng, service được gọi chỉ ghi vào UnitOfWork của người gọi; tiền `numeric(18,2)`, giờ UTC; không tự cập nhật kho hay tính lại tiền/điểm/voucher.

## Goals / Non-Goals

**Goals:** bán VietQR đầy đủ (giữ → chờ → xác nhận/hủy/hết hạn), xác nhận tiền một nơi, tiền sai/muộn được lưu để đối soát, đóng ca chính xác, Cash và QR dùng chung bước hoàn tất bán, UI POS và màn hình đối soát.

**Non-Goals:** nhà cung cấp ngân hàng thật; hoàn tiền (An); công thức tiền/điểm/voucher (Vy); thao tác kho trực tiếp; QR cho đơn đổi hàng; Store settings; xử lý tiền khách chuyển thừa/thiếu (chỉ lưu và đối soát).

## Decisions

**D1. Thứ tự khóa toàn cục** (mọi luồng tuân theo): ca → đơn → các dòng QR của đơn → thiết bị (id tăng dần) → tồn kho (product, variant) → voucher → khách hàng. Khớp thứ tự An đang dùng trong `VietQrHoldService` (ca, đơn, QR) và luồng Cash (ca, thiết bị, kho). Luồng hoàn của An khóa đơn rồi ca nhưng chỉ chạm đơn đã `Completed`, còn các luồng QR chỉ chạm đơn `Pending`, nên không thể cùng một đơn và không tạo vòng chờ.

**D2. `SaleSettlement` dùng chung.** Một service nội bộ `ISaleSettlementService.CompleteAsync(order, shift, PaymentMethod, paidAt, actor)` làm đúng chuỗi: `ConfirmSaleAsync` → lưu `UnitCost` từng dòng → `IVoucherService.RedeemAsync` (nếu có voucher) → `StageSaleAsync` điểm (nếu có khách) → `Completed`/`PaidAt`/`CompletedAt` → cộng `cash_sales_total` hoặc `vietqr_sales_total`. Không mở transaction, không save. `PosCheckoutService` giữ phần riêng của Cash (kiểm tiền khách đưa, `ReserveAsync` hàng, `IVoucherService.ReserveAsync`, tiền thừa) rồi gọi bước chung; `VietQrPaymentService` gọi nó khi xác nhận. Thay thế cân nhắc: để QR gọi `OrderService.UpdateStatus` (hai lần chuyển, không có ca) hoặc chép chuỗi vào hai nơi (dễ lệch).

**D3. Tạo QR một transaction.** Khóa ca của người gọi → kiểm tra idempotency theo `(cashier, checkoutKey)` (đã có `uq_orders_cashier_checkout_key`, trả lại QR hiện có) → `PrepareAsync` giá/thuế/ưu đãi → tạo đơn `Pending`/`VietQr` gắn ca → `ReserveAsync` hàng → `IVoucherService.ReserveAsync` → ghi QR `Pending`, `expires_at = now + 15 phút`, `transaction_code` = `TS` + 10 ký tự chữ-số in hoa ngẫu nhiên (thử lại khi trùng `idx_vietqr_tx_code`). Mỗi đơn một QR chờ nhờ chỉ mục mới `uq_vietqr_pending_order` (`order_id` WHERE `status = 'Pending'`). `expectedTotal` khác tổng server → 409 như checkout Cash.

**D4. Nội dung QR.** Chuỗi EMVCo VietQR (chuẩn NAPAS): payload format `000201`, `010212` (động), merchant account info tag 38 (GUI `A000000727`, BIN + số tài khoản, dịch vụ `QRIBFTTA`), tag 53 `704`, tag 54 số tiền nguyên đồng, tag 58 `VN`, tag 62/08 nội dung chuyển khoản, tag 63 CRC16-CCITT-FALSE. Lớp `VietQrPayload` thuần, test với vector đã biết. Ảnh PNG sinh bằng `QRCoder.PngByteQRCode` (thuần managed, không cần System.Drawing), trả base64.

**D5. Xác nhận một nơi.** `IVietQrPaymentService.ConfirmAsync(ConfirmInput)` với `ConfirmInput { QrId? | TransferCode, Amount, Provider, ProviderTransactionId, ConfirmedByUserId?, ReceivedAt }`. Trong một transaction: khóa ca → đơn → các QR → kiểm tra theo thứ tự: QR tồn tại và khớp mã; chưa có `provider_transaction_id` này ở QR khác; QR còn `Pending` và `now < expires_at`; đơn còn `Pending`/`VietQr`; số tiền bằng đúng `qr.Amount` và `order.TotalAmount`; tài khoản nhận khớp cấu hình; ca còn `Open`. Đạt thì gán chứng cứ, `Confirmed`, gọi `SaleSettlement`, save, commit. Không đạt thì **không đổi đơn/ca** và (tùy lý do) ghi `vietqr_bank_events`.

**D6. Tiền không khớp là sự kiện, không phải lỗi mất.** Webhook luôn ghi `vietqr_bank_events` trước (idempotent theo `(provider, provider_transaction_id)` bằng unique index; gặp trùng thì trả 200 với kết quả đã lưu) rồi thử khớp. Lý do lưu: `LateAfterExpiry`, `LateAfterCancel`, `LateAfterShiftClose`, `AmountMismatch`, `Unmatched`, `AccountMismatch`, `DuplicateReference`, `SettlementFailed` (khớp nhưng bước hoàn tất bán bị từ chối, ví dụ hết hàng). Nếu khớp và hợp lệ thì sự kiện ghi `Applied` cùng QR. Hết hạn/hủy/đóng ca **không** được sửa ca đã đóng. Xác nhận thủ công khi bị từ chối trả lỗi 400/409 cho người dùng (không tạo sự kiện vì người dùng đang tự kiểm chứng), riêng quá hạn thì ghi sự kiện `LateAfterExpiry` để không mất bằng chứng.

**D7. Webhook sandbox.** Tên `provider = "sandbox"`. Ký `HMAC-SHA256(secret, X-Timestamp + "." + rawBody)` hex thường, so sánh bằng `CryptographicOperations.FixedTimeEquals`, đọc `rawBody` trước khi bind JSON, giới hạn kích thước 16 KB, cửa sổ thời gian 5 phút, chống phát lại bằng unique `(provider, transactionId)`. `VietQr:WebhookSecret` trống → endpoint trả 503. Response luôn nêu `"mode": "sandbox"`. Không log thân yêu cầu có số tài khoản đầy đủ.

**D8. Hết hạn và hủy.** Tách `VietQrHoldService.CloseAsync` thành lõi nhận `actor?` (null = hệ thống, bỏ kiểm tra vai trò/chủ). Lõi gọi thêm `IVoucherService.ReleaseAsync` trong cùng transaction thay vì trả 409 khi đơn có voucher. `VietQrExpiryWorker : BackgroundService` mỗi 30 giây lấy tối đa 50 QR `Pending` quá hạn và xử lý từng cái trong scope và transaction riêng, nuốt và log lỗi từng QR. `GET /api/pos/vietqr/{id}` cũng hết hạn lười nếu quá `expires_at`. Cả ba đường dùng một lõi nên idempotent nhờ khóa theo id QR của kho.

**D9. Đóng ca.** `PosShiftService.CloseAsync` sau khi khóa ca đếm QR `Pending` của ca (`IVietQrHoldRepository.ListPendingByShiftAsync`) và trả 409 kèm danh sách. Vì tạo/xác nhận/hết hạn đều khóa ca trước nên không có khe giữa kiểm tra và đóng.

**D10. Cấu hình.** `VietQrOptions { BankBin, AccountNumber, AccountName, WebhookSecret, HoldMinutes = 15 }` bind từ mục `VietQr`, thêm vào `appsettings.example.json` (không giá trị thật) và README; thiếu một trong ba trường ngân hàng thì tạo QR trả 503. Bí mật chỉ qua `.env`/biến môi trường.

**D11. UI.** `PosViewModel` thêm `PaymentMethod` (Cash/VietQr), `PendingQr`, bộ đếm ngược dùng `TimeProvider` (để test được), polling 3 giây bằng một `PeriodicTimer` do ViewModel giữ và hủy trong `OnDeactivated`; trạng thái pending lưu qua `PosDraftStore`/lấy lại từ server khi mở trang (`GET` QR chờ của ca hiện tại: `GET /api/pos/vietqr?status=Pending&shiftId=`). Ảnh QR bind từ `byte[]`. Màn hình Admin/Manager là route mới dưới Orders, ẩn bằng `RoleAccess`.

## Risks / Trade-offs

- [Polling thay vì đẩy] → Chấp nhận 3 giây; có hết hạn lười và worker nên không phụ thuộc client.
- [Hai nơi hết hạn (worker và đọc)] → Cùng lõi, khóa ca, kiểm tra trạng thái dưới khóa nên một nhánh thắng.
- [Webhook sandbox không phải nhà cung cấp thật] → Ghi rõ trong response, README và tài liệu bàn giao; đổi nhà cung cấp chỉ cần thêm bộ đọc payload và `provider`.
- [`QRCoder` là thêm dependency] → Gói MIT, thuần managed; giới hạn dùng ở một lớp.
- [Giữ hàng 15 phút làm kẹt hàng cuối] → Hủy thủ công và hết hạn tự động; lượt giữ nhả đúng một lần.
- [Xác nhận thủ công dựa vào người nhập] → Bắt buộc mã tham chiếu và số tiền, unique theo `(provider, reference)`, ghi người xác nhận.

## Migration Plan

Migration `AddVietQrSettlement`: bảng `vietqr_bank_events` (+ chỉ mục, unique `(provider, provider_transaction_id)`, check trạng thái), chỉ mục `uq_vietqr_pending_order`, thêm cột `bank_account_name` cho `vietqr_transactions` nếu cần hiển thị lại. Chỉ thêm, không đổi dữ liệu cũ. Cập nhật `schema.dbml`. Rollback: `dotnet ef migrations remove` trước khi áp dụng; sau khi áp dụng, hạ xuống migration trước (các bảng mới chỉ chứa dữ liệu QR).
