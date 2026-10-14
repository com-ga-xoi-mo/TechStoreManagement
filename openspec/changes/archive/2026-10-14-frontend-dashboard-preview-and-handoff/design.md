# Design

## Context

Xem `proposal.md` cho động cơ. Nguồn ưu tiên: `docs/MVVM_REPOSITORY_BASE_PROJECT.md` mục 6–11; Change 5 của `FRONTEND_FOUNDATION_PLAN.md`; Dev 6 Sprint 1 trong `MILESTONE_2_PLAN.md`. Hướng dẫn Feature-First cũ không đổi cấu trúc hiện có.

Đã kiểm tra source, tests và specs:

- `DashboardViewModel(IDialogService)` hiện chỉ thử pending/error/dialog; `DashboardPage` có bốn StatCard với dấu “—”, một EmptyStateControl và khối DEVELOPMENT. `ShellDiagnosticViewModelTests` phụ thuộc trực tiếp vào các commands này.
- `UiServiceCollectionExtensions` đăng ký Dashboard/Page transient; navigation tạo scope theo activation. `PageViewModel` có `RunLatestRequestAsync`, tracked work và generation guard; không cần framework lifecycle mới.
- `GalleryOptions` đọc `TECHSTORE_UI_GALLERY`, chỉ chấp nhận unset/0/1. App dùng EnvironmentVariables configuration và gallery có ưu tiên riêng lúc startup.
- Models, Repositories và ViewModels được source-link bằng glob trong Frontend.UnitTests; có thể test Dashboard production headless. UI DI vẫn cần runtime smoke riêng.
- Không tìm thấy Analytics implementation/DTO trong backend/shared hiện tại. `data-seeding` spec là baseline 5 sản phẩm/2 danh mục/2 khách/2 đơn Completed; `DataSeeder` bỏ qua toàn bộ khi đã có roles, dùng thời gian hiện tại và nhiều Guid ngẫu nhiên. Không xem đó là bộ seed đối soát mở rộng đã hoàn tất.
- Mẫu screen.png đã xem: bốn KPI, chọn Hôm nay/7 ngày qua/Tháng này, chart cột + đường, panel tồn thấp và bảng năm đơn. `code.html` có nút/link giả, dữ liệu mẫu và nhãn IMEI cả cho serial; không sao chép những sai lệch này.
- `SettingsControls` 8.2.250402 đang pin qua CPM. Guide hiện ghi Windows/WinApp CLI chưa verified, dù change trước đã được owner nghiệm thu. Archive và acceptance không thay bằng chứng runtime từng nền tảng.

## Goals / Non-Goals

**Goals:** preview độc lập API, DI dễ thay implementation, fixture xác định với ngữ nghĩa rõ, state/cancellation kiểm thử được và tài liệu bàn giao có owner cho từng phụ thuộc.

**Non-Goals:** chốt API/DTO bằng suy đoán; tổng hợp đơn hàng thật trên client; triển khai UC-STAT-01/02/03 hay UC-SEED-01 backend; sửa auth, seed hoặc package versions để mở rộng phạm vi. Demo không cấp quyền Admin và không thay chế độ Catalog API thật.

## Decisions

### 1. Bật demo ở composition root, mặc định chưa khả dụng

Thêm `DashboardPreviewOptions` trong `Services/Development`, đọc `TECHSTORE_DASHBOARD_DEMO`: unset/0 là tắt, 1 là bật, giá trị khác lỗi cấu hình an toàn giống GalleryOptions. Đọc một lần khi startup; thay mode cần restart, không có switch ẩn tự chuyển khi lỗi.

Đăng ký transient `IAnalyticsRepository` cạnh implementations trong `Repositories/Analytics`: `FixtureAnalyticsRepository` khi bật; `UnavailableAnalyticsRepository` khi tắt. Repository chưa khả dụng trả kết quả có kiểu rõ ràng, không ném lỗi mạng giả và không trả snapshot zero. Đây là hai nguồn có hành vi khác nhau, không phải lớp service chuyển tiếp. Options singleton bất biến; VM/Page transient trong scope activation. Constructor không I/O, không tạo provider thứ hai; đăng ký phần thuần C# trong extension được test headless, UI wiring trong extension hiện có.

