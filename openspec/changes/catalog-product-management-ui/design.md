# Design

## Context

Xem `proposal.md` (Why) và các spec trong `specs/` (hành vi bắt buộc). Hiện trạng ảnh hưởng tới cách làm:

- **Hợp đồng API** lấy từ change BE `catalog-product-management-api` (`specs/product-catalog/spec.md`, `design.md` D1): `PUT /api/products/{id}` (body `UpdateProductRequest`, không có `sku` sản phẩm; mỗi biến thể có `id?`, `sku?`, `isActive?`; biến thể không gửi lên được giữ nguyên), `PATCH /api/products/{id}/status` (`{ "isActive": bool }`, trả chi tiết), `DELETE /api/products/{id}` (204; 409 khi đã có giao dịch), quyền ghi `Admin,Manager`. Ba class `shared/Requests/UpdateProductRequest.cs`, `UpdateProductVariantRequest.cs`, `UpdateProductStatusRequest.cs` và toàn bộ code BE (controller có `[Authorize(Roles = "Admin,Manager")]` trên POST/PUT/PATCH/DELETE, DELETE trả `NoContent()`, 409 qua `Problem(detail: ...)`) đã có trên nhánh `feat/catalog-product-management-api`, nơi change này được làm. `isSerialTracked` là bắt buộc khi PUT.
- **Transport**: `IApiClient` có GET/POST/PUT/PATCH, chưa có DELETE. Đường dẫn là tương đối từ gốc host và có tiền tố `api/` (ví dụ `"api/products/{id:D}"`). Repository nhận `JsonElement`, tự kiểm tra hình dạng rồi mới deserialize (mẫu `ProductRepository`, `OrderRepository`). 4 lớp cài đặt `IApiClient`: `ApiClient` và 3 stub test (`StubApiClient` trong `ProductRepositoryTests.cs`, `JsonApiClient` + `ThrowingApiClient` trong `CategoryRepositoryTests.cs`, `RecordingApiClient` trong `Repositories/Orders/RecordingApiClient.cs`).
- **Ai đang dùng code Catalog phía client**: `OrderCreateViewModel` (Dev 4) dùng `IProductRepository.SearchAsync/GetByIdAsync`, và `tests/Frontend.UnitTests/ViewModels/Orders/OrderFakes.cs` có `FakeProductRepository : IProductRepository` cùng `new ProductDetail(...)` theo vị trí tham số. Đổi interface hoặc record này sẽ làm hỏng test của Dev 4.
- **ViewModel**: `PageViewModel` cung cấp `ActivationToken`, `RunLatestRequestAsync`, `ContentState`, `RetryCommand`, `ManagesLoadState`. `ProductListViewModel` và `ProductDetailViewModel` đã có `SetRoute(...)` không I/O, đã nghe `ISessionStore.SessionChanged`. Test tạo chúng qua helper `CreateViewModel(...)` (ProductListViewModelTests.cs dòng ~308) và `Create(...)` + một lời gọi trực tiếp ở dòng ~84 (ProductDetailViewModelTests.cs).
- **View**: hai trang Catalog là XAML. Code-behind có `Click`, `ImageFailed` đặt `Visibility`, `ViewModel.PropertyChanged +=` để trả focus về thẻ sản phẩm sau khi quay lại từ chi tiết; spec `frontend-shell-navigation` bắt buộc hành vi trả focus này. Các converter trong `Converters/ProductImageSourceConverter.cs` chỉ hai trang này dùng.
- **Mẫu C# Markup**: `Views/Development/MarkupReferencePage.cs`, `Views/Orders/OrderListPage.cs` (hàng trong `ItemsControl` + `.ItemTemplate<T>(Build)`; command của trang đặt trực tiếp `.Command(ViewModel.XCommand)` kèm `.CommandParameter(x => x.Binding(() => item))`), `Views/Orders/OrderCreatePage.cs` (form: `TextBox` TwoWay, `ComboBox` `SelectedItem` TwoWay, `FieldError`, `PrimaryButton`), `Views/Orders/OrderViewParts.cs` (helper `ShowIf`, `Caption`, `HeaderText`).
- **Dialog**: `IDialogService.ConfirmAsync(new DialogRequest(title, message, primary, secondary))` trả `DialogResult.Confirmed/Cancelled/Dismissed`. WinUI chỉ mở một dialog mỗi lúc.
- **Session**: `ISessionStore.Current` là `SessionSnapshot(Generation, Status, AccessToken, RefreshToken, Username, FullName, Role)`; `Role` là tên enum (`"Admin"`, `"Manager"`, `"SalesStaff"`, `"WarehouseStaff"`).
- **Test headless** tự liên kết `Models/**`, `Repositories/**`, `ViewModels/**`, `Services/Api|Dialog|Navigation|Session/**`, `Controls/Presentation/**`. File mới ở các thư mục đó không cần sửa csproj; `Views/**` không test được.
- **Thiết kế Stitch** (project `5285040591926339401`, xem bằng Stitch MCP `get_screen` / `download_file`): `012a390949c7443891a89abe02f446d7` (Lưới thẻ sản phẩm), `f97fbb336c3c4ceb9f51d90ae91a349d` (Modal thêm sản phẩm), `8bac057210974818bd39e709af06e47d` (Modal chỉnh sửa Master-Detail), `993d14613ed041139f61684e6383bb53` (Popup xác nhận xóa), `1d963485569d4712916f9ff0ed03d769` (Showroom split-panel, tham khảo cho chi tiết). Stitch chỉ là mẫu nhìn; khi mâu thuẫn với spec/design này thì làm theo spec/design (bảng ở D13).

## Goals / Non-Goals

**Goals:**
- Hai trang Catalog chuyển sang C# Markup mà hành vi đọc không đổi (test hiện có vẫn pass, không sửa nội dung assert).
- Mọi quy tắc nhập liệu và quy tắc hiển thị nằm trong lớp C# thuần có test headless; View chỉ khai báo cây và binding.
- Không đổi `IProductRepository`, `ProductDetail`, `ProductSummary`, `ProductQuery`, nên Orders và test của Dev 4 không phải sửa.
- Từng bước nhỏ, có lệnh kiểm tra rõ ràng, để model nhỏ làm theo được (`tasks.md`).

