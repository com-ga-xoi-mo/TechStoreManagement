# Tasks

Read `proposal.md`, `design.md` (D1–D15) and every file under `specs/` before starting. Rules for **every** task:

- **Layering is strict.**
  - Backend: Controller → Service → Repository → `AppDbContext`. Only repositories touch `AppDbContext`. Services use repository interfaces, `IUnitOfWork` and `IObjectStorage` only. Controllers call services only.
  - Client: View → ViewModel → client Repository → `IApiClient`. No WinUI type in `Models/`, `Repositories/`, `ViewModels/`, `Services/Api|Files/`, `Controls/Presentation/`.
- **One responsibility per file:** create exactly the files named in design D2–D15. Do not put a second public type in a file unless the task says so (small records next to their interface are named in the task).
- **Code style:**
  - file-scoped namespaces and primary constructors;
  - `CancellationToken cancellationToken = default` passed to every async call;
  - never `.Result`, `.Wait()` or `async void`.
- **Backend tests:** xUnit + FluentAssertions, hand-written fakes (no Moq), `// Arrange` / `// Act` / `// Assert`, namespace `TechStore.UnitTests`, names `Method_StateUnderTest_ExpectedBehavior`.
- **Frontend tests:** plain xUnit `Assert`, the same three comments, names `Subject_State_Expected`.
- **Views:** copy the style of `Views/Catalog/ProductEditorPage.cs`. Colors only through `AppColors.Brushes.*` set on the element.
  - Forbidden: `PropertyChanged +=`, `TextChanged +=`, `Checked +=`, `Click +=`, `ImageFailed`, `Color.FromArgb`, `new SolidColorBrush`, `StitchTokens`, setting `Visibility`/`Text` in event handlers.
  - Commands that receive a template `CommandParameter` take `object?`.
- **Do not edit** these files and folders:
  - `backend/Common/Data/AppDbContext.cs`, `backend/Common/Data/UnitOfWork.cs`;
  - `backend/Orders/`, `backend/Inventory/`, `backend/Customers/`, `backend/Sales/`, `backend/Identity/`;
  - `openspec/changes/catalog-product-management-*`, `openspec/changes/archive/`, `openspec/specs/`.
- **Commands used below:**
  - `BUILD_ALL` = `dotnet build TechStore.sln`
  - `BUILD_CLIENT` = `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`
  - `TEST_BACKEND` = `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj`
  - `TEST_CLIENT` = `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`
  - "passes" means 0 errors and 0 failed tests.
- **Commits:** commit at the end of each group with the message given there. No `Co-Authored-By` line. Commit **locally only**: never run `git push` and never open a pull request (owner decision, 2026-10-23).

## 1. Branch, baseline and planning commit

- [x] 1.1 Work on the current branch `feat/catalog-product-management` (owner decision, 2026-10-23); do not create or switch branches. Verify `git branch --show-current` prints `feat/catalog-product-management` and `git status --short` shows nothing except `openspec/changes/catalog-product-images/`
- [ ] 1.2 Commit only the planning artifacts: `git add openspec/changes/catalog-product-images && git commit -m "docs(openspec): record catalog-product-images change artifacts"`; verify `git show --stat HEAD` lists only files under that folder
- [ ] 1.3 Record the baseline: run `BUILD_ALL`, `TEST_BACKEND`, `TEST_CLIENT`; verify all pass and write both passed counts into this line (baseline: backend ___, client ___)
- [ ] 1.4 Read design "Coordination notes" once. You never send it to anyone; it is handed to the owner in `evidence.md` in 20.4. Verify `grep -n "## Coordination notes" openspec/changes/catalog-product-images/design.md` prints one line

## 2. Shared contracts (additive only)

- [ ] 2.1 Create `shared/DTOs/ProductImageDto.cs` (namespace `TechStore.Shared.DTOs`, class `ProductImageDto`) with `Guid Id`, `Guid? VariantId`, `string Url = string.Empty`, `string ContentType = string.Empty`, `int SizeBytes`, `int SortOrder`. In `shared/DTOs/ProductDto.cs` add `public List<ProductImageDto> Images { get; set; } = [];` after `ImageUrl`; verify `dotnet build shared/TechStore.Shared.csproj` passes
- [ ] 2.2 Create `shared/Requests/ProductImageLayoutItemRequest.cs` (`Guid? Id`, `Guid? VariantId`) and `shared/Requests/UpdateProductImagesLayoutRequest.cs` (`List<ProductImageLayoutItemRequest>? Items`), each class in its own file, all properties nullable with no initializer; verify `BUILD_ALL` passes and `TEST_BACKEND` / `TEST_CLIENT` keep the 1.3 counts
- [ ] 2.3 Commit: `feat(shared): add product image contracts`

## 3. Object storage in `backend/Common/Storage/` (design D2, D3)

- [ ] 3.1 Add the package: `dotnet add backend/TechStore.Api.csproj package AWSSDK.S3`, then edit the `PackageReference` so it pins the exact version that was restored (no wildcard). Write the version into this line; verify `dotnet build backend/TechStore.Api.csproj` passes
- [ ] 3.2 Create these files (namespace `TechStore.Api.Common.Storage`), one type per file:
  - `IObjectStorage.cs`: interface exactly as design D2.
  - `ObjectStorageUnavailableException.cs`.
  - `ObjectStorageKey.cs`: `public static class` with `EnsureValid(string key)` using the regex from D2, throwing `ArgumentException` with message `"Invalid storage key."`.
  - `ObjectStorageOptions.cs`:
    - `const string SectionName = "ObjectStorage"`;
    - `string Provider = "Local"`;
    - nested option classes `LocalStorageOptions { RootPath = "uploads", RequestPath = "/uploads" }` and `R2StorageOptions { AccountId, AccessKeyId, SecretAccessKey, BucketName, PublicBaseUrl }` (all `string`, default `""`);
    - properties `public LocalStorageOptions Local { get; set; } = new();` and `public R2StorageOptions R2 { get; set; } = new();`;
    - `public IReadOnlyList<string> Validate()` returning the full setting names that are wrong: `ObjectStorage:Provider` when not `Local`/`R2` (ignoring case), and for R2 each empty field as `ObjectStorage:R2:<Field>`.

  Verify the backend builds.
- [ ] 3.3 Create `LocalObjectStorage.cs` (`public sealed class LocalObjectStorage(ObjectStorageOptions options, string contentRootPath, ILogger<LocalObjectStorage> logger) : IObjectStorage`):
  - `PutAsync`: calls `ObjectStorageKey.EnsureValid`, writes the bytes to `Path.Combine(contentRootPath, options.Local.RootPath, key)` (create the directory), returns `options.Local.RequestPath.TrimEnd('/') + "/" + key`. Wraps `IOException` / `UnauthorizedAccessException` into `ObjectStorageUnavailableException`.
  - `TryDeleteAsync`: deletes the file if present, returns true. On error, logs `"Could not delete stored image {Key}"` and returns false.

  Verify the backend builds.
- [ ] 3.4 Create `R2ObjectStorage.cs` (`public sealed class R2ObjectStorage(ObjectStorageOptions options, ILogger<R2ObjectStorage> logger) : IObjectStorage, IDisposable`). Build one `AmazonS3Client` in the constructor with the settings of design D3 (`ServiceURL`, `ForcePathStyle`, `AuthenticationRegion = "auto"`, both checksum settings `WHEN_REQUIRED`).
  - `PutAsync`: sends `PutObjectRequest` with `DisablePayloadSigning = true`, `ContentType` and `Headers.CacheControl`; wraps `AmazonS3Exception`, `HttpRequestException` and timeout into `ObjectStorageUnavailableException` without the secret in the message.
  - `TryDeleteAsync`: sends `DeleteObjectRequest`, catches and logs the key only.
  - Add `internal static string PublicUrlFor(string publicBaseUrl, string key)`.

  Verify the backend builds.
- [ ] 3.5 Create `ObjectStorageServiceCollectionExtensions.cs` with two methods:
  - `AddObjectStorage(this IServiceCollection services, IConfiguration configuration)`: bind the section; when `Validate()` returns names, throw `InvalidOperationException($"Missing or invalid object storage settings: {string.Join(", ", names)}")`. Register `ObjectStorageOptions` as singleton, and `IObjectStorage` as singleton `R2ObjectStorage` or `LocalObjectStorage` (passing `IHostEnvironment.ContentRootPath`).
  - `UseObjectStorageFiles(this WebApplication app)`: only for `Local`, creates the root folder and calls `app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(root), RequestPath = options.Local.RequestPath, ContentTypeProvider = <FileExtensionContentTypeProvider with ".webp" = "image/webp">, OnPrepareResponse = add "X-Content-Type-Options: nosniff" })`.

  Verify the backend builds.
