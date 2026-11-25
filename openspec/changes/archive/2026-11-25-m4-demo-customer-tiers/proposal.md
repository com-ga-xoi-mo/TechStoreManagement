# Proposal

## Why

Spec `data-seeding` đã yêu cầu "coherent loyalty balances and tier classifications", nhưng sáu trong tám khách `Demo Customer 01–08` mang hạng không khớp ngưỡng điểm của M4 (Tiêu chuẩn dưới 500, Bạc từ 500, Vàng từ 1.500, Bạch kim từ 5.000). Ví dụ `Demo Customer 04` là Vàng với 400 điểm. Hậu quả: hồ sơ khách hiện "còn 4.600 điểm để lên Bạch kim" cho khách Vàng 400 điểm; giảm giá VIP khi tạo đơn tính theo hạng đang lưu nên khách này được giảm 5%; và hạng tự rớt ở lần đổi điểm đầu tiên vì `LoyaltyRules.TierFor` tính lại từ điểm.

## What Changes

- `DemoSeedManifest.BuildCustomers`: hạng của `Demo Customer 01–08` lấy theo điểm (100–400 là Tiêu chuẩn, 500–800 là Bạc).
- Test mới: hạng của mọi khách seed bằng `LoyaltyRules.TierFor(điểm)`, và số dư ban đầu giữ nguyên.
- **Không làm**: đổi số điểm hay lịch sử điểm (kế hoạch dòng 90 yêu cầu giữ), thêm khách mới (test đếm của người khác đặt đúng 10 khách và 7 dòng sổ điểm), sửa DB đã seed (seed chỉ thêm, không ghi đè).

## Capabilities

### Modified Capabilities

- `data-seeding`: hạng khách demo khớp điểm.

## Impact

- `backend/Common/Data/DemoSeedManifest.cs`; test `tests/Backend.UnitTests/DemoSeedCustomerTierTests.cs`.
- DB đã có sẵn dữ liệu seed cũ vẫn giữ hạng cũ; chỉ DB tạo mới có hạng đúng. Muốn sửa DB cũ phải tạo lại DB hoặc cập nhật tay.
- Ảnh hưởng người khác: file seed của nhóm, chỉ đổi cột hạng của sáu khách. Các test dùng hạng khách (`LoyaltyRulesTests`, `OrderServiceTests`, `CustomerServiceTests`, `PromotionsPostgresHttpTests`) tự dựng khách riêng, không đọc từ seed.
