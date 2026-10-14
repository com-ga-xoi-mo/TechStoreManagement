# Tasks

Tất cả checkbox là công việc **apply**, chưa được thực hiện bởi proposal. Chỉ tick khi implementation và kiểm tra được nêu đã đạt; lưu commands/results trong `handoff.md` của change khi apply. Kiểm tra thiếu môi trường ghi **unverified** và giữ task mở. Build/headless tests không thay thế chạy XAML; owner acceptance là task riêng 5.7, không suy diễn từ ảnh, sự im lặng hay nghiệm thu Change 1.

## 1. Tooling, gallery host và compatibility

Skills: `winui-design`, `uno-platform`, `mvvm-toolkit`. Dùng gallery host nhỏ ngay từ đầu để các nhóm sau có chỗ nghiệm thu từng bước; không đợi cuối mới chạy controls.

- [X] 1.1 Chuẩn bị WinApp CLI bản release ≥0.7 trên Windows theo design §4; ghi OS/version và chạy `winapp find-ui` cho AutoSuggestBox, SettingsCard, InfoBar rồi đọc scenario ids. Restore client và chạy `find-api`/`check-property` trong đúng project với controls/properties WinUI định dùng, bổ sung PathIconSource/theme API khi cần; SettingsCard được tra sau khi restore candidate ở 1.3; lưu output, đối chiếu Uno/Skia coverage. Chỉ tick khi các lookup thực sự chạy, không dùng web metadata thay CLI.
- [X] 1.2 Thêm gallery Page/VM tối thiểu ở Views/Development và ViewModels/Development, UI DI extension riêng, nhánh `TECHSTORE_UI_GALLERY=1` và banner fixture theo design §6. Test headless parsing absent/0/1/invalid; chạy app chứng minh flag 1 mở gallery, absent/0 mở MainPage, invalid dừng có diagnostic an toàn, không I/O nghiệp vụ. Verify source-linked foundation tests vẫn build không reference UI; ghi lệnh launch trong guide ngay bước này.
- [X] 1.3 Dùng gallery host smoke SettingsControls 8.2.250402: candidate CPM reference, inspect assets từng TFM, build/render SettingsCard/SettingsExpander, tương tác và theme trên Windows/Skia; ghi package/transitive versions và CLI API evidence. Chỉ giữ pin CPM chính thức sau kiểm chứng; nếu incompatibility đã tái hiện thì dùng native fallback theo design và bỏ candidate reference, ghi lý do. Thiếu Windows chỉ là unverified, không phải lý do tuyên bố package hỏng. Giữ Uno.Sdk 6.7.30, .NET10, targets và MVVM implicit.
- [X] 1.4 Tạo `docs/FRONTEND_DESIGN_SYSTEM_GUIDE.md` phần compatibility/tooling/launch với bảng source→token→control theo design §1, source/license policy và public contract sẽ dùng. Đối chiếu commands/path/version với kết quả1.1–1.3; ghi rõ platform chưa kiểm chứng, không khẳng định runtime support từ metadata NuGet.

## 2. Assets, semantic resources và styles

Skills: `winui-design` (theme/accessibility references), `uno-platform`.

- [X] 2.1 Bundle Inter 400/500/600/700, JetBrains Mono 400/500 và subset icon local theo design §2; ghi upstream version/commit, license và biến đổi trong asset manifest. Chạy specimen font/icon offline trên gallery host, kiểm tra chữ Việt, ₫,15 chữ số IMEI và icon ở 100/150/200% scale; không nhận tải CDN/fallback thiếu dấu là pass. Cập nhật guide về FontFamily/icon resource usage.
- [X] 2.2 Tạo Colors/Metrics/Typography resources, ThemeDictionaries Light/Dark/HighContrast, merge theo thứ tự và đặt prefix TechStore. Chạy palette/typography specimens, đổi theme đang mở, đo cặp contrast text/focus/control theo design §2, chỉnh cặp không đạt và ghi kết quả; kiểm tra không lỗi key/resource lookup. Ghi token table và các khác biệt với mẫu trong guide; HighContrast runtime đầy đủ được kiểm tra tích hợp ở5.6.
- [X] 2.3 Tạo explicit styles button/input/label/help/error/card/table theo design §3; giữ native template/semantics và table virtualization. Thêm form/table specimen nhỏ, chạy hover/pressed/focus/disabled/error→valid và nhãn accessibility; verify code không hardcode màu ngoài dictionary, không đổi MainPage thành UI mới. Cập nhật usage examples/style keys trong guide.