- [ ] 3.6 Wire it up:
  - In `backend/Program.cs` add `builder.Services.AddObjectStorage(builder.Configuration);` after the Unit of Work registration (with a `// Object storage (product images)` comment), and `app.UseObjectStorageFiles();` right after `app.UseHttpsRedirection();`. Add `using TechStore.Api.Common.Storage;`.
  - In `backend/appsettings.json` add the `ObjectStorage` block from design D3 with `Provider` `Local` and every R2 value empty.
  - Append `backend/uploads/` to `.gitignore`.

  Verify `BUILD_ALL` passes, `ASPNETCORE_URLS=http://localhost:5177 ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/TechStore.Api.csproj --no-launch-profile` starts (stop it after the "Now listening on: http://localhost:5177" line), and `git status --short` does not list `backend/uploads`. Never start a backend on port 5176 in any task: the owner's own backend may be running there.
- [ ] 3.7 Add tests in `tests/Backend.UnitTests/ObjectStorageTests.cs`:
  - `Validate_DefaultLocalOptions_ReturnsNoErrors`;
  - `Validate_UnknownProvider_ReturnsProviderError`;
  - `Validate_R2WithoutSecret_ReturnsOnlyThatSettingName` (and the message contains no value);
  - `EnsureValid_TraversalKey_Throws` (InlineData `../a.jpg`, `products/../x.png`, `Products/A.jpg`, `a.gif`, `""`);
  - `EnsureValid_ServerKey_DoesNotThrow` (`products/0f8fad5bd9cb469fa16570867728950e/7c9e6679742540de944be07fc1f90ae7.webp`);
  - `PublicUrlFor_TrailingSlash_JoinsOnce`;
  - `LocalPutAsync_WritesFileAndReturnsRelativeUrl` (use `Directory.CreateTempSubdirectory()` as content root, delete it in `finally`);
  - `LocalTryDeleteAsync_MissingFile_ReturnsTrue`;
  - `AddObjectStorage_R2MissingSecret_ThrowsNamingTheSetting`: build the configuration with `new ConfigurationBuilder().AddInMemoryCollection(...)` (`Provider` = `R2`, every R2 field set except `SecretAccessKey`, `AccessKeyId` = `"AKID-VALUE"`); assert the `InvalidOperationException` message contains `ObjectStorage:R2:SecretAccessKey` and does not contain `AKID-VALUE`;
  - `AddObjectStorage_NoSection_RegistersLocalStorage`: empty configuration, resolve `IObjectStorage` from the built provider (register an `IHostEnvironment` stub with a temp `ContentRootPath` and `NullLogger<>` first), assert it is `LocalObjectStorage`.

  Verify `TEST_BACKEND` passes (baseline + new tests).
- [ ] 3.8 Create `tests/Backend.UnitTests/FakeObjectStorage.cs`:
  - `public Dictionary<string, byte[]> Objects`, `public List<string> DeletedKeys`, `public bool ThrowOnPut`, `public int PutCount`;
  - `PutAsync`: throws `ObjectStorageUnavailableException` when `ThrowOnPut`, otherwise stores the bytes and returns `"https://img.test/" + key`;
  - `TryDeleteAsync`: records the key, removes it and returns true.

  Verify `TEST_BACKEND` passes.
- [ ] 3.9 Update `AGENTS.md` for the backend storage (design Goals):
  - section 3, "Services" bullet: replace the sentence that ends "depend only on repository interfaces and `IUnitOfWork`." with "Services depend only on repository interfaces, `IUnitOfWork` and infrastructure ports from `Common/` (`IObjectStorage`, `TimeProvider`).";
  - "Key Modules", `backend/Common/` bullet: add `Storage/` (`IObjectStorage`, `ObjectStorageOptions`, R2 or local folder);
  - "Key Directories": add `Storage/` under `Common/` with a one-line comment;
  - section 9, Backend Singleton bullet: add `IObjectStorage` (`R2ObjectStorage` or `LocalObjectStorage`).

  Verify `grep -n "IObjectStorage" AGENTS.md` prints at least three lines and `grep -n "repository interfaces and .IUnitOfWork.\." AGENTS.md` prints nothing.
- [ ] 3.10 Commit: `feat(api): add configurable object storage for images`

## 4. Entity, configuration, migration and schema docs (design D1)

- [ ] 4.1 Create `backend/Catalog/Entities/ProductImage.cs` (`public class ProductImage : BaseEntity`) with the properties of design D4 and navigations `Product Product = null!`, `ProductVariant? Variant`. In `Product.cs` add `public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();`; verify the backend builds
- [ ] 4.2 Create `backend/Catalog/Configurations/ProductImageConfiguration.cs` exactly as design D1:
  - table `product_images` with the three check constraints `ck_product_images_sort_order_ge_zero`, `ck_product_images_size_bytes_range`, `ck_product_images_content_type`;
  - snake_case columns with the given lengths; `id` default `gen_random_uuid()`; `created_at` default `now()`; `timestamptz`;
  - index `idx_product_images_product_id` on `(ProductId, SortOrder)`, index `idx_product_images_variant_id` on `VariantId`, unique `uq_product_images_object_key`;
  - `HasOne(x => x.Product).WithMany(p => p.Images).HasForeignKey(x => x.ProductId).OnDelete(Cascade)`;
  - `HasOne(x => x.Variant).WithMany().HasForeignKey(x => new { x.VariantId, x.ProductId }).HasPrincipalKey(v => new { v.Id, v.ProductId }).OnDelete(Cascade)`.

  Verify the backend builds.
- [ ] 4.3 Create the migration: `dotnet ef migrations add AddProductImages --project backend/TechStore.Api.csproj --output-dir Common/Data/Migrations` (if `dotnet ef` is missing, install it with `dotnet tool install dotnet-ef --version 10.0.13 --tool-path <scratch folder>` and call that binary). Open the generated file and verify it only creates `product_images` with the two FKs, three indexes and three checks, and drops nothing. Verify `dotnet ef migrations script AddSpecDefinitions AddProductImages --project backend/TechStore.Api.csproj` prints `CREATE TABLE product_images`
- [ ] 4.4 Apply and inspect: start the database (`docker compose up -d`), run `dotnet ef database update --project backend/TechStore.Api.csproj`; verify `docker exec techstore_postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "\d product_images"'` shows the columns, both FKs (one composite) and the indexes
- [ ] 4.5 In `docs/database/schema.dbml`:
  - add `Table product_images` after `product_variants`, with the columns, notes and indexes of D1;
  - add `Ref: product_images.product_id > products.id [delete: cascade]` and the composite ref `Ref: product_images.(variant_id, product_id) > product_variants.(id, product_id) [delete: cascade]`;
  - add `product_images` to `TableGroup Catalog`;
  - change the `products.image_url` note to `'Cover image URL, computed by the server from product_images (first shared image); clients never set it'`.

  Verify the file still opens in dbdiagram syntax (no unmatched braces: `grep -c "{" ` equals `grep -c "}"` for the file).
- [ ] 4.6 Add `CatalogEntityTests` cases in `tests/Backend.UnitTests/CatalogEntityTests.cs` (follow the existing in-memory model tests there):
  - `ProductImage_Model_HasCompositeVariantForeignKey` (the FK on `product_images` with properties `VariantId, ProductId` targets `ProductVariant` with principal key `Id, ProductId`);
  - `ProductImage_Model_HasRequiredIndexes`.

  Verify `TEST_BACKEND` passes.
- [ ] 4.7 Commit: `feat(catalog): add product_images table`

## 5. Repositories (design D4)

