# Tasks

> **Đọc trước khi làm**
>
> - Đọc `design.md` (D1–D9) và các delta spec trong `specs/`. Mỗi task ghi quyết định thiết kế (Dx) mà nó làm theo.
> - Đường dẫn trong nhóm 1–8 là đường dẫn **trước khi chuyển thư mục**. Nhóm 9 mới chuyển `Features/**`.
> - Lệnh kiểm tra chung, chạy ở thư mục gốc repo:
>   - `B` = `dotnet build TechStore.sln` (yêu cầu 0 lỗi, 0 cảnh báo)
>   - `TB` = `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj`
>   - `TF` = `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`
>   - `BD` = `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`
> - Test theo AAA, không dùng thư viện mock, tên test dạng `Method_State_Expected` (AGENTS.md "Testing & QA").
> - **Không chạy backend ở cổng 5176**, vì cổng này có thể đang được người dùng dùng. Khi cần API thật, dùng `dotnet run --project backend/TechStore.Api.csproj -- --urls http://localhost:5179`.
> - **Khi chạy client thử**, đặt `XDG_DATA_HOME` sang thư mục tạm, để không ghi đè `~/.local/share/TechStore/session.json` của người dùng.
> - Hết mỗi nhóm thì chạy `B`, `TB`, `TF` cho xanh rồi commit một lần (Conventional Commits, ví dụ `fix(catalog): ...`, `refactor(inventory): ...`).

## 0. Chuẩn bị

- [x] 0.1 Hỏi người dùng nên làm trên nhánh nào: nhánh mới tạo từ `dev` (sau khi nhánh Dev 3 đã merge) hay nhánh local `feat/dev3-catalog-inventory-management` (có commit merge `594320d`). Ghi nhánh đã chọn vào `evidence.md`. Kiểm tra: `git status --short --branch` sạch và đúng nhánh, `B`/`TB`/`TF` xanh trước khi sửa (ghi số test vào `evidence.md`, tạo file nếu chưa có trong thư mục change)

## 1. Catalog: chặn danh mục ẩn (D1)

- [ ] 1.1 `backend/Catalog/Services/ProductService.cs:266-272` (trong `CreateAsync`): đổi điều kiện `if (category == null)` thành `if (category == null || !category.IsActive)`, và đổi thông báo thành `$"Category '{request.CategoryId!.Value}' does not exist or is inactive."` (giống dòng 410). Kiểm tra: `B` xanh
- [ ] 1.2 Thêm test vào `tests/Backend.UnitTests/ProductServiceTests.cs`: `CreateAsync_HiddenCategory_ReturnsCategoryIdValidationError`. Arrange: `categoryRepo.Categories[id] = new Category { Id = id, Name = "Ẩn", IsActive = false }`. Assert: kết quả có lỗi key `categoryId`, `FakeUnitOfWork.SaveChangesCount == 0`, fake product repository không có sản phẩm mới. Kiểm tra: `TB` xanh, test mới pass
- [ ] 1.2a `backend/Catalog/Services/ProductService.cs:408` (trong `UpdateAsync`): đổi `if (category == null || !category.IsActive)` thành `if (category == null || (!category.IsActive && request.CategoryId!.Value != product.CategoryId))`, giữ nguyên thông báo (D1). Thêm 2 test vào `tests/Backend.UnitTests/ProductServiceUpdateTests.cs` (theo fixture có sẵn trong file):
  - `UpdateAsync_KeepsCurrentHiddenCategory_Succeeds`: sản phẩm thuộc danh mục `IsActive = false`, request giữ nguyên `CategoryId` và đổi `BasePrice`; assert thành công, giá mới được lưu, `SaveChangesCount == 1`;
  - `UpdateAsync_MovesToOtherHiddenCategory_ReturnsCategoryIdError`: sản phẩm ở danh mục active, request chuyển sang danh mục ẩn khác; assert lỗi `categoryId`, không lưu.
  
  Test hiện có cho trường hợp chuyển sang danh mục ẩn (nếu có) phải vẫn pass. Kiểm tra: `TB` xanh
- [ ] 1.3 `frontend/TechStore.Client/Models/Catalog/CategoryOption.cs`: đổi thành `public sealed record CategoryOption(Guid Id, Guid? ParentId, string Name, string DisplayPath, bool IsActive = true) { public string Label => IsActive ? DisplayPath : $"{DisplayPath} (đang ẩn)"; }`. Trong `Repositories/Catalog/CategoryRepository.cs`, hàm `Flatten` truyền `node.IsActive`. Kiểm tra: `B` xanh
- [ ] 1.4 Đổi `.DisplayMemberPath("DisplayPath")` thành `.DisplayMemberPath("Label")` ở `Views/Catalog/ProductEditorPage.cs:103` và `Views/Catalog/ProductListPage.cs:96`. Kiểm tra: `BD` xanh, `git grep -n 'DisplayMemberPath("DisplayPath")' frontend` không còn kết quả
- [ ] 1.5 `ViewModels/Catalog/ProductEditorViewModel.cs`:
  - giữ toàn bộ cây trong trường `_allCategories`;
  - sau khi biết `SelectedCategoryId` (nạp sản phẩm khi sửa, hoặc `null` khi tạo), gán `Categories = _allCategories.Where(o => o.IsActive || o.Id == SelectedCategoryId).ToArray()`. Chỉ lọc một lần sau khi nạp xong; khi người dùng đổi lựa chọn thì không lọc lại;
  - thêm `public string CategoryHint => Categories.FirstOrDefault(o => o.Id == SelectedCategoryId) is { IsActive: false } ? "Danh mục này đang bị ẩn. Sản phẩm vẫn lưu được, nhưng không thể chuyển sản phẩm khác vào danh mục này." : string.Empty;`;
  - phát `OnPropertyChanged(nameof(CategoryHint))` trong `partial void OnSelectedCategoryIdChanged` và sau khi gán `Categories`.
  
  Kiểm tra: `B` xanh
- [ ] 1.6 `Views/Catalog/ProductEditorPage.cs`: dưới picker danh mục, thêm một `TextBlock`:
  - nội dung `.Text(x => x.Binding(() => ViewModel.CategoryHint))`;
  - `.Visibility(x => x.Binding(() => ViewModel.CategoryHint).Convert((string s) => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible))`;
  - màu `.Foreground(AppColors.Brushes.InfoText)` (thông tin, không phải lỗi), style `.Style(AppResources.GetStyle(AppTypography.CaptionText))`.
  
  Kiểm tra: `BD` xanh
