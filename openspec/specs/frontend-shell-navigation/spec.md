# frontend-shell-navigation Specification

## Purpose

Cung cấp khung điều hướng TechStore có vòng đời trang và state rõ ràng để các phân hệ gắn vào, kiểm thử độc lập và nghiệm thu trên desktop được hỗ trợ.

## Requirements

### Requirement: Offline development shell
Client SHALL open the Shell with valid configuration without requiring the backend/database. Shell MUST show the development unauthenticated state when there is no session and MUST NOT create a fake authentication session. When Dashboard is activated, the default live Analytics request may fail visibly; demo fixture data is shown only when preview is explicitly enabled. The Shell itself SHALL remain navigable after that request failure.

#### Scenario: Default offline startup
- **WHEN** the application starts with backend unavailable, valid configuration, and gallery disabled
- **THEN** Shell opens at Overview with “Bản phát triển — chưa đăng nhập”, no fake token/session is created, and the Dashboard reports the actual unauthenticated/network failure without fixture data

#### Scenario: Explicit dashboard preview
- **WHEN** Dashboard preview is enabled and gallery is disabled
- **THEN** Shell opens offline at Overview with a persistent fixture banner and no authentication session or Analytics request

#### Scenario: Shell remains usable after Dashboard API failure
- **WHEN** the Dashboard API request fails during shell startup or navigation
- **THEN** the error is contained in Dashboard state and the user can navigate to the other supported Shell destinations

### Requirement: Six coherent admin destinations
Shell SHALL have exactly six entry points: Overview, Products, Categories and Inventory, Orders, Customers, and Settings. They SHALL be structured with C# Markup Fluent API (`Uno.Extensions.Markup.WinUI` on `NavigationView`). Selected menu, title, and content SHALL reflect the successfully navigated route. Shell Pane Footer SHALL show the active account (FullName, Role) and Logout action through declarative C# Markup bindings. Products SHALL open live Catalog data and provide productId detail navigation; Overview SHALL show live Analytics by default or labeled preview only when enabled; unimplemented business pages SHALL clearly remain placeholders without fabricated data. At expanded desktop widths, the navigation pane SHALL visually follow the left-sidebar hierarchy in `docs/sample_dashboard.png`: brand and subtitle, menu section label, highlighted current destination, compact icon/label rows, Settings separated near the bottom, and account footer. Visual changes MUST preserve existing route, selection, session and logout behavior.

#### Scenario: Visit every destination
- **WHEN** lần lượt chọn sáu điểm vào bằng chuột hoặc keyboard
- **THEN** nội dung tương ứng hiển thị, Sản phẩm tải Catalog thật, selection/title đúng, Cài đặt chỉ xuất hiện một lần ở footer và không có mục POS/Identity bổ sung

#### Scenario: Same destination selected again
- **WHEN** chọn lại route hiện hành với cùng tham số
- **THEN** nội dung/state giữ nguyên, không tạo trang hoặc load lần nữa

#### Scenario: Unknown or unavailable destination
- **WHEN** yêu cầu route không hỗ trợ, productId rỗng hoặc không tạo được trang đích
- **THEN** giữ trang/title/selection trước đó và báo lỗi an toàn, không để Shell trống hoặc selection sai

#### Scenario: Read-only detail navigation
- **WHEN** mở card A rồi chuyển tới detail B trong nhánh Sản phẩm
- **THEN** truyền đúng id từng sản phẩm, tải chi tiết tương ứng, hủy activation trước và giữ Sản phẩm selected; detail không tạo mục menu thứ bảy

#### Scenario: Preview stays within dashboard
- **WHEN** mở Dashboard demo rồi chọn Sản phẩm hoặc mục khác
- **THEN** vẫn có đúng sáu mục, Catalog tiếp tục dùng API thật và các mục chưa triển khai giữ trạng thái khung

#### Scenario: Shell reflects active user session
- **WHEN** Quản trị viên đăng nhập thành công với tài khoản "admin"
- **THEN** Pane footer của Shell cập nhật tên người dùng và vai trò "Quản trị viên", nhãn trạng thái chuyển thành "Đã đăng nhập"

