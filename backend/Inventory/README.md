# Module Inventory (Dev 3 phụ trách)

## 1. Danh sách Thực thể (Entities - Kế thừa BaseEntity)
- `InventoryStock.cs` (`inventory_stocks`): Số dư tồn kho vật lý và số lượng đặt trước theo sản phẩm/biến thể.
- `InventoryMovement.cs` (`inventory_movements`): Sổ cái kiểm toán biến động kho bất biến (Append-Only Audit Ledger).
- `Supplier.cs` (`suppliers`): Danh bạ nhà cung cấp / nhà phân phối thiết bị chính hãng.
- `PurchaseOrder.cs` (`purchase_orders`): Phiếu nhập kho từ nhà cung cấp. Phiếu có thể nhận đủ ngay hoặc lập chờ giao và nhận nhiều đợt. Số lượng đặt, số lượng đã nhận và trạng thái phiếu được lưu riêng.
- `PurchaseOrderItem.cs` (`purchase_order_items`): Chi tiết từng dòng sản phẩm/biến thể cần nhập, số lượng đặt và số lượng thực nhận.
- `SerialImei.cs` (`serial_imeis`): Quản lý định danh từng máy cụ thể (mã Serial hoặc IMEI 15 số), tình trạng máy và ngày bảo hành.

## 2. Các Enum Vòng đời Nghiệp vụ
- **`SerialImeiStatus`**: `InStock`, `Reserved` (khóa giữ chỗ chống bán trùng), `Sold`, `Returned` (chờ kiểm định), `UnderRepair` (bảo hành), `Defective` (lỗi chờ trả NCC). *(Thời hạn bảo hành tính động theo `warranty_start_at` và `warranty_end_at`).*
- **`PurchaseOrderStatus`**: `Draft`, `Ordered`, `PartiallyReceived` (giao hàng từng đợt), `Received`, `Cancelled`. API hiện chỉ tạo `Received`; các trạng thái còn lại để dành cho luồng đặt hàng nhiều đợt sau này.
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
- **Giá vốn bắt buộc khi nhập hàng:** máy có Serial/IMEI phải có `unit_cost` riêng (theo từng máy hoặc `defaultUnitCost` của cả lô); hàng không có Serial/IMEI phải có `unitCost` khi khởi tạo tồn kho, và được ghi thành một dòng `purchase_order_items` đã nhận trên đơn nhập hệ thống (nhà cung cấp "Số dư đầu kỳ"). Đơn hàng khi hoàn tất chốt `order_items.unit_cost` từ các nguồn này, không dùng `products.cost_price`.
- **Bất biến số dư & Concurrency:**
  - Hàng serial-tracked (`is_serial_tracked = true`): `inventory_stocks.quantity = COUNT(serial_imeis WHERE status IN ('InStock', 'Reserved'))`.
  - Số lượng bán khả dụng: `available = quantity - reserved_quantity`.
  - Mọi thao tác giữ chỗ/trừ kho bắt buộc mở Transaction với khóa dòng `FOR UPDATE` và ghi tương ứng vào `inventory_movements`.

## 4. Cấu trúc Thành phần Module
- **`Configurations/`**: `InventoryStockConfiguration.cs`, `InventoryMovementConfiguration.cs`, `SupplierConfiguration.cs`, `PurchaseOrderConfiguration.cs`, `PurchaseOrderItemConfiguration.cs`, `SerialImeiConfiguration.cs`.
- **`Repositories/`**: `IInventoryStockRepository` (gồm `GetForUpdateAsync` - khóa dòng `SELECT ... FOR UPDATE`), `ISerialImeiRepository`, `IPurchaseOrderRepository` (phiếu nhập: tìm theo `clientRequestId`, danh sách, chi tiết), `ISupplierRepository`. Method khóa dòng và method ghi trực tiếp (`ExecuteUpdateAsync`/`ExecuteDeleteAsync`) chỉ được gọi bên trong transaction mà service đã mở qua `IUnitOfWork`.
- **`Services/`**: `IInventoryService.cs`, `InventoryService.cs` (Quản lý nhập hàng, chuyển trạng thái Serial/IMEI; mở transaction qua `IUnitOfWork` khi cần khóa `FOR UPDATE`).
- **Controller** (đặt tại thư mục gốc của module): `InventoryController.cs` (tồn kho, điều chỉnh, Serial/IMEI, báo lỗi và đưa máy về kho), `PurchaseReceiptsController.cs` (`/api/inventory/purchase-receipts`), `SuppliersController.cs` (`/api/inventory/suppliers`).
- **Services phiếu nhập và nhà cung cấp**: `IPurchaseReceiptService`/`PurchaseReceiptService`, `ISupplierService`/`SupplierService`.

