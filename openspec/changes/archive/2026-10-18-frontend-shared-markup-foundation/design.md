# Design

## Context

Xem `proposal.md` cho động cơ và phạm vi. Khảo sát ngày 18/10/2026:

- `TechStore.Client.csproj` đã bật `CSharpMarkup`; Uno SDK 6.7.30 và CPM hiện có không cần nâng để lập kế hoạch này.
- `App.xaml` nạp XamlControlsResources rồi Metrics, Colors, Typography, ControlStyles, Icons. Constructor App gọi InitializeComponent trước logging; OnLaunched chọn gallery hoặc Login/Shell theo session. Không sửa chọn phiên/route.
- Colors.xaml có theme dictionaries; primary Light cũ là #2563EB, trong khi StitchTokens và spec mới yêu cầu #0066FF, hover #0052CC, pressed #0040A8. Đây là lệch baseline đã quan sát, không phải yêu cầu giữ màu cũ.
- Consumer XAML gồm Catalog list/detail, Dashboard, Inventory, Orders, Customers và gallery; Dashboard code còn lookup `Application.Current.Resources`. Login/Shell/Settings C# dùng StitchTokens.
- Bốn controls là UserControl partial, constructor không tham số, DependencyProperties và XAML visual tree. Search đang truyền Text vào SearchCommand, null vào ClearCommand và trả focus về AutoSuggestBox.
- Gallery có DI constructor với ComponentGalleryViewModel, ShellDiagnosticViewModel, ContentDialogPresenter. Loaded/Unloaded quản lý PropertyChanged, presenter và diagnostic activation; resize điều chỉnh grids. Tests hiện có kiểm tra gallery VM/diagnostics/options, không dựng UI tree.
- MainPage là trang khung cũ có NavigationView bảy menu; App/DI không chọn nó làm Shell. Chuyển cấu trúc trang này không đưa menu đó vào navigation sản phẩm.

## Goals / Non-Goals

**Goals:** hoàn thiện toàn bộ migration trong một lượt apply; một nguồn semantic tokens; tương thích resource keys và DP API; giữ behavior, theme, offline assets và accessibility. Verification chạy sau khi hoàn thành toàn bộ implementation.

**Non-Goals:** viết lại màn hình dev khác, chuyển Dashboard, tích hợp Analytics/Seeder, đổi auth hoặc navigation, thêm route cho MainPage, đổi SDK/package, xóa App.xaml host. Không đổi backend/DTO/database.

## Decisions

### 1. C# resources với compatibility keys và một điểm đăng ký

Tạo các định nghĩa `AppColors`, `AppTypography`, `AppMetrics`, `AppIcons`, `ControlStyles` và một entry point đăng ký resource collection trong Styles. Các lớp/tên hàm cụ thể có thể điều chỉnh khi apply, nhưng public keys/type được giữ nguyên. Resources vẫn cung cấp ResourceDictionary/Style/IconSource để consumer XAML hiện tại đọc được; không thay toàn bộ bằng C# extension chỉ hoạt động với trang mới.

Trong App.xaml bỏ năm Source dictionary sau khi C# tương đương đã sẵn sàng, giữ XamlControlsResources. Đăng ký resources C# ngay sau InitializeComponent trong constructor, trước mọi Page/control/DI resolve UI. Thứ tự phụ thuộc tương đương: metrics/colors/fonts → text styles → control styles → icons. Không đăng ký mỗi lần navigation; bảo đảm gọi entry point lặp không tạo duplicate dictionaries hoặc đảo thứ tự override.

Giữ `App.xaml` vì đây là host application, không thuộc sáu View/control cần chuyển. Phương án xóa toàn bộ application XAML sẽ mở rộng bootstrap ownership không cần thiết.

### 2. Canonical palette và theme động

Lấy giá trị Light primary/hover/pressed từ spec hiện hành và StitchTokens. Giữ các semantic key khác với mapping rõ; Dark/Contrast dùng nhánh tài nguyên phù hợp, Contrast dựa vào màu hệ thống. Metrics/type/font assets giữ nguyên trừ khác biệt được mô tả rõ theo spec. Không tải fonts/icons qua mạng.

