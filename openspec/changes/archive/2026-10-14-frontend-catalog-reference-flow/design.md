# Design

## Context

Xem `proposal.md` cho động cơ và phạm vi. Nguồn kiến trúc ưu tiên là `docs/MVVM_REPOSITORY_BASE_PROJECT.md`, mục 6–11; Change 4 trong `docs/FRONTEND_FOUNDATION_PLAN.md` là kế hoạch feature mẫu. Đã đọc đầy đủ `openspec/specs/product-catalog/spec.md`, DTO/request Catalog và implementation API hiện tại.

Các điểm quan sát từ repository:

- `ProductListViewModel : PageViewModel` hiện rỗng; trang Sản phẩm là placeholder. Chưa có frontend ProductRepository.
- `IApiClient.GetAsync<T>` đã xử lý URL tương đối, HTTP/JSON, cancellation, timeout và `ApiException`; Bearer/session dùng chung. Deserializer DTO hiện không phát hiện mọi missing field vì DTO có default initializer.
- `INavigationService.Navigate(ShellDestination)` và `ShellRoutePageFactory` chỉ hiểu sáu root destinations, chưa có productId. Scope cấp theo activation.
- `PageViewModel.ReloadAsync` gộp yêu cầu khi lần tải cũ đang chạy; generation hiện chỉ bảo vệ trạng thái ở base, không tự bảo vệ assignments trong derived VM. `PageActivationLease` đang chờ task initial activation; các query/retry sau đó cũng phải được tính vào lifetime để không dispose dependency sớm.
- `GET /api/products` nhận `ProductSearchRequest`; trả `PagedResultDto<ProductSummaryDto>`. Detail là `ProductDto` chứa `ProductVariantDto`; specs/effectiveSpecs dùng `Dictionary<string, JsonElement>`. List không có stock hoặc variant màu. Tree categories chỉ có danh mục active, không có counts; không có brand facet endpoint.
- ProductsController hiện không có `[Authorize]`; Program hiện không cấu hình authentication middleware. Không giả login để gọi API và không diễn giải trạng thái này thành contract auth mới. 401/403 vẫn phải được test ở client.
- Detail controller trả `NotFound()`; client dựa HTTP 404, không phụ thuộc body luôn là ProblemDetails. Category service hiện có thể đưa node active không có parent trong tập active lên root, khác mô tả loại cả nhánh inactive của category spec; client dùng cây nhận được, ghi drift nếu gặp ở integration, không sửa backend trong change này.
- API Development auto-migrate/seed; DataSeeder có năm sản phẩm và ba variants, không phải tám card/128 products như mẫu. Integration phải kiểm tra dataset có thật; không mặc định seed luôn chạy lại.
- Đã xem `screen.png` và `code.html` của grid mẫu: sidebar, header/count, hàng filters, category pills/sort, card ảnh vuông, SKU/brand, giá, swatches/stock và paging. Screenshot và HTML có khác biệt giá hiển thị; dữ liệu API quyết định nội dung, tokens/design system đã hoàn thành quyết định style.

## Goals / Non-Goals

**Goals:** một feature mẫu có ranh giới View → ViewModel → Repository → IApiClient; contract/mapping có kiểm tra; thao tác nhanh không gây stale state; code thuần C# test headless; các đường query/detail/back có ownership rõ.

**Non-Goals:** CRUD, nhập/xuất dữ liệu, Inventory, login/refresh, API/backend/shared DTO changes, offline cache, lưu lựa chọn qua process, một framework navigation/query tổng quát. Không đổi target frameworks/packages hiện tại chỉ để làm màn hình. Nghiệm thu của change này chỉ Linux/Uno Skia, không có Windows runtime/Contrast/WinApp CLI acceptance gate.

## Decisions

### 1. Repository và Model độc lập UI

