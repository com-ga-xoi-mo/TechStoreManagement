# Proposal

## Why

M4 Sprint 1 (Vy): "Điểm thưởng, VIP và voucher. Làm service tính tiền, giữ/xác nhận/hủy voucher và cộng/đảo điểm trước UI; bàn giao cách gọi, phân bổ từng dòng, số dư và lỗi validation." Hiện hạng VIP, điểm và voucher chỉ có bảng và entity; chưa có service nào cộng điểm, đổi hạng, tính giảm giá VIP/voucher hay giữ lượt voucher. Đạt (checkout) và An (hoàn tiền) cần các hàm này để không tự tính điểm/ưu đãi.

## What Changes

- **Điểm thưởng**: 1 điểm / 100.000 đồng tiền hàng sau giảm giá, chưa thuế (`Subtotal − DiscountAmount`), làm tròn xuống. Chỉ cộng cho đơn `Completed` có khách. Chống cộng lặp theo mã đơn.
- **Hạng VIP**: Standard < 500, Silver ≥ 500 (giảm 2%), Gold ≥ 1.500 (5%), Platinum ≥ 5.000 (10%). Hạng tính lại sau mỗi lần điểm đổi; ưu đãi dùng hạng tại thời điểm tính tiền.
- **Đảo điểm khi hoàn**: tính lại điểm trên hàng còn giữ, trừ chênh lệch, ghi lịch sử theo mã lần hoàn, không âm số dư, gọi lặp không đổi số liệu.
- **Một ưu đãi cấp đơn**: mỗi đơn chọn đúng một trong VIP, voucher, giảm thủ công. `CreateOrderRequest` thêm `voucherCode`, `applyVipDiscount`; gửi hơn một nguồn là 400. Công thức M3 giữ nguyên (giảm dòng → giảm đơn → thuế).
- **Quyền giảm giá (đã chốt với Vy, cách A)**: mức tối đa 10% của Sales chỉ tính giảm giá dòng và giảm thủ công; VIP và voucher là chính sách cửa hàng nên không tính vào mức này.
- **Voucher**: API quản lý (Admin/Manager), kiểm tra điều kiện, giữ/tiêu/trả lượt chống tranh lượt cuối bằng khóa dòng.
- **Lưu trữ (additive)**: `customer_loyalty_points.reference_key`, `vouchers.reserved_count`, bảng `voucher_redemptions`, `orders.discount_source`; migration bằng `dotnet ef`.
- **Không làm ở change này**: giao diện (change `m4-loyalty-vip-vouchers-ui`), nối vào checkout/hoàn tiền (Đạt/An gọi service theo README), đổi điểm lấy tiền.

## Capabilities

### New Capabilities

- `loyalty-points`: cộng/đảo điểm, hạng, tóm tắt điểm.
- `voucher-management`: quản lý voucher và vòng đời giữ/tiêu/trả lượt.

### Modified Capabilities

- `order-management`: ưu đãi cấp đơn (một nguồn), quyền giảm giá theo cách A.
- `database-schema`: cột và bảng mới.
- `api-role-authorization`: quyền cho API voucher và tóm tắt điểm.

## Impact

- Backend: `Customers/Services` (`LoyaltyRules`, `LoyaltyService`, `VoucherService`), `Customers/Repositories` (voucher, loyalty), `Customers/VouchersController.cs`, `CustomersController` (tóm tắt điểm), `Orders/Services/OrderService.cs` (phần định giá ưu đãi), `OrderDiscountCalculator`, entities/configurations, migration, `Program.cs` (DI).
- Shared: `VoucherDto`, `CreateVoucherRequest`, `UpdateVoucherRequest`, `VoucherSearchRequest`, `LoyaltySummaryDto`, `OrderDiscountSource`, `CreateOrderRequest.VoucherCode/ApplyVipDiscount`, `OrderDto.DiscountSource`.
- Docs: `docs/database/schema.dbml`, `docs/API_PERMISSIONS.md`, `backend/Customers/README.md` (cách gọi cho Đạt/An).
- Tests: công thức điểm/hạng/voucher, service với fake, quyền HTTP, tranh lượt cuối và đảo điểm trên PostgreSQL thật.
- Ảnh hưởng người khác: không sửa `PosCheckoutService` hay luồng hoàn tiền; request mới chỉ thêm trường tùy chọn.
