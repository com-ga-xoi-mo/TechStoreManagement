# Tasks

## 1. Công thức thuần (TDD: test trước)

- [x] 1.1 `LoyaltyRules`: `PointsFor`, `TierFor`, `DiscountPercentFor`, `VipDiscount`, `NextTier`; test mốc 499/500/1.499/1.500/4.999/5.000, 99.999/100.000, ví dụ 18 điểm.
- [x] 1.2 `VoucherRules.Evaluate`: voucher phần trăm/số tiền, trần giảm, không vượt tiền hàng, điều kiện (hoạt động, hạn, đơn tối thiểu, lượt); test từng ca.

## 2. Lưu trữ

- [x] 2.1 Entities/configurations: `reference_key`, `reserved_count`, `VoucherRedemption`, `Order.DiscountSource`, enum `OrderDiscountSource`.
- [x] 2.2 Migration bằng `dotnet ef migrations add AddLoyaltyVoucherLifecycle`, cập nhật `schema.dbml`, kiểm `has-pending-model-changes`.

## 3. Service

- [x] 3.1 Repository voucher/loyalty (khóa dòng `FOR UPDATE`), fake tương ứng cho test.
- [x] 3.2 `LoyaltyService`: cộng điểm, đảo điểm, tóm tắt; test lặp, số dư không âm, đổi hạng hai chiều.
- [x] 3.3 `VoucherService`: quản lý, `Reserve`/`Redeem`/`Release`; test lặp, hết hạn, hết lượt, không trả lượt đã tiêu.
- [x] 3.4 `OrderService.PrepareAsync`: một nguồn ưu đãi, quyền giảm giá cách A, lưu `discountSource`/`voucherId`; test các ca spec.

## 4. API

- [x] 4.1 DTO/request trong `shared`, `VouchersController`, `loyalty-summary`, quyền, `docs/API_PERMISSIONS.md`, `ApiAuthorizationCoverageTests`.
- [x] 4.2 DI trong `Program.cs`; `backend/Customers/README.md` ghi cách gọi cho Đạt/An, lỗi trả về, ví dụ M4.

## 5. Kiểm thử (AI tự chạy, ghi lệnh và kết quả thật)

- [x] 5.1 Build 0 warning, backend và frontend unit tests.
- [x] 5.2 PostgreSQL thật: migration trên dữ liệu cũ, ràng buộc DB, hai giao dịch đồng thời tranh lượt voucher cuối, đảo điểm theo ví dụ M4.
- [x] 5.3 HTTP thật: quyền theo bốn role, tạo đơn với VIP/voucher/thủ công, hai nguồn cùng lúc, tóm tắt điểm.

## 6. Hoàn tất

- [x] 6.1 `openspec validate --strict`, rồi archive change.

## Kết quả kiểm thử (AI, 2026-11-23, Windows 11)

- 5.1 `dotnet build TechStore.sln`: 0 warning, 0 error (gồm `Frontend.IntegrationTests`). Backend unit có PostgreSQL 16 (Docker, `TECHSTORE_TEST_CONNECTION`, fixture tự tạo/xóa DB): 1048 Pass / 0 Fail / 0 Skipped. Frontend unit: 1141 Pass / 0 Fail / 0 Skipped. `Frontend.IntegrationTests` (live, opt-in) chỉ build, chưa chạy. **Pass**
- 5.2 PostgreSQL thật: migration `AddLoyaltyVoucherLifecycle` áp lên dữ liệu cũ (khách 700 điểm, 2 dòng sổ điểm, voucher dùng 3/5, đơn có voucher) giữ nguyên mọi giá trị, cột mới có mặc định (`reserved_count` 0, `reference_key` null, `discount_source` None); ràng buộc `reference_key` duy nhất, `used_count + reserved_count <= usage_limit`, một dòng sử dụng mỗi đơn đều bị PostgreSQL từ chối khi vi phạm. Giao dịch đồng thời (mỗi bên một transaction và một context): 2 đơn tranh lượt cuối chỉ 1 thành công; 8 đơn tranh 3 lượt đúng 3 thành công, `reserved_count` = 3; 2 lần cộng điểm cùng đơn chỉ cộng một lần; 2 đơn khác nhau của một khách cộng cùng lúc ra đúng 30 điểm, số dư nối liền. Ví dụ M4: cộng 18, đảo 9 và 9, gửi lại cùng mã lần hoàn không đổi (số dư 18 → 9 → 0). Rollback không để lại lượt giữ hay dòng sử dụng. Chạy lặp 3 lần, ổn định. **Pass**
  - Kiểm bằng đột biến: bỏ `FOR UPDATE` của voucher thì 2 test tranh lượt đỏ; tính VIP/voucher vào mức 10% của Sales thì 2 test cách A đỏ; đã khôi phục code.
- 5.3 HTTP thật (JWT, controller, service, repository, PostgreSQL): Sales tạo đơn với voucher viết thường (giảm 1.000.000, thuế 900.000, tổng 9.900.000), VIP Gold (giảm 500.000, thuế 950.000, tổng 10.450.000), giảm thủ công; hai nguồn cùng lúc, VIP không khách, mã lạ, giảm thủ công 1.100.000 và giảm dòng 1.100.000 kèm voucher đều 400 đúng trường; voucher 20% vẫn cho Sales; báo giá không tiêu hay giữ lượt voucher; `discountSource` đọc lại từ chi tiết đơn. Quản lý voucher: Sales và Warehouse 403, Manager tạo 201, trùng mã 409, dữ liệu sai 400, trần giảm và đơn tối thiểu, tắt/bật voucher, 404. Tóm tắt điểm: Gold 1.600 điểm → Platinum còn thiếu 3.400, khách lạ 404, Warehouse 403, khách vô hiệu hóa 404 với Sales và 200 với Manager. **Pass**
- Chưa làm trong change này (theo Non-Goals): UI quản lý voucher và hiển thị điểm/hạng (change `m4-loyalty-vip-vouchers-ui`); nối `ReserveAsync`/`RedeemAsync`/`ReleaseAsync`/`AwardForOrderAsync` vào checkout (Đạt) và `ReverseForReturnAsync` vào hoàn tiền (An); `PosCheckoutRequest` chưa chuyển `voucherCode`/`applyVipDiscount`; chưa chạy build Windows native và chạy giao diện.
