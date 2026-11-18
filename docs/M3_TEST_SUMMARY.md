# M3 — Kết quả kiểm thử Sprint 5

## Build bàn giao của Bình

- Ngày chạy 18/11/2026. Windows `10.0.26200.0`, .NET SDK `10.0.404`, Debug và desktop `net10.0-desktop`.
- Baseline đã build là `b08abe03b9d04182c8ed95368b0345b08e4cd2b9` trên nhánh `feat/m3-manager-dashboard-access`, chứa `origin/dev` tại `6c5d8c567bc69deebeb41e9c99bbce9814cec9bc`. `git merge --no-edit origin/dev` báo `Already up to date`. Nhánh Bình chưa merge vào `dev`. Ref `dev` đã fetch chưa có controller receiving.
- Trạng thái công bố build là **chờ xác nhận push nhánh Bình**. Auto-review chặn thao tác ghi lên GitHub khi chưa có xác nhận trực tiếp. SHA nguồn và SHA-256 artifact bên dưới ghi kết quả kiểm thử cục bộ.
- SHA-256 build API `43D6A94FE0F55F8B0E29B7941B25285FAE538EB5FCA4579700054AFDDD198204`. Build desktop `82DBF943696FFF47DFD33A441920F164DB39E19628CB6DA74D6C3DE615BEBAEF`.

```powershell
dotnet build backend/TechStore.Api.csproj --no-restore
dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore "-p:RestoreSources=$localPackages"
```

`$localPackages` lấy từ `packageFolders` trong `frontend/TechStore.Client/obj/project.assets.json`. Cả hai lượt báo `Build succeeded. 0 Warning(s) 0 Error(s)`. Log phụ tại `backend/obj/m3-acceptance/build-api.log` và `build-desktop.log`.

Các lượt test dưới đây dùng cùng baseline API. Lượt backend có thêm lớp [M3AcceptancePostgresHttpTests](../tests/Backend.UnitTests/M3AcceptancePostgresHttpTests.cs) ở working tree lúc chạy. SHA-256 của `TechStore.UnitTests.dll` sau lần sửa payload là `A2FDCC73E6246EA872F21AC8D78B7D2E26C0EEB503E2CC3CF8E4E65DFD7A45CF`. Các fixture PostgreSQL dùng database test riêng. Catalog live trỏ Kestrel `http://127.0.0.1:5299/` và DB demo migrate/seed mới.

## Kết quả theo lượt chạy

Chạy từ gốc repo. Đặt `TECHSTORE_TEST_CONNECTION` trỏ PostgreSQL test có quyền tạo DB fixture. Đặt `TECHSTORE_SEED_TEST_POSTGRES` và `TECHSTORE_ANALYTICS_TEST_POSTGRES` trỏ hai database test riêng. Analytics sử dụng quyền reset database đã chỉ định. Cấu hình kết nối được giữ cục bộ.

```powershell
$env:TECHSTORE_SEED_TEST_ALLOW_SCHEMA = 'YES'
$env:TECHSTORE_ANALYTICS_ALLOW_DATABASE_RESET = 'YES'
$env:TECHSTORE_API_BASE_URL = 'http://127.0.0.1:5299/'
$env:Logging__EventLog__LogLevel__Default = 'None'
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --no-restore --filter 'FullyQualifiedName~M3AcceptancePostgresHttpTests|FullyQualifiedName~DashboardLiveFlowPostgresHttpTests|FullyQualifiedName~ProductSearchPostgresTests|FullyQualifiedName~CategoryPostgresTests|FullyQualifiedName~CustomerServiceTests|FullyQualifiedName~CustomerRepositoryPostgresTests|FullyQualifiedName~CustomerHistoryPostgresHttpTests' --logger 'console;verbosity=detailed'
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --no-build --no-restore --logger 'console;verbosity=normal'
dotnet test tests/Backend.IntegrationTests/TechStore.Backend.IntegrationTests.csproj --no-restore --logger 'console;verbosity=detailed'
dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj --no-restore --logger 'console;verbosity=normal'
dotnet test tests/Frontend.IntegrationTests/TechStore.Client.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~CatalogApiIntegrationTests' --logger 'console;verbosity=detailed'
dotnet test tests/Frontend.IntegrationTests/TechStore.Client.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~CatalogManagementApiIntegrationTests' --logger 'console;verbosity=detailed'
```