- [ ] 5.1 Create `backend/Catalog/Repositories/IProductImageRepository.cs` with `void Add(ProductImage image);` and `void Remove(ProductImage image);` (XML comment: "Stages changes only; the service saves through IUnitOfWork"). Create `ProductImageRepository.cs` (`public sealed class ProductImageRepository(AppDbContext dbContext)`) using `dbContext.Set<ProductImage>()`. Register `builder.Services.AddScoped<IProductImageRepository, ProductImageRepository>();` under "Catalog Module repositories" in `Program.cs`; verify the backend builds
- [ ] 5.2 In `IProductRepository` add `Task<Product?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);` with comment "Read-only product with category, variants and images (sorted by SortOrder) for the detail response." Implement it in `ProductRepository` with `AsNoTracking().AsSplitQuery().Include(p => p.Category).Include(p => p.Variants).Include(p => p.Images.OrderBy(i => i.SortOrder))`. In `GetForUpdateAsync`, after loading variants, also `await dbContext.Entry(product).Collection(p => p.Images).LoadAsync(cancellationToken);`. Do **not** change `GetWithVariantsAsync`; verify `git diff backend/Catalog/Repositories/ProductRepository.cs` shows no change inside `GetWithVariantsAsync`, and the backend builds
- [ ] 5.3 Test doubles:
  - In `tests/Backend.UnitTests/FakeProductRepository.cs` add `GetDetailAsync` returning the stored product (same instance), and keep every existing member unchanged.
  - Create `tests/Backend.UnitTests/FakeProductImageRepository.cs` with `List<ProductImage> Added` and `List<ProductImage> Removed`.

  Verify `TEST_BACKEND` passes with the count from 4.6.
- [ ] 5.4 Commit: `feat(catalog): add product image repository and detail load`

## 6. Pure image rules (design D4, D5)

- [ ] 6.1 Create `backend/Catalog/Services/ProductImageFileInspector.cs`:
  - `public static class` with `public const int MaxBytes = 5_242_880;`;
  - `public sealed record ProductImageFileInfo(string ContentType, string Extension);` in the same file;
  - `public static ProductImageFileInfo? Inspect(ReadOnlySpan<byte> content, out string? error)` following design D5 with the exact English messages.

  Verify the backend builds.
- [ ] 6.2 Create `backend/Catalog/Services/ProductCoverImage.cs` (`internal static class`):
  - `string? UrlOf(IEnumerable<ProductImage> images)`: order by `SortOrder`, first with `VariantId == null`, else first, else null;
  - `void Renumber(IList<ProductImage> imagesInOrder)`: sets `SortOrder = index`.

  Verify the backend builds.
- [ ] 6.3 Create `backend/Catalog/Services/ProductImageLayoutValidator.cs` (`public static class`, `Dictionary<string, string[]> Validate(UpdateProductImagesLayoutRequest? request)`):
  - null request or null `Items` → `items` "items is required.";
  - item null or `Id` null/`Guid.Empty` → `items[i].id` "Image id is required.";
  - a repeated id → `items[i].id` "Image '{id}' is listed more than once.";
  - `VariantId == Guid.Empty` → `items[i].variantId` "Variant id cannot be empty.".

  Verify the backend builds.
- [ ] 6.4 Tests in `tests/Backend.UnitTests/ProductImageRulesTests.cs`:
  - Inspector: `Inspect_JpegSignature_ReturnsJpeg`, `Inspect_PngSignature_ReturnsPng`, `Inspect_WebpSignature_ReturnsWebp`, `Inspect_TextNamedJpg_ReturnsTypeError` (bytes of `"hello"`), `Inspect_Empty_ReturnsEmptyError`, `Inspect_OverLimit_ReturnsSizeError` (`new byte[MaxBytes + 1]` with a PNG header), `Inspect_ExactlyLimit_Passes`. Build byte arrays inline.
  - Cover: `UrlOf_SharedAfterVariantImage_ReturnsShared`, `UrlOf_OnlyVariantImages_ReturnsFirst`, `UrlOf_NoImages_ReturnsNull`, `Renumber_Gaps_MakesZeroBased`.
  - Layout validator: `Validate_NullItems_ReturnsItemsError`, `Validate_DuplicateId_ReturnsErrorOnSecond`, `Validate_EmptyVariantId_ReturnsVariantError`, `Validate_ValidItems_ReturnsNoErrors`.

  Verify `TEST_BACKEND` passes.
- [ ] 6.5 Commit: `feat(catalog): add image file, cover and layout rules`

## 7. `ProductImageService` (design D6) with tests

- [ ] 7.1 Create `backend/Catalog/Services/IProductImageService.cs` with:
  - `public sealed record ProductImageUpload(string? FileName, byte[] Content, Guid? VariantId);`
  - `public enum ProductImageCommandStatus { Success, NotFound, ValidationFailed, Conflict, StorageUnavailable }`
  - `public sealed record ProductImageCommandResult<T>(ProductImageCommandStatus Status, T? Data, Dictionary<string, string[]>? ValidationErrors, string? Message)` with static factories `Success`, `NotFound`, `ValidationFailed`, `Conflict(message)`, `StorageUnavailable(message)`
  - the interface with `UploadAsync(Guid productId, ProductImageUpload upload, ct)` → `ProductImageCommandResult<ProductImageDto>`, `DeleteAsync(Guid productId, Guid imageId, ct)` → `ProductImageCommandResult<bool>`, `SaveLayoutAsync(Guid productId, UpdateProductImagesLayoutRequest request, ct)` → `ProductImageCommandResult<IReadOnlyList<ProductImageDto>>`

  Verify the backend builds.
- [ ] 7.2 Create `backend/Catalog/Services/ProductImageService.cs` (`public sealed class ProductImageService(IProductRepository productRepository, IProductImageRepository imageRepository, IUnitOfWork unitOfWork, IObjectStorage storage) : IProductImageService`) and implement `UploadAsync` exactly as the design D6 pseudo-code:
  - storage key `products/{productId:N}/{Guid.NewGuid():N}{extension}`;
  - the second check happens under `GetForUpdateAsync`;
  - every exit after the file was stored and before commit calls `storage.TryDeleteAsync(key)`;
  - the conflict message is `"A product can have at most 20 images."`;
  - the storage message is `"Image storage is unavailable. Try again later."`.

  Map to `ProductImageDto` through a new `public static ProductImageDto ToImageDto(ProductImage image)` added to `backend/Catalog/Services/ProductDtoMapper.cs`; it is the only image-to-DTO mapping in the backend (group 9 reuses it, so do not write a private copy in the service). Register `builder.Services.AddScoped<IProductImageService, ProductImageService>();` under "Catalog Module services". Verify the backend builds.
- [ ] 7.3 Implement `DeleteAsync` and `SaveLayoutAsync` in the same class as design D6:
  - lock with `GetForUpdateAsync`;
  - `ProductCoverImage.Renumber` / `UrlOf`;
  - layout id set must equal the stored set (error key `items`, message `"The layout must list every image of the product exactly once."`);
  - foreign `variantId` → `items[i].variantId` `"Variant '{id}' does not belong to this product."`;
  - `TryDeleteAsync` only after `CommitAsync`;
  - every catch block rolls back and rethrows, like `ProductService`.

  Verify the backend builds.
- [ ] 7.4 Tests in `tests/Backend.UnitTests/ProductImageServiceTests.cs` (fixture: `FakeProductRepository`, `FakeProductImageRepository`, `FakeUnitOfWork`, `FakeObjectStorage`, products from `ProductCommandTestData`; PNG bytes helper `Png(int size = 100)`):
  - `UploadAsync_FirstSharedImage_StoresFileAddsRowAndSetsCover`;
  - `UploadAsync_AppendsAfterExistingImages`;
  - `UploadAsync_InvalidFile_ReturnsFileErrorAndStoresNothing`;
  - `UploadAsync_UnknownProduct_ReturnsNotFoundAndStoresNothing`;
  - `UploadAsync_ForeignVariant_ReturnsVariantErrorAndStoresNothing`;
  - `UploadAsync_TwentyImages_ReturnsConflictAndStoresNothing`;
  - `UploadAsync_LimitReachedUnderLock_DeletesStoredFile` (make `GetDetailAsync` see 19 and `GetForUpdateAsync` see 20 by adding `public Action? BeforeGetForUpdate { get; set; }` to `FakeProductRepository`, invoked first thing in `GetForUpdateAsync`; this is the only new member this task may add to that fake, and the test uses it to add a 20th image between the two calls);
  - `UploadAsync_StorageDown_ReturnsStorageUnavailableAndNoRow`;
  - `UploadAsync_SaveThrows_DeletesStoredFileAndRethrows`;
  - `UploadAsync_VariantImage_KeepsSharedCover`;
  - `DeleteAsync_MiddleImage_RenumbersAndDeletesFileAfterCommit` (assert `CommitCount` 1 and `DeletedKeys` contains the key);
  - `DeleteAsync_LastImage_ClearsCover`;
  - `DeleteAsync_ImageOfAnotherProduct_ReturnsNotFound`;
  - `DeleteAsync_UnknownProduct_ReturnsNotFound`;
  - `SaveLayoutAsync_Reorder_SetsSortOrderAndCover`;
  - `SaveLayoutAsync_MissingImage_ReturnsItemsErrorAndChangesNothing`;
  - `SaveLayoutAsync_AssignsVariant`;
  - `SaveLayoutAsync_ForeignVariant_ReturnsIndexedError`;
  - `SaveLayoutAsync_UnknownProduct_ReturnsNotFound`.

  Verify `TEST_BACKEND` passes.