**Non-Goals:**
- Không sửa `shared/`. Backend chỉ đổi một điều kiện tìm kiếm theo D15.
- Không làm optimistic concurrency (ghi sau thắng, giống BE).
- Không chặn rời trang qua thanh điều hướng Shell khi đang soạn (Shell chưa có cơ chế guard).
- Không chuyển các trang XAML khác; chỉ dọn `TechStoreMetricTextStyle` (D12).

## Decisions

### D1. `IProductCommandRepository` riêng, `ProductRepository` cài đặt cả hai interface
`Repositories/Catalog/IProductCommandRepository.cs`:
```csharp
public interface IProductCommandRepository
{
    /// <summary>POST api/products. 400/409 reach the caller as ApiException.</summary>
    Task<ProductDetail> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    /// <summary>PUT api/products/{id}. 400/404/409 reach the caller as ApiException.</summary>
    Task<ProductDetail> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    /// <summary>PATCH api/products/{id}/status.</summary>
    Task<ProductDetail> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    /// <summary>DELETE api/products/{id}; 204 is success.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```
`ProductRepository` thêm `, IProductCommandRepository` vào khai báo lớp. Phần thân `GetByIdAsync` sau khi lấy được `json` được tách thành `private static ProductDetail ParseDetail(JsonElement json, Guid? expectedId)` (gồm `ValidateDetailShape`, deserialize, so id khi `expectedId` có giá trị, `ValidateDetail`, `MapDetail`) để bốn phương thức ghi dùng lại. Lỗi HTTP (kể cả 404) **không** bị nuốt ở các phương thức ghi; ViewModel cần `StatusCode`, `Detail`, `FieldErrors`.

DI: `services.AddTransient<IProductCommandRepository, ProductRepository>();` (thêm một dòng, giữ dòng `IProductRepository`).

*Phương án khác:* thêm phương thức vào `IProductRepository` → làm hỏng `OrderFakes.FakeProductRepository` của Dev 4. Tách `ProductJson` dùng chung như `OrderJson` → di chuyển nhiều code đã test, không cần thiết.

### D2. `DeleteAsync` trong transport
`IApiClient`: `Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);`. `ApiClient`: tách phần thân của `PostAsync<TRequest>(...)` (gửi, kiểm tra status, timeout, lỗi mạng, không đọc body) thành `private async Task SendWithoutResponseBodyAsync(HttpMethod method, string relativePath, HttpContent? content, CancellationToken cancellationToken)`; `PostAsync<TRequest>` gọi nó với `JsonContent`, `DeleteAsync` gọi nó với `HttpMethod.Delete` và `content: null`. Hành vi `PostAsync<TRequest>` không đổi (test transport hiện có chứng minh). Bốn stub test thêm `DeleteAsync`: ba stub cũ ném `NotSupportedException`; `RecordingApiClient` ghi `("DELETE", path, null)` và hoàn tất (để test repository).

### D3. Route `ProductCreate` và `ProductEdit`
`AppRouteKind` thêm `ProductCreate`, `ProductEdit`. Trong constructor `AppRoute`:
- nhóm Catalog = `ProductList`, `ProductDetail`, `ProductCreate`, `ProductEdit` → bắt buộc `ShellDestination.Products`;
- `ProductDetail` và `ProductEdit` bắt buộc `productId` khác rỗng; các kind khác không được mang `productId`;
- các kiểm tra khác giữ nguyên.

Factory mới: `AppRoute.ProductCreate(ProductQuery? returnQuery = null)`, `AppRoute.ProductEdit(Guid productId, ProductQuery? returnQuery = null)` (cả hai không mang `focusProductId`). `ShellRoutePageFactory`: `ResolvePage` thêm hai nhánh trả `ProductEditorPage`; sau khi resolve gọi `editorPage.SetRoute(route.Kind == AppRouteKind.ProductEdit ? route.ProductId : null, route.ReturnQuery)`; `VerifyAllRoutesResolve` thêm `using var productCreate = Create(AppRoute.ProductCreate());` và `using var productEdit = Create(AppRoute.ProductEdit(Guid.NewGuid()));` (`SetRoute` không I/O nên an toàn).

Luồng điều hướng:
```
List --[Thêm]--> ProductCreate(applied)  --Lưu ok--> ProductDetail(newId, applied, focus=newId)
                                         --Hủy-----> Products(applied)
Detail --[Sửa]--> ProductEdit(id, ret)   --Lưu ok / Hủy--> ProductDetail(id, ret, focus=id)
Detail --[Xóa] 204--> Products(ret)
```
*Phương án khác:* dialog. Đã loại: form lồng hai cấp danh sách không vừa `ContentDialog`, và WinUI không mở được hộp xác nhận chồng lên dialog (cùng lý do Dev 4 loại "lập đơn trong dialog").

### D4. Dữ liệu soạn thảo và lớp thuần (testable)
- `Models/Catalog/SpecValueKind.cs`: `public enum SpecValueKind { Text, Number, Boolean }`.
- `Models/Catalog/ProductDraft.cs`: record thô do ViewModel dựng từ ô nhập (toàn chuỗi, chưa parse):
  ```csharp
  public sealed record SpecDraft(string Key, SpecValueKind Kind, string Value);
  public sealed record VariantDraft(Guid? Id, string VariantName, string Sku, string Price, string Barcode, bool IsActive, IReadOnlyList<SpecDraft> Specs);
  public sealed record ProductDraft(Guid? CategoryId, string Name, string Sku, string Brand, string BasePrice, string CostPrice,
      string Barcode, string Description, string ImageUrl, bool IsSerialTracked, IReadOnlyList<SpecDraft> Specs, IReadOnlyList<VariantDraft> Variants);
  ```
