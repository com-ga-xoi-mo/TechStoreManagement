## 1. Contract, options and schema

- [x] 1.1 `shared/`: DTO `VietQrPaymentDto` (id, orderId, orderCode, status, orderStatus, amount, transactionCode, bankBin, bankAccountNumber, bankAccountName, qrContent, qrImagePng, createdAt, expiresAt, secondsLeft, cashierUserId, cashierName), `VietQrBankEventDto`, `PosShiftDto.VietQrSalesTotal`; requests `CreateVietQrRequest` (như `PosCheckoutRequest` không có `CashTendered`, có `VoucherCode`, `ApplyVipDiscount`), `ConfirmVietQrRequest` (`ReferenceCode`, `ReceivedAmount`), `ResolveVietQrEventRequest` (`Note`), `VietQrWebhookRequest`, `VietQrSearchRequest`; enum `VietQrEventReason`, `VietQrEventStatus`
- [x] 1.2 `VietQrOptions` (`BankBin`, `AccountNumber`, `AccountName`, `WebhookSecret`, `HoldMinutes`=15) bind từ mục `VietQr`, đăng ký trong `Program.cs`, thêm mục trống vào `backend/appsettings.example.json`
- [x] 1.3 Entity + configuration `VietQrBankEvent` (`vietqr_bank_events`), chỉ mục `uq_vietqr_pending_order` trên `vietqr_transactions`, check ràng buộc trạng thái; migration `AddVietQrSettlement`; cập nhật `docs/database/schema.dbml`
- [x] 1.4 Test PostgreSQL schema: hai QR `Pending` cùng đơn bị chặn, trùng `(provider, transactionId)` bị chặn ở bảng sự kiện, `dotnet ef migrations has-pending-model-changes` sạch

## 2. Hoàn tất bán dùng chung

- [x] 2.1 `ISaleSettlementService`/`SaleSettlementService` theo design D2 (ConfirmSale + UnitCost, `IVoucherService.RedeemAsync`, `StageSaleAsync` điểm, trạng thái `Completed`, cộng `cash_sales_total` hoặc `vietqr_sales_total`), idempotent theo đơn, không mở transaction
- [x] 2.2 `PosCheckoutService` giữ phần riêng của Cash, thêm `IVoucherService.ReserveAsync` và gọi bước chung; sửa test cũ nếu cần, không đổi hành vi Cash ngoài việc tiêu lượt voucher
- [x] 2.3 Test đơn vị: bước chung với fake (thứ tự, lỗi từng bước rollback, gọi lại không đổi), Cash dùng voucher còn một lượt, hai checkout tranh lượt cuối trên PostgreSQL chỉ một thắng

## 3. Nội dung QR

- [x] 3.1 `VietQrPayload` (EMVCo VietQR + CRC16-CCITT-FALSE) và ảnh PNG trong `VietQrPayload.ToPngBase64` (QRCoder `PngByteQRCode`, base64); thêm gói `QRCoder` vào `TechStore.Api.csproj` với phiên bản cố định
- [x] 3.2 Test: vector chuẩn cho CRC, payload có đúng tag/độ dài/số tiền/memo, đổi một ký tự làm CRC sai, PNG hợp lệ (có chữ ký PNG), mã chuyển khoản chỉ gồm A-Z0-9 và duy nhất

## 4. Tạo, đọc, hủy, hết hạn

- [x] 4.1 `IVietQrPaymentService.CreateAsync` theo D3 (khóa ca, idempotent, giữ hàng, giữ voucher, QR 15 phút, 503 khi thiếu cấu hình ngân hàng); `GetAsync`, `SearchAsync` theo quyền; hết hạn lười khi đọc
- [x] 4.2 Tách lõi `VietQrHoldService` nhận `actor?`, gọi `IVoucherService.ReleaseAsync` thay vì trả 409 khi có voucher; giữ nguyên các kiểm tra còn lại của An và test hiện có của nó
- [x] 4.3 `VietQrExpiryWorker` (30 giây, tối đa 50 QR, mỗi QR một scope/transaction, log lỗi từng QR); đăng ký hosted service; có thể tắt bằng cấu hình cho test
- [x] 4.4 `VietQrController` (`POST /api/pos/vietqr`, `GET /api/pos/vietqr`, `GET /api/pos/vietqr/{id}`) và mở rộng route cancel/expire hiện có; ProblemDetails 400/403/404/409/503
- [x] 4.5 Test đơn vị với fake: tạo thành công, thiếu cấu hình, ca đóng/không phải của mình, thiết bị hết, voucher hết lượt, gửi lại cùng khóa, hủy hai lần, hủy sai chủ, hết hạn sớm/muộn

