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
  - SKU sản phẩm và SKU biến thể đã tạo không được sửa.
  - Sản phẩm có biến thể không có barcode cấp sản phẩm.
- **Vòng đời & RBAC:**
  - Ngừng kinh doanh = `PATCH /api/products/{id}/status`; xóa thật chỉ khi chưa có tồn kho/IMEI/phiếu nhập/đơn hàng.
  - Không đổi `is_serial_tracked` và không thêm biến thể đầu tiên khi sản phẩm đã được sử dụng.
  - Ghi sản phẩm yêu cầu role Admin hoặc Manager.
- **Kiểu dữ liệu:** `base_price` và `cost_price` trên `products`, `price` trên `product_variants` dùng kiểu `numeric(18, 2)`.

## 3. Cấu trúc Thành phần Module
- **`Configurations/`**: `CategoryConfiguration.cs`, `ProductConfiguration.cs`, `ProductVariantConfiguration.cs`, `SpecDefinitionConfiguration.cs` (Fluent API assembly scanning; seed tên thông số).
- **`Entities/`**: `Category`, `Product`, `ProductVariant`, `SpecDefinition` (key, tên hiển thị, đơn vị, kiểu giá trị của thông số; `SpecValueKinds`).
- **`Repositories/`** (nơi duy nhất dùng `AppDbContext`, trả về entity, không gọi `SaveChanges`):
  - `IProductRepository.cs`, `ProductRepository.cs` (Dev 2 - `Product` và các `ProductVariant`).
  - `ICategoryRepository.cs`, `CategoryRepository.cs` (khởi tạo tối thiểu bởi Dev 2; Dev 3 sở hữu và mở rộng cho cây danh mục).
  - `ISpecDefinitionRepository.cs`, `SpecDefinitionRepository.cs` (Dev 2 - bảng `spec_definitions`; chỉ thêm, không sửa tên đã có).
- **`Services/`** (nghiệp vụ; chỉ gọi repository và `IUnitOfWork`):
  - `IProductService.cs`, `ProductService.cs` (Dev 2 - Quản lý sản phẩm, tìm kiếm, lọc specs JSONB, gợi ý và tên thông số qua `IProductRepository`, `ICategoryRepository`, `ISpecDefinitionRepository` và `IUnitOfWork`).
  - `SpecLabelValidator.cs`, `ProductSpecSuggestionBuilder.cs` (kiểm `specLabels`; dựng gợi ý lọc thông số).
  - `ICategoryService.cs`, `CategoryService.cs` (Dev 3 - Cây danh mục phân cấp cha - con).
- **Controllers (đặt tại thư mục gốc của module `backend/Catalog/`)**:
  - `ProductsController.cs` (Dev 2 - CRUD, tìm kiếm và lọc nâng cao theo JSONB specs; `GET /api/products/spec-keys` gợi ý giá trị lọc, `GET /api/products/spec-definitions` tên thông số; `GET /api/products/brands` danh sách thương hiệu kèm số sản phẩm).
- **Giá vốn dự kiến (`costPrice`):** do chủ cửa hàng nhập tay để định giá và ước tính lãi gộp; chỉ tạo/sửa sản phẩm thay đổi nó, nhập hàng không ghi đè. Giá vốn thực tế của đơn bán là `order_items.unit_cost` (Orders/Inventory).
- **Thương hiệu:** cột `brand` vẫn là chữ tự do, nhưng khi tạo/sửa sản phẩm, thương hiệu trùng một cách viết đã có (không phân biệt hoa thường) được lưu theo cách viết được nhiều sản phẩm dùng nhất; sản phẩm đang sửa không được tính.
  - `CategoriesController.cs` (Dev 3 - Quản lý và tra cứu cây danh mục).

## Product images storage