| Lượt (Bình) | Dòng tổng runner | Fail hoặc skip | Kết quả quan sát |
| --- | --- | --- | --- |
| Backend lọc lần đầu | `Total tests: 40, Passed: 39, Failed: 1, Skipped: 0` | `AdminCategory_TreeCrudAndValidation_PersistOnlyValidRequests` fail. | `Expected: OK, Actual: BadRequest` tại PUT của test. |
| Backend lọc chạy lại | `Total tests: 40, Passed: 40, Failed: 0, Skipped: 0` | — | Bổ sung trường bắt buộc `isActive` vào payload hợp lệ và assert riêng lỗi thiếu trường. Rerun đạt. |
| Backend đầy đủ, PostgreSQL opt-in | `Total tests: 772, Passed: 772, Failed: 0, Skipped: 0` | — | `Test Run Successful`, gồm các nhóm Catalog, Customers, Identity, POS/ca và test mới. |
| Backend integration | `Total tests: 4, Passed: 4, Failed: 0, Skipped: 0` | — | Ba ca seeder và một ca Analytics repository PostgreSQL. |
| Frontend headless | `Total tests: 1063, Passed: 1063, Failed: 0, Skipped: 0` | — | `CategoryWorkspaceTests.Search_DeepMatch_OpensAncestorsAndRestoresExpansion` pass trong suite. |
| Catalog đọc qua Kestrel | `Total tests: 1, Passed: 1, Failed: 0, Skipped: 0` | — | `Seed dataset: 20 products, 5 active category roots, 10 pages at pageSize=2.` |
| Catalog ghi qua Kestrel | `Total tests: 1, Passed: 1, Failed: 0, Skipped: 0` | — | `LiveCatalog_FullManagementLifecycleMatchesServerContract` pass. |

Các số 0 Fail/Skipped trong những lượt có `Test Run Successful` được chuẩn hóa từ tổng và số Passed. Log phụ nằm trong `backend/obj/m3-acceptance/`. Hai lượt lọc backend trùng tập ca với suite backend đầy đủ. Lượt Catalog là hai filter riêng, suite live của frontend còn các test ngoài phạm vi hai filter này. PostgreSQL integration tại mốc cố định ghi `today` 27.980.000 ₫/2 đơn và `last7Days`, `thisMonth`, `custom` 136.080.000 ₫/20 đơn.

## Regression Admin M2

Nguồn build cho các mã trong bảng là baseline hiện tại ở đầu trang. Các ca tự động dùng TestServer/PostgreSQL, Kestrel hoặc headless như cột bằng chứng. Phần này không có ca Admin chạy tay.

