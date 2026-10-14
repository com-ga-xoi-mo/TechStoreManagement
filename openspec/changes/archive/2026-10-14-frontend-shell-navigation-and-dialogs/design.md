# Design

## Context

Xem `proposal.md` về mục tiêu. Nguồn quyết định là `docs/MVVM_REPOSITORY_BASE_PROJECT.md` (đặc biệt mục 6–9) và Change 3 trong `docs/FRONTEND_FOUNDATION_PLAN.md`, ưu tiên hơn hướng dẫn Feature-First cũ.

Hiện App tạo ServiceCollection/provider và Window, gán gallery qua DI khi `TECHSTORE_UI_GALLERY=1`, còn mặc định gọi `Frame.Navigate(typeof(MainPage))`. `UiServiceCollectionExtensions` chỉ đăng ký gallery; MainPage chưa có navigation thực. Tests là project net10.0 headless, source-link production C# và không reference project UI. Resources đã có Light/Dark/HighContrast, font/assets offline cùng StatCard, SearchBoxControl, EmptyStateControl, StatusBadge. CPM pin SettingsControls 8.2.250402; Change 2 đã được chủ dự án nghiệm thu. Comment CPM còn ghi Windows pending không thay thế acceptance record đã chốt, cũng không chứng minh Shell mới đã được kiểm tra.

Đã xem `screen.png`, `code.html` trong mẫu Dashboard và `fluent_retail_command/DESIGN.md`. HTML quy định sidebar 250px, năm mục chính và Cài đặt ở footer, thương hiệu trên cùng, tài khoản dưới cùng. Screenshot minh họa hierarchy, không phải yêu cầu sao chép clipping hay toàn bộ số liệu/chart/table. Resources đã hoàn thành là baseline khi DESIGN.md và HTML có chênh lệch.

## Goals / Non-Goals

**Goals:** Một đường lắp feature có constructor DI, ownership rõ, state/lifecycle kiểm thử được không cần UI thread; UI adapter giữ toàn bộ phụ thuộc WinUI. Hành vi khung có thể nghiệm thu offline trước backend.

**Non-Goals:** Navigation history/deep links, cache feature, persistence settings, multi-window support, phân quyền client, login/logout thật, analytics/chart engine hoặc repository chuyển tiếp. Không đổi .NET 10/Uno 6.7.30, không chuyển MVUX/Material, không thay composition root bằng Generic Host.

## Decisions

### 1. Shell development startup và route map

Default startup tạo Shell qua DI và gán vào root content; giữ validation cấu hình foundation, gallery flag 1/0/absent/invalid như hiện tại. Gallery bật thì không tạo Shell. Shell hiện nhãn ngắn “Bản phát triển — chưa đăng nhập”, tài khoản placeholder không giả mạo user thật và không ghi token/session. Các entry Admin là khung thiết kế, không tuyên bố RBAC đã hoàn thành.

| Route thuần C# | View / ViewModel trong thư mục nghiệp vụ tương ứng | Nhãn |
| --- | --- | --- |
| Dashboard | Views/Dashboard/DashboardPage; ViewModels/Dashboard/DashboardViewModel | Tổng quan |
| Products | Views/Catalog/ProductListPage; ViewModels/Catalog/ProductListViewModel | Sản phẩm |
| Inventory | Views/Inventory/InventoryPage; ViewModels/Inventory/InventoryViewModel | Danh mục và Kho |
| Orders | Views/Orders/OrderListPage; ViewModels/Orders/OrderListViewModel | Đơn hàng |
| Customers | Views/Customers/CustomerListPage; ViewModels/Customers/CustomerListViewModel | Khách hàng |
| Settings | Views/Settings/SettingsPage; ViewModels/Settings/SettingsViewModel | Cài đặt |

Shell nằm ở Views/Shell và ViewModels/Shell. Dashboard là initial route; Settings chỉ xuất hiện một lần ở footer. Không đưa POS/Identity thành mục thứ bảy. Trang khung dùng EmptyStateControl giải thích chưa có nghiệp vụ; không hiển thị doanh thu/số đơn giả. Dashboard có thể dùng StatCard giá trị “—” để giữ hierarchy mẫu, không dựng biểu đồ giả. Không gắn action nghiệp vụ chưa hoạt động.

Lựa chọn này thay MainPage stub vì phù hợp Change 3; delta gallery sửa đúng requirement Default launch thay vì để hai spec mâu thuẫn.

### 2. Navigation adapter và constructor injection

Đặt route identifier, metadata thuần dữ liệu và INavigationService cạnh implementation trong Services/Navigation. ViewModel chỉ biết route và async result, không nhận Page, Frame, XamlRoot, UI Type hay IServiceProvider. Page factory ở UI boundary có whitelist map route sang Page đã đăng ký; mỗi constructor nhận VM trước InitializeComponent để x:Bind sẵn sàng. Adapter gán content do factory tạo, không trông cậy Frame.Navigate tự constructor-inject và không dùng Frame back stack. Không tạo generic navigation framework.

