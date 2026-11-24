# Module Customers & Vouchers (Dev 5 phụ trách)

## 1. Danh sách Thực thể (Entities - Kế thừa BaseEntity)
- `Customer.cs` (`customers`): Hồ sơ khách hàng, phân hạng thành viên VIP và điểm tích lũy.
- `CustomerLoyaltyPoint.cs` (`customer_loyalty_points`): Sổ cái biến động điểm thưởng có thể kiểm toán (Accrual / Redemption).
- `Voucher.cs` (`vouchers`): Mã phiếu giảm giá khuyến mãi (chiết khấu % hoặc số tiền cố định).

## 2. Các Enum Nghiệp vụ
- **`CustomerTier`**:
  - `Standard`: 0 - 499 điểm (mặc định).
  - `Silver`: 500 - 1.499 điểm (giảm 2%).
  - `Gold`: 1.500 - 4.999 điểm (giảm 5%).
  - `Platinum`: 5.000+ điểm (giảm 10%).
- **`VoucherType`**:
  - `Percentage`: Giảm theo tỷ lệ phần trăm (ví dụ: 10%).
  - `FixedAmount`: Giảm số tiền trực tiếp (ví dụ: 200.000 VND).

## 3. Quy ước Thiết kế & Ràng buộc Schema (Theo schema.dbml)
- **100% Khóa ngoại được đánh Index B-tree:**
  - `customer_loyalty_points.customer_id`, `customer_loyalty_points.order_id`.
  - `customers.phone` (Unique index) tăng tốc tra cứu khách hàng tại quầy POS.
  - `customers.tier` (Index) hỗ trợ lọc danh sách hội viên VIP.
- **Ràng buộc kiểm tra (Check Constraints):**
  - Khách hàng: `date_of_birth <= CURRENT_DATE` (áp dụng khuyến mãi sinh nhật), `loyalty_points >= 0`.
  - Tích điểm: `points_change <> 0` (dương khi tích điểm, âm khi tiêu điểm), `balance_after >= 0`.
  - Voucher: `discount_value > 0` (và `<= 100` nếu là `Percentage`), `min_order_amount >= 0`, `max_discount_amount >= 0`, `usage_limit > 0`, `0 <= used_count <= usage_limit`, `expires_at > created_at`.
- **Chỉ mục Một phần (Partial Index):**
  - `vouchers`: Composite partial index `(is_active, expires_at)` WHERE `is_active = true`. Logic kiểm tra còn hạn `expires_at > now()` được thực hiện tại tầng truy vấn do `now()` không bất biến (immutable) trong PostgreSQL.
- **Kiểu dữ liệu:** Điểm thưởng dạng `integer`, giá trị giảm và ngưỡng đơn tối thiểu dạng `numeric(18, 2)`.

## 4. Cấu trúc Thành phần Module
- **`Configurations/`**: `CustomerConfiguration.cs`, `CustomerLoyaltyPointConfiguration.cs`, `VoucherConfiguration.cs`.
- **`Repositories/`** (dự kiến): `ICustomerRepository` (`Customer` cùng `CustomerLoyaltyPoint`), `IVoucherRepository`.
- **`Services/`**: `ICustomerService.cs`, `AnalyticsService.cs`.
- **Controllers** (đặt tại thư mục gốc của module): `CustomersController.cs` (CRUD khách hàng, tra cứu hội viên, lịch sử điểm), `AnalyticsController.cs` (Báo cáo doanh thu, lợi nhuận, phân tích mua sắm).

## 5. Quyền (M3, change `m3-sales-customers`)
- Tìm, xem hồ sơ, tạo khách: `Admin`, `Manager`, `SalesStaff`. SalesStaff chỉ thấy khách đang hoạt động; server ép `isActive=true` và trả 404 cho khách đã vô hiệu hóa.
- Sửa, vô hiệu hóa/kích hoạt, lịch sử đơn/thiết bị/điểm: `Admin`, `Manager`.
- `WarehouseStaff`: 403 mọi endpoint. Bảng đầy đủ: [API_PERMISSIONS](../../docs/API_PERMISSIONS.md).
- Client dùng chung `CustomerPickerViewModel` + `CustomerPickerView` để chọn/tạo khách khi bán (trang tạo đơn, POS).

## 6. Điểm thưởng, VIP và voucher (M4, change `m4-loyalty-vip-vouchers`, người sở hữu: Vy)

Đạt (checkout) và An (hoàn tiền) **gọi** các service dưới đây, không tự tính điểm, ưu đãi hay lượt voucher. Các hàm giữ/tiêu/trả voucher và cộng/đảo điểm **không** mở transaction, không `SaveChanges`, không `Commit`: người gọi mở transaction, gọi service, lưu một lần. Chúng khóa dòng (`FOR UPDATE`) nên bắt buộc có transaction đang mở, và người gọi đã giữ khóa đơn của mình.