- [ ] 1.7 Thêm test trong `tests/Frontend.UnitTests/ViewModels/Catalog/ProductEditorViewModelTests.cs` (dùng fake trong `CatalogFakes.cs`; thêm `IsActive` vào fake cây danh mục nếu cần):
  - `Load_CreateMode_HidesInactiveCategories`;
  - `Load_EditProductInHiddenCategory_KeepsItWithHint` (kiểm tra `Categories` có danh mục ẩn đó, `CategoryHint` khác rỗng, các danh mục ẩn khác không có);
  - `SelectActiveCategory_ClearsHint`.
  
  Thêm `CategoryOption_Label_AddsHiddenSuffix` trong `tests/Frontend.UnitTests/Repositories/Catalog/CategoryRepositoryTests.cs`, kèm kiểm tra `Flatten` truyền `isActive`. Kiểm tra: `TF` xanh
- [ ] 1.8 Thêm `Save_ProductInHiddenCategory_SendsSameCategoryId` vào `ProductEditorViewModelTests.cs`: nạp sản phẩm thuộc danh mục ẩn, đổi giá, lưu; assert fake command repository nhận đúng một lệnh update với `CategoryId` không đổi. Đồng thời kiểm tra lỗi 400 `categoryId` từ server vẫn hiện dưới picker: tìm test hiện có trong `ProductEditorViewModelTests` hoặc `CatalogApiErrorsTests`; nếu chưa có thì thêm `Save_ServerRejectsCategory_ShowsErrorUnderCategory`. Kiểm tra: `TF` xanh
- [ ] 1.9 Commit `fix(catalog): keep hidden categories out of new products`. Kiểm tra: `git log -1 --stat` chỉ có file của nhóm 1

## 2. Backend chung và Category (D7)

- [ ] 2.1 Tạo `backend/Common/Data/CheckConstraintViolationException.cs`, copy mẫu `ForeignKeyViolationException.cs` (thuộc tính `ConstraintName`, câu mặc định "A check constraint prevented the operation."). Trong `UnitOfWork.SaveChangesAsync`, thêm nhánh `catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23514" } pgEx) { throw new CheckConstraintViolationException(pgEx.ConstraintName, pgEx.Message, ex); }` ngay sau nhánh 23503. Kiểm tra: `B` xanh
- [ ] 2.2 Thêm `TimeProvider` cho `ProductService`, `ProductImageService`, `CategoryService`:
  - tham số cuối của primary constructor `TimeProvider? timeProvider = null`;
  - trường `private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;`;
  - thay mọi `DateTime.UtcNow` bằng `_time.GetUtcNow().UtcDateTime`.
  
  Các dòng cần thay: `ProductService.cs:101, 302, 511, 611`, `ProductImageService.cs:132`, `CategoryService.cs:241`. Kiểm tra: `git grep -n "DateTime.UtcNow" backend/Catalog/Services` không còn kết quả, `B`/`TB` xanh
- [ ] 2.3 Thêm test `UpdateAsync_UsesTimeProvider_ForUpdatedAt` vào `tests/Backend.UnitTests/CategoryServiceTests.cs`: dựng service với `new FixedTimeProvider(new DateTimeOffset(2026, 10, 25, 8, 0, 0, TimeSpan.Zero))`, assert `UpdatedAt` của dto bằng thời điểm đó. Kiểm tra: `TB` xanh
- [ ] 2.4 Chuyển cây danh mục sang projection:
  - tạo `backend/Catalog/Repositories/CategoryTreeRow.cs` chứa record `CategoryTreeRow` (xem D7);
  - trong `ICategoryRepository`, đổi `GetActiveCategoriesAsync` thành `Task<IReadOnlyList<CategoryTreeRow>> GetAllAsync(CancellationToken cancellationToken = default)`;
  - viết lại thân hàm trong `CategoryRepository` bằng `Select` như D7, bỏ `Include`;
  - `CategoryService.GetTreeAsync` dựng `CategoryTreeNodeDto` từ `row.*` với `ProductCount = row.ProductCount`, `SkuCount = row.SkuCount`;
  - `tests/Backend.UnitTests/FakeCategoryRepository.cs` dựng `CategoryTreeRow` từ `Categories`, đếm qua `c.Products`/`p.Variants` như logic cũ;
  - `tests/Backend.UnitTests/Catalog/CategoryPostgresTests.cs:288`: lớp bọc implement `ICategoryRepository` đổi dòng `GetActiveCategoriesAsync` thành `public Task<IReadOnlyList<CategoryTreeRow>> GetAllAsync(CancellationToken cancellationToken = default) => inner.GetAllAsync(cancellationToken);`. Chạy `git grep -n GetActiveCategoriesAsync` để chắc không còn chỗ nào khác.
  
  Kiểm tra: `B`/`TB` xanh, các test hiện có trong `CategoryServiceTests` về `ProductCount`/`SkuCount` vẫn pass
- [ ] 2.5 Kiểm tra SQL của projection. Nếu có PostgreSQL thử, chạy `TECHSTORE_TEST_CONNECTION=<chuỗi kết nối DB thử> dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~CategoryPostgresTests"`. Nếu không có, chạy API ở cổng 5179, đăng nhập `admin`/`Admin@123`, gọi `GET /api/categories/tree` và so `productCount`/`skuCount` với dữ liệu seed. Nếu EF báo không dịch được, chuyển sang phương án hai truy vấn group-by trong D7. Kiểm tra: ghi kết quả (lệnh và số đếm) vào `evidence.md`
- [ ] 2.6 `CategoryService.UpdateAsync`: xóa các biến `originalName`, `originalSlug`, `originalParentId`, `originalDescription`, `originalIsActive`, `originalUpdatedAt`, các phép gán `original* = category.*`, hàm `RestoreCategory` và hai lời gọi `RestoreCategory(...)`. Biến `category` khai báo trong khối `try`. Kiểm tra: `B`/`TB` xanh (`CategoryServiceTests` và `CategoriesControllerTests` pass)
- [ ] 2.7 Commit `refactor(catalog): project category counts and use TimeProvider`. Kiểm tra: `B`/`TB`/`TF` xanh

