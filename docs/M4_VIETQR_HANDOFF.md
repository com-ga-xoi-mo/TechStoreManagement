# M4 VietQR và ca thu ngân: bàn giao

Phạm vi: phần M4 của Đạt. Hợp đồng chi tiết nằm ở `openspec/changes/m4-vietqr-settlement/`; luồng và API ở [backend/Sales/README.md](../backend/Sales/README.md) mục 6; quyền ở [API_PERMISSIONS.md](API_PERMISSIONS.md).

## Đã làm
- **Bước hoàn tất bán dùng chung** (`ISaleSettlementService`): trừ kho + giá vốn + bảo hành (Inventory của An), tiêu lượt voucher (Vy), cộng điểm (Vy), cộng tiền ca. Cash và QR cùng gọi, nên không còn hai bản logic.
- **Checkout tiền mặt tiêu voucher.** Trước đây không ai gọi `ReserveAsync/RedeemAsync` nên lượt voucher không bao giờ bị tiêu; nay `PosCheckoutService` giữ rồi tiêu lượt.
- **Tạo QR** (`POST /api/pos/vietqr`): đơn `Pending`, giữ hàng/IMEI và voucher 15 phút, nội dung EMVCo VietQR có CRC, ảnh PNG sinh ở backend (QRCoder), idempotent theo `checkoutKey`.
- **Xác nhận tiền:** thủ công (Admin/Manager, `provider = manual`) và webhook **sandbox** ký HMAC-SHA256 (`provider = sandbox`), cùng một service. Chống lặp theo `(provider, provider_transaction_id)`.
- **Tiền không hoàn tất được đơn** (đến muộn, sai tiền/tài khoản/mã, lặp, lỗi hoàn tất) lưu ở `vietqr_bank_events` để Admin/Manager đối soát; không sửa ca hay đơn cũ.
- **Hết hạn/hủy** nhả hàng và lượt voucher đúng một lần; có hết hạn lười khi đọc và `VietQrExpiryWorker` (30 giây).
- **Đóng ca** bị chặn 409 khi còn QR chờ; tiền két chỉ gồm Cash, QR hiện ở `vietQrSalesTotal`.
- **Client:** `IVietQrRepository`, chọn Tiền mặt/VietQR trên POS, khung QR có đếm ngược và polling 3 giây (hủy khi rời trang), khôi phục QR chờ khi mở lại, trang **Đối soát VietQR** (nút ở danh sách đơn, chỉ Admin/Manager).

## Cấu hình
`VietQr__BankBin`, `VietQr__AccountNumber`, `VietQr__AccountName`, `VietQr__WebhookSecret` (xem README gốc). Thiếu thông tin ngân hàng: tạo QR trả 503. Thiếu khóa: webhook trả 503. Mô phỏng ngân hàng: `scripts/simulate-vietqr-webhook.ps1`.

## Kiểm chứng
- Backend: 1253/1254 test qua (22 test HTTP PostgreSQL mới cho QR/voucher: tạo, xác nhận, webhook lặp song song, sai chữ ký/thời gian, tiền đến muộn, xác nhận tranh với hủy/đóng ca, hai QR tranh thiết bị cuối, tranh lượt voucher cuối, rollback giữa chừng). `dotnet ef migrations has-pending-model-changes` sạch.
- Frontend: 1372/1372 test headless qua; build `net10.0-desktop` thành công.
- Một test cũ của Q. Vinh (`WarrantyCase_*` trong `PosCheckoutPostgresHttpTests.Warranty.cs`) đôi khi lỗi trên Windows vì độ chính xác thời gian (100 ns so với µs của PostgreSQL). Không thuộc phần này; bản sửa nằm ở nhánh `fix/warranty-timestamp-precision` (chưa đẩy).

## Chưa làm / cần biết
- **UI chưa chạy thử trực tiếp** (POS có QR, trang đối soát, ảnh QR, đổi Sáng/Tối). Cần chạy app và kiểm các điểm ghi trong AGENTS.md: binding path, theme, ảnh QR hiển thị trên Skia và Windows.
- Webhook là **sandbox**, chưa nối nhà cung cấp ngân hàng thật. Chữ ký, thời gian và giới hạn thân đã đủ để thay bằng nhà cung cấp thật sau.
- Luồng đổi trạng thái thủ công qua `PATCH /api/orders/{id}/status` (Admin) vẫn không tiêu voucher; chỉ POS Cash/QR tiêu. Cần quyết định riêng nếu muốn gộp.
- Đơn QR đi thẳng `Pending → Completed` khi tiền về (hàng giao tại quầy), không qua `Processing`.
- Mã tham chiếu đã dùng hoặc đơn đã trả bằng giao dịch khác được ghi là sự kiện `DuplicateReference`, không phải lỗi.
