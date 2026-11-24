# Tasks

## 1. Test trước (TDD)

- [x] 1.1 `DemoSeedVoucherTests`: manifest có mã duy nhất, thỏa ràng buộc schema, đủ bốn trạng thái và cả hai loại giảm.
- [x] 1.2 Seed tạo đúng từng voucher một lần, chạy lại không đổi; sửa/tắt/dùng `DEMO-OCT26` rồi seed lại không lỗi và giữ nguyên; xóa một dòng thì chỉ dòng đó được thêm lại; hai dòng cùng mã báo lỗi rõ.

## 2. Cài đặt

- [x] 2.1 `DemoSeedManifest.Vouchers` và `VoucherSeed`.
- [x] 2.2 `EnsureVouchersAsync` chỉ thêm dòng thiếu; bỏ so sánh giá trị/trạng thái/lượt dùng.

## 3. Kiểm tra

- [x] 3.1 Backend unit: `DemoSeed*` 15/15; toàn bộ 973 đạt, 80 bỏ qua (cần Postgres, như trước), 0 lỗi.
- [x] 3.2 PostgreSQL thật (DB `techstore_seed_test`, schema riêng): `DataSeederPostgresIntegrationTests` 3/3, thêm kiểm số voucher `DEMO-*`; các ràng buộc `expires_at > created_at` và `used_count ≤ usage_limit` chấp nhận mọi trạng thái.
- [x] 3.3 Chạy backend lên DB dev đã có sẵn dữ liệu: năm voucher có mặt, voucher cũ không đổi.

## 4. Tài liệu

- [x] 4.1 `backend/Customers/README.md`: bảng voucher demo và quy tắc "đã có thì giữ nguyên".
- [x] 4.2 Sync spec `data-seeding` và archive change.