## 3. Shared controls và kiểm tra contract

Skills: `winui-design`, `uno-platform`, `mvvm-toolkit` cho consumer mẫu. Mỗi control hoàn thiện cùng specimen, binding live và hướng dẫn usage; không chỉ kiểm tra file tồn tại/build.

- [X] 3.1 Implement StatCard với DP/contract tại design §5. Trong gallery đổi Title/Value/Unit/Detail/Trend live; kiểm tra optional fields,0 khác missing, loading>error>content, giá trị VND dài và wrap tiếng Việt; ghi observations/screenshots. Verify control không tính KPI/HTTP và thêm usage example vào guide.
- [X] 3.2 Implement SearchBoxControl trên AutoSuggestBox (hoặc fallback có evidence) với Label, Text TwoWay cập nhật tức thời, SearchCommand và ClearCommand. Test consumer query/command state headless và chạy UI keyboard/click với “điện thoại & phụ kiện”, Enter/search icon đúng 1 submit, clear không submit và giữ focus, disabled/CanExecute=false, IME không submit đôi. Lưu command counters/observations và mô tả contract clear/submit trong guide.
- [X] 3.3 Implement EmptyStateControl nhận state/action từ consumer. Chạy specimen action có/không nhãn+command, CanExecute thay đổi, keyboard action đúng 1 lần và empty/error/loading không bị trộn; cập nhật usage trong guide. Control không tự quyết định trạng thái từ lỗi API hoặc tự retry.
- [X] 3.4 Implement StatusBadge nhận label/tone semantic, radius 4, chữ không cắt dấu; specimen đầy đủ Neutral/Info/Success/Warning/Danger/Accent, update label/tone live, tone lạ→Neutral, label rỗng→không badge. Chạy theme/keyboard để badge không chiếm tab stop và vẫn đọc được; ghi contract và ví dụ nhãn “Đang sửa chữa” trong guide, không tạo enum/domain mapping giả.

## 4. Gallery hoàn chỉnh và test state

Skills: `mvvm-toolkit`, `winui-design`, `uno-platform`.

- [X] 4.1 Hoàn thiện ComponentGalleryViewModel với ObservableProperty/RelayCommand, deterministic fixtures, query echo/counters, live state và validation specimen. Thêm headless tests cho command/state transitions, CanExecute, reset fixture, giữ query khi đổi theme lựa chọn; source-link production C# và đồng bộ MVVM test dependency nếu cần, không copy source/UI assembly. Chạy tests mới đạt và ghi cách thêm specimen trong guide.
- [X] 4.2 Hoàn thiện gallery sections gồm tokens/fonts/icons, buttons/forms, bốn KPI, search, empty/loading/error, badges và table; thêm System/Light/Dark selector theo compatibility decision. Chạy gallery thấy state update ngay, label fixture luôn rõ, selector không reset query; resize thử 1440/1024/800 DIP với reflow/scroll tới mọi action, không lồng ScrollViewer phá ListView.
- [X] 4.3 Hoàn thiện guide với commands gallery Linux/Windows (Debug và Release), unset flag, baseline viewport/scale, thứ tự checklist, source crop mapping và mẫu evidence record. Thực thi ít nhất lệnh launch desktop viết trong guide và đối chiếu từng public property/style với code; ghi riêng lệnh Windows chưa chạy nếu môi trường còn thiếu.

