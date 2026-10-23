# Design

## Context

Xem `proposal.md` (Why) và các file trong `specs/` (hành vi bắt buộc). Hiện trạng ảnh hưởng tới cách làm:

- **DB:** `products.image_url` (`text`, null) là chỗ duy nhất liên quan tới ảnh. DB dev có 20 sản phẩm, không sản phẩm nào có ảnh; `DataSeeder` không gán ảnh. Không có bảng ảnh, và biến thể không có cột ảnh.
- **Backend:**
  - `Program.cs` chưa có `UseStaticFiles`, chưa có middleware ProblemDetails chung. Lỗi do controller tự trả bằng `ValidationProblem` / `Problem`.
  - `ProductService` (primary constructor: `IProductRepository`, `ICategoryRepository`, `IUnitOfWork`, `ISpecDefinitionRepository`) xử lý tạo, sửa, đổi trạng thái, xóa. `GetForUpdateAsync` khóa dòng sản phẩm (`FOR UPDATE`) rồi nạp biến thể dạng tracked.
  - `GetWithVariantsAsync` (AsNoTracking) còn được `OrderService`, `InventoryService`, `SerialImeiService` dùng, nên không được đổi hành vi của nó.
  - `AppDbContext` nạp cấu hình bằng `ApplyConfigurationsFromAssembly`, nên entity mới chỉ cần một file `IEntityTypeConfiguration`; repository dùng `dbContext.Set<ProductImage>()` mà không phải sửa `AppDbContext.cs`.
  - Khóa ngoại kép `(variant_id, product_id) → product_variants(id, product_id)` đã có mẫu ở `OrderItemConfiguration` (`HasPrincipalKey`), và unique index `uq_product_variants_id_product` đã tồn tại.
  - `ProductService` được tạo trực tiếp trong 18 chỗ thuộc 8 file test.
- **Client:**
  - `IApiClient` có `Get/Post/Put/Patch/Delete`, chưa gửi được file. Có 6 lớp test cài đặt `IApiClient` (liệt kê ở tasks 11.3).
  - `ProductEditorViewModel` đã dài 721 dòng với 8 dependency. Vì vậy logic ảnh phải tách ra lớp riêng, không viết thêm vào lớp này.
  - Dự án đã có mẫu tách hợp đồng thuần (headless, link vào test) khỏi phần cài đặt WinUI: `Services/Dialog/IDialogPresenter` (thuần) và `Views/Shell/ContentDialogPresenter` (WinUI). Hộp chọn file sẽ đi theo đúng mẫu này.
  - `CatalogViewParts.ProductImage(...)` đặt icon giữ chỗ bên dưới `Image`, nên ảnh lỗi tự lộ icon mà không cần `ImageFailed`.
  - `CatalogPresentation.ResolveImageUri` nhận cả URL tuyệt đối http/https và đường dẫn tương đối theo base API, nên URL `/uploads/...` dùng được ngay.
- **Uno 6.7.135 trên Linux:** `Uno.UI.Runtime.Skia.X11` có `LinuxFilePickerExtension.PickMultipleFilesAsync`, gọi `org.freedesktop.portal.FileChooser`. Máy dev (Ubuntu 24.04, GNOME) đang chạy `xdg-desktop-portal-gnome`.

## Goals / Non-Goals

**Goals:**
- Mỗi file trong change có đúng một trách nhiệm, đúng tầng (Controller → Service → Repository → AppDbContext; View → ViewModel → Client Repository → ApiClient), và có test viết tay (không mock library).
- AGENTS.md mục 3 hiện ghi service chỉ phụ thuộc repository và `IUnitOfWork`, trong khi `OrderService` đã dùng `TimeProvider` và change này thêm `IObjectStorage`. Quy tắc được sửa ngay trong nhóm task tạo storage (task 3.9) thành: service phụ thuộc repository, `IUnitOfWork` và các cổng hạ tầng trong `Common/`. Service vẫn không bao giờ chạm `AppDbContext` hay SDK của R2.
- Không thêm dependency nào vào `OrderService`, `InventoryService`, `SerialImeiService`, và không đổi hành vi `GetWithVariantsAsync`.
- Server không bao giờ để dòng DB trỏ tới file chưa lưu được. File thừa trên storage chấp nhận được (dọn sau), dữ liệu sai thì không.
- Khóa R2 không xuất hiện trong git, log hay response.
- Chạy được toàn bộ khi không có tài khoản Cloudflare (storage cục bộ).

**Non-Goals:**
- Không nén, resize hay tạo thumbnail phía server. Ảnh nhỏ được trình duyệt/app tự thu nhỏ khi vẽ.
- Không dọn file mồ côi định kỳ, không có thùng rác.
- Không làm optimistic concurrency cho layout: người lưu sau thắng, nhưng layout phải liệt kê đúng toàn bộ ảnh hiện có nên không thể vô tình xóa ảnh của người khác.
- Không đổi `ProductSummaryDto` và trang danh sách.
- Không sửa `UnitOfWork.cs`, `AppDbContext.cs` hay các file khác đang có trong `backend/Common/` (chỉ thêm thư mục mới `Common/Storage/`).

## Decisions

### D1. Bảng `product_images`

| Cột | Kiểu | Ghi chú |
|:--|:--|:--|
| `id` | `uuid` PK, default `gen_random_uuid()` | |
| `product_id` | `uuid` not null | FK → `products(id)` ON DELETE CASCADE |
| `variant_id` | `uuid` null | null = ảnh dùng chung |
| `object_key` | `varchar(300)` not null | unique `uq_product_images_object_key`; dạng `products/{productId:N}/{guid:N}.{ext}` |
| `url` | `varchar(2048)` not null | link công khai lúc tải lên |
| `content_type` | `varchar(20)` not null | check `IN ('image/jpeg','image/png','image/webp')` |
| `size_bytes` | `integer` not null | check `size_bytes > 0 AND size_bytes <= 5242880` |
| `sort_order` | `integer` not null | check `>= 0` |
| `created_at` / `updated_at` | `timestamptz` | theo `BaseEntity` |