## 5. Luồng Phiếu Nhập & Quản Lý Thiết Bị Lỗi

### 5.1. Luồng Phiếu Nhập Kho (Purchase Receipts)
- Phiếu nhập kho được thực hiện qua `POST /api/inventory/purchase-receipts`, mặc định tạo phiếu ở trạng thái `Received` ngay lập tức trong một transaction ACID nguyên tử.
- Hỗ trợ nhập cả hàng có Serial/IMEI và hàng không có Serial/IMEI:
  - Hàng không có serial: tăng số dư `inventory_stocks.quantity`, ghi `inventory_movements` (loại `PurchaseReceipt`).
  - Hàng có serial: tăng số dư `inventory_stocks.quantity`, ghi các dòng thiết bị `serial_imeis` (trạng thái `InStock`, có `unit_cost`, thời hạn bảo hành), ghi `inventory_movements` (loại `PurchaseReceipt`).
- **Ngữ nghĩa Idempotency qua `clientRequestId`**: Client sinh một GUID `clientRequestId` cho mỗi lần mở form. Nếu gửi lại cùng GUID (do rớt mạng hoặc retry), hệ thống trả HTTP 200 OK replay chi tiết phiếu đã tạo trước đó, tuyệt đối không tạo đơn trùng hay cộng dồn tồn kho hai lần.
- **Giới hạn nghiệp vụ**: Phiếu nhập đã nhận không cho phép sửa, xóa hoặc hủy để đảm bảo tính toàn vẹn của sổ cái và giá vốn đã ghi nhận. Nếu nhập sai số lượng hàng không serial, thủ kho dùng điều chỉnh tồn kho; nếu thừa máy có serial, thủ kho tạm chuyển sang trạng thái `Defective`.

#### Nhận hàng nhiều đợt

1. Admin hoặc Thủ kho chọn **Lập phiếu chờ nhận hàng** khi tạo phiếu. API nhận `receiveLater: true`, số lượng đặt, đơn giá và bảo hành của từng dòng. Phiếu bắt đầu ở `Ordered`, tổng thực nhận bằng 0.
2. Trong chi tiết phiếu, nhập số lượng giao đợt này và Serial/IMEI cho từng dòng có hàng. Để trống các dòng chưa giao. `POST /api/inventory/purchase-receipts/{id}/deliveries` nhận `clientRequestId`, `supplierDocumentNumber` tùy chọn và `lines` gồm `lineId`, `expectedReceivedQuantity`, `quantity`, `devices`. Dùng lại cùng mã yêu cầu khi thử lại một đợt. Nếu người khác đã thay đổi số thực nhận của dòng, API trả 409. Khi vượt số còn lại, API cũng trả 409.
3. Mỗi đợt khóa phiếu trước rồi khóa tồn theo thứ tự sản phẩm và biến thể. Số thực nhận, tiền, thiết bị và lịch sử kho được ghi cùng giao dịch. Mỗi dòng lịch sử có người nhận, thời điểm và mã đợt. Chứng từ đợt được ghi trong diễn giải lịch sử, mặc định theo chứng từ phiếu. Trạng thái là `PartiallyReceived` hoặc `Received` khi tất cả các dòng đã đủ.
4. Để kết thúc sớm, xác nhận rồi chọn **Hủy phần chưa nhận**. `POST /api/inventory/purchase-receipts/{id}/cancel-remainder` đóng phiếu đang chờ hoặc đã nhận một phần, lưu người hủy và thời điểm. Số đã nhận, giá vốn và lịch sử được giữ nguyên. Phiếu đã nhận đủ từ chối hủy với 409. Manager được đọc phiếu, SalesStaff không có quyền truy cập.

