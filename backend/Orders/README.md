# Module Orders (Dev 4 phụ trách)

## 1. Danh sách Thực thể (Entities - Kế thừa BaseEntity)
- `Order.cs` (`orders`): Đơn hàng bán lẻ tại quầy POS và trực tuyến, lưu mã đơn `order_code` duy nhất.
- `OrderItem.cs` (`order_items`): Từng dòng sản phẩm trong đơn, lưu snapshot tên sản phẩm, đơn giá, chiết khấu và liên kết thiết bị đã bán.
- `OrderReturn.cs` (`order_returns`): Phiếu hoàn trả hàng, yêu cầu bảo hành và hoàn tiền.

## 2. Các Enum Dùng chung (Shared Enums)
- **`OrderStatus`**: `Pending`, `Processing`, `Completed`, `Cancelled`, `PartiallyRefunded`, `Refunded`.
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
- **`Repositories/`**: `IOrderRepository` / `OrderRepository` (tìm kiếm có phân trang, chi tiết đơn, khóa đơn `FOR UPDATE`, mã đơn cuối của tháng), `OrderSearchCriteria`.
- **`Services/`**:
  - `IOrderService` / `OrderService`: tạo đơn, danh sách, chi tiết, chuyển trạng thái. Service sở hữu transaction (`IUnitOfWork`).
  - Hàm thuần (test không cần DB): `OrderPricingHelper`, `OrderStatusTransitions`, `OrderCodeGenerator`, `CreateOrderValidator`, `OrderEnumParser`.
  - `ReturnRefundService`: dự kiến cho đổi trả/hoàn tiền (chưa làm trong Milestone 2).
- **Controller** (đặt tại thư mục gốc của module): `OrdersController.cs`.

## 5. API (Milestone 2, Feature 4)
| Use case | Endpoint | Ghi chú |
| :--- | :--- | :--- |
| UC-ORD-01 | `POST /api/orders` | Tạo đơn `Pending` (nháp, **không giữ chỗ**). Giá lấy từ catalog; máy có IMEI phải chọn `serialImeiId` ngay khi tạo |
| UC-ORD-02 | `GET /api/orders` | Lọc `status`, `customerId`, `from`, `to`, `q` (mã đơn, tên/SĐT khách); `sort=createdAt\|totalAmount:asc\|desc`; phân trang |
| UC-ORD-03 | `GET /api/orders/{id}` | Chi tiết đơn kèm IMEI/serial từng dòng (không trả giá vốn) |
| UC-ORD-04 | `PATCH /api/orders/{id}/status` | Body `{ "targetStatus": "Processing" \| "Completed" \| "Cancelled" }` |

`status`, `targetStatus`, `paymentMethod` là **chuỗi tên enum** (không phân biệt hoa thường), không nhận số.

### Vòng đời trạng thái
```
Pending ──► Processing ──► Completed
   │  └────────────────────────▲   (tiền mặt: Pending → Completed trong một transaction)
   └──► Cancelled              (chỉ khi chưa thanh toán)
```
| Chuyển | Tác động lên Inventory |
| :--- | :--- |
| `Pending → Processing` | Máy `InStock → Reserved`, `reserved_quantity += q`, đặt `paid_at`; không ghi movement |
| `Processing → Completed` | Máy `Reserved → Sold` + bảo hành, `quantity` và `reserved_quantity` giảm, 1 movement `Sale` mỗi dòng, chốt `unit_cost`, đặt `completed_at` |
| `Pending → Cancelled` | Không có (đơn nháp không giữ chỗ) |

Đơn đã thanh toán không hủy được (đi qua luồng hoàn tiền, ngoài phạm vi Milestone 2). Chuyển trạng thái không hợp lệ hoặc lặp lại trả `409`; thiết bị hết khả dụng khi chuyển trạng thái cũng trả `409` và toàn bộ thay đổi được hoàn tác.

## 6. Phụ thuộc vào module khác
- **Inventory** (`IInventoryFulfillmentService`, file mới trong `backend/Inventory/Services/`): `CheckAvailabilityAsync`, `ReserveAsync`, `ConfirmSaleAsync`. Chỉ *staging*, không `SaveChanges`/commit; `OrderService` mở và đóng transaction. Orders không ghi trực tiếp vào bảng của Inventory.
- **Catalog**: đọc sản phẩm/biến thể qua `IProductRepository.GetWithVariantsAsync`.
- **Customers**: kiểm tra khách qua `ICustomerRepository.GetByIdAsync` (bản tối thiểu, Dev 5 mở rộng).
- **Người thao tác**: chưa có đăng nhập nên lấy từ claim `sub`/`NameIdentifier`, nếu không có thì dùng admin cố định `11111111-1111-1111-1111-111111111111` (do `DataSeeder` tạo). Bỏ khi có JWT.
- Chưa làm trong Milestone 2: hoàn trả/hoàn tiền, giải phóng giữ chỗ (`Release`), voucher, giảm giá theo hạng, thuế, ca POS (`pos_session_id` để `NULL`), xác minh thanh toán VietQR/thẻ.