| Mã | Owner | Đầu vào | Expected | Actual | Bằng chứng | Trạng thái |
| --- | --- | --- | --- | --- | --- | --- |
| ADM-P01 | Bình | Admin tạo SKU thử, cập nhật biến thể, tắt/bật, xóa | Bản ghi và biến thể đúng. GET sau xóa không còn bản ghi | `LiveCatalog_FullManagementLifecycleMatchesServerContract` pass, bao gồm đọc lại sau xóa | Kestrel, lệnh Catalog ghi ở trên | Pass |
| ADM-P02 | Bình | 20 sản phẩm seed, `pageSize=2`, kết hợp từ khóa/brand/category/giá/spec | 10 trang ổn định, kết quả đúng bộ lọc. Detail có 200/404 phù hợp | 20 sản phẩm và 10 trang, các probe search/filter/detail khớp | Kestrel Catalog đọc và `ProductSearchPostgresTests` | Pass |
| ADM-P03 | Bình | Giá và giá vốn 0. Tên trống, tên 201 ký tự, giá −1, danh mục không tồn tại, sort/spec sai | Giá 0 được lưu. Các input sai trả 400 theo trường | `POST 201, GET 200 basePrice=0 costPrice=0, DELETE 204`. Bốn yêu cầu ghi sai trả 400 đúng trường, không có SKU mới. Sort/spec sai trả 400 | HTTP/PostgreSQL ca `AdminProduct_PriceZeroAndRejectedWrites_MatchCatalogContract`, Catalog đọc | Pass |
| ADM-CAT01 | Bình | Danh mục rỗng, tạo → đọc cây → PUT → DELETE | 201/200/200/204, cây sau xóa không còn ID | Khớp. PUT đầy đủ có `isActive` và slug | HTTP/PostgreSQL ca `AdminCategory_TreeCrudAndValidation_PersistOnlyValidRequests` | Pass |
| ADM-CAT02 | Bình | Tên trống/201, slug `bad slug!`, PUT thiếu `isActive` | 400 đúng trường, cây giữ tên/slug hợp lệ gần nhất | 400 dưới `name`, `slug` và `isActive`. Cây không có bản ghi lỗi | Cùng ca HTTP ở lượt rerun | Pass |
| ADM-CAT03 | Bình | Match trong node sâu của cây | Mở tổ tiên và phục hồi trạng thái mở cây | `Search_DeepMatch_OpensAncestorsAndRestoresExpansion` pass | Frontend headless | Pass |
| ADM-CUS01 | Bình | Hai khách test. Tạo, sửa, tìm tên/điện thoại, lọc Standard/active, trang 1–2, deactivate/activate | 201/200/204, 2 kết quả ở 2 trang khác nhau, khách tắt hiện trong lọc inactive | Khớp. GET sau bật lại trả đúng số điện thoại, tên đã sửa và active | HTTP/PostgreSQL ca `AdminCustomer_LifecycleSearchAndInvalidInputs_MatchCustomerContract` | Pass |
| ADM-CUS02 | Bình | Tên trắng, phone `invalid`, tier `invalid` | 400 theo trường, dữ liệu cũ giữ nguyên | 400 `fullName`, `phone`, `tier`. Không tạo khách tên trắng hay cập nhật phone sai | Cùng ca HTTP ở trên | Pass |
| ADM-CUS03 | Bình | Tên khách 201 ký tự | Cột `full_name` là `text`, contract hiện tại cho phép đọc lại nguyên giá trị | `POST 201, GET 200 nameLength=201`. Giới hạn 200 áp dụng cho Catalog | HTTP/PostgreSQL ca `AdminCustomer_NameOverCatalogLimit_RoundTripsUnderCustomerTextContract`, [schema](database/schema.dbml) | Pass |

## Commit và rollback qua HTTP/PostgreSQL

Hai ca live flow được chạy lại trong lượt backend lọc. Các mã dưới đây dùng IMEI fixture `490000000000004`, SKU `DEMO-TAB-IPAD-AIR` trên cùng SHA nguồn và test artifact ở đầu báo cáo. Tất cả là test tự động.