### 5.2. Vòng Đời Thiết Bị Lỗi & Phục Hồi Kho
```
             POST serials/{id}/defective (reason, note)
 [InStock] ----------------------------------------------> [Defective]
     ^          quantity -1, movement Defective -1              |
     |                                                          |
     +----------------------------------------------------------+
             POST serials/{id}/restore (note)
             quantity +1, movement Adjustment +1
```
- **Báo lỗi máy (`POST /api/inventory/serials/{id}/defective`)**: Chuyển thiết bị từ `InStock` sang `Defective`. Giảm `inventory_stocks.quantity` đi 1, ghi movement `Defective` (-1) kèm IMEI và lý do.
- **Đưa máy đã sửa về kho (`POST /api/inventory/serials/{id}/restore`)**: Chuyển thiết bị từ `Defective` về `InStock`. Tăng `inventory_stocks.quantity` lên 1, ghi movement `Adjustment` (+1) kèm IMEI và ghi chú sửa chữa. Máy giữ nguyên giá vốn ban đầu, thời hạn bảo hành và liên kết phiếu nhập gốc.
- **Xử lý máy thất lạc**: Máy bị mất dùng chung trạng thái `Defective` với lý do "Mất", không thêm trạng thái mới vào database. Thiết bị không bao giờ bị xóa vật lý để giữ toàn vẹn lịch sử.

### 5.3. Thứ Tự Khóa Dòng Dùng Chung (Lock Ordering)
Để ngăn ngừa triệt để tình trạng Deadlock giữa luồng bán hàng (POS / Order fulfillment) và luồng quản lý kho (Nhập hàng, Báo lỗi máy):
1. **Khóa thiết bị (`serial_imeis`) trước**: Sắp xếp theo `Id` tăng dần (`SELECT ... FOR UPDATE`).
2. **Khóa dòng tồn kho (`inventory_stocks`) sau**: Sắp xếp theo `(ProductId, VariantId)` tăng dần (`SELECT ... FOR UPDATE`).

### 5.4. Bảng Các Đường Ghi Giữ Bất Biến Tồn Hàng Serial
Bất biến: `quantity = COUNT(InStock) + COUNT(Reserved)` và `reserved_quantity = COUNT(Reserved)`.

| Đường ghi | Cách bảo đảm bất biến |
| :--- | :--- |
| Phiếu nhập (`POST /api/inventory/purchase-receipts`) | Tăng tồn +N đồng thời thêm N thiết bị `InStock` trong cùng transaction |
| Nhập lô Serial (`POST serials/batch-import`) | Tăng tồn +N đồng thời thêm N thiết bị `InStock` trong cùng transaction |
| Giữ chỗ đơn hàng (`ReserveAsync`) | Thiết bị `InStock` -> `Reserved`, đồng thời `reserved_quantity += 1` |
| Bán hàng (`ConfirmSaleAsync`) | Thiết bị `Reserved` -> `Sold`, đồng thời `quantity -= 1` và `reserved_quantity -= 1` |
| Báo lỗi máy (`POST serials/{id}/defective`) | Thiết bị `InStock` -> `Defective`, đồng thời `quantity -= 1` |
| Đưa máy về kho (`POST serials/{id}/restore`) | Thiết bị `Defective` -> `InStock`, đồng thời `quantity += 1` |
| Điều chỉnh tồn kho (`stocks/{id}/adjust`) | **Chặn hoàn toàn** đối với sản phẩm `is_serial_tracked = true` (409 Conflict) |
| Khởi tạo tồn kho (`stocks/initialize`) | **Chặn hoàn toàn** đối với sản phẩm `is_serial_tracked = true` (400 Bad Request) |
| Xóa số dư tồn kho (`DELETE stocks/{id}`) | Chỉ cho phép xóa khi toàn bộ máy ở trạng thái `InStock` và không có lịch sử |

### 5.5. Cảnh Báo Demo Seed Conflict (Dự Kiến ở Môi Trường Dev)
Khi thao tác nhập hàng, bán hoặc báo lỗi trên các sản phẩm mẫu có mã `DEMO-`, lần khởi động backend tiếp theo seeder có thể log thông báo:
`Demo seed conflict: ... (Changes were not applied)`
Đây là **hành vi dự kiến** theo đúng đặc tả `data-seeding`: seeder phát hiện dữ liệu người dùng đã thay đổi nên chủ động bỏ qua đợt seed để không ghi đè dữ liệu. Ứng dụng vẫn chạy bình thường.