- **FK kép** `(variant_id, product_id) → product_variants(id, product_id)` ON DELETE CASCADE. PostgreSQL dùng MATCH SIMPLE, nên khi `variant_id` null thì không kiểm, đúng nghĩa ảnh dùng chung. Cascade cả hai đường không gây lỗi trên PostgreSQL, và hệ thống không bao giờ xóa riêng một biến thể.
- **Index:**
  - `idx_product_images_product_id` trên `(product_id, sort_order)`: cột đầu phủ FK `product_id`, cột sau phục vụ đọc theo thứ tự;
  - `idx_product_images_variant_id` trên `(variant_id, product_id)`: phủ đầy đủ FK kép, đồng thời tra cứu theo `variant_id` nhờ cột đầu. Chủ dự án đồng ý đổi index đơn thành index kép (2026-10-23) để EF dùng chung index này cho FK, giữ đúng ba index.
- **Không có unique `(product_id, sort_order)`.** EF cập nhật nhiều dòng trong một lần lưu theo thứ tự tùy ý; đổi chỗ 0 và 1 sẽ vi phạm unique giữa chừng nếu ràng buộc không deferrable. Thay vào đó, service giữ thứ tự liền mạch `0..n-1` dưới khóa dòng sản phẩm (D6).
  - *Cách khác:* unique `DEFERRABLE INITIALLY DEFERRED` bằng SQL thô trong migration. Không chọn, vì EF không mô hình hóa được và thêm một chỗ SQL tay.
- **Lưu cả `object_key` và `url`.** Mapper không cần biết storage, và `products.image_url` chỉ việc chép `url`. Nếu sau này đổi domain công khai thì phải chạy một câu `UPDATE` (ghi trong Risks).
  - *Cách khác:* chỉ lưu key rồi ghép link lúc trả về. Không chọn, vì mapper tĩnh `ProductDtoMapper` sẽ phải nhận storage.
- **Migration** `AddProductImages` tạo bằng `dotnet ef migrations add AddProductImages --project backend/TechStore.Api.csproj --output-dir Common/Data/Migrations`. Không có SQL tay.

### D2. Storage: hợp đồng chung trong `backend/Common/Storage/`

Lưu file là hạ tầng chung, không phải nghiệp vụ Catalog, nên đặt ở `Common/Storage/` (thư mục mới, không đụng file `Common/` có sẵn):

```csharp
public interface IObjectStorage
{
    /// Stores the bytes under the key and returns the public URL. Throws ObjectStorageUnavailableException when the store cannot be reached or refuses the write.
    Task<string> PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default);

    /// Best-effort delete. Returns false and logs (key only, never credentials) instead of throwing on storage errors.
    Task<bool> TryDeleteAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class ObjectStorageUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
```

- **`TryDeleteAsync` không ném lỗi,** để các service không cần logger và không phải bọc `try/catch` sau khi commit (spec "Image removal", "Delete only unused products").
- **Key do server tạo,** không bao giờ dùng tên file người dùng gửi lên. Hàm thuần `ObjectStorageKey.EnsureValid(key)` (trong `Common/Storage/`) kiểm key theo regex `^[a-z0-9]+(/[a-z0-9]+)*\.(jpg|png|webp)$` và ném `ArgumentException` khi sai; cả hai bản cài đặt gọi nó trước mọi thao tác. Nhờ vậy không thể ghi ra ngoài thư mục bằng `..`.

### D3. Hai bản cài đặt và cấu hình

`ObjectStorageOptions` (section `ObjectStorage`):

```json
"ObjectStorage": {
  "Provider": "Local",
  "Local": { "RootPath": "uploads", "RequestPath": "/uploads" },
  "R2": { "AccountId": "", "AccessKeyId": "", "SecretAccessKey": "", "BucketName": "", "PublicBaseUrl": "" }
}
```

**`LocalObjectStorage`** (mặc định):
- Ghi vào `Path.Combine(contentRoot, RootPath, key)` và tạo thư mục nếu chưa có.
- Trả URL tương đối `RequestPath + "/" + key`.
- `ObjectStorageServiceCollectionExtensions.UseObjectStorageFiles(app)` gọi `app.UseStaticFiles(...)` với `PhysicalFileProvider(root)` và `RequestPath`, ánh xạ `.webp → image/webp`, thêm header `X-Content-Type-Options: nosniff`. Hàm này chỉ chạy khi `Provider = Local`.
- `backend/uploads/` được thêm vào `.gitignore`.

**`R2ObjectStorage`:**
- Dùng `AmazonS3Client` (gói `AWSSDK.S3`, ghim đúng phiên bản), tạo một lần (singleton) với:
  - `ServiceURL = https://{AccountId}.r2.cloudflarestorage.com`;
  - `ForcePathStyle = true`, `AuthenticationRegion = "auto"`;
  - `RequestChecksumCalculation` và `ResponseChecksumValidation` = `WHEN_REQUIRED` (R2 không nhận các header checksum mặc định của SDK v4).
- `PutObjectRequest` đặt `DisablePayloadSigning = true` (R2 không hỗ trợ streaming SigV4) và `Headers.CacheControl = "public, max-age=31536000, immutable"`. Key luôn mới nên cache vĩnh viễn được.
- URL trả về là `PublicBaseUrl.TrimEnd('/') + "/" + key`.
- Lỗi `AmazonS3Exception`, `HttpRequestException` hay timeout được gói thành `ObjectStorageUnavailableException`.

