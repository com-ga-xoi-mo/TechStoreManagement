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
| DASH-01 | Dashboard Manager · Bình | HTTP JWT bốn role, desktop Manager chọn 12–18/10/2026 | A/M 200, S/W 403. Manager đọc live khi preview tắt | Khớp, 20 đơn và 136.080.000 ₫. Bản chạy `4a1f095` + working tree, binary hash ở bàn giao | [Dashboard Manager](#dashboard-manager) | Pass |
| SEED-01 | Dữ liệu demo · Bình | PostgreSQL mới, Local storage, đọc ảnh qua API | 20 sản phẩm có giá vốn/ảnh, 5 danh mục, 10 khách, 20 đơn nhiều trạng thái, 4 tài khoản | Đạt, 20 URL ảnh trả được HTTP. Fixture hiện có 50 IMEI. Đã chạy lại trên baseline Sprint 5, 3 ca seed pass | [Seed PostgreSQL và Expected](#expected-tính-từ-manifest) · Lượt backend integration ở trên | Pass |
| SEED-02 | Seed lại và KPI · Bình | Seed fresh/rerun/legacy/partial/conflict, API bốn kỳ với mốc 18/10 | Bảo toàn credential/ID/thời gian, giữ ngày zero và tổng kỳ khớp | PostgreSQL rerun đạt. 18/10 Today 27.980.000 ₫ và 2 đơn. Các kỳ còn lại 136.080.000 ₫ và 20 đơn; zero-fill giữ đúng ngày | [Actual cố định](#actual-qua-http-ở-mốc-cố-định) · Lượt backend integration trên baseline Sprint 5 | Pass |
| LIVE-01 | POS → Dashboard · Bình | Skia/Kestrel với IMEI fixture, khách lẻ, mở ca/quét/bán/đóng ca, Manager đổi kỳ | Live nhận đúng mã đơn và delta trên UI | Baseline `e587ee7` + probe tạm, Dashboard ngày 18/11 cộng 1 đơn và 18.689.000 ₫, mã `ORD-202611-0001` | [Desktop với Kestrel](#kiểm-chứng-desktop-với-kestrel) | Pass |

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
| REC-02 | Q. Vinh, Đạt, Bình | Bán đúng IMEI từ phiếu nhận, đóng ca, Dashboard phản ánh đơn và tiền | Chỉ có lượt fixture. Trạng thái được chốt bàn giao trong change live flow với phần receiving chưa chạy | Sau REC-01, chạy cùng build, lặp ca 409, đối chiếu ID phiếu/IMEI/đơn/ca và KPI. [Điều kiện live flow](#phụ-thuộc-bàn-giao) | Chưa kiểm tra |
| CON-01 | Đạt, Q. Vinh | Hai giao dịch tranh cùng IMEI cuối, một 201 và một 409, chỉ trừ kho và cộng ca một lần | Test POS trên baseline Sprint 5 pass mã HTTP/tổng. Gate client chưa chứng minh giao dịch chồng nhau | `Checkout_TwoCashiersRaceForTheLastDevice_ExactlyOneSucceeds`. Bổ sung bằng chứng hai transaction cùng vào đoạn lock/ghi hoặc lock wait, snapshot cuối và SHA | Chưa kiểm tra |
| CON-02 | Q. Vinh | Hai giao dịch tranh phụ kiện cuối hoặc cùng nhận một phiếu, không âm kho hoặc cộng lần hai | Chưa có bằng chứng giao dịch thực sự chồng trên build bàn giao | Chờ receiving và fixture tranh tài nguyên qua hai connection độc lập, log đồng bộ/lock wait và read-back | Chưa kiểm tra |
| CON-03 | Đạt | Hai transaction chuyển trạng thái cùng đơn, hoặc checkout trong lúc đóng ca, ca chỉ nhận tiền đã commit | Test checkout/đóng ca trên baseline Sprint 5 pass kết quả cuối. Gate trước request chưa chứng minh chồng transaction. Ca `OrdersApiIntegrationTests` ghi tuần tự | Bàn giao trace transaction của cả hai luồng, kết quả đơn/ca/ledger và SHA | Chưa kiểm tra |
| POS-DISC | Đạt, Vy | Checkout POS với giảm cấp đơn khác 0, số lưu, VAT, tiền ca và Dashboard khớp | Baseline thiếu nối giảm cấp đơn từ POS. Ca Orders của Vy và ca VAT/giảm dòng fixture đã có bằng chứng riêng | Nối request POS theo hợp đồng Vy và chạy lại trên build tích hợp | Chưa kiểm tra |
| POS-CUST | Đạt, Vy | Sales chọn khách trên màn POS, lưu cash order và thấy đúng khách | Lượt desktop của Bình chạy khách lẻ. API chọn khách đã được kiểm riêng | Nối bộ chọn vào POS desktop, bán thật và đối chiếu `customerId` trên đơn | Chưa kiểm tra |
| UI-WIN | T. Vinh | Build và thao tác Windows native, theme/resize/Tab/binding và các quyền UI | Bằng chứng hiện có là Skia. Nguồn Vy từng báo build native lỗi `ProbeReportItem`, chưa có lượt kiểm chứng native mới | Ghi build, OS, testcase, ảnh/log. Đưa lỗi còn tồn tại và rerun vào báo cáo | Chưa kiểm tra |
| UI-POS-THEME | Đạt | Mở màn POS theme Tối, ô quét đọc được | Owner ghi đã sửa màu nhưng chưa chạy lại UI | Xác nhận kết quả lúc chạy thật và ghi SHA/target | Chưa kiểm tra |

## Mẫu số nghiệm thu

Ma trận trên có **32 mã duy nhất** gồm **23 Pass, 0 Fail, 0 Skipped, 9 Chưa kiểm tra**. Đây là số mục nghiệm thu trong ba bảng kết quả và bảng còn mở. Dòng lịch sử lỗi ADM-CAT01 được tính theo kết quả rerun cuối, còn lượt runner fail đầu tiên vẫn nằm trong bảng suite. Các kết quả suite ở những build khác không cộng vào 32 mã này.

## Dashboard Manager

Kiểm chứng ngày 18/11/2026 trên Windows `10.0.26200.0` với .NET SDK `10.0.404`. Source là commit nền `4a1f095a4b8f7143e4868bb4d3d84998da0e6fb6` cộng các thay đổi cục bộ của `m3-manager-dashboard-access`. Kestrel dùng `http://127.0.0.1:5297` và database `techstore_m3_dashboard_live_test` đã migrate/seed trong container PostgreSQL 16 cô lập. Các test repository và seed dùng database test riêng.

Giao diện được kiểm trên cửa sổ Uno Skia `net10.0-desktop` với bộ điều khiển kiểm thử tạm dùng cùng login, Shell, Dashboard và API của bản build. Probe dùng tệp phiên theo process để giữ dữ liệu phiên đang có trên máy. SHA-256 bản probe là `776864c0de73e28e2e93140e8cd83b6592fb33ed32275ba3faf897b239361591`. Mã probe đã được gỡ khỏi source bàn giao.

- SHA-256 `TechStore.Api.dll` dùng với Kestrel test — `4deb2a47a7e0ca83f246174dcb5ac9726453baa88888fc7cc0ec3496e305954a`.
- SHA-256 `TechStore.Client.dll` từ build desktop sau khi gỡ probe — `54493960ad655a5c35a56c0bd18a67d2090d7e2aeeb483bd476330d6c3a38d45`. Build 0 warning, 0 error.

| Ca | Mong đợi | Kết quả thực tế | Trạng thái |
| --- | --- | --- | --- |
| JWT từ đăng nhập thật, gọi `last7Days` và `custom` | Admin/Manager 200, SalesStaff/WarehouseStaff 403 | Cả hai kỳ trả 200/200/403/403. Body của 403 không có dữ liệu Dashboard | Pass |
| Thiếu JWT và JWT sai, gọi `custom` | HTTP 401 | Cả hai trả 401 | Pass |
| Manager gửi kỳ sai, khoảng trên 31 ngày hoặc khoảng trong tương lai | HTTP 400 và chỉ rõ trường lỗi | Cả ba trả 400 với trường validation | Pass |
| Manager gửi `custom` từ `2026-10-12` đến `2026-10-18` | Bảy điểm theo UTC+07, doanh thu dương, có đơn demo thật | `asOf=2026-11-18T03:35:57.775208Z`, 7 ngày, 20 đơn, doanh thu quan sát được `136080000.00`, có `DEMO-202610-0011` | Pass |
| Swagger của endpoint Dashboard | Hiện `period`, `from`, `to` và giới hạn 31 ngày | Đã kiểm tra JSON tại `/swagger/v1/swagger.json` | Pass |
| Backend Analytics và kiểm kê phân quyền (chạy có lọc) | Các test đã chọn chạy đạt | 35 pass, 0 fail, 0 skipped, gồm ca quyền dùng PostgreSQL | Pass |
| Toàn bộ suite `Backend.UnitTests` | Chạy lại sau khi sửa `IdentityHttpTests` theo quyền Dashboard mới | 649 pass, 0 fail, 39 skipped | Pass |
| PostgreSQL Analytics và seed regression | Biên UTC+07, ngày trống, số đơn/doanh thu theo đúng trường, seed tiếp tục chạy ổn định | 1 ca Analytics và 3 ca seed pass. Repository giữ snapshot tồn kho/sản phẩm tại thời điểm đọc | Pass |
| Đăng nhập `manager1` trong app, chọn 12–18/10/2026 | Vào Tổng quan, có dữ liệu live, nhãn kỳ/as-of và đơn seed | Tổng quan được mở, menu không có Khách hàng, `asOf=2026-11-18T03:45:28.4485200Z`, quan sát 20 đơn và doanh thu 136.080.000 đ | Pass |
| Khoảng ngày trên desktop | Ô ngày và Áp dụng hiển thị, Tab đi từ Từ ngày đến Đến ngày, lỗi ngày giữ báo cáo cũ | Gửi Tab vào chính cửa sổ probe và xác nhận focus. `31/02/2026` hiện lỗi đúng ô, snapshot giữ nguyên | Pass |
| Làm mới sau khi sửa nháp | Giữ khoảng 12–18/10 và đọc snapshot live mới | Sửa nháp thành 13/10, bấm Làm mới vẫn nhận ngày đầu 12/10 và snapshot mới | Pass |
| Đổi Sáng/Tối, resize và kỳ qua tháng | Màu form cập nhật khi trang đang mở, form vừa cửa sổ, bảng ngày đầy đủ và phạm vi rõ | Shell viewport 994×698 và 610×474 DIP ở raster scale 1,25. Đổi theme giữ snapshot. Kỳ 28/09–18/10 có 21 ngày, chart hiện nhãn `18/10`, tiêu đề đơn/tồn nêu đúng phạm vi | Pass |
| Preview với `TECHSTORE_DASHBOARD_DEMO=1` | Dùng fixture và chỉ hiện ba kỳ sẵn có | Snapshot mang `SourceKind=Preview`, nút Khoảng ngày ẩn và VM có đúng ba kỳ | Pass |
| Frontend headless | Test điều hướng, repository, preview, VM và các test khác | 1.042 pass khi lọc riêng ca DI bên dưới | Pass |
| Frontend DI `AddFrontendFoundation_ResolvesSharedSessionAndTransientTypedClient` | Test hoàn thành với tệp phiên test | Lần chạy đầy đủ có 1 fail do sandbox từ chối xóa `AppData/Local/TechStore/session.json`. Tệp phiên người dùng được giữ nguyên | Fail môi trường |

Đã sửa [`IdentityHttpTests`](../tests/Backend.UnitTests/IdentityHttpTests.cs) để giữ kiểm tra Dashboard trả `403` cho SalesStaff và WarehouseStaff. `AnalyticsControllerTests` kiểm tra Manager nhận `200`. Lần chạy toàn bộ `Backend.UnitTests` chưa bật `TECHSTORE_TEST_CONNECTION`, nên 39 ca PostgreSQL opt-in được ghi là skipped.

Trích bằng chứng HTTP đã lọc, không chứa JWT hoặc thông tin kết nối:

```text
Admin           last7Days=200 custom=200
Manager         last7Days=200 custom=200
SalesStaff      last7Days=403 custom=403
WarehouseStaff  last7Days=403 custom=403
Anonymous=401; invalid JWT=401
Manager invalid period=400, oversized range=400, future range=400
```

Trích log cửa sổ Skia từ phiên live:

```text
[UI PASS] Tab moves from start date to end date
[UI PASS] Invalid date shows a bound field error
[UI PASS] Live custom range reached database
[UI PASS] Rendered report interval and as-of info
[UI PASS] Refresh uses applied interval after draft edit
[UI PASS] Dark theme updates form surface while Dashboard remains open
[UI PASS] Narrow Dashboard keeps date fields and Apply visible
[UI PASS] Cross-month chart and complete daily-date values
[UI PASS] Preview fixture and exactly three periods (lần chạy preview riêng)
```

Kiểm tra UI lần này chạy trên Skia/Win32. Windows native và các nền tảng Skia khác chưa kiểm tra. Các số của luồng live là output quan sát khi nghiệm thu truy cập. Bằng chứng đối soát riêng của `m3-demo-data-reconciliation` nằm trong phần [Dữ liệu demo và đối soát KPI](#dữ-liệu-demo-và-đối-soát-kpi).

[bảng Expected/Actual, bằng chứng kiểm thử và hướng dẫn chạy lại demo Local/R2](#dữ-liệu-demo-và-đối-soát-kpi) · [bằng chứng API/PostgreSQL và desktop sau checkout](#đối-soát-giao-dịch-pos-live)

## Dữ liệu demo và đối soát KPI

Bốn kỳ API ở mốc seed đều khớp bảng tính tay. Luồng live ghi `asOf` riêng theo ngày demo.

Mốc cố định là `2026-10-18T05:00:00Z`, tức 12:00 ngày 18/10/2026 (UTC+07). Các bảng Expected áp dụng cho PostgreSQL mới, chỉ có dữ liệu seed. Dashboard đếm đơn theo `CreatedAt` ở mọi trạng thái và cộng `TotalAmount` của đơn Completed theo ngày `CompletedAt`. Hai biên dùng ngày Việt Nam, với thời điểm cuối kỳ giới hạn theo `asOf` nếu kỳ còn đang diễn ra.

### Expected tính từ manifest

Mỗi dòng dưới đây là một đơn, một mặt hàng, số lượng 1. Tiền đơn bằng đơn giá trong fixture, thuế và giảm giá đều bằng 0. Seeder gán trực tiếp các số này. Kiểm chứng công thức của Vy trên đơn POS thật thuộc `m3-dashboard-live-flow`. Cột mã lấy tiền tố chung `DEMO-202610-`. Giờ và ngày đều là giờ Việt Nam tháng 10/2026.

| Mã | SKU | Trạng thái | Tạo lúc | Hoàn tất lúc | Tiền đơn (₫) |
| --- | --- | --- | --- | --- | ---: |
| 0001 | DEMO-TAB-IPAD-AIR | Completed | 17/10 11:39 | 17/10 11:59 | 16.990.000 |
| 0002 | DEMO-TAB-GALAXY-S9 | Completed | 16/10 11:39 | 16/10 11:59 | 19.990.000 |
| 0003 | DEMO-TAB-LENOVO-P12 | Completed | 15/10 11:39 | 15/10 11:59 | 8.990.000 |
| 0004 | DEMO-MON-DELL-U2723 | Completed | 13/10 11:39 | 13/10 11:59 | 15.990.000 |
| 0005 | DEMO-MON-LG-27GP | Completed | 12/10 11:39 | 12/10 11:59 | 11.990.000 |
| 0006 | DEMO-MON-ASUS-PROART | Completed | 18/10 11:39 | 18/10 11:59 | 10.990.000 |
| 0007 | DEMO-SSD-SAMSUNG-T7 | Completed | 17/10 11:39 | 17/10 11:59 | 2.690.000 |
| 0008 | DEMO-SSD-SANDISK-EXT | Completed | 16/10 11:39 | 16/10 11:59 | 2.990.000 |
| 0009 | DEMO-DOCK-ANKER-777 | Completed | 15/10 11:39 | 15/10 11:59 | 5.990.000 |
| 0010 | DEMO-KEY-LOGI-MX | Completed | 12/10 11:39 | 12/10 11:59 | 2.490.000 |
| 0011 | DEMO-TAB-IPAD-AIR | Completed | 18/10 11:39 | 18/10 11:59 | 16.990.000 |
| 0012 | DEMO-TAB-GALAXY-S9 | Completed | 13/10 11:39 | 13/10 11:59 | 19.990.000 |
| 0013 | DEMO-TAB-IPAD-AIR | Processing | 16/10 23:00 | — | 16.990.000 |
| 0014 | DEMO-TAB-GALAXY-S9 | Processing | 16/10 22:00 | — | 19.990.000 |
| 0015 | DEMO-MOUSE-LOGI-MX3 | Pending | 16/10 21:00 | — | 1.690.000 |
| 0016 | DEMO-WEBCAM-LOGI-BRIO | Pending | 16/10 20:00 | — | 4.290.000 |
| 0017 | DEMO-HEADSET-JABRA-EVOLVE | Pending | 16/10 19:00 | — | 6.290.000 |
| 0018 | DEMO-HEADSET-JABRA-EVOLVE | Cancelled | 15/10 18:00 | — | 6.290.000 |
| 0019 | DEMO-CHARGER-ANKER-735 | Cancelled | 15/10 17:00 | — | 1.190.000 |
| 0020 | DEMO-CABLE-USB-C | Cancelled | 15/10 16:00 | — | 290.000 |

| Ngày | Mã đơn cộng vào doanh thu | Cộng tiền (₫) | Đơn tạo trong ngày |
| --- | --- | ---: | ---: |
| 01–11/10, mỗi ngày | — | 0 | 0 |
| 12/10 | 0005 + 0010 | 14.480.000 | 2 |
| 13/10 | 0004 + 0012 | 35.980.000 | 2 |
| 14/10 | — | 0 | 0 |
| 15/10 | 0003 + 0009 | 14.980.000 | 5 |
| 16/10 | 0002 + 0008 | 22.980.000 | 7 |
| 17/10 | 0001 + 0007 | 19.680.000 | 2 |
| 18/10 đến 12:00 | 0006 + 0011 | 27.980.000 | 2 |
| **Cộng 01–18/10** | **12 đơn Completed** | **136.080.000** | **20** |

| Kỳ | Ngày UTC+07 | Điểm theo ngày | Doanh thu (₫) | Số đơn | Đơn mới nhất theo thứ tự |
| --- | --- | ---: | ---: | ---: | --- |
| `today` | 18/10 | 1 | 27.980.000 | 2 | 0011, 0006 |
| `last7Days` | 12–18/10 | 7 | 136.080.000 | 20 | 0011, 0006, 0001, 0007, 0013 |
| `thisMonth` | 01–18/10 | 18 | 136.080.000 | 20 | 0011, 0006, 0001, 0007, 0013 |
| `custom&from=2026-10-12&to=2026-10-18` | 12–18/10 | 7 | 136.080.000 | 20 | 0011, 0006, 0001, 0007, 0013 |

Tổng ngày của từng kỳ bằng doanh thu kỳ. Ngày 14/10 được trả về với giá trị 0 trong cả ba kỳ chứa ngày đó. `thisMonth` còn có 11 điểm 0 đầu tháng. Đơn mới nhất sắp theo `CreatedAt DESC, id DESC`, dùng ID fixture để phân thứ tự khi giờ tạo trùng.

**Snapshot tồn kho.** Cả 20 sản phẩm fixture đều hoạt động. Mười SKU có IMEI nhập 5 chiếc mỗi SKU. iPad Air và Galaxy Tab S9 bán 2 chiếc, giữ 1 chiếc mỗi SKU nên tồn 3, khả dụng 2. Tám SKU theo dõi IMEI còn lại bán 1 chiếc mỗi SKU nên tồn 4. Năm SKU không theo dõi IMEI có dòng tồn bằng 0. Ngưỡng là 3 và điều kiện Dashboard là tồn vật lý ≤ 3, nên có **7** dòng tồn thấp.

| Thứ tự | Sản phẩm trong nhóm tồn thấp | Tồn | Đang giữ | Khả dụng | Ngưỡng | API trả trong danh sách |
| ---: | --- | ---: | ---: | ---: | ---: | --- |
| 1 | Anker 735 Charger 65W | 0 | 0 | 0 | 3 | Có |
| 2 | Jabra Evolve2 65 Flex | 0 | 0 | 0 | 3 | Có |
| 3 | Logitech Brio 4K Webcam | 0 | 0 | 0 | 3 | Có |
| 4 | Logitech MX Master 3S | 0 | 0 | 0 | 3 | Có |
| 5 | USB-C 100W Braided Cable | 0 | 0 | 0 | 3 | Có |
| 6 | Galaxy Tab S9 256GB | 3 | 1 | 2 | 3 | — |
| 7 | iPad Air 11 M2 128GB | 3 | 1 | 2 | 3 | — |

Fixture hiện có 5 danh mục, 20 sản phẩm có giá vốn, 3 biến thể, 10 khách, 20 đơn (12 Completed, 2 Processing, 3 Pending, 3 Cancelled), 4 tài khoản và 50 IMEI. Các mục tối thiểu của M3 gồm 5 danh mục, 20 sản phẩm có giá vốn và ảnh trên DB mới, 10 khách, 20 đơn nhiều trạng thái và 4 tài khoản. 50 IMEI là số hồi quy của fixture hiện tại.

### Actual qua HTTP ở mốc cố định

Chạy ngày 18/11/2026 với .NET SDK `10.0.404`, Debug, commit nền `4a1f095` và thay đổi cục bộ của hai change Dashboard Manager và dữ liệu demo. `WebApplicationFactory<Program>` dùng PostgreSQL schema riêng và storage Local. Manager lấy JWT qua login thật. Host test cố định riêng thời điểm snapshot mà Analytics đọc, để controller, truy vấn, phân quyền và JSON chạy theo đường API thực tế.

| Kỳ | Biên ngày Actual | Expected doanh thu / đơn | Actual doanh thu / đơn | Sản phẩm / tồn thấp Actual | Điểm ngày Actual | HTTP |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| `today` | 18/10 | 27.980.000 / 2 | 27.980.000 / 2 | 20 / 7 | 1 | 200 · Pass |
| `last7Days` | 12–18/10 | 136.080.000 / 20 | 136.080.000 / 20 | 20 / 7 | 7 | 200 · Pass |
| `thisMonth` | 01–18/10 | 136.080.000 / 20 | 136.080.000 / 20 | 20 / 7 | 18 | 200 · Pass |
| `custom` | 12–18/10 | 136.080.000 / 20 | 136.080.000 / 20 | 20 / 7 | 7 | 200 · Pass |

Cả bốn response có `asOf=2026-10-18T05:00:00Z` và `timeZone=UTC+07:00`. Actual khớp từng dòng Expected về ngày, doanh thu, tổng kỳ, danh sách tồn thấp và thứ tự đơn mới. Hai chỉ số phần trăm thay đổi đều trả null. Các ngày zero gồm 14/10 cho ba kỳ chứa ngày đó và thêm 01–11/10 cho `thisMonth`.

#### Chạy lại kiểm thử

Đặt biến `TECHSTORE_SEED_TEST_POSTGRES` thành chuỗi kết nối PostgreSQL test có tên database chứa `test`. Fixture tạo và xóa schema riêng, dùng storage tạm trong mỗi ca. Lệnh PowerShell dưới đây in Actual của từng kỳ khi chạy với logger chi tiết.

```powershell
$env:TECHSTORE_SEED_TEST_ALLOW_SCHEMA = 'YES'
dotnet test tests/Backend.IntegrationTests/TechStore.Backend.IntegrationTests.csproj --filter 'FullyQualifiedName~DataSeederPostgresIntegrationTests' --logger 'console;verbosity=detailed'
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter 'FullyQualifiedName~DemoSeed|FullyQualifiedName~DataSeederTests'
```

| Bộ kiểm thử | Pass | Fail | Skipped | Bằng chứng |
| --- | ---: | ---: | ---: | --- |
| Seed PostgreSQL opt-in | 3 | 0 | 0 | Fresh, rerun, DB Sprint 1, thiếu một phần và conflict. Kiểm chứng 20 URL ảnh qua HTTP, tài khoản theo role, liên kết đơn/ca/VietQR/IMEI/kho và bốn kỳ Dashboard. |
| Seed unit tests có lọc | 15 | 0 | 0 | Manifest, ảnh nhúng, idempotency và lỗi asset, storage, save, phản hồi commit. |
| Toàn bộ Backend.UnitTests | 654 | 0 | 39 | 39 ca PostgreSQL opt-in của suite này được skip. Trong sandbox Windows, chạy lại với `Logging__EventLog__LogLevel__Default=None` và Local storage trong workspace để tránh lỗi quyền Windows Event Log. |
| Toàn bộ Backend.UnitTests bật PostgreSQL | 694 | 0 | 0 | Lần cuối bật `TECHSTORE_TEST_CONNECTION` trỏ PostgreSQL test, gồm ca giữ credential của tài khoản demo qua rerun. |

SHA-256 bản chạy HTTP cố định cuối là `839ED352E99706B9036EBCE36D596B4E2EBAC5830A8D0CE2A43B85DCF1BBF10E` cho `TechStore.Api.dll` và `5B3E29B1A39487866092A2A192BA4DA990541DB4F56985AA94C284E24282BDB9` cho `TechStore.Backend.IntegrationTests.dll`.

### Chạy demo Local

1. Khởi động PostgreSQL bằng `docker compose up -d`. Tạo một database demo trống và dùng chuỗi kết nối của database đó cho `ConnectionStrings__DefaultConnection`. API tự chạy migrations và seed ở môi trường Development.
2. Mở PowerShell ở gốc repo và chạy API. `ObjectStorage__Local__RootPath` phải là thư mục demo có quyền ghi. Giữ cùng database và thư mục qua các lần seed lại.

   ```powershell
   $env:ObjectStorage__Provider = 'Local'
   $env:ObjectStorage__Local__RootPath = Join-Path (Get-Location) 'backend/uploads/m3-demo'
   $env:ObjectStorage__Local__RequestPath = '/uploads'
   dotnet run --project backend/TechStore.Api.csproj --launch-profile http
   ```

3. Ở cửa sổ PowerShell khác, chạy client và đăng nhập `manager1` với mật khẩu demo `Manager@123`. Mở **Tổng quan → Khoảng ngày**, nhập **12/10/2026** đến **18/10/2026** rồi bấm **Áp dụng**.

   ```powershell
   $env:TECHSTORE_API_BASE_URL = 'http://127.0.0.1:5176/'
   $env:TECHSTORE_DASHBOARD_DEMO = '0'
   dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
   ```

4. Mở chi tiết sản phẩm trong Catalog. Có thể xác nhận trực tiếp bằng HTTP với đoạn dưới đây. Dùng cùng địa chỉ gốc nếu API chạy trên cổng khác. Kiểm tra `imageUrl` trùng URL ảnh bìa trong gallery và URL trả HTTP 200, `image/png`.

   ```powershell
   $baseUri = [uri]'http://127.0.0.1:5176/'
   $auth = Invoke-RestMethod ([uri]::new($baseUri, 'api/auth/login')) -Method Post -ContentType 'application/json' -Body '{"username":"manager1","password":"Manager@123"}'
   $headers = @{ Authorization = "Bearer $($auth.accessToken)" }
   $catalog = Invoke-RestMethod ([uri]::new($baseUri, 'api/products?page=1&pageSize=20')) -Headers $headers
   $product = Invoke-RestMethod ([uri]::new($baseUri, "api/products/$($catalog.items[0].id)")) -Headers $headers
   $image = Invoke-WebRequest ([uri]::new($baseUri, $product.imageUrl)) -UseBasicParsing
   [pscustomobject]@{
       CoverMatches = $product.imageUrl -eq $product.images[0].url
       Status = $image.StatusCode
       ContentType = $image.Headers['Content-Type']
   }
   Invoke-RestMethod ([uri]::new($baseUri, 'api/analytics/dashboard?period=custom&from=2026-10-12&to=2026-10-18')) -Headers $headers
   ```

### Chạy demo R2

Dùng một database demo trống khác, bucket R2 test có quyền ghi/xóa và public base URL đã bật cho bucket đó. Đặt cấu hình qua biến môi trường, user-secrets nếu đã cấu hình, hoặc `backend/.env` đã được git ignore trong Development.

| Tên biến | Giá trị |
| --- | --- |
| `ObjectStorage__Provider` | `R2` |
| `ObjectStorage__R2__AccountId` | Account ID Cloudflare của môi trường test |
| `ObjectStorage__R2__AccessKeyId` | Access key của bucket test |
| `ObjectStorage__R2__SecretAccessKey` | Secret key của bucket test |
| `ObjectStorage__R2__BucketName` | Tên bucket test |
| `ObjectStorage__R2__PublicBaseUrl` | URL HTTPS public của bucket test hoặc domain public đã nối tới bucket |

Chạy API/client như với Local sau khi chọn provider R2. Lấy `imageUrl` từ product detail và mở nguyên URL HTTPS bằng trình duyệt hoặc `Invoke-WebRequest`. Kiểm tra MIME, hình hiển thị và số object sau khi seed lại. Credential chỉ nằm trong cấu hình cục bộ. Nếu file biến mất nhưng DB còn tham chiếu, khôi phục object theo key cũ từ bản sao, hoặc thay ảnh qua Catalog đối với gallery hiện hữu. Nếu commit báo kết quả chưa xác định, kiểm tra tham chiếu `product_images.object_key` với database tương ứng trước khi dọn object của lần chạy.

**Trạng thái R2:** chưa chạy với bucket thật do thiếu môi trường R2 test. Unit test đã xác nhận URL HTTPS do provider trả về được giữ nguyên trong gallery và cover.

### Actual live trên database demo mới

Chạy ngày **18/11/2026** bằng Kestrel trên cổng 5297, với PostgreSQL demo mới đã migrate và seed, Local storage và JWT của `manager1`. SHA-256 của API dùng cho phiên live là `C8D1E6F87B3DA2D3A2771BF1C291CEC7A7B21A1E6FC46FCE25D85BEF545C8899`. Kỳ custom đọc từ `2026-10-11T17:00:00Z` đến trước `2026-10-18T17:00:00Z` theo UTC+07, tức hết ngày 18/10. Lần cố định kết thúc ở 12:00 ngày 18/10 theo giờ Việt Nam. Fixture không có đơn sau 12:00 hôm đó nên hai lần đối soát cho cùng doanh thu và số đơn kỳ custom.

| Kỳ | Biên ngày UTC+07 | `asOf` API (UTC) | Expected doanh thu / đơn | Actual doanh thu / đơn | Điểm ngày |
| --- | --- | --- | ---: | ---: | ---: |
| `today` | 18/11/2026 | `2026-11-18T05:55:47.920706Z` | 0 / 0 | 0 / 0 | 1 |
| `last7Days` | 12–18/11/2026 | `2026-11-18T05:55:48.119261Z` | 0 / 0 | 0 / 0 | 7 |
| `thisMonth` | 01–18/11/2026 | `2026-11-18T05:55:48.210231Z` | 0 / 0 | 0 / 0 | 18 |
| `custom` | 12–18/10/2026 | `2026-11-18T05:55:48.246998Z` | 136.080.000 / 20 | 136.080.000 / 20 | 7 |

Cả bốn response HTTP thành công và có snapshot gồm 20 sản phẩm hoạt động, 7 dòng tồn thấp. Ba kỳ tháng 11 có doanh thu từng ngày bằng 0 và danh sách đơn rỗng. Kỳ custom có đủ bảy điểm theo bảng tay, 14/10 bằng 0, đơn đầu tiên `DEMO-202610-0011`. Catalog có 20 file Local, ảnh mở thử trả **HTTP 200, image/png, 4.543 byte** và URL trùng ảnh bìa trong gallery.

**Dashboard desktop.** Cửa sổ Uno Skia/Win32 chạy với preview tắt. Bộ kiểm tra tạm đăng nhập `manager1`, mở Dashboard qua Shell, điền hai ô ngày và dùng lệnh gắn trên nút **Áp dụng**. Kết quả có `source=Live`, `asOf=2026-11-18T06:00:48.9676630+00:00`, doanh thu 136.080.000 ₫, 20 đơn, 20 sản phẩm hoạt động, 7 tồn thấp và 7 điểm ngày. Đã kiểm nội dung doanh thu và nhãn kỳ hiển thị trong cây giao diện. Sau khi ghi kết quả Pass, probe gặp exception ở luồng render OpenGL khi gọi thoát. SHA-256 bản probe là `1373524CB277ED8617A1C8FD4A31B5A53C0E0655A434E80D6648CBB6E1DF36C9`. Sau khi dỡ probe, build desktop có 0 warning, 0 error và SHA-256 `54493960AD655A5C35A56C0BD18A67D2090D7E2AEEB483BD476330D6C3A38D45`.

## Đối soát giao dịch POS live

Trên build chứa POS/ca và API tìm khách cho Sales, hai ca HTTP kiểm chứng doanh thu ngày Việt Nam sau commit và dữ liệu giữ nguyên sau 409. Lượt desktop trên Kestrel kiểm chứng bán khách lẻ, đóng ca và Manager thấy đúng đơn mới. Luồng từ phiếu nhập đến Dashboard còn chờ receiving của Q. Vinh vào `dev`.

### Build và phạm vi

Lượt kiểm chứng ngày 18/11/2026 dùng .NET SDK `10.0.404`. SHA nền `e587ee787a42a719dc72ff4d4efdf5f99b086036` đã chứa hai commit Dashboard Manager và demo data, cùng `dev` mới nhất tại thời điểm fetch (`6c5d8c567bc69deebeb41e9c99bbce9814cec9bc`). Lớp test mới của Bình được build cục bộ trên SHA này. SHA-256 bản Debug tham gia chạy HTTP:

| Artifact | SHA-256 |
| --- | --- |
| `TechStore.Api.dll` | `828DC01839695C137A2920E0702381A47F534A458446D6459C1E23E9E81AAC2B` |
| `TechStore.UnitTests.dll` | `37052B1A80B46BBDDE9530EC76D3B0E40DC01D76130B6B693884B92DADFE5B00` |

Endpoint đã chạy gồm login, tìm sản phẩm qua `GET /api/products`, dò IMEI qua `GET /api/inventory/serials`, tìm khách qua `GET /api/customers` bằng Sales, mở và đóng ca qua `/api/pos/shifts`, checkout qua `/api/pos/checkout`, đọc lại đơn qua `GET /api/orders/{id}` và Dashboard qua `GET /api/analytics/dashboard` bằng Manager. Phân quyền Dashboard có bằng chứng trong [bàn giao Dashboard Manager](#dashboard-manager).

### Kiểm chứng HTTP trên PostgreSQL

`IdentityPostgresApi` tạo database riêng cho từng ca, áp migrations và seed qua API host. Login dùng JWT thật. Test chỉ cố định đồng hồ ca, checkout và thời điểm snapshot Analytics. Lượt này dùng thiết bị `InStock` từ fixture. DB được đọc lại bằng context độc lập để kiểm tra dữ liệu đã lưu.

Đặt `TECHSTORE_TEST_CONNECTION` thành chuỗi kết nối PostgreSQL test có quyền tạo database riêng. Từ gốc repo, có thể ghi lại log kết quả bằng PowerShell:

```powershell
New-Item -ItemType Directory -Path backend/obj/m3-live-flow -Force | Out-Null
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter FullyQualifiedName~DashboardLiveFlowPostgresHttpTests --logger 'console;verbosity=detailed' *> backend/obj/m3-live-flow/api-postgres.log
```

Lượt ghi nhận đạt **2 Pass, 0 Fail, 0 Skipped**. Log cục bộ tại `backend/obj/m3-live-flow/api-postgres.log` ghi HTTP, dữ liệu read-back và snapshot. Các trích đoạn bên dưới giữ ID thử nghiệm và không in JWT, mật khẩu hay chuỗi kết nối.

#### Cash thành công, ghi doanh thu vào ngày Việt Nam

Đầu vào gồm một iPad `DEMO-TAB-IPAD-AIR`, IMEI fixture `490000000000004`, khách `Demo Customer 01` tra bằng `cashier1`, tiền đầu ca 2.000.000 ₫ và tiền nhận 20.000.000 ₫. Đơn `ORD-202610-0001` có ID `be903d21-5824-45a2-a92d-aa946d96cfdd`; ca `836ed425-a916-4823-8401-99166abe9f61`. Kỳ tùy chọn là 20–21/10/2026 theo UTC+07.

| Hạng mục | Expected | Actual |
| --- | --- | --- |
| Mở ca, checkout, đọc đơn và đóng ca | Ca Open, checkout 201, đơn lưu Completed/Cash, ca Closed | Đạt. Đơn liên kết đúng Sales, khách, ca và IMEI. Đóng ca trả 200. |
| Thời điểm | PaidAt và CompletedAt bằng nhau. UTC và UTC+07 khác ngày | Cả hai cùng `2026-10-20T17:01:00Z`, tức 00:01 ngày 21/10 giờ Việt Nam. |
| Giá, giảm và VAT | Giá 16.990.000 ₫, giảm dòng 90.000 ₫, giảm đơn 0 ₫, subtotal 16.900.000 ₫, VAT 10% là 1.690.000 ₫ | Các trường đã lưu khớp. Tổng **18.590.000 ₫**, tiền thừa **1.410.000 ₫**. |
| Tồn và serial | Tồn 3 → 2, giữ chỗ 1 → 1, IMEI Sold | Khớp. Bảo hành từ lúc hoàn tất đến `2027-10-20T17:01:00Z`. Snapshot giá vốn 14.500.000 ₫. |
| Lịch sử kho | Một dòng Sale, lượng −1, tồn sau 2 và đúng actor/khóa nguồn | Khớp. Movement ID `8ff4539a-6eb7-47e0-a7c1-2f9efd8a5bfb`. |
| Tiền ca | Cash tăng bằng tổng lưu. Tiền dự kiến và thực đếm 20.590.000 ₫, chênh lệch 0 | Khớp. Ca đóng lúc `2026-10-20T17:02:00Z`. |
| Dashboard | Thêm 1 đơn và 18.590.000 ₫. Đơn mới nằm trong 5 đơn gần nhất | 0 → 1 đơn, 0 → 18.590.000 ₫. Danh sách có đúng một dòng cho mã đơn mới. Sản phẩm hoạt động 20, tồn thấp 7. |
| Doanh thu ngày | 20/10 giữ 0, 21/10 tăng 18.590.000 ₫. Tổng các ngày bằng summary | Khớp ở cả hai snapshot. `asOf` trước `2026-10-20T17:00:30Z`, sau `2026-10-20T17:03:00Z`. |

#### IMEI đã Sold, khóa checkout mới

Ca độc lập tạo một đơn thành công để bán đúng IMEI fixture trên. Sau đó giữ ca `4858f89c-2e60-4cc9-9495-645bbbfb34d2` ở trạng thái Open và thử lại với khóa mới, cùng khách và cùng số tiền đủ. Đơn gốc ID `4dd08d53-e14c-42cf-b528-632c84b26ead`.

| Expected | Actual |
| --- | --- |
| HTTP 409 vì thiết bị Sold | 409. Log: `Line 1 (DEMO-TAB-IPAD-AIR): Device is Sold; expected InStock.` |
| Khóa lỗi không lưu, đơn/dòng/movement giữ nguyên | Không tìm thấy khóa lỗi. 21 order, 21 line và cùng các ID movement trước/sau. |
| Stock, bảo hành và ca giữ nguyên | Tồn 2, giữ chỗ 1, Sold, bảo hành giữ nguyên, ca Open và Cash 18.590.000 ₫. |
| Summary, daily, recent và low stock giữ nguyên | 1 đơn, 18.590.000 ₫, daily 0 / 18.590.000 ₫, cùng đơn và các dòng tồn thấp. `asOf` tiến từ 17:02 đến 17:03 UTC. |

Đây là ca xung đột tuần tự.

### Kiểm chứng desktop với Kestrel

Lượt ngày 18/11/2026 dùng Windows Uno Skia (`net10.0-desktop`), preview tắt, Kestrel `127.0.0.1:5298` và một PostgreSQL demo mới do API migrate/seed. Probe tạm được đưa vào bản build desktop qua MSBuild target trong thư mục build. Probe dùng màn hình, binding và command hiện có, và tệp phiên riêng. SHA-256 của `TechStore.Client.dll` bản probe là `E91621F9077A0F5B0E5B595091D9CF31C4E1E673C4ED97F4788CD1DFC71E0B86`. API dùng artifact ở bảng build phía trên.

Đầu vào gồm IMEI fixture `490000000000004`, khách lẻ, tiền đầu ca 2.000.000 ₫ và tiền nhận 20.000.000 ₫. Phiên desktop không nhập giảm dòng. Với giá 16.990.000 ₫ và VAT 10%, tổng mong đợi là 18.689.000 ₫. Đơn `ORD-202611-0001` có ID `5d5414da-d168-48c1-ab3d-45f29f25a3c7`, ca `064aaba5-0173-43d5-80e9-8ccf1d989e49`.

| Bước | Expected | Actual | Trạng thái |
| --- | --- | --- | --- |
| Manager mở kỳ 18/11 trên Dashboard | Nguồn Live, 0 đơn, 0 ₫ | Khớp tại `asOf=2026-11-18T07:44:43.5929460Z`. | Pass |
| Sales mở ca, quét IMEI, thanh toán Cash | Ca Open, giỏ có một dòng đúng IMEI, có biên nhận | Khớp. Completed lúc `2026-11-18T07:44:46.479016Z`, VAT 1.699.000 ₫, tổng 18.689.000 ₫ và tiền thừa 1.311.000 ₫. | Pass |
| Đóng ca với tiền thực đếm | Tiền dự kiến và đếm 20.689.000 ₫, chênh lệch 0 | Khớp. UI trở về trạng thái chưa mở ca. HTTP read-back riêng xác nhận Closed và số tiền. | Pass |
| Manager đọc lại cùng kỳ | Nguồn Live, thêm 1 đơn và 18.689.000 ₫ ngày 18/11, có mã đơn vừa bán | Khớp, đã xác nhận doanh thu và nhãn kỳ của UI tại `asOf=2026-11-18T07:45:17.0802100Z`. | Pass |
| UI receiving | Nhận IMEI trước bán | Build chưa có receiving. Lượt này dùng IMEI đã có từ seeder trên DB demo. | Chưa kiểm tra, Q. Vinh |
| Chọn khách trên POS desktop | Chọn khách đã tìm và lưu liên kết đơn | Build chưa nối bộ chọn vào POS. Lượt này bán khách lẻ. | Chưa kiểm tra, Đạt nối bộ chọn của Vy |

Log công cụ desktop nằm tại `backend/obj/m3-live-flow/desktop-evidence.log`. HTTP read-back của ca, đơn và Dashboard nằm riêng ở `backend/obj/m3-live-flow/desktop-read-back.json`. Probe kết thúc mã 0 sau khi ghi Pass. Chưa kiểm tra Windows native hoặc desktop nền tảng khác.

### Phụ thuộc bàn giao

| Mục | Trạng thái | Owner và điều kiện chạy lại |
| --- | --- | --- |
| Nhà cung cấp → phiếu nhận IMEI → POS → Dashboard | Chưa kiểm tra đầu-cuối. `dev` đã fetch chưa có controller receiving. | Q. Vinh merge receiving và migration vào `dev`. Bình chạy cùng build, bán đúng IMEI từ phiếu nhận qua API và lặp ca 409 trên SHA đó. |
| Đối chiếu tiền và ca 409 trên build có receiving (task 2.2) | Đã quan sát VAT 10% và tổng lưu 18.590.000 ₫ trong ca HTTP dùng IMEI fixture. Chưa chạy lại ca 409 và ca giảm cấp đơn khác 0 trên build có receiving. | Chờ Q. Vinh merge receiving rồi chạy lại ca 409 trên build mới. Ca giảm cấp đơn còn chờ Đạt nối POS với contract của Vy. Khi đủ phụ thuộc, đối chiếu tổng lưu, tiền ca và delta Dashboard theo cấu hình bàn giao. |
| Khách hàng qua Sales HTTP | Pass | API của Vy đã được gọi bằng `cashier1`. |

Tổng hợp baseline gồm **2/2 ca HTTP/PostgreSQL** và **1/1 lượt desktop** đạt trong phạm vi đã chạy. Không có ca fail hoặc skipped trong lượt HTTP có lọc. Checklist 2.1 và 2.2 đã chốt để archive với trạng thái **Chưa kiểm tra** cho các lượt cần build có receiving. Chạy bổ sung theo mục Risks / Trade-offs của design sau khi owner bàn giao.