- [ ] 7.5 Commit: `feat(catalog): add product image service`

## 8. Controller, docs and manual requests (design D5, D7)

- [ ] 8.1 Create `backend/Catalog/ProductImagesController.cs`:
  - `[ApiController]`, `[Route("api/products/{productId:guid}/images")]`, `[Authorize(Roles = "Admin,Manager")]` on the class;
  - constructor injection of `IProductImageService` only;
  - actions `Upload` (`[HttpPost]`, `[Consumes("multipart/form-data")]`, `[RequestSizeLimit]` + `[RequestFormLimits]` of design D5, parameters `IFormFile? file`, `[FromForm] Guid? variantId`), `Delete` (`[HttpDelete("{imageId:guid}")]`), `SaveLayout` (`[HttpPut("layout")]`, `[FromBody] UpdateProductImagesLayoutRequest request`);
  - each action has `ProducesResponseType` attributes like `ProductsController`;
  - status mapping exactly as the design D7 table; `CreatedAtAction(nameof(ProductsController.GetById), "Products", new { id = productId }, dto)`.

  Verify the backend builds, start it with `ASPNETCORE_URLS=http://localhost:5177 ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/TechStore.Api.csproj --no-launch-profile`, check that Swagger (`http://localhost:5177/`) shows the three endpoints with a file input, then stop it.
- [ ] 8.2 Add `tests/Backend.UnitTests/ProductImagesControllerAuthorizationTests.cs` following `ProductsControllerAuthorizationTests`: the class has `Authorize` with roles `Admin,Manager`; `Upload` has `Consumes("multipart/form-data")` and a request size limit of 6 MB (read it as `((IRequestSizeLimitMetadata)attribute).MaxRequestBodySize`, which must equal `6_291_456`; `RequestSizeLimitAttribute` has no public property for the limit); no action has `AllowAnonymous`. Verify `TEST_BACKEND` passes
- [ ] 8.3 Add sample requests to `backend/TechStore.Api.http` (upload with `multipart/form-data` boundary and `< ./samples/pixel.png`, delete, layout) and create `backend/samples/pixel.png` (a valid 1×1 PNG: `echo iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg== | base64 -d > backend/samples/pixel.png`); verify `file backend/samples/pixel.png` prints `PNG image data, 1 x 1`
- [ ] 8.4 Update `backend/Catalog/README.md`:
  - list the three image endpoints with status codes;
  - add a "Product images storage" section with the `ObjectStorage` settings, the `dotnet user-secrets` commands of design D3, the R2 setup steps (bucket, public development URL, API token with Object Read & Write on that bucket), the cover rule, and the domain-change `UPDATE` from design Risks.

  Also update `AGENTS.md`:
  - "Key Modules", `backend/Catalog/` bullet: add `IProductImageService`, `IProductImageRepository`, `ProductImagesController`;
  - section 9, Backend Scoped bullet: add `IProductImageRepository`, `IProductImageService`;
  - section 5: add a short "Product images" list: files live in object storage and the database keeps only references (`product_images`); `products.image_url` is the cover computed by the server; at most 20 images of at most 5 MB (JPEG/PNG/WebP by content); credentials only through user-secrets or environment variables.

  Verify every endpoint path in the README exists in a controller (`grep -n "images" backend/Catalog/README.md`) and `grep -n "IProductImageService" AGENTS.md` prints at least two lines.
- [ ] 8.5 Commit: `feat(catalog): add product image endpoints`

## 9. Product service: images in detail, no client `imageUrl`, cleanup on delete

- [ ] 9.1 In `ProductDtoMapper.ToDto` set `Images = product.Images.OrderBy(i => i.SortOrder).Select(ToImageDto).ToList()` (reuse the 7.2 mapper; no second mapping). In `ProductService.GetByIdAsync` replace `GetWithVariantsAsync` with `GetDetailAsync`; verify the backend builds
- [ ] 9.2 Remove `ImageUrl` from `shared/Requests/CreateProductRequest.cs` and `UpdateProductRequest.cs`. Fix every compile error the build reports, and only these:
  - drop the `imageUrl` rule in `CreateProductValidator` and `UpdateProductValidator`;
  - drop the two `ImageUrl =` assignments in `ProductService` (create leaves `ImageUrl` null; update no longer touches it);
  - `ProductCommandTestData.NewRequestFrom` stops copying it; in `ProductServiceUpdateTests.UpdateAsync_BlankOptionalStrings_StoredAsNull` delete the three `ImageUrl` lines (`product.ImageUrl = "https://example.com/img.png";`, `request.ImageUrl = "   ";`, `product.ImageUrl.Should().BeNull();`) and keep the barcode and description checks;
  - in the client, only the two `ImageUrl = imageUrlToSend` lines in `ProductDraftValidator` and the `imageUrlToSend` local (the rest of the client clean-up is group 18).

  Verify `BUILD_ALL` passes.
- [ ] 9.3 Add `IObjectStorage objectStorage` as the last parameter of the `ProductService` primary constructor. In `DeleteAsync`, before `productRepository.Remove(product)`, take `var keys = product.Images.Select(i => i.ObjectKey).ToList();`; after `CommitAsync`, `foreach (var key in keys) await objectStorage.TryDeleteAsync(key, cancellationToken);`. Update the 18 `new ProductService(` calls in the 8 test files to pass `new FakeObjectStorage()` (use `grep -rn "new ProductService(" tests` to find them); verify `TEST_BACKEND` passes
- [ ] 9.4 Add tests in the existing files:
  - `ProductServiceTests`: `GetByIdAsync_ProductWithImages_ReturnsImagesInSortOrder`, and `CreateAsync_NewProduct_HasNoImagesAndNullCover`.
  - `ProductServiceUpdateTests`: `UpdateAsync_ProductWithImages_KeepsImagesAndCover`.
  - `ProductServiceDeleteTests`: `DeleteAsync_ProductWithImages_DeletesFilesAfterCommit`, and `DeleteAsync_InUseProduct_DeletesNoFile`.
  - A test that a JSON body containing `"imageUrl"` still deserializes into `CreateProductRequest` without error (`JsonSerializer.Deserialize<CreateProductRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))`).

  Verify `TEST_BACKEND` passes.
- [ ] 9.5 Commit: `feat(catalog): serve product images and drop client image URLs`

## 10. Backend live check (local storage)

- [ ] 10.1 Build the API to a scratch folder (`dotnet build backend/TechStore.Api.csproj -o <scratch>/api`) and run it **from inside that folder** so the content root, and therefore the uploads folder, is `<scratch>/api/uploads`: `cd <scratch>/api && ASPNETCORE_URLS=http://localhost:5177 ASPNETCORE_ENVIRONMENT=Development ./TechStore.Api` (the build copies `appsettings.Development.json` into that folder, so the connection string is already there). Port 5177 keeps the owner's own backend on 5176 untouched. Log in with `POST /api/auth/login` (admin / Admin@123). Pick a product id from `GET /api/products?pageSize=1`. Then with `curl`:
  - upload `backend/samples/pixel.png` (201, `url` starts with `/uploads/products/`);
  - `GET` the returned `url` (200, `Content-Type: image/png`, header `X-Content-Type-Options: nosniff`);
  - upload a text file renamed `.jpg` (400 `file`);
  - upload without a token (401);
  - `PUT` layout with the image id (200);
  - `GET /api/products/{id}` (`images` has one item and `imageUrl` equals its url);
  - `DELETE` the image (204, the file disappears from `<scratch>/api/uploads/...`, `imageUrl` null).

  Write the status codes into this line, stop the API (`pkill -f "<scratch>/api/TechStore.Api"`) and delete `<scratch>/api/uploads`.