**`AddObjectStorage(services, configuration)`:**
- bind options;
- `Provider` không phải `Local`/`R2` → ném lỗi khi khởi động;
- `R2` thiếu bất kỳ trường nào → ném `InvalidOperationException("ObjectStorage:R2:SecretAccessKey is required.")`, chỉ nêu tên trường, không in giá trị;
- đăng ký `IObjectStorage` singleton.

Phần kiểm options là hàm thuần `ObjectStorageOptions.Validate()` trả danh sách tên trường thiếu, để test được.

**Khóa trên máy dev:**

```bash
dotnet user-secrets init --project backend/TechStore.Api.csproj
dotnet user-secrets set "ObjectStorage:Provider" "R2" --project backend/TechStore.Api.csproj
dotnet user-secrets set "ObjectStorage:R2:AccountId" "<account id>" --project backend/TechStore.Api.csproj
# ... AccessKeyId, SecretAccessKey, BucketName, PublicBaseUrl
```

Khi chạy thật có thể dùng biến môi trường `ObjectStorage__R2__SecretAccessKey`.

*Cách khác:* Cloudflare Images (resize sẵn) bị loại vì tính phí theo tháng. Presigned URL (client tải thẳng lên R2) bị loại vì phải kiểm quyền và kiểm file ở hai nơi, và app desktop phải nói chuyện với một origin thứ hai.

### D4. Tầng Catalog phía backend: mỗi file một việc

| File | Trách nhiệm | Phụ thuộc |
|:--|:--|:--|
| `Entities/ProductImage.cs` | Entity: `ProductId`, `VariantId?`, `ObjectKey`, `Url`, `ContentType`, `SizeBytes`, `SortOrder`, navigation `Product`, `Variant?` | `BaseEntity` |
| `Entities/Product.cs` (sửa) | Thêm `ICollection<ProductImage> Images` | |
| `Configurations/ProductImageConfiguration.cs` | Bảng, cột, check, index, hai FK như D1 | EF |
| `Repositories/IProductImageRepository.cs` + `ProductImageRepository.cs` | `Add(image)`, `Remove(image)` (chỉ stage, không save) | `AppDbContext` |
| `Repositories/IProductRepository.cs` + `ProductRepository.cs` (sửa) | Thêm `GetDetailAsync(id)` (AsNoTracking, `Include` Category, Variants, Images, `AsSplitQuery`); `GetForUpdateAsync` nạp thêm `Images` (tracked) | `AppDbContext` |
| `Services/ProductImageFileInspector.cs` | Hàm thuần: nhận `byte[]`, trả `(ContentType, Extension)` hoặc lỗi cho `file` theo D5 | không |
| `Services/ProductImageLayoutValidator.cs` | Hàm thuần: kiểm hình dạng request layout (null, id rỗng, id trùng) → lỗi theo `items` / `items[i].id` | không |
| `Services/ProductCoverImage.cs` | Hàm thuần: `UrlOf(IEnumerable<ProductImage>)` theo spec "Cover image", và `Renumber(IList<ProductImage>)` đánh lại `SortOrder` 0..n-1 | không |
| `Services/IProductImageService.cs` | Hợp đồng `UploadAsync`, `DeleteAsync`, `SaveLayoutAsync`; record `ProductImageUpload(string? FileName, byte[] Content, Guid? VariantId)`; enum/record kết quả `ProductImageCommandStatus` (`Success`, `NotFound`, `ValidationFailed`, `Conflict`, `StorageUnavailable`) | Shared DTO |
| `Services/ProductImageService.cs` | Điều phối D6: kiểm, lưu file, khóa, ghi DB, ảnh bìa, dọn file khi lỗi | `IProductRepository`, `IProductImageRepository`, `IUnitOfWork`, `IObjectStorage` |
| `Services/ProductDtoMapper.cs` (sửa) | Thêm `ToImageDto(ProductImage)`, nơi duy nhất map ảnh sang `ProductImageDto` (dùng bởi `ProductImageService` và `ToDto`); `ToDto` map `product.Images` theo `SortOrder` | |
| `Services/ProductService.cs` (sửa) | Bỏ gán `ImageUrl` khi tạo/sửa; `GetByIdAsync` dùng `GetDetailAsync`; `DeleteAsync` gom `ObjectKey` trước khi `Remove`, sau commit gọi `TryDeleteAsync` từng key | thêm `IObjectStorage` |
| `Services/CreateProductValidator.cs`, `UpdateProductValidator.cs` (sửa) | Bỏ luật `imageUrl` | |
| `ProductImagesController.cs` | Route `api/products/{productId:guid}/images`; `[Authorize(Roles = "Admin,Manager")]` cấp lớp; đổi `IFormFile` thành `ProductImageUpload`; map kết quả sang HTTP | `IProductImageService` |

- **Vì sao tách `ProductImageService` khỏi `ProductService`:** `ProductService` đã dài khoảng 660 dòng. Ảnh có vòng đời riêng (tải lên, xóa, sắp xếp) và dependency riêng (storage).
- **Vì sao `ProductImageService` dùng lại `IProductRepository.GetForUpdateAsync`:** khóa đúng dòng sản phẩm như mọi thao tác ghi sản phẩm khác, và nạp sẵn biến thể để kiểm `variantId`. Hai repository cùng module Catalog, không vượt ranh giới module.
- **`GetDetailAsync` mới thay vì sửa `GetWithVariantsAsync`:** giữ nguyên truy vấn mà Orders/Inventory dùng (Context).

### D5. Kiểm file

`ProductImageFileInspector.Inspect(ReadOnlySpan<byte> content)` trả về một trong các kết quả sau:

- **Rỗng:** lỗi `"The image file is empty."`
- **Quá lớn:** `> 5_242_880` bytes → lỗi `"The image file cannot exceed 5 MB."`
- **JPEG:** bắt đầu bằng `FF D8 FF` → `image/jpeg`, `.jpg`.
- **PNG:** bắt đầu bằng `89 50 4E 47 0D 0A 1A 0A` → `image/png`, `.png`.
- **WebP:** byte 0–3 là `RIFF` và byte 8–11 là `WEBP` → `image/webp`, `.webp`.
- **Còn lại:** lỗi `"Only JPEG, PNG and WebP images are allowed."`

Tên file và `Content-Type` client gửi lên bị bỏ qua (spec "Image file rules").

**Ở controller:**
- `[RequestSizeLimit(6 * 1024 * 1024)]` và `[RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]` chặn request quá lớn trước khi đọc.
- File thiếu → `ValidationProblem` cho `file`.
- `file.Length > 5_242_880` → trả lỗi ngay, không chép vào bộ nhớ.
- Còn lại chép vào `MemoryStream` rồi gọi service với `byte[]`.

Service không phụ thuộc ASP.NET (`IFormFile`), nên test được bằng mảng byte.

### D6. Luồng ghi trong `ProductImageService`

**Upload** (lưu file trước, khóa sau, để không giữ khóa dòng trong lúc gọi mạng):

```text
1. Inspect(content)                      -> lỗi: ValidationFailed{file}
2. productRepository.GetDetailAsync(id)  -> null: NotFound   (đọc thường, không khóa)
   variantId có nhưng không thuộc sản phẩm -> ValidationFailed{variantId}
   product.Images.Count >= 20            -> Conflict
3. key = products/{id:N}/{Guid.NewGuid():N}{ext}
   url = storage.PutAsync(key, ...)      -> ObjectStorageUnavailableException: StorageUnavailable (503)
4. BeginTransaction
   product = GetForUpdateAsync(id)       (khóa + nạp Variants, Images)
   kiểm lại 2 dưới khóa (sản phẩm còn, variant còn đúng, Count < 20)
     -> sai: Rollback, storage.TryDeleteAsync(key), trả NotFound/ValidationFailed/Conflict
   image = new ProductImage { SortOrder = product.Images.Count, ... }
   imageRepository.Add(image); product.Images.Add(image)
   product.ImageUrl = ProductCoverImage.UrlOf(product.Images)
   SaveChanges; Commit                   -> Success(dto)
   catch (mọi lỗi sau bước 3): Rollback, TryDeleteAsync(key), throw
```

**Vì sao gọi cả `imageRepository.Add` lẫn `product.Images.Add`:** repository là nơi duy nhất stage thay đổi (đúng quy tắc tầng). Thêm vào `product.Images` (đang được tracked) chỉ để `ProductCoverImage.UrlOf` và `Renumber` thấy đủ ảnh trong bộ nhớ. EF nhận ra cùng một đối tượng nên chỉ ghi một dòng. Khi xóa cũng vậy: `imageRepository.Remove(image)` rồi `product.Images.Remove(image)`.

Bước 2 chỉ để trả lỗi sớm mà không phải đẩy file. Bước 4 mới là kiểm tra quyết định, nên hai lần tải lên cùng lúc ở mức 19 ảnh chỉ một lần thành công (spec "Image limit per product").

**Delete:**
1. Khóa sản phẩm. Không có sản phẩm → `NotFound`. Ảnh không thuộc sản phẩm → `NotFound`.
2. `Remove`, `Renumber`, tính lại ảnh bìa, lưu, commit.
3. Sau commit gọi `TryDeleteAsync(key)`.

**Layout:**
1. `ProductImageLayoutValidator` kiểm hình dạng request.
2. Khóa sản phẩm. Không có sản phẩm → `NotFound`.
3. Tập id trong request phải bằng đúng tập id ảnh hiện có, nếu không → lỗi `items`.
4. Mỗi `variantId` phải thuộc sản phẩm, nếu không → lỗi `items[i].variantId`.
5. Gán `SortOrder = i` và `VariantId`, tính lại ảnh bìa, lưu, commit, trả danh sách.

`product.UpdatedAt` không đổi khi chỉ ảnh thay đổi: ảnh có `created_at`/`updated_at` riêng. Như vậy "Product update" và "Product activation status" giữ đúng nghĩa của `updatedAt`.

### D7. HTTP

| Kết quả service | HTTP |
|:--|:--|
| `Success` (upload) | 201 `CreatedAtAction` → `GET /api/products/{productId}`, body `ProductImageDto` |
| `Success` (layout) | 200, body `List<ProductImageDto>` |
| `Success` (delete) | 204 |
| `NotFound` | 404 |
| `ValidationFailed` | 400 `ValidationProblem` |
| `Conflict` | 409 `Problem("A product can have at most 20 images.")` |
| `StorageUnavailable` | 503 `Problem("Image storage is unavailable. Try again later.")` |

Endpoint upload dùng `[Consumes("multipart/form-data")]`, tham số `IFormFile? file`, `[FromForm] Guid? variantId`.

Request có thân lớn hơn 6 MiB bị ASP.NET dừng khi đọc form. Kiểm thật trên cổng 5177 (2026-10-23) trả **400**, lỗi nằm ở khóa rỗng `""` ("Failed to read the request form. Request body too large…"), không phải ở `file`. Từ trên 5 MB tới 6 MiB thì controller trả 400 với lỗi `file`. Hai trường hợp đều không lưu gì. Client đã lọc file trên 5 MB nên người dùng không gặp trường hợp đầu; spec ghi đúng hành vi này thay vì thêm code ép lỗi về `file`.

### D8. Hợp đồng shared