- **Vị trí theo cấu trúc dự án**: `Controls/Presentation/` chỉ chứa quy tắc hiển thị (AGENTS.md mục 6, giống `OrderPresentation`); phần đọc nhập liệu thuộc tầng ViewModel (giống `OrderCreateViewModel` tự parse số lượng/giảm giá). Vì vậy parser nằm ở `ViewModels/Catalog/`, vẫn được test headless vì `ViewModels/**` đã link vào project test.
- `ViewModels/Catalog/ProductInputParser.cs` (thuần, `public static class ProductInputParser`): `TryParseMoney(string text, out decimal value)`, `ParseSpecs(IReadOnlyList<SpecDraft>)` trả `SpecParseResult(Dictionary<string, JsonElement> Values, IReadOnlyList<string?> RowErrors)`, và `SpecsFromJson(...)`. Quy tắc tiền (`TryParseMoney`): cắt khoảng trắng; chấp nhận `^\d{1,3}(\.\d{3})+(,\d{1,2})?$` (kiểu `35.990.000`), `^\d+(,\d{1,2})?$` hoặc `^\d+(\.\d{1,2})?$` (kiểu `35990000` hoặc `12.5`); trường hợp `^\d+\.\d{3}$` (ví dụ `6.100`) được hiểu là có nhóm nghìn (6100); kết quả phải ≥ 0 và < 10^16. Quy tắc thông số:
  - key cắt khoảng trắng, phải khớp `^[a-z][a-z0-9_]*$`, không trùng trong cùng nhóm (so chính xác, key đã là chữ thường);
  - giá trị cắt khoảng trắng, không rỗng;
  - `Number`: chấp nhận `^-?(0|[1-9]\d*)([.,]\d+)?$` (không nhận số 0 ở đầu như `08`, vì JSON không cho phép); dấu `,` đổi thành `.`; phải `decimal.TryParse(..., InvariantCulture)`; tạo `JsonElement` bằng `JsonDocument.Parse(normalized).RootElement.Clone()` để giữ đúng chữ số (không qua `double`);
  - `Boolean`: chỉ `true`/`false` (không phân biệt hoa thường);
  - `Text`: `JsonSerializer.SerializeToElement(value)`.
  - `SpecsFromJson(IReadOnlyDictionary<string, JsonElement>)` làm chiều ngược lại khi nạp sản phẩm (Number → `GetRawText()`, True/False → `"true"/"false"`, String → `GetString()`), giữ thứ tự key của server.
- `Controls/Presentation/CatalogPresentation.cs` (thuần, chỉ quy tắc hiển thị, thay các converter):
  - `FormatMoney(decimal)` → `"35.990.000 ₫"` (`N0`, `vi-VN`), `PriceRange(decimal from, decimal to)` (bằng nhau → một giá, khác → `"a – b ₫"`), `ActiveLabel(bool)` → `"Đang hoạt động"`/`"Ngừng hoạt động"`, `ActiveTone(bool)` → `StatusTone.Success`/`StatusTone.Neutral`, `VariantCountLabel(int)` → `"Không có biến thể"`/`"{n} biến thể"`, `OptionalText(string?)` → `"—"` khi rỗng, `SerialTrackedLabel(bool)` → `"Có"`/`"Không"`;
  - `ResolveImageUri(string? url, Uri apiBase)` → `Uri?` (cùng quy tắc `ProductImageSourceConverter.Convert` hiện tại: rỗng/sai → null, chỉ http/https, đường dẫn tương đối ghép với `apiBase`);
  - `MarginText(decimal basePrice, decimal costPrice)` (nhận số đã parse; ViewModel gọi `ProductInputParser.TryParseMoney` trước) → `MarginInfo(string Text, bool IsLoss)?`: `null` khi `basePrice <= 0`; lãi = base − cost; `Text` = `"Lãi gộp dự kiến {FormatMoney(lãi)} ({pct}%)"` với `pct = Math.Round(lãi / base * 100, 1, MidpointRounding.AwayFromZero)` định dạng `vi-VN` một chữ số thập phân; lỗ → `"Lỗ gộp dự kiến {FormatMoney(-lãi)}"`, `IsLoss = true`.
- `ViewModels/Catalog/ProductDraftValidator.cs` (thuần, dùng `ProductInputParser`): `Validate(ProductDraft draft, bool isCreate)` trả `DraftValidationResult(IReadOnlyDictionary<string, string> FieldErrors, IReadOnlyList<string?> ProductSpecRowErrors, IReadOnlyList<IReadOnlyList<string?>> VariantSpecRowErrors, CreateProductRequest? Create, UpdateProductRequest? Update)`. SKU của biến thể đã lưu không được gửi lên (BE giữ nguyên), nên validator không cần danh sách SKU đã lưu. Khóa lỗi dùng **đúng khóa camelCase của API** (`name`, `sku`, `brand`, `categoryId`, `basePrice`, `costPrice`, `barcode`, `description`, `imageUrl`, `specs`, `variants[i].variantName`, `variants[i].sku`, `variants[i].price`, `variants[i].barcode`, `variants[i].specs`) để lỗi client và lỗi server rơi vào cùng một chỗ. Giới hạn theo `CreateProductValidator` của BE: tên ≤ 200, SKU ≤ 64, thương hiệu ≤ 100, barcode ≤ 64, mô tả ≤ 2000, URL ảnh ≤ 2048, giá ≥ 0. Khi có ít nhất một biến thể, `barcode` sản phẩm gửi `null`. Request chỉ được dựng khi không còn lỗi. Thông điệp lỗi tiếng Việt, ví dụ "Tên sản phẩm là bắt buộc.", "Giá bán không hợp lệ.", "SKU trùng với một biến thể khác.".
- `ViewModels/Catalog/CatalogPermissions.cs`: `CanManage(SessionSnapshot s) => s.Status == SessionStatus.Active && (s.Role == "Admin" || s.Role == "Manager")` (so sánh `StringComparison.Ordinal`).
- `ViewModels/Catalog/CatalogApiErrors.cs`: như `OrderApiErrors`, có `Describe(ApiException, string subject)` với: Timeout → "Yêu cầu quá thời gian chờ. Hãy tải lại sản phẩm để kiểm tra trước khi thử lại."; Network → "Không thể kết nối API. Hãy kiểm tra kết nối rồi thử lại."; InvalidResponse → "API trả dữ liệu không đúng định dạng."; 404 → "{subject} không còn tồn tại."; 409 → `Detail` của server hoặc "{subject} không thể thực hiện lúc này."; 400 → lỗi đầu tiên hoặc "Dữ liệu chưa hợp lệ."; 401 → "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục."; 403 → "Bạn không có quyền thực hiện thao tác này."; còn lại → "Không thể hoàn tất thao tác. Hãy thử lại.".