## 3. Backend Inventory (D7)

- [ ] 3.1 `InventoryService`:
  - xóa `using Microsoft.EntityFrameworkCore;` và `using Npgsql;`;
  - đổi `catch (DbUpdateException ex) when (... "23514")` trong `AdjustStockAsync` thành `catch (CheckConstraintViolationException)`, giữ nguyên thân;
  - thêm `TimeProvider? timeProvider = null` như 2.2 và thay `DateTime.UtcNow` ở `:286`, `:298`.
  
  Kiểm tra: `git grep -n "Npgsql\|EntityFrameworkCore\|DateTime.UtcNow" backend/Inventory/Services/InventoryService.cs` không còn kết quả, `B` xanh
- [ ] 3.2 Thêm test `AdjustStockAsync_CheckConstraintViolation_ReturnsConflict` vào `tests/Backend.UnitTests/InventoryServiceTests.cs`: cho `FakeUnitOfWork` ném `CheckConstraintViolationException` khi `SaveChangesAsync`. Nếu fake chưa có thì thêm cơ chế giống cách nó đang ném `DuplicateKeyException`. Assert: kết quả `IsConflict`, `RollbackCount == 1`. Kiểm tra: `TB` xanh
- [ ] 3.3 Read model serial:
  - tạo `backend/Inventory/Repositories/InventoryStockView.cs`;
  - đổi `IInventoryStockRepository.SearchStocksAsync` và `GetDetailAsync` theo D7;
  - trong `InventoryStockRepository`, thay các chỗ gán `stock.RelatedSerialCount = ...` bằng tạo `new InventoryStockView(stock, count)`;
  - `InventoryService.MapToDto(InventoryStockView view)` và `MapToDetailDto(InventoryStockView view)` dùng `view.Stock` và `view.SerialCount`;
  - `InitializeStockAsync` gọi `MapToDto(new InventoryStockView(stock, 0))`;
  - xóa `RelatedSerialCount` và `using System.ComponentModel.DataAnnotations.Schema;` khỏi `backend/Inventory/Entities/InventoryStock.cs`;
  - cập nhật `tests/Backend.UnitTests/FakeInventoryStockRepository.cs` cho khớp chữ ký mới.
  
  Kiểm tra: `git grep -n RelatedSerialCount` không còn kết quả, `B`/`TB` xanh
- [ ] 3.4 Chuyển serial sang `ISerialImeiRepository`:
  - thêm `GetByStockKeyForUpdateAsync(Guid productId, Guid? variantId, CancellationToken)` và `RemoveRange(IEnumerable<SerialImei>)` vào `ISerialImeiRepository`/`SerialImeiRepository` (chuyển nguyên thân hàm từ `InventoryStockRepository`);
  - xóa `GetSerialsForUpdateAsync` và `DeleteSerials` khỏi `IInventoryStockRepository`/`InventoryStockRepository`;
  - `InventoryService` nhận thêm `ISerialImeiRepository serialRepository` (đặt sau `productRepository`) và dùng nó trong `DeleteStockAsync`;
  - cập nhật `FakeSerialImeiRepository`, `FakeInventoryStockRepository` và `InventoryServiceTests.cs:21`. Nếu `CategoryPostgresTests` có tạo `InventoryService` thì cập nhật luôn.
  
  Kiểm tra: `B`/`TB` xanh, các test `DeleteStockAsync_*` vẫn pass
- [ ] 3.5 Escape LIKE trong `InventoryStockRepository.SearchStocksAsync` và `SerialImeiRepository.SearchAsync` theo D7 (copy `EscapeLikePattern` từ `backend/Customers/Repositories/CustomerRepository.cs:115`, gọi `EF.Functions.ILike(col, pattern, "!")`). Kiểm tra: `B` xanh. Nếu có `TECHSTORE_TEST_CONNECTION`, thêm test Postgres `SearchStocks_PercentInSearch_MatchesLiterally` trong `tests/Backend.UnitTests/Inventory/` theo mẫu `CategoryPostgresTests`; nếu không có thì gọi API thật `GET /api/inventory/stocks?search=%25` ở cổng 5179 và ghi kết quả vào `evidence.md`
- [ ] 3.6 Quyền xóa tồn kho (D10): `backend/Inventory/InventoryController.cs`, trên action `DeleteStock`, đổi `[Authorize(Roles = "Admin,Manager,WarehouseStaff")]` thành `[Authorize(Roles = "Admin,WarehouseStaff")]`. **Test cũ sẽ đỏ, đây là chủ ý:** `tests/Backend.UnitTests/Inventory/InventoryControllerTests.cs:94` `Inventory_DeleteStock_Should_AllowWarehouseManagersAndRejectSalesStaff` đang khẳng định Manager xóa được (vòng `foreach (var role in new[] { "Admin", "Manager", "WarehouseStaff" })` mong 204). Sửa **test** chứ không sửa lại code: đổi danh sách 204 thành `{ "Admin", "WarehouseStaff" }`, thêm vòng `{ "Manager", "SalesStaff" }` mong `HttpStatusCode.Forbidden` (theo mẫu dòng 58-62), kiểm tra `FakeInventoryService` không nhận lời gọi xóa nào từ hai role đó (nếu fake có đếm số lần gọi), rồi đổi tên test thành `Inventory_DeleteStock_Should_AllowAdminAndWarehouseStaffOnly`. Kiểm tra: `TB` xanh, `grep -n "AllowWarehouseManagers" tests/Backend.UnitTests -r` không còn kết quả
- [ ] 3.7 Bộ lọc `lowStock` không chồng với hết hàng (D11):
  - trong `InventoryStockRepository.SearchStocksAsync`, đổi điều kiện `LowStock` thành `s.Quantity - s.ReservedQuantity > 0 && s.Quantity - s.ReservedQuantity <= s.MinStockAlert`;
  - trong `shared/DTOs/InventoryStockDto.cs`, đổi `IsLowStock` thành `AvailableQuantity > 0 && AvailableQuantity <= MinStockAlert`;
  - cập nhật `FakeInventoryStockRepository.SearchStocksAsync` dùng cùng điều kiện;
  - thêm test `GetStocksAsync_StatusFilters_AreDisjoint` trong `InventoryServiceTests.cs`: 3 dòng (avail 0, avail 3 với min 5, avail 9 với min 5) → mỗi bộ lọc trả đúng 1 dòng và tổng 3 kết quả bằng 3.
  
  Kiểm tra: `git grep -n "IsLowStock" frontend backend` để xem không chỗ nào đang dựa vào nghĩa cũ (sửa nếu có), `B`/`TB` xanh
