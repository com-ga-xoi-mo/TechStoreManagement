# Tasks

## 1. Công thức thuần (TDD: test trước)

- [ ] 1.1 `LoyaltyRules`: `PointsFor`, `TierFor`, `DiscountPercentFor`, `NextTier`; test mốc 499/500/1.499/1.500/4.999/5.000, 99.999/100.000, ví dụ 18 điểm.
- [ ] 1.2 `OrderDiscountCalculator`: VIP, voucher phần trăm/số tiền, trần giảm, không vượt tiền hàng, điều kiện voucher; test từng ca.

## 2. Lưu trữ

- [ ] 2.1 Entities/configurations: `reference_key`, `reserved_count`, `VoucherRedemption`, `Order.DiscountSource`, enum `OrderDiscountSource`.
- [ ] 2.2 Migration bằng `dotnet ef migrations add AddLoyaltyVoucherLifecycle`, cập nhật `schema.dbml`, kiểm `has-pending-model-changes`.

## 3. Service

- [ ] 3.1 Repository voucher/loyalty (khóa dòng `FOR UPDATE`), fake tương ứng cho test.
- [ ] 3.2 `LoyaltyService`: cộng điểm, đảo điểm, tóm tắt; test lặp, số dư không âm, đổi hạng hai chiều.
- [ ] 3.3 `VoucherService`: quản lý, `Reserve`/`Redeem`/`Release`; test lặp, hết hạn, hết lượt, không trả lượt đã tiêu.
- [ ] 3.4 `OrderService.PrepareAsync`: một nguồn ưu đãi, quyền giảm giá cách A, lưu `discountSource`/`voucherId`; test các ca spec.

## 4. API

- [ ] 4.1 DTO/request trong `shared`, `VouchersController`, `loyalty-summary`, quyền, `docs/API_PERMISSIONS.md`, `ApiAuthorizationCoverageTests`.
- [ ] 4.2 DI trong `Program.cs`; `backend/Customers/README.md` ghi cách gọi cho Đạt/An, lỗi trả về, ví dụ M4.

## 5. Kiểm thử (AI tự chạy, ghi lệnh và kết quả thật)

- [ ] 5.1 Build 0 warning, backend và frontend unit tests.
- [ ] 5.2 PostgreSQL thật: migration trên dữ liệu cũ, ràng buộc DB, hai giao dịch đồng thời tranh lượt voucher cuối, đảo điểm theo ví dụ M4.
- [ ] 5.3 HTTP thật: quyền theo bốn role, tạo đơn với VIP/voucher/thủ công, hai nguồn cùng lúc, tóm tắt điểm.

## 6. Hoàn tất

- [ ] 6.1 `openspec validate --strict`, archive change, PR vào `dev` (tiêu đề và mô tả tiếng Anh, không co-author).
