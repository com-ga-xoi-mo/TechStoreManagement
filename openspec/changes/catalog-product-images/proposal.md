# Proposal

## Why

Hiện mỗi sản phẩm chỉ có một cột `products.image_url`, và người dùng phải tự dán đường link ảnh vào màn soạn. Cửa hàng không có chỗ lưu ảnh: ảnh nằm trên dịch vụ khác, link hay hỏng, không có nhiều góc chụp, và biến thể (ví dụ từng màu) không có ảnh riêng. `REQUIREMENTS.md` yêu cầu thông tin sản phẩm có "hình ảnh", còn proposal của `catalog-product-management-api` và `catalog-product-management-ui` đều ghi "upload ảnh" là ngoài phạm vi. Change này làm phần đó: tải nhiều ảnh từ máy lên, lưu file trên Cloudflare R2, và gắn ảnh cho từng biến thể.

## What Changes

- **Bảng mới `product_images`** (migration `AddProductImages`): mỗi dòng là một ảnh của sản phẩm, có thể gắn cho một biến thể (`variant_id` để trống nghĩa là ảnh dùng chung), lưu `object_key`, `url`, `content_type`, `size_bytes`, `sort_order`. Khóa ngoại kép `(variant_id, product_id)` trỏ tới `product_variants(id, product_id)` như các bảng khác. Database chỉ lưu tham chiếu, không lưu file.
- **Lưu file qua một lớp trừu tượng** `IObjectStorage` trong `backend/Common/Storage/`, có hai bản:
  - `R2ObjectStorage`: Cloudflare R2 qua API S3 (`AWSSDK.S3`), dùng khi cấu hình `ObjectStorage:Provider = R2`;
  - `LocalObjectStorage`: ghi vào thư mục `uploads/` của server và phục vụ ở `/uploads`. Đây là mặc định, để nhóm và test chạy được khi không có khóa Cloudflare.
  Khóa R2 chỉ nằm ở server (user-secrets hoặc biến môi trường), không bao giờ vào git hay app desktop.
- **API ảnh mới** (`ProductImagesController`, quyền `Admin`/`Manager`):
  - `POST /api/products/{id}/images`: tải một ảnh (multipart), tùy chọn `variantId`. Server kiểm tra chữ ký file (JPEG/PNG/WebP), tối đa 5 MB mỗi ảnh, tối đa 20 ảnh mỗi sản phẩm (tính cả ảnh biến thể);
  - `DELETE /api/products/{id}/images/{imageId}`: xóa một ảnh;
  - `PUT /api/products/{id}/images/layout`: lưu thứ tự và việc gắn biến thể của toàn bộ ảnh trong một lần.
- **Ảnh bìa do server tính**: `products.image_url` luôn là link của ảnh dùng chung đứng đầu (nếu không có ảnh chung thì là ảnh đầu tiên; không có ảnh thì null). Trang danh sách và `ProductSummaryDto` không đổi.
- **BREAKING (hợp đồng API)**: bỏ `imageUrl` khỏi `CreateProductRequest` và `UpdateProductRequest`; client không còn tự đặt link ảnh. `ProductDto` thêm danh sách `images`.
- **Xóa sản phẩm chưa dùng** (`DELETE /api/products/{id}`) xóa luôn các dòng ảnh và file của sản phẩm đó.
- **Client – màn soạn**: bỏ ô "URL ảnh"; thay bằng khu "Ảnh sản phẩm": ảnh lớn đang chọn, hàng ảnh nhỏ, nút "+ Thêm" chọn nhiều file, các nút "Đặt làm ảnh bìa", `<`, `>`, "Xóa ảnh", và ô "Dùng cho" để gắn ảnh cho một biến thể. Mọi thay đổi ảnh chỉ được gửi khi bấm Lưu; Hủy bỏ hết.
- **Client – gắn ảnh cho biến thể chưa lưu**: ảnh trỏ tới dòng biến thể trên màn hình; sau khi lưu sản phẩm, app ghép dòng với `id` biến thể theo SKU (không phân biệt hoa thường) rồi mới tải ảnh lên.
- **Client – trang chi tiết**: ảnh lớn, hàng ảnh nhỏ bấm để đổi ảnh, và ô "Xem ảnh của" (Tất cả / từng biến thể).
- **Client – transport**: `IApiClient` thêm `PostFileAsync` gửi multipart; hộp chọn file qua `IImageFilePicker` (Uno `FileOpenPicker`, trên Linux đi qua xdg-desktop-portal).
- **Tài liệu**: `docs/database/schema.dbml` (bảng thứ 23), `AGENTS.md` (thư mục `Common/Storage`, cấu hình R2, quy tắc ảnh), `backend/Catalog/README.md` (endpoint ảnh, cấu hình R2), `TechStore.Api.http`.

Ngoài phạm vi: thay đổi kích thước hoặc nén ảnh, ảnh trong đơn hàng/POS, kéo thả để sắp xếp, URL ký sẵn (presigned) để client tải thẳng lên R2, dọn file mồ côi định kỳ, chuyển dữ liệu `image_url` cũ (DB dev không có sản phẩm nào có ảnh), phân quyền đọc ảnh (bucket công khai).