### D5. `ProductEditorViewModel`
`ViewModels/Catalog/ProductEditorViewModel.cs : PageViewModel`, constructor `(IProductRepository products, IProductCommandRepository commands, ICategoryRepository categories, INavigationService navigation, IDialogService dialogs, ISessionStore sessionStore)`.
- `SetRoute(Guid? productId, ProductQuery? returnQuery)`: không I/O; giống `ProductDetailViewModel`, ném `InvalidOperationException` khi `ContentState != PageContentState.Idle`; `IsEditMode = productId.HasValue`.
- `LoadContentAsync`: đầu tiên xóa sạch `Specs`, `Variants` và mọi lỗi (Retry gọi lại hàm này, không xóa thì dòng bị nhân đôi); tải cây danh mục; nếu sửa thì `GetByIdAsync(id)`; `null` → `IsNotFound = true`, trả `false` (trang hiện Empty với nút quay lại); có sản phẩm → đổ vào các ô và lưu `_baseline = BuildDraft()`. Lỗi → `GetSafeErrorMessage` và Retry của `PageViewModel`. Tạo mới → `_baseline` là draft trống ban đầu (mặc định `IsSerialTracked = true`).
- Ô nhập là `[ObservableProperty] string` (`Name`, `Sku`, `Brand`, `BasePriceText`, `CostPriceText`, `Barcode`, `Description`, `ImageUrl`), `Guid? SelectedCategoryId`, `bool IsSerialTracked`, `IReadOnlyList<CategoryOption> Categories`, `ObservableCollection<SpecRowItem> Specs`, `ObservableCollection<VariantRowItem> Variants`.
- `ViewModels/Catalog/ProductEditorItems.cs`: `SpecRowItem : ObservableObject` (`Key`, `KindIndex` 0/1/2 cho ComboBox, `Value`, `Error`, `HasError`), `VariantRowItem : ObservableObject` (`Id` chỉ đọc, `IsSaved => Id.HasValue`, `VariantName`, `Sku`, `IsSkuEditable => !IsSaved`, `PriceText`, `Barcode`, `IsActive`, `ObservableCollection<SpecRowItem> Specs`, các thuộc tính lỗi `NameError`, `SkuError`, `PriceError`, `BarcodeError`, `SpecsError`). Không có tham chiếu UI.
- Command: `AddSpecCommand`, `RemoveSpecCommand(SpecRowItem)`, `AddVariantCommand`, `RemoveVariantCommand(VariantRowItem)` (chỉ khi `!IsSaved`), `AddVariantSpecCommand(VariantRowItem)`, `RemoveVariantSpecCommand(SpecRowItem)` (tìm dòng biến thể chứa nó), `SaveCommand` (`CanExecute = CanSave`), `CancelCommand`.
- Thuộc tính suy ra: `HasVariants => Variants.Count > 0`, `IsBarcodeEnabled => !HasVariants`; khi `Variants` đổi số lượng từ 0 lên ≥ 1 thì `Barcode = string.Empty` (D-spec "Product barcode follows variants"). `MarginText`, `HasMargin`, `IsMarginLoss` tính lại khi `BasePriceText`/`CostPriceText` đổi (qua `ProductInputParser.TryParseMoney` rồi `CatalogPresentation.MarginText`). Xem trước ảnh không nằm trong ViewModel: trang bind thẳng `ImageUrl` và tự chuyển bằng `CatalogPresentation.ResolveImageUri(url, apiBase)` như danh sách và chi tiết (D7).
- Lưu (theo mẫu `OrderCreateViewModel.SubmitAsync`): `if (!CanSave) return;` → xóa lỗi cũ → `BuildDraft()` → `ProductDraftValidator.Validate` → có lỗi thì gán vào ô/dòng và dừng → `IsSaving = true` → gọi `CreateAsync` hoặc `UpdateAsync` với `ActivationToken` → thành công: điều hướng theo D3 → `ApiException`: 400 có `FieldErrors` thì gán theo khóa (khóa `variants[i].x` → dòng `Variants[i]`; khóa không khớp vào `GeneralError`), còn lại `GeneralError = CatalogApiErrors.Describe(...)` → `finally IsSaving = false`. Không thử lại tự động.
- `CanSave => !IsSaving && ContentState == PageContentState.Content && CanManage`.
- Hủy: nếu `HasUnsavedChanges` (so sánh `BuildDraft()` với `_baseline` bằng hàm so sánh giá trị từng trường, kể cả danh sách) thì `ConfirmAsync(new DialogRequest("Bỏ thay đổi?", "Các thay đổi chưa lưu sẽ bị mất.", "Bỏ thay đổi", "Tiếp tục chỉnh sửa"))`; chỉ điều hướng khi `Confirmed`.
- Session đổi (`SessionChanged`): cập nhật `CanManage`; nếu không còn quyền thì `GeneralError = "Bạn không có quyền thực hiện thao tác này."` và Lưu bị vô hiệu.

### D6. Hành động trên chi tiết và nút Thêm trên danh sách
- `ProductDetailViewModel` constructor thêm `IProductCommandRepository commands, IDialogService dialogs` (thêm vào cuối danh sách tham số). Thuộc tính mới: `CanManage`, `IsBusy`, `ActionError`, `HasActionError`, `ToggleStatusText` ("Ngừng kinh doanh"/"Mở bán lại" theo `Product.IsActive`). Command: `EditCommand` → `Navigate(AppRoute.ProductEdit(ProductId, _returnCriteria))`; `ToggleStatusCommand` → xác nhận → `SetStatusAsync(id, !Product.IsActive)` → `Product = result` (kiểm id); `DeleteCommand` → xác nhận → `DeleteAsync(id)` → `Navigate(AppRoute.Products(_returnCriteria))`. Lỗi: `ActionError = CatalogApiErrors.Describe(e, "Sản phẩm")`; khi Xóa nhận 409 thì nối thêm " Bạn có thể dùng Ngừng kinh doanh để ẩn sản phẩm khỏi bán hàng.". Tất cả `CanExecute = CanRunAction` (`CanManage && !IsBusy && Product is not null`).
- Nội dung hộp xác nhận:
  - Ngừng: `("Ngừng kinh doanh?", "Sản phẩm {Name} sẽ không còn được chọn khi lập đơn. Bạn có thể mở bán lại sau.", "Ngừng kinh doanh", "Hủy")`;
  - Mở bán: `("Mở bán lại?", "Sản phẩm {Name} sẽ được chọn lại khi lập đơn.", "Mở bán lại", "Hủy")`;
  - Xóa: `("Xóa sản phẩm?", "Chỉ xóa được sản phẩm chưa phát sinh tồn kho, nhập hàng hay đơn hàng. Thao tác này không hoàn tác được. Nếu sản phẩm đã có giao dịch, hãy dùng Ngừng kinh doanh.", "Xóa vĩnh viễn", "Hủy")`.