| Endpoint (Admin/Manager) | Success | Errors |
|---|---|---|
| `POST /api/products/{productId}/images` (multipart `file`, optional `variantId`) | 201 `ProductImageDto` | 400, 401, 403, 404, 409 (20 images), 503 (storage) |
| `DELETE /api/products/{productId}/images/{imageId}` | 204 | 401, 403, 404 |
| `PUT /api/products/{productId}/images/layout` (`items` with every image id and optional variant id) | 200 image list | 400, 401, 403, 404 |

`ProductImageService` dùng các repository, `IUnitOfWork` và cổng `Common/Storage/IObjectStorage`; service không gọi SDK S3. `product_images` chỉ lưu tham chiếu, file nằm ngoài DB. Server nhận JPEG/PNG/WebP theo chữ ký nội dung, tối đa 5 MB mỗi ảnh và 20 ảnh mỗi sản phẩm. `products.image_url` là ảnh chung đầu tiên theo thứ tự; nếu chỉ có ảnh biến thể thì dùng ảnh đầu tiên, không có ảnh thì null. Client không đặt link bìa qua request tạo/sửa sản phẩm.

Mặc định `ObjectStorage:Provider=Local`, `Local:RootPath=uploads`, `Local:RequestPath=/uploads`: file trong `backend/uploads` được API phục vụ công khai, có header `X-Content-Type-Options: nosniff`.

Để dùng R2: tạo bucket trong Cloudflare R2; bật Public Development URL (hoặc custom domain); tạo API token với quyền **Object Read & Write** chỉ trên bucket đó, lấy Account ID và cặp S3 Access Key ID/Secret Access Key. Đặt cấu hình ngoài git:

```bash
dotnet user-secrets init --project backend/TechStore.Api.csproj
dotnet user-secrets set "ObjectStorage:Provider" "R2" --project backend/TechStore.Api.csproj
dotnet user-secrets set "ObjectStorage:R2:AccountId" "<account id>" --project backend/TechStore.Api.csproj
dotnet user-secrets set "ObjectStorage:R2:AccessKeyId" "<access key id>" --project backend/TechStore.Api.csproj
dotnet user-secrets set "ObjectStorage:R2:SecretAccessKey" "<secret access key>" --project backend/TechStore.Api.csproj
dotnet user-secrets set "ObjectStorage:R2:BucketName" "<bucket name>" --project backend/TechStore.Api.csproj
dotnet user-secrets set "ObjectStorage:R2:PublicBaseUrl" "<public base URL>" --project backend/TechStore.Api.csproj
```

Hoặc tạo `backend/.env` (git bỏ qua theo `*.env`, nên `chmod 600`) với các dòng `ObjectStorage__Provider=R2`, `ObjectStorage__R2__AccountId=...`, `ObjectStorage__R2__AccessKeyId=...`, `ObjectStorage__R2__SecretAccessKey=...`, `ObjectStorage__R2__BucketName=...`, `ObjectStorage__R2__PublicBaseUrl=...`. Khi `ASPNETCORE_ENVIRONMENT=Development` (profile `http`/`https`), `Program.cs` nạp file này trước khi tạo builder qua `Common/Configuration/DotEnvFile`; biến đã có sẵn trong shell hoặc launch profile được giữ nguyên. `RunWorkingDirectory` trong csproj cho `dotnet run` chạy trong `backend/` dù gọi từ thư mục nào. Không đưa file này vào bản nén nộp bài.

R2 thiếu cấu hình sẽ dừng khởi động, chỉ nêu tên trường thiếu. Có thể dùng biến môi trường tương ứng, ví dụ `ObjectStorage__R2__SecretAccessKey`; khóa không đi vào desktop, response hoặc log. Xóa file sau commit là best effort; file mồ côi có thể dọn tay theo tiền tố `products/{productId:N}/`.

Khi đổi domain công khai, cập nhật URL đã lưu (thay hai placeholder bằng URL thực, không chứa khóa):

```sql
BEGIN;
UPDATE product_images SET url = replace(url, '<old public base URL>', '<new public base URL>');
UPDATE products SET image_url = replace(image_url, '<old public base URL>', '<new public base URL>');
COMMIT;
```
