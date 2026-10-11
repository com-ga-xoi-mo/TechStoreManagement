# Ghi Chú Code Review Sprint 1 — Dev 3 (Bình 218)

**Phân hệ phụ trách:** Feature 3 — Quản lý Danh mục & Tồn kho / Serial-IMEI ban đầu  
**Nhánh làm việc:** `catalog-inventory-api`  
**Mục đích:** Ghi nhận các điểm sai lệch so với chuẩn chung của dự án để refactor tập trung một lần khi tổng hợp nhánh.

---

## Danh Sách Lỗi & Hạng Mục Cần Sửa (Refactoring Backlog)

### 1. Không nhất quán tiền tố Route (Thừa `/v1`) — [ĐÃ HOÀN THÀNH]
* **Vấn đề:**  
  Dev 3 tự cấu hình thêm tiền tố `/api/v1/...` làm route chính trong `CategoriesController` và `InventoryController`. Điều này gây lệch chuẩn với `ProductsController` (`/api/products`) của Dev 2 và đặc tả trong `docs/REQUIREMENTS.md`.
* **Quyết định (Phương án B):**  
  Loại bỏ hoàn toàn tiền tố `/v1`, thống nhất toàn bộ hệ thống chỉ sử dụng format route trực tiếp `/api/<module>/*` để URL ngắn gọn và đồng bộ.
* **Các file & dòng code cần sửa khi tổng hợp:**
  1. `backend/Catalog/CategoriesController.cs`:
     - Xóa bỏ attribute `[Route("api/v1/categories")]`, chỉ giữ lại `[Route("api/categories")]`.
     - Sửa đường dẫn Location header trong action `Create`:
       ```csharp
       // Đổi từ:
       return Created($"/api/v1/categories/{result.Data!.Id}", result.Data);
       // Thành:
       return Created($"/api/categories/{result.Data!.Id}", result.Data);
       ```
  2. `backend/Inventory/InventoryController.cs`:
     - Xóa bỏ attribute `[Route("api/v1/inventory")]`, chỉ giữ lại `[Route("api/inventory")]`.
     - Sửa đường dẫn Location header trong action `InitializeStock`:
       ```csharp
       // Đổi từ:
       return Created($"/api/v1/inventory/stocks?productId={result.Data!.ProductId}", result.Data);
       // Thành:
       return Created($"/api/inventory/stocks?productId={result.Data!.ProductId}", result.Data);
       ```
     - Sửa đường dẫn Location header trong action `BatchImportSerials`:
       ```csharp
       // Đổi từ:
       return Created($"/api/v1/inventory/serials?productId={request.ProductId}", ...);
       // Thành:
       return Created($"/api/inventory/serials?productId={request.ProductId}", ...);
       ```
  3. `backend/TechStore.Api.http`:
     - Cập nhật tất cả các request mẫu từ dòng 250 đến 400 đổi URI từ `/api/v1/...` thành `/api/...`.
  4. `scripts/test_dev3_api.sh`:
     - Cập nhật các URL gọi `curl` từ `/api/v1/...` thành `/api/...`.
* **Trạng thái:** Đã refactor toàn bộ Controllers (`CategoriesController`, `InventoryController`), Location headers, `.http` và `.sh`.
---

### 2. Hardcode Fallback User ID gây lỗi Khóa Ngoại (`23503`) — [ĐÃ HOÀN THÀNH]
* **Vấn đề:**  
  Trong `InventoryController.cs` (hàm `GetUserId()`), Dev 3 tự hardcode fallback:
  ```csharp
  return Guid.Parse("11111111-1111-1111-1111-111111111111");
  ```
  khi request unauthenticated. Tuy nhiên, bảng `inventory_movements` có ràng buộc khóa ngoại tới `users(id)`, mà ID này không tồn tại mặc định trong DB của Dev 1, dẫn đến quăng lỗi `PostgresException 23503` (Foreign Key Violation).
* **Kế hoạch xử lý khi tổng hợp:**
  - Gán cố định `adminUser.Id = Guid.Parse("11111111-1111-1111-1111-111111111111")` trong `DataSeeder.cs` để bảo toàn toàn vẹn khóa ngoại tham chiếu từ `inventory_movements` khi chạy không có JWT.
* **Trạng thái:** Đã đồng bộ ID trong `DataSeeder.cs`, vượt qua `DataSeederTests`.

---

### 3. Chưa validate sự tồn tại của `ProductId` ở tầng Service — [ĐÃ HOÀN THÀNH]
* **Vấn đề:**  
  Trong cả `InventoryService.InitializeStockAsync` và `SerialImeiService.BatchImportAsync`, Dev 3 chỉ kiểm tra:
  ```csharp
  if (!request.ProductId.HasValue || request.ProductId.Value == Guid.Empty)
  ```
  chứ chưa kiểm tra xem `ProductId` đó có thực sự tồn tại trong bảng `products` hay không.
* **Hệ quả:**  
  Nếu client gửi một `ProductId` không tồn tại, tầng CSDL sẽ quăng lỗi vi phạm khóa ngoại `FK_inventory_stocks_products_product_id` (PostgreSQL 23503), làm hệ thống phản hồi lỗi **HTTP 500 Internal Server Error** thay vì phản hồi chuẩn **HTTP 404 Not Found** hoặc **HTTP 400 Bad Request**.
