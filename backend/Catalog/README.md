# Module Catalog (Dev 2 phụ trách)

## 1. Danh sách Thực thể (Entities - Kế thừa BaseEntity)
- `Category.cs` (`categories`): Cây danh mục sản phẩm, hỗ trợ phân cấp cha - con.
- `Product.cs` (`products`): Sản phẩm gốc, chứa thông số kỹ thuật chung dạng JSONB.
- `ProductVariant.cs` (`product_variants`): Biến thể phần cứng (màu sắc, cấu hình RAM/SSD), SKU và giá bán riêng.

## 2. Quy ước Thiết kế & Ràng buộc Schema (Theo schema.dbml)
- **Cây danh mục:** Khóa ngoại tự tham chiếu `parent_id` trỏ về `categories.id` (`[delete: set null]`), đánh chỉ mục `idx_categories_parent_id`. Backend có trách nhiệm từ chối liên kết cha-con vòng tròn (ancestor cycles).
- **Thông số kỹ thuật động (`specs JSONB`):**
  - Cột `specs` trên cả `products` và `product_variants` có chỉ mục GIN với `jsonb_path_ops` (`idx_products_specs_gin`, `idx_product_variants_specs_gin`).
  - Quy ước cấu trúc JSON phẳng với khóa dạng lowercase snake_case (`cpu`, `ram_gb`, `storage_gb`, `battery_mah`, `color`).
  - Kế thừa thông số động: `effective_specs = product.specs || variant.specs` (khóa của variant ghi đè sản phẩm cha; thuộc tính không khai báo ở variant kế thừa từ cha).
- **Khóa ngoại tổng hợp (Composite Reference Target):**
  - `product_variants` thiết lập ràng buộc duy nhất `(id, product_id)` tên `uq_product_variants_id_product` làm đích tham chiếu cho các composite FK từ `inventory_stocks`, `purchase_order_items`, `serial_imeis`, và `order_items`.
- **Quy tắc SKU & Barcode:**
  - Sản phẩm không có biến thể: bán trực tiếp theo SKU sản phẩm, `variant_id = NULL`.
  - Sản phẩm có biến thể: SKU sản phẩm đóng vai trò mã tra cứu model (lookup code), từng biến thể sở hữu SKU bán lẻ, giá bán riêng (`product_variants.price`) và barcode riêng.
- **Kiểu dữ liệu:** `base_price` và `cost_price` trên `products`, `price` trên `product_variants` dùng kiểu `numeric(18, 2)`.

## 3. Cấu trúc Thành phần Module
- **`Configurations/`**: `CategoryConfiguration.cs`, `ProductConfiguration.cs`, `ProductVariantConfiguration.cs` (Fluent API assembly scanning).
- **`Services/`**:
  - `IProductService.cs`, `ProductService.cs` (Dev 2 - Quản lý sản phẩm, tìm kiếm, lọc specs JSONB).
  - `ICategoryService.cs`, `CategoryService.cs` (Dev 3 - Cây danh mục phân cấp cha - con).
- **Controllers (đặt tại thư mục gốc của module `backend/Catalog/`)**:
  - `ProductsController.cs` (Dev 2 - CRUD, tìm kiếm và lọc nâng cao theo JSONB specs).
  - `CategoriesController.cs` (Dev 3 - Quản lý và tra cứu cây danh mục).