- **Mới** `shared/DTOs/ProductImageDto.cs`: `Guid Id`, `Guid? VariantId`, `string Url`, `string ContentType`, `int SizeBytes`, `int SortOrder`.
- **Sửa** `ProductDto`: thêm `List<ProductImageDto> Images { get; set; } = [];`.
- **Mới** `shared/Requests/UpdateProductImagesLayoutRequest.cs` (`List<ProductImageLayoutItemRequest>? Items`) và `ProductImageLayoutItemRequest.cs` (`Guid? Id`, `Guid? VariantId`).
- **Sửa** `CreateProductRequest` / `UpdateProductRequest`: xóa `ImageUrl`.
  - System.Text.Json bỏ qua thuộc tính lạ, nên client cũ gửi `imageUrl` vẫn tạo được sản phẩm (spec "Image URL in the request is ignored").

### D9. Transport client: `PostFileAsync`

```csharp
public sealed record ApiFileContent(string FieldName, string FileName, string ContentType, byte[] Content);

Task<TResponse> PostFileAsync<TResponse>(
    string relativePath,
    ApiFileContent file,
    IReadOnlyDictionary<string, string>? fields = null,
    CancellationToken cancellationToken = default);
```

`ApiClient` dựng `MultipartFormDataContent`:
- mỗi trường chữ là một `StringContent`;
- phần file là `ByteArrayContent` có header `Content-Type`;
- rồi gọi `SendForBodyAsync(HttpMethod.Post, ...)` có sẵn.

Nhờ vậy giữ nguyên kiểm origin, bearer token (qua `BearerTokenHandler`), timeout, chuẩn hóa lỗi, và không gửi lại.

Repository không bao giờ dựng `HttpContent`; lớp transport giữ mọi chi tiết HTTP. `ApiFileContent` nằm trong `Services/Api/`, đã được link sẵn vào test.

### D10. Hộp chọn file: hợp đồng thuần + cài đặt WinUI

- **`Services/Files/IImageFilePicker.cs`** (thuần, link vào test):
  ```csharp
  public interface IImageFilePicker
  {
      /// Opens the system picker for JPG/JPEG/PNG/WebP, multi-select. Returns an empty list when cancelled.
      /// Files larger than maxBytesToRead are returned with Content = null (not read into memory).
      Task<IReadOnlyList<PickedImageFile>> PickImagesAsync(long maxBytesToRead, CancellationToken cancellationToken = default);
  }
  public sealed record PickedImageFile(string FileName, long SizeBytes, byte[]? Content);
  ```
- **`Views/Shell/ImageFilePicker.cs`** (WinUI, cùng chỗ với `ContentDialogPresenter`):
  - dùng `FileOpenPicker` với `FileTypeFilter` `.jpg .jpeg .png .webp` và `SuggestedStartLocation = PicturesLibrary`;
  - gọi `PickMultipleFilesAsync`;
  - với mỗi file: `GetBasicPropertiesAsync().Size`, rồi chỉ đọc nội dung (`OpenStreamForReadAsync`) khi kích thước nằm trong giới hạn.
  - Thuộc tính `Window? Host` được `App.xaml.cs` gán sau khi tạo `MainWindow`. Trong `#if WINDOWS` gọi `WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(Host))`.
  - Repo chưa từng dùng `GetBasicPropertiesAsync`/`OpenStreamForReadAsync` trên Skia. Nếu một trong hai không biên dịch được trên `net10.0-desktop` hoặc ném `NotImplementedException`, dùng `new FileInfo(file.Path).Length` và `File.ReadAllBytesAsync(file.Path, cancellationToken)`; ghi cách đã dùng vào task 13.2.
  - Lỗi từ portal (không có xdg-desktop-portal) được gói thành `InvalidOperationException("Không mở được hộp chọn file.")`. ViewModel hiện thông điệp này.
- Test project thêm một dòng link `Services/Files/**/*.cs`.

### D11. ViewModel phía client: tách khỏi `ProductEditorViewModel`

| File (`ViewModels/Catalog/`) | Trách nhiệm |
|:--|:--|
| `ProductImageRules.cs` | Hằng số và luật thuần: `MaxImages = 20`, `MaxBytes = 5_242_880`, đuôi cho phép, `ContentTypeFor(fileName)`, `CoverIndex(...)` (cùng luật với server), `FilterPicked(picked, existingCount)` trả về file nhận và thông điệp lỗi tiếng Việt |
| `ProductImageItem.cs` | Một ảnh trong màn soạn: `Id?`, `Url?`, `PendingContent?`, `FileName`, `ContentType`, `[ObservableProperty] VariantRowItem? variant`, `IsSelected`, `IsCover`, `IsNew`, `VariantLabel`, `Preview` (`ImagePreview(string? Url, byte[]? Bytes)`), `MarkSaved(id, url)`. Không có kiểu WinUI |
| `ProductImageGallery.cs` | Trạng thái và lệnh của khu ảnh: `Images`, `SelectedImage`, `VariantChoices`, `SelectedVariantChoice`, `CountText`, `CanAddMore`, `PickMessage`, `ShowMakeCover`, `CanMoveLeft/Right`; lệnh `AddImages`, `SelectImage(object?)`, `MakeCover`, `MoveLeft`, `MoveRight`, `RemoveSelected`; `Load(saved, rows)`, `Reset()`, `HasChanges`, `RemovedImageIds`, `AcceptChanges()`. Theo dõi `Variants` (thêm/bỏ dòng, đổi tên) để cập nhật lựa chọn, nhãn và `VariantRowItem.ImageCount` |
| `VariantSkuMatcher.cs` | Hàm thuần: ghép các dòng chưa lưu với biến thể trong phản hồi theo SKU `OrdinalIgnoreCase`; trả map và danh sách SKU không khớp |
| `ProductImageSaver.cs` | Điều phối lưu ảnh sau khi sản phẩm đã lưu: xóa → tải lên từng ảnh mới (theo thứ tự) → layout; cập nhật từng item ngay khi bước của nó thành công, để lần thử lại chỉ gửi phần còn thiếu |
| `ProductImageViewer.cs` | Trang chi tiết: `Filters` ("Tất cả" + biến thể có ảnh), `SelectedFilter`, `VisibleImages`, `SelectedImage`, `HasThumbnails`, `HasFilter`, lệnh `SelectImage(object?)` |
| `ProductEditorItems.cs` (sửa) | `VariantRowItem`: `Id` thành settable qua `MarkSaved(Guid id)` (báo `IsSaved`, `IsSkuEditable`); `CanChooseSelling` thành observable; thêm `ImageCount` và `ImageCountText` ("" / "1 ảnh" / "2 ảnh") |
| `ProductEditorViewModel.cs` (sửa) | Bỏ mọi thứ `ImageUrl`. Thêm `public ProductImageGallery Gallery { get; }`. Nạp/đặt lại gallery khi activate. Tính `Gallery.HasChanges` vào kiểm tra thay đổi chưa lưu. `SaveAsync` gọi `ApplySavedProduct` rồi `ProductImageSaver` (D12) |
| `ProductDetailViewModel.cs` (sửa) | Thêm `public ProductImageViewer ImageViewer { get; } = new();` (không đặt tên `Images` để khỏi nhầm với `Product.Images`), gọi `ImageViewer.Load(product)` khi `Product` đổi |
| `ProductDraft.cs`, `ProductDraftValidator.cs` (sửa) | Bỏ `ImageUrl` |