- [ ] 3.8 Commit `refactor(inventory): translate check violations, move serial locks, tighten stock rules`. Kiểm tra: `B`/`TB`/`TF` xanh

## 4. Lọc tồn kho theo `categoryId`, đầu cuối (D3, D7, spec `inventory-management`, `inventory-stock-workspace`)

- [ ] 4.1 Đổi contract:
  - `shared/Requests/InventoryStockFilterRequest.cs`: đổi `string? CategoryName` thành `Guid? CategoryId`;
  - `shared/DTOs/InventoryStockDto.cs` và `InventoryStockDetailDto.cs`: thêm `public Guid? CategoryId { get; set; }` ngay trước `CategoryName`;
  - `backend/Inventory/Repositories/InventoryStockSearchCriteria.cs`: đổi `string? CategoryName` thành `Guid? CategoryId`;
  - `InventoryStockRepository`: đổi điều kiện lọc thành `query = query.Where(s => s.Product.CategoryId == criteria.CategoryId.Value)` khi có giá trị;
  - `InventoryService`: truyền `request.CategoryId`, và `MapToDto`/`MapToDetailDto` gán `CategoryId = view.Stock.Product?.CategoryId`.
  
  Kiểm tra: `B` báo lỗi biên dịch ở client (được xử lý ở 4.2)
- [ ] 4.2 Client:
  - `Services/Api/InventoryApiClient.cs` `BuildPath`: thay `Add("categoryName", request.CategoryName)` bằng `Add("categoryId", request.CategoryId?.ToString("D"))`;
  - `Features/Inventory/ViewModels/InventoryStockViewModel.cs`:
    - `CategoryOptions` đổi thành `ObservableCollection<CategoryOption>`, phần tử đầu là `AllCategories = new(Guid.Empty, null, "Danh mục: Tất cả", "Danh mục: Tất cả")`;
    - `[ObservableProperty] string _selectedCategory` đổi thành `[ObservableProperty] Guid _selectedCategoryId` (mặc định `Guid.Empty` = tất cả). `partial void OnSelectedCategoryChanged(string)` (dòng 123) đổi thành `OnSelectedCategoryIdChanged(Guid)`, giữ nguyên thân;
    - lấy danh sách từ `ICategoryRepository.GetTreeAsync` (thay tham số `ICategoryApiClient? categoryApiClient` bằng `ICategoryRepository? categoryRepository`), bỏ `FlattenCategoryNames`;
    - request (dòng 174) gửi `CategoryId = SelectedCategoryId == Guid.Empty ? null : SelectedCategoryId`; điều kiện dòng 184 đổi thành `SelectedCategoryId != Guid.Empty`;
    - lọc phía client (dòng ~335) đổi thành `SelectedCategoryId == Guid.Empty || row.CategoryId == SelectedCategoryId`;
    - không còn thêm tên danh mục lấy từ `_loadedRows` vào options.
  - Hàng model (`Features/Inventory/Models/InventoryStockModels.cs`) thêm `CategoryId` lấy từ DTO;
  - `Features/Inventory/Pages/InventoryStockPage.cs:60-61`: đổi thành `CreateCombo(ViewModel.CategoryOptions, "Danh mục", nameof(CategoryOption.Label), nameof(CategoryOption.Id))` (cùng overload combo trạng thái ở dòng 66 đang dùng) và `.SelectedValue(x => x.Binding(() => ViewModel.SelectedCategoryId).Mode(BindingMode.TwoWay))`.
  
  Kiểm tra: `B`/`BD` xanh
- [ ] 4.3 Cập nhật test:
  - `tests/Backend.UnitTests/InventoryServiceTests.cs` và `Inventory/InventoryControllerTests.cs`: phần lọc danh mục dùng `CategoryId`; thêm `GetStocksAsync_CategoryId_ReturnsOnlyDirectCategory` và assert DTO có `CategoryId`;
  - `tests/Frontend.UnitTests/Services/InventoryApiClientTests.cs`: assert URL chứa `categoryId=<guid>` và không chứa `categoryName`;
  - `tests/Frontend.UnitTests/Features/Inventory/InventoryStockViewModelTests.cs`: thay fake `ICategoryApiClient` bằng fake `ICategoryRepository` (xem `tests/Frontend.UnitTests/ViewModels/Catalog/CatalogFakes.cs`), thêm test hai danh mục trùng tên khác ID chỉ lọc đúng ID.
  
  Kiểm tra: `TB`/`TF` xanh
- [ ] 4.4 Client không ẩn bớt dòng theo trạng thái (D11): trong `InventoryStockViewModel`, ở hàm chiếu dòng hiển thị (khoảng dòng 336, điều kiện `StockStatusFilter == InventoryStockStatusFilter.All || row.Status == ToStatus(StockStatusFilter)`), bỏ điều kiện trạng thái khi đang dùng phân trang server (cùng điều kiện đang quyết định gửi `LowStock`/`OutOfStock`/`InStock` lên server ở dòng 177-185). Giữ `InventoryStockPresentationRules.StatusFor`. Thêm test vào `tests/Frontend.UnitTests/Features/Inventory/InventoryStockViewModelTests.cs`:
  - `LowStockFilter_ShowsEveryServerRow_AndKeepsServerTotals` (fake trả 15 dòng sắp hết, `TotalCount` 40 → `VisibleRows` 15, tổng và số trang theo 40);
  - `StatusFor_UsesRowMinStockAlert` (min 10, avail 8 → `LowStock`; min 0, avail 5 → `LowStock`; avail 0 → `OutOfStock`).
  
  Kiểm tra: `TF` xanh
- [ ] 4.5 Commit `feat(inventory): filter stocks by category id`. Kiểm tra: `git grep -n "CategoryName" shared/Requests backend/Inventory/Repositories` không còn kết quả, `B`/`TB`/`TF` xanh

## 5. Một repository danh mục phía client (D2)