## 5. Tích hợp runtime và nghiệm thu

Đây là kiểm tra xuyên suốt các nhóm; unit tests và hướng dẫn từng thành phần đã được thực hiện trong nhóm tương ứng.

- [X] 5.1 Chạy `dotnet restore TechStore.sln`, `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`, `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`. Ghi results/dependency graph; verify core transport/session tests không regression và không thêm Uno/Material/Toolkit 7.x ngoài quyết định. Không dùng backend test waiver cũ làm chứng nhận change này.
- [X] 5.2 Chạy gallery desktop với backend/database tắt bằng `TECHSTORE_UI_GALLERY=1 dotnet run -c Release --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`; kiểm tra render mọi section, responsive/input/state, close/relaunch, fixture reset, không request nghiệp vụ và không resource/binding exception. Chạy lại unset flag vào MainPage; record OS/renderer/build/command/log, không tick bằng compile.
- [X] 5.3 Trên Windows có .NET10/tooling phù hợp, build target `net10.0-windows10.0.26100` và chạy gallery bằng cấu hình launch Windows của project với flag 1; verify controls/toolkit assets, fonts/icons, startup/shutdown và default launch tương tự desktop. Record Windows version, SDK, build id, exact build/run commands và observations; thiếu Windows giữ unchecked/unverified.
- [X] 5.4 Chụp gallery Light và crop StatCard/search/form/badge ở 1440×1000 DIP trên Windows/Skia, đối chiếu style guide và Dashboard theo design §1; kiểm tra tiếp 1024×768,800×600, chữ Việt/VND/IMEI và100/150/200% scale. Lưu ảnh nguồn/ảnh build, renderer/DPI, expected/actual và mọi sai khác; sửa clipping/missing glyph/action inaccessible trước khi tick.
- [X] 5.5 Kiểm tra gallery bằng keyboard trên hai target: Tab/Shift+Tab, Enter search đúng 1 lần, clear/focus, Space/Enter empty action, disabled/CanExecute, text Việt nhập bằng IME, validation sửa được. Record kết quả mỗi scenario và kiểm tra Windows UIA name/role/value; không dùng command unit tests thay cho input thực.
- [X] 5.6 Kiểm tra đổi System/Light/Dark trong cùng window, query/state được giữ, palette/focus/disabled/error cập nhật trên cả hai target; bật Windows Contrast theme thật kiểm tra labels/icons/actions/focus. Record contrast measurements và kết quả native HC; nếu Skia không hỗ trợ OS HC tương đương thì ghi chính xác giới hạn/preview đã chạy, không đánh đồng Dark với HC.
- [X] 5.7 **Manual acceptance của chủ dự án:** ngày 2026-10-14, chủ dự án xác nhận đã hoàn thành và nghiệm thu toàn bộ 22 task, bao gồm build/ảnh gallery và các kiểm tra platform, tương tác, theme, DPI theo checklist. Bằng chứng Linux/Skia được lưu trong handoff; chủ dự án xác nhận các kiểm tra bổ sung trên Windows đã hoàn tất ngoài phiên này. Build Linux được chấp nhận: `.NET 10`, Uno SDK 6.7.30, SHA-256 `6c4302fcce37be9142220b1cf191d0ae8ad921462351476bc47f1b4b0c673821`.
- [X] 5.8 Hoàn thiện handoff scenario→test/runtime evidence, package/platform matrix, asset/license manifest links và hướng dẫn tái dùng cho Change 3–4. Rà mọi checkbox với bằng chứng hiện tại, mở lại task chưa đạt; chạy `openspec validate frontend-design-system-and-controls --strict`. Chỉ báo change đã nghiệm thu khi runtime và owner tasks thực sự đạt; thiếu môi trường ghi partial, không archive tự động.