Mặc định Dashboard: “Thống kê chưa tích hợp”, KPI “—”, không vẽ chart số 0; điều khiển kỳ/refresh không khả dụng. Demo: banner liên tục “Dữ liệu minh họa — chưa kết nối Analytics API”, ngày tham chiếu fixture và kỳ thực tế; không thay nhãn Shell chưa đăng nhập. `TECHSTORE_UI_GALLERY=1` vẫn mở gallery, dù demo=1; không mở Dashboard hoặc gọi repository ngầm. Khi trở sang Sản phẩm trong Shell, Catalog vẫn gọi API thật; lỗi Catalog không lấy fixture Analytics thay thế.

Không chọn auto-demo khi API down vì làm sai ngữ nghĩa dữ liệu. Không thêm HTTP AnalyticsRepository giả route cho endpoint chưa tồn tại.

### 2. Hợp đồng client nhỏ, tách khỏi contract backend

Đề xuất trong client, chưa phải shared DTO hay wire contract:

- `DashboardPeriod`: Today, Last7Days, ThisMonth; default Last7Days.
- `DashboardQuery`: period; source trả lại bounds và snapshot timestamp thực tế.
- `IAnalyticsRepository.GetDashboardAsync(DashboardQuery, CancellationToken)` trả `DashboardReadResult` với Unavailable hoặc Available + `DashboardSnapshot`; không dùng null/zero để mã hóa Unavailable.
- `Models/Analytics`: snapshot chứa kỳ, as-of, source kind (Demo), `DashboardSummary`, daily revenue points, low-stock rows và recent-order rows. Decimal cho tiền, DateOnly cho ngày và DateTimeOffset cho thời điểm; collections không mutable dùng chung qua activations, không chứa Brush/Geometry/command.
- Summary gồm revenue, orderCount, productCount, lowStockCount và optional comparison; recent orders tối đa 5 (id hiển thị riêng demo, customer display, product summary, optional serial label, amount, status); low-stock rows tối đa 5 (product/variant display, available quantity, threshold). Năm dòng không quyết định tổng KPI.
- Aggregate một snapshot để không trộn kỳ giữa widgets; endpoint tương lai có thể một hoặc nhiều, backend chưa quyết định. Repository sẽ điều phối/mapping sau khi contract được chốt; VM không phụ thuộc topology HTTP.

Fixture baseline có ngày tham chiếu cố định **2026-10-14, Asia/Ho_Chi_Minh (UTC+07)**. Today = ngày đó; Last7Days = 08–14/10; ThisMonth = 01–14/10. Hiển thị kỳ cụ thể để không khiến người dùng hiểu “Hôm nay” là ngày chạy máy. Các bộ trả về là aggregate cố định, không download hay tính từ orders thật; tất cả số tiền daily cộng bằng revenue của kỳ, ngày thiếu có điểm zero, so sánh phần trăm null khi chưa có cơ sở/previous=0. Refresh cùng kỳ không random hóa. Có fixture empty/all-zero/long-text và lỗi/delay qua fake repository hoặc khu diagnostics có nhãn; không ảnh hưởng route Catalog.

Quy ước **chỉ cho demo**: revenue của Completed, count đơn phát sinh trong kỳ, productCount là số product records (không số máy/variants), tồn thấp là số product/variant balances có available = quantity − reserved dưới threshold, tồn thấp/sản phẩm là snapshot tại as-of, recent orders trong kỳ theo thời gian giảm dần. Không tự trừ refund hay diễn giải margin. Optional comparison không có thì ẩn tỷ lệ, không hiển thị 0% giả. Product card dùng “sản phẩm”; không đưa count danh mục vào subtitle khi không có field. Labels trạng thái dùng enum `OrderStatus` hiện có; unknown không gán Completed. Không dùng id demo để điều hướng đến dữ liệu thật.

