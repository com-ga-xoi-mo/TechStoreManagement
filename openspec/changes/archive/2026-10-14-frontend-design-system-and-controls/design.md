# Design

## Context

Xem `proposal.md` về mục tiêu. Căn cứ phạm vi là Change 2 trong `docs/FRONTEND_FOUNDATION_PLAN.md`; kiến trúc theo `docs/MVVM_REPOSITORY_BASE_PROJECT.md` mục 6–7. Handoff của Change 1 có mô tả Change 2–4 không khớp plan gốc; dùng plan gốc: Change 2 design system, Change 3 shell/navigation/dialogs, Change 4 Catalog.

Quan sát repository lúc lập proposal:

- `App.xaml` mới merge `XamlControlsResources`; chưa có design dictionaries hay controls được yêu cầu.
- `App.xaml.cs` tạo provider, validate API options, tạo Window/Frame rồi `Frame.Navigate(typeof(MainPage), ...)`; có ownership/disposal khi đóng cửa sổ. Không mặc định Frame hỗ trợ constructor DI.
- `Views/MainPage.xaml` là shell placeholder hiện có. Change này không thay nó thành shell sản phẩm.
- `UnoFeatures` có `Mvvm;SkiaRenderer`; assets restore hiện resolve MVVM Toolkit 8.4.2, Uno.WinUI 6.7.135, Uno.Fonts.Fluent 2.6.1. Uno.Sdk vẫn 6.7.30; SDK và Uno.WinUI runtime là hai phiên bản khác nhau.
- Headless test project source-link `ServiceCollectionExtensions.cs` và foundation thuần C#, chưa có UI reference. UI registrations mới không được phá đường build này.
- `winapp` chưa có trên PATH Linux. Proposal chỉ nghiên cứu và chuẩn bị quy trình; chưa cài tool hoặc thêm dependency.
- Đã đọc style guide/Dashboard HTML và PNG, form validation HTML, cùng `fluent_retail_command/DESIGN.md`. Các nguồn không hoàn toàn đồng nhất.

## Goals / Non-Goals

**Goals:** cho các feature sau tái sử dụng tài nguyên semantic và controls cùng hành vi trên Windows/Skia; gallery có thể tái lập với fixture offline và đủ bằng chứng visual/accessibility.

**Non-Goals:** dựng lại toàn bộ các màn hình mẫu, chart, Settings nghiệp vụ, auth, repository, navigation service, persistence theme hay Material theme. Theme selector trong gallery chỉ phục vụ preview. Không dùng custom template thay mọi control WinUI.

## Decisions

### 1. Chuẩn thiết kế và xử lý xung đột

Base path: `UI/stitch_techstore_retail_admin_dashboard/`. Đơn vị XAML là DIP; không sao chép kích thước bitmap thành kích thước cửa sổ vật lý.