- Thêm `Models/Catalog` với `ProductQuery`, `ProductPage`, `ProductSummary`, `ProductDetail`, `ProductVariant`, category option và typed spec value nếu cần. Giá dùng decimal; metadata giữ đủ page/pageSize/totalCount/totalPages. Data Model không chứa visibility/brush/command.
- `Repositories/Catalog/IProductRepository` cạnh `ProductRepository`: `SearchAsync(ProductQuery, token)` và `GetByIdAsync(Guid, token)`; detail 404 trả null, mọi lỗi khác giữ `ApiException`. Không thêm `ProductApiClient` chỉ chuyển tiếp và không generic CRUD repository.
- `ICategoryRepository`/`CategoryRepository` tối thiểu chỉ lấy tree rồi map thành lựa chọn có id + display path. Đây là logic mapping có giá trị, không lớp service trung gian. Không suy category từ list hiện tại; category lookup lỗi độc lập với list.
- Repository dùng `IApiClient.GetAsync<JsonElement>` để kiểm tra presence/type ở boundary trước khi deserialize thành shared DTO và map Model. DTO mặc định false/0/[] không đủ để phân biệt field thiếu. Validated JSON phải có metadata/list/detail required fields; nullable fields hợp lệ được chấp nhận, unknown fields được bỏ qua. Kiểm tra Guid khác Empty, list metadata hợp lý, giá/count không âm, priceFrom ≤ priceTo và id detail đúng id yêu cầu. Không reject trang vượt totalPages nếu items rỗng theo contract. Copy/clone JsonElement khi giữ qua lifetime; giữ nguyên effectiveSpecs server, không merge lại.
- Specs chỉ nhận string/number/boolean theo Catalog contract; null/nested/array là lỗi Catalog mặc dù transport tổng quát có thể đọc JSON null hợp lệ ở field khác. Validate invariant field presence ở fixture raw JSON tests; không sửa global DTO deserialization cho các feature khác.
- Repository/API/VM transient được resolve trong scope mỗi activation; singleton navigation giữ factory và route values, không giữ HTTP client/repository/VM. Đăng ký tại `ServiceCollectionExtensions`/`UiServiceCollectionExtensions` hiện có, không build service provider thứ hai.

Thay thế đã cân nhắc: VM dùng shared DTO trực tiếp đơn giản hơn nhưng không cung cấp ranh giới validation/typed spec/copy ownership cần thiết cho feature mẫu. Thêm API client đặc thù chỉ tăng lớp chuyển tiếp nên không chọn.

### 2. Query rõ ràng, chạy ở server

| UI | Query và policy |
| --- | --- |
| SearchBoxControl + nút Tìm kiếm/Enter | Trim q, bỏ khi rỗng; placeholder “Tên, SKU, barcode hoặc tên biến thể”, không hứa tìm IMEI |
| Thương hiệu | Ô nhập có nhãn, exact match case-insensitive; không danh sách hãng giả hoặc facets suy từ current page |
| Danh mục | Tree API flatten thành đường dẫn có phân cấp, lựa chọn Tất cả bỏ categoryId; chọn parent chỉ lọc trực tiếp; không counts |
| Trạng thái | Tất cả/Đang hoạt động/Ngừng hoạt động → omit/true/false isActive; không phải stock status |
| Giá | Hai ô min/max optional, parse theo culture UI rồi serialize decimal invariant, reject âm/đảo ngược |
| Specs nâng cao | Khu mở rộng key/value rows; giữ value nguyên nghĩa number/bool/string để server parse, escape mỗi repeated `spec=key:value`; key snake_case, value không rỗng, không trùng key mâu thuẫn |
| Sort | 6 lựa chọn name/price/createdAt × asc/desc; default createdAt:desc; bỏ stock sort |
| Paging | page=1, pageSize=20 default; selector 20/50/100; API vẫn có normalization 1..100; metadata response là nguồn sự thật |

Search/filter có DraftCriteria và AppliedCriteria. Chỉ submit hợp lệ mới bắt đầu truy vấn trang 1; clear q sẽ submit bộ lọc còn lại, Reset đưa mọi criteria về default. Sort/pageSize áp dụng ngay lên AppliedCriteria và reset page=1; paging dùng AppliedCriteria. Không debounce/live search trong change này. Một thao tác chỉ tạo một list request; category lookup một lần mỗi activation list, retry riêng; chỉ user mở detail mới gửi detail GET. Query builder percent-encode từng value, bool/integer/decimal invariant, không nối raw input vào URL.