**Ô "Dùng cho" và binding hai chiều:** khi `VariantChoices` được dựng lại (thêm, bỏ hay đổi tên dòng biến thể), `ComboBox` không còn thấy mục đang chọn và đẩy `null` ngược về `SelectedVariantChoice`. Nếu nhận giá trị đó, ảnh đang chọn sẽ bị đổi về "Chung" mà người dùng không hề bấm. Vì vậy `ProductImageGallery` bỏ qua mọi giá trị trong lúc dựng lại (cờ `_rebuildingChoices`), bỏ qua `null` ở mọi lúc, và sau khi dựng lại thì tự chọn lại mục có `Row` trùng `SelectedImage.Variant`. Chỉ một lựa chọn khác null mới đổi biến thể của ảnh.

`ProductEditorViewModel` nhận thêm `IImageFilePicker` và `IProductImageRepository` (thành 10 dependency) và tự tạo `new ProductImageSaver(imageRepository)`. Không tách thêm lớp vì chưa có lợi ích rõ. Độ dài lớp phải giảm hoặc giữ nguyên nhờ đưa logic ra các lớp trên.

### D12. Luồng Lưu ở màn soạn

```text
SaveAsync (IsSaving = true cho cả chuỗi -> nút Lưu bị vô hiệu)
  1. validate draft (như cũ)                    -> lỗi: dừng
  2. create hoặc update sản phẩm (như cũ)       -> lỗi: như cũ, dừng
  3. ApplySavedProduct(saved):
       ProductId = saved.Id; IsEditMode = true; PageTitle = "Chỉnh sửa sản phẩm"
       VariantSkuMatcher -> row.MarkSaved(id); mọi row.CanChooseSelling = true
       baseline sản phẩm = BuildDraft()
       SKU không khớp -> GeneralError "Đã lưu sản phẩm nhưng chưa lưu được ảnh: ..." ; dừng
  4. nếu Gallery.HasChanges: await saver.SaveAsync(saved.Id, Gallery, token)
       lỗi (ApiException / InvalidOperationException) ->
         GeneralError = "Đã lưu sản phẩm nhưng chưa lưu được ảnh: {CatalogApiErrors.Describe(ex, "Ảnh")}. Bấm Lưu để thử lại."
         dừng (ở lại màn soạn)
  5. Navigate(AppRoute.ProductDetail(saved.Id, _returnQuery, saved.Id))
```

- `CatalogApiErrors.Describe` thêm trường hợp 503: "Nơi lưu ảnh đang không truy cập được. Hãy thử lại sau." Server trả 503 khi storage lỗi (D7), nên thông báo ở bước 4 nói đúng nguyên nhân thay vì "Không thể hoàn tất thao tác".
- Bước 3 làm cho lần bấm Lưu kế tiếp gửi `PUT` thay vì `POST`, nên không tạo trùng sản phẩm.
- Route vẫn là route tạo. Nút Quay lại khi đó về danh sách, đúng với spec "Leave the editor".
- Không tự gửi lại; người dùng bấm Lưu thì chỉ các bước ảnh còn thiếu được gửi (`ProductImageSaver` dựa trên `IsNew` và `RemovedImageIds`).

**Ảnh bị người khác đổi trong lúc đang sửa.** Hai người cùng sửa một sản phẩm thì bấm Lưu lại không được kẹt mãi ở cùng một lỗi:

| Tình huống | Server trả | Client xử lý |
|:--|:--|:--|
| Xóa ảnh đã bị người khác xóa | 404 ở `DELETE .../images/{imageId}` | `ProductImageSaver` coi như đã xóa: `ForgetRemoved(id)` rồi làm tiếp. Lỗi khác vẫn ném ra như cũ |
| Layout không còn khớp ảnh trên server (người khác vừa thêm hoặc xóa ảnh) | 400 ở `PUT .../layout`, lỗi theo trường `items` | Không bảo "Bấm Lưu để thử lại" vì lưu lại vẫn lỗi. Hiện "Đã lưu sản phẩm nhưng chưa lưu được ảnh: ảnh của sản phẩm vừa được thay đổi ở nơi khác. Hãy mở lại sản phẩm để xem ảnh mới nhất." |
| Tải ảnh khi server đã đủ 20 ảnh (người khác vừa thêm) | 409 ở `POST .../images` | Cùng thông báo như dòng trên |