- [ ] 5.1 Tạo `frontend/TechStore.Client/Models/Catalog/CategoryNode.cs`: chuyển record `CategoryNode` từ `Features/Categories/Models/CategoryNode.cs` sang đây (namespace `TechStore.Client.Models.Catalog`), xóa file cũ, sửa `using` ở các nơi dùng. Thêm link file này vào `tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` nếu project test chưa link cả `Models/Catalog/**`. Kiểm tra: `B` xanh
- [ ] 5.2 Tạo `Repositories/Catalog/CategoryWorkspaceFailure.cs` chứa `CategoryApiFailure` (bỏ `SessionRequired`) và `CategoryApiException` (chép từ `Services/Api/ICategoryApiClient.cs`). Tạo `Repositories/Catalog/ICategoryWorkspaceRepository.cs` đúng chữ ký ở D2. Kiểm tra: `B` xanh
- [ ] 5.3 `Repositories/Catalog/CategoryRepository.cs`:
  - implement thêm `ICategoryWorkspaceRepository`;
  - tách phần đọc và kiểm tra JSON thành `private async Task<IReadOnlyList<CategoryTreeNodeDto>> LoadTreeAsync(CancellationToken)`;
  - `GetTreeAsync` làm phẳng như cũ;
  - `GetNodesAsync` map đệ quy sang `CategoryNode` (chép hàm `Map` cuối `CategoryApiClient.cs`);
  - `CreateAsync` gọi `apiClient.PostAsync<CreateCategoryRequest, CategoryDto>("api/categories", request, ct)`;
  - `UpdateAsync` gọi `PutAsync<UpdateCategoryRequest, CategoryDto>($"api/categories/{id:D}", ...)`;
  - `DeleteAsync` gọi `apiClient.DeleteAsync($"api/categories/{id:D}", ct)`;
  - mỗi hàm bọc `try { ... } catch (ApiException e) { throw Translate(e, defaultMessage); }`, với `Translate` theo bảng ở D2 và câu mặc định chép từ `CategoryApiClient`.
  
  Kiểm tra: `B` xanh
- [ ] 5.4 Thay mọi chỗ dùng `ICategoryApiClient` bằng `ICategoryWorkspaceRepository`:
  - `CategoryManagementViewModel`, `CreateCategoryDialogViewModel`, `EditCategoryDialogViewModel`, `DeleteCategoryDialogViewModel`;
  - `CategoryManagementPage`, `CategoryDeleteCoordinator`;
  - mọi nơi khác do `git grep -n ICategoryApiClient frontend` tìm ra.
  
  Xóa các nhánh `CategoryApiFailure.SessionRequired => ...` trong 5 ViewModel (đã liệt kê ở `design.md` Context). Kiểm tra: `git grep -n "ICategoryApiClient\|SessionRequired" frontend` không còn kết quả, `B` xanh
- [ ] 5.5 Xóa `Services/Api/CategoryApiClient.cs`, `Services/Api/ICategoryApiClient.cs`, `Services/Authentication/IAccessTokenProvider.cs`. Trong `ServiceCollectionExtensions.cs`, xóa 2 dòng đăng ký `SessionAccessTokenProvider`/`IAccessTokenProvider`, khối `AddHttpClient<ICategoryApiClient, CategoryApiClient>` và `using TechStore.Client.Services.Authentication;`. Trong `UiServiceCollectionExtensions.cs`, đăng ký `services.AddTransient<ICategoryWorkspaceRepository, CategoryRepository>();` cạnh dòng `ICategoryRepository`. Trong `tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`, xóa khối `<Compile Include="..\..\frontend\TechStore.Client\Services\Authentication\**\*.cs" .../>` (dòng 50-51). Kiểm tra: `git grep -n "IAccessTokenProvider\|SessionAccessTokenProvider\|CategoryApiClient" frontend tests` không còn kết quả, `B`/`BD` xanh
- [ ] 5.6 Test:
  - xóa `tests/Frontend.UnitTests/Services/CategoryApiClientTests.cs`;
  - thêm vào `tests/Frontend.UnitTests/Repositories/Catalog/CategoryRepositoryTests.cs` (dùng fake `IApiClient` đang có trong file đó) các test sau:
    - `GetNodesAsync_MapsCountsAndChildren`;
    - `CreateAsync_PostsToCategories`;
    - `UpdateAsync_PutsToCategoryId`;
    - `DeleteAsync_DeletesCategoryId`;
    - `CreateAsync_Http409_ThrowsConflictWithDetail`;
    - `UpdateAsync_Http400_JoinsFieldErrors`;
    - `DeleteAsync_Http401_ThrowsUnauthorized`;
    - `GetNodesAsync_NetworkError_ThrowsUnavailable`;
  - các test dialog Category đổi fake sang `ICategoryWorkspaceRepository`, xóa `InlineData(CategoryApiFailure.SessionRequired, ...)` trong `DeleteCategoryDialogTests.cs:214`;
  - chạy `tests/Frontend.UnitTests/Composition/ServiceCollectionExtensionsTests.cs` để chắc `CategoryManagementPage` còn resolve được.
  
  Kiểm tra: `TF` xanh
- [ ] 5.7 Commit `refactor(client): route category workspace through IApiClient`. Kiểm tra: `B`/`TB`/`TF` xanh

## 6. `InventoryApiClient` thành `InventoryRepository` (D3)

- [ ] 6.1 Chuyển file và đổi tên:
  - `git mv frontend/TechStore.Client/Services/Api/InventoryApiClient.cs frontend/TechStore.Client/Repositories/Inventory/InventoryRepository.cs`;
  - `git mv frontend/TechStore.Client/Services/Api/IInventoryApiClient.cs frontend/TechStore.Client/Repositories/Inventory/IInventoryRepository.cs`;
  - đổi namespace thành `TechStore.Client.Repositories.Inventory`, lớp thành `InventoryRepository`, interface thành `IInventoryRepository`;
  - thêm `using TechStore.Client.Services.Api;` cho `IApiClient`/`ApiException`;
  - thay mọi tham chiếu: `git grep -ln "IInventoryApiClient\|InventoryApiClient" frontend tests | xargs sed -i 's/IInventoryApiClient/IInventoryRepository/g; s/InventoryApiClient/InventoryRepository/g'`, rồi thêm `using TechStore.Client.Repositories.Inventory;` ở file nào còn thiếu;
  - trong `ServiceCollectionExtensions.cs`, chuyển dòng `services.AddTransient<IInventoryRepository, InventoryRepository>();` sang `UiServiceCollectionExtensions.cs`, cạnh các repository khác.
  
  Kiểm tra: `B`/`BD` xanh