#### Scenario: Expanded sidebar matches the reference hierarchy
- **WHEN** Shell is shown with its navigation pane expanded at a wide desktop size
- **THEN** brand, menu caption, selected Overview row, other destinations, separated Settings entry and account footer follow the reference's visual hierarchy without adding/removing destinations or changing navigation behavior

### Requirement: Cancellable page activation
Trang SHALL bắt đầu load một lần mỗi activation, hủy load khi rời trang hoặc đóng cửa sổ và bỏ mọi kết quả thuộc activation cũ. Chuyển trang SHALL không phải đợi load cũ hoàn tất; cancellation dự kiến MUST NOT hiện thành lỗi người dùng.

#### Scenario: Rapid navigation and late completion
- **WHEN** chuyển A sang B rồi về A trong khi load A cũ chưa xong, sau đó load cũ thành công hoặc lỗi
- **THEN** trang A mới giữ state của activation mới, kết quả/lỗi/loading-finalization cũ không thay đổi nó hoặc trang B

#### Scenario: Window closes while loading
- **WHEN** đóng cửa sổ khi load đang chạy
- **THEN** load nhận cancellation, không cập nhật UI đã đóng, không nhận thao tác mới và tài nguyên được giải phóng đúng một lần khi công việc kết thúc

### Requirement: Predictable presentation state
Pages SHALL present Loading, Empty, Content, or Error from the current result and provide retry for the current failure; Catalog detail also supports NotFound. Dashboard SHALL distinguish live API success with zero/empty values, live API errors, and preview results; only an explicit preview flag may select fixture data. A new activation resets page data. Back from Catalog detail preserves query/page as values and reloads. Resize, theme, or dialogs SHALL NOT reload or clear current page state. A Dashboard session change SHALL clear data from the previous session and prevent late responses from committing.

#### Scenario: Failure and retry
- **WHEN** load hiện hành lỗi rồi người dùng retry
- **THEN** Error chuyển Loading rồi sang kết quả mới; không chạy chồng retry cùng request hoặc hiển thị dữ liệu giả để che lỗi

#### Scenario: Placeholder and revisit
- **WHEN** mở trang nghiệp vụ khung chưa triển khai rồi rời và quay lại
- **THEN** trang mới bắt đầu lại lifecycle, kết thúc ở Empty giải thích chưa triển khai, không giữ input từ activation trước

#### Scenario: Presentation-only change
- **WHEN** trang có state hiện hành rồi resize, đổi theme hoặc mở/đóng dialog
- **THEN** state vẫn giữ và không có lần load mới chỉ vì các thao tác này

#### Scenario: Back from product detail
- **WHEN** người dùng quay lại danh sách từ chi tiết
- **THEN** tạo activation list mới với query/page đã áp dụng trước khi mở detail, tải lại API và phục hồi focus về card nếu còn hoặc heading danh sách

#### Scenario: Leave catalog branch
- **WHEN** rời Sản phẩm sang mục Shell khác rồi mở lại Sản phẩm
- **THEN** query/page khởi đầu mặc định, dữ liệu và input từ activation Catalog trước không tự phục hồi

#### Scenario: Dashboard revisit
- **WHEN** the user leaves Dashboard and returns
- **THEN** the new activation starts at the default period and loads the live snapshot, or a fresh fixture when preview is explicitly enabled; prior activation state is not restored

#### Scenario: Live zero values and independent empty lists
- **WHEN** a successful live Dashboard response contains zero KPIs or an empty recent-order or low-stock list
- **THEN** zero values and each list's own empty state are shown as successful data, without preview or not-integrated messaging

#### Scenario: Dashboard session and authorization changes
- **WHEN** an anonymous request receives 401, an active session receives 401, an Admin receives 403, or the user logs out during a Dashboard request
- **THEN** anonymous 401 shows sign-in-required Error without changing session; active 401 expires only the matching session; 403 preserves the session and shows access denied; logout clears the snapshot and cancels the request into sign-in-required Empty state

#### Scenario: Dashboard late response after a session change
- **WHEN** a Dashboard request completes after logout, session replacement, or a newer period request
- **THEN** its data, error, or loading finalization does not modify the current page or expose data from the prior session

