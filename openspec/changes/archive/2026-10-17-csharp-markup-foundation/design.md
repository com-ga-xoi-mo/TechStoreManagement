# Design

## Context

Lý do của change xem `proposal.md` (mục Why). Hiện trạng kỹ thuật ảnh hưởng tới cách làm:

- **Resource dùng chung.** `App.xaml` merge `XamlControlsResources` cùng 5 dictionary `Styles/*.xaml`, tổng khoảng 130 key:
  - `Colors.xaml` có ThemeDictionaries Light/Dark/HighContrast; HighContrast trỏ tới màu hệ thống.
  - `ControlStyles.xaml` và `Typography.xaml` là các `Style` tham chiếu chéo tới font, metric và brush.
  - `Icons.xaml` là các `PathIconSource`.
- **Ai đang dùng resource.** 6 trang XAML và 4 base controls tham chiếu resource theo tên key (`{ThemeResource TechStoreCanvasBrush}`, `{StaticResource TechStorePanelStyle}`...). Riêng `DashboardPage.xaml.cs` đọc `Application.Current.Resources["TechStoreSecondaryTextBrush"]`.
- **Base controls hiện tại.** Đều là `UserControl` XAML, API qua DependencyProperty. Mỗi control có một hàm `Refresh()` tự đặt `Visibility` và `Run.Text`. `SearchBoxControl` bắt `TextChanged`, `QuerySubmitted` và `Click`. `StatusBadge` đổi màu bằng `VisualStateManager`, với setter dùng `ThemeResource`.
- **Ràng buộc từ test headless.** `tests/Frontend.UnitTests` link source thuần C# từ `ViewModels/**`, `Services/Development/**`, `Services/Theme/**`, `Controls/Charts/**`... vào một project `net10.0` không có WinUI. Vì vậy, file nào nằm trong các thư mục này thì không được tham chiếu kiểu WinUI.
- **Package Markup.** `Uno.WinUI.Markup`, `Uno.Extensions.Markup.WinUI` và `Uno.Extensions.Markup.Generators` 6.7.21 đã có trong restore qua `UnoFeatures` `CSharpMarkup`.
- **Trang C# Markup đã có (PR #10, `845c522`).** `LoginPage.cs`, `SettingsPage.cs` và `ShellPage.cs` đã dùng fluent API với binding thật. Mẫu chung của cả ba:
  - page nhận ViewModel qua constructor, giữ thuộc tính `ViewModel` và đặt `DataContext = viewModel`;
  - binding viết `.Text(x => x.Binding(() => ViewModel.Prop).Mode(BindingMode.TwoWay))`;
  - đổi kiểu bằng `.Convert((bool v) => ...)`;
  - command viết `.Command(x => x.Binding(() => ViewModel.Cmd))`.

  Tuy vậy, màu trong ba trang này vẫn dùng `StitchTokens` / `Color.FromArgb`, chỉ có màu sáng. Repo chưa có ví dụ nào về token theo theme hay shared control viết bằng Markup.
- **Ngoại lệ đã có trong repo.** `LoginPage` vẫn dùng `PasswordChanged +=` và `ViewModel.PropertyChanged +=` cho `PasswordBox`, vì `PasswordBox.Password` không phải DependencyProperty. Design của PR #10 có ghi nhận điều này.
- **Tài liệu đang lệch.** AGENTS.md vẫn ghi `x.Bind(...)`, design của PR #10 ghi `.Bind(...)`, còn code thật dùng `x.Binding(...)`.
- **Gallery.** Gallery chạy ngoài Shell: khi `TECHSTORE_UI_GALLERY=1`, `App.xaml.cs` gán thẳng `MainWindow.Content = ComponentGalleryPage`. Theme của gallery do code-behind đặt `RequestedTheme` theo `ThemeIndex`.

## Goals / Non-Goals

**Goals:**
- Một chuẩn viết C# Markup được chứng minh bằng code chạy thật, để Dev 1–6 sao chép được.
- Gỡ `Styles/*.xaml` mà không làm hỏng màn hình nào chưa chuyển đổi.
- Logic trình bày của controls và trang mẫu tách thành phần thuần C# có thể kiểm thử headless.

**Non-Goals:**
- Không tạo hệ thống theme mới (chẳng hạn Uno.Themes/Material). Change này chỉ chuyển nơi định nghĩa resource, không đổi cơ chế theme của WinUI.
- Không thiết kế bảng màu Dark mới theo Stitch, vì Stitch chỉ có bản Light.
- Không thay `StatusTone` hay các API công khai của controls.

## Decisions

### D1. Chuẩn viết View/Control bằng C# Markup