StitchTokens là compatibility facade giữ toàn bộ public members/type mà Login/Shell/Settings đang dùng, chuyển định nghĩa trùng về canonical constants. Không xóa facade hay sửa toàn bộ callsites của Dev 1. Existing consumers tạo brush cố định không tự có live theme nhờ facade; chỉ khẳng định dynamic theme cho shared resource path và controls/pages trong phạm vi này. Ghi tồn tại consumer cố định vào handoff Dev 1, không tuyên bố đã sửa theme toàn app.

Dynamic colors trong styles, icon và VisualStates phải giữ liên kết theme resource, không resolve một lần thành brush snapshot. Với SDK đã pin, probe runtime có phạm vi hẹp xác nhận resource binding từ C# cho Style Setter, IconSource và VisualState Setter trên một control đang mount. Dùng API có thật của SDK; nếu API đó không đáp ứng, adapter cập nhật khi theme thay đổi phải có attach/detach và cùng acceptance. Không dùng XamlReader/string XAML để giả chuyển đổi; không nâng SDK như fallback tự động.

Phương án giữ song song XAML tokens và StitchTokens bị loại vì tiếp tục drift; phương án thay theme bằng constants bị loại vì không đáp ứng live adaptation.

### 3. Giữ public contracts của controls

Giữ namespace `TechStore.Client.Controls`, kiểu UserControl, constructor không tham số, CLR wrappers và các static DependencyProperty fields. XAML consumer chưa chuyển vẫn dùng trực tiếp các control C#. Không chuyển public state sang field/CLR-only property.

| Control | Contract giữ lại |
| --- | --- |
| StatCard | Title, ValueText, UnitText, DetailText, TrendText, ErrorText, IsLoading, IconSource, Tone |
| SearchBoxControl | Label (default Tìm kiếm), PlaceholderText, Text (default rỗng), SearchCommand, ClearCommand |
| EmptyStateControl | Title, Description, ActionText, ActionCommand, IconSource |
| StatusBadge | Label, Tone; StatusTone gồm Neutral/Info/Success/Warning/Danger/Accent |

Giữ DP default values/type theo source; header/label và AutomationProperties có tên rõ. Bind visual tree khai báo bằng C# Markup; phần xử lý UI đặc thù như composition, focus, resize và lifecycle nằm trong View/control, không đưa WinUI vào VM. Không cần tạo VM cho từng shared control.

Search: text thay đổi cập nhật consumer ngay, Enter/query-submit chỉ gọi command một lần với string hiện hành; không submit khi kết thúc composition tiếng Việt. Clear giữ argument null, reset text, focus input và không kích hoạt search. Disabled/CanExecute áp dụng trước execute; command replacement và CanExecuteChanged cập nhật giao diện, không để subscription cũ. EmptyState action giữ command semantics hiện có; không có label/command thì ẩn, CanExecute false thì disabled. StatCard giữ ưu tiên loading > error > content, thiếu = dash, zero là giá trị thật. Badge đổi tone/state không phụ thuộc ngôn ngữ label và không tạo tab stop.

Chuyển từng control và xóa cặp XAML của nó trong cùng bước; không để hai class definitions cùng tên. Giữ visual template/state behavior theo specs, không giữ lỗi cũ nếu lỗi trái acceptance.

### 4. Gallery và MainPage

Gallery giữ DI signature, ViewModel/DiagnosticViewModel properties, fixtures, automation IDs, counters, controls/settings specimens, bảng và diagnostics. Event subscriptions phải cân bằng trên mount/unmount; presenter detach và diagnostic cancellation khi rời. Declarative binding thay manual PropertyChanged khi có thể; lifecycle vẫn cần xử lý UI thích hợp. Reflow theo viewport thực, không gán kích thước giả để chụp evidence.

Mở rộng specimens cho dynamic value/tone/command replacement, disabled CanExecute, missing optional content và theme while input exists. Kiểm chứng legacy XAML compatibility bằng consumer hiện có (Catalog/Dashboard) trong runtime smoke riêng; không giữ một gallery XAML cũ hoặc thêm production XAML mới để test.

MainPage.cs giữ namespace, constructor không tham số và nội dung khung hiện tại. Không đăng ký DI/route/menu mới hoặc thay entrypoint. Kiểm tra tạo trang bằng UI harness thích hợp, không dùng trang này làm Shell kiểm thử nghiệp vụ.

### 5. Specs và ownership

Delta bổ sung contracts tương thích ở ba capability đã có. Yêu cầu C# Markup trong main specs đã tồn tại; không lặp lại chúng dưới capability mới.