* **Kế hoạch xử lý khi tổng hợp:**
  - Tuân thủ quy tắc đọc cross-module trong `AGENTS.md` (*"a service may call another module's repository interface for reads"*), inject `IProductRepository` vào `InventoryService` và `SerialImeiService`.
  - Kiểm tra `var product = await productRepository.GetWithVariantsAsync(productId, cancellationToken);` trước khi xử lý tồn kho. Nếu `null`, trả về lỗi validation: `Product with ID '{productId}' does not exist.`
* **Trạng thái:** Đã inject `IProductRepository` vào cả `InventoryService` và `SerialImeiService`, trả về HTTP 400 Bad Request khi không tìm thấy sản phẩm.
---

### 4. Chưa giới hạn số lượng tối đa của Payload Batch Import Serial/IMEI — [ĐÃ HOÀN THÀNH]
* **Vấn đề:**  
  Trong `SerialImeiService.BatchImportAsync`, code mới chỉ kiểm tra danh sách rỗng:
  ```csharp
  if (request.Items == null || request.Items.Count == 0)
  ```
  chưa có ngưỡng trần giới hạn số lượng items trong một lần nạp (`MaxBatchSize`).
* **Hệ quả / Rủi ro:**  
  Nếu người dùng tải lên file Excel chứa hàng nghìn mã IMEI cùng lúc, hàm `FindExistingIdentifiersAsync` sẽ tạo câu truy vấn SQL với mệnh đề `WHERE imei IN (...)` chứa quá nhiều tham số, dễ làm quá tải kết nối cơ sở dữ liệu PostgreSQL hoặc gây nghẽn bộ nhớ RAM (OOM).
* **Kế hoạch xử lý khi tổng hợp:**
  - Bổ sung ngưỡng giới hạn an toàn (tối đa 500 items/lần) trong hàm validate của `SerialImeiService`:
    ```csharp
    if (request.Items.Count > 500)
    {
        errors["items"] = ["Batch import size cannot exceed 500 items per request."];
    }
    ```
* **Trạng thái:** Đã bổ sung chặn `request.Items.Count > 500` trong `SerialImeiService.BatchImportAsync` và viết unit test kiểm chứng.
---

### 5. Cho phép `quantity = 0` khi khởi tạo tồn kho vi phạm Check Constraint CSDL (`23514`) — [ĐÃ HOÀN THÀNH]
* **Vị trí lỗi:** `backend/Inventory/Services/InventoryService.cs` (dòng 66–69).
* **Vấn đề:**  
  Điều kiện validate hiện tại chỉ kiểm tra:
  ```csharp
  else if (request.Quantity.Value < 0)
  {
      errors["quantity"] = ["Quantity must be greater than or equal to zero."];
  }
  ```
  Điều này cho phép người dùng truyền `quantity = 0`. Khi đó, Service sẽ cố gắng tạo bản ghi `InventoryMovement` với `QuantityChange = 0`.
* **Hệ quả:**  
  Bảng `inventory_movements` trong PostgreSQL có ràng buộc kiểm tra toàn vẹn:
  ```sql
  CONSTRAINT ck_inventory_movements_qty_change_not_zero CHECK (quantity_change <> 0)
  ```
  Do đó, PostgreSQL kích hoạt lỗi `PostgresException (23514): violates check constraint ck_inventory_movements_qty_change_not_zero`, làm toàn bộ API sập và trả về **HTTP 500 Internal Server Error** thay vì báo lỗi validation hợp lý.
* **Kế hoạch xử lý khi tổng hợp:**
  - Sửa điều kiện kiểm tra trong `InventoryService.cs` thành `request.Quantity.Value <= 0`:
    ```csharp
    else if (request.Quantity.Value <= 0)
    {
        errors["quantity"] = ["Opening quantity must be greater than zero."];
    }
    ```
  - Cập nhật test case tương ứng trong `tests/Backend.UnitTests/InventoryServiceTests.cs`.
* **Trạng thái:** Đã cập nhật validate `<= 0` và test case cho cả `0` lẫn số âm.
---

### 6. Chưa validate Khóa ngoại phức hợp `(variant_id, product_id)` ở tầng Service — [ĐÃ HOÀN THÀNH]
* **Vị trí lỗi:** Cả `InventoryService.cs` và `SerialImeiService.cs`.
* **Vấn đề:**  
  Khi người dùng truyền `variantId`, Service chưa kiểm tra xem Biến thể đó có thực sự thuộc về `productId` được chỉ định hay không mà gửi thẳng xuống Database.
* **Hệ quả:**  
  Nếu client truyền nhầm `variantId` của một sản phẩm khác, CSDL PostgreSQL sẽ kích hoạt ràng buộc khóa ngoại phức hợp:
  ```text
  FK_inventory_stocks_product_variants_variant_id_product_id
  (hoặc FK_serial_imeis_product_variants_variant_id_product_id)
  ```
  dẫn đến lỗi `PostgresException (23503)` và API trả về **HTTP 500 Internal Server Error** thay vì mã lỗi nghiệp vụ chuẩn **HTTP 400 Bad Request**.
* **Kế hoạch xử lý khi tổng hợp:**
  - Khi `request.VariantId.HasValue`, gọi `IProductRepository` (hoặc kiểm tra từ danh sách variants của product) để xác minh:
    ```csharp
    if (variant == null || variant.ProductId != request.ProductId.Value)
    {
        errors["variantId"] = [$"Variant with ID '{request.VariantId.Value}' does not belong to product '{request.ProductId.Value}'."];
    }
    ```
* **Trạng thái:** Đã bổ sung kiểm tra variant thuộc product trong cả `InventoryService` và `SerialImeiService` cùng đầy đủ unit tests.