- Chỉ 404 của bước xóa ảnh được bỏ qua. 404 của bước tải ảnh hay layout nghĩa là sản phẩm không còn, nên vẫn báo lỗi như cũ.
- Nhận diện hai trường hợp sau bằng `ApiException` (`StatusCode == 400` và `FieldErrors` có khóa `items`, không phân biệt hoa thường; hoặc `StatusCode == 409` từ bước tải ảnh). `ProductImageSaver` ném lại đúng `ApiException` đó; `ProductEditorViewModel` chọn thông báo qua hàm thuần `CatalogApiErrors.IsImageSetChanged(ApiException)`.
- Không tự tải lại sản phẩm: người dùng chủ động mở lại để thấy ảnh mới nhất trước khi sửa tiếp.

### D13. Giao diện

| File (`Views/Catalog/`) | Trách nhiệm |
|:--|:--|
| `ProductImageGalleryView.cs` | `UserControl` C# Markup cho khu ảnh trong màn soạn. Constructor `(ProductImageGallery gallery, Uri apiBase)`. Đặt `DataContext` trên phần tử gốc bên trong, bind qua thuộc tính `Gallery` (giống mẫu `ViewModel.X` của các trang) |
| `ProductImageViewerView.cs` | `UserControl` cho trang chi tiết: ảnh lớn, ô "Xem ảnh của", hàng ảnh nhỏ |
| `CatalogViewParts.cs` (sửa) | Thêm `ImageSource? ToImageSource(ImagePreview? preview, Uri apiBase)`: có `Bytes` thì tạo `BitmapImage`, cache bằng `ConditionalWeakTable<byte[], BitmapImage>`. Repo chưa từng dựng ảnh từ bytes, nên thử theo thứ tự: (1) `bitmap.SetSource(new MemoryStream(bytes).AsRandomAccessStream())`; (2) nếu (1) không biên dịch trên `net10.0-desktop`: ghi bytes một lần ra `Path.Combine(Path.GetTempPath(), "techstore-images", Guid.NewGuid().ToString("N") + đuôi)` và dùng `new BitmapImage(new Uri(path))`. Cách đã dùng được ghi vào task 19.1; có `Url` thì dùng `ResolveImageUri`; không có thì null. Ảnh lớn và ảnh nhỏ đều đi qua `ProductImage(Action<IDependencyPropertyBuilder<ImageSource>>, iconSize)` có sẵn, nên ảnh lỗi tự hiện icon |
| `ProductEditorPage.cs` (sửa) | Bỏ `imagePreview`, `imageUrlBox`, `imageUrlError`; cột trái là `new ProductImageGalleryView(ViewModel.Gallery, _apiBase)`; dòng biến thể thêm `TextBlock` bind `variant.ImageCountText` |
| `ProductDetailPage.cs` (sửa) | `imageContainer` chứa `new ProductImageViewerView(ViewModel.ImageViewer, apiBase)` |

Theo xác nhận của chủ dự án, khung ảnh lớn trong màn soạn là một `Button` chứa preview, gắn `AddImagesCommand`, padding 0 và nội dung giãn đầy khung. Bấm khung mở picker; không có nút "+ Thêm" riêng. Command bị vô hiệu khi đủ 20 ảnh. Hàng thumbnail chỉ chọn ảnh đang xem; việc tải ảnh lên vẫn đợi nút Lưu sản phẩm.

Khung có lớp phủ nhẹ với `FontIcon` mũi tên xuống khay (`U+E896`) và chữ "Thêm ảnh". Lớp phủ nằm trong `ControlTemplate` C# Markup của Button, dùng nhóm `CommonStates`: `Normal`/`Disabled` đặt opacity 0, `PointerOver`/`Pressed` đặt opacity 1. Uno C# Markup bọc nội dung factory trong Grid `TemplateRoot`; lớp Button riêng bên trong view dùng `OnApplyTemplate` để chuyển nhóm trạng thái đã khai báo lên root này trước khi gọi base. Callback chỉ cấu hình template, không truy cập ViewModel hay xử lý pointer. Button tự chuyển trạng thái; không gắn event handler rê chuột hay gọi `GoToState` thủ công, không cần ngoại lệ D2. `UseSystemFocusVisuals` giữ dấu hiệu focus khi dùng bàn phím. Màu nền/chữ đặt trực tiếp qua token theme; lớp phủ không nhận hit test và có Visibility bind `CanAddMore` để không mời thêm khi đã đủ ảnh.

**Quy tắc bắt buộc trong hai `UserControl`:**
- Ảnh nhỏ là `Button` có `.Command(Gallery.SelectImageCommand)` (giá trị trực tiếp, như `RemoveVariantCommand`) và `.CommandParameter(x => x.Binding(() => item))`. Lệnh nhận `object?` (AGENTS.md mục 6).
- Viền "đang chọn" là một `Border` thứ hai có `BorderBrush(AppColors.Brushes.Primary)` đặt thẳng trên phần tử, với `Visibility` bind `item.IsSelected`. Không dùng converter trả brush, vì brush trả từ converter không đổi theo Sáng/Tối.
- Nhãn "Bìa", "Mới" và tên biến thể là `TextBlock` có màu đặt trên phần tử và `Visibility` bind.
- Không `Click +=`, không `ImageFailed`, không `Color.FromArgb`. Không thêm ngoại lệ D2 nào.
- Hàng ảnh nhỏ: `ItemsRepeater` với `Layout(new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8 })` và `ItemTemplate<ProductImageItem>(...)`, cùng API mà `ProductListPage` đang dùng, nằm trong `ScrollViewer` cuộn ngang. Không dùng `ItemsControl` + `ItemsPanel` vì repo chưa có chỗ nào dùng cách đó trong C# Markup.