Known pre-existing conflict: `frontend-component-gallery` default-launch scenario còn nói offline development Shell, trong khi App hiện chọn Login/Shell theo session. Change này giữ source startup/auth hiện tại và chỉ kiểm tra gallery flag/đường resource integration, không rollback auth để thỏa scenario cũ. Ghi discrepancy với owner Dev 1; không coi đây là regression do migration hoặc tự sửa spec auth trong phạm vi foundation.

Bootstrap ownership: Dev 6 chỉ resource initialization và giữ các dòng session/window/navigation; Dev 1 review integration diff qua handoff. Không tự gửi tin/PR/commit. Nếu package chưa resolve, ghi lỗi môi trường và evidence thay vì thêm package reference trùng UnoFeatures.

### 6. Sửa probe sau review ngày 18/10/2026

Review source sau apply phần kiểm tra harness trước merge cho thấy probe lấy `TechStoreHeadingTextStyle`/icons từ XAML hiện có, PASS bằng non-null hoặc states count; không mount roots vào window. App gọi probe trước khi gắn Content rồi `Environment.Exit(0)` bất kể report. Vì vậy task 1.2/1.4 mở lại; chưa xác nhận cơ chế C# theme binding cần cho phần implementation resources/bootstrap. Đây là sửa cách kiểm chứng yêu cầu hiện hành, không mở rộng capability/spec.

Harness sửa phải đáp ứng các điều kiện sau:

1. Khai báo một bộ resource/theme/style/icon/visual-state tối thiểu bằng C# với API thực của SDK đã pin. Code source và symbol/API dùng phải được ghi trong evidence; chuỗi mô tả `{ThemeResource ...}` trong log không phải implementation. Không lấy style XAML cũ rồi gọi đó là kiểm chứng C# Setter.
2. Tạo cửa sổ hiển thị thực, gắn root vào Content, activate, đợi Loaded/layout và dispatch bất đồng bộ sau đổi theme. Không dùng blocking wait, không đọc màu trước khi root mount hoặc trước khi theme update. Có timeout báo thất bại, không biến timeout/no-display thành PASS.
3. Đặt expected colors theo Light/Dark/ActualTheme của System; so sánh actual foreground/background/border từng bước. System có thể theo Dark, không mặc định System=Light. Icons phải có visual consumer và được kiểm tra sau đổi theme. VisualStates phải được kích hoạt đủ sáu tones và kiểm tra tác dụng setters, không chỉ đếm states hoặc tạo Badge cũ. Giữ input/state để kiểm tra không bị reset.
4. Phân biệt static lookup với dynamic binding: lookup/gán brush snapshot là một phép kiểm tra riêng. Dùng consumer XAML hiện có để kiểm chứng compatibility với C# resource path, không thêm XAML production hoặc sửa feature ngoài scope; không dùng XamlReader. Harness không cần chuyển toàn bộ resources của phần implementation resources/bootstrap.
5. Aggregate report: bất kỳ assertion/exception/timeout nào thất bại thì exit code khác 0. Chạy riêng negative control với expected value cố ý sai và lưu kết quả FAIL; sau đó chạy expected đúng và lưu PASS. Không hard-code exit 0; không hard-code màu actual hoặc kết quả PASS. Giải phóng window/subscriptions khi kết thúc.
6. Harness có điểm chạy kiểm thử riêng, không để hook TECHSTORE_PROBE_THEME/Environment.Exit trong App production. Lựa chọn host/harness phù hợp Uno đã pin, không tạo đường startup sản phẩm mới. App diff ngoài phần resources phải trở về hành vi trước probe; không reset thay đổi của người dùng.
7. Evidence mới lưu raw log của positive/negative run trong thư mục change, command, exit code, OS/renderer/SDK, window Loaded/ActualTheme/viewport và expected/actual. Fingerprint phải bao gồm HEAD, tracked dirty diff và hash file untracked harness; HEAD hiện tại một mình không xác định được build đã chạy. Giữ báo cáo trước đó dưới nhãn historical/reported, chỉ kết luận verified theo run mới.

Chỉ tiếp tục phần implementation resources/bootstrap sau khi task 1.2 và 1.4 đạt trên môi trường Skia đang có. Windows vẫn có thể unverified ở verification cuối phần kiểm tra harness trước merge và được kiểm tra trong 5.3; không tuyên bố Windows đã pass. Nếu không có môi trường hiển thị cho probe thì báo blocker, giữ checkbox mở, không thay bằng headless/compile-only.


