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
- **`Services/`**: `ICustomerService.cs`, `AnalyticsService.cs`.
- **`Controllers/`**: `CustomersController.cs` (CRUD khách hàng, tra cứu hội viên, lịch sử điểm), `AnalyticsController.cs` (Báo cáo doanh thu, lợi nhuận, phân tích mua sắm).
