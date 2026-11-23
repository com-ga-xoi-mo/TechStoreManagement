# Design

## D1. Ai mở transaction

Theo M4: mỗi luồng chỉ một nơi mở/commit transaction, service được gọi chỉ ghi thay đổi trong cùng `UnitOfWork`. Vì vậy `AwardForOrderAsync`, `ReverseForReturnAsync`, `ReserveAsync`, `RedeemAsync`, `ReleaseAsync` **không** gọi `BeginTransaction`, `SaveChanges` hay `Commit`: chúng khóa dòng (`FOR UPDATE`), kiểm tra và đặt thay đổi vào context. Người gọi (Đạt/An) mở transaction, gọi nhiều service, rồi lưu một lần. Các hàm quản lý voucher (tạo, sửa, bật/tắt) tự lưu vì không nằm trong luồng bán/hoàn.

## D2. Điểm

- Cơ sở tính: `Order.Subtotal − Order.DiscountAmount` (tiền hàng sau giảm dòng và giảm đơn, chưa thuế). `points = floor(base / 100.000)`.
- Chỉ cộng khi `Order.Status == Completed` và có `CustomerId`. Người gọi đặt trạng thái `Completed` trong cùng UoW trước khi gọi.
- Chống lặp: cột `reference_key` duy nhất (khi không null). Cộng dùng `earn:{orderId}`, đảo dùng `reverse:{returnId}`. Gọi lặp trả lại bản ghi cũ, không thêm dòng.
- Đảo điểm: `net = Σ points_change của đơn`; `target = floor(remainingGoods / 100.000)`; nếu `net > target` thì trừ `net − target`, tối đa bằng số dư hiện tại. `remainingGoods` là tiền hàng còn giữ sau lần hoàn này, do An tính từ phân bổ đã lưu. Đơn chưa từng cộng điểm có `net = 0` nên không đảo.
- Không tạo dòng khi thay đổi bằng 0 (ràng buộc `points_change <> 0`).
- Khóa dòng khách (`FOR UPDATE`) trước khi đọc số dư để hai lần cộng/đảo không ghi đè nhau.

## D3. Hạng

`TierFor(points)` là hàm thuần. Sau mỗi lần điểm đổi, `customer.Tier` được tính lại từ số dư (có thể giảm hạng sau khi đảo điểm). Không chạy lại hàng loạt trên dữ liệu cũ: số dư và lịch sử ban đầu giữ nguyên, hạng chỉ đổi khi điểm của khách đó đổi.

## D4. Một ưu đãi cấp đơn

`CreateOrderRequest` có ba nguồn: `orderDiscountAmount > 0` (thủ công), `voucherCode`, `applyVipDiscount = true`. Quá một nguồn → 400 lỗi `orderDiscount`. Số tiền ưu đãi được tính bằng `LoyaltyRules.VipDiscount` và `VoucherRules.Evaluate` (hàm thuần), rồi đi vào `OrderPricingHelper.Calculate` như `orderDiscount` hiện tại. Công thức M3 không đổi.

- VIP: `round(subtotal × percent / 100)`, cần khách; Standard = 0 thì từ chối bằng 400 "no VIP discount" để Sales không gửi nhầm.
- Voucher phần trăm: `round(subtotal × value / 100)`, tối đa `max_discount_amount`, tối đa `subtotal`. Voucher số tiền: `min(value, subtotal)`. Làm tròn đồng, nửa ra xa 0.
- Điều kiện voucher: đang hoạt động, `expires_at > now`, `subtotal ≥ min_order_amount`, `used_count + reserved_count < usage_limit`.
- `subtotal` ở đây là tiền hàng sau giảm giá dòng, trùng cách tính của Calculate.

## D5. Quyền giảm giá (cách A)

Mức tối đa 10% của Sales tính cho `giảm dòng + giảm thủ công`. Khi nguồn là VIP hoặc voucher, chỉ `giảm dòng` bị kiểm tra với mức đó. Lý do: VIP và voucher do Admin/Manager cấu hình, không phải quyết định của nhân viên; nếu tính vào mức 10% thì khách Platinum không còn được giảm thêm và voucher trên 10% không dùng được ở quầy. Admin không giới hạn.

## D6. Vòng đời voucher

`vouchers.reserved_count` là số lượt đang giữ; ràng buộc DB `used_count + reserved_count ≤ usage_limit` là lớp bảo vệ cuối. Bảng `voucher_redemptions` có một dòng cho mỗi đơn dùng voucher (`order_id` duy nhất): `Reserved → Redeemed` hoặc `Reserved → Released`.

- `ReserveAsync(voucherId, orderId)`: khóa voucher, kiểm điều kiện, tăng `reserved_count`, thêm dòng `Reserved`. Gọi lặp cùng đơn không tăng thêm.
- `RedeemAsync(orderId)`: `Reserved → Redeemed`, `reserved_count −1`, `used_count +1`. Gọi lặp là no-op.
- `ReleaseAsync(orderId)`: chỉ `Reserved → Released`, `reserved_count −1`. Đơn đã `Redeemed` thì không trả lượt (hoàn đơn đã trả không phục hồi voucher).
- Hết hạn giữ 15 phút do Đạt điều phối (gọi `ReleaseAsync`); service không có bộ đếm giờ riêng.
- Tranh lượt cuối: khóa dòng voucher trong transaction của người gọi; người đến sau thấy hết lượt và nhận lỗi.

## D7. Lưu nguồn ưu đãi

`orders.discount_source` (`None`, `Manual`, `Vip`, `Voucher`) và `orders.voucher_id` (đã có) được ghi khi dựng đơn để báo cáo/hoàn tiền biết ưu đãi đến từ đâu. An vẫn hoàn theo phân bổ đã lưu trong `order_items`, không tính lại.

## D8. Ranh giới

Không sửa `PosCheckoutService`, luồng hoàn tiền hay `OrderStatusTransitions`. `PosCheckoutService.PrepareCashAsync` không chuyển `voucherCode`/`applyVipDiscount`; Đạt thêm khi nối POS. README của Customers ghi chữ ký, thứ tự gọi, lỗi trả về và ví dụ M4 (18 điểm, đảo 9 + 9).

## Kết quả mong đợi theo ví dụ M4

2 sản phẩm 1.000.000đ, giảm đơn 200.000đ, VAT 10%: thu 1.980.000đ; tiền hàng sau giảm 1.800.000đ → 18 điểm. Trả 1 món: còn giữ 900.000đ → đảo 9. Trả nốt: còn giữ 0 → đảo 9. Gửi lại cùng mã lần hoàn không đổi số.