- [ ] 6.2 Test: `git mv tests/Frontend.UnitTests/Services/InventoryApiClientTests.cs tests/Frontend.UnitTests/Repositories/Inventory/InventoryRepositoryTests.cs`, đổi tên lớp test và namespace. Kiểm tra link production trong `TechStore.Client.UnitTests.csproj`: nếu `Repositories/**` chưa được link thì thêm. Kiểm tra: `TF` xanh, số test client không giảm so với cuối nhóm 5
- [ ] 6.3 Commit `refactor(client): rename inventory api client to repository`. Kiểm tra: `git grep -n InventoryApiClient` không còn kết quả

## 7. ViewModel trang và binding (D4, D5)

- [ ] 7.1 Hai ViewModel tab đã có `CancelLoading()` (`InventoryStockViewModel.cs:246`, `SerialImeiViewModel.cs:132`). **Không tạo phương thức mới.** Chỉ thêm test, nếu file test chưa có: `CancelLoading_DuringLoad_DoesNotApplyResult` trong `InventoryStockViewModelTests.cs` và `SerialImeiViewModelTests.cs` (fake repository trả `TaskCompletionSource` chưa xong, gọi `CancelLoading()`, sau đó hoàn tất TCS; assert danh sách dòng không đổi). Kiểm tra: `TF` xanh
- [ ] 7.2 Chuyển `CategoryManagementViewModel` sang `PageViewModel` theo D4:
  - constructor `(ICategoryWorkspaceRepository categories, ICategoryDialogRequests dialogs, InventoryStockViewModel inventory, SerialImeiViewModel serials)`;
  - thuộc tính `Inventory`, `Serials`;
  - bỏ `_isLoading`, `_errorMessage`, `_hasLoaded`, `_loadCancellation`, `_loadGeneration`, `LoadCommand`, `CancelLoading`, `HasError`, `NotifyState` cùng các `partial void On...Changed` liên quan;
  - override `LoadContentAsync` (có `catch { ClearRows(); throw; }`, **không** gán `ErrorMessage`), `GetSafeErrorMessage` (đúng code mẫu D4), `OnDeactivated` (gọi `Inventory.CancelLoading()` và `Serials.CancelLoading()`), `OnContentStateChanged` (phát `ShowStatePanel`, `EmptyText`);
  - các chỗ trước gọi `LoadCommand.ExecuteAsync(null)` sau khi tạo/sửa/xóa đổi thành `await ReloadAsync()`.
  
  Kiểm tra: `B` xanh
- [ ] 7.3 `CategoryManagementPage`:
  - bỏ tham số constructor `InventoryStockViewModel`, `SerialImeiViewModel`, `ICategoryApiClient`/`ICategoryWorkspaceRepository` nếu chỉ để truyền tiếp; dùng `ViewModel.Inventory`, `ViewModel.Serials`;
  - xóa lớp lồng `CategoryRouteViewModel` và trường `_routeViewModel`;
  - `PageViewModel IRoutePage.ViewModel => ViewModel;`;
  - nút "Thử lại" gắn `ViewModel.RetryCommand`;
  - các binding `HasError` đổi thành `IsError`.
  
  Kiểm tra: `BD` xanh, `git grep -n "CategoryRouteViewModel\|LoadCommand" frontend/TechStore.Client/Features` không còn kết quả
- [ ] 7.4 Sửa các chỗ vi phạm theo bảng D5:
  - `stateWrapper` dùng binding `ShowStatePanel`;
  - tạo `public sealed record PageButtonItem(int Number, bool IsCurrent);` trong `Features/Categories/Models/PageButtonItem.cs`;
  - `CategoryPageButtons` (`CategoryManagementPage.cs`) và phân trang ở `InventoryStockPage.cs:281-290`, `SerialImeiPage.cs:165-175` thay bằng `ItemsControl` gắn `PageButtons`, dùng **đúng template hai nút** trong D5. ViewModel dựng lại `PageButtons` ở cùng chỗ đang cập nhật `PageNumbers`/`CurrentPage`. `GoToPage(object? parameter)` kiểm tra `parameter is int page`. Xóa `OnPageNumbersChanged` và các dòng `CollectionChanged +=`/`-=`;
  - `CategoryTableRows.cs:70` chỉ viết lại thành `expand.Visibility(ShowIf(row.HasChildren));` (không đổi ý nghĩa);
  - `CreateCategoryDialog` thêm `CancelCommand`, hai nút đóng gắn command;
  - **không** xóa các event "yêu cầu mở dialog", `RequestClose`, hover và `Loaded`/`Unloaded` vòng đời: đây là ngoại lệ đã ghi ở D5.
  
  Thêm test `PageButtons_MarkCurrentPage` cho `InventoryStockViewModel` và `SerialImeiViewModel`. Kiểm tra: `git grep -nE "PropertyChanged \+=|Click \+=|CollectionChanged \+=|\.Visibility = " frontend/TechStore.Client/Features` không còn kết quả, `BD` xanh, `TF` xanh
- [ ] 7.5 Thêm các ngoại lệ ở D5 vào danh sách "Allowed exceptions" ở AGENTS.md §6: `RequestClose` → `Hide()`; event "yêu cầu mở dialog" từ ViewModel tới trang; hover bằng `PointerEntered/Exited` chỉ đổi màu theo token. Sửa câu "documented in `openspec/changes/csharp-markup-foundation/design.md` D2" và "Any new exception must be added to design D2 first" thành đường dẫn `openspec/changes/archive/2026-10-17-csharp-markup-foundation/design.md`, kèm câu: các ngoại lệ thêm sau ghi trong design của change đã thêm chúng (ví dụ `catalog-inventory-alignment` D5). Kiểm tra: `grep -n "RequestClose" AGENTS.md` có kết quả
- [ ] 7.6 Cập nhật `tests/Frontend.UnitTests/Features/Categories/CategoryWorkspaceTests.cs` cho ViewModel mới:
  - tạo ViewModel với fake repository và hai ViewModel tab;
  - thay `LoadCommand.ExecuteAsync` bằng `ActivateAsync()`;
  - `HasError` đổi thành `IsError`;
  - `IsLoading`/`IsEmpty` theo `ContentState`.
  
  Thêm test:
  - `ActivateAsync_TreeLoads_ContentState`;
  - `ActivateAsync_Unauthorized_ErrorWithSessionMessage`;
  - `Filter_NoMatch_EmptyThenBackToContent`;
  - `Deactivate_CancelsTabViewModels`;
  - `PageButtons_MarkCurrentPage`.
  
  Kiểm tra: `TF` xanh