| Mã | Owner | Đầu vào | Expected | Actual | Bằng chứng | Trạng thái |
| --- | --- | --- | --- | --- | --- | --- |
| POS-201 | Bình | Cash 20.000.000 ₫, giảm dòng 90.000 ₫, khách Demo Customer 01, ca đầu 2.000.000 ₫ | 201, tồn giảm 1, giữ chỗ giữ nguyên, IMEI Sold, ledger −1, ca cộng đúng tổng lưu, Dashboard tăng đúng 1 đơn tại ngày UTC+07 | Tồn 3 → 2, giữ 1 → 1, Sold, bảo hành 12 tháng, Sale −1 tới tồn 2. VAT 1.690.000 ₫, tổng lưu và Cash 18.590.000 ₫, đóng ca 20.590.000 ₫. Hoàn tất 20/10 lúc 17:01Z, Dashboard ngày 21/10 cộng 18.590.000 ₫ và 1 đơn | `CashCheckout_AndClosedShift_UpdateManagerDashboardOnVietnamDate`, log `committed` và `after` | Pass |
| POS-409 | Bình | Checkout IMEI đã Sold với khóa mới, ca Open, tiền đủ | 409, không thêm đơn/dòng/ledger, giữ stock, serial, tiền ca và Dashboard | `Device is Sold; expected InStock`. Tồn 2, giữ 1, ca Open Cash 18.590.000 ₫. 21 đơn, 21 dòng, các ID ledger và snapshot Dashboard giữ nguyên | `CheckoutWithNewKey_ForSoldDevice_LeavesManagerDashboardAndStoresUnchanged`, log `conflict` và `rollback-read-back` | Pass |
| POS-400 | Bình | Ca Open, IMEI InStock, tiền nhận 1 ₫, khóa mới | 400 `cashTendered`, không ghi khóa, snapshot trước/sau giống nhau | `HTTP 400 field=cashTendered, key absent`. Tồn 3, giữ 1, InStock, bảo hành null, 3 movement của stock, 20 đơn, 20 dòng và Cash 0 giữ nguyên | `CashCheckout_InsufficientCash_Returns400AndPreservesCommittedState`, log `cash checkout` | Pass |

Ca POS-409 là xung đột tuần tự. Ca POS-400 chứng minh dữ liệu còn nguyên khi request bị từ chối đầu vào.

## Bằng chứng các phần đã bàn giao

Các kết quả trong bảng giữ SHA, platform và mức xác nhận của nguồn. SHA nền kèm working tree chỉ đại diện đúng bản đã ghi trong nguồn. Kết quả từ suite lịch sử là tham chiếu, không cộng với số runner Sprint 5. Chi tiết lỗi đã sửa và phần còn mở nằm ở hai mục tiếp theo.

Các lượt suite đã công bố trước Sprint 5 gồm 600 backend opt-in và 936 frontend của An trên `4548b45` + working tree, 609 backend của T. Vinh trên `7240d3f`, 643 backend opt-in và 975 frontend của Vy trong archive pricing, và 694 backend opt-in của Bình trong lần cuối seed reconciliation. Đây là số Pass do từng nguồn ghi. Xem điều kiện, Fail/Skipped và mốc build trong các nguồn ở bảng dưới.