- [ ] 10.2 Delete-product cleanup: create a throwaway product (`POST /api/products`, SKU `SMOKE-IMG-<time>`), upload two images, `DELETE /api/products/{id}`; verify 204, both files are gone from the uploads folder and `select count(*) from product_images where product_id = '<id>'` returns 0. Write the result into this line

## 11. Client transport: `PostFileAsync` (design D9, spec `frontend-api-access`)

- [ ] 11.1 Create `frontend/TechStore.Client/Services/Api/ApiFileContent.cs` with the record from design D9 (`ArgumentException` in a static `Create` is not needed; validate in `ApiClient`). Add `PostFileAsync<TResponse>` to `IApiClient.cs` after `PostAsync<TRequest>`; do not build yet
- [ ] 11.2 Implement `PostFileAsync` in `ApiClient.cs`:
  - throw `ArgumentNullException` for a null file and `ArgumentException` for an empty `FieldName`/`FileName`;
  - build `MultipartFormDataContent`: each field as `StringContent`, then `ByteArrayContent` with `ContentType` set, added with `(content, file.FieldName, file.FileName)`;
  - return `SendForBodyAsync<TResponse>(HttpMethod.Post, relativePath, multipart, cancellationToken)`.

  Verify `BUILD_CLIENT` passes.
- [ ] 11.3 Add `PostFileAsync` to every test stub that implements `IApiClient`:
  - `StubApiClient` in `Repositories/Catalog/ProductRepositoryTests.cs`;
  - `JsonApiClient` and `ThrowingApiClient` in `Repositories/Catalog/CategoryRepositoryTests.cs`;
  - `ThrowingApiClient` in `Development/DashboardPreviewOptionsTests.cs`;
  - `StubApiClient` in `Repositories/Analytics/AnalyticsApiRepositoryTests.cs`;

  These throw `NotSupportedException`. In `Repositories/Orders/RecordingApiClient.cs`, `PostFileAsync` records `new RecordedCall("POST", relativePath, file)`, stores `fields` in a new `public Dictionary<string, string>? LastFields`, and returns `Handle<TResponse>`-style output from `respond("POST", relativePath, file)`. Verify `TEST_CLIENT` keeps the baseline count.
- [ ] 11.4 Add tests to `tests/Frontend.UnitTests/Transport/ApiClientTransportTests.cs`:
  - `PostFileAsync_SendsMultipartWithFileAndFields`: the captured request is POST, `Content-Type` starts with `multipart/form-data`, the body contains `name="file"; filename="a.jpg"`, `Content-Type: image/jpeg`, the bytes and `name="variantId"`;
  - `PostFileAsync_MapsValidationProblemAndDoesNotRetry`: 400 with `errors.file`, one request;
  - `PostFileAsync_RejectsForeignOrAbsoluteRoutesBeforeSending`;
  - `PostFileAsync_SendsBearerToken`: build the client with `BearerTokenHandler` exactly as `Transport/ApiClientErrorTests.cs` does, and assert the captured request has `Authorization: Bearer <token>`.

  Verify `TEST_CLIENT` passes.
- [ ] 11.5 Commit: `feat(client): add multipart file upload to the API client`

## 12. Client models and image repository (design D15)

- [ ] 12.1 Create `frontend/TechStore.Client/Models/Catalog/ProductImage.cs`, `ImageUpload.cs`, `ImageLayoutEntry.cs` (one record each, design D15). Add `IReadOnlyList<ProductImage> Images` as the last parameter of `ProductDetail`. In `ProductRepository`:
  - map `dto.Images` in `ParseDetail` (ordered by `SortOrder`);
  - make `ValidateDetailShape` require an `images` array whose items have string `id`, string `url` and number `sortOrder`.

  Fix `tests/Frontend.UnitTests/ViewModels/Orders/OrderFakes.cs` and every JSON fixture the tests report (add `"images": []`). Verify `TEST_CLIENT` passes.
- [ ] 12.2 Create `frontend/TechStore.Client/Repositories/Catalog/IProductImageRepository.cs` (three methods of design D15, XML comments naming the route and the status codes that reach the caller as `ApiException`) and `ProductImageRepository.cs` (`public sealed class ProductImageRepository(IApiClient apiClient)`):
  - upload sends `new ApiFileContent("file", upload.FileName, upload.ContentType, upload.Content)` with field `variantId` only when set, to `api/products/{productId:D}/images`;
  - delete calls `DeleteAsync("api/products/{productId:D}/images/{imageId:D}")`;
  - layout calls `PutAsync<UpdateProductImagesLayoutRequest, JsonElement>("api/products/{productId:D}/images/layout", ...)`;
  - responses are validated like `ProductRepository` (throw `ApiException(ApiErrorKind.InvalidResponse, ...)`);
  - `Guid.Empty` ids throw `ArgumentException`.

  Verify `BUILD_CLIENT` passes.
- [ ] 12.3 Tests in `tests/Frontend.UnitTests/Repositories/Catalog/ProductImageRepositoryTests.cs` using `RecordingApiClient`:
  - `UploadAsync_SharedImage_PostsFileWithoutVariantField`;
  - `UploadAsync_VariantImage_SendsVariantId`;
  - `UploadAsync_MalformedResponse_ThrowsInvalidResponse`;
  - `DeleteAsync_CallsDeleteRoute`;
  - `SaveLayoutAsync_SendsItemsInOrderAndMapsList`;
  - `ProductDetail_Images_AreMappedInSortOrder` (in `ProductRepositoryTests`).

  Verify `TEST_CLIENT` passes.
- [ ] 12.4 Register `services.AddTransient<IProductImageRepository, ProductImageRepository>();` next to the other catalog repositories in `UiServiceCollectionExtensions.cs`; verify `BUILD_CLIENT` passes and the composition test in `tests/Frontend.UnitTests/Composition/` still passes
- [ ] 12.5 Commit: `feat(client): add product image repository`

## 13. File picker (design D10, D14)

- [ ] 13.1 Create `frontend/TechStore.Client/Services/Files/IImageFilePicker.cs` with the interface and the `PickedImageFile` record exactly as design D10. In `tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` add `<Compile Include="..\..\frontend\TechStore.Client\Services\Files\**\*.cs" Link="Production\Services\Files\%(RecursiveDir)%(Filename)%(Extension)" />` next to the other `Services` links; verify `TEST_CLIENT` builds and passes
- [ ] 13.2 Create `frontend/TechStore.Client/Views/Shell/ImageFilePicker.cs` (`public sealed class ImageFilePicker : IImageFilePicker`) as design D10:
  - `Window? Host` property;
  - filters `.jpg .jpeg .png .webp`;
  - `PickMultipleFilesAsync()` (null or empty result → empty list);
  - size from `GetBasicPropertiesAsync()` and content from `OpenStreamForReadAsync()`, read only when `size <= maxBytesToRead`. If either does not compile on `net10.0-desktop`, use the design D10 fallback (`new FileInfo(file.Path).Length`, `File.ReadAllBytesAsync(file.Path, cancellationToken)`) and write which one you used into this line;
  - the `#if WINDOWS` window-handle block;
  - any exception from the picker call → `InvalidOperationException("Không mở được hộp chọn file.", ex)`;
  - honour `cancellationToken` between files.

  Verify `BUILD_CLIENT` passes.
- [ ] 13.3 Register the picker in `UiServiceCollectionExtensions.cs` as design D14 and set `Host` in `App.xaml.cs` next to the `MarkupReferenceNavigator` line (outside the gallery `if`, right after `MainWindow` is created); verify `BUILD_CLIENT` passes and `TEST_CLIENT` passes (composition test)
- [ ] 13.4 Commit: `feat(client): add image file picker`

## 14. Pure image rules and items on the client (design D11)

- [ ] 14.1 Create `frontend/TechStore.Client/ViewModels/Catalog/ProductImageRules.cs` (`public static class`) with:
  - `MaxImages = 20`, `MaxBytes = 5_242_880`;
  - `IsAllowedExtension(string fileName)` (case-insensitive `.jpg .jpeg .png .webp`);
  - `ContentTypeFor(string fileName)` (`image/jpeg`, `image/png`, `image/webp`);
  - `int CoverIndex<T>(IReadOnlyList<T> items, Func<T, bool> isShared)` (first shared, else 0, else -1);
  - `PickFilterResult FilterPicked(IReadOnlyList<PickedImageFile> picked, int existingCount)`, with `public sealed record PickFilterResult(IReadOnlyList<PickedImageFile> Accepted, IReadOnlyList<string> Messages)` in the same file. Messages:
    - `"{name}: chỉ nhận JPG, PNG, WebP"`;
    - `"{name} lớn hơn 5 MB"`;
    - `"Chỉ thêm được {n} ảnh nữa (tối đa 20)"` once, when files are dropped for the limit;
    - when `n` is 0: `"Đã đủ 20 ảnh"`.

  Verify `BUILD_CLIENT` passes.