### 5.6. Truy Vấn Đối Soát Tồn Kho Hàng Serial (Reconciliation Query)
Truy vấn SQL đối soát kiểm tra bất kỳ sự sai lệch nào giữa số lượng tồn kho vật lý và danh sách thiết bị thực tế:
```sql
SELECT
    s.id AS stock_id,
    s.product_id,
    s.variant_id,
    s.quantity AS stock_quantity,
    s.reserved_quantity AS stock_reserved_quantity,
    COALESCE(dev.in_stock_count, 0) + COALESCE(dev.reserved_count, 0) AS expected_quantity,
    COALESCE(dev.reserved_count, 0) AS expected_reserved_quantity
FROM inventory_stocks s
JOIN products p ON p.id = s.product_id
LEFT JOIN (
    SELECT
        product_id,
        variant_id,
        COUNT(*) FILTER (WHERE status = 'InStock') AS in_stock_count,
        COUNT(*) FILTER (WHERE status = 'Reserved') AS reserved_count
    FROM serial_imeis
    GROUP BY product_id, variant_id
) dev ON dev.product_id = s.product_id AND dev.variant_id IS NOT DISTINCT FROM s.variant_id
WHERE p.is_serial_tracked = true
  AND (
      s.quantity <> COALESCE(dev.in_stock_count, 0) + COALESCE(dev.reserved_count, 0)
      OR s.reserved_quantity <> COALESCE(dev.reserved_count, 0)
  );
```

**Cách chạy bằng `psql` qua Docker:**
```bash
docker exec techstore_postgres psql -U techstore_user -d techstore_db -c "
SELECT s.id, s.product_id, s.quantity, s.reserved_quantity
FROM inventory_stocks s
JOIN products p ON p.id = s.product_id
LEFT JOIN (
    SELECT product_id, variant_id,
           COUNT(*) FILTER (WHERE status = 'InStock') AS in_stock_count,
           COUNT(*) FILTER (WHERE status = 'Reserved') AS reserved_count
    FROM serial_imeis GROUP BY product_id, variant_id
) dev ON dev.product_id = s.product_id AND dev.variant_id IS NOT DISTINCT FROM s.variant_id
WHERE p.is_serial_tracked = true
  AND (s.quantity <> COALESCE(dev.in_stock_count, 0) + COALESCE(dev.reserved_count, 0)
       OR s.reserved_quantity <> COALESCE(dev.reserved_count, 0));
"
```
Kết quả đạt chuẩn là `(0 rows)`.

## 6. Kiểm thử Tích hợp PostgreSQL (Integration Tests)
Bộ test tích hợp PostgreSQL xác thực các bất biến ACID, concurrency và tính toàn vẹn của phiếu nhập kho (`PurchaseReceiptPostgresTests`, `DeviceStatusPostgresTests`):
- Ghi phiếu có và không có serial, đối chiếu số dư tồn kho, sổ cái `inventory_movements`, danh sách thiết bị và giá vốn bình quân gia quyền.
- Rollback toàn bộ khi lỗi phát sinh giữa giao dịch, không để lại dữ liệu rác.
- Idempotency với `clientRequestId`: tuần tự và đồng thời chỉ ghi nhận một lần (201 Created lần đầu, 200 OK replay, không phát sinh lỗi 409).
- Ghi đồng thời 2 phiếu nhập cùng sản phẩm: sử dụng cơ chế khóa dòng `SELECT ... FOR UPDATE` bảo đảm cộng dồn chính xác.
- Ghi đồng thời 2 phiếu nhập cùng Serial/IMEI: một phiếu thành công (201), phiếu kia bị xung đột (409 Conflict).
- Báo lỗi máy và hoàn trả kho: kiểm tra số lượng và trạng thái máy luôn khớp.
- Báo lỗi máy đồng thời với đặt hàng giữ chỗ cùng một máy: đúng một bên thành công, bên còn lại nhận 409 Conflict.
- Đối soát tự động `AssertSerialStockReconciledAsync` được thực hiện sau mọi test.

Lệnh chạy bộ test PostgreSQL cho module kho:
```bash
TECHSTORE_TEST_CONNECTION="Host=localhost;Port=5432;Username=techstore_user;Password=TechStorePassword123!;Database=techstore_db" \
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~Inventory.PurchaseReceiptPostgresTests|FullyQualifiedName~Inventory.DeviceStatusPostgresTests"
```