- `ProductListViewModel`: thêm `CanManage` (tính trong constructor và trong `OnSessionChanged`), `CreateProductCommand` → `Navigate(AppRoute.ProductCreate(AppliedCriteria))`, và phương thức công khai `ClearFocusRequest()` đặt `FocusProductId = null` (trang danh sách gọi sau khi đã trả focus, D8). Constructor **không đổi**.

### D7. Trang C# Markup
- Theo `MarkupReferencePage`/`OrderListPage`: lớp `sealed`, kế thừa `Page`, cài `IRoutePage`, constructor nhận ViewModel (và `ApiClientOptions` cho ảnh), `DataContext = viewModel`, màu đặt trên phần tử bằng `AppColors.Brushes.*`, text style có màu riêng dùng `AppResources.GetStyle(...)`. `Views/Catalog/CatalogViewParts.cs` (giống `OrderViewParts`): `ShowIf`, `Caption`, `SectionTitle`, `ProductImage` (xem tasks 10.1).
- **Danh sách**: bố cục gọn theo D15 (tiêu đề trang + mô tả + Làm mới + **Thêm sản phẩm** hiện khi `CanManage`; một hàng lọc; ô thông số thu gọn; thanh kết quả có sắp xếp; lưới thẻ; phân trang có kích thước trang). Bản đầu của change giữ nguyên bố cục XAML cũ; D15 thay thế quyết định đó. Lưới thẻ: dùng `new ItemsRepeater().ItemsSource(x => x.Binding(() => ViewModel.Products)).Layout(new UniformGridLayout { MinItemWidth = 258, MinItemHeight = 340, MinColumnSpacing = 14, MinRowSpacing = 14, ItemsStretch = UniformGridLayoutItemsStretch.Fill }).ItemTemplate<ProductSummary>(BuildCard)`. **Nếu** `.ItemTemplate<ProductSummary>` không biên dịch cho `ItemsRepeater`, dùng `new GridView().SelectionMode(ListViewSelectionMode.None).IsItemClickEnabled(false).ItemsSource(...).ItemTemplate<ProductSummary>(BuildCard)` (GridView là `ItemsControl`, xếp lưới tự động) và ghi lại lựa chọn trong task. Thẻ là `Button` với `.Command(ViewModel.OpenProductCommand).CommandParameter(x => x.Binding(() => product))`.
- **Ảnh** (danh sách, chi tiết, màn soạn): `Grid` chứa icon giữ chỗ **ở dưới** và `Image` ở trên; `Image.Source` bind `ImageUrl` qua `.Convert((string? url) => CatalogPresentation.ResolveImageUri(url, _apiBase) is { } uri ? new BitmapImage(uri) : null)`. Ảnh lỗi không vẽ gì nên icon tự lộ ra: không cần `ImageFailed`.
- **Chi tiết**: bố cục XAML hiện tại + thanh hành động (Sửa, Ngừng/Mở bán, Xóa; hiện khi `CanManage`) + banner `ActionError`. Trạng thái dùng `StatusBadge` với `Label`/`Tone` từ `CatalogPresentation`.
- **Màn soạn** (wireframe, lấy từ Stitch `f97fbb33…` + `8bac0572…`):
```
[< Quay lại]  Thêm sản phẩm mới | Chỉnh sửa: {Name}
[banner GeneralError]
+-- Ảnh --------------+  +-- 1. Thông tin cơ bản ---------------------------+
| [xem trước]         |  | Tên *  [______]                                  |
| URL ảnh [_________] |  | Danh mục * [▼]       Thương hiệu * [______]       |
+---------------------+  | SKU * [____] (chỉ đọc khi sửa)  Barcode [____]   |
                         | Mô tả [______________]                           |
                         +--------------------------------------------------+
+-- 2. Giá bán & giá vốn ------------------------------ [Lãi gộp dự kiến …] ----+
| Giá bán * [________ ₫]            Giá vốn dự kiến * [________ ₫]         |
+-------------------------------------------------------------------------+
+-- 3. Thông số kỹ thuật ---------------------------- [+ Thêm thông số] ---+
| [key______] [Chuỗi|Số|Đúng/Sai ▼] [giá trị________] [Bỏ]  lỗi dòng      |
+-------------------------------------------------------------------------+
+-- 4. Biến thể ------------------------------------- [+ Thêm biến thể] ---+
| Tên [____] SKU [____](ro) Giá [____] Barcode [____] [x] Đang bán [Bỏ*]  |
|   Thông số riêng: (giống khối 3, có [+ Thêm thông số])                  |
+-------------------------------------------------------------------------+
[x] Quản lý từng máy bằng Serial/IMEI
                                              [Hủy bỏ] [Lưu thay đổi]
(* Bỏ chỉ hiện ở dòng mới chưa lưu)
```
  `ComboBox` kiểu thông số dùng `ItemsSource(new[] { "Chuỗi", "Số", "Đúng/Sai" })` + `SelectedIndex` TwoWay với `KindIndex`. Danh mục dùng `ItemsSource` Categories, `DisplayMemberPath("DisplayPath")`, `SelectedValuePath("Id")`, `SelectedValue` TwoWay với `SelectedCategoryId`. Màn soạn không có nút Xóa (Xóa ở trang chi tiết, D6).