### D14. DI và khởi động client

- `UiServiceCollectionExtensions`:
  - `AddSingleton<ImageFilePicker>()`;
  - `AddSingleton<IImageFilePicker>(sp => sp.GetRequiredService<ImageFilePicker>())`;
  - `AddTransient<IProductImageRepository, ProductImageRepository>()`.
- `App.xaml.cs`: sau khi tạo `MainWindow`, gán `GetRequiredService<ImageFilePicker>().Host = MainWindow`, theo mẫu `MarkupReferenceNavigator.Host`.

### D15. Repository client

`Repositories/Catalog/IProductImageRepository.cs` + `ProductImageRepository.cs` (lớp riêng, không gộp vào `ProductRepository` vốn đã cài đặt 4 interface):
- `UploadAsync(Guid productId, ImageUpload upload, ct)` → `PostFileAsync<JsonElement>("api/products/{id:D}/images", new ApiFileContent("file", ...), fields: variantId?)` → `ProductImage`.
- `DeleteAsync(Guid productId, Guid imageId, ct)`.
- `SaveLayoutAsync(Guid productId, IReadOnlyList<ImageLayoutEntry> entries, ct)` → `PutAsync` → `IReadOnlyList<ProductImage>`.

Kiểm hình dạng JSON và ném `ApiErrorKind.InvalidResponse` khi sai, như `ProductRepository`.

Model mới trong `Models/Catalog/`:
- `ProductImage(Guid Id, Guid? VariantId, string Url, string ContentType, int SizeBytes, int SortOrder)`;
- `ImageUpload(string FileName, string ContentType, byte[] Content, Guid? VariantId)`;
- `ImageLayoutEntry(Guid ImageId, Guid? VariantId)`.

`ProductDetail` thêm `IReadOnlyList<ProductImage> Images` (tham số cuối). `ProductRepository.ParseDetail` map `images` và coi thiếu mảng là phản hồi sai.

## Risks / Trade-offs

- **[File mồ côi trên R2]** khi xóa file thất bại sau commit, hoặc server chết giữa bước lưu file và commit → Chấp nhận. Key có tiền tố `products/{id}/` nên dọn tay được. Ghi log key. Không bao giờ có điều ngược lại (dòng DB trỏ tới file không tồn tại do lỗi của server).
- **[Đổi domain công khai]** làm `url` cũ hỏng → Chạy một câu `UPDATE product_images SET url = replace(url, '<cũ>', '<mới>')` và cập nhật `products.image_url` tương tự. Ghi trong `backend/Catalog/README.md`.
- **[Bộ nhớ client]** tối đa 20 ảnh × 5 MB giữ trong RAM khi đang soạn (≈ 100 MB) → Chấp nhận cho app desktop. File quá lớn không được đọc (D10).
- **[Timeout 30 s]** khi mạng chậm và ảnh 5 MB → Mỗi ảnh một request, nên lỗi chỉ ảnh hưởng ảnh đó và người dùng bấm Lưu lại được. Không đổi `TimeoutSeconds` toàn cục.
- **[xdg-desktop-portal không có]** trên một số máy Linux → Hộp chọn file báo lỗi rõ ràng (D10). Máy dev đã có portal.
- **[Hai người sửa layout cùng lúc]** → Người lưu sau thắng. Nếu người kia vừa thêm hay xóa ảnh thì layout sai tập ảnh và bị 400; người dùng tải lại trang.
- **[Hợp đồng đổi]** (`imageUrl` bị bỏ khỏi request, `ProductDto.Images` mới) → Client của nhóm chỉ có màn soạn Catalog gửi `imageUrl`. Báo Dev 3, Dev 4 (Coordination notes).
- **[`ProductEditorViewModel` có 10 dependency]** → Chấp nhận trong change này. Toàn bộ logic ảnh nằm ở các lớp riêng có test.
- **[Bucket công khai]** ai có link cũng xem được ảnh → Chấp nhận vì ảnh sản phẩm là thông tin công khai. Key ngẫu nhiên nên không đoán được ảnh chưa dùng.

## Coordination notes

Chủ dự án gửi cho Dev 3 (Inventory), Dev 4 (Orders) và Dev 6 (seeder/analytics) khi quyết định chia sẻ nhánh (change này không push, không mở PR):
- Có bảng mới `product_images` và migration `AddProductImages`. Ai đang có migration chưa merge cần tạo lại migration sau khi pull để snapshot không xung đột.
- `CreateProductRequest` và `UpdateProductRequest` không còn `ImageUrl`. `ProductDto` có thêm `Images`.
- `products.image_url` vẫn còn và vẫn là ảnh bìa; chỉ server được ghi cột này.
- `IProductRepository.GetWithVariantsAsync` không đổi.
- Xóa sản phẩm chưa dùng giờ xóa cả ảnh.

## Migration Plan

1. Làm và commit local trên nhánh hiện tại `feat/catalog-product-management`, nối tiếp các commit catalog. Không push, không mở PR (quyết định của chủ dự án, 2026-10-23); việc chia sẻ nhánh do chủ dự án quyết định sau.
2. Khi khởi động ở Development, server tự chạy migration `AddProductImages` (như mọi migration hiện có). Không có dữ liệu cũ cần chuyển.
3. Mặc định `Provider = Local`, nên mọi máy chạy được ngay. Bật R2 bằng user-secrets (D3).
4. **Rollback:**
   - `dotnet ef database update AddSpecDefinitions --project backend/TechStore.Api.csproj` để bỏ bảng;
   - revert commit;
   - file trên R2 / `uploads/` có thể xóa tay theo tiền tố `products/`.
