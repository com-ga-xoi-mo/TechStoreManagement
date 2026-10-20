# Tasks

## 1. Chuẩn bị

- [x] 1.1 Cập nhật `dev` lên `origin/dev` (tối thiểu `845c522`, gồm PR #10), rồi tạo nhánh `feature/csharp-markup-foundation` từ đó.
  - Đã làm: nhánh từ `845c522`, mốc 207 test pass, build 0 lỗi.
  - Bước chặn đã kiểm bằng thao tác thật (`xdotool` gõ phím/bấm chuột trên Xvfb `:99` 1440x900, Linux Skia, backend + PostgreSQL), ảnh trong `~/.cache/techstore-uitest/`:
    - (a) đạt: gõ `admin`, gõ `Admin@123` vào ô mật khẩu ở chế độ hiện chữ (TextBox bind TwoWay), bấm Đăng nhập → vào Shell (`06-shell.png`). Binding TwoWay `Username`/`Password` và binding `Command` chạy.
    - (b) đạt: Cài đặt → thêm " UITEST" vào tên cửa hàng → Lưu → hiện "Đã lưu thay đổi thông tin cửa hàng thành công", API `GET /api/settings/store` trả tên mới, chuyển trang rồi quay lại vẫn giữ (`10-11.png`); đã trả tên cũ. Đổi theme bằng ComboBox: Theo hệ thống → Sáng → Tối đều áp dụng (`09-settings-light.png`).
    - Ghi chú: gõ bằng `xdotool` vào `PasswordBox` thật thì ViewModel chỉ nhận `123` (mất `Admin@`); chưa xác định do `xdotool` hay do `PasswordBox` của `LoginPage` (ngoài phạm vi, cần người gõ tay kiểm lại).
  - Kiểm tra: nhánh chứa `845c522`, và ngoài thư mục `openspec/changes/csharp-markup-foundation/` thì không có thay đổi nào chưa commit.
  - Chạy `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`, ghi lại số test pass làm mốc (con số 207 được đo trước PR #10, phải đo lại).
  - Build `net10.0-desktop` thành công.
  - Nếu `origin/dev` không build hoặc không pass test, dừng lại và báo nhóm trước khi làm tiếp.
  - **Bước chặn: chạy thử cú pháp binding của PR #10 trước khi dùng làm chuẩn (D1).** Chạy backend cùng PostgreSQL và app `net10.0-desktop` trên Linux Skia, rồi kiểm tra:
    - (a) gõ `admin` / `Admin@123` trên LoginPage và bấm Đăng nhập. Phải vào được Shell. Bước này chứng minh binding TwoWay `Username`/`Password` và binding `Command` chạy;
    - (b) vào Cài đặt, sửa tên cửa hàng, lưu, rồi tải lại. Giá trị phải được giữ. Sau đó đổi theme bằng ComboBox, theme phải đổi. Bước này chứng minh TwoWay trên `SettingsCard` và `SelectedIndex`.
  - Ghi kết quả vào `design.md` (D1).
  - Nếu (a) hoặc (b) fail: dừng lại, báo Dev 1, chốt lại cú pháp binding (ví dụ chuyển sang mẫu DataContext lambda `(page, vm)`) và cập nhật D1 trước khi sang nhóm 2.
- [x] 1.2 Lấy cú pháp binding từ `LoginPage.cs` / `SettingsPage.cs` làm chuẩn (D1), không nghiên cứu lại. Chỉ tra thêm trong package đã restore và tài liệu Uno Markup 6.7 hai điểm repo chưa có ví dụ:
  - áp ThemeResource/StaticResource cho thuộc tính từ code;
  - hành vi của `Uno.Extensions.Markup.Generators` với `UserControl` có DependencyProperty.
  - Kiểm tra: ghi nguồn tra cứu và cú pháp dự kiến cho hai điểm này vào mục D3/D5 của `design.md`.

## 2. Tokens và đăng ký resource bằng C#

- [x] 2.1 Viết `AppColors`, `AppTypography`, `AppMetrics`, `AppIcons`, `AppControlStyles` trong `Styles/`, giữ đúng tên key cũ.
  - Giá trị Light theo D4: primary `#0066FF` / `#0052CC` / `#0040A8`, focus `#0066FF`.
  - Dark và HighContrast giữ nguyên, HighContrast trỏ màu hệ thống.
  - Tách theo D3: hằng số key ở file thuần C# (không tham chiếu WinUI), phần dựng giá trị và `ResourceDictionary` ở file riêng.
  - Icon: giữ chuỗi path, đổi sang `Geometry` qua một hàm chuyển đổi duy nhất (Uno `GeometryConverter` trên Skia; nhánh WinAppSDK dùng `XamlBindingHelper.ConvertValue`, ghi chưa kiểm).
  - Thêm điểm đăng ký merge vào `Application.Resources` trong constructor `App`: sau `InitializeComponent` (để `AccentButtonStyle` đã có), trước khi tạo window. Tạm thời vẫn giữ các file XAML.
  - Kiểm tra: build `net10.0-desktop` pass; khởi động app không có lỗi resource hay lỗi `BasedOn`.
- [x] 2.2 Thêm test headless cho bảng hằng số key (file thuần C# ở 2.1):
  - snapshot: tập key phải bằng đúng tập key trích từ 5 file `Styles/*.xaml` hiện tại (danh sách ghi cố định trong test);
  - độ đầy đủ theo theme: mỗi key màu có mặt trong Light, Dark và HighContrast, và mọi key HighContrast được khai báo là tham chiếu màu hệ thống.
  - Thêm source-link vào `TechStore.Client.UnitTests.csproj`, chỉ link file hằng số key.
  - Kiểm tra: test pass; fail khi cố ý xóa một key, và fail khi bỏ một key khỏi một theme.
- [x] 2.3 Cung cấp accessor có kiểu cho trang C# Markup, trả về tham chiếu theo theme (ThemeResource), không trả về `Brush`/`Color` tĩnh.
  - Kiểm tra: build pass, và ghi cú pháp sử dụng vào mục D3 của `design.md`.
- [x] 2.4 Gỡ 5 mục `ResourceDictionary Source="Styles/..."` khỏi `App.xaml` (giữ `XamlControlsResources`) và xóa 5 file `Styles/*.xaml`.
  - Đã kiểm trên Linux Skia (Xvfb, backend + PostgreSQL bật): xóa 5 file `Styles/*.xaml`, build 0 lỗi; Login, Shell (đăng nhập `admin`, Sản phẩm, Tổng quan, Cài đặt) và gallery khởi động, log không có cảnh báo resource/`BasedOn`; `DashboardPage` hiển thị bình thường (`~/.cache/techstore-uitest/fix-dashboard-light.png`). Style màu theo theme phải dựng qua `XamlReader` (D3), nếu dựng bằng Markup thì không đổi theo `RequestedTheme`.
  - Chưa kiểm: HighContrast lúc chạy, target Windows.
  - Kiểm tra:
    - `find frontend/TechStore.Client/Styles -name '*.xaml'` rỗng;
    - build pass;
    - chạy app trên Linux Skia, mở được Login, Shell và gallery (`TECHSTORE_UI_GALLERY=1`) mà log không có lỗi resource;
    - `DashboardPage` vẫn đọc được `TechStoreSecondaryTextBrush`.
- [x] 2.5 Kiểm tra contrast các cặp màu đổi trong D4 (chữ trắng trên `#0066FF`, focus `#0066FF` trên canvas/surface) theo yêu cầu "Readable foreground and indicators".
  - Kiểm tra: ghi tỉ lệ đo được vào `design.md`. Cặp nào không đạt thì ghi phương án xử lý trước khi sang nhóm 3.

## 3. Base controls bằng C# Markup

- [x] 3.1 Viết helper trình bày thuần C# trong `Controls/Presentation/` theo D5:
  - trạng thái StatCard: loading > lỗi > nội dung, giá trị thiếu hiện `—`, đơn vị;
  - hiện action của EmptyState;
  - hiện/ẩn của StatusBadge;
  - ánh xạ `StatusTone` sang key Fill/Stroke/Text, tone lạ rơi về Neutral.
  - Link helper vào test headless và viết test cho các scenario của spec `frontend-shared-controls`: giá trị 0 không thành `—`, loading ưu tiên lỗi, action thiếu nhãn/command bị ẩn, label rỗng ẩn badge.
  - Kiểm tra: test pass.
- [x] 3.2 Pilot: chuyển `StatusBadge` sang C# Markup theo D1/D2/D5.
  - Giữ base `UserControl`, DP `Label` / `Tone` và giá trị mặc định; xóa `StatusBadge.xaml` / `.xaml.cs`.
  - Kiểm tra trên Linux Skia:
    - gallery hiển thị đủ 6 tone;
    - đổi Light/Dark trong gallery thì màu badge đổi tại chỗ;
    - không tạo tab stop.
  - Chốt cú pháp thật vào D1/D5 của `design.md`. Nếu ThemeResource áp từ code không cập nhật khi đổi theme, áp phương án dự phòng VisualStateManager và ghi lại.
  - Đã kiểm bằng probe lập trình trên Linux Skia (probe tạm, đã xóa): 6 tone hiển thị đủ; đổi `RequestedTheme` Light/Dark/Light thì Fill/Stroke/Text đổi tại chỗ (ví dụ Info Light `#EFF6FF`/`#BFDBFE`/`#1D4ED8` sang Dark `#172554`/`#3B82F6`/`#BFDBFE`); `IsTabStop=false`; label rỗng/khoảng trắng thì `Collapsed`. Cú pháp đã chốt ghi ở D5.
  - Chưa kiểm: cần người kiểm giao diện (nhìn badge trong gallery ở Light và Dark, bấm Tab không dừng ở badge).
- [x] 3.3 Chuyển `StatCard` sang C# Markup, giữ đủ 9 DP (Title, ValueText, UnitText, DetailText, TrendText, ErrorText, IsLoading, IconSource, Tone) cùng mặc định.
  - UI bind vào thuộc tính phái sinh từ helper; không còn `Refresh()` tự đặt `Visibility`.
  - Xóa file XAML.
  - Kiểm tra: gallery và Dashboard (fixture) hiển thị các trạng thái loading / lỗi / nội dung / giá trị thiếu đúng spec.
  - Đã kiểm bằng probe: giá trị `0` giữ nguyên, giá trị rỗng hiện `—`, lỗi hiện `ErrorText`, loading ẩn lỗi và hiện ProgressRing; badge trend đổi màu theo theme.
  - Chưa kiểm: cần người kiểm giao diện (Dashboard fixture, gallery).
- [x] 3.4 Chuyển `EmptyStateControl` sang C# Markup, giữ 5 DP và mặc định; action bind `ActionCommand`; xóa file XAML.
  - Kiểm tra: gallery có action khả dụng, action thiếu bị ẩn, action disabled không chạy; kích hoạt được bằng bàn phím.
  - Đã kiểm bằng probe: action hiện khi đủ nhãn và command, ẩn khi thiếu command, command chạy 1 lần khi Invoke, `IsEnabled=false` khi `CanExecute=false`.
  - Chưa kiểm: cần người kiểm giao diện (kích hoạt bằng Tab + Enter/Space).
- [x] 3.5 Chuyển `SearchBoxControl` sang C# Markup, giữ 5 DP và mặc định ("Tìm kiếm", "Tên sản phẩm, SKU hoặc IMEI").
  - Ô nhập bind TwoWay với DP `Text`.
  - Chỉ giữ hai ngoại lệ theo D2: `QuerySubmitted` để phát `SearchCommand`, và trả focus khi xóa.
  - Xóa file XAML.
  - Kiểm tra trên gallery và trang Sản phẩm:
    - nhập "điện thoại & phụ kiện" thì consumer nhận đủ text;
    - Enter phát đúng một lần;
    - xóa làm rỗng text, focus quay về ô nhập, không tự tìm kiếm.
  - Đã kiểm bằng probe: gõ "điện thoại & phụ kiện" thì `Text` của control nhận đủ; `QuerySubmitted` phát `SearchCommand` đúng 1 lần với text đầy đủ dấu; nút Xóa làm rỗng text, chạy `ClearCommand` 1 lần, focus về ô nhập (`TextBox` bên trong AutoSuggestBox), không phát `SearchCommand`; đổi `Label`/`PlaceholderText` cập nhật.
  - Chưa kiểm: cần người kiểm giao diện (phím Enter thật trên gallery và trang Sản phẩm).
- [x] 3.6 Chạy kiểm tra tự động cho `Controls/`.
  - Kiểm tra:
    - `grep -rnE 'PropertyChanged \+=|TextChanged \+=|Color\.FromArgb' frontend/TechStore.Client/Controls` rỗng;
    - không còn `.xaml` trong `Controls/` (trừ `Charts/` nếu có);
    - toàn bộ `Frontend.UnitTests` pass.

  - Đã chạy: grep `PropertyChanged +=|TextChanged +=|Color.FromArgb|new SolidColorBrush(` trong `Controls/` rỗng; không còn `.xaml` trong `Controls/`; `Frontend.UnitTests` 248 pass; khởi động gallery không có lỗi resource/binding.
## 4. Trang mẫu tham chiếu C# Markup

- [x] 4.1 Viết `MarkupReferenceViewModel` trong `ViewModels/Development/` theo D6, không tham chiếu kiểu WinUI:
  - kế thừa `PageViewModel`, dữ liệu minh họa cố định;
  - các command chuyển tải / rỗng / lỗi / nội dung;
  - query, bộ đếm submit, action đặt lại, `ThemeIndex`.
  - Kiểm tra bằng test headless:
    - submit đúng một lần với text đầy đủ dấu;
    - đặt lại xóa query;
    - mỗi lúc chỉ một trạng thái;
    - đổi `ThemeIndex` không reset query hay trạng thái.
  - Đã chạy: `MarkupReferenceViewModelTests` (8 test) pass: submit đúng một lần với text có dấu, đặt lại xóa query, mỗi lúc một trạng thái, đổi `ThemeIndex` giữ query và trạng thái, thử lại về nội dung, quay lại gọi navigator.
- [x] 4.2 Thêm contract mở/đóng trang mẫu trong `Services/Development` (thuần C#) và phần hiện thực đổi `MainWindow.Content` trong `Views/Development`. Chỉ đăng ký khi `GalleryOptions.Enabled`. Thêm đúng một nút "Mở mẫu C# Markup" vào gallery.
  - Kiểm tra:
    - khi gallery tắt, không resolve được trang mẫu và Shell không có điểm vào;
    - test headless có liên quan tới `GalleryOptions` vẫn pass.
  - Đã làm: `IMarkupReferenceNavigator` (`Services/Development`), `MarkupReferenceNavigator` (`Views/Development`, giữ `Host` do `App` gán), đăng ký trong `AddFrontendUi` chỉ khi `galleryOptions.Enabled`; một nút "Mở mẫu C# Markup" trên gallery (`Click` ở code-behind, navigator là tham số tùy chọn của `ComponentGalleryPage`). Khởi động `TECHSTORE_UI_GALLERY=0` và `=1` (Debug, `ValidateOnBuild`) không lỗi.
  - Chưa kiểm: chưa có test tự động rằng trang mẫu không resolve được khi gallery tắt (`AddFrontendUi` không được link vào test headless); bảo đảm chỉ bằng đọc code và khởi động thật.
- [x] 4.3 Viết `MarkupReferencePage` bằng C# Markup theo D1. Trang thể hiện:
  - `SearchBoxControl` bind TwoWay cùng vùng hiển thị giá trị của ViewModel;
  - command;
  - 4 trạng thái nội dung;
  - `StatCard`, `StatusBadge`, `EmptyStateControl`;
  - nhãn "dữ liệu minh họa";
  - `RequestedTheme` bind qua converter;
  - màu chỉ lấy qua accessor theme;
  - nút quay lại gallery.
  - Kiểm tra trên Linux Skia, với backend tắt: mở từ gallery, đúng các scenario của requirement "C# Markup reference specimen", và không phát request mạng.
  - Đã kiểm bằng probe lập trình trên Linux Skia, backend tắt (probe tạm, đã xóa): mở từ gallery bằng nút; gõ "điện thoại & phụ kiện" thì `ViewModel.Query` nhận đủ; submit đúng 1 lần; 4 trạng thái chỉ hiện một (tải/rỗng/lỗi/nội dung), Thử lại và action rỗng về nội dung; đổi theme Dark/Light thì `RequestedTheme`, nền (`#0F172A`/`#F8FAFC`) và màu chữ đổi, query và trạng thái giữ nguyên; Đặt lại xóa ô nhập lẫn ViewModel; Quay lại về gallery.
  - Chưa kiểm: cần người kiểm giao diện (phím Enter thật, bố cục/hình thức, ComboBox theme thật, không gọi mạng chỉ suy ra từ việc ViewModel không có repository).
- [x] 4.4 Chạy kiểm tra tự động cho `Views/Development/MarkupReferencePage.cs`.
  - Kiểm tra: `grep -nE 'PropertyChanged \+=|TextChanged \+=|Checked \+=|Click \+=|Color\.FromArgb|new SolidColorBrush' frontend/TechStore.Client/Views/Development/MarkupReferencePage.cs` rỗng.

  - Đã chạy: grep rỗng.
## 5. Tài liệu chuẩn cho nhóm

- [x] 5.1 Cập nhật AGENTS.md mục 6:
  - thay ví dụ `x.Bind(...)` bằng cú pháp đang chạy (`x => x.Binding(() => ViewModel.Prop)`, `.Mode(BindingMode.TwoWay)`, `.Convert(...)`, `.Command(x => x.Binding(...))`), trích từ `MarkupReferencePage` / controls và khớp với `LoginPage` / `SettingsPage`;
  - thêm danh sách điều cấm và ngoại lệ hợp lệ (D2), gồm `QuerySubmitted`, trả focus, và đồng bộ `PasswordBox`;
  - cách dùng accessor token và cách đặt shared control trong trang C# Markup;
  - bỏ tham chiếu `docs/MVVM_REPOSITORY_BASE_PROJECT.md`;
  - sửa Data Flow còn ghi `{x:Bind}`;
  - mô tả `Styles/` và `Controls/` khớp tên lớp thật.
  - Kiểm tra: mọi đường dẫn, lớp và lệnh nêu trong mục 6 đều tồn tại hoặc chạy được.
  - Đã cập nhật mục 6, Data Flow bước 1 và Key Directories; bỏ tham chiếu `docs/MVVM_REPOSITORY_BASE_PROJECT.md`. Đối chiếu đường dẫn/lớp nêu trong mục 6 với file thật.
- [x] 5.2 Thêm bảng trạng thái chuyển đổi vào AGENTS.md:
  - Đã là C# Markup, dùng token theo theme: 4 controls, trang mẫu.
  - Đã là C# Markup nhưng màu còn cố định (`StitchTokens` / `Color.FromArgb`): Login, Shell, Settings (PR #10). Việc tồn đọng của Dev 1 là chuyển sang token theo theme.
  - Còn XAML: ProductList, ProductDetail, Dashboard, Inventory, OrderList, CustomerList, ComponentGallery, MainPage.
  - Kiểm tra: đối chiếu bảng với `find frontend/TechStore.Client/Views -name '*.xaml'`.

  - Đã thêm bảng vào mục 6, đối chiếu với `find frontend/TechStore.Client/Views -name "*.xaml"` (8 trang XAML còn lại khớp).
## 6. Kiểm chứng tích hợp

- [x] 6.1 Chạy regression toàn bộ.
  - Kiểm tra:
    - `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` pass, số test ≥ mốc ở 1.1 cộng test mới;
    - `dotnet build TechStore.sln` không có lỗi mới;
    - `find frontend/TechStore.Client/Styles -name '*.xaml'` rỗng.
  - Đã chạy: `Frontend.UnitTests` 256 pass (mốc 207 + test mới); `dotnet build TechStore.sln` 0 lỗi, 1 cảnh báo MSB3277 sẵn có ở `Backend.UnitTests` (không liên quan); `Styles/*.xaml` rỗng.
- [x] 6.2 Chạy kiểm tra runtime trên Linux Skia với backend và PostgreSQL bật, đăng nhập `admin`. Mở 6 trang XAML (Sản phẩm, Chi tiết sản phẩm, Tổng quan, Kho, Đơn hàng, Khách hàng) cùng gallery và trang mẫu, ở Light và Dark. Ghi vào mô tả PR: OS/renderer, commit, lệnh chạy, theme, expected / actual và ảnh chụp.
  - Kiểm tra:
    - không lỗi resource;
    - controls và icon hiển thị đúng;
    - chỉ nhóm màu primary (và focus) đổi sang `#0066FF`;
    - trang mẫu đổi theme mà giữ nguyên state.
  - Ghi rõ là **chưa kiểm**:
    - HighContrast khi chạy thật: Skia không kích hoạt được; đã được bao bởi test 2.2;
    - target Windows, gồm cả nhánh dựng `Geometry` cho icon trên WinAppSDK.
  - Trang Login / Shell / Settings vẫn màu sáng cố định là hành vi đã biết, thuộc phạm vi của Dev 1.

  - Đã kiểm bằng ảnh (Linux Skia, Xvfb 1440x900, backend + PostgreSQL, `admin`, `~/.cache/techstore-uitest/`): gallery Sáng/Tối/Sáng (`fix-g1-light`, `fix-g2-dark`, `fix-g3-light`); trang mẫu Sáng và Tối giữ query `iphone` (`fix-m3-light`, `fix-m4-dark`, `fix-m5-error-dark`); Shell Sản phẩm Sáng (`fin-products-light`) và Tối (`fin-products-dark`); Tổng quan Sáng (`fix-dashboard-light`). Log không có cảnh báo resource.
  - Bổ sung (sau bản sửa Style theo theme), đi qua đủ 6 trang (Tổng quan, Sản phẩm, Chi tiết sản phẩm, Danh mục và Kho, Đơn hàng, Khách hàng) trong ba lượt, mỗi lượt một ảnh ghép:
    - Tối theo hệ thống (desktop `prefer-dark`): `r1-system-dark-sheet.png`;
    - đổi sang Sáng lúc đang chạy: `r2-live-light-sheet.png`;
    - đổi sang Tối lúc đang chạy: `r3-live-dark-sheet.png`.
    - Expected = actual ở cả ba lượt: không lỗi resource, icon và controls đúng, chữ trên thẻ sản phẩm và trang chi tiết đọc được (trước bản sửa, lượt đổi sang Sáng làm chữ thẻ sản phẩm gần như trắng).
  - Màu primary: `TechStorePrimaryBrush` chỉ được dùng ở đường biểu đồ doanh thu của Dashboard (chế độ demo) và ô mẫu màu trong gallery. Nút chính trên 6 trang dùng `AccentButtonStyle` (màu accent hệ thống) nên không đổi; nền canvas và nền thẻ trùng từng điểm ảnh với bản `dev` gốc (`base-products-light.png`).
  - Ngoài phạm vi, đã biết: khung Shell (sidebar) và Login/Settings dùng màu sáng cố định của `StitchTokens`, nên khi app ở theme Tối thì chữ sidebar không đọc được. Việc này thuộc Dev 1.
  - Chưa kiểm: HighContrast khi chạy thật; target Windows (gồm nhánh dựng `Geometry` cho icon trên WinAppSDK).

## Workflow follow-up

- Báo nhóm (đặc biệt Dev 1 và Dev 6) về việc đổi màu primary, chuẩn C# Markup mới và việc tồn đọng `StitchTokens` trước khi merge PR vào `dev`.
- Archive change sau khi PR được review và merge.