### D8. Ngoại lệ D2 mới: trả focus trong trang Catalog
Focus không biểu diễn được bằng binding. Các trang Catalog được phép:
- `ProductListPage`: đăng ký `ViewModel.PropertyChanged` **chỉ** để khi `Snapshot` đổi và `FocusProductId` có giá trị thì cuộn tới thẻ đó và gọi `Focus(FocusState.Programmatic)` (không có thẻ → focus tiêu đề), rồi gọi `ViewModel.ClearFocusRequest()` (phương thức mới đặt `FocusProductId = null`); hủy đăng ký ở `Unloaded`. Với `ItemsRepeater` dùng `ElementPrepared`/`TryGetElement` như code-behind cũ; với `GridView` dùng `ScrollIntoView(item)` + `ContainerFromItem(item)`.
- `ProductDetailPage` và `ProductEditorPage`: `Loaded` đặt focus vào tiêu đề trang.
Không handler nào ghi dữ liệu nghiệp vụ vào ViewModel hay đặt `Visibility`/`Text`. Ngoại lệ này được thêm vào danh sách "Allowed exceptions" trong AGENTS.md mục 6 (tasks nhóm 13); file design đã lưu trữ của `csharp-markup-foundation` không bị sửa.

### D9. Chiều chảy lỗi theo dòng biến thể
Request gửi biến thể theo đúng thứ tự `Variants` đang hiển thị, nên khóa server `variants[i].*` ứng với `Variants[i]`. Regex `^variants\[(\d+)\]\.(\w+)$`; phần `\w+` là `variantName|sku|price|barcode|specs|id` → gán vào lỗi tương ứng của dòng (`id` → `GeneralError`). Khóa `specs` → `SpecsError` của khối thông số sản phẩm.

### D10. Phân quyền chỉ ẩn nút, server quyết định
`CanManage` chỉ dùng để ẩn/vô hiệu nút. Không đổi luồng đọc. Mọi phản hồi 401/403 của thao tác ghi vẫn đi qua `CatalogApiErrors`.

### D11. Test
- Transport: `DeleteAsync` (phương thức DELETE, không body, 204 thành công, 409 ProblemDetails → `ApiException`, URL ngoài origin bị từ chối, một request duy nhất).
- Repository: dùng `RecordingApiClient` (namespace `TechStore.Client.UnitTests.Repositories.Orders`, `internal`, dùng được trong cùng assembly) cho bốn phương thức ghi: đúng phương thức/đường dẫn/body, parse chi tiết trả về, id không khớp → `InvalidResponse`, lỗi HTTP đi nguyên.
- Lớp thuần, mỗi quy tắc một test: `CatalogPresentation` → `tests/Frontend.UnitTests/Controls/CatalogPresentationTests.cs` (cạnh `OrderPresentationTests.cs`); `ProductInputParser`, `ProductDraftValidator`, `CatalogPermissions`, `CatalogApiErrors` → `tests/Frontend.UnitTests/ViewModels/Catalog/`.
- DI của UI (`UiServiceCollectionExtensions`) không có test headless (file này tham chiếu `Views`). Bản Debug gọi `ShellRoutePageFactory.VerifyAllRoutesResolve()` lúc khởi động (`App.xaml.cs`), nên thiếu đăng ký sẽ làm app dừng ngay khi mở; bước chạy thử app là bước kiểm tra DI.
- ViewModel: `ProductEditorViewModel` (tạo, sửa, lỗi client, lỗi 400 theo dòng, 409, bấm Lưu hai lần, Hủy có/không thay đổi, 404 khi sửa, barcode khi thêm biến thể, quyền), `ProductDetailViewModel` (ngừng/mở bán, xóa 204/409, từ chối xác nhận, quyền), `ProductListViewModel` (`CanManage`, `CreateProductCommand`). Fake mới đặt ở `tests/Frontend.UnitTests/ViewModels/Catalog/CatalogFakes.cs` với tên không trùng các lớp lồng sẵn có: `RecordingProductCommands`, `StubProductReader`, `StubCategoryReader`, `ScriptedDialogService`.
- Điều hướng: `NavigationStateTests` thêm test route mới.
- Tích hợp opt-in: `tests/Frontend.IntegrationTests/CatalogManagementApiIntegrationTests.cs` đăng nhập `admin` qua `POST api/auth/login`, rồi tạo → sửa (thêm biến thể) → ngừng/mở bán → xóa (204) → đọc lại (404) qua `ProductRepository` thật; chỉ chạy khi có `TECHSTORE_API_BASE_URL` (backend đang chạy).

### D12. Dọn `TechStoreMetricTextStyle` (AGENTS.md quy tắc 4)
Sau khi xóa hai trang XAML Catalog, `git grep -n "TechStoreMetricTextStyle" -- '*.xaml'` không còn kết quả. Chuyển style này khỏi chuỗi XAML trong `AppResources.AddThemedStyles` sang `AddControlStyles` thành `Style<TextBlock>` không màu (FontFamily SemiBold, FontSize 24, FontWeight SemiBold, LineHeight 32, TextWrapping Wrap), và thêm `.Foreground(AppColors.Brushes.Text)` vào hai nơi đang dùng (`Controls/StatCard.cs` ô giá trị; `Views/Orders/OrderCreatePage.cs` ô "Tạm tính" — đổi `.Style(AppTypography.Styles.MetricText)` thành `.Style(AppResources.GetStyle(AppTypography.MetricText))`). Các style khác (`Panel`, `Section`, `Body`, `Heading`, `Caption`, `FieldError`…) vẫn có trang XAML dùng nên giữ nguyên. Khóa `TechStoreMetricTextStyle` không đổi nên `DesignTokenKeysTests` vẫn đúng.