### Quy tắc
- Điểm = `floor((Subtotal − DiscountAmount) / 100.000)`: tiền hàng sau giảm dòng và giảm đơn, chưa thuế. Chỉ cộng cho đơn `Completed` có khách. Hạng: Standard < 500, Silver ≥ 500 (giảm 2%), Gold ≥ 1.500 (5%), Platinum ≥ 5.000 (10%); hạng tính lại sau mỗi lần điểm đổi.
- Mỗi đơn một ưu đãi cấp đơn: `orderDiscountAmount > 0` (thủ công), `voucherCode` hoặc `applyVipDiscount`. Gửi nhiều hơn một nguồn → 400 lỗi `orderDiscount`. Đơn lưu `discountSource` (`None`/`Manual`/`Vip`/`Voucher`) và `voucherId`.
- Giới hạn giảm giá 10% của Sales chỉ tính giảm giá dòng + giảm thủ công; VIP và voucher không tính, nhưng giảm giá dòng đi kèm vẫn bị kiểm.
- Định giá (`OrderService.PrepareAsync` / `CreateAsync`) chỉ **báo giá**: không giữ và không tiêu lượt voucher.

### Cách gọi (đúng thứ tự)
| Bước | Hàm | Ghi chú |
|:---|:---|:---|
| Bắt đầu trả tiền (QR/Cash) | `IVoucherService.ReserveAsync(order)` | Dùng `order.VoucherId` và `order.Subtotal`, kiểm lại điều kiện. `Applied` / `Unchanged` (đã giữ) / `Refused` (kèm `Reason`) / `NotFound`. Giữ lại sau khi đã `Released` là được. |
| Nhận tiền | `IVoucherService.RedeemAsync(order.Id)` | `Reserved → Redeemed`; gọi lặp là `Unchanged`; sau `Released` là `Refused`. |
| Hủy hoặc hết hạn giữ 15 phút trước khi trả | `IVoucherService.ReleaseAsync(order.Id)` | Chỉ trả lượt đang giữ; lượt đã tiêu không bao giờ được trả lại (hoàn đơn đã trả không phục hồi voucher). Đạt phụ trách hẹn giờ 15 phút và gọi hàm này. |
| Hoàn tất bán | `ILoyaltyService.AwardForOrderAsync(order)` | Gọi sau khi đặt `order.Status = Completed` trong cùng UoW. Trả `LoyaltyChange` với `Outcome`: `Applied`, `AlreadyApplied` (gọi lặp, khóa `earn:{orderId}`), `NoPoints` (< 1 điểm), `NotEligible` (không có khách / chưa `Completed`). |
| Hoàn hàng | `ILoyaltyService.ReverseForReturnAsync(order, returnId, remainingGoodsAmount)` | `remainingGoodsAmount` = tiền hàng (sau giảm giá, chưa thuế) khách còn giữ **sau** lần hoàn này, An tính từ phân bổ đã lưu. Trừ phần chênh lệch điểm, không âm số dư, khóa `reverse:{returnId}` nên gọi lặp không đổi số. Đơn chưa từng cộng điểm trả `NoPoints`. |

Ví dụ M4 (2 sản phẩm 1.000.000đ, giảm đơn 200.000đ, VAT 10%): thu 1.980.000đ, cộng 18 điểm. Trả 1 món: còn giữ 900.000đ → đảo 9. Trả nốt: còn giữ 0 → đảo 9. Gửi lại cùng `returnId` không đổi.

### API
- Admin, Manager: `GET/POST /api/vouchers`, `GET/PUT /api/vouchers/{id}`, `PATCH /api/vouchers/{id}/activate|deactivate`. Mã được cắt khoảng trắng, đổi sang chữ hoa, duy nhất (409 khi trùng); mã và loại giảm không sửa sau khi tạo.
- Admin, Manager, Sales: `GET /api/customers/{id}/loyalty-summary` (điểm, hạng, mức giảm VIP, hạng kế tiếp và điểm còn thiếu).
- `POST /api/orders` nhận thêm `voucherCode`, `applyVipDiscount`; `OrderDto.discountSource` trả nguồn ưu đãi.
- POS chưa chuyển `voucherCode`/`applyVipDiscount` trong `PosCheckoutRequest`: Đạt thêm khi nối POS.

### Dữ liệu
`customer_loyalty_points.reference_key` (duy nhất khi có giá trị), `vouchers.reserved_count` (ràng buộc `used_count + reserved_count ≤ usage_limit`), bảng `voucher_redemptions` (một dòng mỗi đơn), `orders.discount_source`. Chi tiết: [schema.dbml](../../docs/database/schema.dbml).

### Voucher demo (seed)
Seed thêm đủ các trạng thái để thử màn Voucher và luật kiểm tra khi bán. Chỉ mã (`code`) nhận dạng một dòng; dòng đã có thì seed **không** sửa và **không** báo xung đột, vì Admin/Manager có thể tắt, sửa hoặc dùng hết voucher đã seed. Dòng bị xóa sẽ được thêm lại ở lần chạy sau.

| Mã | Loại | Điều kiện | Trạng thái |
| --- | --- | --- | --- |
| `DEMO-OCT26` | 100.000đ | đơn từ 1.000.000đ, 100 lượt | đang hoạt động |
| `DEMO-VIP10` | 10% | đơn từ 5.000.000đ, tối đa 500.000đ, 50 lượt | đang hoạt động |
| `DEMO-EXPIRED` | 50.000đ | 12/100 lượt | hết hạn (17/10/2026) |
| `DEMO-SOLDOUT` | 5% | tối đa 100.000đ, 20/20 lượt | hết lượt |
| `DEMO-OFF` | 200.000đ | đơn từ 2.000.000đ | đã tắt |

Số lượt dùng của `DEMO-EXPIRED` và `DEMO-SOLDOUT` là số cố định, chưa có dòng `voucher_redemptions` đi kèm.