- [ ] 14.2 Create `ProductImageItem.cs` (design D11) with `public sealed record ImagePreview(string? Url, byte[]? Bytes);` in the same file:
  - `IsNew => Id is null`;
  - `VariantLabel => Variant?.VariantName ?? string.Empty`;
  - `HasVariantLabel`;
  - raise `VariantLabel`/`HasVariantLabel` when `Variant` changes;
  - `MarkSaved(Guid id, string url)` sets both, clears `PendingContent`, and raises `IsNew` and `Preview`;
  - static factories `FromSaved(ProductImage image, VariantRowItem? row)` and `FromPicked(PickedImageFile file)`.

  Verify `BUILD_CLIENT` passes.
- [ ] 14.3 Change `VariantRowItem` in `ProductEditorItems.cs`:
  - `Id` gets a private setter and `public void MarkSaved(Guid id)`, which sets it and raises `Id`, `IsSaved`, `IsSkuEditable`;
  - `CanChooseSelling` becomes `[ObservableProperty] private bool canChooseSelling = true;` (keep object-initializer use working);
  - add `[ObservableProperty] private int imageCount;` and `ImageCountText => ImageCount == 0 ? string.Empty : $"{ImageCount} ảnh"`, raised on change.

  Verify `TEST_CLIENT` passes.
- [ ] 14.4 Create `VariantSkuMatcher.cs` (`public static class`, `public static SkuMatchResult Match(IReadOnlyList<VariantRowItem> rows, IReadOnlyList<ProductVariant> saved)`, with `public sealed record SkuMatchResult(IReadOnlyDictionary<VariantRowItem, Guid> Matches, IReadOnlyList<string> MissingSkus)` in the same file). Only rows with `Id == null` are matched, by trimmed SKU with `StringComparer.OrdinalIgnoreCase`; verify `BUILD_CLIENT` passes
- [ ] 14.5 Tests in `tests/Frontend.UnitTests/ViewModels/Catalog/ProductImageRulesTests.cs`:
  - `IsAllowedExtension` for `A.JPG`, `b.webp`, `c.heic`;
  - `CoverIndex` (variant first then shared → 1; only variants → 0; empty → -1);
  - `FilterPicked`: wrong type, oversize (`Content` null), limit with 18 existing and 4 picked (2 accepted + one message), 20 existing.

  And in `VariantSkuMatcherTests.cs`:
  - `Match_DifferentOrderAndCase_UsesSku` (rows `ip16-bk`, `IP16-WH`; saved in reverse order, upper case);
  - `Match_SavedRowsAreSkipped`;
  - `Match_MissingSku_IsReported`.

  And `ProductImageItemTests.cs`:
  - `MarkSaved_ClearsPendingAndIsNew`;
  - `VariantLabel_FollowsRowRename`.

  Verify `TEST_CLIENT` passes.
- [ ] 14.6 Commit: `feat(client): add product image rules and items`

## 15. `ProductImageGallery` (design D11, spec `frontend-product-images`)

- [ ] 15.1 Create `ProductImageGallery.cs` (`public sealed partial class ProductImageGallery : ObservableObject`, constructor `(IImageFilePicker picker, ObservableCollection<VariantRowItem> variants, Func<CancellationToken> token)`). Members:
  - **state:** `Images`, `SelectedImage`, `VariantChoices`, `SelectedVariantChoice`, `CountText`, `CanAddMore`, `PickMessage` / `HasPickMessage`, `ShowMakeCover`, `CanMoveLeft`, `CanMoveRight`, `HasSelection`, `LargePreview`;
  - `public sealed record ImageVariantChoice(string Label, VariantRowItem? Row);` in the same file, where the first choice is `"Chung cho mọi biến thể"` with `Row` null;
  - **selection and cover:** selecting sets `IsSelected` on exactly one item; changing `SelectedVariantChoice` sets `SelectedImage.Variant`; after every change, recompute `IsCover` with `ProductImageRules.CoverIndex` and each row's `ImageCount`;
  - **row tracking:** subscribe to `variants.CollectionChanged` (removed rows → their images become shared; rebuild choices) and to each row's `PropertyChanged` for `VariantName` (rebuild choices; the item raises `VariantLabel`). Unsubscribe from removed rows.
  - **"Dùng cho" and the two-way binding** (design D11): while `VariantChoices` is rebuilt, a private `_rebuildingChoices` flag makes the `SelectedVariantChoice` change handler ignore every value, because the `ComboBox` pushes `null` back when its selected item disappears. After the rebuild, set `SelectedVariantChoice` to the choice whose `Row` is `SelectedImage?.Variant` (the first choice when that is null). A `null` value outside a rebuild is ignored too; only a non-null choice changes `SelectedImage.Variant`.

  Verify `BUILD_CLIENT` passes.
- [ ] 15.2 Add the commands (`[RelayCommand]`, parameters `object?` where they come from a template):
  - `AddImagesAsync`: `picker.PickImagesAsync(MaxBytes, token())`, then `ProductImageRules.FilterPicked`, append `FromPicked` items, select the first added, set `PickMessage` to the joined messages (or empty). An `InvalidOperationException` from the picker sets `PickMessage` to its message.
  - `SelectImage(object?)`.
  - `MakeCover`: move the selected shared item to index 0.
  - `MoveLeft` / `MoveRight`: `Images.Move`.
  - `RemoveSelected`: saved items add their `Id` to `RemovedImageIds`; then select the next item, else the previous.

  `CanExecute` follows the matching `Can*`/`Show*` properties. Verify `BUILD_CLIENT` passes.
- [ ] 15.3 Add the lifecycle members:
  - `Load(IReadOnlyList<ProductImage> saved, IReadOnlyList<VariantRowItem> rows)`: maps `VariantId` to the row with that `Id` and selects the first image;
  - `Reset()`;
  - `HasChanges`: true when `RemovedImageIds` is not empty, any item `IsNew`, or the sequence of `(Id, Variant?.Id ?? row reference)` differs from the baseline taken by `Load`/`AcceptChanges`;
  - `AcceptChanges()`: takes a new baseline and clears `RemovedImageIds`;
  - `ForgetRemoved(Guid id)`.

  Verify `BUILD_CLIENT` passes.
- [ ] 15.4 Tests in `tests/Frontend.UnitTests/ViewModels/Catalog/ProductImageGalleryTests.cs` with a hand-written `ScriptedImageFilePicker` (in `CatalogFakes.cs`: queue of results, or an exception to throw). One test per spec scenario of "Image area in the editor", "Add images from the computer" (including picker cancelled → empty list), "Cover image in the editor", "Reorder and remove images", "Assign an image to a variant" (including unsaved row removed and `ImageCount` "2 ảnh"), plus:
  - `HasChanges_AfterLoad_IsFalse`;
  - `HasChanges_AfterReorder_IsTrue`;
  - `HasChanges_AfterVariantChange_IsTrue`;
  - `AcceptChanges_ResetsHasChanges`;
  - `PickerThrows_ShowsMessage`;
  - `RenameRow_KeepsSelectedImageVariant` (tag the selected image "Đen", rename the row, set `SelectedVariantChoice = null` as the `ComboBox` would; the image still points to that row and the selected choice shows the new name);
  - `VariantChoiceNull_IsIgnored`.

  Verify `TEST_CLIENT` passes.
- [ ] 15.5 Commit: `feat(client): add product image gallery state`

## 16. `ProductImageSaver` (design D11, D12)

- [ ] 16.1 Create `ProductImageSaver.cs` (`public sealed class ProductImageSaver(IProductImageRepository images)`, `Task SaveAsync(Guid productId, ProductImageGallery gallery, CancellationToken cancellationToken)`):
  - return when `!gallery.HasChanges`;
  - for each id in `gallery.RemovedImageIds` (copy the list first): `DeleteAsync`, then `gallery.ForgetRemoved(id)`;
  - for each `IsNew` item in display order: `UploadAsync` with `item.Variant?.Id`, then `item.MarkSaved(result.Id, result.Url)`; throw `InvalidOperationException("Biến thể của ảnh chưa được lưu.")` if `item.Variant` is set but its `Id` is null;
  - when `gallery.Images.Count > 0`, call `SaveLayoutAsync` with every item's `Id` and `Variant?.Id` in display order;
  - finally `gallery.AcceptChanges()`.

  Exceptions propagate. Verify `BUILD_CLIENT` passes.