Previous/Next disabled khi không có trang hợp lệ hoặc đang chuyển trang. Nếu dataset thay đổi làm page vượt cuối, hiện thông báo và nút Về trang 1 thay vì tự vòng lặp retry. TotalCount là tổng khớp bộ lọc, không phải toàn bộ kho; khi rỗng footer “0 kết quả”, không “1–0”.

### 3. State, cancellation và scope

Mở rộng nhỏ `PageViewModel`/`PageActivationLease` cho Catalog: activation lifetime token riêng với request token/generation; request mới supersede/cancel request cũ, cho request mới bắt đầu mà không đợi request cũ hợp tác. Tách fetch-result khỏi commit; kiểm tra generation + activation + session revision trước mọi cập nhật Items, metadata, error và finally/loading, không chỉ check token sau await.

Lease phải track mọi task list/detail/category/retry trong activation, cấm nhận work mới sau deactivate và chờ tất cả task còn sử dụng scope trước DisposeAsync đúng một lần. Navigation không chờ dispose hoàn tất. Các placeholder VM giữ hành vi cũ; thêm regression tests cho cả default activation và late non-cooperative loads.

- Initial load: Loading → Content/Empty/Error. Lỗi không thành empty.
- Subsequent request: giữ LastSuccessfulSnapshot cùng applied query/page cũ trong lúc tải; CurrentRequestedQuery riêng. Nếu lỗi network/timeout/400/5xx/invalid response, Error/InfoBar + snapshot có nhãn “Kết quả trước đó”; paging cũ không giả thuộc query mới. Retry chạy chính query thất bại; success mới thay snapshot nguyên khối.
- 400: map field errors phù hợp input khi nhận diện, còn lại message an toàn + trace id; không render raw server body. 401: báo cần xác thực, dùng session handler hiện có, không login/refresh. 403: báo không đủ quyền, giữ session. Với 401/403 xóa/ẩn nội dung truy cập bị từ chối; session replacement/clear hủy request và xóa snapshot/return state cũ, detach subscription khi scope kết thúc.
- Detail: Loading/Content/NotFound/Error; chuyển id xóa nội dung id cũ; 404 có Back; retry không đổi productId. Cancellation dự kiến không tạo lỗi.

Không dùng chỉ `AllowConcurrentExecutions=true` để giải quyết race. Skill `mvvm-toolkit` dùng cho observable/command và CanExecute; task-returning commands, không async void ngoài XAML events.

### 4. Navigation có tham số và Back

Dùng route value thuần C# biểu diễn root destination + Catalog view kind + productId + optional list return criteria/focus id. Mở rộng navigation state/preparer/factory/adapter hiện có để chuẩn bị trang trước commit; giữ overload Navigate(ShellDestination) cho sáu root. Equality phải gồm view kind/id, không chỉ Products; factory validation lỗi phải giữ route/title/content trước đó.

List → Detail thay content của Shell trong nhánh Products, không thêm menu. ProductDetailPage/ViewModel nhận Guid qua factory trước activation, constructor không I/O. Back tạo list activation mới nhận immutable AppliedCriteria/page và card id làm focus hint rồi tải API lại; không giữ VM, Scope hoặc toàn bộ items trong navigation singleton. Nếu card không còn trên trang, focus heading. Rời Products sang root khác xóa return context; chọn Products ở menu khi đang detail mở list default, cùng route hiện hành vẫn no-op. Không xây history stack tổng quát.

Tái sử dụng dialog service khi thật sự có thông báo modal; detail là page có Back, không nhồi cả detail UI vào ContentDialog và không bổ sung dialog abstraction mới.

### 5. UI và khác biệt mẫu/API