## 5. Xác nhận tiền và đối soát

- [x] 5.1 `IVietQrPaymentService.ConfirmAsync` theo D5 dùng chung cho thủ công và webhook; chống lặp theo `(provider, reference)`; chỉ một nhánh thắng so với hết hạn/hủy
- [x] 5.2 `POST /api/pos/vietqr/{id}/confirm` (Admin, Manager) với `ReferenceCode`, `ReceivedAmount`
- [x] 5.3 `POST /api/payments/vietqr/webhook` theo D7 (HMAC, cửa sổ thời gian, raw body, 16 KB, 503 khi thiếu secret, ghi sự kiện idempotent rồi khớp); thêm `[AllowAnonymous]` đúng một nơi và cập nhật guard phân quyền
- [x] 5.4 `VietQrBankEvent` repository/service, `GET /api/payments/vietqr/events`, `POST .../events/{id}/resolve` (Admin, Manager, ghi chú ≤ 500, giải quyết hai lần giữ lần đầu)
- [x] 5.5 Test đơn vị: sai số tiền/mã/tài khoản/chữ ký/thời gian, tham chiếu trùng, sự kiện cho từng lý do, thủ công thiếu tham chiếu, sai role

## 6. Ca

- [x] 6.1 `PosShiftService.CloseAsync` chặn 409 khi còn QR chờ (danh sách mã đơn), `PosShiftDto.VietQrSalesTotal`; tiền két không cộng QR
- [x] 6.2 Test: đóng ca có/không QR chờ, đóng sau khi QR kết thúc, `expectedClosingBalance` không đổi khi có QR, `vietqr_sales_total` đúng sau xác nhận

## 7. Quyền, tài liệu và công cụ

- [x] 7.1 `docs/API_PERMISSIONS.md`, baseline `ApiAuthorizationCoverageTests` (webhook là điểm ẩn danh duy nhất mới), test HTTP 401/403 cho từng route mới
- [x] 7.2 `backend/Sales/README.md` (luồng, thứ tự khóa, trạng thái, sandbox), `README.md` gốc (cấu hình `VietQr__*`), script `scripts/simulate-vietqr-webhook.ps1` ký HMAC và gửi một thông báo (tham số: mã chuyển khoản, số tiền, transactionId)
- [x] 7.3 Test sạch số tài khoản đầy đủ không xuất hiện trong log/lỗi của webhook

## 8. Client

- [x] 8.1 Model/repository `IVietQrRepository` (tạo, đọc, danh sách, hủy, xác nhận, sự kiện, giải quyết), test repository (đường dẫn, body, ánh xạ lỗi, JSON sai dạng)
- [x] 8.2 `PosViewModel`: chọn phương thức, tạo QR, khóa giỏ, đếm ngược theo `TimeProvider`, polling 3 giây hủy khi rời trang, hủy QR, biên nhận, khôi phục QR chờ khi mở lại trang; test headless gồm hết hạn, lỗi mạng khi poll, hủy, xác nhận trong lúc poll
- [x] 8.3 `PosPage`: lựa chọn phương thức, khung QR (ảnh, số tiền, nội dung chuyển khoản, ngân hàng, trạng thái, đếm ngược, nút Hủy); panel đóng ca nêu QR đang chờ
- [x] 8.4 Màn hình Admin/Manager (`VietQrReconcileViewModel`/`Page`): danh sách QR chờ + xác nhận (tham chiếu, số tiền), danh sách sự kiện cần đối soát + giải quyết; route, `RoleAccess`, menu, đăng ký DI; test headless
- [x] 8.5 Build `net10.0-desktop`, chạy toàn bộ test frontend

## 9. Kiểm chứng

- [x] 9.1 Test PostgreSQL HTTP đồng thời: xác nhận với hết hạn, xác nhận với hủy, xác nhận với đóng ca, tạo QR với đóng ca, webhook lặp song song, hai QR tranh thiết bị cuối, hai đơn tranh lượt voucher cuối; mất response rồi gửi lại; lỗi giữa chừng rollback toàn bộ
- [x] 9.2 Chạy toàn bộ test backend và frontend, `has-pending-model-changes`, đối chiếu tổng tiền ca (Cash, QR, hoàn) bằng một ca mẫu
- [x] 9.3 Cập nhật checkbox của Đạt trong `docs/MILESTONE_4_PLAN.md` chỉ cho mục có bằng chứng; ghi vào tài liệu bàn giao điều chưa chạy UI thật