Đăng ký UI trong UiServiceCollectionExtensions, giữ foundation registration không phụ thuộc WinUI. Một window scope sở hữu Shell/navigation/dialog/theme; mỗi activation tạo scope con độc lập qua scope factory cho page/VM transient. Scope con DI không tự kế thừa instance scoped của window: window-owned services dùng registration singleton cho ứng dụng một cửa sổ này, page scopes dùng chung instance ấy. App hủy window operations trước dispose provider; navigation lease giữ và dispose page scope đúng một lần sau khi load đã hoàn tất/hủy. Shell không capture repository/typed HTTP client. Khi thêm multi-window sẽ phải thay ownership singleton, ngoài phạm vi hiện tại.

Route lạ hoặc page construction lỗi: giữ route/title/content trước đó và báo lỗi an toàn; không cancel trang hiện tại trước khi factory thành công. Chọn lại route hiện tại là no-op; chuyển route thành công cập nhật selected item/title/content thống nhất. Không public service locator; resolution chỉ ở composition root/page factory.

### 3. Lifecycle và state có chủ đích

Mỗi lần vào route khác tạo VM mới, constructor không I/O. Adapter kích hoạt load sau khi view đã gắn; mỗi activation chỉ load một lần, không load lại do resize/theme/repeated Loaded. Rời trang hoặc đóng window: cancel lifetime token, ngắt subscriptions, vô hiệu hóa activation generation trước khi cho trang mới load. Không chặn chuyển trang để chờ request cũ không hợp tác cancellation.

Dùng ObservableObject và async RelayCommand phù hợp, kết nối token activation với load/retry token. Mọi cập nhật state sau await (kể cả catch/finally) kiểm tra generation/token; lỗi/cancel cũ không được ghi đè state mới. Expected cancellation không hiện lỗi. Lỗi hiện tại vào error với retry; retry xóa error và tạo load generation mới, không chạy chồng một load hiện hành. Chuỗi state Loading → Empty/Content/Error; placeholder bình thường kết thúc Empty. Không tạo HTTP fake production chỉ để trình diễn loading: tests dùng controlled fake loader/VM qua lifecycle seam, UI có khu “Kiểm tra khung” ghi nhãn phát triển để kích hoạt các trạng thái minh họa và dialog.

State trang reset khi rời rồi quay lại; không cache Page/VM. Theme giữ trong phiên qua shared presentation state; đổi theme/resize/mở dialog không reset state hiện tại. Đây là lựa chọn nhỏ nhất trước khi feature xác định yêu cầu giữ filter. Bỏ lựa chọn cache mọi trang vì dễ giữ dependency và dữ liệu session cũ.

UI synchronization thuộc adapter/boundary: bắt đầu lifecycle trên UI context, marshal cập nhật UI khi cần; headless core không hard-code DispatcherQueue. Tất cả tasks được observe; async void chỉ tại event handler framework có catch. Nếu load phớt lờ cancel, giữ lease đến khi task kết thúc, không dispose dependency đang dùng; tách khỏi active UI ngay, log an toàn và không chờ vô hạn khi đóng app.

### 4. Dialog contract và root ownership

Services/Dialog chứa IDialogService với request text/title/button labels và result Confirmed/Cancelled/Dismissed; confirm và information là đủ. CancellationToken bị hủy hoàn tất bằng OperationCanceledException, phân biệt với người dùng bấm Hủy. Không cho VM truyền UI content hoặc focus element.

UI DialogService dùng ContentDialog; attach root sau Shell Loaded, detach khi teardown, giữ root chỉ trong UI layer. FIFO queue trong service bảo đảm tối đa một dialog đang hiện mỗi root. Có thể tách presenter interface thuần request/result để test queue bằng fake, production presenter giữ ContentDialog. Request trước root-ready bị từ chối bằng lỗi availability có kiểm soát (không treo queue); request khi closing bị cancel. Cancel khi queued loại request; cancel khi visible đóng dialog, trả task một lần, không mở dialog kế cho root đã chết. Page commands truyền lifetime token; điều hướng hủy dialog thuộc trang cũ.

Adapter lưu focus trước Show, restore khi element còn loaded trong trang hiện hành; nếu đã rời trang thì focus heading/nội dung trang mới, không focus element đã detached. Confirm dùng nhãn động từ tiếng Việt; Enter tuân default button an toàn, Escape đóng/hủy, Tab/Shift+Tab giữ trong modal. Không tự retry confirmation sau lỗi.