### 3. State và diagnostics tái sử dụng foundation

VM dùng CommunityToolkit.Mvvm observable/Task-returning commands, constructor injection và lifecycle PageViewModel. Load chỉ khi activate; chọn kỳ/refresh chạy `RunLatestRequestAsync` để supersede. Commit toàn snapshot cùng applied period; protect success/error/finally bằng activation/generation. Đổi kỳ xóa nội dung kỳ cũ trong khi loading, tránh hiển thị KPI cũ dưới nhãn kỳ mới; lỗi giữ requested period để Retry, không fallback sample khác. Đây là quyết định riêng Dashboard, không đổi snapshot policy Catalog.

Unavailable là cờ/kết quả Dashboard riêng với shared Empty presentation, thông điệp khác Empty thành công; không sửa toàn bộ enum PageContentState. Available với KPI hợp lệ là Content kể cả tổng 0; mỗi danh sách rỗng có empty riêng. Error và Loading dùng shared state. Rời Dashboard/close hủy mọi task và dispose scope sau khi chúng kết thúc; quay lại tạo activation mới/kỳ default. Theme/resize không tải lại repository.

Di chuyển probe dialog/pending/error hiện có thành ViewModel/khu DEVELOPMENT trong gallery được bật bằng `TECHSTORE_UI_GALLERY=1`; không thêm root menu thứ bảy. Giữ các fake/dialog assertions, chỉnh `ShellDiagnosticViewModelTests` theo nơi mới; các tests PageActivationLease/navigation tiếp tục là regression. Không xóa tests chỉ vì Dashboard thay constructor. Code-behind chỉ wiring, layout/chart geometry và accessibility cần thiết, không nghiệp vụ/HTTP.

### 4. UI, chart và compatibility

Tái sử dụng semantic resources/font/icon hiện có. Layout ở rộng: 4 KPI; chart chiếm khoảng 2/3, low-stock 1/3; recent orders phía dưới. Ở hẹp KPI 2 rồi 1 cột, panels xếp dọc, orders thành row/card đọc được đủ thông tin; không cắt text bằng chiều cao cố định. Bộ chọn kỳ dùng RadioButtons hoặc ComboBox chuẩn tùy bề rộng. Rows có bounds tối đa 5 nên có thể dùng ItemsControl không cuộn riêng trong page ScrollViewer; nếu chọn ListView phải giới hạn viewport và tránh lồng ScrollViewer tùy tiện. Không thêm DataGrid.

Mặc định dùng WinUI Grid/Border/Shapes (cột và Polyline nối điểm, không smoothing làm méo giá trị), bao gồm axis/labels/peak annotation từ series fixture. Không mặc định WCT có chart. Geometry thuộc View, tính scale từ series có guard empty/all-zero/single-point; decimal chỉ đổi double tại tọa độ vẽ. Có bảng dữ liệu ngày–VND tương đương truy cập bằng keyboard và screen reader; không dựa hover/màu để đọc trị số. Đường thẳng nối điểm là sai khác có chủ đích với đường cong mẫu. Không xây chart engine tương tác, zoom, export hay package mới.

Ưu tiên control đã có: StatCard, EmptyStateControl, StatusBadge, WinUI InfoBar; WCT SettingsControls giữ nguyên. Nếu restored references không hỗ trợ API vẽ đã chọn, dùng cột bằng Grid/Border với bảng số liệu, ghi sai khác và đưa vào owner review; không nâng Uno hay thay Material. Mọi library mới nếu thực sự cần phải có lý do, bằng chứng hai target và pin CPM trước khi nhận.