Ưu tiên `GridView` với typed DataTemplate/ItemsWrapGrid, virtualization và chiều cao được giới hạn bởi layout; không bọc thêm ScrollViewer quanh grid. Toolbar responsive, category lựa chọn qua controls chuẩn, có thể dùng selector/ComboBox theo available width. Dùng SearchBoxControl, EmptyStateControl, StatusBadge và theme tokens hiện có; `x:Bind` động phải OneWay/TwoWay thích hợp. Giữ font/icon assets nội bộ; không nạp font/icon từ Google/CDN như HTML mẫu.

| Thành phần mẫu | Quyết định thực hiện |
| --- | --- |
| Sidebar và header | Tái sử dụng Shell, không tạo sidebar/account giả thứ hai; title/count không bị lặp giữa Shell và content |
| 128 products và category counts | Chỉ totalCount response của applied query; category chỉ có tên, không counts và không cộng current-page items |
| Còn 12 / Hết hàng / Sắp hết, stock filter/sort | Không hiển thị và không suy từ isActive; dùng badge Đang hoạt động/Ngừng hoạt động đúng field |
| Swatches/các lựa chọn màu trên card | Bỏ; card chỉ dùng variantCount thật. Detail hiển thị specs màu dưới dạng text nếu API có key, không đoán mã màu hoặc thêm request |
| Giá Từ | priceFrom=priceTo: giá đơn; khác: khoảng giá. Format vi-VN/VND, giữ decimal. Không làm mất giá của các card chỉ vì screenshot không hiện đủ |
| Ảnh | imageUrl hợp lệ HTTP(S) tải bằng UI/image pipeline không mang bearer; URL tương đối chỉ resolve từ API base đã cấu hình; null/lỗi/scheme khác dùng placeholder nội bộ, không lấy ảnh sample gán cho sản phẩm thật |
| Add/Edit/Delete/more menu, list-view toggle | Bỏ CTA mutation và toggle mode chưa làm; card/Enter mở chi tiết; có Refresh thay cho action giả |
| Detail | Tên/SKU/brand/category, barcode, basePrice/costPrice, description, active/serial flags, dates và product specs; variants dùng danh sách read-only với SKU/barcode/price/status/specs/effectiveSpecs; null optional hiện “Chưa có”, variants=[] hiện không có biến thể |

Mục tiêu bố cục 4 cột ở viewport rộng khi đủ chỗ, giảm số cột theo content width để card khoảng 230 DIP vẫn đọc được. Ở 800×600 controls reflow/cuộn trong vùng hợp lý, mọi filter/paging/Back còn truy cập bằng keyboard; không ép bốn cột. Light/Dark/System và text scale 100/150/200%, tên dài, dấu tiếng Việt và ảnh lỗi nằm trong visual matrix.

`winui-design`, `uno-platform` được dùng để chọn controls/style và verify API trên restored refs Uno 6.7.30 cùng samples/reference local. Yêu cầu mới bỏ kiểm thử Windows áp dụng cho change này: không gate triển khai bằng Windows-only WinApp CLI. Tương thích controls được kiểm chứng bằng compile và runtime Skia, ghi nguồn reference; không mặc định các API của phiên bản Uno mới hơn. Không thêm package trừ khi thật sự cần và đã kiểm chứng/pin qua CPM; mặc định giữ nguyên dependency hiện tại.

### 6. Kiểm thử và bàn giao