Chọn ContentDialog thay custom overlay để giữ keyboard/accessibility platform. XamlRoot và focus chỉ kiểm chứng qua runtime UI, không lấy headless queue test làm bằng chứng đầy đủ.

### 5. Settings, resources và responsive Shell

Giữ SettingsControls 8.2.250402 qua CPM, dùng SettingsCard cho lựa chọn System/Light/Dark và SettingsExpander cho thông tin ứng dụng; chỉ hiển thị thông tin không nhạy cảm. Theme state thuần C# giữ theo phiên (mặc định System mỗi lần chạy), adapter áp dụng RequestedTheme lên root và dialog; VM không dùng ElementTheme. Không tạo settings repository/persistence cho hai trường khung.

NavigationView và WinUI controls sẵn có, lightweight resource overrides; không custom template toàn phần. Sidebar expanded 250 DIP ở >=1100 DIP, compact ở 800–1099 DIP với toggle mở pane overlay; footer Settings/tài khoản vẫn truy cập bằng keyboard, icon có accessible name/tooltip. Content padding dùng tokens 24/16; heading “Tổng quan kinh doanh” theo mẫu, trang khác theo route. Ưu tiên ThemeResource, font/icon offline, không tải avatar từ URL HTML. Dùng initials/local icon cho tài khoản khung. Không sao chép màu hardcoded từ HTML hay đổi tokens toàn cục.

Mốc nghiệm thu viewport nội dung 1440×1000, 1024×768 và 800×600 DIP. Đo ActualWidth/ActualHeight và RasterizationScale; ảnh physical pixels không thay DIP. Resize thật cửa sổ và xác nhận layout nhận SizeChanged, không chỉ thay kích thước screenshot. Không cố định Width/Height root để giả resize. Kiểm tra tiếng Việt 100/150/200%, Light/Dark/System và Windows Contrast.

### 6. Verification và skill mapping

- `mvvm-toolkit-di`: route/page factory, constructor DI, scope ownership và shutdown.
- `mvvm-toolkit`: state, async commands, cancellation/retry, headless tests.
- `uno-platform`: API availability trên exact target, wiring desktop/native và build/run.
- `winui-design`: NavigationView/ContentDialog/Settings patterns, x:Bind, keyboard/theme/resize và review mẫu.

Trước XAML khi apply, WinApp CLI >=0.7: find-ui cho NavigationView, ContentDialog và SettingsCard/SettingsExpander; find-api từ restored frontend project để kiểm tra đúng references. Trên Linux nếu CLI không thực thi được, kiểm tra restored reference/source cùng official docs để tiến hành phần portable, ghi giới hạn và giữ task Windows CLI chưa hoàn tất; không suy luận Uno hỗ trợ từ WinUI sample. Không nâng package để chạy sample mới. Runtime Windows vẫn là bước riêng bắt buộc.

Tests source-link production pure navigation/lifecycle/dialog policy/ViewModels vào Frontend.UnitTests; UI implementations nằm file riêng không bị kéo vào headless suite. Dùng fake presenter/page activation và TaskCompletionSource điều khiển thứ tự thay sleep. Test A→B→A, old success/error/finally, same-route, unknown route/factory failure, cancellation khi close, queue/dialog cancellation và DI cleanup. Runtime bổ sung actual factory + x:Bind + focus/XamlRoot, Settings toolkit và shell appearance. Mỗi record ghi build id/hash hoặc commit kèm dirty diff, target/OS, command, DIP/scale/theme, expected/actual, ảnh/log; owner acceptance task riêng và không kế thừa acceptance Change 2.

## Risks / Trade-offs

- [Uno/Windows khác behavior root/focus] → kiểm tra cả Skia Linux và Windows native, giữ unverified nơi chưa chạy; không coi compile là runtime pass.
- [Scope sống lâu do load không hợp tác] → cancel, tách UI, observe task và dispose sau completion; production load phải truyền token xuyên suốt.
- [Native NavigationView khác HTML] → giữ hierarchy/khoảng cách/màu của mẫu bằng resources, ưu tiên keyboard và chữ đầy đủ thay pixel-copy clipping.
- [State reset khi revisit] → công bố policy; Change 4 chỉ thêm giữ state khi có use case rõ.
- [Mặc định mở Shell trước auth] → nhãn development rõ, không tạo session/token/request, auth/RBAC thực là change sau.

## Migration Plan

Khi apply: thêm contracts/headless behavior rồi DI adapter, Shell/pages/dialog/Settings; đổi default startup sau khi factory sẵn sàng. Chạy regression gallery flag và foundation tests. Không có migration backend/database. Nếu rollback, khôi phục MainPage startup cùng wiring UI trước change; giữ foundation/design assets, không xóa dữ liệu. Chỉ archive sau khi tasks kỹ thuật và owner acceptance của build mới hoàn tất.
