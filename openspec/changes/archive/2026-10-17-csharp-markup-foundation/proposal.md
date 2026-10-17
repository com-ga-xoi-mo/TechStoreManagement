# Proposal

## Why

Ngày 15/10, kế hoạch Sprint 2 (commit `24a13c9`) chuyển toàn bộ View sang C# Markup (`Uno.Extensions.Markup`). Ngày 17/10, PR #10 (`845c522`) đưa `LoginPage`, `ShellPage`, `SettingsPage` sang Markup fluent với binding thật (`x.Binding(...).Mode(...)`, `.Convert(...)`). Tuy vậy repo vẫn thiếu phần nền để cả nhóm làm theo:
- Màu của các trang C# vẫn hardcode qua `StitchTokens` / `Color.FromArgb`, chỉ có màu sáng, không theo theme.
- Styles và 4 base controls vẫn là XAML. Chưa có ví dụ nào về token theo theme, về shared control viết bằng Markup, hay về trang có `PageContentState`.
- Repo đang có hai bảng màu mâu thuẫn nhau.
- AGENTS.md vẫn ghi ví dụ `x.Bind(...)`, khác với cú pháp `x.Binding(...)` mà code thật đang dùng.

Nếu không dựng phần nền này trước, các màn hình nghiệp vụ của Dev 2–6 sẽ mỗi màn một cách lấy màu và dùng control. Change này nhận phần nền thuộc việc được bổ sung cho Dev 6 (đã thống nhất với Dev 6) để mở đường cho cả nhóm, và viết cùng kiểu binding với các trang Dev 1 đã chuyển.

## What Changes

- **Định nghĩa chuẩn C# Markup cho dự án**:
  - View và control dựng bằng API fluent của `Uno.Extensions.Markup`; binding và command khai báo qua API binding của Markup, cùng cú pháp với các trang đã chuyển ở PR #10.
  - Cấm các cách làm sau trong View/Control mới:
    - đồng bộ ViewModel bằng `new X { ... }` kèm `PropertyChanged +=` / `TextChanged +=` / `Checked +=` / `Click +=`;
    - màu hardcode (`Color.FromArgb`, `new SolidColorBrush(...)`).
  - Ngoại lệ phải có lý do ghi trong design.
- **Tokens/Styles → C#**:
  - Thay 5 file `Styles/*.xaml` (Colors, Typography, ControlStyles, Icons, Metrics) bằng `AppColors`, `AppTypography`, `AppMetrics`, `AppIcons`, `AppControlStyles`.
  - Giữ nguyên toàn bộ tên key resource cũ, gồm ThemeDictionaries Light/Dark/HighContrast, để 6 trang XAML chưa convert vẫn chạy.
  - Thêm accessor có kiểu, tham chiếu resource theo theme, cho trang C# Markup.
- **Thống nhất bảng màu theo spec `frontend-design-system`** (bảng màu Stitch):
  - Primary Light đổi từ `#2563EB` sang `#0066FF`, hover `#0052CC`, pressed `#0040A8`.
  - Canvas, surface, stroke và text vốn đã trùng giữa hai bảng.
  - Thay đổi này làm **đổi màu primary nhìn thấy được** trên các trang XAML hiện có.
- **4 base controls → C# Markup**: `StatCard`, `SearchBoxControl`, `EmptyStateControl`, `StatusBadge`.
  - Giữ nguyên tên, kiểu và giá trị mặc định của mọi DependencyProperty.
  - Giữ nguyên hành vi theo spec `frontend-shared-controls`.
  - Xóa các file `.xaml` / `.xaml.cs` tương ứng.
- **Trang mẫu tham chiếu C# Markup** mở từ gallery (vẫn opt-in), dùng dữ liệu minh họa. Trang thể hiện:
  - binding OneWay/TwoWay và command;
  - các trạng thái `PageViewModel`/`PageContentState`;
  - 4 base controls;
  - token theo theme.
- **Cập nhật AGENTS.md**:
  - Thay ví dụ binding sai (`x.Bind(...)`) bằng cú pháp đang chạy trong repo (`x.Binding(...)`), kèm đoạn mẫu, danh sách điều cấm và các ngoại lệ hợp lệ (ví dụ `PasswordBox`).
  - Bỏ tham chiếu `docs/MVVM_REPOSITORY_BASE_PROJECT.md`, file này đã bị xóa ở `2e83a5f`.
  - Sửa dòng Data Flow còn ghi `{x:Bind}`.
  - Ghi trạng thái chuyển đổi của từng trang và control.

## Capabilities

### New Capabilities

Không có.

### Modified Capabilities

- `frontend-design-system`: thêm yêu cầu về design tokens C# theo theme, gồm:
  - một nguồn giá trị duy nhất;
  - giữ tương thích key cho consumer XAML;
  - thích ứng theme khi đang chạy;
  - không hardcode màu trong View/Control C# Markup.
- `frontend-shared-controls`: làm rõ yêu cầu "Consumer owned behavior":
  - controls dựng bằng C# Markup;
  - API công khai không đổi với consumer XAML và C# Markup;
  - màu tone cập nhật khi đổi theme.
- `frontend-component-gallery`: thêm trang mẫu tham chiếu C# Markup trong chế độ gallery, dùng làm chuẩn cho các màn hình khác.

## Impact

- **Client**:
  - `frontend/TechStore.Client/Styles/`: xóa các file `.xaml`, thêm lớp C#; giữ nguyên `StitchTokens.cs`.
  - `Controls/`: 4 control chuyển sang `.cs`; giữ nguyên `StatusTone`.
  - `App.xaml` / `App.xaml.cs`: đăng ký resource.
  - Thêm View/ViewModel trang mẫu trong `Views/Development` / `ViewModels/Development`.
  - Gallery chỉ được sửa ở mức cần để mở trang mẫu.
- **Ngoài phạm vi**:
  - Các trang thuộc người khác: `DashboardPage`, `ComponentGalleryPage` (trừ điểm mở trang mẫu) và `MainPage` (Dev 6); `LoginPage`, `ShellPage`, `SettingsPage` và `StitchTokens.cs` (Dev 1).
  - Các trang nghiệp vụ của Dev 2–5.
  - Backend, shared contracts, ViewModel/Repository hiện có.
- **Phụ thuộc**:
  - Giữ .NET 10, Uno.Sdk 6.7.30 và CPM.
  - Chỉ dùng các package Markup đã có trong restore (`Uno.WinUI.Markup`, `Uno.Extensions.Markup.WinUI`, `Uno.Extensions.Markup.Generators` 6.7.21).
- **Tests**:
  - 207 test của `tests/Frontend.UnitTests` phải tiếp tục pass.
  - Thêm test thuần C# cho logic tách được: ánh xạ tone → key resource, ViewModel trang mẫu.
- **Đồng đội**:
  - Trang XAML của Dev 2–6 không phải sửa code, nhưng màu primary đổi sang `#0066FF`.
  - Dev 1 có việc tồn đọng: chuyển màu của `Login`/`Shell`/`Settings` (đã là Markup từ PR #10) khỏi `StitchTokens` / `Color.FromArgb` sang token theo theme.
- **Nền tảng kiểm chứng**: chỉ kiểm trên Linux/Uno Skia (`net10.0-desktop`); target Windows ghi là chưa kiểm.