- Unit tests tại `tests/Frontend.UnitTests`: source-link Models/Repositories và helpers thuần C#; fake IApiClient cho mapping, fake HttpMessageHandler + production ApiClient cho query encoding/contract/status, TaskCompletionSource cho state/race/cancellation/navigation/disposal. Không gọi API thật từ unit suite.
- Thêm `tests/Frontend.IntegrationTests` net10.0 dùng cùng production source-links và shared DTO, opt-in rõ ràng qua biến base URL. Khi opt-in mà API/dataset thiếu phải fail preflight rõ ràng, không skip giả pass. Không cần sửa Program để dùng WebApplicationFactory: chạy tiến trình API thật và PostgreSQL local/disposable bằng cấu hình hiện có. Suite chỉ GET, không thay seed/backend hoặc viết DB. Unit tests độc lập DB.
- Integration lấy ID/category/brand thực từ API, dùng pageSize nhỏ để đi nhiều trang với seed năm sản phẩm, so sánh totals, sort ties/stability khi dataset đứng yên, search SKU/variant/barcode, brand/category AND, price overlap, typed spec và detail effectiveSpecs. Kiểm tra trực tiếp 400 cho sort/spec/range sai, 404 cho Guid chưa tồn tại. Unicode/special query có thể trả rỗng hợp lệ; encode đúng được assert chặt ở fake-handler tests. Payload sai/network/401/403 dùng unit tests; API thật hiện không enforce auth nên không tuyên bố live auth pass.
- Chạy API bằng `dotnet run --project backend/TechStore.Api.csproj --launch-profile http`, kiểm tra URL thực và redirect/certificate, cấu hình client qua `TECHSTORE_API_BASE_URL`; không disable HTTPS certificate checks. API startup có thể log seed failure nhưng vẫn chạy, do đó phải kiểm tra GET và dataset chứ không dựa process sống.
- Chạy `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`, integration project riêng, build/run client `net10.0-desktop`. Ưu tiên Release; foundation trước từng gặp UNOB0019 (DevServer trong optimized Release). Nếu gặp lại, thử config/package condition phù hợp SDK trong phạm vi frontend; hoặc ghi chính xác `-p:Optimize=false` là ngoại lệ cấu hình được dùng, không gọi đó là optimized Release. Không thay version để né lỗi và không tick runtime task chỉ vì compile pass.
- Handoff `docs/FRONTEND_CATALOG_HANDOFF.md` ở apply: build hash/commit + dirty diff, OS/renderer, API origin không có secrets, dataset/preconditions, commands, logs, expected/actual, viewport DIP/RasterizationScale, ảnh Linux đối chiếu mẫu và giải thích các khác biệt đã chốt. Các kiểm tra chưa chạy ghi unverified; không tự ghi người dùng đã test.
- Fixture nếu cần visual edge cases chỉ ở tests/gallery được ghi nhãn và bật tường minh, tách khỏi route API thật. Handoff luôn có evidence API thật riêng; thiếu dữ liệu không cho phép mock fallback ở Catalog.
- Manual acceptance riêng, chỉ tick khi chủ dự án xác nhận build Linux Catalog cụ thể, ngày và kết quả/ngoại lệ. Không thừa kế nghiệm thu Shell trước đó. Không task kiểm thử Windows, không tuyên bố Windows đã verified cho Catalog.

## Risks / Trade-offs

- Shared DTO default che giấu field thiếu → kiểm tra JSON shape ở Catalog boundary, test legitimate zero/false/empty và additive fields.
- Scope bị dispose trong lúc query mới còn chạy → track mọi task activation và test non-cooperative cancellation; regression Shell/dialog.
- Mẫu có dữ liệu không tồn tại trong API → dùng bảng sai khác ở trên làm tiêu chí visual; ưu tiên trung thực thay vì bịa để khớp ảnh.
- Không brand facets, category tree chỉ active → input brand tự do, không hứa toàn bộ lựa chọn hay counts; lỗi lookup có retry độc lập.
- Dataset/HTTPS/migration có thể chặn integration → preflight cụ thể, báo lỗi backend riêng; không mở rộng scope sửa backend.
- Text scale/theme hỗ trợ có thể khác trên Skia → dùng kiểm tra runtime và ghi cách đặt text scale thực tế, không coi screenshot resize là kiểm tra text scale.

## Migration Plan

1. Bổ sung Models/Repositories và tests, sau đó lifecycle/route parameter extensions với regression tests.
2. Wire DI và thay placeholder Products; thêm read-only detail; không migration database và không đổi default Dashboard/gallery behavior.
3. Chạy unit + API integration, Linux build/run/visual và bàn giao evidence; chủ dự án nghiệm thu ở task riêng.
4. Rollback bằng cách phục hồi ProductList placeholder và wiring/navigation trước change; giữ foundation/design system đã có, không chạm database, không rollback archive cũ.