Nguồn chính thức tham khảo (không phải chứng minh runtime Uno 6.7.30): [Uno Shapes & Brushes](https://platform.uno/docs/articles/features/shapes-and-brushes.html), [Uno WCT 8.x](https://platform.uno/docs/articles/uno-community-toolkit-v8.html), [WCT migration](https://github.com/CommunityToolkit/Windows/wiki/Migration-Guide-from-v7-to-v8). Tài liệu WCT hướng dẫn SettingsCard nhưng không bảo đảm mọi control trên mọi platform.

Khi apply dùng `winui-design` để tra samples/API; WinApp CLI ≥0.7 trên Windows, `find-ui` và `find-api` scoped thư mục app đã restore. Trên Linux dùng restored refs/local samples để tiến hành, lưu kết quả riêng; thiếu Windows CLI không chặn phần Linux nhưng không được tick task verification Windows. Planning này không viết XAML và không tuyên bố các lookups đã chạy.

Link “Xem tất cả 128 đơn”, click order id và low-stock drilldown trong mẫu bị bỏ vì feature thật chưa có; rows demo chỉ đọc. Không làm action giả dẫn đến id demo hay trang trống. Không sao chép ảnh avatar, số điện thoại/IMEI mẫu thành dữ liệu khách hàng thật.

### 5. Ma trận nhu cầu backend và seed

Ở apply viết `docs/ANALYTICS_DATA_REQUIREMENTS.md`, mọi hàng có UC, field/type/unit/nullability, scope thời gian, ví dụ fixture, nguồn đã quan sát, câu hỏi, owner, status và evidence xác nhận. Trạng thái: Observed / Proposed / Pending / Confirmed; chỉ Confirmed khi có nguồn quyết định, không vì preview chạy được.

| UC / dữ liệu | Nhu cầu và phụ thuộc còn mở | Owner cần phản hồi |
| --- | --- | --- |
| STAT-01 revenue | Milestone yêu cầu chỉ Completed; cần chốt timestamp (created/settled), timezone/range inclusive-exclusive, discount/tax/refund, currency/rounding | Dev 6 + Dev 4 Orders |
| STAT-01 counts | Số đơn tất cả trạng thái hay Completed; product hay variant; active-only; snapshot hay trong kỳ; comparison period và previous=0 | Dev 6 + Dev 2 Catalog + Dev 4 |
| STAT-02 low stock | Quantity/reserved/available, granularity product+variant, threshold/config, zero/inactive, order/limit/total | Dev 6 + Dev 3 Inventory |
| STAT-02 recent orders | Time ordering/ties, limit, product summary nhiều dòng, khách vãng lai/null, trạng thái và serial/IMEI optional; dữ liệu nhạy cảm cần thiết | Dev 6 + Dev 4 + Dev 5 Customers |
| STAT-03 trend | Theo ngày và timezone, zero-fill ngày thiếu, cùng định nghĩa revenue KPI; đây là chuẩn bị preview, không hoàn thành API STAT-03 | Dev 6 + Dev 4 |
| Endpoint/security | Route, request/DTO shape, range limits, errors, freshness/as-of, Admin policy và auth integration | Dev 6 + Dev 1 Identity |

Không bịa endpoint hoặc sửa shared DTO. API integration Analytics được ghi **Not implemented / blocked by confirmed contract + endpoint + auth + seed**, khác unit test fixture pass.

`docs/DEMO_SEED_REQUIREMENTS.md` ghi baseline đã quan sát và mục tiêu riêng Sprint 2: ≥20 SP, ≥5 DM, ≥10 KH, ≥20 đơn, ≥50 IMEI. Yêu cầu đề xuất: deterministic ids/time anchor, repeat-run không tăng counts, khôi phục partial seed, lịch sử nhiều ngày gồm Completed/Pending/Processing/Cancelled/refund để đối soát, zero-sales dates, tồn thấp/zero/reserved, JSONB/variant/serial consistency, ledger/payment/order snapshots. Expected KPIs tính từ dataset seed cuối cùng trên server và được ghi bảng expected–actual; không ép số mẫu Dashboard bằng số seed. Đánh dấu roles-only skip của seeder hiện tại là rủi ro mở rộng; đề xuất sửa ở Sprint 2, không sửa trong change này. Dev 1 quản baseline/migrations, Dev 6 mở rộng; phối hợp Dev 2–5 về invariants.

### 6. Bàn giao và bốn loại bằng chứng

Cập nhật `FRONTEND_FOUNDATION_GUIDE.md` làm index năm change, dùng Catalog thật làm ví dụ View → VM → Repository → IApiClient; namespace, DI lifetimes, activation/work tracking, errors/session, route/dialog/theme/control và checklist thêm feature. Ma trận Dev 1 auth/settings; Dev 2 Catalog CRUD; Dev 3 Inventory; Dev 4 Orders; Dev 5 Customers; Dev 6 Analytics/seed chỉ rõ file cần mở rộng và contract chờ ai. Không tái ghi lịch sử acceptance cũ thành runtime mới.

`FRONTEND_DASHBOARD_HANDOFF.md` có source matrix (Dashboard fixture, Catalog live, các module placeholder), flags/lệnh launch, fixture version/anchor, build hash + commit/dirty state, target/OS/renderer, packages, viewport DIP/raster scale/theme, ảnh mẫu–thực tế và sai khác. Bốn trạng thái riêng: tests; platform verification; owner visual acceptance; backend confirmation/integration.

- Headless: options/DI, nguồn fixture vs unavailable/no HTTP, zero/null/comparison, ranges, snapshot integrity, empty/error/retry, late response/cancel/close, navigation/dialog regressions. Không tuyên bố unit test là Analytics integration.
- Linux: build/run net10.0-desktop default/demo/gallery, API tắt; Catalog thật regression với API bật và lỗi thật khi tắt. Rerun opt-in Catalog integration có preflight dataset, không sửa backend nếu môi trường thiếu.
- Windows: task riêng build/run net10.0-windows10.0.26100, WinApp lookups, controls/runtime, native Contrast/UIA/IME. Chưa có môi trường thì unverified và task unchecked; Linux pass không thay thế. Yêu cầu bỏ Windows ở change Catalog trước không tự mở rộng sang foundation cross-platform của Change 5.
- Visual: 1440×1000, 1024×768, 800×600 DIP; Light/Dark/System; text scale 100/150/200%; kỳ/ngày chart, long Vietnamese names, zero/empty/errors, keyboard/accessible values. Owner chỉ tick task riêng khi xác nhận build/target/ngày và ngoại lệ. Acceptance bốn change trước không áp dụng tự động.

## Risks / Trade-offs

- Model local bị hiểu nhầm contract backend → nhãn Proposed và không tạo shared DTO/route; owner/status per field.
- Dữ liệu khác nhau giữa widgets/kỳ → snapshot atomic, fixture invariant tests, nhãn as-of/kỳ rõ.
- Chart custom tăng accessibility cost → chỉ primitives không tương tác, có bảng trị số tương đương và kiểm tra zero/resize/theme.
- Windows chưa có evidence → verification riêng, không dùng archive cũ hay Linux screenshot để tick.
- Backend chưa xác nhận → giữ ledger pending, không chặn hoàn thành phần preview/viết nhu cầu; không đóng milestone Analytics/seed thật.
- Guide cũ có acceptance notes chưa đồng bộ → liên kết archive/record gốc và ghi trạng thái theo bằng chứng, không xóa lịch sử.

## Migration Plan

1. Thêm Models/options/repositories thuần C# và tests; default off.
2. Nối Dashboard VM/UI vào factory hiện có, chuyển diagnostics và regression; không migration DB.
3. Hoàn tất tài liệu, checks nền tảng và visual acceptance độc lập.
4. Rollback UI/wiring về Dashboard placeholder; bỏ registrations/flag mới. Catalog/core/design system giữ nguyên; không rollback dữ liệu.

## Open Questions

Các câu hỏi contract trong bảng mục 5 được phép còn Pending sau preview và phải được chốt trước live Analytics Sprint 2. Không cần câu trả lời đó để thực hiện fixture policy đã chốt ở mục 2. Sổ phụ thuộc được cập nhật bằng phản hồi thực tế; không yêu cầu agent tự gửi tin cho các dev.