### D13. Khác biệt có chủ đích so với Stitch
| Stitch vẽ | Làm thế nào | Lý do |
|---|---|---|
| Modal thêm/sửa | Trang `ProductEditorPage` | D3 |
| Lưới: "Còn 12", "Hết hàng", lọc trạng thái tồn | Bỏ; nhãn chỉ Đang hoạt động/Ngừng hoạt động | API không trả tồn kho; spec cấm suy diễn |
| Lưới: chấm màu biến thể | Bỏ; hiện số biến thể | Spec cấm suy màu |
| Chip danh mục kèm số lượng | Giữ ComboBox danh mục không số | Spec cấm bịa số lượng |
| Nút "…" trên thẻ, đổi lưới/danh sách, Xuất Excel | Bỏ | Ngoài phạm vi |
| Cột "Tồn kho ban đầu" | Bỏ | Tạo sản phẩm không tạo tồn kho (UC-INV-02 của Dev 3) |
| Cột Màu sắc + Dung lượng | Một ô Tên biến thể + thông số riêng | Biến thể chỉ có `variantName` + `specs` |
| Xóa từng biến thể | Công tắc Đang bán; Bỏ chỉ cho dòng chưa lưu | BE không xóa biến thể |
| Giá vốn từng biến thể | Bỏ | Biến thể không có `costPrice` |
| Ô thông số dạng nhãn tự do | Dòng key/kiểu/giá trị | Hợp đồng specs có kiểu |
| "Đổi ảnh" upload | Ô URL + xem trước | BE chỉ nhận URL |
| Badge SKU "Hợp lệ" | Chỉ kiểm định dạng; trùng thì báo khi lưu (409) | Không có API kiểm tra trùng |
| Biên lợi nhuận 15,3% (lãi/giá vốn) | 13,3% (lãi/giá bán) | Thống nhất một cách tính, ghi trong spec |
| Popup xóa "dữ liệu tồn kho sẽ bị hủy" | "Chỉ xóa được sản phẩm chưa phát sinh giao dịch…" | Server từ chối xóa sản phẩm đã có giao dịch |
| Nút Xóa trong modal sửa | Nút Xóa ở trang chi tiết | Tránh hai lối xóa |

### D14. Một nhánh cho cả BE và FE
- Change BE đã hoàn tất (`4196a83`, mọi task đã tick, chưa push). Change này làm thẳng trên `feat/catalog-product-management-api` trong thư mục chính của repo; không có worktree hay nhánh FE riêng.
- Không sửa `shared/`, `openspec/changes/catalog-product-management-api/`: phần BE đã xong và đã được kiểm. Ngoại lệ duy nhất ở `backend/` là điều kiện tìm theo hãng của D15.
- Kết thúc (nhóm 16 của tasks): build và test toàn solution, chạy thật, ghi `evidence.md`, đổi tên nhánh thành `feat/catalog-product-management` (`git branch -m`), push và mở một PR duy nhất (BE + FE) vào `dev`.
- Phương án đã bỏ: nhánh FE trong worktree riêng rồi merge vào nhánh BE. Chỉ có ích khi BE và FE chạy song song; BE đã xong nên chỉ thêm bước gộp và rủi ro conflict.

### D15. Thu gọn bộ lọc danh sách sản phẩm
Bản đầu giữ đủ 7 ô lọc của trang XAML cũ (từ khóa, danh mục, thương hiệu, trạng thái, giá từ, giá đến, thông số nhiều dòng) cùng sắp xếp và kích thước trang. Khối lọc chiếm hơn nửa chiều cao cửa sổ và trang không có tiêu đề. Kế hoạch nhóm (`docs/MILESTONE_2_PLAN.md`, mục Dev 2) chỉ yêu cầu phân trang, tìm theo tên/SKU/hãng và lọc danh mục.
- Giữ trên giao diện: từ khóa, danh mục, trạng thái (cần để tìm sản phẩm đã ngừng kinh doanh), thông số JSONB (điểm nhấn của đồ án, thu gọn sau nút "Lọc theo thông số"), sắp xếp, kích thước trang.
- Bỏ khỏi giao diện: ô thương hiệu riêng (gộp vào từ khóa: backend thêm `ILike(p.Brand, pattern)` vào điều kiện `q`, delta `product-catalog`), giá từ và giá đến. `ProductQuery`, `IProductRepository` và repository giữ nguyên nên vẫn gửi được `brand`, `minPrice`, `maxPrice`; chỉ `ProductListViewModel` thôi tạo chúng.
- Lọc thông số bằng các dòng chọn (cập nhật sau review, change API D12): `IProductSpecRepository.GetSpecKeysAsync(categoryId)` (do `ProductRepository` cài đặt) gọi `GET api/products/spec-keys`; `ProductListViewModel` tải danh sách khi kích hoạt và mỗi khi `SelectedCategoryId` (bản nháp) đổi, request mới nhất thắng. Mỗi `SpecFilterRow` giữ `KeyOptions`, `SelectedKey`, `ValueOptions`, `SelectedFilterValue` để template chỉ bind vào chính dòng; `SetOptions` giữ lựa chọn khi danh sách đổi (ComboBox có thể xóa lựa chọn lúc đổi items). Tiêu chí gửi `key:filterValue` nguyên văn; dòng chưa có tên bị bỏ qua, có tên mà thiếu giá trị hoặc trùng tên là lỗi. Ô nhập tự do và `TryParseSpecs` đã bỏ. Command nhận tham số từ template (`RemoveSpecFilter`, và 4 command dòng của màn soạn) nhận `object?`: thử thật cho thấy lúc xóa dòng binding có thể tạm đưa DataContext của trang, `RelayCommand<T>` khi đó ném `ArgumentException`.
- Vẫn chỉ áp dụng khi bấm Tìm kiếm hoặc Enter (kế hoạch ghi "realtime", nhưng spec và test hiện có dùng submit; giữ để không gọi API mỗi phím).
- Cả trang (tiêu đề, bộ lọc, thanh kết quả, lưới thẻ) nằm trong một `ScrollViewer`; chỉ thanh phân trang cố định ở đáy, để cửa sổ thấp vẫn thấy thẻ. Trả focus (D8) cộng thêm vị trí của lưới trong vùng cuộn. Thẻ `Button` đặt `HorizontalAlignment/VerticalAlignment = Stretch` để lấp đầy ô của `UniformGridLayout`; khung ảnh 120 px, thẻ cao 300 px.
- Không dùng `VisualState` + `AdaptiveTrigger` để đổi `Grid.Row/Column` theo bề rộng: thử trên Uno/Skia chỉ một phần setter được áp, ô lọc chồng lên nhau. Bố cục hai hàng cố định đủ dùng từ 800 DIP.
```
Danh sách sản phẩm                              [Làm mới] [+ Thêm sản phẩm]
Tìm, lọc và quản lý sản phẩm, giá bán và thông số kỹ thuật.
+---------------------------------------------------------------------+
| Tìm kiếm sản phẩm                                                   |
| [Tên, SKU, hãng hoặc barcode______________] [Xóa] [Tìm kiếm]        |
| Danh mục          Trạng thái                                        |
| [Tất cả     v]    [Tất cả     v]   [Lọc theo thông số] [Đặt lại bộ lọc] |
| (mở) [ram_gb (7)  v] [16 (1)  v] [Bỏ]   + Thêm điều kiện            |
| (chỉ khi có lỗi) thông báo lỗi               [Thử tải danh mục]     |
+---------------------------------------------------------------------+
20 sản phẩm                                          Sắp xếp [Mới nhất v]
[thẻ] [thẻ] [thẻ] [thẻ]                     <- cả khối trên cuộn chung
Trang 1/1 · 20 kết quả               Mỗi trang [20 v] [Trang trước] [Trang sau]   <- cố định
```

