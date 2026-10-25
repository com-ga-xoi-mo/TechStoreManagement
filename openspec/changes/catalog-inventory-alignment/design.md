# Design

## Context

Lý do của change xem `proposal.md` mục Why. Hành vi cần đạt xem các delta spec trong `specs/`. Phần dưới đây chỉ mô tả hiện trạng code liên quan, đo trên nhánh `feat/dev3-catalog-inventory-management` sau commit merge `594320d` (đã gộp `origin/dev` chứa PR #16).

### Hiện trạng backend

- **`ProductService`**
  - `CreateAsync` kiểm tra danh mục tại `ProductService.cs:266-272`, nhưng chỉ kiểm tra danh mục có tồn tại.
  - `UpdateAsync` thì kiểm tra cả active tại `:407-411`, với thông báo `"Category '{id}' does not exist or is inactive."`.
- **`CategoryRepository.GetActiveCategoriesAsync`**
  - Không còn lọc `IsActive`.
  - `Include(c => c.Products).ThenInclude(p => p.Variants)` để `CategoryService.GetTreeAsync` tính `ProductCount` và `SkuCount` (với `SkuCount = Σ max(1, số biến thể)`).
- **`CategoryService.UpdateAsync`** có hàm `RestoreCategory(...)` để trả lại giá trị entity khi lỗi. Việc này thừa, vì `UnitOfWork.RollbackAsync` đã gọi `ChangeTracker.Clear()`.
- **`InventoryService`**
  - Có `using Microsoft.EntityFrameworkCore; using Npgsql;`.
  - `AdjustStockAsync` bắt `DbUpdateException when (ex.InnerException is PostgresException { SqlState: "23514" })`.
  - Dùng `DateTime.UtcNow` ở `:286` và `:298`.
- **`InventoryStock`** có thuộc tính `[NotMapped] public int? RelatedSerialCount`, do `InventoryStockRepository.SearchStocksAsync` và `GetDetailAsync` gán giá trị.
- **`IInventoryStockRepository`** có `GetSerialsForUpdateAsync(productId, variantId)` và `DeleteSerials(...)`, đều thao tác bảng `serial_imeis`.
- **Tìm kiếm** trong `InventoryStockRepository.SearchStocksAsync` và `SerialImeiRepository.SearchAsync` dùng `EF.Functions.ILike(x, $"%{search}%")` mà không escape. Catalog và Customers đã có `EscapeLikePattern` (ký tự escape `!`): xem `CustomerRepository.cs:115` và `ProductRepository.cs:34`.
- **Lọc tồn kho theo danh mục** dùng `criteria.CategoryName` (so sánh chuỗi tên).
- **`Program.cs:106`** đã có `builder.Services.TryAddSingleton(TimeProvider.System);`. Hiện mới `CustomerService`, `OrderService`, `AuthService`, `StoreSettingsService` dùng `TimeProvider`.

### Hiện trạng client

- **`Services/Api/CategoryApiClient.cs`** (270 dòng)
  - Dùng một `HttpClient` riêng, đăng ký trong `ServiceCollectionExtensions.cs` với `Timeout = InfiniteTimeSpan` và không qua `BearerTokenHandler`.
  - Tự gắn token lấy từ `IAccessTokenProvider`.
  - Ném `CategoryApiException(CategoryApiFailure, message)`, kể cả `SessionRequired` khi chưa có token.
- **`Services/Api/InventoryApiClient.cs`** đi qua `IApiClient`, tức là đúng hướng, chỉ sai tên và sai chỗ. Nó trả DTO và các record kết quả (`InventoryStockDetailResult`, ...), định nghĩa trong `IInventoryApiClient.cs`.
- **`Repositories/Catalog/CategoryRepository.cs`** (của Catalog) cũng gọi `api/categories/tree` qua `IApiClient`, kiểm tra JSON rồi trả `IReadOnlyList<CategoryOption>` phẳng, kèm `DisplayPath` dạng "Cha / Con". `CategoryOption` chưa có `IsActive`.
- **`CategoryManagementPage`** là trang route của mục "Danh mục và Kho" (`ShellDestination.Inventory`).
  - Trang tự tạo `InventoryStockPage` và `SerialImeiPage` làm nội dung tab.
  - Trang bọc `CategoryManagementViewModel : ObservableObject` trong lớp riêng `CategoryRouteViewModel : PageViewModel` (dòng 661) để có activation lease.
- **View trong `Features/**`**
  - Dựng UI bằng object initializer và helper trong `Features/Categories/Pages/CategoryWorkspaceStyles.cs`: `Brush(hex)`, `Text(value, size, colorHex, weight)`, `Badge(content, bgHex, borderHex)`, `Button(content, primary, tooltip)`, `Icon(symbol, colorHex, size)`.
  - Cả 13 file view có khoảng 380 chỗ dùng màu hex.
  - Ba trang đặt `RequestedTheme = ElementTheme.Light`.
  - `Typeface` trỏ tới font Plus Jakarta Sans trong `Assets/Fonts/`, còn token chung là Inter.
- **`CategoryIconResolver`** và **`InventoryStockModels.StockStatusPresentation`** trả mã hex trong model.
- **`AppColors.Light[DangerText]`** bị đổi từ `#B91C1C` sang `#EF4444`. Contrast trên nền `#FFFFFF` chỉ khoảng 3.8:1, dưới mức 4.5:1 của spec `frontend-design-system`.
- **Project test client** link `Features/Categories/{Models,ViewModels,Services}` và `Features/Inventory/{Models,ViewModels,Services}` bằng `<Compile Include ... Link=...>` (`TechStore.Client.UnitTests.csproj:52-63`).

## Goals / Non-Goals

**Goals:**
- Mọi lời gọi HTTP của client đi qua `IApiClient` và `BearerTokenHandler`. Mỗi endpoint danh mục chỉ có một nơi phân tích JSON.
- Backend tuân thủ đúng lớp: service không biết tới EF/Npgsql, entity không chứa dữ liệu hiển thị, mỗi repository chỉ làm việc với aggregate của nó.
- Trang và dialog Category/Inventory đổi theo Sáng/Tối, không còn mẫu bị cấm ở AGENTS.md §6.
- Mỗi nhóm task để lại build xanh và test xanh, để có thể commit từng bước.

**Non-Goals:**
- Không đổi schema database, không thêm migration.
- Không đổi quyền theo role, trừ việc bỏ quyền xóa tồn kho của Manager (D10).
- Không viết lại bố cục hay thiết kế của các trang Dev 3. Chỉ thay nguồn màu/font, cách gắn dữ liệu và chỗ đặt file.
- Không đổi định nghĩa "sắp hết" của Dashboard (`AnalyticsRepository`, code Dev 6). Xem D11.
- Không ẩn nút xóa/điều chỉnh theo role ở client (hiện cả workspace chưa gác nút theo role). Lỗi 403 vẫn được dialog báo bằng `InventoryStockDeleteFailure.Forbidden`.
- Không bỏ các default interface method "unsupported" trong `IInventoryRepository`.
- Không sửa `InventoryFulfillmentService` (luồng đơn hàng) và `DashboardPage`.
- Không đổi suppress `MVVMTK0045`/`NETSDK1198` trong `frontend/Directory.Build.props`.

## Decisions

### D1. Danh mục ẩn: chặn ở server, lọc ở client

- **Quy tắc chung:** ẩn danh mục chỉ ngăn **đưa thêm** sản phẩm vào danh mục đó (tạo mới, hoặc chuyển từ danh mục khác sang). Sản phẩm đang nằm sẵn trong danh mục vẫn sửa được bình thường.
- **Server, tạo:** `ProductService.CreateAsync` (dòng 266-272) trả lỗi `categoryId` khi `category == null || !category.IsActive`, với nội dung `"Category '{id}' does not exist or is inactive."`.
- **Server, sửa:** `ProductService.UpdateAsync` (dòng 407-411) đổi điều kiện thành `category == null || (!category.IsActive && request.CategoryId!.Value != product.CategoryId)`. Biến `product` đã được khóa và nạp ở dòng 400 trước đoạn này, nên so sánh được ngay. Thông báo giữ nguyên.
- **Phương án đã loại (A):** bắt buộc danh mục active cả khi giữ nguyên. Cách này làm việc ẩn danh mục "đóng băng" mọi sản phẩm bên trong, đến đổi giá cũng không được. Người dùng đã chọn B ngày 2026-10-25.
- **Client:** `CategoryOption` thêm `bool IsActive`.
  - `ProductEditorViewModel` lọc danh sách cho picker: giữ `o.IsActive || o.Id == SelectedCategoryId` (với `SelectedCategoryId` là danh mục của sản phẩm đang sửa, gán sau khi nạp sản phẩm).
  - Danh mục ẩn còn giữ lại được hiển thị với nhãn `DisplayPath + " (đang ẩn)"`.
  - Thêm `CategoryHint`: khi danh mục đang chọn bị ẩn thì hiện "Danh mục này đang bị ẩn. Sản phẩm vẫn lưu được, nhưng không thể chuyển sản phẩm khác vào danh mục này.", ngược lại để rỗng. Đây là thông tin, không phải lỗi, nên dùng màu `InfoText`. Picker chỉ có danh mục active cộng danh mục hiện tại, nên trên giao diện không thể chọn một danh mục ẩn khác.
  - `ProductListViewModel` giữ mọi danh mục, chỉ thêm hậu tố "(đang ẩn)" vào nhãn.
- **Hậu tố đặt ở đâu:** trên model, dạng thuộc tính tính toán `public string Label => IsActive ? DisplayPath : $"{DisplayPath} (đang ẩn)";` trong record `CategoryOption`. Hai ComboBox (`ProductEditorPage.cs:103`, `ProductListPage.cs:96`) đổi `.DisplayMemberPath("DisplayPath")` thành `.DisplayMemberPath("Label")`. Repository vẫn trả dữ liệu thô (`DisplayPath`, `IsActive`), còn quy tắc hiển thị nằm ở model, test được không cần UI.
- **Phương án đã loại:** ẩn hẳn danh mục hiện tại khỏi picker khi sửa. Cách này làm ComboBox mất giá trị đang chọn, và người dùng lưu xong sẽ bị đổi danh mục ngoài ý muốn.

### D2. Một repository danh mục phía client

- `Repositories/Catalog/CategoryRepository` implement thêm interface mới `ICategoryWorkspaceRepository`, giống cách `ProductRepository` implement 4 interface:

  ```csharp
  public interface ICategoryWorkspaceRepository
  {
      Task<IReadOnlyList<CategoryNode>> GetNodesAsync(CancellationToken cancellationToken = default);
      Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
      Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
      Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
  }
  ```

- `GetNodesAsync` và `GetTreeAsync` dùng chung một hàm private `LoadTreeAsync` (gọi `GetAsync<JsonElement>`, kiểm tra JSON, deserialize). Một hàm map ra `CategoryNode`, một hàm làm phẳng ra `CategoryOption`.
- **Lỗi:** giữ `CategoryApiException` và `CategoryApiFailure` để ViewModel ít phải đổi, nhưng chuyển sang file `Repositories/Catalog/CategoryWorkspaceFailure.cs` (namespace `TechStore.Client.Repositories.Catalog`) và **bỏ giá trị `SessionRequired`**. Khi thiếu token, server trả 401, nên lỗi gom về `Unauthorized`. Bảng chuyển từ `ApiException`:

  | `ApiException` | `CategoryApiFailure` | Thông báo |
  |:--|:--|:--|
  | `Kind == Http`, `StatusCode` 401 | `Unauthorized` | `null` (ViewModel tự có câu) |
  | 403 | `Forbidden` | `null` |
  | 404 | `NotFound` | `Detail` hoặc câu mặc định theo thao tác |
  | 409 | `Conflict` | `Detail` hoặc câu mặc định theo thao tác |
  | 400 | `BadRequest` | `Detail`, hoặc nối `FieldErrors` bằng `"; "`, hoặc câu mặc định |
  | Http khác | `Unavailable` | `null` |
  | `Network`, `Timeout` | `Unavailable` | `null` |
  | `InvalidResponse` | `InvalidResponse` | `null` |

  Câu mặc định giữ nguyên chữ đang có trong `CategoryApiClient` (ví dụ "Danh mục hoặc slug đã tồn tại.", "Không thể xóa danh mục vì vẫn còn danh mục con."). `OperationCanceledException` do người gọi hủy thì ném tiếp.
- Bỏ hẳn `CategoryApiClient`, `ICategoryApiClient`, `Services/Authentication/IAccessTokenProvider.cs` (gồm `SessionAccessTokenProvider`) và đăng ký `AddHttpClient<ICategoryApiClient, CategoryApiClient>`.
- **Phương án đã loại:**
  - Giữ `CategoryApiClient` nhưng sửa cho dùng `IApiClient`: vẫn còn hai lớp cùng phân tích một endpoint.
  - Gộp mọi thứ vào `ICategoryRepository`: buộc màn Catalog (chỉ đọc) phụ thuộc vào các lệnh ghi danh mục.

### D3. `InventoryApiClient` thành `InventoryRepository`

- Đổi tên lớp, interface và chỗ đặt: `Services/Api/InventoryApiClient.cs` sang `Repositories/Inventory/InventoryRepository.cs`, `IInventoryApiClient.cs` sang `Repositories/Inventory/IInventoryRepository.cs`. Namespace là `TechStore.Client.Repositories.Inventory`. Các enum và record kết quả đi theo file interface.
- Repository vẫn trả DTO, như `AuthRepository` và `StoreSettingsRepository` đang làm. Chuyển sang Model là việc về sau, không làm trong change này.
- `BuildPath` đổi `categoryName` thành `categoryId` (dạng `ToString("D")`).

### D4. ViewModel trang kế thừa `PageViewModel`

- `CategoryManagementViewModel : PageViewModel`, nhận qua constructor:
  - `ICategoryWorkspaceRepository categories`;
  - `ICategoryDialogRequests dialogs`;
  - `InventoryStockViewModel inventory`;
  - `SerialImeiViewModel serials`.
- Hai ViewModel tab được công khai qua thuộc tính `Inventory` và `Serials`. `CategoryManagementPage` lấy `ViewModel.Inventory` và `ViewModel.Serials` thay vì tự nhận instance từ DI. Lý do: DI transient sẽ tạo instance khác với instance mà ViewModel trang giữ.
- **Chuyển trạng thái tải:**
  - Bỏ `[ObservableProperty] _isLoading`, `_errorMessage`, `_hasLoaded`, `_loadCancellation`, `_loadGeneration`, `CancelLoading()` và `LoadCommand`.
  - `LoadContentAsync(ct)` nạp cây, dựng `_allRows`, gọi `ResetProjection()`, trả `true` nếu có ít nhất một danh mục.
  - Khi `ResetProjection()` lọc ra 0 dòng trong lúc trang đang ở trạng thái `Content`, đặt `ContentState = Empty`. Ngược lại, đang `Empty` mà lọc ra có dòng thì đặt về `Content`.
  - `HasError` đổi thành `IsError` của lớp cha. `ErrorMessage` dùng thuộc tính `string` của lớp cha (lớp cha đặt `string.Empty` khi bắt đầu tải).
  - Lỗi ném ra từ `LoadContentAsync` được lớp cha chuyển sang `Error`, và lớp cha **ghi đè** `ErrorMessage = GetSafeErrorMessage(exception)` (`PageViewModel.LoadGenerationAsync`). Vì vậy không được tự gán `ErrorMessage` trong `LoadContentAsync`. Thay vào đó, override theo mẫu `ProductEditorViewModel.cs:733`:

    ```csharp
    protected override string GetSafeErrorMessage(Exception exception) => exception is CategoryApiException api
        ? api.Failure switch
        {
            CategoryApiFailure.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
            CategoryApiFailure.Forbidden => "Bạn không có quyền xem danh mục.",
            _ => "Không thể tải danh mục. Vui lòng thử lại."
        }
        : "Không thể tải danh mục. Vui lòng thử lại.";
    ```
  - Để không lộ dữ liệu của phiên trước khi tải lỗi (giữ hành vi cũ ở `LoadAsync`), `LoadContentAsync` bọc phần gọi repository trong `try { ... } catch { ClearRows(); throw; }`. `ClearRows()` xóa `RootCategories`, `_allRows`, `_byId`, `VisibleRows`, đặt `TotalCount = 0`. Lỗi `OperationCanceledException` cũng đi qua nhánh này; như vậy là chấp nhận được.
- Nút "Thử lại" dùng `RetryCommand` của lớp cha. Sau khi tạo/sửa/xóa xong, gọi `ReloadAsync()`.
- `OnDeactivated()` gọi `Inventory.CancelLoading()` và `Serials.CancelLoading()`. Hai phương thức này **đã có sẵn** (`InventoryStockViewModel.cs:246`, `SerialImeiViewModel.cs:132`), không cần tạo mới.
- Hai ViewModel tab và các ViewModel dialog **vẫn kế thừa `ObservableObject`**, vì chúng không phải trang route và không có activation lease riêng.
- Bỏ lớp `CategoryRouteViewModel`. `IRoutePage.ViewModel` trả thẳng `ViewModel`.

### D5. Gắn dữ liệu thay cho event handler trong View

Các chỗ vi phạm AGENTS.md §6 và cách sửa:

| Chỗ | Hiện tại | Sửa thành |
|:--|:--|:--|
| `CategoryManagementPage.cs:290-295` | `ViewModel.PropertyChanged +=` rồi tự đặt `stateWrapper.Visibility` | `.Visibility(x => x.Binding(() => ViewModel.ShowStatePanel).Convert(ShowIf))`. `ShowStatePanel => IsLoading \|\| IsEmpty \|\| IsError`, phát `OnPropertyChanged(nameof(ShowStatePanel))` trong `OnContentStateChanged()` |
| `CategoryPageButtons` (`CategoryManagementPage.cs:675-715`) | `PropertyChanged +=` rồi `Render()` dựng lại nút | `ItemsControl` gắn `ViewModel.PageButtons` (`ObservableCollection<PageButtonItem>`), với `PageButtonItem(int Number, bool IsCurrent)`. Template theo AGENTS.md §6 (item chỉ dùng trong lambda binding). Mỗi nút `.Command(x => x.Binding(() => ViewModel.GoToPageCommand))` và `.CommandParameter(x => x.Binding(() => item.Number))`. `GoToPage(object? parameter)` kiểm tra `parameter is int page` |
| `InventoryStockPage.cs:281-290`, `SerialImeiPage.cs:165-175` | `_viewModel.PageNumbers.CollectionChanged += OnPageNumbersChanged` rồi dựng lại nút trang bằng code | Cùng cách với `CategoryPageButtons`: mỗi ViewModel tab thêm `ObservableCollection<PageButtonItem> PageButtons` (dựng lại ở chỗ đang cập nhật `PageNumbers`), trang dùng `ItemsControl` + template. `PageButtonItem` dùng chung, đặt cạnh `CategoryRow` trong `Features/Categories/Models/` (nhóm 9 chuyển về `Models/Inventory/`). Xóa `OnPageNumbersChanged` và hai dòng đăng ký/hủy đăng ký |
| `CreateCategoryDialog.cs:159, 629` | `closeBtn.Click += (_, _) => Hide();` | `CreateCategoryDialogViewModel` thêm `[RelayCommand] private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);`. Hai nút gắn `.Command(x => x.Binding(() => ViewModel.CancelCommand))`. Dialog đã nghe `ViewModel.RequestClose += (_, _) => Hide();` ở dòng 68, nên không cần thêm gì |

**Template nút trang** (dùng cho cả ba chỗ). Không bind `Style` theo điều kiện. Mỗi item dựng **hai nút chồng nhau**, chỉ một nút hiện:

```csharp
new Grid().Children(
    new Button().Style(AppControlStyles.Styles.PrimaryButton)
        .Content(x => x.Binding(() => item.Number))
        .Visibility(x => x.Binding(() => item.IsCurrent).Convert(ShowIf)),
    new Button().Style(AppControlStyles.Styles.Button)
        .Content(x => x.Binding(() => item.Number))
        .Command(x => x.Binding(() => ViewModel.GoToPageCommand))
        .CommandParameter(x => x.Binding(() => item.Number))
        .Visibility(x => x.Binding(() => item.IsCurrent).Convert((bool current) => ShowIf(!current))))
```

`item` chỉ được dùng trong lambda binding (AGENTS.md §6, mục Item templates). `GoToPage(object? parameter)` kiểm tra `parameter is int page`. Nút Trước/Sau giữ nguyên, gắn command như hiện có.

**`CategoryTableRows.cs:70`** (`expand.Visibility = row.HasChildren ? ...`) **không vi phạm**. Trang dựng từng dòng từ object `CategoryRow` thật, và `HasChildren` tính từ `Node.Children` nên không đổi. Chỉ viết lại dạng fluent `expand.Visibility(ShowIf(row.HasChildren))`, để lệnh grep `\.Visibility = ` trong task không báo nhầm.

**Ngoại lệ được ghi nhận:**
- Dialog nghe C# event `RequestClose` của ViewModel để gọi `Hide()` (đang dùng ở `CreateCategoryDialog`, `EditCategoryDialog` và các dialog Inventory). Không bind được việc đóng `ContentDialog`, và đây không phải `PropertyChanged`.
- Trang nghe các C# event "yêu cầu mở dialog" của ViewModel để mở dialog tương ứng: `InventoryStockViewModel.DeleteRequested`, `StockDetailRequested`, `StockAdjustmentRequested`, `ActionRequested`; `StockDetailHistoryDialogViewModel.AdjustmentRequested`; `EditCategoryDialogViewModel.CategoryUpdated`, `DeleteCategoryDialogViewModel.CategoryDeleted` (qua `CategoryDeleteCoordinator`); `ICategoryDialogRequests.Requested`. Mở `ContentDialog` cần `XamlRoot` của View, không đặt trong ViewModel được, và đây không phải đồng bộ dữ liệu. **Giữ nguyên, không xóa.**
- Hover trên dòng danh mục (`CategoryTableRows.cs:45-46`, `143-144`) dùng `PointerEntered/PointerExited`. Đây là hiệu ứng thuần giao diện, không đồng bộ ViewModel, nên giữ sự kiện, nhưng màu phải theo token (xem D6, mục Hover).
- `Loaded`/`Unloaded` gọi `LoadCommand`/`CancelLoading` ở `InventoryStockPage.cs:29-30`, `SerialImeiPage.cs:28-29` và các dialog là vòng đời, không phải đồng bộ dữ liệu. Giữ nguyên. Các ngoại lệ này được ghi tại D5 này và thêm vào danh sách ngoại lệ ở AGENTS.md §6. Danh sách đó hiện trỏ tới `openspec/changes/csharp-markup-foundation/design.md`, một đường dẫn không còn tồn tại; sửa thành `openspec/changes/archive/2026-10-17-csharp-markup-foundation/design.md`. Không sửa file trong archive.

`ResponsivePair.SizeChanged` (trong `CategoryWorkspaceStyles.cs`) chỉ đổi bố cục theo độ rộng, không đồng bộ dữ liệu với ViewModel, nên giữ nguyên.

### D6. Màu theo theme: helper nhận key token, không nhận mã hex

- Viết lại `CategoryWorkspaceStyles` thành `Views/Inventory/WorkspaceParts.cs` (xem D8). Mọi helper nhận **key token** (`AppColors.*`) và áp màu bằng fluent extension, vì `ThemeResource` chỉ gắn được qua fluent API, không gắn được trong object initializer:

  ```csharp
  internal static Action<IDependencyPropertyBuilder<Brush>> Theme(string key) => ThemeResource.Get<Brush>(key);

  internal static TextBlock Text(string value = "", double size = 14, string colorKey = AppColors.Text, ushort weight = 400) =>
      new TextBlock { Text = value, FontSize = size, FontWeight = new FontWeight { Weight = weight },
                      VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }
          .FontFamily(weight >= 700 ? AppTypography.Fonts.BoldFont
              : weight >= 600 ? AppTypography.Fonts.SemiBoldFont
              : weight >= 500 ? AppTypography.Fonts.MediumFont
              : AppTypography.Fonts.RegularFont)
          .Foreground(Theme(colorKey));
  ```

  (`AppTypography.Fonts.*` nằm trong `Styles/AppTypography.Resources.cs`, kiểu `Action<IDependencyPropertyBuilder<FontFamily>>`.)
- `Badge(content, StatusTone tone)` lấy key từ `StatusBadgePresentation.GetKeys(tone)` rồi áp `.Background(Theme(keys.Fill)).BorderBrush(Theme(keys.Stroke))`.
- `Button(content, primary)` dùng `.Style(AppControlStyles.Styles.PrimaryButton)` hoặc `.Style(AppControlStyles.Styles.QuietButton)`. **Xóa** mọi dòng `button.Resources["ButtonBackground..."] = ...`.
- `Icon(symbol, colorKey, size)` áp `.Stroke(Theme(colorKey))` cho từng `Path`.
- Xóa `Brush(string hex)`, các hằng `Primary`/`Slate*` và `Typeface`.
- Xóa mọi `RequestedTheme = ElementTheme.Light/Default` trong `Features/**`.
- Nền trong suốt (`"#00000000"`): không đặt `Background`/`BorderBrush`, hoặc dùng style `QuietButton`. Không tạo `new SolidColorBrush(Colors.Transparent)`.
- **Bảng đổi hex sang token**, dùng cho mọi file trong `Features/**`. Chọn theo vai trò của thuộc tính đang nhận màu:

  | Hex hiện có | Nền / fill | Chữ / icon | Viền |
  |:--|:--|:--|:--|
  | `#FFFFFF` | `Surface` | `OnPrimary` (chỉ khi nằm trên nền `Primary`), còn lại `Surface` | `Surface` |
  | `#F8FAFC`, `#F7F9FB`, `#80F8FAFC` | `Canvas` (bản `80` bỏ alpha, **không** đặt `Opacity`, vì `grid.Opacity` đã dùng để làm mờ dòng bị ẩn) | — | — |
  | `#F1F5F9`, `#F2F4F6`, `#F1F3F5` | `Subtle` | — | `Stroke` |
  | `#E2E8F0`, `#E5E7EB`, `#E0E3E5`, `#E5E7E9` | `Subtle` | — | `Stroke` |
  | `#CBD5E1` | `NeutralFill` | — | `NeutralStroke` |
  | `#94A3B8`, `#64748B`, `#475569`, `#334155` | — | `SecondaryText` | `InputStroke` |
  | `#1E293B`, `#0F172A` | — | `Text` | — |
  | `#0066FF`, `#2563EB`, `#4F46E5` | `Primary` | `InfoText` | `Focus` |
  | `#1D4ED8` | `PrimaryHover` | `InfoText` | — |
  | `#1E40AF` | `PrimaryPressed` | `InfoText` | — |
  | `#EFF6FF`, `#EAF2FF`, `#E8F0FF`, `#DBEAFE`, `#DCE8FF`, `#EEF2FF`, `#E0E7FF`, `#C9DDFF` | `InfoFill` | — | `InfoStroke` |
  | `#BFDBFE`, `#93C5FD` | `InfoFill` | — | `InfoStroke` |
  | `#F0F7FF` | `EditContextFill` | — | `EditContextStroke` |
  | `#DC2626`, `#B91C1C`, `#991B1B` | `DangerFill` | `DangerText` | `DangerStroke` |
  | `#FEF2F2` | `DangerFill` | — | `DangerStroke` |
  | `#FCA5A5` | — | — | `DangerStroke` |
  | `#059669`, `#0D9488` | `SuccessFill` | `SuccessText` | `SuccessStroke` |
  | `#ECFDF5`, `#F0FDFA`, `#D1FAE5`, `#CCFBF1` | `SuccessFill` | — | `SuccessStroke` |
  | `#B45309` | `WarningFill` | `WarningText` | `WarningStroke` |
  | `#FFFBEB` | `WarningFill` | — | `WarningStroke` |

  Hover/pressed của nút không cần map: style `PrimaryButton` và `QuietButton` đã lo phần này.
- **Hover không phải nút** (`CategoryTableRows.cs:45-46` cho nền dòng, `143-144` cho icon xóa): giữ `PointerEntered/PointerExited`, nhưng trong handler gọi fluent với token thay cho gán `Brush`:
  - nền dòng: `grid.PointerEntered += (_, _) => grid.Background(Theme(AppColors.Subtle));` và `grid.PointerExited += (_, _) => grid.Background(Theme(normalKey));`, với `normalKey = row.IsRoot ? AppColors.Surface : AppColors.Canvas`;
  - icon xóa: `Icon("delete", AppColors.DangerText, ...)` khi vào, `Icon("delete", AppColors.SecondaryText, ...)` khi ra.
- **Màu trong model:**
  - `CategoryIconResolver` trả `(string Symbol, StatusTone Tone)` thay cho 3 mã hex. Bảng chọn tone: `phone` → `Info`, `laptop` → `Accent`, `tablet` → `Success`, `headphones` → `Warning`, mặc định → `Neutral`.
  - `StockStatusPresentation` trả `StatusTone` thay cho 2 mã hex: `InStock` → `Success`, `LowStock` → `Warning`, `OutOfStock` → `Danger`.
  - View lấy key qua `StatusBadgePresentation.GetKeys(tone)`.
- **Font:** dùng font chung Inter qua `AppTypography`. Xóa `Assets/Fonts/PlusJakartaSans*`, `OFL.txt` và phần Plus Jakarta trong `Assets/Fonts/README.md`, sau khi grep không còn tham chiếu. Thay đổi này làm chữ khác thiết kế HTML của Dev 3 một chút, nhưng đổi lại cả app thống nhất một font (phải báo Dev 3).
- **`DangerText` (Light)** trả về `#B91C1C`. Thêm test contrast trong `DesignTokenKeysTests`: với cả `Light` và `Dark`, mỗi key chữ (`Text`, `SecondaryText`, `NeutralText`, `InfoText`, `SuccessText`, `WarningText`, `DangerText`, `AccentText`) phải đạt contrast ≥ 4.5 so với `Surface`. Tính theo công thức relative luminance của WCAG 2.x. Mình đã tính trước ngày 2026-10-25: chỉ `Light[DangerText] = #EF4444` trượt (3.76:1). Sau khi đổi về `#B91C1C` (6.47:1), cả 16 cặp đều ≥ 4.5 (thấp nhất là `Light[WarningText]` 5.02). Vì vậy **không cần chỉnh token nào khác**. Nếu test vẫn đỏ ở key khác thì dừng lại và báo, không tự đổi màu.
- **Phương án đã loại:** giữ helper `Brush(hex)` và thêm một bảng hex cho Dark. Cách này lặp lại hệ token, và màu vẫn không đổi khi người dùng chuyển theme lúc trang đang mở.

### D7. Backend: dịch lỗi constraint, read model, đúng repository

- **`CheckConstraintViolationException`**
  - Tạo file `backend/Common/Data/CheckConstraintViolationException.cs`, cùng mẫu với `ForeignKeyViolationException`.
  - `UnitOfWork.SaveChangesAsync` thêm `catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23514" } pgEx)` rồi ném exception mới.
  - `InventoryService` bắt `CheckConstraintViolationException` và bỏ hai `using` EF/Npgsql.
- **Read model:**
  - Tạo `backend/Inventory/Repositories/InventoryStockView.cs` với `public sealed record InventoryStockView(InventoryStock Stock, int SerialCount);`.
  - `IInventoryStockRepository.SearchStocksAsync` trả `(IReadOnlyList<InventoryStockView> Items, int TotalCount)`. `GetDetailAsync` trả `InventoryStockView?`.
  - `MapToDto`/`MapToDetailDto` nhận `InventoryStockView` và đặt `TotalSerialsCount = view.SerialCount`.
  - Xóa `RelatedSerialCount` khỏi entity cùng `using System.ComponentModel.DataAnnotations.Schema`.
  - Riêng nhánh tạo tồn kho ban đầu (`InitializeStockAsync`) thì map với `SerialCount = 0`.
- **Serial thuộc `ISerialImeiRepository`:**
  - Chuyển `GetSerialsForUpdateAsync(productId, variantId)` thành `ISerialImeiRepository.GetByStockKeyForUpdateAsync(Guid productId, Guid? variantId, CancellationToken)`. Thân hàm giữ nguyên hai câu `FromSqlInterpolated ... FOR UPDATE`.
  - Chuyển `DeleteSerials` thành `ISerialImeiRepository.RemoveRange(IEnumerable<SerialImei>)`.
  - `InventoryService` nhận thêm `ISerialImeiRepository serialRepository`.
- **Escape LIKE:** cả hai repository Inventory thêm `private static string EscapeLikePattern(string value) => value.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");` và gọi `EF.Functions.ILike(col, pattern, "!")` với `pattern = $"%{EscapeLikePattern(search)}%"`. Theo đúng `CustomerRepository`.
- **Lọc theo ID danh mục:**
  - `InventoryStockFilterRequest` và `InventoryStockSearchCriteria` đổi `string? CategoryName` thành `Guid? CategoryId`.
  - Repository lọc `s.Product.CategoryId == criteria.CategoryId`.
  - `InventoryStockDto` và `InventoryStockDetailDto` thêm `public Guid? CategoryId { get; set; }`, map từ `stock.Product?.CategoryId`.
- **Cây danh mục bằng projection:**
  - `ICategoryRepository.GetActiveCategoriesAsync` đổi tên thành `GetAllAsync` và trả `IReadOnlyList<CategoryTreeRow>` với `public sealed record CategoryTreeRow(Guid Id, string Name, string Slug, string? Description, Guid? ParentId, bool IsActive, int ProductCount, int SkuCount);`.
  - Truy vấn: `dbContext.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new CategoryTreeRow(c.Id, c.Name, c.Slug, c.Description, c.ParentId, c.IsActive, c.Products.Count(), c.Products.Sum(p => p.Variants.Count() == 0 ? 1 : p.Variants.Count())))`.
  - `CategoryService.GetTreeAsync` đọc `row.ProductCount`/`row.SkuCount`. Bỏ `Include`.
  - Kiểm tra SQL Npgsql dịch được bằng test Postgres hiện có (`CategoryPostgresTests`, khi có `TECHSTORE_TEST_CONNECTION`). Nếu `Sum` lồng `Count` không dịch được thì tách hai truy vấn group-by (đếm sản phẩm theo `CategoryId`, đếm biến thể theo `Product.CategoryId`), rồi ghép trong bộ nhớ.
- **`RestoreCategory`:** xóa hàm và các biến `original*`. Trong các nhánh `catch`, chỉ cần `RollbackAsync`.
- **`TimeProvider`:**
  - `ProductService`, `ProductImageService`, `CategoryService`, `InventoryService` thêm tham số cuối của primary constructor `TimeProvider? timeProvider = null`, cùng trường `private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;`.
  - Thay `DateTime.UtcNow` bằng `_time.GetUtcNow().UtcDateTime`.
  - Tham số tùy chọn giúp khoảng 23 chỗ `new XxxService(...)` trong test không phải sửa. DI vẫn truyền `TimeProvider.System` đã đăng ký.
  - **Phương án đã loại:** tham số bắt buộc như `CustomerService`. Cách này phải sửa mọi fixture, mà không thêm giá trị gì cho change này.

### D8. Thư mục theo cấu trúc nhóm, làm sau cùng

Chuyển file bằng `git mv`, đổi namespace bằng sed, sau khi mọi nhóm task khác đã xanh. Như vậy các task trước ghi đường dẫn cũ vẫn đúng.

| Từ | Sang | Namespace mới |
|:--|:--|:--|
| `Features/Categories/Pages/*.cs` | `Views/Inventory/` | `TechStore.Client.Views.Inventory` |
| `Features/Categories/Dialogs/*.cs` | `Views/Inventory/Dialogs/` | `TechStore.Client.Views.Inventory.Dialogs` |
| `Features/Inventory/Pages/*.cs` | `Views/Inventory/` | `TechStore.Client.Views.Inventory` |
| `Features/Inventory/Dialogs/*.cs` | `Views/Inventory/Dialogs/` | `TechStore.Client.Views.Inventory.Dialogs` |
| `Features/Categories/ViewModels/*.cs` | `ViewModels/Inventory/` | `TechStore.Client.ViewModels.Inventory` |
| `Features/Inventory/ViewModels/*.cs` | `ViewModels/Inventory/` | `TechStore.Client.ViewModels.Inventory` |
| `Features/Categories/Models/*.cs` | `Models/Inventory/` | `TechStore.Client.Models.Inventory` |
| `Features/Inventory/Models/*.cs` | `Models/Inventory/` | `TechStore.Client.Models.Inventory` |
| `Features/Categories/Services/*.cs` | `Services/Inventory/` | `TechStore.Client.Services.Inventory` |
| `Features/Inventory/Services/*.cs` | `Services/Inventory/` | `TechStore.Client.Services.Inventory` |

- Tên thư mục là `Inventory` vì route là `ShellDestination.Inventory` ("Danh mục và Kho").
- `CategoryWorkspaceStyles.cs` đổi tên thành `Views/Inventory/WorkspaceParts.cs` (lớp `WorkspaceParts`).
- `CategoryNode` đi vào `Models/Catalog/CategoryNode.cs` (namespace `TechStore.Client.Models.Catalog`), vì repository Catalog trả kiểu này.
- Trước khi chuyển, chạy `git ls-files frontend/TechStore.Client/Features` để kiểm tra không có hai file trùng tên ở cùng thư mục đích.

### D9. Thứ tự làm và commit

Thứ tự nhóm 0 → 10 như trong `tasks.md` (D10 và D11 nằm trong nhóm 3–4). Nhóm 1 (Catalog) độc lập và làm trước. Nhóm 2–3 là backend. Nhóm 4 đổi `categoryName` thành `categoryId` ở cả shared, backend và client trong cùng một commit, để build không bị gãy giữa chừng. Nhóm 5–8 là client: repository, ViewModel, rồi theme. Nhóm 9 chuyển thư mục và cập nhật AGENTS.md. Nhóm 10 chỉ kiểm tra tích hợp. Mỗi nhóm tự mang test và tài liệu của nó, và kết thúc bằng một commit theo Conventional Commits như lịch sử repo.

### D10. Manager không được xóa tồn kho

- `InventoryController.DeleteStock` đổi `[Authorize(Roles = "Admin,Manager,WarehouseStaff")]` thành `[Authorize(Roles = "Admin,WarehouseStaff")]`, để mọi thao tác ghi kho (khởi tạo, nhập serial, điều chỉnh, xóa) có cùng một tập role.
- Lý do: ba spec hiện có (`inventory-management` cho khởi tạo và nhập, `inventory-stock-detail` cho điều chỉnh) đều loại Manager. Endpoint xóa là chỗ duy nhất lệch, và không spec nào ghi quyền của nó. Manager phụ trách sản phẩm, giá, cấu hình; số lượng thực tế thuộc thủ kho.
- Spec `inventory-management` thêm requirement "Stock deletion API", ghi lại các quy tắc 409 đã có trong `InventoryService.DeleteStockAsync` cùng quyền mới.
- **Phương án đã loại:** cho Manager thêm quyền điều chỉnh, khởi tạo và nhập. Việc này mở rộng quyền ghi kho, cần cả nhóm quyết định, và không phải sửa một chỗ lệch.

### D11. Ba trạng thái tồn không chồng nhau, theo `MinStockAlert`

Gọi `avail = Quantity - ReservedQuantity` và `min = MinStockAlert`:

| Trạng thái | Điều kiện | Tham số API |
|:--|:--|:--|
| Hết hàng | `avail <= 0` | `outOfStock=true` |
| Sắp hết hàng | `0 < avail <= min` | `lowStock=true` |
| Còn hàng | `avail > min` | `inStock=true` |

- **Backend:** `InventoryStockRepository.SearchStocksAsync` đổi điều kiện `lowStock` từ `s.Quantity - s.ReservedQuantity <= s.MinStockAlert` thành `s.Quantity - s.ReservedQuantity > 0 && s.Quantity - s.ReservedQuantity <= s.MinStockAlert`. `InventoryStockDto.IsLowStock` đổi theo cho khớp: `AvailableQuantity > 0 && AvailableQuantity <= MinStockAlert`.
- **Client:** `InventoryStockPresentationRules.StatusFor` dùng đúng bảng này với `MinStockAlert` của từng dòng, **không** thay giá trị 0 bằng 5 (sửa ở D12: bản đầu có ngưỡng thay thế 5, làm badge lệch với bộ lọc server khi `min = 0`). Vì server đã lọc đúng, `InventoryStockViewModel` **không ẩn bớt dòng theo trạng thái ở client**, để số dòng, tổng số và số trang khớp metadata server (D12).
- **Spec:** `inventory-stock-workspace` sửa "Stock status presentation" (bỏ con số 5 cố định); `inventory-management` sửa scenario `lowStock` và thêm scenario "không chồng nhau".
- **Dashboard** (`AnalyticsRepository.cs:40`, `Quantity <= MinStockAlert`) mang nghĩa "cần nhập thêm" và là code của Dev 6, nên không đổi ở đây. Ghi vào evidence để báo Dev 6.
- **Phương án đã loại:** giữ `lowStock` gồm cả dòng hết hàng, rồi để client lọc tiếp. Cách này chính là nguyên nhân gây sai tổng số và thiếu dòng trên trang.

### D12. Bộ lọc tồn kho luôn do server lọc (bổ sung sau review ngày 2026-10-25)

Review sau khi làm xong nhóm 0–10 thấy `InventoryStockViewModel` vẫn lọc nửa client nửa server (code gốc của Feature 3):

- đổi bộ lọc chỉ gọi lại server khi `TotalPages > 1` (`ReloadServerFilterIfPaged`). Lọc xong còn 1 trang rồi bỏ bộ lọc thì không gọi lại, danh sách kẹt ở tập đã lọc;
- đổi bộ lọc không đưa `Page` về 1;
- dữ liệu chỉ có 1 trang thì chọn "Sắp hết" không gửi `lowStock=true`, tiêu đề vẫn ghi tổng chưa lọc;
- mỗi lần tải đều `CategoryOptions.Clear()` rồi gọi lại `GetTreeAsync`, thêm một request mỗi lần đổi trang và có thể làm ComboBox mất lựa chọn.

Quyết định, theo mẫu `SerialImeiViewModel.ReloadAfterFilterChanged` đã có:

- `OnSearchTextChanged`, `OnStockStatusFilterChanged`, `OnSelectedCategoryIdChanged` gọi chung `ReloadAfterFilterChanged()`: nếu `HasLoaded || IsLoading` thì `Page = 1` và `_ = LoadCommand.ExecuteAsync(null)` (cả ở `InventoryStockViewModel` và `SerialImeiViewModel`, để đổi bộ lọc trong lúc lần tải đầu đang chạy vẫn tự hủy request cũ và tải lại). `LoadAsync` đã tự hủy request cũ qua `CancelLoading()`, nên không cần debounce.
- Bỏ `ProjectVisibleItems` lọc client, `_serverFilterApplied`, `_serverStatusFilterApplied`, `ReloadServerFilterIfPaged`. Sau khi tải, `VisibleItems` = đúng các dòng server trả, `FilteredCount` = số dòng đó. `FooterText` tính theo `Page`, `PageSize`, `TotalCount` như `SerialImeiViewModel.FooterText`.
- `SelectedWarehouse` chỉ có một giá trị, `OnSelectedWarehouseChanged` không làm gì.
- Cây danh mục tải một lần: chỉ gọi `GetTreeAsync` khi chưa tải thành công (`_categoriesLoaded == false`). Không `Clear()` `CategoryOptions` khi đã có dữ liệu.
- `StatusFor(int availableQuantity, int minStockAlert)` bỏ giá trị mặc định 5. `InventoryStockRow.StockStatus` truyền thẳng `Dto.MinStockAlert`.

**Các sửa nhỏ đi kèm (cùng review):**

- **Làm mới trang từ View:** `CategoryManagementPage` gọi `ViewModel.ActivateAsync()` sau khi dialog thành công, vô tình đặt lại `_windowLifetime` thành `CancellationToken.None`. Thêm `public Task RefreshAsync() => ReloadAsync();` vào `CategoryManagementViewModel`, page gọi hàm này.
- **Đổi trạng thái danh mục:** `ToggleStatusAsync` truyền `ActivationToken` cho `UpdateAsync`, bỏ qua **mọi** `OperationCanceledException`, không đặt điều kiện `when (ActivationToken.IsCancellationRequested)`: sau `Deactivate()`, `_activation` thành null nên `ActivationToken` trả về `_windowLifetime` vốn không bị hủy, điều kiện đó sẽ để lỗi lọt ra và `AsyncRelayCommand` ném lại lên luồng UI (app không có handler `UnhandledException`). `ReloadAsync()` sau khi trang đã đóng cũng ném `OperationCanceledException` và được bỏ qua theo cách này. Khi lỗi API thì **không** chuyển cả trang sang `Error` (bảng biến mất), mà đặt `[ObservableProperty] string _statusUpdateError` và hiện một dòng chữ `DangerText` phía trên bảng; lần đổi trạng thái thành công kế tiếp hoặc lần tải lại sẽ xóa nó.
- **Nút "Xem Serial" trong dialog chi tiết tồn kho:** `RequestSerials` raise `SerialsRequested`, dialog `Hide()` giống `AdjustmentRequested`; page ghi nhận cờ, sau khi dialog đóng gọi `ViewModel.ShowSerialsFor(row)`. Hàm này chuyển `SelectedTab = SerialImei` và gọi `Serials.FilterByStock(row)` để lọc theo đúng `ProductId`/`VariantId` của dòng tồn kho (thay vì `SearchText` theo tên dễ lẫn sản phẩm cùng tiền tố), kèm thanh trạng thái `"Đang xem serial của: ..."` và nút `"Bỏ lọc"` (`ClearProductFilterCommand`) trên `SerialImeiPage`.
- **Tên cũ còn sót:** trường/tham số `apiClient`, `_apiClient`, `_inventoryApiClient`, `inventoryApiClient` có kiểu `IInventoryRepository` hoặc `ICategoryWorkspaceRepository` đổi thành `inventoryRepository`/`categoryRepository` (và `_inventoryRepository`/`_categoryRepository`). File `Repositories/Catalog/CategoryWorkspaceFailure.cs` đổi tên thành `CategoryApiException.cs` (giữ tên kiểu). Namespace test `TechStore.Frontend.UnitTests.Features.Categories` đổi thành `TechStore.Client.UnitTests.ViewModels.Inventory`. Bỏ `using` trùng namespace của chính file.
- **AGENTS.md:** mục Unit of Work thêm dòng 23514 → `CheckConstraintViolationException`; danh sách ngoại lệ D5 thêm các event báo kết quả dialog (`CategoryCreated`, `ImportSucceeded`, `AdjustmentSucceeded`, `DeleteSucceeded`, `SerialsRequested`) và `SizeChanged` chỉ dùng để co giãn bố cục.

**Để lại backlog (không sửa ở change này):** `CategoryManagementPage` nhận `IInventoryRepository`/`IProductRepository` và tự tạo ViewModel cho dialog, `CategoryManagementViewModel.Categories` để lộ repository cho View. Muốn sửa phải chuyển việc tạo dialog sang `IDialogService` hoặc factory, đụng toàn bộ luồng dialog Feature 3.

### D13. Xuất báo cáo tồn kho: toàn bộ dòng theo bộ lọc, hộp thoại Lưu file (bổ sung ngày 2026-10-26)

**Hiện trạng (code Feature 3):**

- `CsvInventoryReportExporter` (`Services/Inventory/InventoryReportExporter.cs`) tự ghi vào `~/Downloads/inventory-report-yyyyMMdd-HHmmss.csv`, không hỏi người dùng. Toast chỉ ghi tên file nên người dùng không biết file nằm đâu.
- `InventoryStockViewModel.ExportReportAsync` xuất `VisibleItems`, tức **chỉ trang đang xem** (tối đa 20 dòng).
- Khi lỗi, code đặt `ErrorMessage = exception.Message`. Vì `HasError` quyết định hiện panel lỗi, **bảng tồn kho bị thay bằng màn lỗi** chỉ vì xuất file thất bại.
- Toast luôn có tiêu đề cố định "Thành công" và icon dấu tích xanh, kể cả khi nội dung là "Không thể xuất báo cáo...".
- `await ShowSuccessToastAsync(...)` chờ hết 5 giây toast bên trong command, nên nút "Xuất báo cáo" bị khóa thêm 5 giây sau khi xong.

**Đã kiểm tra khả năng của nền tảng:** Uno 6.7.135 `Uno.UI.Runtime.Skia.X11.dll` có `LinuxFilePickerExtension.PickSaveFileAsync` dùng portal `org.freedesktop.portal.FileChooser` (cần portal phiên bản 3 trở lên, có hỗ trợ `current_name` cho tên gợi ý). Máy dev trả `busctl --user get-property org.freedesktop.portal.Desktop /org/freedesktop/portal/desktop org.freedesktop.portal.FileChooser version` = `u 3`. Skia Win32 và WinUI native cũng có `FileSavePicker`. Máy không có portal thì picker ném lỗi, nên vẫn cần đường dự phòng.

**Quyết định:**

1. **Cổng lưu file, theo mẫu `IImageFilePicker`:**
   - `Services/Files/ICsvFileSaver.cs` (thuần C#, đã được project test link qua `Services\Files\**`):
     ```csharp
     namespace TechStore.Client.Services.Files;

     public interface ICsvFileSaver
     {
         /// <summary>Opens the save dialog with a suggested name; returns null when the user cancels.
         /// When the dialog cannot be shown, writes to the Downloads folder and reports UsedFallbackFolder.</summary>
         Task<CsvSaveResult?> SaveAsync(string suggestedFileName, string content, CancellationToken cancellationToken = default);
     }

     public sealed record CsvSaveResult(string Path, bool UsedFallbackFolder);
     ```
   - `Services/Files/CsvFileWriter.cs` (thuần C#, test được): `public static class CsvFileWriter` với
     - `public static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);` (Excel đọc đúng tiếng Việt);
     - `public static async Task<string> WriteToFolderAsync(string directory, string fileName, string content, CancellationToken ct)`: `Directory.CreateDirectory(directory)`; nếu `fileName` đã tồn tại thì thêm hậu tố `-2`, `-3`... trước `.csv` cho tới khi chưa tồn tại; ghi bằng `File.WriteAllTextAsync(path, content, Utf8WithBom, ct)`; trả về đường dẫn đầy đủ;
     - `public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");`
   - `Views/Shell/CsvFileSaver.cs` (WinUI, không link vào test), cùng chỗ với `ImageFilePicker.cs`, có `public Window? Host { get; set; }`:
     ```csharp
     var picker = new FileSavePicker
     {
         SuggestedStartLocation = PickerLocationId.Downloads,
         SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
         DefaultFileExtension = ".csv"
     };
     picker.FileTypeChoices.Add("CSV (UTF-8)", new List<string> { ".csv" });
     #if WINDOWS
     if (Host == null) throw new InvalidOperationException("Không mở được hộp lưu file.");
     WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(Host));
     #endif
     ```
     - gọi `await picker.PickSaveFileAsync()` trong `try`; `null` → trả `null` (người dùng hủy);
     - picker ném lỗi (trừ `OperationCanceledException` của `cancellationToken`) → dự phòng: `var path = await CsvFileWriter.WriteToFolderAsync(CsvFileWriter.DefaultFolder, suggestedFileName, content, ct); return new(path, UsedFallbackFolder: true);`
     - có file → ghi nội dung: `var bytes = CsvFileWriter.Utf8WithBom.GetPreamble().Concat(CsvFileWriter.Utf8WithBom.GetBytes(content)).ToArray();` thử `await FileIO.WriteBytesAsync(file, bytes)`; nếu `NotImplementedException` thì `await File.WriteAllBytesAsync(file.Path, bytes, ct)` (giống nhánh dự phòng trong `ImageFilePicker`). Trả `new(string.IsNullOrEmpty(file.Path) ? file.Name : file.Path, false)`.
   - DI (`UiServiceCollectionExtensions.cs`): `services.AddSingleton<CsvFileSaver>(); services.AddSingleton<ICsvFileSaver>(p => p.GetRequiredService<CsvFileSaver>());`, đặt cạnh `ImageFilePicker`. `App.xaml.cs` ngay dưới dòng `GetRequiredService<ImageFilePicker>().Host = MainWindow;` thêm `GetRequiredService<CsvFileSaver>().Host = MainWindow;`.
   - **Xóa** `IInventoryReportExporter`, `InventoryReportExportResult`, `CsvInventoryReportExporter` và dòng đăng ký DI của chúng. **Giữ** `InventoryReportBuilder.BuildCsv` (cột và cách escape không đổi), thêm `public static string SuggestedFileName(DateTime localNow) => $"bao-cao-ton-kho-{localNow:yyyyMMdd-HHmmss}.csv";`.

2. **Xuất toàn bộ dòng theo bộ lọc hiện tại, không chỉ trang đang xem:**
   - Tách hàm `private InventoryStockFilterRequest BuildFilterRequest(int page, int pageSize)` trong `InventoryStockViewModel`, dùng chung cho `LoadAsync` và xuất báo cáo, để bộ lọc của hai nơi không lệch nhau (search, `CategoryId` khi `IsCategoryFilterAvailable`, `LowStock`/`OutOfStock`/`InStock`).
   - Xuất: lặp `page = 1, 2, ...` với `pageSize = 100` (giới hạn tối đa của `InventoryService.GetStocksAsync`), cộng dồn `Items` thành `InventoryStockRow`, dừng khi `page >= result.TotalPages` hoặc `Items` rỗng. Thêm chặn an toàn tối đa 200 trang (20.000 dòng).
   - Lấy dữ liệu **trước**, mở hộp thoại **sau**, vì `ICsvFileSaver` cần nội dung. Không có dòng nào thì không mở hộp thoại.

3. **Trạng thái và thông báo:**
   - Trong lúc xuất: `IsExporting = true`, nút đổi chữ thành "Đang xuất..." (`ExportButtonText`) và bị khóa (`CanExportReport`). Bỏ toast "Đang tạo báo cáo tồn kho...".
   - Toast có loại: thêm `public enum ToastKind { Success, Error }` (đặt trong `Models/Inventory/InventoryStockModels.cs`), `[ObservableProperty] ToastKind _toastKind`, thuộc tính `ToastTitle` (`Success` → "Thành công", `Error` → "Không thành công"), `IsErrorToast`, `IsSuccessToastKind`. Thêm `public Task ShowErrorToastAsync(string message, CancellationToken ct = default)`; `ShowSuccessToastAsync` giữ chữ ký, đặt `ToastKind = Success`. Cả hai dùng chung một hàm riêng `ShowToastAsync(ToastKind kind, string message, CancellationToken ct)`, chứa thân hiện tại của `ShowSuccessToastAsync`.
   - Page (`CategoryManagementPage.BuildInventorySuccessToast`): tiêu đề bind `ViewModel.Inventory.ToastTitle` thay cho chữ cố định "Thành công"; có hai `Border` icon chồng nhau trong cột 0: icon `check_circle` trên `SuccessFill` (hiện khi `IsSuccessToastKind`), icon `warning` màu `DangerText` trên `DangerFill` (hiện khi `IsErrorToast`); viền toast đổi từ `SuccessStroke` sang `Stroke` cho hợp cả hai loại.
   - Kết quả:

     | Tình huống | Thông báo |
     |:--|:--|
     | Không có dòng nào | Error: "Không có dữ liệu tồn kho phù hợp để xuất." (không mở hộp thoại) |
     | Người dùng bấm Hủy | Không có toast, không ghi file |
     | Lưu thành công | Success: "Đã xuất {n:N0} dòng tồn kho vào {đường dẫn đầy đủ}" |
     | Hộp thoại không mở được, đã ghi vào Tải xuống | Success: "Không mở được hộp lưu file nên đã lưu {n:N0} dòng vào {đường dẫn đầy đủ}" |
     | Lỗi API hoặc lỗi ghi file | Error: "Không thể xuất báo cáo tồn kho. Vui lòng thử lại." |

   - **Không** đặt `ErrorMessage` khi xuất lỗi, để bảng tồn kho vẫn hiện.
   - Không `await` toast bên trong command: dùng `_ = ShowSuccessToastAsync(...)` / `_ = ShowErrorToastAsync(...)` **sau** khi đã đặt `IsExporting = false`, để nút mở lại ngay.
   - Hủy: rời trang thì hủy việc xuất. Thêm `public void CancelExport() => ExportReportCommand.Cancel();` và gọi trong `CategoryManagementViewModel.OnDeactivated()` cạnh `Inventory.CancelLoading()`. `OperationCanceledException` khi token bị hủy thì bỏ qua, không toast.
   - Constructor: `InventoryStockViewModel(IInventoryRepository inventoryRepository, ICsvFileSaver? fileSaver = null, ICategoryRepository? categoryRepository = null)`. `fileSaver == null` (chỉ xảy ra trong test cũ) thì `CanExportReport` trả `false`. Các test hiện có truyền `categoryRepository:` theo tên nên không phải sửa.

**Phương án đã loại:**

- Giữ ghi thẳng vào Tải xuống, chỉ hiện đường dẫn đầy đủ: không cho người dùng chọn chỗ lưu và tên file như yêu cầu.
- Mở hộp thoại trước rồi mới tải dữ liệu: phải đổi `ICsvFileSaver` thành hai bước (chọn file, rồi ghi), phức tạp hơn mà không lợi bao nhiêu với vài nghìn dòng.
- Xuất bằng endpoint CSV phía server: thêm API mới, không cần cho quy mô hiện tại.

## Risks / Trade-offs

- **[Rủi ro] Tranh code với Dev 3:** change sửa phần lớn file Feature 3. → Báo Dev 3 trước khi apply. Làm trên nhánh tạo từ `dev` sau khi nhánh Dev 3 đã merge, hoặc thống nhất để Dev 3 ngừng sửa các file này trong lúc làm.
- **[Rủi ro] Đổi màu và font làm UI khác thiết kế HTML của Dev 3.** → Giữ nguyên bố cục và kích thước, chỉ đổi nguồn màu/font. Chụp ảnh trước/sau ở cả Sáng và Tối để Dev 3 duyệt.
- **[Rủi ro] Theme reference áp lên phần tử tạo bằng object initializer có thể không cập nhật khi đổi theme**, nếu phần tử được tạo trước khi vào cây giao diện. → Task kiểm tra chạy app và đổi Sáng/Tối khi trang đang mở (AGENTS.md §6 quy tắc 5). Nếu chỗ nào không đổi, dùng lại mẫu `Loaded += ApplyThemeColors` của AGENTS.md §6 (mục Shared controls) cho đúng phần tử đó.
- **[Rủi ro] Projection `Sum(Count)` không dịch được SQL trên Npgsql.** → Phương án dự phòng là hai truy vấn group-by (D7). Test fake không bắt được lỗi này, nên phải chạy `CategoryPostgresTests` hoặc chạy API thật.
- **[Đánh đổi] `categoryName` bị đổi thành `categoryId` (breaking).** → Chỉ có client trong repo dùng tham số này. Đổi cả hai phía trong cùng commit nhóm 3+5, hoặc giữ backend nhận cả hai tạm thời. Không cần, vì không có client bên ngoài.
- **[Đánh đổi] Repository Inventory vẫn trả DTO** (D3). → Chấp nhận theo tiền lệ Identity. Ghi vào AGENTS.md là việc về sau.
- **[Rủi ro] Chuyển thư mục làm hỏng link trong project test.** → Task 8 cập nhật `<Compile Include>` và chạy lại toàn bộ test.

## Migration Plan

Không có dữ liệu cần chuyển. Không có migration EF. Triển khai bằng cách merge nhánh. Muốn rollback thì revert các commit theo thứ tự ngược. Commit nhóm 3 (DTO `categoryId`) và nhóm 5 (client gửi `categoryId`) phải revert cùng nhau.

## Open Questions

- (không còn). Hai câu hỏi trước đây (quyền xóa của Manager, ngưỡng sắp hết) đã được chốt ở D10 và D11 ngày 2026-10-25.