- [ ] 16.2 Tests in `ProductImageSaverTests.cs` with a hand-written `RecordingImageRepository` (in `CatalogFakes.cs`; records calls in order; can throw on the n-th upload):
  - `SaveAsync_NoChanges_SendsNothing`;
  - `SaveAsync_DeletesThenUploadsThenLayout` (call order);
  - `SaveAsync_VariantImage_SendsRowId`;
  - `SaveAsync_SecondUploadFails_FirstIsSavedSecondStaysNew` (and `RemovedImageIds` already empty);
  - `SaveAsync_Retry_SendsOnlyMissingUploadAndLayout`;
  - `SaveAsync_AllImagesRemoved_SkipsLayout`.

  Verify `TEST_CLIENT` passes.
- [ ] 16.3 Commit: `feat(client): add product image saver`

## 17. Detail page viewer (design D11, spec "Images on the detail page")

- [ ] 17.1 Create `ProductImageViewer.cs` (`public sealed partial class ProductImageViewer : ObservableObject`) with:
  - `Load(ProductDetail? product)`;
  - `Filters`: `public sealed record ImageFilterChoice(string Label, Guid? VariantId, bool IsAll)` in the same file; "Tất cả" first, then each variant that has at least one image, in the product's variant order;
  - `SelectedFilter`, whose change rebuilds `VisibleImages` (all images, or shared + that variant's images, in sort order) and selects the first image of that variant (else the first visible);
  - `SelectedImage`, `SelectedUrl`;
  - `HasThumbnails => VisibleImages.Count >= 2`, `HasFilter => Filters.Count > 1`;
  - `[RelayCommand] SelectImage(object?)`.

  Verify `BUILD_CLIENT` passes.
- [ ] 17.2 In `ProductDetailViewModel` add `public ProductImageViewer ImageViewer { get; } = new();` (not `Images`, which would read like `Product.Images`) and call `ImageViewer.Load(value)` in the existing `Product` change handler (next to `SpecLines`/`VariantLines`); verify `TEST_CLIENT` passes
- [ ] 17.3 Tests in `ProductImageViewerTests.cs`, one per scenario of "Images on the detail page" (filter by variant; single image → no thumbnails), plus:
  - `Load_NoImages_SelectedUrlNull`;
  - `Filters_OnlyVariantsWithImages`;
  - `ProductDetailViewModel_Load_FillsViewer` (in `ProductDetailViewModelTests`).

  Verify `TEST_CLIENT` passes.
- [ ] 17.4 Commit: `feat(client): show product images by variant on the detail view model`

## 18. Editor ViewModel integration (design D11, D12)

- [ ] 18.1 Remove every image URL member from the client:
  - `ProductEditorViewModel`: `ImageUrl`, `ImageUrlError`, `HasImageUrlError`, `OnImageUrlErrorChanged`, the `imageurl` case in `ApplyFieldErrors`, the clears in `ClearAllErrors`, the `ImageUrl` lines in load and reset, the `ImageUrl:` argument in `BuildDraft`, and the `ImageUrl` comparison in the unsaved-changes check;
  - `Views/Catalog/ProductEditorPage.cs`: delete `imagePreview`, `imageUrlBox`, `imageUrlError` and `leftImageCol`, and put an empty `new Border()` in column 0 of `topSectionGrid` instead (group 19 replaces it with the gallery), so the app keeps building;
  - `ProductDraft.ImageUrl`;
  - the `ImageUrl` rule in `ProductDraftValidator`;
  - matching assertions in `ProductDraftValidatorTests` and `CatalogFakes`.

  Verify `grep -rn "ImageUrl" frontend/TechStore.Client/ViewModels frontend/TechStore.Client/Models/Catalog/ProductDraft.cs frontend/TechStore.Client/Views/Catalog/ProductEditorPage.cs` prints nothing, and `BUILD_CLIENT` and `TEST_CLIENT` both pass.
- [ ] 18.2 Add the dependencies `IImageFilePicker picker` and `IProductImageRepository imageRepository` as the last two constructor parameters (null checks like the others). Create `Gallery = new ProductImageGallery(picker, Variants, () => ActivationToken)` and `_imageSaver = new ProductImageSaver(imageRepository)`. On activate:
  - edit mode: call `Gallery.Load(product.Images, Variants)` after the variant rows are built;
  - create mode: call `Gallery.Reset()`.

  Add `|| Gallery.HasChanges` to the unsaved-changes check. Update every `new ProductEditorViewModel(` in tests (`grep -rn "new ProductEditorViewModel(" tests`) to pass a `ScriptedImageFilePicker` and a `RecordingImageRepository`. Verify `TEST_CLIENT` passes.
- [ ] 18.3 Change `SaveAsync` as design D12:
  - add `private bool ApplySavedProduct(ProductDetail saved)`, which sets `ProductId`, `IsEditMode`, `PageTitle`, marks rows with `VariantSkuMatcher`, sets `CanChooseSelling = true` on every row, retakes `_baseline`, and returns false with `GeneralError` set when SKUs are missing;
  - after create/update, `if (!ApplySavedProduct(saved)) return;`, then `await _imageSaver.SaveAsync(saved.Id, Gallery, ActivationToken)` inside its own `try`, with `catch (ApiException ex)` and `catch (InvalidOperationException ex)` setting `GeneralError = $"Đã lưu sản phẩm nhưng chưa lưu được ảnh: {...}. Bấm Lưu để thử lại."` and returning;
  - navigate only after both succeed;
  - keep `IsSaving` true for the whole chain.

  Verify `BUILD_CLIENT` passes.
- [ ] 18.4 Tests in `tests/Frontend.UnitTests/ViewModels/Catalog/ProductEditorImageTests.cs`, one per scenario of "Image changes are saved with the product", "Unsaved variants are matched by SKU" (including SKU missing) and "Image save failure after the product was saved" (network error on the second upload, retry sends `PUT` not `POST`, and only the missing upload). Plus:
  - `Save_WhileSaving_DoesNotStartSecondChain`;
  - `Cancel_WithOnlyImageChanges_AsksToDiscard`.

  Verify `TEST_CLIENT` passes and `wc -l frontend/TechStore.Client/ViewModels/Catalog/ProductEditorViewModel.cs` is at most 760 (write the number here).
- [ ] 18.5 In `ViewModels/Catalog/CatalogApiErrors.cs` add `{ StatusCode: 503 } => "Nơi lưu ảnh đang không truy cập được. Hãy thử lại sau.",` before the default case (design D12), and add `Describe_503_ReturnsStorageMessage` to `tests/Frontend.UnitTests/ViewModels/Catalog/CatalogApiErrorsTests.cs`; verify `TEST_CLIENT` passes
- [ ] 18.6 Commit: `feat(client): save product images with the product`

## 19. Views (design D13) and project docs

- [ ] 19.1 Add `ToImageSource(ImagePreview? preview, Uri apiBase)` to `Views/Catalog/CatalogViewParts.cs` as design D13 (with the `ConditionalWeakTable` cache). The repo has never built a `BitmapImage` from bytes: try option 1 of D13 (`BitmapImage.SetSource`), and use option 2 (temp file + `file://` URI) if option 1 does not compile on `net10.0-desktop`. Write the option used into this line; verify `BUILD_CLIENT` passes (the picture itself is checked by the owner in 20.2 item 1)
- [ ] 19.2 Create `Views/Catalog/ProductImageGalleryView.cs` (`public sealed class ProductImageGalleryView : UserControl`, constructor `(ProductImageGallery gallery, Uri apiBase)`, property `Gallery`, root element `.DataContext(gallery)`). Content, top to bottom:
  - the section title "Ảnh sản phẩm" with `CountText` on the right;
  - the large preview through `CatalogViewParts.ProductImage(x => x.Binding(() => Gallery.LargePreview).Convert(...), 44)` with min height 260;
  - a "Bìa" badge (`StatusBadge` tone `Info`, visible when the selected item `IsCover`);
  - the "Dùng cho" `ComboBox` (`ItemsSource` `VariantChoices`, `DisplayMemberPath("Label")`, `SelectedItem` TwoWay `SelectedVariantChoice`, visible when `HasSelection`);
  - the buttons "Đặt làm ảnh bìa" (visible `ShowMakeCover`), `<`, `>`, "Xóa ảnh", each bound to its command;
  - the thumbnail row: `new ItemsRepeater().ItemsSource(x => x.Binding(() => Gallery.Images)).Layout(new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8 }).ItemTemplate<ProductImageItem>(BuildThumbnail)` (the same `ItemsRepeater` API `ProductListPage` uses; no `ItemsPanel`), where each thumbnail follows the design D13 rules (`Button` with `CommandParameter` binding, overlay `Border` with `AppColors.Brushes.Primary` for `IsSelected`, labels "Bìa" / "Mới" / `VariantLabel`, 72×72), followed by a "+ Thêm" button (`AddImagesCommand`, visible `CanAddMore`); both sit in a horizontal `StackPanel` inside a `ScrollViewer` with horizontal scrolling enabled and vertical scrolling disabled;
  - `PickMessage` in `WarningText` when `HasPickMessage`;
  - the caption "JPG, PNG, WebP – tối đa 5 MB mỗi ảnh, 20 ảnh mỗi sản phẩm".

  Verify `BUILD_CLIENT` passes and `grep -nE "Click \+=|ImageFailed|FromArgb|SolidColorBrush|PropertyChanged \+=" frontend/TechStore.Client/Views/Catalog/ProductImage*View.cs` prints nothing.
- [ ] 19.3 Create `Views/Catalog/ProductImageViewerView.cs` (same pattern):
  - large image bound to `SelectedUrl` through the existing `ProductImage(url, apiBase, 44)` overload;
  - "Xem ảnh của" `ComboBox` (visible `HasFilter`);
  - thumbnail row (visible `HasThumbnails`) built with the same `ItemsRepeater` + horizontal `StackLayout` as 19.2, with the selected overlay.

  Verify `BUILD_CLIENT` passes.
- [ ] 19.4 Edit `ProductEditorPage.cs`:
  - replace the empty `Border` that 18.1 left in column 0 of `topSectionGrid` with `new ProductImageGalleryView(ViewModel.Gallery, _apiBase)`;
  - in the variant row add a `TextBlock` bound to `variant.ImageCountText` (caption style, `SecondaryText`), next to the selling tick box.

  Edit `ProductDetailPage.cs`: the `imageContainer` child becomes `new ProductImageViewerView(ViewModel.ImageViewer, apiBase)`. Verify `BUILD_CLIENT` passes and `grep -n "URL ảnh" frontend/TechStore.Client/Views` prints nothing.
- [ ] 19.5 Run the client against the scratch backend from 10.1 (local storage) and check the bindings yourself with the screenshot probe, if one is available: editor in create mode shows the empty gallery `0/20`; detail page of a product with two images shows the thumbnail row. Write what you saw, or "no probe, left to 20.2", into this line. Remove any probe file afterwards (`git status --short` must not list it)
- [ ] 19.6 Update `AGENTS.md` for the client (the backend parts were done in 3.9 and 8.4):
  - "Key Modules", `frontend/TechStore.Client/` bullet and "Key Directories": `Services/` lists `Files` (`IImageFilePicker`; WinUI implementation `Views/Shell/ImageFilePicker.cs`);
  - section 9, Frontend `AddFrontendUi` bullet: `ImageFilePicker` / `IImageFilePicker` (Singleton) and `IProductImageRepository` (Transient);
  - section 6 "Conversion status of views" table: add `ProductImageGalleryView`, `ProductImageViewerView` to the "C# Markup with theme-aware tokens" row.

  Verify `grep -n "IImageFilePicker" AGENTS.md` prints at least two lines and `grep -n "ProductImageGalleryView" AGENTS.md` prints one.
- [ ] 19.7 Commit: `feat(client): add product image gallery and viewer`

## 20. Real app check with the owner and hand-off

- [ ] 20.1 Run `BUILD_ALL`, `TEST_BACKEND`, `TEST_CLIENT`; verify all pass and write the counts here (must be ≥ the 1.3 baseline plus the new tests). Run `openspec validate catalog-product-images --strict`; verify it prints "is valid" (the two INFO lines about archive order are expected)
- [ ] 20.2 Real app check **with the project owner**. You cannot click in the desktop app yourself: start the backend with the 8.1 command (local storage, port 5177) and the client (`TECHSTORE_API_BASE_URL=http://localhost:5177/ dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`), give the owner this checklist and write down only what the owner reports. Do not tick until the owner confirms every item:
  1. **Thêm sản phẩm:**
     - the image area shows `0/20`;
     - "+ Thêm" opens the system file dialog and allows selecting several files;
     - pick 3 images and one `.heic` or text file → 3 images added and each shows its own picture (not only the placeholder) in the thumbnail and the large preview, the wrong file named in the message.
  2. **Ảnh bìa và thứ tự:**
     - select the third image → large preview changes;
     - "Đặt làm ảnh bìa" moves it first with the "Bìa" label;
     - `<` / `>` move it;
     - "Xóa ảnh" removes it.
  3. **Gắn biến thể:**
     - add two variant rows "Đen" (SKU in lower case) and "Trắng";
     - tag one image "Đen" → thumbnail shows "Đen", the "Đen" row shows "1 ảnh";
     - rename the row → label follows;
     - remove an unsaved row → its image returns to shared.
  4. **Lưu:**
     - the product detail opens with the images in the same order;
     - "Xem ảnh của: Đen" shows the shared images plus the "Đen" image;
     - the list card shows the cover.
  5. **Sửa:**
     - reorder and delete one image, press Hủy → asked to discard;
     - press Lưu → detail reflects the change;
     - the deleted file is gone from `uploads/`;
     - delete one remaining image file by hand from `backend/uploads/products/...` and reopen the product → that image shows the placeholder on the detail page and in the editor, the other images still show, and Lưu still works.
  6. **Lỗi khi lưu ảnh:**
     - before this item, restart the backend with `ObjectStorage__Local__RootPath=<scratch>/ro-uploads`, after `mkdir -p <scratch>/ro-uploads && chmod 555 <scratch>/ro-uploads`, so every image upload returns 503 while product saves still work;
     - create a product with two images and press Lưu → banner "Đã lưu sản phẩm nhưng chưa lưu được ảnh: Nơi lưu ảnh đang không truy cập được…", editor title becomes "Chỉnh sửa sản phẩm", both images still marked "Mới";
     - run `chmod 755 <scratch>/ro-uploads` (no restart needed), press Lưu again → the detail page opens with both images, and the product list has only one product with that SKU;
     - restart the backend without the override before item 7.
  7. **Theme:** switch Sáng/Tối in Settings while the editor is open → thumbnails, labels and the selected border stay readable.
  8. **Delete:** delete the test product from the detail page → its files are removed from `uploads/`.
- [ ] 20.3 R2 check **with the owner's keys**:
  - the owner sets the user-secrets of design D3 (never paste keys into chat or files);
  - restart the backend, upload one image → its `url` starts with the R2 public base URL and opens in a browser;
  - delete it → the object is gone from the bucket.

  Write the outcome (no keys) into this line. If the owner has no bucket yet, ask the owner; when the owner agrees, write "R2 not checked: no bucket (owner decision, <date>)" into this line, record the same in `evidence.md`, and tick the task so it does not block archiving.
- [ ] 20.4 Write `openspec/changes/catalog-product-images/evidence.md` with:
  - commit hash, date, test counts;
  - the 10.1/10.2 status codes;
  - the owner's answers to 20.2 (item / expected / owner result) and 20.3;
  - exceptions.

  Add a short "Hand-off" section to `evidence.md` for the owner: the "Coordination notes" from design, the R2 setup steps and the archive order. Do not push and do not open a pull request (owner decision). Verify `git status --short` is clean after committing `docs(openspec): record catalog-product-images evidence`.

## Workflow follow-up

- Archive `catalog-product-management-api`, then `catalog-product-management-ui`, then this change. The `product-catalog` and `frontend-catalog-management` deltas here modify requirements that only exist in the main specs after those two archives.
- Pushing the branch or opening a pull request is the owner's decision and is not part of this change. When the owner shares the branch, give Dev 3, Dev 4 and Dev 6 the "Coordination notes", and tell teammates that images work without Cloudflare (local storage) and how to enable R2.