- [ ] 7.7 Commit `refactor(inventory): page view model and bindings for category workspace`. Kiểm tra: `B`/`TB`/`TF` xanh

## 8. Màu và font theo theme (D6, spec `category-workspace`)

- [ ] 8.1 `frontend/TechStore.Client/Styles/AppColors.cs`: đổi `Light[DangerText]` về `"#B91C1C"` và xóa chú thích "Bright red action/text token ...". Thêm test `AppColors_TextTokens_MeetContrastOnSurface` (theory `Light`/`Dark`) vào `tests/Frontend.UnitTests/Styles/DesignTokenKeysTests.cs`: tính relative luminance theo WCAG (kênh sRGB `c <= 0.03928 ? c/12.92 : ((c+0.055)/1.055)^2.4`, `L = 0.2126R + 0.7152G + 0.0722B`, tỉ lệ `(L1+0.05)/(L2+0.05)`) cho 8 key chữ ở D6 so với `Surface`, yêu cầu ≥ 4.5. Kỳ vọng: sau khi đổi `DangerText`, cả 16 cặp đều đạt (đã tính trước, xem D6). **Nếu test đỏ ở key khác thì dừng lại và báo người dùng, không tự đổi màu.** Kiểm tra: `TF` xanh
- [ ] 8.2 Viết lại helper trong `Features/Categories/Pages/CategoryWorkspaceStyles.cs` theo D6:
  - `Theme(key)`;
  - `Text(value, size, colorKey, weight)` dùng `AppTypography.Fonts.*`;
  - `Badge(content, StatusTone tone)`;
  - `Button(content, primary, tooltip)` dùng `AppControlStyles.Styles.PrimaryButton`/`QuietButton`, không còn `button.Resources[...]`;
  - `Icon(symbol, colorKey, size)` áp `.Stroke(Theme(colorKey))`;
  - giữ `ShowIf` và `ResponsivePair`;
  - xóa `Brush`, `Primary`, `Slate*`, `Typeface`.
  
  Kiểm tra: biên dịch báo lỗi ở các file gọi helper (sửa ở 8.3–8.5)
- [ ] 8.3 Model trả tone:
  - `Features/Categories/Models/CategoryIconResolver.cs` trả `(string Symbol, StatusTone Tone)` theo bảng D6;
  - `Features/Inventory/Models/InventoryStockModels.cs` (`StockStatusPresentation`) trả `StatusTone` thay cho 2 chuỗi hex;
  - test trong `tests/Frontend.UnitTests/Features/**` assert tone thay cho hex.
  
  Kiểm tra: `git grep -n '"#' frontend/TechStore.Client/Features/*/Models` không còn kết quả
- [ ] 8.4 Đổi màu trong các file trang (`Features/Categories/Pages/CategoryManagementPage.cs`, `CategoryTableRows.cs`, `Features/Inventory/Pages/InventoryStockPage.cs`, `SerialImeiPage.cs`):
  - mọi `Brush("#...")`/hằng `Slate*` dùng **bảng hex → token ở D6** và fluent `.Background(Theme(AppColors.X))`/`.Foreground(...)`/`.BorderBrush(...)`. Không được dùng object initializer cho thuộc tính màu;
  - xóa mọi `RequestedTheme = ...`, `FontFamily = Typeface`.
  
  Kiểm tra: `git grep -nE '"#[0-9A-Fa-f]{6}|RequestedTheme|Typeface|ColorHelper|SolidColorBrush' frontend/TechStore.Client/Features/*/Pages` không còn kết quả, `BD` xanh
- [ ] 8.5 Làm như 8.4 cho các dialog: `Features/Categories/Dialogs/{CreateCategoryDialog,EditCategoryDialog,DeleteCategoryDialog}.cs`, `Features/Inventory/Dialogs/{DeleteStockItemDialog,ImportStockDialog,StockAdjustmentDialog,StockDetailHistoryDialog}.cs`. Kiểm tra: `git grep -nE '"#[0-9A-Fa-f]{6}|RequestedTheme|Typeface|ColorHelper|SolidColorBrush|Color\.FromArgb' frontend/TechStore.Client/Features` không còn kết quả, `BD` xanh
- [ ] 8.6 Font:
  - `git grep -n "PlusJakarta" frontend` phải chỉ còn trong `Assets/Fonts`;
  - `git rm` các file `PlusJakartaSans*.ttf`, `PlusJakartaSans.ttf.manifest`, `OFL.txt`;
  - sửa `Assets/Fonts/README.md` bỏ phần Plus Jakarta (nếu README chỉ nói về Plus Jakarta thì xóa luôn file).
  
  Kiểm tra: `BD` xanh, `git grep -n "PlusJakarta" frontend` không còn kết quả
- [ ] 8.7 Kiểm tra theme khi chạy (AGENTS.md §6 quy tắc 5):
  - chạy backend ở 5179;
  - chạy client với `XDG_DATA_HOME=<thư mục tạm> TECHSTORE_API_BASE_URL=http://localhost:5179/ dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`;
  - đăng nhập `admin`/`Admin@123`, mở "Danh mục và Kho";
  - ở cả 3 tab, đổi Cài đặt Sáng → Tối → Sáng trong khi trang vẫn mở;
  - mở từng dialog (tạo/sửa/xóa danh mục, nhập kho, điều chỉnh, chi tiết, xóa tồn kho) ở chế độ Tối.
  
  Chỗ nào không đổi màu thì sửa theo mục Risks của design. Kiểm tra: ảnh chụp trước/sau cho Sáng và Tối lưu vào `openspec/changes/catalog-inventory-alignment/evidence/` và liệt kê trong `evidence.md`
- [ ] 8.8 Commit `fix(inventory): follow app theme tokens in category and stock UI`. Kiểm tra: `B`/`TB`/`TF` xanh