### 6.1. Acceptance của runtime probe (các review ngày 18/10/2026)

Harness `Testing/ThemeBindingProbeHarness.cs` hiện đã chạy trên Skia với raw logs, nhưng chưa chứng minh đầy đủ cơ chế cho phần implementation resources/bootstrap. Các sửa sau nằm trong task 1.2/1.4; không chuyển resources production trong lượt apply liên tục.

| Gap hiện tại | Sửa trong harness | Evidence bắt buộc |
|---|---|---|
| Style/icon/VisualState chỉ gán snapshot; adapter chỉ đổi Border/TextBlock riêng | Gán một `Style` thực có `Setter` semantic brush vào styled element rồi dùng cơ chế native hoặc adapter trên chính control đó; kiểm tra Style Setter thực sự được áp dụng. Dùng IconSource có visual consumer với geometry khác rỗng và badge sáu tones; đổi theme trên ancestor chung. Assert foreground/background/border áp dụng cho từng specimen ở từng theme; giữ control identity, input và tone state qua chuyển theme | Matrix specimen × Light/Dark/System; sáu tones trong mỗi theme, actual/expected và state trước/sau. Có thể giữ static lookup như test riêng, không dùng nó thay live-binding test |
| Default chỉ kiểm tra non-null | Đợi transition, đọc ActualTheme thực của consumer, chọn expected Light/Dark tương ứng từ bảng kỳ vọng độc lập; kiểm tra cả brush và màu. Không lấy actual làm expected | Log RequestedTheme, ActualTheme, expected/actual sau Default; stale brush sai palette phải FAIL |
| Compatibility chỉ kiểm tra keys tồn tại | Trong host kiểm thử, cung cấp semantic keys C# cho consumer XAML có sẵn và consumer C#. Đọc màu cả hai consumer ở Light và Dark; điều kiện PASS bắt buộc là conjunction của cả bốn expected values. Ghi rõ consumer/property/key và nguồn resource; dùng giá trị probe phân biệt để không thể pass bằng XAML cũ. Null/missing/mismatch phải FAIL. Override chỉ trong harness, restore khi kết thúc | Hai consumer đã mount nhận đúng resource C# và cùng đổi màu tại chỗ; không thêm XAML production, không cần migrate toàn bộ dictionaries ở phần kiểm tra harness trước merge |
| WaitForLayoutAsync bỏ qua kết quả WhenAny; token chưa được sử dụng | Check enqueue result; đợi Loaded/layout/dispatch với deadline và cancellation thực. Timeout phải throw/report fail; có watchdog cho trường hợp Loaded không đến. Cleanup subscriptions/window/resources trong finally trước khi kết thúc process | Run cưỡng bức timeout/no-mount có deadline hữu hạn, lý do failure và exit khác 0; không dùng timeout như dấu hiệu layout thành công |

Giữ negative control bằng expected sai thực sự; không chỉ ép boolean FAIL. Positive chỉ PASS khi mọi assertion bắt buộc được chạy và đạt; một test bị bỏ qua/unverified không được tính PASS. Adapter là ứng viên đã thử trên một Border/TextBlock, chưa phải giải pháp được nghiệm thu cho styles/icons/states. Không suy ra API native không hỗ trợ chỉ từ phép gán snapshot.

Lưu raw logs mới bằng tên riêng để bảo toàn hai logs hiện có; ghi build fingerprint mới, các lệnh và exit codes. Cập nhật handoff/evidence phân biệt kết quả cũ hẹp với acceptance mới. Windows native vẫn có thể unverified nếu thiếu runner; ghi rõ trong kết quả verification cuối. Probe R1–R4 kiểm tra behavior sau khi hoàn tất implementation, không phải điều kiện để bắt đầu một phần implementation khác. Owner UI acceptance được ghi riêng.

## Risks / Trade-offs