### Requirement: Session local settings
Cài đặt SHALL cho chọn System/Light/Dark, xem thông tin ứng dụng không nhạy cảm, đồng thời cho phép Quản trị viên xem và chỉnh sửa thông tin cấu hình cửa hàng TechStore (tên cửa hàng, địa chỉ, hotline, mã số thuế) thông qua Uno C# Markup Fluent API (`Uno.Extensions.Markup`) kết hợp với CommunityToolkit SettingsControls. Toàn bộ form fields, thông báo lưu thành công và nút lưu SHALL sử dụng declarative two-way bindings. Theme SHALL áp dụng ngay trong Shell/dialog, giữ khi chuyển trang trong phiên và mặc định System ở phiên mới. Thao tác theme không cần backend; các thay đổi cấu hình cửa hàng phát sinh lưu trữ qua API cấu hình.

#### Scenario: Theme survives navigation
- **WHEN** chọn Dark, chuyển Sản phẩm rồi trở lại Cài đặt
- **THEN** Shell và dialogs dùng Dark, lựa chọn còn Dark; theme không phát request và Catalog chỉ gọi API cho dữ liệu của activation

#### Scenario: New process
- **WHEN** đóng rồi khởi chạy client lần mới
- **THEN** theme bắt đầu System, không tuyên bố đã lưu tùy chọn lâu dài

#### Scenario: Store configuration view and update
- **WHEN** Quản trị viên mở trang Cài đặt và cập nhật hotline cửa hàng
- **THEN** Giao diện hiển thị form thông tin cửa hàng sử dụng SettingsCard/SettingsExpander, lưu dữ liệu thành công và thông báo kết quả

### Requirement: Responsive reference shell
Shell SHALL giữ hierarchy sidebar/thương hiệu/footer/header của mẫu Dashboard và tái sử dụng design system. UI SHALL dùng được từ 800×600 đến 1440×1000 DIP, có keyboard focus rõ, chữ tiếng Việt đầy đủ và hỗ trợ Light/Dark/System cùng Windows Contrast.

#### Scenario: Reference and narrow viewport
- **WHEN** chạy ở 1440×1000 rồi resize thật xuống 1024×768 và 800×600 DIP
- **THEN** sidebar rộng chuyển compact với pane mở được, sáu mục và hành động vẫn truy cập được, content cuộn/reflow không che action, nhãn dài không mất dấu

#### Scenario: Offline keyboard and theme
- **WHEN** không có mạng, dùng Tab/Shift+Tab/Enter/Space, đổi theme và text scale 100%,150%,200%
- **THEN** font/icon có sẵn, focus và selected state phân biệt được, nhãn “Danh mục và Kho” cùng dấu tiếng Việt đọc được, không có keyboard trap ngoài modal

### Requirement: Isolated behavioral verification
Navigation, lifecycle, dialog contracts và ViewModel state SHALL kiểm thử được qua production logic không cần UI thread, API hay database. ViewModel MUST NOT sở hữu UI controls hoặc thực hiện HTTP trực tiếp.

#### Scenario: Headless run
- **WHEN** chạy suite frontend với fake activation/presenter có completion điều khiển được
- **THEN** kiểm chứng được route/state/cancellation/late results mà không tạo cửa sổ hay network request

### Requirement: Build specific shell acceptance
Shell SHALL có bằng chứng runtime trên Windows native và Skia Linux gắn với build cụ thể. Chủ dự án SHALL nghiệm thu giao diện bằng task riêng; acceptance predecessor MUST NOT tự động áp dụng cho Shell mới.

#### Scenario: Technical evidence
- **WHEN** chạy kiểm tra navigation/dialog/resize/theme
- **THEN** record ghi build id/hash hoặc commit kèm dirty diff, OS/target, command, actual viewport DIP/scale, expected/actual và ảnh/log; kiểm tra chưa chạy ghi unverified

#### Scenario: Owner confirmation
- **WHEN** kỹ thuật đã kiểm tra nhưng chủ dự án chưa xác nhận build/target đã xem
- **THEN** task owner acceptance vẫn unchecked; chỉ tick khi có xác nhận kèm ngày, build/target và kết quả hoặc ngoại lệ