## 9. Chuyển thư mục về cấu trúc nhóm (D8)

- [ ] 9.1 Chạy `git ls-files frontend/TechStore.Client/Features tests/Frontend.UnitTests/Features`, đối chiếu bảng D8, xác nhận không có hai file trùng tên trong cùng thư mục đích (đặc biệt `Views/Inventory/` và `ViewModels/Inventory/`). Kiểm tra: liệt kê kết quả vào `evidence.md`
- [ ] 9.2 `git mv` từng nhóm file theo bảng D8. Đổi `CategoryWorkspaceStyles.cs` thành `Views/Inventory/WorkspaceParts.cs` (lớp `WorkspaceParts`, sửa các `using static`). Sửa namespace bằng sed theo bảng, ví dụ `sed -i 's/namespace TechStore.Client.Features.Categories.Pages;/namespace TechStore.Client.Views.Inventory;/'`, và sửa mọi `using TechStore.Client.Features...` tương ứng trong `frontend` và `tests`. Kiểm tra: `git grep -n "TechStore.Client.Features" frontend tests` không còn kết quả, thư mục `frontend/TechStore.Client/Features` không còn, `B`/`BD` xanh
- [ ] 9.3 Test:
  - `git mv tests/Frontend.UnitTests/Features/Categories/*` và `Features/Inventory/*` sang `tests/Frontend.UnitTests/ViewModels/Inventory/`, sửa namespace;
  - trong `TechStore.Client.UnitTests.csproj`, **xóa** cả 6 khối `<Compile Include="..\..\frontend\TechStore.Client\Features\...">` (dòng 52-63). Project đã glob sẵn `Models/**`, `Repositories/**`, `ViewModels/**` (dòng 31-34), nên **không thêm** link cho ba thư mục đó (thêm sẽ biên dịch trùng). Chỉ thêm một khối mới cho `Services\Inventory\**\*.cs` theo mẫu khối `Services\Session` (dòng 48).
  
  Kiểm tra: `TF` xanh, tổng số test client bằng số cuối nhóm 8
- [ ] 9.4 `Views/Shell/ShellRoutePageFactory.cs` và `UiServiceCollectionExtensions.cs` chỉ đổi `using`. Chạy `VerifyAllRoutesResolve` qua test composition hiện có. Kiểm tra: `TF` xanh
- [ ] 9.5 AGENTS.md:
  - mục "Key Modules" phần `frontend/TechStore.Client/` bỏ ý nói về `Features`;
  - "Key Directories": `Views/` thêm Inventory, `Repositories/` thêm Inventory;
  - bảng "Conversion status of views" bỏ `InventoryPage` khỏi "Still XAML", thêm `CategoryManagementPage`, `InventoryStockPage`, `SerialImeiPage` cùng các dialog Inventory/Category vào dòng "C# Markup with theme-aware tokens";
  - mục 9 Frontend DI thêm `ICategoryWorkspaceRepository`, `IInventoryRepository`;
  - ghi chú repository Inventory còn trả DTO là việc về sau.
  
  Kiểm tra: `grep -n "InventoryPage\b\|Features/" AGENTS.md` không còn kết quả sai
- [ ] 9.6 Commit `refactor(client): move category and inventory UI into team folders`. Kiểm tra: `B`/`TB`/`TF`/`BD` xanh

## 10. Kiểm tra tích hợp

- [ ] 10.1 Gọi API thật ở cổng 5179 với token `admin`:
  - (a) `POST /api/products` với danh mục ẩn → 400 `categoryId`; `PUT /api/products/{id}` của một sản phẩm thuộc danh mục ẩn, giữ nguyên `categoryId` và đổi giá → 200; `PUT` chuyển một sản phẩm khác sang danh mục ẩn đó → 400 `categoryId`;
  - (b) `GET /api/inventory/stocks?categoryId=<id>` chỉ trả dòng của danh mục đó, mỗi dòng có `categoryId`;
  - (c) `GET /api/inventory/stocks?search=%25` không trả mọi dòng;
  - (d) `GET /api/categories/tree` có `productCount`/`skuCount` đúng;
  - (e) đăng nhập một tài khoản Manager (tạo qua seed hoặc API quản lý người dùng nếu có; nếu không có cách tạo thì ghi rõ là bỏ qua và dựa vào test 3.6), gọi `DELETE /api/inventory/stocks/<id>` → 403;
  - (f) tổng `totalCount` của `inStock=true`, `lowStock=true`, `outOfStock=true` bằng `totalCount` khi không lọc.
  
  Kiểm tra: lệnh và kết quả rút gọn (không có token) ghi trong `evidence.md`
- [ ] 10.2 Chạy client (như 8.7):
  - tạo sản phẩm: picker không có danh mục ẩn;
  - sửa một sản phẩm trong danh mục ẩn: thấy nhãn "(đang ẩn)" và dòng thông tin; đổi giá rồi lưu thành công, sản phẩm vẫn ở danh mục đó; picker không có danh mục ẩn nào khác;
  - danh sách sản phẩm: bộ lọc có hậu tố "(đang ẩn)";
  - tab Tồn kho: chọn danh mục lọc đúng.
  
  Kiểm tra: ghi kết quả từng bước vào `evidence.md`
- [ ] 10.3 `openspec validate catalog-inventory-alignment` và `openspec validate --specs`. Kiểm tra: cả hai báo hợp lệ
- [ ] 10.4 Tổng kết vào `evidence.md`: số test backend/client trước và sau, danh sách commit (`git log --oneline <base>..HEAD`), ghi chú cho Dev 6: Dashboard vẫn đếm "sắp hết" theo `Quantity <= MinStockAlert` (gồm cả hết hàng, không trừ số giữ chỗ), khác định nghĩa D11. Kiểm tra: file có đủ các mục trên

## Workflow follow-up

- Gửi Dev 3 bản tóm tắt thay đổi (kể cả bỏ quyền xóa của Manager và định nghĩa `lowStock` mới) cùng ảnh Sáng/Tối để duyệt trước khi mở PR.
- Báo Dev 6 về khác biệt định nghĩa "sắp hết" giữa Dashboard và Inventory.
- Push và mở PR vào `dev` chỉ khi người dùng yêu cầu.
- Archive change bằng `/opsx:archive catalog-inventory-alignment` sau khi PR được merge.