| Mã | Nhóm và owner | Loại, đầu vào | Expected | Actual, build | Bằng chứng | Trạng thái |
| --- | --- | --- | --- | --- | --- | --- |
| RBAC-01 | Identity · An | HTTP/PostgreSQL, bốn role và các ca token, scope, khóa/reset | Role/actor đúng, token cũ mất hiệu lực, scope và metadata an toàn | Nguồn ghi 69 kiểm tra HTTP đạt ở `identity-access-management`. SHA cụ thể của lượt 69 checks chưa ghi trong tài liệu chốt | [Identity acceptance](IDENTITY_ACCESS_MANAGEMENT_ACCEPTANCE.md#hai-task-cuối) | Pass |
| RBAC-02 | Identity · An | TestServer/PostgreSQL và Kestrel, login, profile, 401/403, actor đơn | Kestrel dùng JWT thật, quyền chặn đúng | 7/7 ca bổ sung, Kestrel bốn login/profile và anonymous 401. Build `4548b45` + working tree của change An, nguồn có hash bó test | [Identity handoff](M3_IDENTITY_INTEGRATION_HANDOFF.md#kết-quả-theo-build) | Pass |
| UI-01 | Skia · T. Vinh | Tác giả chạy tay A–G trên Ubuntu, role, tìm, Tab, theme, resize | Menu, bảng, giá vốn, focus và trạng thái phiên đúng | A–G đạt trên Skia, build `7597418`. Nguồn ghi riêng ảnh do owner giữ | [UI nghiệm thu A–G](M3_UI_API_VERIFICATION.md#bổ-sung-17112026-nghiệm-thu-ui-và-kiểm-tra-lại-quyền-đổi-phiên) | Pass |
| HTTP-01 | Methods và lỗi · T. Vinh | GET/POST/DELETE, JSON, lỗi HTTP và mất mạng | Các mã 200/201/204/400/401/403/404/409/500 và phục hồi đúng | Nguồn ghi khớp 8 mã đầu trên nền `7240d3f`, bổ sung 500 qua test pipeline thật và phục hồi mạng. Build lần bổ sung ghi ở nguồn | [HTTP và JSON](M3_UI_API_VERIFICATION.md#http-methods-json-và-mã-lỗi) | Pass |
| CASH-01 | Cash/ca · Đạt | HTTP/PostgreSQL mở ca, bán, đọc đơn, đóng ca. POS UI do người dùng chạy tay | Số tiền thu/thừa và ca khớp, thiết bị chuyển Sold | Nguồn bàn giao ca 7.2 đạt trên build POS của ngày 17/11, SHA chính xác của lượt UI chưa ghi kèm. Phần checkout/ca HTTP cũng có test chạy đạt trong backend build Sprint 5 | [POS Cash/ca](../openspec/changes/archive/2026-11-17-m3-pos-cash-shift/tasks.md#7-kiểm-chứng) | Pass |
| PRICE-01 | Công thức tiền · Vy | HTTP tạo đơn Admin hai dòng, giảm đơn 200.000 ₫, VAT 10%, đọc lại đơn | Subtotal 31.980.000 ₫, VAT 3.178.000 ₫, total 34.958.000 ₫, phân bổ khớp | POST 201 và GET trả đúng phân bổ. Sales vượt 10% trả 400. SHA chính xác chưa ghi trong phần kết quả của archive. Bằng chứng này thuộc Orders | [Order pricing](../openspec/changes/archive/2026-11-17-m3-order-pricing/tasks.md#kết-quả-kiểm-thử-ai-2026-11-17-windows-11) | Pass |
| CUST-01 | Khách hàng · Vy | Sales dùng HTTP tìm/tạo khách và tạo đơn nháp có `customerId`. Vy chạy kiểm tra UI chọn khách trên trang tạo đơn | API trả 201 và đơn giữ đúng liên kết. Sales không sửa hoặc xem lịch sử | API khớp. UI do Vy xác nhận. Archive ghi ngày 18/11, SHA lượt UI chưa ghi rõ. Lượt Bình chạy trên baseline hiện tại kiểm lại thao tác tìm khách và lưu liên kết qua checkout HTTP | [Sales customers](../openspec/changes/archive/2026-11-18-m3-sales-customers/tasks.md#kết-quả-kiểm-thử-ai-2026-11-17-windows-11) | Pass |
| DASH-01 | Dashboard Manager · Bình | HTTP JWT bốn role, desktop Manager chọn 12–18/10/2026 | A/M 200, S/W 403. Manager đọc live khi preview tắt | Khớp, 20 đơn và 136.080.000 ₫. Bản chạy `4a1f095` + working tree, binary hash ở bàn giao | [Dashboard Manager](M3_DASHBOARD_DATA_TEST_HANDOFF.md#dashboard-manager) | Pass |
| SEED-01 | Dữ liệu demo · Bình | PostgreSQL mới, Local storage, đọc ảnh qua API | 20 sản phẩm có giá vốn/ảnh, 5 danh mục, 10 khách, 20 đơn nhiều trạng thái, 4 tài khoản | Đạt, 20 URL ảnh trả được HTTP. Fixture hiện có 50 IMEI. Đã chạy lại trên baseline Sprint 5, 3 ca seed pass | [Seed PostgreSQL và Expected](M3_DEMO_DATA_RECONCILIATION.md#expected-tính-từ-manifest) · Lượt backend integration ở trên | Pass |
| SEED-02 | Seed lại và KPI · Bình | Seed fresh/rerun/legacy/partial/conflict, API bốn kỳ với mốc 18/10 | Bảo toàn credential/ID/thời gian, giữ ngày zero và tổng kỳ khớp | PostgreSQL rerun đạt. 18/10 Today 27.980.000 ₫ và 2 đơn. Các kỳ còn lại 136.080.000 ₫ và 20 đơn; zero-fill giữ đúng ngày | [Actual cố định](M3_DEMO_DATA_RECONCILIATION.md#actual-qua-http-ở-mốc-cố-định) · Lượt backend integration trên baseline Sprint 5 | Pass |
| LIVE-01 | POS → Dashboard · Bình | Skia/Kestrel với IMEI fixture, khách lẻ, mở ca/quét/bán/đóng ca, Manager đổi kỳ | Live nhận đúng mã đơn và delta trên UI | Baseline `e587ee7` + probe tạm, Dashboard ngày 18/11 cộng 1 đơn và 18.689.000 ₫, mã `ORD-202611-0001` | [Desktop với Kestrel](M3_DASHBOARD_LIVE_FLOW.md#kiểm-chứng-desktop-với-kestrel) | Pass |

## Lỗi và lượt chạy lại

| Mã ca | Lỗi và owner | Thay đổi / bằng chứng | Lượt chạy lại |
| --- | --- | --- | --- |
| ADM-CAT01 | Bình. Payload test PUT thiếu `isActive`, runner nhận 400 thay vì 200 | Bổ sung trường bắt buộc cho payload hợp lệ và giữ kỳ vọng PUT 200. Assertion riêng kiểm 400 khi thiếu trường | Lượt đầu 39/40. Lượt lọc lại 40/40, backend đầy đủ 772/772 trên baseline Sprint 5 với test đã sửa |
| HTTP-01 | T. Vinh. Mã 500 cần xác minh body an toàn | Nguồn ghi bổ sung `AddProblemDetails` và `UseExceptionHandler` | `UnhandledException_Returns500ProblemDetailsWithoutStackTrace` đạt ở bản bàn giao sau sửa. [Bằng chứng](M3_UI_API_VERIFICATION.md#còn-mở) |
| CASH-01 | Đạt. Giỏ trắng ở cửa sổ nhỏ, mất giỏ khi rời trang, điều hướng hỏng khi đổi tài khoản, màu chữ ô quét theme Tối | Nguồn POS ghi đã sửa cả bốn lỗi | Ba lỗi đầu được kiểm tra lại trong nghiệm thu của Đạt. Ô quét theme Tối còn thiếu lượt UI xác nhận. [POS Cash, mục 7.2](../openspec/changes/archive/2026-11-17-m3-pos-cash-shift/tasks.md#7-kiểm-chứng) |
| CUST-01 | Vy. Test escape từng trả về sớm nếu thiếu PostgreSQL | Đổi sang `PostgresFact` và test thêm `%`/`_` | Nguồn ghi probe bỏ escape làm test fail. Test repository đạt trong lượt lọc Sprint 5. [Review trong archive](../openspec/changes/archive/2026-11-18-m3-sales-customers/tasks.md#kết-quả-kiểm-thử-ai-2026-11-17-windows-11) |

## Mục còn mở để An ghép bàn giao

`dev` đã fetch chưa có receiving. Test Checkout của Đạt có hai request và gate ở phía gửi. Suite backend hiện tại chạy đạt các test đó, nhưng bằng chứng đang có chưa xác nhận thời điểm giao dịch hoặc lock wait chồng nhau. Hai ca API của Bình 409 và 400 dùng request tuần tự. Các ca dưới đây cần thỏa điều kiện ở cột cuối để được ghi là đã kiểm.

| Mã | Owner | Đầu vào / Expected | Actual và build | Bằng chứng hoặc điều kiện chạy lại | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| REC-01 | Q. Vinh | Nhận phiếu có/không IMEI, khớp phiếu/giá vốn/tồn/ledger. Lỗi không ghi dở, gửi lại không cộng hai lần | `dev` mới fetch thiếu controller, chưa chạy trên build ghép | Merge receiving và migration. Chạy test PostgreSQL và trả phiếu, serial, ledger, SHA và truy vấn read-back | Chưa kiểm tra |
| REC-02 | Q. Vinh, Đạt, Bình | Bán đúng IMEI từ phiếu nhận, đóng ca, Dashboard phản ánh đơn và tiền | Chỉ có lượt fixture. Trạng thái được chốt bàn giao trong change live flow với phần receiving chưa chạy | Sau REC-01, chạy cùng build, lặp ca 409, đối chiếu ID phiếu/IMEI/đơn/ca và KPI. [Điều kiện live flow](M3_DASHBOARD_LIVE_FLOW.md#phụ-thuộc-bàn-giao) | Chưa kiểm tra |
| CON-01 | Đạt, Q. Vinh | Hai giao dịch tranh cùng IMEI cuối, một 201 và một 409, chỉ trừ kho và cộng ca một lần | Test POS trên baseline Sprint 5 pass mã HTTP/tổng. Gate client chưa chứng minh giao dịch chồng nhau | `Checkout_TwoCashiersRaceForTheLastDevice_ExactlyOneSucceeds`. Bổ sung bằng chứng hai transaction cùng vào đoạn lock/ghi hoặc lock wait, snapshot cuối và SHA | Chưa kiểm tra |
| CON-02 | Q. Vinh | Hai giao dịch tranh phụ kiện cuối hoặc cùng nhận một phiếu, không âm kho hoặc cộng lần hai | Chưa có bằng chứng giao dịch thực sự chồng trên build bàn giao | Chờ receiving và fixture tranh tài nguyên qua hai connection độc lập, log đồng bộ/lock wait và read-back | Chưa kiểm tra |
| CON-03 | Đạt | Hai transaction chuyển trạng thái cùng đơn, hoặc checkout trong lúc đóng ca, ca chỉ nhận tiền đã commit | Test checkout/đóng ca trên baseline Sprint 5 pass kết quả cuối. Gate trước request chưa chứng minh chồng transaction. Ca `OrdersApiIntegrationTests` ghi tuần tự | Bàn giao trace transaction của cả hai luồng, kết quả đơn/ca/ledger và SHA | Chưa kiểm tra |
| POS-DISC | Đạt, Vy | Checkout POS với giảm cấp đơn khác 0, số lưu, VAT, tiền ca và Dashboard khớp | Baseline thiếu nối giảm cấp đơn từ POS. Ca Orders của Vy và ca VAT/giảm dòng fixture đã có bằng chứng riêng | Nối request POS theo hợp đồng Vy và chạy lại trên build tích hợp | Chưa kiểm tra |
| POS-CUST | Đạt, Vy | Sales chọn khách trên màn POS, lưu cash order và thấy đúng khách | Lượt desktop của Bình chạy khách lẻ. API chọn khách đã được kiểm riêng | Nối bộ chọn vào POS desktop, bán thật và đối chiếu `customerId` trên đơn | Chưa kiểm tra |
| UI-WIN | T. Vinh | Build và thao tác Windows native, theme/resize/Tab/binding và các quyền UI | Bằng chứng hiện có là Skia. Nguồn Vy từng báo build native lỗi `ProbeReportItem`, chưa có lượt kiểm chứng native mới | Ghi build, OS, testcase, ảnh/log. Đưa lỗi còn tồn tại và rerun vào báo cáo | Chưa kiểm tra |
| UI-POS-THEME | Đạt | Mở màn POS theme Tối, ô quét đọc được | Owner ghi đã sửa màu nhưng chưa chạy lại UI | Xác nhận kết quả lúc chạy thật và ghi SHA/target | Chưa kiểm tra |

## Mẫu số nghiệm thu

Ma trận trên có **32 mã duy nhất** gồm **23 Pass, 0 Fail, 0 Skipped, 9 Chưa kiểm tra**. Đây là số mục nghiệm thu trong ba bảng kết quả và bảng còn mở. Dòng lịch sử lỗi ADM-CAT01 được tính theo kết quả rerun cuối, còn lượt runner fail đầu tiên vẫn nằm trong bảng suite. Các kết quả suite ở những build khác không cộng vào 32 mã này.