## Capabilities

### New Capabilities
- `product-images`: lưu, tải lên, xóa, sắp xếp và gắn biến thể cho ảnh sản phẩm qua API, giới hạn loại/kích thước/số lượng, ảnh bìa tính từ danh sách ảnh, và nơi lưu file cấu hình được (R2 hoặc thư mục cục bộ).
- `frontend-product-images`: khu ảnh trong màn soạn (chọn nhiều file, chọn ảnh bìa, đổi thứ tự, gắn biến thể, lưu cùng nút Lưu, ghép biến thể chưa lưu theo SKU) và xem ảnh trên trang chi tiết.

### Modified Capabilities
- `product-catalog`: "Product creation", "Product update" và "Required fields and length limits" bỏ `imageUrl` khỏi request; "Product detail" trả thêm `images`; "Product summary content" ghi rõ `imageUrl` là ảnh bìa do server tính; "Delete only unused products" xóa cả ảnh.
- `frontend-catalog-management`: bỏ requirement "Image URL preview"; "Client validation mirrors the API limits" bỏ kiểm tra URL ảnh.
- `frontend-api-access`: thêm requirement gửi file dạng multipart.

## Impact

- **Thứ tự lưu trữ (archive)**: change này sửa các requirement mà `catalog-product-management-api` và `catalog-product-management-ui` thêm vào. Phải archive hai change đó trước, rồi mới archive change này.
- **Backend**:
  - Mới: `Common/Storage/` (`IObjectStorage`, `ObjectStorageUnavailableException`, `ObjectStorageKey`, `ObjectStorageOptions`, `R2ObjectStorage`, `LocalObjectStorage`, `ObjectStorageServiceCollectionExtensions`); `Catalog/Entities/ProductImage.cs`; `Catalog/Configurations/ProductImageConfiguration.cs`; `Catalog/Repositories/IProductImageRepository.cs`, `ProductImageRepository.cs`; `Catalog/Services/IProductImageService.cs`, `ProductImageService.cs`, `ProductImageFileInspector.cs`, `ProductImageLayoutValidator.cs`, `ProductCoverImage.cs`; `Catalog/ProductImagesController.cs`; migration `AddProductImages`.
  - Sửa: `Product.cs` (navigation `Images`), `ProductRepository.cs` (nạp ảnh), `ProductService.cs` (bỏ `ImageUrl` khỏi tạo/sửa, xóa file khi xóa sản phẩm), `ProductDtoMapper.cs`, hai validator, `Program.cs` (đăng ký storage, phục vụ `/uploads`), `TechStore.Api.csproj` (`AWSSDK.S3`), `appsettings.json`, `.gitignore` (`backend/uploads/`).
- **Shared**: mới `ProductImageDto`, `UpdateProductImagesLayoutRequest`, `ProductImageLayoutItemRequest`; sửa `ProductDto` (thêm `Images`), `CreateProductRequest`/`UpdateProductRequest` (bỏ `ImageUrl`).
- **Client**: mới `Services/Api/ApiFileContent.cs`, `Services/Files/` (`IImageFilePicker`, `PickedImageFile`), `Views/Shell/ImageFilePicker.cs`, `Models/Catalog/` (`ProductImage`, `ImageUpload`, `ImageLayoutEntry`), `Repositories/Catalog/IProductImageRepository.cs`, `ProductImageRepository.cs`, `ViewModels/Catalog/` (`ProductImageRules`, `ProductImageItem`, `ProductImageGallery`, `VariantSkuMatcher`, `ProductImageSaver`, `ProductImageViewer`), `Views/Catalog/ProductImageGalleryView.cs`, `ProductImageViewerView.cs`; sửa `IApiClient`/`ApiClient`, `ProductDetail`, `ProductRepository` (map `images`), `ProductDraft`, `ProductDraftValidator`, `ProductEditorItems` (`VariantRowItem`), `ProductEditorViewModel`, `ProductDetailViewModel`, `CatalogApiErrors` (thông báo 503), `CatalogViewParts`, `ProductEditorPage`, `ProductDetailPage`, `UiServiceCollectionExtensions`, `App.xaml.cs` (gắn cửa sổ cho hộp chọn file), test project (link `Services/Files`), 6 stub `IApiClient` trong test.
- **Phụ thuộc mới**: gói `AWSSDK.S3` cho backend. Không thêm gói nào cho client.
- **Cấu hình**: người dùng tạo bucket R2, bật URL công khai, tạo API token, đặt khóa bằng `dotnet user-secrets`. Không có khóa thì server tự dùng thư mục cục bộ.
- **Nhánh và commit**: làm trên nhánh hiện tại `feat/catalog-product-management`, chỉ commit local; không push, không mở PR (quyết định của chủ dự án, 2026-10-23).
- **Phối hợp nhóm**: khi chủ dự án chia sẻ nhánh, báo Dev 3, Dev 4 và Dev 6, vì có bảng mới, migration mới và `ProductDto`/request đổi hợp đồng (xem design "Coordination notes").