- **Cách dựng:** View và control dựng cây UI bằng API fluent của `Uno.Extensions.Markup`, kiểu `new TextBox().Text(...)`, `.Children(...)`.
- **Gắn ViewModel:** theo đúng mẫu của các trang PR #10. Page nhận ViewModel qua constructor, giữ thuộc tính `ViewModel` và đặt `DataContext = viewModel`.
- **Binding và command:** dùng cú pháp đang chạy trong repo:
  - `x => x.Binding(() => ViewModel.Prop)`, kèm `.Mode(BindingMode.TwoWay)` khi cần hai chiều;
  - `.Convert(...)` khi cần đổi kiểu;
  - `.Command(x => x.Binding(() => ViewModel.Cmd))`.

  Trang mẫu và controls viết cùng kiểu này để cả nhóm chỉ có một cách viết.
- **Cú pháp:** cú pháp binding lấy từ `LoginPage.cs` / `SettingsPage.cs` làm chuẩn, không nghiên cứu lại từ đầu. Chỉ cần tra thêm hai điểm repo chưa có ví dụ: áp ThemeResource từ code, và generator cho `UserControl` (D3, D5). Cú pháp cuối cùng được ghi vào AGENTS.md, thay cho `x.Bind(...)`.
- **Điều cấm:** danh sách cấm áp dụng cho việc **đồng bộ dữ liệu với ViewModel trong View**, như đã nêu trong `proposal.md`.

Phương án khác đã cân nhắc:
- (a) Theo kiểu C# mệnh lệnh mà `LoginPage.cs` dùng trước PR #10. Loại vì mỗi thuộc tính phải tự nối hai chiều, quên nối thì compiler không báo, và View chứa logic. PR #10 cũng đã bỏ kiểu này.
- (c) Mẫu DataContext dạng lambda `(page, vm)` của Markup. Không chọn, để thống nhất với ba trang đã có.
- (b) Giữ XAML. Loại vì kế hoạch Sprint 2 đã chốt C# Markup.

