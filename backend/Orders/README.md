# Module Orders (Dev 4 phụ trách)

## 1. Danh sách Thực thể (Entities - Kế thừa BaseEntity)
- `Order.cs` (`orders`): Đơn hàng bán lẻ tại quầy POS và trực tuyến, lưu mã đơn `order_code` duy nhất.
- `OrderItem.cs` (`order_items`): Từng dòng sản phẩm trong đơn, lưu snapshot tên sản phẩm, đơn giá, chiết khấu và liên kết thiết bị đã bán.
- `OrderReturn.cs` (`order_returns`): Phiếu hoàn trả hàng, yêu cầu bảo hành và hoàn tiền.

## 2. Các Enum Dùng chung (Shared Enums)
- **`OrderStatus`**: `Pending`, `Processing`, `Completed`, `Cancelled`, `Refunded`.
- **`PaymentMethod`**: `Cash`, `VietQr`, `Card`.

## 3. Quy ước Thiết kế & Ràng buộc Schema (Theo schema.dbml)
- **100% Khóa ngoại được đánh Index B-tree:**
  - `orders`: `customer_id`, `cashier_user_id`, `pos_session_id`, `voucher_id`.
  - `order_items`: `order_id` (CASCADE), `product_id`, `variant_id`, `serial_imei_id`.
  - `order_returns`: `order_id`, `order_item_id`, `processed_by_user_id`.
- **Khóa ngoại tổng hợp (Composite Foreign Key):**
  - `order_items.(variant_id, product_id) > product_variants.(id, product_id)` đảm bảo biến thể thuộc về đúng sản phẩm.
- **Liên kết Thiết bị Serial/IMEI:**
  - `order_items.serial_imei_id` liên kết trực tiếp tới máy cụ thể trong `serial_imeis`. Khi đơn chuyển `Completed`, kích hoạt ngày bảo hành `warranty_start_at` và `warranty_end_at`, đồng thời chuyển trạng thái máy sang `Sold`.
- **Hoàn trả & Restock:**
  - `order_returns.is_restocked`: Nếu kiểm định máy đạt yêu cầu và đưa trở lại kho bán, tạo bản ghi `inventory_movements` với loại `CustomerReturn` (+1 kho) và cập nhật trạng thái IMEI về `InStock`.
- **Ràng buộc kiểm tra (Check Constraints):**
  - `orders`: `subtotal >= 0`, `discount_amount >= 0`, `tax_amount >= 0`, `total_amount >= 0`.
  - `order_items`: `unit_price >= 0`, `quantity > 0`, `discount_amount >= 0`, `total_price >= 0`.
  - `order_returns`: `refund_amount >= 0`.
- **Kiểu dữ liệu:** Toàn bộ tiền tệ dùng kiểu `numeric(18, 2)`, thời gian `completed_at`, `created_at` dùng `timestamptz` (UTC).

## 4. Cấu trúc Thành phần Module
- **`Configurations/`**: `OrderConfiguration.cs`, `OrderItemConfiguration.cs`, `OrderReturnConfiguration.cs`.
- **`Services/`**: `IOrderService.cs`, `OrderService.cs`, `ReturnRefundService.cs`.
- **`Controllers/`**: `OrdersController.cs` (API lập đơn, chuyển trạng thái đơn, hủy/hoàn tiền, tra cứu lịch sử mua hàng).