| Nguồn / quan sát | Quyết định cho Change 2 | Control/resource nhận quyết định |
| --- | --- | --- |
| `techstore_style_guide_system_specification_board/{screen.png,code.html}`: nền Slate, primary #2563EB, Inter; panel rounded-xl | Là chuẩn tokens và typography | `Styles/Colors.xaml`, `Typography.xaml`, `Metrics.xaml` |
| `admin_dashboard_techstore_management_system/{screen.png,code.html}`: KPI trắng, viền nhẹ, icon tile, delta; có nhãn bị xuống dòng/cắt | Dùng bố cục KPI, không sao chép lỗi clipping hoặc số doanh thu như dữ liệu thật | `StatCard`, fixture screenshot |
| Dashboard có font override Be Vietnam Pro, khai báo Plus Jakarta Sans; style guide yêu cầu Inter | Chọn Inter xuyên suốt; JetBrains Mono chỉ cho SKU/IMEI/token captions theo style guide | Font assets; tránh ba font sans cùng cạnh tranh |
| `DESIGN.md`: YAML mang palette khác (#004ac6), phần prose lại dùng #2563EB; prose cards 8/controls 6 | Dùng màu style guide; panel 12, metric card 8, input/button8; badge 4. Mượn quy tắc diacritics, density và phân cấp từ prose | Metrics và lightweight styles |
| Style guide CSS `.status-badge` ghi pill nhưng ảnh/header hướng dẫn góc 4 | Badge có chữ là hình chữ nhật góc 4, viền 1, không chấm tròn trạng thái; count pill chưa cần | `StatusBadge` |
| Mẫu form validation dùng palette/tên token Material và icon font CDN | Chỉ lấy pattern label/input/error text; giữ WinUI và assets local | Input styles, `InfoBar`, form specimen |
| Mẫu badge có chữ xanh/vàng nhạt; label `UnderRepair` là “Đang bảo hành” | Dùng foreground đậm hơn để đạt contrast; ví dụ sửa thành “Đang sửa chữa”. Không biến warranty thành enum mới | Tone resources, fixture badge |

Không import HTML, Tailwind, CDN font hoặc Material resources vào Uno. Light là chuẩn visual. Dark/HighContrast là biến thể cùng hierarchy, không đòi giống màu ảnh Light.

### 2. Tokens, typography và assets

Prefix public resources `TechStore` để tránh đè khóa WinUI ngầm. Merge sau `XamlControlsResources`: primitive metrics/fonts → semantic colors/brushes → typography → styles. Dùng explicit style keys ở gallery/controls; không áp style toàn ứng dụng làm thay đổi MainPage ngoài phạm vi.

| Nhóm token | Light baseline | Dark baseline / lưu ý |
| --- | --- | --- |
| Canvas / surface / subtle surface | #F8FAFC / #FFFFFF / #F1F5F9 | #0F172A / #1E293B / #334155 |
| Text primary / secondary | #0F172A / #64748B | #F8FAFC / #CBD5E1 |
| Stroke / hover stroke | #E2E8F0 / #CBD5E1 | #475569 / #64748B; input/focus dùng stroke mạnh hơn khi cần |
| Primary action / hover / pressed | #2563EB / #1D4ED8 / #1E40AF; chữ #FFFFFF | Giữ cặp action/chữ có contrast; không dùng blue nhạt với chữ trắng |
| Focus / accent tint | #2563EB / #EFF6FF | #93C5FD / #1E3A5F |
| Success text / fill / stroke | #047857 / #ECFDF5 / #A7F3D0 | #6EE7B7 / #064E3B / #10B981 |
| Warning text / fill / stroke | #B45309 / #FFFBEB / #FDE68A | #FCD34D / #451A03 / #D97706 |
| Danger text / fill / stroke | #B91C1C / #FEF2F2 / #FECACA | #FCA5A5 / #450A0A / #EF4444 |
| Info text / fill / stroke | #1D4ED8 / #EFF6FF / #BFDBFE | #BFDBFE / #172554 / #3B82F6 |
| Neutral text / fill / stroke | #475569 / #F1F5F9 / #CBD5E1 | #E2E8F0 / #334155 / #64748B |
| Accent text / fill / stroke | #6D28D9 / #F5F3FF / #DDD6FE | #DDD6FE / #2E1065 / #8B5CF6 |

Đây là palette đề xuất để đo contrast khi apply, không tuyên bố đã đạt mọi cặp. Normal text ≥4.5:1, large text ≥3:1; focus/control indicator cần ≥3:1 với vùng kề. Có thể làm đậm stroke input cần nhận diện; không ép viền trang trí card đạt yêu cầu của interactive indicator. Ghi lại điều chỉnh contrast thay vì sao chép literal kém đọc trong mẫu.

Typography: page heading 20/28 SemiBold, section 14/20 SemiBold, body 13/20 Regular, form 14/20, caption/badge 12/18 Medium, metric 24/32 SemiBold; unit 13/20. Inter 400/500/600/700; JetBrains Mono 400/500 cho dữ liệu định danh. Tránh line box cố định cắt dấu; cho wrap/auto height. Số tiền fixture dạng `34.990.000 ₫`, dùng tabular numbers nếu API đã kiểm chứng; không phát triển formatter nghiệp vụ trong control. Value/unit do consumer truyền.

Spacing 4/8/12/16/20/24/32; page padding 24 rộng,16 hẹp; gap 16; panel 12, metric 8, control 8, badge 4; border 1; form/button min-height 36 (không fixed Height cắt text scale); table header 40, row min 44. Card nền phẳng/viền nhẹ là baseline; không cần thư viện shadow. Bỏ hiệu ứng web không tái hiện ổn định trên Skia.

Font phải bundle đầy đủ glyph Việt dưới `Assets/Fonts/`, ưu tiên static TTF đúng weight để tránh khác biệt variable font. Icon custom lấy tập nhỏ Fluent System Icons (MIT), chuyển geometry monochrome thành `PathIcon`/`PathIconSource` có Foreground theo theme; built-in dùng SymbolIcon hoặc font có sẵn Uno. Không phụ thuộc Segoe cài trên Linux, không dùng text ligature Material. Resource IconSource được chọn ở View, không lưu UI object trong ViewModel. Kiểm tra không reuse cùng UIElement ở nhiều parent.

Ghi upstream URL, version/commit, license file và biến đổi asset trong manifest khi apply. Inter và JetBrains Mono dùng OFL; giữ license đi kèm. Chỉ nhập assets cần cho gallery; không tải avatar/product photo từ HTML.

### 3. Theme và styles giữ semantics của WinUI

Tạo ThemeDictionaries `Light`, `Dark`, `HighContrast` rõ ràng. Usage sites dùng ThemeResource; dictionary-local alias dùng StaticResource trừ SystemColor/SystemAccent động. HighContrast map foreground/background/stroke/focus về cặp system brushes; không hardcode màu semantic, giảm opacity hoặc tắt HighContrastAdjustment. Trạng thái vẫn có nhãn chữ.

Gallery cung cấp chọn System/Light/Dark. Apply RequestedTheme tại root View bằng UI wiring; ViewModel chỉ giữ enum/string thuần, không nhận FrameworkElement. HighContrast được kiểm tra bằng Contrast theme thực của Windows; selector Dark không chứng minh HighContrast. Skia: ghi khả năng phản ứng OS thực tế; nếu không có cơ chế OS thì preview resource chỉ là kiểm tra giới hạn, không nhận là native HighContrast pass.

Styles explicit: primary/secondary/destructive/quiet button; TextBox/AutoSuggestBox/ComboBox; field label/help/error; table header/cell/row; card surface. Native controls giữ keyboard, focus, automation peers, disabled và validation semantics. Table specimen dùng ListView + Grid header/row, có vùng cao giới hạn để virtualization hoạt động; không thêm legacy DataGrid, không bọc ListView trong ScrollViewer khác. Gallery ngoài là các sections scroll được, table specimen đặt vùng riêng có giới hạn.

Alternative full custom ControlTemplate bị loại: thay chrome nhẹ bằng style/resource trước, chỉ override template nếu API/sample chứng minh cần và có đủ state tests.

### 4. Toolkit selection và WinApp CLI gate

Các quyết định API phải có sample + API check trước khi viết XAML tại apply. Không nâng Uno SDK hoặc target vì sample mới hơn.

| Nhu cầu | Lựa chọn | Trạng thái / fallback |
| --- | --- | --- |
| Metric/empty/badge | WinUI Grid/Border/TextBlock/PathIcon/Button | Ghép thành TechStore UserControl, không thêm UI framework |
| Search | WinUI AutoSuggestBox, không có suggestions trong change này | Native query submission; nếu thiếu property ở Skia thì dùng TextBox + Button có cùng contract, ghi evidence |
| Loading/error | ProgressRing, InfoBar | Không cần Shimmer package chỉ để có animation |
| Gallery theme/state settings | WCT SettingsCard/SettingsExpander, `CommunityToolkit.WinUI.Controls.SettingsControls` **8.2.250402** là ứng viên | Nếu smoke không đạt, dùng label + ComboBox/Expander WinUI cho cùng chức năng và ghi lý do; không đổi scope |
| ViewModel | CommunityToolkit.Mvvm implicit hiện resolve8.4.2 | Không thêm PackageReference trùng ở client; test project ngoài frontend dùng version tương ứng khi cần generator |

Metadata SettingsControls 8.2.250402 có net9.0 phụ thuộc Uno.WinUI≥5.5.87, Windows net8/net9 phụ thuộc WindowsAppSDK≥1.6.250108002 và Triggers cùng dòng. Đây chỉ là bằng chứng chọn ứng viên, không phải chứng nhận Uno 6.7.30 runtime. Apply dùng smoke trên project thật: kiểm tra resolved assets theo từng TFM, XAML parse, render/interaction/theme trên Windows và Skia. Chỉ giữ pin chính thức trong `frontend/Directory.Packages.props` khi evidence đủ; PackageReference trong csproj không có Version. Không trộn 7.x/8.x, không pin lại Uno.WinUI/WinAppSDK để ép restore. Không thêm Settings nghiệp vụ.

WinApp CLI: dùng máy Windows/worktree cùng bản source (tool cung cấp binary Windows); cài từ release chính thức hoặc `winget install Microsoft.winappcli --source winget`, kiểm tra `winapp --version`≥0.7 và help. Không chạy `winapp init`, không thay manifest/project chỉ để tra API. Ghi version CLI, OS, sample ids và project path trong compatibility evidence.

```powershell
winapp find-ui "AutoSuggestBox"
winapp find-ui "SettingsCard"
winapp find-ui "InfoBar"
winapp find-ui --id <scenario-id>
winapp find-api AutoSuggestBox --json --project-dir .\frontend\TechStore.Client
winapp find-api check-property AutoSuggestBox Text Header QueryIcon --json --project-dir .\frontend\TechStore.Client
winapp find-api SettingsCard --json --project-dir .\frontend\TechStore.Client
```

Chạy từ restored project đúng TFM/dependencies; dùng help để tra cú pháp bổ sung. Kết quả WinApp là Windows API evidence, cần Uno coverage + Skia build/runtime riêng. Máy hiện tại thiếu CLI/Windows: giữ gate tương ứng unchecked khi apply nếu chưa có môi trường, không ghi “verified” dựa trên web docs. Không cài CLI vào Linux rồi coi là đáp ứng Windows gate.

### 5. Contract controls và MVVM boundary

Controls là UI composition, dùng dependency properties/binding; không truy cập HTTP, repository, session, global provider hoặc tự tạo ViewModel riêng. Code-behind được phép forward native event sang command, đặt focus, cập nhật visual state; không xử lý nghiệp vụ. Typed x:Bind khai báo OneWay/TwoWay rõ; text input cập nhật mỗi thay đổi, không chờ LostFocus.

| Control | Đầu vào đề xuất | Hành vi và điều kiện biên |
| --- | --- | --- |
| StatCard | Title, ValueText, UnitText, DetailText, TrendText, semantic Tone, IconSource, IsLoading, ErrorText | Không tính KPI. Priority loading > error > content; loading/error không hiển thị số fixture như thật. Content thiếu value hiển thị “—”, khác số0. Optional fields rỗng không để gap; wrap tiêu đề/detail; giá trị lớn vẫn đọc đủ. Card chỉ đọc không nhận tab focus giả |
| SearchBoxControl | Label, PlaceholderText, Text TwoWay, SearchCommand, ClearCommand tùy chọn, IsEnabled | Nhãn visible; Enter hoặc search icon gọi command đúng 1 lần với Text hiện tại; clear cập nhật Text rỗng, gọi ClearCommand nếu có, giữ focus; không tự search lần hai, không debounce/network. Disabled/CanExecute=false chặn action; IME commit không bị coi thành hai submit |
| EmptyStateControl | Title, Description, IconSource, ActionText, ActionCommand | Consumer quyết định khi nào là empty; không suy diễn từ null, lỗi hay loading. Action chỉ xuất hiện khi có text+command, tôn trọng CanExecute; keyboard thao tác được |
| StatusBadge | Label, Tone (Neutral/Info/Success/Warning/Danger/Accent) | Chỉ đọc; label đầy đủ, padding 2×8, min line 18, radius 4. Tone lạ fallback Neutral, label rỗng không sinh badge trống. Không đoán màu từ tiếng Việt/enum nghiệp vụ |

Tone là enum thuần C# về presentation, đặt trong `Controls/` cạnh badge (không có UI type bên trong) hoặc cùng contract presentation dùng chung khi thực sự cần. Domain-to-tone mapping thuộc ViewModel/feature tương lai; không tạo bảng ánh xạ toàn bộ enum backend trong controls. Badge ví dụ là fixture có nhãn, không contract domain mới.

### 6. Gallery và tổ chức source

```text
Styles/{Colors,Metrics,Typography,ControlStyles}.xaml
Controls/{StatCard,SearchBoxControl,EmptyStateControl,StatusBadge}.*
Assets/Fonts/ + Assets/Icons/ + asset license manifest
Views/Development/ComponentGalleryPage.xaml(.cs)
ViewModels/Development/ComponentGalleryViewModel.cs
UiServiceCollectionExtensions.cs
```

Gallery chọn bằng `TECHSTORE_UI_GALLERY=1`; absent/0 vào MainPage hiện có ở cả Debug/Release, invalid value báo diagnostic flag an toàn và không âm thầm bật. Gallery là công cụ preview có banner “Thư viện giao diện — dữ liệu minh họa”; không phải auth bypass vì không có nghiệp vụ/HTTP. Cho phép Release opt-in để nghiệm thu khi Debug HotDesign không sẵn sàng. Không persist flag/theme/session; App vẫn validate API config hợp lệ như foundation trước khi mở window.

App gọi UI registration riêng sau foundation registration. Gallery ViewModel transient và Page constructor nhận VM, tạo Page qua DI tại composition root rồi gán Frame.Content cho nhánh gallery; MainPage giữ đường startup cũ. Không cần navigation service tổng quát ở change này. VM dùng ObservableObject/ObservableProperty/RelayCommand, lưu query, last-submitted query, submit/action count, mẫu state và fixture; không I/O trong constructor. Theme application/responsive layout là View concern.

Sections: palette/typography/icon specimens; buttons/inputs/validation; bốn StatCard theo dashboard; search editable+disabled và echo; empty/action/error/loading; các badge tone; table specimen với tên Việt dài, VND/IMEI. Có controls để đổi state sống, lỗi→retry→content là simulation có nhãn. Không hẹn giờ ngẫu nhiên/network khiến screenshot khó tái lập.

Test headless source-link gallery VM và helper thuần C# có hành vi thực; bổ sung MVVM Toolkit reference đúng version ở test project nếu cần. Không source-link Views/Controls XAML hay UI registration. Test query/state/command, không dùng unit test đọc tên resource để thay cho XAML runtime.

### 7. Acceptance matrix và bằng chứng

| Kiểm tra | Thực thi / pass criteria |
| --- | --- |
| Build/dependency | Restore và build .NET10 hai target, ghi graph/version toolkit; Windows từ máy Windows; cảnh báo/lỗi mới được giải quyết hoặc ghi rõ |
| Launch | Backend tắt, unset flag→MainPage; flag 1→gallery có banner; close/relaunch; không request API; invalid flag diagnostic. Chạy Release khi cần, ghi đúng config |
| Visual Light | 1440×1000 DIP (chuẩn style guide), ảnh full gallery + crop StatCard/search/badge/form đối chiếu nguồn. So sánh hierarchy, alignment, mật độ, font/radius và các khác biệt đã quyết định, không yêu cầu pixel-exact anti-aliasing |
| Resize | 1440×1000,1024×768,800×600 DIP: KPI 4→2→1 cột theo vùng có chỗ; sections 2→1; scroll dọc tới mọi action, không clip/đè; table giữ truy cập cột bằng cuộn ngang riêng nếu cần |
| Vietnamese/scale | “Nguyễn Thị Mỹ”, “Điện thoại & phụ kiện”, “Đang sửa chữa”, “Ắ ằ ễ ộ ự ỹ”, 34.990.000 ₫, IMEI 15 chữ số; glyph offline; scale 100/150/200%, không mất dấu/giá trị/action |
| Keyboard | Tab/Shift+Tab thứ tự hợp lý, focus thấy rõ; Enter submit đúng 1; Space/Enter action; clear trả focus; disabled bỏ qua hợp lý; CanExecute=false không phát command; input đang gõ cập nhật echo ngay |
| Theme/a11y | Switch System/Light/Dark khi window đang mở, controls đang có vẫn cập nhật; Windows Contrast theme thật, kiểm tra name/role/value qua UIA và keyboard; ghi riêng giới hạn OS contrast trên Skia |
| Regression | Toàn suite Frontend.UnitTests hiện có + state/command tests mới; XAML/binding/resource lỗi cần runtime riêng, không dùng headless pass thay thế |
| Owner | Chủ dự án xem tested build và ảnh/runtime, xác nhận rõ ngày, build/commit hoặc build id nếu working tree chưa commit, target và checks; task riêng để unchecked tới khi xác nhận |

Mỗi runtime record có OS, renderer, SDK/package, build id, logical size/DPI, command, expected/actual, screenshot/log và hạn chế. Thiếu môi trường phải ghi unverified và giữ checkbox liên quan mở. Waiver backend test của Change 1 không chứng minh UI và không tự áp dụng cho change mới.

## Risks / Trade-offs

- [Metadata toolkit phù hợp nhưng Uno/Skia runtime khác] → smoke trước khi chốt CPM; native composition fallback cùng contract; không nâng SDK.
- [Mẫu font/radius/label mâu thuẫn] → bảng quyết định trên là baseline rõ ràng; owner xem crop controls, tránh implementation chọn lại tùy ý.
- [Badge tint đẹp nhưng text contrast thấp] → foreground semantic đậm, đo contrast, chữ trạng thái luôn hiện; native system pairs trong HC.
- [ViewModel injection phá Frame activation/headless tests] → gallery Page tạo ở composition root, UI registration tách riêng; unit test không tham chiếu executable Uno.
- [Bundled assets hoặc typography thay đổi renderer] → chọn subset asset có license, kiểm thử glyph offline trên cả Windows và Skia; scale test không thay bằng screenshot100%.
- [Không có Windows/WinApp CLI hiện tại] → setup task cụ thể với lệnh chính thức, giữ Windows/API/HC checks unverified cho tới có evidence; không hứa đã chuẩn bị máy ở proposal.

## Migration Plan

Apply theo thứ tự tooling/compatibility → fonts/tokens/styles → controls → gallery → build/tests/runtime → owner acceptance. Chỉ thêm resources explicit và startup branch opt-in; không migration DB/API. Khi rollback, bỏ gallery flag trước, rồi gỡ UI registrations/dictionaries/assets/packages thuộc change; giữ core/API foundation nguyên vẹn. Main specs chỉ sync khi được yêu cầu ở workflow phù hợp; proposal hiện tạo delta specs.

## Sources and verification limits

- Local design sources và quyết định nêu ở §1; quyết định font/token là lựa chọn của proposal, chưa được owner nghiệm thu.
- [Uno WCT 8 guide](https://platform.uno/docs/articles/uno-community-toolkit-v8.html): WCT 8 hỗ trợ Uno; guide minh họa CPM 8.1.240916, không phải chứng nhận mọi package/version.
- [SettingsControls 8.2.250402 metadata](https://www.nuget.org/packages/CommunityToolkit.WinUI.Controls.SettingsControls/8.2.250402) và [SettingsCard usage](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/windows/settingscontrols/settingscard): căn cứ ứng viên và chức năng, chưa restore/build/run ứng viên trong proposal.
- [WinApp CLI installation](https://github.com/microsoft/WinAppCli), [usage](https://github.com/microsoft/WinAppCli/blob/main/docs/usage.md), [releases](https://github.com/microsoft/WinAppCli/releases): dùng release≥0.7, xác minh help phiên bản thực tế; không suy từ nhánh main rằng máy đã có API.
- [Inter license](https://github.com/rsms/inter/blob/master/LICENSE.txt), [JetBrains Mono license](https://github.com/JetBrains/JetBrainsMono/blob/master/OFL.txt), [Fluent System Icons](https://github.com/microsoft/fluentui-system-icons): căn cứ assets; exact font/icon revision và checksums sẽ được ghi lúc bundle ở apply.