- [C# theme bindings khác giữa renderers] → Probe theo SDK đã pin; runtime theme cả hai target, không kết luận từ compile.
- [Resources khác type/order làm crash consumer cũ] → Inventory keys/types/BasedOn dependencies; registry smoke và Catalog/Dashboard regression.
- [Palette mới đổi ảnh UI] → Ghi mapping #2563EB → #0066FF theo existing spec; owner xác nhận build mới, không kế thừa acceptance cũ.
- [Command handlers bị gắn hai lần] → Kiểm tra counters sau nhiều mount/unmount, CanExecute changes và IME composition.
- [Specimens headless tạo cảm giác đã test UI] → Tách unit tests với runtime evidence; headless csproj chỉ link pure production logic.
- [Dev 1 sửa bootstrap đồng thời] → Re-read diff trước apply phần implementation resources/bootstrap; giữ patch resource nhỏ; không thay session semantics.
- [Không có Windows runner] → Đánh dấu Windows/Contrast unverified và để task acceptance tương ứng mở.

## Apply toàn change và verification cuối

1. Đọc lại source/requirements và inventory contracts; đối chiếu code đã có trên branch hiện tại.
2. Hoàn thành mọi phần còn thiếu trong resources/bootstrap, controls, gallery và MainPage trong một lượt apply. Giữ phần đã merge từ `dev` nếu đáp ứng requirements; không tạo implementation trùng.
3. Sau khi toàn bộ implementation hoàn tất, chạy build, unit/integration/runtime verification cần thiết, rồi cập nhật evidence và handoff.
4. Ghi platform chưa chạy là unverified; owner UI acceptance là xác nhận riêng. Không dừng apply giữa các phần implementation.
Không migration database/deploy. Nếu một giai đoạn chưa đạt, khôi phục phần thay thế của chính giai đoạn và resource references tương ứng, giữ dữ liệu/source thay đổi ngoài phạm vi; không reset worktree. Không xóa XAML trước khi replacement đầy đủ trong lượt apply; verification chạy sau khi hoàn tất toàn bộ implementation. Các tệp handoff/evidence chỉ được tạo ở apply, không ghi kết quả runtime giả trong planning.

Commands dự kiến khi apply: `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`; native Windows build theo môi trường Windows; `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`; gallery qua `TECHSTORE_UI_GALLERY=1`. Evidence ghi build hash hoặc HEAD + dirty diff hash, OS/renderer, viewport DIP/DPI/text scale/theme, expected/actual và ảnh/log. Snapshot production logic/API behavior giữ nguyên; kiểm tra live Catalog chỉ là regression nếu backend sẵn sàng, không phải Analytics acceptance.


### 6.2. Acceptance bổ sung cho Style Setter và mixed consumers

Review source của harness sau lần sửa R1–R4 cho thấy specimen `styledTextBlock` chưa có `Style` hoặc `Setter`; nó chỉ nhận `Foreground` qua adapter. Vì thế chưa có bằng chứng Style Setter hoạt động. Ở R3, màu XAML `StatusBadge` được đọc và ghi log nhưng không tham gia biến `pass`; chỉ màu C# consumer quyết định PASS. Do đó positive log cũ không chứng minh XAML consumer tương thích.

Trong đợt verification cuối sau khi hoàn tất implementation:

1. Gắn một `Style` C# có `Setter` vào element được assert. Setter phải tham chiếu semantic brush trong probe dictionary; cùng element phải được kiểm tra qua Light/Dark/System. Log ghi style, key, setter, expected và actual.
2. Tại R3 assert cả XAML và C# consumer ở Light và Dark. Công thức đạt phải tương đương `xamlLight == expectedLight && xamlDark == expectedDark && csharpLight == expectedLight && csharpDark == expectedDark`; mọi thiếu element/key hoặc mismatch đều FAIL. Negative run phải chứng minh assertion XAML sai cũng khiến aggregate run thất bại.
3. Tạo positive/negative logs mới và fingerprint khớp chính xác source. Không sửa/ghi đè logs cũ; đánh dấu chúng là lịch sử chưa đủ bằng chứng R1/R3.
4. Sau khi hoàn tất toàn bộ implementation, rerun desktop build, Frontend.UnitTests và startup smoke; cập nhật evidence/handoff. Các kiểm tra R1–R4 là acceptance trong verification cuối, không tạo checkpoint riêng.


### Apply toàn change sau khi sync dev

Branch hiện tại đã fast-forward tới `dev`, nơi một phần lớn C# Markup foundation đã được tích hợp qua change đã archive. AI apply phải đọc source mới nhất, map requirements vào implementation hiện có, hoàn tất phần thiếu trong phạm vi này rồi mới chạy toàn bộ verification cuối. Không dùng các task/artifact lịch sử như chỉ thị dừng sau từng nhóm; không lặp lại code đã đạt yêu cầu.