**Kết quả triển khai (task 1.1).** Nhánh `feature/csharp-markup-foundation` tạo từ `dev` tại `845c522` (gồm PR #10). Mốc đo lại sau PR #10: `dotnet test Frontend.UnitTests` = 207 pass, build `net10.0-desktop` 0 lỗi. Bước chặn smoke test bằng giao diện (đăng nhập `admin`/`Admin@123`; sửa tên cửa hàng, lưu, tải lại, đổi theme) ban đầu chưa kiểm vì không thao tác được GUI; sau đó **đã kiểm và đạt** bằng thao tác thật (`xdotool` trên Xvfb, Linux Skia): (a) đăng nhập bằng giao diện vào được Shell; (b) sửa tên cửa hàng, lưu, tải lại giữ giá trị, đổi theme bằng ComboBox áp dụng. Vì vậy cú pháp binding của PR #10 (`x.Binding(...).Mode(...)`, `.Convert(...)`, `.Command(x => x.Binding(...))`) được xác nhận làm chuẩn. Bằng chứng ghi ở task 1.1.

**Kết quả pilot (task 3.2-3.5), chạy thật trên Linux Skia bằng probe tạm đã xóa.** Cú pháp thực tế cho shared control:
- Giữ `UserControl`; dựng cây trong constructor; `Content = new X().DataContext(this)...`. Do `DataContext` của control phải để consumer đặt, cây bên trong đặt `DataContext(this)` ở phần tử gốc và bind qua thuộc tính private `Self => this`: `.Text(x => x.Binding(() => Self.Label))`. Cách này chạy được cho OneWay, TwoWay (`.Mode(BindingMode.TwoWay).UpdateSourceTrigger(UpdateSourceTrigger.PropertyChanged)`), `.Convert((T v) => ...)` và `.Command(x => x.Binding(() => Self.Cmd))`.
- DP công khai khai báo bằng `DependencyProperty.Register` như cũ; DP phái sinh là DP private (`DisplayState`, `HasAction`), được gán trong callback của DP nguồn từ helper `Controls/Presentation`.
- Màu theo theme: `ThemeResource.Get<Brush>(key)` / `AppColors.Brushes.X` áp từ code cập nhật tại chỗ khi đổi theme, không cần VisualStateManager dự phòng. Lưu ý: `Style` đặt bằng `.Style(...)` sau khi đã áp brush có thể đè brush, nên các control gọi lại `ApplyThemeColors()` trong `Loaded`.
- Generator: không cần khai báo `GenerateMarkupForAssembly`. Extension fluent cho DP của control (`new StatusBadge().Label(...).Tone(...)`, `new SearchBoxControl().Text(x => x.Binding(...))`) build và chạy được trong `StatCard` và `MarkupReferencePage` (đã chạy thật bằng probe tạm).
- Sự kiện giữ lại đúng như D2: `QuerySubmitted` và `Click` của nút Xóa (trả focus).
- Khi probe tìm nút Xóa phải lọc theo nội dung, vì `AutoSuggestBox` có `Button` nội bộ.

### D2. Ranh giới "logic của control" và "đồng bộ ViewModel"

Control không có ViewModel; API của nó là các DependencyProperty. Quy ước như sau:

- **Callback khi DP đổi được phép.** Callback chỉ tính các thuộc tính trình bày phái sinh qua helper thuần C#, rồi gán vào DP phái sinh private hoặc read-only (ví dụ giá trị hiển thị `—`, cờ hiển thị phần trend/detail/error). Cây UI bind vào các thuộc tính phái sinh này. Nhờ vậy bỏ được kiểu `Refresh()` tự đặt `Visibility` lên từng phần tử.
- **Ngoại lệ có chủ đích:**
  - `SearchBoxControl` dùng sự kiện `QuerySubmitted` để phát `SearchCommand`, vì AutoSuggestBox không có command cho submit.
  - Action xóa trả focus về ô nhập, vì focus không biểu diễn được bằng binding.
  - Ngoại lệ đã có trong repo, được ghi vào AGENTS.md như mẫu hợp lệ: đồng bộ `PasswordBox` bằng `PasswordChanged` (cùng `PropertyChanged` để nhận lệnh ẩn/hiện), vì `PasswordBox.Password` không phải DependencyProperty.
- **Ô nhập tìm kiếm:** text của ô nhập bind TwoWay với DP `Text` của chính control, không bắt `TextChanged`.
- Mọi ngoại lệ mới phát sinh khi implement phải được bổ sung vào mục này.

Phương án khác đã cân nhắc: dùng converter đa giá trị cho từng phần tử. Loại vì khó đọc và khó kiểm thử hơn helper thuần C#.

### D3. Token: một bảng giá trị, đăng ký resource bằng code, giữ key cũ

- **Nguồn giá trị duy nhất:** các lớp `AppColors`, `AppTypography`, `AppMetrics`, `AppIcons`, `AppControlStyles` (trong `Styles/`) giữ:
  - hằng số tên key, giữ nguyên chính xác tên cũ;
  - bảng giá trị theo theme Light/Dark/HighContrast, với HighContrast tiếp tục trỏ tới màu hệ thống.
- **Đăng ký:** một điểm đăng ký dựng `ResourceDictionary` (gồm ThemeDictionaries và các `Style` tham chiếu chéo) và merge vào `Application.Resources` khi khởi động. Việc này phải xong **trước khi tạo trang đầu tiên** (Login, Shell hoặc Gallery).
- **Tách hai phần để test headless được:**
  - *Hằng số key* (tên key, danh sách key theo nhóm và theo theme) đặt ở file thuần C#, không tham chiếu kiểu WinUI, để link được vào `tests/Frontend.UnitTests`.
  - *Phần dựng giá trị* (`Color`, `SolidColorBrush`, `Style`, `FontFamily`, `PathIconSource`, `ResourceDictionary`) dùng WinUI, nằm ở file riêng và không link vào test.
- **Thứ tự đăng ký:** merge **sau** khi `App.xaml` đã nạp `XamlControlsResources` (sau `InitializeComponent`), vì `TechStorePrimaryButtonStyle` kế thừa `AccentButtonStyle` của WinUI. Các `Style` trong cùng dictionary phải được tạo sau font, metric và brush mà chúng tham chiếu.
- **Icon (`PathIconSource`):** dữ liệu path giữ nguyên dạng chuỗi trong bảng token. Phần dựng giá trị đổi chuỗi sang `Geometry` bằng bộ chuyển đổi của nền tảng:
  - Trên Uno/Skia: `Uno.UI` 6.7.135 có `GeometryConverter` / `ParseGeometry` và phép chuyển kiểu ngầm từ chuỗi; đã kiểm tra có trong package.
  - Trên WinAppSDK: dùng `XamlBindingHelper.ConvertValue(typeof(Geometry), data)`; chưa kiểm.
  - Không chuyển icon sang font glyph trong change này, để giữ nguyên hình icon hiện có.
  - `Foreground` của icon vẫn tham chiếu `TechStoreInfoTextBrush` theo theme.
- **`App.xaml`:** chỉ còn `XamlControlsResources`.
- **Accessor cho trang C# Markup:** trả về **tham chiếu resource theo theme**, tức key được áp dụng qua cơ chế ThemeResource của Markup, không trả về `Brush`/`Color` tĩnh. Có như vậy màu mới tự đổi khi đổi theme.
  - *Gợi ý cho task 1.2 (mới thấy có trong package, chưa chạy thử):* `Uno.Extensions.Markup.dll` 6.7.21 có sẵn `ThemeResourceKey<T>`, `ThemeResource<T>`, `StaticResourceKey<T>`, `ResourceKeyDefinitionAttribute` và `ResourceKeyExtensions`. Ưu tiên dùng các kiểu này để định nghĩa accessor có kiểu, ví dụ một `ThemeResourceKey<Brush>` cho mỗi key màu, thay vì tự viết cơ chế riêng.
  - Các kiểu này phụ thuộc WinUI, nên chúng nằm ở phần dựng giá trị. Hằng số key dạng chuỗi vẫn ở file thuần C# để test headless dùng được.

Phương án khác đã cân nhắc:
- (a) Giữ `Styles/*.xaml` và chỉ thêm accessor C#. Đây là cách ít rủi ro nhất, nhưng trái yêu cầu gỡ XAML styles của kế hoạch. Giữ làm phương án dự phòng nếu D3 gặp lỗi nền tảng (xem Risks).
- (b) Màu tĩnh kiểu `StitchTokens`. Loại vì không theo theme.

**Kết quả tra cứu và triển khai (task 1.2, 2.1-2.4).**
- Nguồn tra: `Uno.Extensions.Markup.dll` 6.7.21 (đọc metadata) và thử biên dịch. Đã xác nhận bằng build thật:
  - `ThemeResource.Get<Brush>("Key")` và `StaticResource.Get<T>("Key")` trả về `Action<IDependencyPropertyBuilder<T>>` (không phải `ThemeResourceKey<T>`); truyền thẳng vào `.Background(...)`, `.Foreground(...)`, `.Style(...)`.
  - `Uno.Extensions.Markup.Style<T>().Setters(s => s.Foreground(ThemeResource.Get<Brush>(...)))` dùng được cho Style dựng từ code. `Style<T>` là builder, không phải `Style`; phải chuyển bằng `(Style)styleT` trước khi đặt vào `ResourceDictionary` (đã bọc trong hàm `Put`).
- **Accessor có kiểu** (Task 2.3), trong file `*.Resources.cs` (dùng WinUI, không link vào test):
  - `AppColors.Brushes.Canvas` (và 29 màu còn lại) cho màu theo theme: `new Border().Background(AppColors.Brushes.Canvas)`, `new TextBlock().Foreground(AppColors.Brushes.SecondaryText)`;
  - `AppTypography.Styles.BodyText` (cũng có `HeadingText`, `SectionText`, `CaptionText`, `MetricText`, `MonoText`) và `AppTypography.Fonts.*`;
  - `AppControlStyles.Styles.Panel`, `.PrimaryButton`...; `AppMetrics.Resources.*`; `AppIcons.Sources.*`.
  - Hằng số key dạng chuỗi (`AppColors.Canvas == "TechStoreCanvasBrush"`) nằm ở file thuần C# (`AppColors.cs`...) để test.
- **Kiểm tra runtime trên Linux Skia, đã sửa lại sau khi test GUI thật.** Probe đầu tiên (đã xóa) chỉ thử Style gán cho element đã nằm trong cây, và kết luận "Style dựng bằng Markup đổi theme tại chỗ". Kết luận đó **chỉ đúng cho một trường hợp**, xem ma trận bên dưới. `ThemeResource.Get<Brush>` đặt **trực tiếp lên element** thì đổi theme đúng; `ThemeResource` trong **Setter của Style dựng bằng Markup** thì không đáng tin.

**Ma trận điều tra (probe tạm `ZzRepro`, đã xóa; ghi `Background` thực tế của `Border` sau khi đổi `RequestedTheme` của root Light, Dark, Light; Surface = `#FFFFFF` Light, `#1E293B` Dark).** Kết quả sau khi đổi sang Dark:

|Style (Setter Background = ThemeResource Surface)|Gán `Style` từ code khi tạo element|Gán `Style` từ code sau khi element đã Loaded|XAML `Style="{StaticResource}"`|XAML `Style="{ThemeResource}"`|
|:---|:---|:---|:---|:---|
|Markup `Style<Border>().Setters(s => s.Background(ThemeResource.Get<Brush>(k)))`|không đổi (#FFFFFF)|đổi (#1E293B)|không đổi|không đổi (lần chạy sau), đổi (lần chạy đầu): không ổn định|
|Markup `s.Background(x => x.ThemeResource(k))`|không đổi|đổi|không đổi|không đổi|
|`XamlReader.Load` của `<Style>` có `Setter Value="{ThemeResource k}"`|đổi|đổi|đổi|đổi|
|`XamlReader.Load` của `<ResourceDictionary>` chứa Style (dạng dùng trong `AppResources`)|đổi|đổi|đổi|đổi|

Đọc thêm: `Setter.Value` của Style dựng bằng Markup là `List<BindingCallback>` (hàm gọi lại áp một lần khi Style được áp lên element), còn Setter từ XAML parser mang tham chiếu ThemeResource được Uno đăng ký để làm mới (`ThemeResourceReference` / `ResourceBinding`, kiểu internal). `ResourceDictionaryBuilder.Add(key, style)` của Markup chỉ lưu builder `Style<T>` thô, không đổi gì về cơ chế này. Các kiểu `ThemeResourceKey<T>`, `StaticResourceKey<T>`, `ResourceDictionaryBuilder.Add(ThemeResourceKey<Brush>, Color, Color)` chỉ hỗ trợ dựng **giá trị trong dictionary theme**, không sửa được Setter của Style. Dùng được các kiểu internal của Uno để tự dựng Setter có ThemeResource, nhưng đó không phải API Markup và dễ gãy khi nâng Uno, nên không chọn.

**Quyết định.** Không có cách thuần Markup nào đạt cả hai loại consumer (trang XAML qua `{StaticResource}` và element Markup khởi tạo trong constructor). Áp dụng phương án dự phòng đã định: Style **mang màu theo theme** được dựng bằng `XamlReader.Load` một `ResourceDictionary` nhỏ từ C# (cùng kỹ thuật với icon), key, `BasedOn` và giá trị giữ nguyên; không thêm file `.xaml`. Gồm 6 Style chữ (`Body`, `Heading`, `Section`, `Caption`, `Metric`, `Mono`), `FieldError`, `TableHeader`, `Panel` và `DangerButton`. Style không mang màu theo theme (Button, PrimaryButton, QuietButton, TextBox, AutoSuggestBox, ComboBox, TableRow) vẫn dựng bằng Markup. Màu đặt trực tiếp lên element bằng `AppColors.Brushes.*` vẫn dùng bình thường.

**Kiểm tra sau sửa (Skia, Xvfb).** Probe với key thật `TechStorePanelStyle`: cả 4 kiểu consumer đổi `#FFFFFF` sang `#1E293B` rồi về `#FFFFFF`. GUI: gallery Sáng, Tối, Sáng; Shell, Cài đặt Sáng rồi Sản phẩm; `MarkupReferencePage` Sáng và Tối giữ nguyên query `iphone`. Ảnh trong `~/.cache/techstore-uitest/fix-*.png`. Chưa kiểm: HighContrast lúc chạy, target Windows.
- **Icon, lệch nhỏ so với D3.** `PathIconSource` là `DependencyObject` không phải `FrameworkElement`, nên không đặt được `ThemeResource` cho `Foreground` từ code (nếu đặt `SolidColorBrush` tĩnh thì icon không đổi màu theo theme). Cách làm: tạo `PathIconSource` bằng `XamlReader.Load` chỉ với `Foreground="{ThemeResource TechStoreInfoTextBrush}"` (không có file `.xaml`), rồi gán `Data` qua hàm `AppResources.ToGeometry` duy nhất (Uno `GeometryConverter`; nhánh `#if WINDOWS` dùng `XamlBindingHelper.ConvertValue`, **chưa kiểm**). Probe xác nhận `Foreground` icon đổi `#BFDBFE` sang `#1D4ED8` theo theme và `Data` là `StreamGeometry`. Icon được tạo sau khi dictionary đã merge, để tránh cảnh báo "Couldn't statically resolve resource".
- **Đăng ký** trong constructor `App` ngay sau `InitializeComponent()` (`AppResources.Register(this)`), trước khi tạo window. Nút primary kế thừa `AccentButtonStyle` lấy từ `XamlControlsResources`; kiểm tra bằng probe `AccentButtonStyle` có trong resources lúc đăng ký.
- Khởi động `net10.0-desktop` ở chế độ thường (Login) và gallery: log không có cảnh báo/lỗi resource hay `BasedOn` (chỉ có nhiễu môi trường: RemoteControlClient, X11 `_NET_WM_STATE`, spell checking). Shell chưa mở được ở bước này vì cần phiên đăng nhập.
- Test snapshot toàn bộ key của 5 file `Styles/*.xaml` cũ (30 màu, 5 font, 6 style chữ, 12 metric, 5 icon, 11 style control) nằm ở `tests/Frontend.UnitTests/Styles/DesignTokenKeysTests.cs`.

### D4. Bảng màu

- **Light:** nhóm primary theo spec (`#0066FF`, hover `#0052CC`, pressed `#0040A8`). `TechStoreFocusBrush` cũng đổi theo primary (`#0066FF`) để vòng focus đồng bộ với màu chủ đạo.
- **Giữ nguyên:**
  - các nhóm semantic Info, Success, Warning, Danger, Accent, Neutral;
  - toàn bộ bảng Dark và HighContrast hiện có;
  - canvas, surface, stroke và text (vốn đã trùng giữa hai bảng).
- **Kiểm tra contrast:** khi implement, kiểm tra lại cặp chữ trắng trên `#0066FF` và các cặp liên quan theo yêu cầu "Readable foreground and indicators".
- **Việc tồn đọng của Dev 1:** `StitchTokens.cs` không bị sửa trong change này. Dev 1 sau đó chuyển `Login`/`Shell`/`Settings` sang accessor theo theme rồi xóa `StitchTokens`.

**Kết quả đo contrast (task 2.5).** Tính theo công thức WCAG 2.x (độ chói tương đối):

| Cặp | Tỉ lệ | Yêu cầu | Kết quả |
|:---|:---|:---|:---|
| Chữ trắng `#FFFFFF` trên `#0066FF` | 4.83:1 | 4.5:1 (chữ thường) | Đạt |
| Chữ trắng trên hover `#0052CC` | 6.82:1 | 4.5:1 | Đạt |
| Chữ trắng trên pressed `#0040A8` | 9.14:1 | 4.5:1 | Đạt |
| Focus `#0066FF` trên canvas `#F8FAFC` | 4.62:1 | 3:1 (thành phần giao diện) | Đạt |
| Focus `#0066FF` trên surface `#FFFFFF` | 4.83:1 | 3:1 | Đạt |
| Focus `#0066FF` trên subtle `#F1F5F9` | 4.41:1 | 3:1 | Đạt |
| Focus `#0066FF` trên stroke `#E2E8F0` | 3.92:1 | 3:1 | Đạt |

Không cặp nào cần phương án xử lý. Lưu ý: nút `AccentButtonStyle` của WinUI lấy nền từ màu accent hệ thống, không dùng `TechStorePrimaryBrush`; việc này không đổi trong change này.

### D5. Cấu trúc base controls

- **Kiểu control:** giữ `UserControl` làm base. Như vậy namespace, tên lớp, tên/kiểu/giá trị mặc định của DependencyProperty và cách đặt control trong XAML (`controls:StatCard ...`) đều không đổi.
- **Thay đổi:** cây UI chuyển từ `.xaml` sang dựng bằng Markup trong constructor; xóa các file `.xaml` / `.xaml.cs` tương ứng.
- **Helper thuần C#:** đặt ở thư mục không tham chiếu WinUI, đề xuất `Controls/Presentation/`, và link vào test headless. Gồm:
  - trạng thái hiển thị của StatCard (loading > lỗi > nội dung, giá trị thiếu hiện `—`);
  - quyền hiện action của EmptyState;
  - hiện/ẩn của StatusBadge;
  - ánh xạ `StatusTone` sang bộ key Fill/Stroke/Text, tone lạ rơi về Neutral.
  Helper chỉ trả về `bool`/`string`/key. Việc đổi sang `Visibility` làm bằng converter trong binding.
- **Màu tone của StatusBadge/StatCard:** áp dụng qua ThemeResource theo bộ key do helper trả về, để màu đổi tại chỗ khi đổi theme. Nếu ThemeResource áp từ code không cập nhật khi đổi theme trên Skia, dùng phương án dự phòng: dựng `VisualStateManager` với các setter ThemeResource bằng C#, giống bản XAML hiện tại.
- **Generator:** dùng `Uno.Extensions.Markup.Generators` để có extension fluent cho DependencyProperty của control, nếu generator hỗ trợ `UserControl` trong project. Nếu không hỗ trợ, consumer C# Markup đặt thuộc tính bằng API binding chung của Markup, và ghi rõ điều này trong AGENTS.md.
  - *Gợi ý cho task 1.2 (mới thấy có trong package, chưa chạy thử):* `Uno.Extensions.Markup.Generators` 6.7.21 có `GenerateMarkupForAssemblyAttribute`. Có thể cần khai báo attribute này ở cấp assembly cho `TechStore.Client` thì generator mới sinh extension fluent cho controls trong chính project. Xác nhận bằng pilot `StatusBadge` (task 3.2) và ghi cách khai báo thật vào AGENTS.md.

### D6. Trang mẫu tham chiếu và cách mở từ gallery

- **Vị trí file:** `Views/Development/MarkupReferencePage.cs` và `ViewModels/Development/MarkupReferenceViewModel.cs`.
- **ViewModel:**
  - kế thừa `PageViewModel`;
  - không có kiểu WinUI, vì `ViewModels/**` được link vào test headless;
  - dữ liệu minh họa cố định;
  - các command chuyển trạng thái tải/rỗng/lỗi/nội dung;
  - query tìm kiếm, bộ đếm số lần submit và action đặt lại;
  - `ThemeIndex` (int).
- **Theme của trang:** View bind `RequestedTheme` với `ThemeIndex` qua converter, không bắt `PropertyChanged`. Đây cũng là ví dụ mẫu cho cách bind thuộc tính có kiểu WinUI.
- **Điều hướng giữa gallery và trang mẫu:**
  - Gallery chạy trực tiếp trong `MainWindow`, không có Frame. Vì vậy thêm một contract nhỏ thuần C# trong `Services/Development` để yêu cầu hiển thị trang mẫu hoặc quay lại gallery.
  - Phần hiện thực đổi `MainWindow.Content` đặt ở `Views/Development`, không đặt ở `Services/Development` vì thư mục đó được link vào test headless.
  - Contract chỉ được đăng ký khi `GalleryOptions.Enabled` bật.
- **Thay đổi trên gallery:** đúng một nút "Mở mẫu C# Markup".

Phương án khác đã cân nhắc:
- (a) Đưa trang mẫu vào Shell. Loại vì vi phạm yêu cầu không có điểm vào khi gallery tắt.
- (b) Thêm cờ cấu hình riêng. Loại vì thêm một bề mặt cấu hình phải validate trong khi gallery đã có sẵn.

### D7. Kiểm thử

- **Test headless:**
  - các helper ở D5;
  - độ đầy đủ theo theme: mỗi key màu có mặt trong Light, Dark và HighContrast, và các key HighContrast được khai báo là tham chiếu màu hệ thống (kiểm trên bảng hằng số thuần C# ở D3);
  - `MarkupReferenceViewModel`: chuyển trạng thái, submit đúng một lần, đặt lại, giữ query khi đổi theme;
  - test so khớp: danh sách hằng số key trong các lớp token phải bằng đúng danh sách key trích từ 5 file XAML cũ. Danh sách này ghi cố định trong test làm snapshot, nhằm bắt lỗi gõ sai hoặc thiếu key.
- **Kiểm tra bằng lệnh tìm kiếm:**
  - các file mới trong `Views/` và `Controls/` không chứa `PropertyChanged +=`, `TextChanged +=`, `Color.FromArgb`;
  - `Styles/` không còn file `.xaml`.
- **Kiểm tra runtime trên Linux Skia:** 6 trang XAML (chạy kèm backend để có dữ liệu thật), gallery và trang mẫu, ở Light và Dark.
  - HighContrast là cài đặt hệ thống của Windows; `ElementTheme` chỉ có Default/Light/Dark nên không kích hoạt được trên Skia. HighContrast được kiểm bằng test độ đầy đủ theo theme ở trên; phần chạy thật ghi là chưa kiểm.
  - Target Windows ghi là chưa kiểm.

## Risks / Trade-offs

- **[Binding path được suy ra từ chuỗi biểu thức lúc chạy]** Markup dùng `CallerArgumentExpression` để lấy chuỗi `() => ViewModel.X` rồi suy ra path, nên path sai vẫn build và test headless pass, nhưng màn hình không cập nhật. PR #10 mới chỉ được kiểm bằng compile và test ViewModel. → Bước chặn ở task 1.1: chạy thử đăng nhập và lưu Cài đặt bằng giao diện trước khi dùng cú pháp này làm chuẩn. Mỗi control và trang mẫu sau đó cũng được kiểm bằng chạy thật, không chỉ bằng build.
- **[Cú pháp Markup 6.7 hoặc generator hành xử khác kỳ vọng]** → Chuyển `StatusBadge` (control nhỏ nhất) trước làm pilot. Ngay sau đó chốt cú pháp vào mục D1/D5 rồi mới làm các control còn lại. Lệch nào không khắc phục được thì ghi thành ngoại lệ trong D2/D5.
- **[ThemeResource áp từ code không cập nhật khi đổi theme trên Skia]** → Đã xảy ra với **Setter trong Style dựng bằng Markup** (xem ma trận D3). Xử lý: Style mang màu theo theme dựng qua `XamlReader.Load` từ C#, không cần giữ `Colors.xaml`. ThemeResource đặt trực tiếp lên element vẫn đổi đúng. Dự phòng VisualStateManager (D5) vẫn là phương án nếu một control cụ thể gặp lại lỗi.
- **[Style Markup có màu theo theme được thêm sau này]** → Lỗi chỉ lộ khi đổi theme lúc chạy, build và test headless vẫn pass. Quy tắc trong AGENTS.md mục 6: mọi Style có Setter màu theo theme phải đi qua `AppResources.AddThemedStyles`, và phải kiểm bằng gallery Sáng/Tối.
- **[Resource chưa kịp đăng ký khi trang đầu tiên được tạo]** → Đăng ký trong constructor của `App`, trước khi tạo window. Runtime check mở trực tiếp Login, Shell và Gallery.
- **[Gõ sai hoặc thiếu key khi chuyển ~130 key]** → Test snapshot danh sách key (D7) và chạy qua 6 trang XAML.
- **[Màu primary đổi trên giao diện của đồng đội]** → Ghi trong mô tả PR và báo nhóm trước khi merge. Thay đổi này có chủ đích, theo spec.
- **[Conflict với đồng đội đang sửa trang XAML]** → Change không sửa file trang của người khác. Chỉ đụng tới `App.xaml`, `Styles/`, `Controls/` và một nút trên gallery.
- **[Dựng `Geometry` cho icon từ chuỗi path khác nhau giữa Skia và WinAppSDK]** → Gom việc chuyển đổi vào một hàm duy nhất trong phần dựng giá trị (D3). Skia dùng bộ chuyển đổi của Uno (đã có trong package). Nhánh WinAppSDK ghi là chưa kiểm. Nếu trên Skia không dựng được, tạm giữ một file XAML nhỏ chỉ chứa icon và ghi lý do, không chặn các phần khác.
- **[Style kế thừa style WinUI bị đăng ký trước khi `XamlControlsResources` nạp]** → Đăng ký sau `InitializeComponent` (D3). Runtime check nút primary trên các trang XAML.
- **[HighContrast không kiểm runtime được trên Linux]** → Test độ đầy đủ theo theme (D7). Ghi "chưa kiểm" cho phần chạy thật, không tick như đã kiểm.
- **[Target Windows không được kiểm]** → Ghi rõ trong tasks và PR là chưa kiểm. Không coi kết quả trên Skia là xác minh cho Windows.

## Migration Plan

Change gộp trong một PR từ nhánh `feature/csharp-markup-foundation` vào `dev`. Thứ tự:
1. Tokens và đăng ký resource; giữ song song file XAML cho tới khi test snapshot key pass.
2. Gỡ `Styles/*.xaml`.
3. Chuyển controls, bắt đầu bằng pilot StatusBadge.
4. Trang mẫu.
5. AGENTS.md.

Rollback: revert PR, các file XAML trở lại nguyên trạng. Không có thay đổi dữ liệu hay backend.

### Việc về sau: bỏ phần Style tạo bằng XAML parser

`AppResources.AddThemedStyles` (10 Style có màu theo theme, tạo bằng `XamlReader.Load`) tồn tại chủ yếu để các trang XAML cũ dùng Style theo tên (`{StaticResource TechStorePanelStyle}`, `TechStore*TextStyle`...) mà vẫn đổi màu đúng khi đổi theme lúc đang chạy (D3). Hướng gỡ dần:

1. **Khi convert một trang sang C# Markup:** trong trang mới, chỉ dùng Style cho thuộc tính không phải màu (font, cỡ chữ, padding, bo góc), còn màu thì gán trực tiếp lên phần tử bằng `AppColors.Brushes.*`. Cách này đã kiểm là cập nhật đúng khi đổi theme. Không thêm Style mới có màu theo theme vào Markup.
2. **Khi không còn trang XAML nào dùng một Style trong `AddThemedStyles`** (kiểm bằng `git grep` tên key trong các file `.xaml`): tách phần màu ra khỏi Style đó, chuyển phần còn lại sang `new Style<T>()` của Markup, rồi bỏ khỏi `AddThemedStyles`.
3. **Khi `AddThemedStyles` rỗng:** xóa hàm và phần `XamlReader.Load` cho Style. Icon (`AddIcons`) chỉ được bỏ `XamlReader` khi có cách đặt `Foreground` theo theme cho `PathIconSource` từ code.
4. **Xem lại khi nâng Uno:** nếu phiên bản Uno sau cho setter tạo bằng Markup cập nhật khi đổi theme, có thể bỏ sớm hơn. Kiểm bằng cách đổi Sáng/Tối trong gallery (`TECHSTORE_UI_GALLERY=1`).

Mỗi bước đều phải đổi Sáng/Tối lúc đang chạy để kiểm, vì build và test headless không bắt được lỗi này.