### D16. Spec names in the client (follows API D13)
- Data: `Models/Catalog/SpecDefinition.cs` (`Key`, `Label`, `Unit`, `SpecValueKind Kind`); `IProductSpecRepository.GetSpecDefinitionsAsync`; `SpecKeyOption` carries the optional `Label`/`Unit` of `spec-keys`.
- Display rules (pure, `Controls/Presentation/SpecPresentation.cs`): name = label or the key; value = number + unit, `Có`/`Không`, text as is; `WithCount`.
- Name input (pure, `ViewModels/Catalog/SpecKeyGenerator.cs`): `SplitUnit("Tần số quét (Hz)")` → (`Tần số quét`, `Hz`); `Generate(label, unit)` → `tan_so_quet_hz` (NFD without marks, `đ`→`d`, lower case, non `[a-z0-9]` → `_`, collapsed, leading digit → `spec_` prefix, max 64; null when nothing is left).
- `SpecCatalog` (pure, `ViewModels/Catalog/`): definitions by key and by accent-insensitive label; `Suggest(text)` returns up to 6 definitions whose label or key contains the text.
- Editor rows (`SpecRowItem`): `Name` (typed, TwoWay with `UpdateSourceTrigger.PropertyChanged`), `Key`, `KindIndex`, `Value`, `Unit`, `IsKnown`, `IsKeyLocked`, `Suggestions`, `KeyCaption`. Typing a name that matches a definition applies it (key, kind locked, unit); otherwise the key is generated. A row loaded with a key that has no definition keeps its key (locked) and may get a name. `PickSpecSuggestionCommand(object)` sets the row's name. New names go to `ProductDraft.SpecLabels` → `specLabels` of the request; server errors `specLabels.<key>` land on the row with that key. The editor loads definitions on activation; if that fails the editor still works with generated keys.
- Detail: `ProductDetailViewModel` loads definitions after the product (failure → keys shown) and exposes `SpecLines` and `VariantLines` already formatted, so the page binds plain text.
- List filter: `SpecFilterRow` options become `Choice(Value, DisplayName)` built with `SpecPresentation`.
- No `AutoSuggestBox`: its events are forbidden in Views; suggestions are a list of buttons bound to the row.

### D17. Brand suggestions (follows API D14)
`IProductBrandRepository.GetBrandsAsync` (implemented by `ProductRepository`) → `BrandOption(Name, ProductCount)`. `ProductEditorViewModel` loads brands with the categories (failure only hides suggestions) and recomputes `BrandSuggestions` (up to 5, name contains the text ignoring case and accents, not shown on an exact match) and `BrandHint` (same brand, other letter case) when `Brand` changes; `PickBrandCommand(object)` sets the brand. The brand box binds with `UpdateSourceTrigger.PropertyChanged`; suggestions are buttons like the spec name suggestions. The server still normalizes the spelling (API D14), so the hint is only information.

### D18. Giá vốn dự kiến (thống nhất với nhóm)
`costPrice` của Catalog là **giá vốn dự kiến** do chủ cửa hàng nhập tay để định giá bán và ước tính lãi; nhập hàng không ghi đè nó. Giá vốn thực tế của đơn bán do Orders lấy từ Inventory (`order_items.unit_cost`), độc lập với `costPrice` (phần của Dev 3/Dev 4). Vì vậy giao diện Catalog ghi "Giá vốn dự kiến" (ô nhập, trang chi tiết, thông báo lỗi) và "Lãi gộp dự kiến" / "Lỗ gộp dự kiến" (nhãn ở màn soạn), để không ai nhầm với kết quả bán thực tế. Ví dụ: giá bán 20 triệu, giá vốn dự kiến 17 triệu → màn soạn hiện lãi gộp dự kiến 3 triệu; nếu giá vốn thực tế lúc bán là 18 triệu thì đơn ghi nhận lãi gộp 2 triệu.

## Risks / Trade-offs

- [`ItemTemplate<T>` có thể không có cho `ItemsRepeater` trong Uno Markup] → D7 có phương án `GridView` đã định sẵn; task ghi lại lựa chọn.
- [Binding C# Markup chỉ sai lúc chạy] → mỗi trang có bước chạy thật, thao tác từng ô và đổi Sáng/Tối (AGENTS.md quy tắc 5).
- [Thêm tham số constructor `ProductDetailViewModel` làm hỏng test] → chỉ sửa helper `Create` và lời gọi trực tiếp trong `ProductDetailViewModelTests.cs`; không sửa assert.
- [Thêm `DeleteAsync` vào `IApiClient` làm hỏng 4 lớp cài đặt] → cùng một nhóm task sửa cả 4, build ngay.
- [Thiếu đăng ký DI cho trang/ViewModel mới mà test không bắt được] → bước chạy app ở Debug (D11) phải tới được màn đăng nhập.
- [PR lớn vì gồm cả BE và FE] → commit theo từng nhóm task có thông điệp rõ; mô tả PR tách hai phần BE/FE.
- [Dọn `MetricText` làm mất màu chữ ở StatCard/OrderCreate khi đổi theme] → task kiểm Sáng/Tối trên Dashboard (StatCard) và màn Lập đơn.
- [Giá trị Đúng/Sai hiện "true"/"false" ở chi tiết thay vì "Có/Không"] → giữ như hành vi hiện tại (`JsonElement.ToString()`), không đổi trong change này.

## Migration Plan

Không có migration dữ liệu. Triển khai bằng một PR từ `feat/catalog-product-management` vào `dev` (D14). Rollback: revert các commit của change này; trang XAML cũ quay lại theo lịch sử git.
