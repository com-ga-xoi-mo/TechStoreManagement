# Module Inventory (Dev 3 phụ trách)

## 1. Danh sách Thực thể (Entities - Kế thừa BaseEntity)
- `InventoryStock.cs` (`inventory_stocks`): Số dư tồn kho vật lý và số lượng đặt trước theo sản phẩm/biến thể.
- `InventoryMovement.cs` (`inventory_movements`): Sổ cái kiểm toán biến động kho bất biến (Append-Only Audit Ledger).
- `Supplier.cs` (`suppliers`): Danh bạ nhà cung cấp / nhà phân phối thiết bị chính hãng.
- `PurchaseOrder.cs` (`purchase_orders`): Đơn đặt hàng nhập kho từ nhà cung cấp (hỗ trợ nhập từng phần).
- `PurchaseOrderItem.cs` (`purchase_order_items`): Chi tiết từng dòng sản phẩm/biến thể cần nhập, số lượng đặt và số lượng thực nhận.
- `SerialImei.cs` (`serial_imeis`): Quản lý định danh từng máy cụ thể (mã Serial hoặc IMEI 15 số), tình trạng máy và ngày bảo hành.

## 2. Các Enum Vòng đời Nghiệp vụ
- **`SerialImeiStatus`**: `InStock`, `Reserved` (khóa giữ chỗ chống bán trùng), `Sold`, `Returned` (chờ kiểm định), `UnderRepair` (bảo hành), `Defective` (lỗi chờ trả NCC). *(Thời hạn bảo hành tính động theo `warranty_start_at` và `warranty_end_at`).*
- **`PurchaseOrderStatus`**: `Draft`, `Ordered`, `PartiallyReceived` (giao hàng từng đợt), `Received`, `Cancelled`.
- **`InventoryMovementType`**: `OpeningBalance`, `PurchaseReceipt`, `Sale`, `CustomerReturn`, `Adjustment`, `Defective`.

## 3. Quy ước Thiết kế & Ràng buộc Schema (Theo schema.dbml)
- **100% Khóa ngoại được đánh Index B-tree:**
  - `inventory_stocks.variant_id`
  - `inventory_movements.inventory_stock_id`, `performed_by_user_id`, `purchase_order_item_id`, `order_item_id`, `order_return_id`
  - `purchase_orders.supplier_id`, `created_by_user_id`, `received_by_user_id`
  - `purchase_order_items.purchase_order_id`, `product_id`, `variant_id`
  - `serial_imeis.product_id`, `variant_id`, `purchase_order_id`
- **Khóa ngoại tổng hợp (Composite Foreign Keys):**
  - `inventory_stocks.(variant_id, product_id) > product_variants.(id, product_id)`
  - `purchase_order_items.(variant_id, product_id) > product_variants.(id, product_id)`
  - `serial_imeis.(variant_id, product_id) > product_variants.(id, product_id)`
- **PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT`:**
  - `inventory_stocks`: `idx_inventory_stocks_product_variant` trên `(product_id, variant_id)` đảm bảo mỗi sản phẩm chỉ có duy nhất 1 dòng số dư kể cả khi không có biến thể (`variant_id = NULL`).
  - `purchase_order_items`: `uq_po_items_order_product_variant` trên `(purchase_order_id, product_id, variant_id)` đảm bảo mỗi sản phẩm/biến thể chỉ xuất hiện 1 lần trên đơn nhập hàng.
- **Ràng buộc kiểm tra (Check Constraints):**
  - Tồn kho: `quantity >= 0`, `reserved_quantity >= 0`, `reserved_quantity <= quantity`, `min_stock_alert >= 0`.
  - Nhập hàng: `ordered_quantity > 0`, `0 <= received_quantity <= ordered_quantity`, `unit_cost >= 0`, `total_cost >= 0`.
  - Thiết bị: Có ít nhất `serial_number` hoặc `imei`; nếu có IMEI thì phải đúng 15 chữ số (`imei ~ '^[0-9]{15}$'`); `unit_cost >= 0`.
  - Sổ cái: `quantity_change <> 0`, `quantity_after >= 0`.
- **Partial Index:**
  - `serial_imeis`: `(product_id, status)` WHERE `status = 'InStock'` tối ưu hóa tốc độ quét POS tại quầy.
- **Bất biến số dư & Concurrency:**
  - Hàng serial-tracked (`is_serial_tracked = true`): `inventory_stocks.quantity = COUNT(serial_imeis WHERE status IN ('InStock', 'Reserved'))`.
  - Số lượng bán khả dụng: `available = quantity - reserved_quantity`.
  - Mọi thao tác giữ chỗ/trừ kho bắt buộc mở Transaction với khóa dòng `FOR UPDATE` và ghi tương ứng vào `inventory_movements`.

## 4. Cấu trúc Thành phần Module
- **`Configurations/`**: `InventoryStockConfiguration.cs`, `InventoryMovementConfiguration.cs`, `SupplierConfiguration.cs`, `PurchaseOrderConfiguration.cs`, `PurchaseOrderItemConfiguration.cs`, `SerialImeiConfiguration.cs`.
- **`Services/`**: `IInventoryService.cs`, `InventoryService.cs` (Quản lý nhập hàng, chuyển trạng thái Serial/IMEI, khóa concurrency `FOR UPDATE`).
- **`Controllers/`**: `InventoryController.cs` (API tra cứu tồn kho, lập phiếu nhập PO, quét danh sách IMEI).
