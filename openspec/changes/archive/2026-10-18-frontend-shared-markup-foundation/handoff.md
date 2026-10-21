# Handoff & Contract Inventory — frontend-shared-markup-foundation

> **Trạng thái sau sync `dev`:** các log bên dưới gắn với fingerprint HEAD `845c522` trước fast-forward. Branch hiện tại ở `7116a71`; vì vậy chúng là evidence lịch sử cho probe riêng, không phải verification cuối trên source hiện tại hoặc toàn change. Apply toàn phạm vi trước, rồi chạy verification cuối theo `tasks.md`.


> **Khắc phục Review lần 3 (18/10/2026): đã PASS probe R1–R4 riêng trên fingerprint được ghi bên dưới; đây không phải lệnh dừng apply.**  
> 1. Gán `Style` C# có `Setter(TextBlock.ForegroundProperty, ProbeTextBrush)` vào `styledTextBlock`, kiểm tra qua Light/Dark/System và xác nhận `ReadLocalValue == DependencyProperty.UnsetValue`.  
> 2. Khẳng định điều kiện PASS R3 là phép hội nghiêm ngặt của 4 điều kiện (XAML Light, XAML Dark, C# Light, C# Dark). Mọi mismatch hoặc null đều FAIL.  
> 3. Cả 2 lượt chạy Skia X11 mới (`probe_r3_positive.log` [exit 0], `probe_r3_negative.log` [exit 1]) đã kiểm chứng thành công. Toàn bộ logs cũ được bảo tồn nguyên vẹn làm lịch sử.  
> 4. Build fingerprint và evidence đã được cập nhật đồng bộ. Kết quả này không thay verification cuối sau sync dev.

> **Lịch sử Review lần 2 (18/10/2026):**  
> Đã kiểm chứng 4 yêu cầu R1–R4 (`probe_r1_r4_positive.log`, `probe_r1_r4_negative.log`, `probe_r1_r4_timeout.log`). Giữ lại làm tài liệu đối chiếu lịch sử.

Ngày cập nhật: 18/10/2026  
Mục đích: Bản kiểm kê hợp đồng tương thích (Contracts & Resources Inventory), định hướng đích C#, ghi nhận xung đột palette và bất cập startup spec trước khi thực hiện chuyển đổi C# Markup.

---

## 1. Kiểm kê Tài nguyên XAML (5 Dictionaries) và Đích C# Tương Ứng

### 1.1. Metrics (`Styles/Metrics.xaml` → Đích: `Styles/AppMetrics.cs`)

| Public Resource Key | Kiểu XAML | Giá trị hiện hành | Đích C# (`AppMetrics`) | Ghi chú tương thích |
| :--- | :--- | :--- | :--- | :--- |
| `TechStoreSpaceSmall` | `x:Double` | `8` | `AppMetrics.SpaceSmall` (double: 8) | Giữ x:Key trong ResourceDictionary |
| `TechStoreSpaceMedium` | `x:Double` | `16` | `AppMetrics.SpaceMedium` (double: 16) | Giữ x:Key trong ResourceDictionary |
| `TechStoreSpaceLarge` | `x:Double` | `24` | `AppMetrics.SpaceLarge` (double: 24) | Giữ x:Key trong ResourceDictionary |
| `TechStoreControlMinHeight` | `x:Double` | `36` | `AppMetrics.ControlMinHeight` (double: 36) | Giữ x:Key trong ResourceDictionary |
| `TechStoreRowMinHeight` | `x:Double` | `44` | `AppMetrics.RowMinHeight` (double: 44) | Giữ x:Key trong ResourceDictionary |
| `TechStorePanelRadius` | `CornerRadius` | `12` | `AppMetrics.PanelRadius` (CornerRadius: 12) | Giữ x:Key trong ResourceDictionary |
| `TechStoreCardRadius` | `CornerRadius` | `8` | `AppMetrics.CardRadius` (CornerRadius: 8) | Giữ x:Key trong ResourceDictionary |
| `TechStoreControlRadius` | `CornerRadius` | `8` | `AppMetrics.ControlRadius` (CornerRadius: 8) | Giữ x:Key trong ResourceDictionary |
| `TechStoreBadgeRadius` | `CornerRadius` | `4` | `AppMetrics.BadgeRadius` (CornerRadius: 4) | Giữ x:Key trong ResourceDictionary |
| `TechStorePanelPadding` | `Thickness` | `20` | `AppMetrics.PanelPadding` (Thickness: 20) | Giữ x:Key trong ResourceDictionary |
| `TechStoreBadgePadding` | `Thickness` | `8,2` | `AppMetrics.BadgePadding` (Thickness: 8,2) | Giữ x:Key trong ResourceDictionary |
| `TechStoreStrokeThickness` | `Thickness` | `1` | `AppMetrics.StrokeThickness` (Thickness: 1) | Giữ x:Key trong ResourceDictionary |

### 1.2. Colors (`Styles/Colors.xaml` → Đích: `Styles/AppColors.cs`)

Các tài nguyên được định nghĩa qua `ResourceDictionary.ThemeDictionaries` (nhánh `Light`, `Dark`, `HighContrast`):

| Public Resource Key | Kiểu | Light (Hiện hành) | Light (Spec Canonical) | Dark | HighContrast | Đích C# (`AppColors`) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TechStoreCanvasBrush` | `SolidColorBrush` | `#F8FAFC` | `#F8FAFC` | `#0F172A` | `SystemColorWindowColorBrush` | `AppColors.CanvasBrush` |
| `TechStoreSurfaceBrush` | `SolidColorBrush` | `#FFFFFF` | `#FFFFFF` | `#1E293B` | `SystemColorWindowColorBrush` | `AppColors.SurfaceBrush` |
| `TechStoreSubtleBrush` | `SolidColorBrush` | `#F1F5F9` | `#F1F5F9` | `#334155` | `SystemColorWindowColorBrush` | `AppColors.SubtleBrush` |
| `TechStoreTextBrush` | `SolidColorBrush` | `#0F172A` | `#0F172A` | `#F8FAFC` | `SystemColorWindowTextColorBrush` | `AppColors.TextBrush` |
| `TechStoreSecondaryTextBrush` | `SolidColorBrush` | `#475569` | `#475569` | `#CBD5E1` | `SystemColorWindowTextColorBrush` | `AppColors.SecondaryTextBrush` |
| `TechStoreStrokeBrush` | `SolidColorBrush` | `#E2E8F0` | `#E2E8F0` | `#475569` | `SystemColorWindowTextColorBrush` | `AppColors.StrokeBrush` |
| `TechStoreInputStrokeBrush` | `SolidColorBrush` | `#64748B` | `#64748B` | `#94A3B8` | `SystemColorWindowTextColorBrush` | `AppColors.InputStrokeBrush` |
| **`TechStorePrimaryBrush`** | `SolidColorBrush` | **`#2563EB`** | **`#0066FF`** | `#2563EB` / `#0066FF` | `SystemColorHighlightColorBrush` | `AppColors.PrimaryBrush` |
| **`TechStorePrimaryHoverBrush`** | `SolidColorBrush` | **`#1D4ED8`** | **`#0052CC`** | `#1D4ED8` / `#0052CC` | `SystemColorHighlightColorBrush` | `AppColors.PrimaryHoverBrush` |
| **`TechStorePrimaryPressedBrush`**| `SolidColorBrush` | **`#1E40AF`** | **`#0040A8`** | `#1E40AF` / `#0040A8` | `SystemColorHighlightColorBrush` | `AppColors.PrimaryPressedBrush` |
| `TechStoreOnPrimaryBrush` | `SolidColorBrush` | `#FFFFFF` | `#FFFFFF` | `#FFFFFF` | `SystemColorHighlightTextColorBrush` | `AppColors.OnPrimaryBrush` |
| `TechStoreFocusBrush` | `SolidColorBrush` | `#2563EB` | `#0066FF` | `#93C5FD` | `SystemColorHighlightColorBrush` | `AppColors.FocusBrush` |
| `TechStoreNeutralTextBrush` | `SolidColorBrush` | `#475569` | `#475569` | `#E2E8F0` | `SystemColorWindowTextColorBrush` | `AppColors.NeutralTextBrush` |
| `TechStoreNeutralFillBrush` | `SolidColorBrush` | `#F1F5F9` | `#F1F5F9` | `#334155` | `SystemColorWindowColorBrush` | `AppColors.NeutralFillBrush` |
| `TechStoreNeutralStrokeBrush`| `SolidColorBrush` | `#CBD5E1` | `#CBD5E1` | `#64748B` | `SystemColorWindowTextColorBrush` | `AppColors.NeutralStrokeBrush`|
| `TechStoreInfoTextBrush` | `SolidColorBrush` | `#1D4ED8` | `#1D4ED8` | `#BFDBFE` | `SystemColorWindowTextColorBrush` | `AppColors.InfoTextBrush` |
| `TechStoreInfoFillBrush` | `SolidColorBrush` | `#EFF6FF` | `#EFF6FF` | `#172554` | `SystemColorWindowColorBrush` | `AppColors.InfoFillBrush` |
| `TechStoreInfoStrokeBrush` | `SolidColorBrush` | `#BFDBFE` | `#BFDBFE` | `#3B82F6` | `SystemColorWindowTextColorBrush` | `AppColors.InfoStrokeBrush` |
| `TechStoreSuccessTextBrush` | `SolidColorBrush` | `#047857` | `#065F46` / `#047857` | `#6EE7B7` | `SystemColorWindowTextColorBrush` | `AppColors.SuccessTextBrush` |
| `TechStoreSuccessFillBrush` | `SolidColorBrush` | `#ECFDF5` | `#ECFDF5` | `#064E3B` | `SystemColorWindowColorBrush` | `AppColors.SuccessFillBrush` |
| `TechStoreSuccessStrokeBrush`| `SolidColorBrush` | `#A7F3D0` | `#A7F3D0` | `#10B981` | `SystemColorWindowTextColorBrush` | `AppColors.SuccessStrokeBrush`|
| `TechStoreWarningTextBrush` | `SolidColorBrush` | `#B45309` | `#B45309` | `#FCD34D` | `SystemColorWindowTextColorBrush` | `AppColors.WarningTextBrush` |
| `TechStoreWarningFillBrush` | `SolidColorBrush` | `#FFFBEB` | `#FFFBEB` | `#451A03` | `SystemColorWindowColorBrush` | `AppColors.WarningFillBrush` |
| `TechStoreWarningStrokeBrush`| `SolidColorBrush` | `#FDE68A` | `#FDE68A` | `#D97706` | `SystemColorWindowTextColorBrush` | `AppColors.WarningStrokeBrush`|
| `TechStoreDangerTextBrush` | `SolidColorBrush` | `#B91C1C` | `#991B1B` / `#B91C1C` | `#FCA5A5` | `SystemColorWindowTextColorBrush` | `AppColors.DangerTextBrush` |
| `TechStoreDangerFillBrush` | `SolidColorBrush` | `#FEF2F2` | `#FEF2F2` | `#450A0A` | `SystemColorWindowColorBrush` | `AppColors.DangerFillBrush` |
| `TechStoreDangerStrokeBrush` | `SolidColorBrush` | `#FECACA` | `#FECACA` | `#EF4444` | `SystemColorWindowTextColorBrush` | `AppColors.DangerStrokeBrush` |
| `TechStoreAccentTextBrush` | `SolidColorBrush` | `#6D28D9` | `#6D28D9` | `#DDD6FE` | `SystemColorWindowTextColorBrush` | `AppColors.AccentTextBrush` |
| `TechStoreAccentFillBrush` | `SolidColorBrush` | `#F5F3FF` | `#F5F3FF` | `#2E1065` | `SystemColorWindowColorBrush` | `AppColors.AccentFillBrush` |
| `TechStoreAccentStrokeBrush` | `SolidColorBrush` | `#DDD6FE` | `#DDD6FE` | `#8B5CF6` | `SystemColorWindowTextColorBrush` | `AppColors.AccentStrokeBrush` |

> **Ghi nhận Xung đột Palette (Palette Conflict):**
> - Trong `Colors.xaml` cũ: Primary Light là `#2563EB`, Hover `#1D4ED8`, Pressed `#1E40AF`.
> - Trong `StitchTokens.cs` & Google Stitch Tokens: Primary là `#0066FF`, Hover `#0052CC`, Pressed `#0040A8`.
> - **Quyết định:** Nguồn canonical C# (`AppColors`) sẽ chuyển sang `#0066FF`, `#0052CC`, `#0040A8` đúng theo spec hiện hành.

### 1.3. Typography (`Styles/Typography.xaml` → Đích: `Styles/AppTypography.cs`)

| Public Resource Key | Kiểu | Thuộc tính / Giá trị | Đích C# (`AppTypography`) |
| :--- | :--- | :--- | :--- |
| `TechStoreRegularFont` | `FontFamily` | `ms-appx:///Assets/Fonts/Inter-Regular.ttf#Inter` | `AppTypography.RegularFont` |
| `TechStoreMediumFont` | `FontFamily` | `ms-appx:///Assets/Fonts/Inter-Medium.ttf#Inter` | `AppTypography.MediumFont` |
| `TechStoreSemiBoldFont` | `FontFamily` | `ms-appx:///Assets/Fonts/Inter-SemiBold.ttf#Inter` | `AppTypography.SemiBoldFont` |
| `TechStoreBoldFont` | `FontFamily` | `ms-appx:///Assets/Fonts/Inter-Bold.ttf#Inter` | `AppTypography.BoldFont` |
| `TechStoreMonoFont` | `FontFamily` | `ms-appx:///Assets/Fonts/JetBrainsMono-Regular.ttf#JetBrains Mono` | `AppTypography.MonoFont` |
| `TechStoreBodyTextStyle` | `Style` (Target: TextBlock) | 13px, Regular, LH 20, Foreground Theme TechStoreTextBrush | `AppTypography.BodyTextStyle` |
| `TechStoreHeadingTextStyle` | `Style` (Target: TextBlock) | 20px, SemiBold, LH 28, Foreground Theme TechStoreTextBrush | `AppTypography.HeadingTextStyle` |
| `TechStoreSectionTextStyle` | `Style` (Target: TextBlock) | 14px, SemiBold, LH 20, Foreground Theme TechStoreTextBrush | `AppTypography.SectionTextStyle` |
| `TechStoreCaptionTextStyle` | `Style` (Target: TextBlock) | 12px, Medium, LH 18, Foreground Theme TechStoreTextBrush | `AppTypography.CaptionTextStyle` |
| `TechStoreMetricTextStyle` | `Style` (Target: TextBlock) | 24px, SemiBold, LH 32, Foreground Theme TechStoreTextBrush | `AppTypography.MetricTextStyle` |
| `TechStoreMonoTextStyle` | `Style` (Target: TextBlock) | 12px, Mono, LH 18, Foreground Theme TechStoreTextBrush | `AppTypography.MonoTextStyle` |

### 1.4. ControlStyles (`Styles/ControlStyles.xaml` → Đích: `Styles/ControlStyles.cs`)

| Public Resource Key | TargetType | BasedOn | Các thuộc tính cốt lõi | Đích C# (`ControlStyles`) |
| :--- | :--- | :--- | :--- | :--- |
| `TechStorePanelStyle` | `Border` | Không | SurfaceBrush, StrokeBrush, PanelRadius, PanelPadding | `ControlStyles.PanelStyle` |
| `TechStoreButtonStyle` | `Button` | Không | MediumFont, 13px, ControlRadius, MinHeight 36, Padding 14,8 | `ControlStyles.ButtonStyle` |
| `TechStorePrimaryButtonStyle`| `Button` | `AccentButtonStyle`| MediumFont, 13px, ControlRadius, MinHeight 36, Padding 14,8 | `ControlStyles.PrimaryButtonStyle` |
| `TechStoreDangerButtonStyle` | `Button` | Không | DangerTextBrush, DangerFillBrush, MinHeight 36 | `ControlStyles.DangerButtonStyle` |
| `TechStoreQuietButtonStyle` | `Button` | Không | Background Transparent, MinHeight 36, Padding 14,8 | `ControlStyles.QuietButtonStyle` |
| `TechStoreTextBoxStyle` | `TextBox` | Không | RegularFont, 14px, ControlRadius, MinHeight 36 | `ControlStyles.TextBoxStyle` |
| `TechStoreAutoSuggestBoxStyle`|`AutoSuggestBox`| Không | RegularFont, 14px, ControlRadius, MinHeight 36 | `ControlStyles.AutoSuggestBoxStyle`|
| `TechStoreComboBoxStyle` | `ComboBox` | Không | RegularFont, 14px, ControlRadius, MinHeight 36 | `ControlStyles.ComboBoxStyle` |
| `TechStoreFieldErrorStyle` | `TextBlock` | `TechStoreCaptionTextStyle` | Foreground Theme TechStoreDangerTextBrush | `ControlStyles.FieldErrorStyle` |
| `TechStoreTableRowStyle` | `ListViewItem` | Không | MinHeight 44, Stretch, Padding 12,8 | `ControlStyles.TableRowStyle` |
| `TechStoreTableHeaderStyle` | `TextBlock` | `TechStoreCaptionTextStyle` | Foreground Theme TechStoreSecondaryTextBrush | `ControlStyles.TableHeaderStyle` |

### 1.5. Icons (`Styles/Icons.xaml` → Đích: `Styles/AppIcons.cs`)

| Public Resource Key | Kiểu | Foreground ThemeResource | Data Path | Đích C# (`AppIcons`) |
| :--- | :--- | :--- | :--- | :--- |
| `TechStoreMoneyIcon` | `PathIconSource` | `TechStoreInfoTextBrush` | `M10.5 8C8.84315 8 ...` | `AppIcons.MoneyIcon` |
| `TechStoreBoxIcon` | `PathIconSource` | `TechStoreInfoTextBrush` | `M10.5911 2.51301 ...` | `AppIcons.BoxIcon` |
| `TechStoreWarningIcon` | `PathIconSource` | `TechStoreInfoTextBrush` | `M9.13848 3.70711 ...` | `AppIcons.WarningIcon` |
| `TechStoreBagIcon` | `PathIconSource` | `TechStoreInfoTextBrush` | `M7 7V5C7 3.34315 ...` | `AppIcons.BagIcon` |
| `TechStoreEmptyIcon` | `PathIconSource` | `TechStoreInfoTextBrush` | `M14 3.5V5.5H17.5 ...` | `AppIcons.EmptyIcon` |

---

## 2. Kiểm kê 4 Shared Controls

| Control | Constructor | DependencyProperties (Tên, Kiểu, Default) | Command Arguments / Hành vi | Đích C# Markup |
| :--- | :--- | :--- | :--- | :--- |
| **StatusBadge** | `StatusBadge()` (không tham số) | • `Label`: `string`, `""`<br/>• `Tone`: `StatusTone`, `Neutral` | Không có command. Rỗng ẩn `Visibility=Collapsed`. Tones: `Neutral`, `Info`, `Success`, `Warning`, `Danger`, `Accent`. IsTabStop=False. | C# Markup UserControl |
| **EmptyStateControl**| `EmptyStateControl()` (không tham số) | • `Title`: `string`, `""`<br/>• `Description`: `string`, `""`<br/>• `ActionText`: `string`, `""`<br/>• `ActionCommand`: `ICommand?`, `null`<br/>• `IconSource`: `IconSource?`, `null` | • Bấm `ActionButton` gọi `ActionCommand.Execute(null)`.<br/>• Ẩn `ActionButton` khi `ActionText` rỗng hoặc `ActionCommand == null`.<br/>• Ẩn `Description` khi rỗng.<br/>• Ẩn `Icon` khi null. | C# Markup UserControl |
| **StatCard** | `StatCard()` (không tham số) | • `Title`: `string`, `""`<br/>• `ValueText`: `string`, `""`<br/>• `UnitText`: `string`, `""`<br/>• `DetailText`: `string`, `""`<br/>• `TrendText`: `string`, `""`<br/>• `ErrorText`: `string`, `""`<br/>• `IsLoading`: `bool`, `false`<br/>• `IconSource`: `IconSource?`, `null`<br/>• `Tone`: `StatusTone`, `Neutral` | • Thứ tự ưu tiên hiển thị: `LoadingRing` > `ErrorLabel` > `ContentPanel`.<br/>• `ValueText` rỗng hiển thị `—` (dash). Zero (`0`) là giá trị hợp lệ, không hiển thị dash.<br/>• TrendText bind vào `StatusBadge`. | C# Markup UserControl |
| **SearchBoxControl** | `SearchBoxControl()` (không tham số) | • `Label`: `string`, `"Tìm kiếm"`<br/>• `PlaceholderText`: `string`, `"Tên sản phẩm, SKU hoặc IMEI"`<br/>• `Text`: `string`, `""`<br/>• `SearchCommand`: `ICommand?`, `null`<br/>• `ClearCommand`: `ICommand?`, `null` | • `TextChanged`: gán `Text = sender.Text` ngay lập tức.<br/>• `QuerySubmitted`: gọi `SearchCommand?.Execute(Text)` với chuỗi hiện hành (không submit khi gõ composition tiếng Việt).<br/>• `Clear` Click: gán `Text = ""`, gọi `ClearCommand?.Execute(null)`, focus lại ô nhập. | C# Markup UserControl |

---

## 3. Kiểm kê Pages (Gallery & MainPage)

### 3.1. ComponentGalleryPage (`Views/Development/ComponentGalleryPage`)
- **Constructor Signature:** `public ComponentGalleryPage(ComponentGalleryViewModel viewModel, ShellDiagnosticViewModel diagnosticViewModel, ContentDialogPresenter dialogPresenter)`
- **Automation IDs:**
  - `ThemeSelector` (`ComboBox` chọn giao diện)
  - `GallerySearch` (`AutoSuggestBox` trong SearchBoxControl)
- **Named Visual Elements:**
  - `GalleryRoot`, `GalleryScrollViewer`, `MetricGrid`, `SpecimenGrid`
  - Cards: `RevenueCard`, `OrdersCard`, `ProductsCard`, `WarningsCard`
  - Diagnostics: `DiagnosticFocusButton`
- **Vòng đời (Lifecycle):**
  - `Loaded`: gắn `PropertyChanged` của ViewModel, gọi `_dialogPresenter.Attach(this, () => DiagnosticFocusButton)`, kích hoạt `DiagnosticViewModel.ActivateAsync()`.
  - `Unloaded`: hủy `PropertyChanged`, gọi `_dialogPresenter.Detach()`, `DiagnosticViewModel.Deactivate()`.
  - Responsive layout: điều chỉnh cột thẻ chỉ số theo độ rộng viewport (`args.NewSize.Width >= 1200 ? 4 : >= 900 ? 2 : 1`).

### 3.2. MainPage (`Views/MainPage`)
- **Constructor:** `public MainPage()` (không tham số).
- **Cấu trúc:** Trang khung chứa `NavigationView` với 7 menu items, hiển thị thông tin giới thiệu hệ thống.
- **Quyền sở hữu Route:** `MainPage` không được đăng ký trong danh sách tuyến đường chính thức của `ShellRoutePageFactory` hay `App.OnLaunched`.

---

## 4. Danh sách Consumers Hiện hành

### 4.1. Consumer XAML (Vẫn cần tiếp tục đọc tài nguyên qua ResourceDictionary)
- `Views/Catalog/ProductListPage.xaml`: Dùng `StatusBadge`, `SearchBoxControl`, `EmptyStateControl`, `TechStorePanelStyle`, `TechStorePrimaryButtonStyle`, v.v.
- `Views/Catalog/ProductDetailPage.xaml`: Dùng `StatusBadge`, `EmptyStateControl`, `TechStorePanelStyle`.
- `Views/Dashboard/DashboardPage.xaml`: Dùng 4 `StatCard`, `StatusBadge`, `EmptyStateControl`, và lookup động `Application.Current.Resources[...]`.
- `Views/Inventory/InventoryPage.xaml`: Dùng `EmptyStateControl`.
- `Views/Orders/OrderListPage.xaml`: Dùng `EmptyStateControl`.
- `Views/Customers/CustomerListPage.xaml`: Dùng `EmptyStateControl`.

### 4.2. Consumer C# (Đang dùng `StitchTokens`)
- `Views/Identity/LoginPage.cs`: Dùng `StitchTokens.PrimaryBrush`, `StitchTokens.SurfaceCanvasBrush`, v.v.
- `Views/Settings/SettingsPage.cs`: Dùng `StitchTokens`.
- `Views/Shell/ShellPage.cs`: Dùng `StitchTokens`.

---

## 5. Bất cập Đặc tả Khởi động (Startup Spec Discrepancy)

- **Đặc tả cũ (`frontend-component-gallery`):** Scenario Default launch nói mở Shell phát triển ở Tổng quan khi gallery không bật; Gallery chỉ mở khi bật tường minh. Bản handoff trước mô tả mặc định vào Gallery là sai và được đính chính tại đây.
- **Thực tế mã nguồn (`App.xaml.cs`):** 
  - Khởi động kiểm tra cấu hình biến môi trường `TECHSTORE_UI_GALLERY` qua `GalleryOptions.Enabled`.
  - Nếu `Enabled == true`: hiển thị `ComponentGalleryPage`.
  - Nếu `Enabled == false`: kiểm tra `ISessionStore.Current.Status` để điều hướng sang `ShellPage` (nếu đã đăng nhập) hoặc `LoginPage` (nếu chưa đăng nhập).
- **Ownership:** Phân luồng điều hướng thuộc quyền sở hữu của Dev 1. Change này không thay đổi thứ tự và logic routing trong `App.OnLaunched`.

---

## 6. Lượt probe harness (lịch sử) — Khắc phục Task 1.2 & 1.4 (Ngày 18/10/2026)

### 6.1. Báo cáo Kiểm chứng Toàn diện R1–R4 sau Khắc phục Review lần 2
1. **R1 — Live Specimens Dynamic Adaptation trên cùng cây UI:**
   - Tất cả specimens (`styledTextBlock`, `pathIcon` với `PathGeometry` có figure/segments không rỗng, `probeBadge` 6 tones, và companion `TextBox`) được mount đồng thời trên cùng `ancestorContainer` trong cửa sổ Skia X11 thực tế.
   - Cơ chế adapter `ThemeResourceAdapter` lắng nghe `ActualThemeChanged` tác động trực tiếp vào các specimens.
   - Khi chuyển đổi `Light → Dark → System`:
     - Styled TextBlock cập nhật màu Text chính xác (`#0F172A` ở Light, `#F8FAFC` ở Dark).
     - PathIcon cập nhật màu Icon chính xác (`#1D4ED8` ở Light, `#BFDBFE` ở Dark).
     - ProbeBadge giữ nguyên tone state (`Warning` giữ nguyên khi chuyển sang Dark và chuyển sang màu Dark Warning `#451A03`, `#D97706`, `#FCD34D`), đồng thời cả 6 tones đều áp dụng đúng bộ ba Background, BorderBrush, Foreground.
     - Control identity được bảo toàn tuyệt đối (`ReferenceEquals=True`), không dựng lại trang.
     - Input state của người dùng (`companionInput.Text = "UserTypingPreserved123"`) được giữ nguyên vẹn 100%.
2. **R2 — System Theme Assertion (`RequestedTheme = Default`):**
   - Đọc `ancestor.ActualTheme` thực tế (Light trên môi trường test hiện tại).
   - Tra cứu và so sánh chính xác cả brush và mã màu hex với bảng kỳ vọng độc lập `ExpectedPalette["Light"]` (Text `#0F172A`, Icon `#1D4ED8`, SuccessBg `#ECFDF5`).
   - Khẳng định không dùng `!= null` làm tiêu chí pass; kiểm chứng negative run chứng minh nếu màu bị stale hoặc lệch palette thì suite báo FAIL ngay lập tức.
3. **R3 — Mixed Consumers (XAML `StatusBadge` & C# Consumer):**
   - Mount đồng thời `StatusBadge` (XAML UserControl hiện có) và C# Consumer trên cùng giao diện.
   - Cung cấp semantic key C# qua `Application.Current.Resources.MergedDictionaries` với giá trị probe phân biệt (`TechStoreInfoFillBrush` = `#E0EAFF` ở Light, `#0F1D4A` ở Dark) khác hoàn toàn giá trị XAML cũ (`#EFF6FF` / `#172554`).
   - Cả hai consumers đều nhận đúng giá trị probe từ C# dictionary và cùng chuyển đổi màu sắc tại chỗ.
   - Khối `finally` bảo đảm gỡ bỏ `probeDictionary` khỏi `MergedDictionaries`, khôi phục 100% tài nguyên gốc.
4. **R4 — Wait / Failure Path & Watchdog:**
   - `WaitForLayoutAsync` kiểm tra kết quả `DispatcherQueue.TryEnqueue(Low, ...)`; sử dụng deadline 4 giây kết hợp `CancellationToken`.
   - Bổ sung watchdog 10 giây chống treo process nếu sự kiện `Loaded` không tới.
   - Lượt chạy cưỡng bức `--timeout-test` chứng minh khi layout quá hạn, harness throw `TimeoutException`, thoát với mã `2` (khác 0), không bị treo và không PASS sai.
   - Toàn bộ adapters, event handlers và resources được dọn dẹp sạch sẽ trong `finally`.

### 6.2. Các Lượt Chạy Kiểm Chứng Thực Tế & Log Files
- **Forced Timeout Run (R4 Failure Path):**
  - Lệnh: `dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -- --probe-theme --timeout-test`
  - Mã thoát: `2` (bắt được `TimeoutException` thành công)
  - Log thô: [`probe_r1_r4_timeout.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_r1_r4_timeout.log)
- **Negative Control Run (Wrong-Expected Fail-Path):**
  - Lệnh: `dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -- --probe-theme --negative`
  - Mã thoát: `1` (phát hiện toàn bộ 5/5 mismatch assertions có chủ đích)
  - Log thô: [`probe_r1_r4_negative.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_r1_r4_negative.log)
- **Positive Run (R1–R4 Complete):**
  - Lệnh: `dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -- --probe-theme`
  - Mã thoát: `0` (100% assertions đạt chuẩn)
  - Log thô: [`probe_r1_r4_positive.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_r1_r4_positive.log)
- **Historical Logs:** Các tệp log của lượt apply trước ([`probe_run_positive.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_run_positive.log), [`probe_run_negative.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_run_negative.log)) được bảo tồn nguyên vẹn làm tài liệu đối chiếu lịch sử.

### 6.3. Phạm vi Patch & Độc lập với Mã Nguồn Production (Dev 1 Ownership)
- **`App.xaml.cs`:** Giữ nguyên **0 diff** so với HEAD, bảo đảm tuyệt đối quyền sở hữu routing/session của Dev 1.
- **`Platforms/Desktop/Program.cs`:** Chỉ bổ sung 1 nhánh kiểm tra cờ CLI `--probe-theme` hoặc biến môi trường `TECHSTORE_PROBE_THEME=1` để rẽ nhánh sang harness; khi khởi động bình thường, ứng dụng chạy `App()` nguyên bản.
- **`Frontend.UnitTests`:** Tập tin harness được đặt trong `Testing/`, hoàn toàn nằm ngoài phạm vi compile của `TechStore.Client.UnitTests.csproj`, bảo đảm 207/207 unit tests pass 100%.

### 6.4. Đánh giá Điều kiện Tiên quyết & Đạt Lượt probe harness (lịch sử)
- [x] Đã hoàn thành bảng ánh xạ chi tiết 100% keys tài nguyên, types và đích C# (Mục 1).
- [x] Đã hoàn thành bảng thông số 4 shared controls: DPs, defaults, commands (Mục 2).
- [x] Đạt toàn bộ acceptance harness R1–R4 theo design 6.1 (Mục 6.1).
- [x] Đã ghi nhận baseline desktop build sạch (0 warnings, 0 errors, exit 0) và unit tests (207 passed, exit 0).
- [x] Đã xác định rõ trạng thái Windows native (Unverified do thiếu máy Windows host).
- [x] Đã gỡ hook tạm khỏi App production (0 diff trên `App.xaml.cs`), giữ nguyên 100% startup routing của Dev 1.
- [x] Đã lưu trữ build fingerprint mới đầy đủ:
  - Git HEAD: `845c522eadc5b477b0d31ea5afa0865fb2de02eb`
  - Tracked dirty diff hash: `d79708aabd5e892ea6247aa31431bbc1778a57fe61f0c5b5540c30f6b5b30af6`
  - Untracked harness hash: `5afa1d3df56a5f2f552e25e854f982f66304f7cc72934262c36f0fb64a329fc6`
  - App production entrypoint: 0 diff.
  - Review 3 Positive log hash: `9ef0bc4b77a301dbbf5e4f193c25698ee5fa8eba10d74951fb0a8fcc7cb3308f`
  - Review 3 Negative log hash: `8a17dc695142a5eb68ff7e73dca06e35192ea66c29106cfc9d863d63f84170dc`
=> **Tasks 1.2 và 1.4 HOÀN THÀNH. ĐẠT CHECKPOINT NHÓM 1.**  
**Đây là trạng thái của lượt làm việc trước; verification cuối hiện tại được thực hiện sau khi hoàn thành toàn bộ change.**

---

## 7. Khắc phục Review lần 3 — Nghiệm thu Hoàn tất (18/10/2026)

1. **R1 Style Setter:**
   - Specimen `styledTextBlock` đã được gán trực tiếp C# `Style` với `Setter(TextBlock.ForegroundProperty, ProbeTextBrush)`.
   - Kiểm tra qua `Light → Dark → System`:
     - Style và Setter không null.
     - `styledTextBlock.ReadLocalValue(TextBlock.ForegroundProperty) == DependencyProperty.UnsetValue`, khẳng định Foreground hoàn toàn do Style Setter điều khiển.
     - Màu thực tế trên TextBlock nhận đúng giá trị theme: Light=`#FF0F172A`, Dark=`#FFF8FAFC`.

2. **R3 XAML Consumer Conjunction:**
   - Đã cập nhật công thức PASS trong `TestMixedConsumersAsync` thành phép hội 4 chiều chặt chẽ:
     `pass = (csharpLightBg == expLightFill && csharpDarkBg == expDarkFill) && (xamlBorder != null && xamlLightBg == expLightFill && xamlDarkBg == expDarkFill)`.
   - Bất kỳ sai lệch màu, null, hoặc thiếu resource trên XAML `StatusBadge` đều khiến test FAIL.
   - Run negative control (`probe_r3_negative.log`) đã chứng minh: khi expected sai, cả XAML lẫn C# consumer đều không thỏa mãn, aggregate báo `[FAIL]` và exit code `1`.
   - Run positive (`probe_r3_positive.log`) chứng minh: cả XAML `StatusBadge` và C# Consumer đều nhận đúng `#FFE0EAFF` (Light) và `#0F1D4A` (Dark), aggregate báo `[PASS]` và exit code `0`.

3. **Lưu trữ Log Files:**
   - Log mới: [`probe_r3_positive.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_r3_positive.log) và [`probe_r3_negative.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_r3_negative.log).
   - Log cũ: được giữ nguyên toàn bộ trong thư mục change để làm hồ sơ lịch sử đối chiếu.

**Kết luận Lượt probe harness (lịch sử):** Đầy đủ bằng chứng thực nghiệm khớp source và log. Tasks 1.2 và 1.4 hoàn thành. Không dùng ghi chú lịch sử này làm điểm dừng apply hiện tại.

## 8. Source audit after fast-forward to dev (7116a71)

Đây là mapping source đã apply và kiểm chứng trên nhánh hiện tại. Build/test/runtime evidence mới được ghi ở `evidence.md` mục 6.

| Deliverable | Source hiện tại | Đánh giá |
|---|---|---|
| C# resources / năm dictionaries | `Styles/AppColors.cs`, `AppTypography.cs`, `AppMetrics.cs`, `AppIcons.cs`, `AppControlStyles.cs`, `AppResources.cs`; năm XAML dictionaries đã bị xóa trên dev | Đã chuyển. Cần kiểm chứng lại public keys/types/theme/bootstrap trên source sau merge. |
| Bootstrap | `App.xaml` giữ `XamlControlsResources`; `App()` gọi `AppResources.Register(this)` sau `InitializeComponent()` | Đã đăng ký trước UI. Không thay auth/session/navigation. |
| StitchTokens | `Styles/StitchTokens.cs` | Compatibility facade giữ nguyên toàn bộ tên/type public; color lấy từ `AppColors` canonical. Brush properties vẫn là snapshots theo hợp đồng cũ; UI mới dùng `AppColors.Brushes` cho live theme. |
| Shared controls | `Controls/{StatusBadge,EmptyStateControl,StatCard,SearchBoxControl}.cs`; XAML pairs đã xóa | Đã có C# Markup và Presentation rules. Runtime contract/focus/theme/command verification còn cần.
| ComponentGalleryPage | `Views/Development/ComponentGalleryPage.cs` và VM | Đã chuyển sang C# Markup; giữ constructor/lifecycle, theme selector và navigation automation IDs, samples, table, SettingsCard, command replacement/CanExecute và optional specimens. |
| MainPage | `Views/MainPage.cs` | Đã chuyển sang C# Markup; giữ menu/frame/content và constructor. Vẫn không đăng ký làm route/Shell. |
| Probe | `Testing/ThemeBindingProbeHarness.cs` và CLI route trong Desktop Program đang là local untracked/modified changes | Giữ lại; verify lại trên tree đầy đủ và cập nhật fingerprint sau implementation.

Recheck final integration notes: `App.xaml.cs`/bootstrap thuộc Dev 1 và không đổi; UI consumers có thể tiếp tục dùng legacy `StitchTokens` public members trong lúc chuyển sang `AppColors.Brushes`; gallery route chỉ bật qua `TECHSTORE_UI_GALLERY=1`. Windows native verification chưa chạy trên Linux.

### 8.1 Final verification outcome (2026-10-18)

- `Styles/AppResources.Register`/`App()` giữ nguyên bootstrap đã merge từ dev; không thêm startup hook. Desktop target build pass, 0 warnings/errors.
- Four controls already migrated on dev were reused without rewriting: `StatusBadge`, `EmptyStateControl`, `StatCard`, `SearchBoxControl`. Gallery consumer tests/probe now use the migrated `StatusBadge.Content` tree instead of a removed XAML name lookup.
- `MainPage.xaml(.cs)` and `ComponentGalleryPage.xaml(.cs)` removed after their C# Markup replacements were added.
- Final desktop runtime smoke: standard startup and gallery startup both remain running after 4 seconds on X11 (`DISPLAY=:0`); processes were stopped by the smoke script (termination 143 expected). Gallery launch no longer throws during color swatches.
- `ThemeBindingProbeHarness` final direct executable results: positive exit 0 (R1 Style Setter, R2 system theme, R3 strict XAML+C# consumer conjunction, R4 dispatcher/layout all PASS), negative control exit 1 (intentional failures caught), forced timeout exit 2 (TimeoutException caught). Viewport 1024x640 DIP, ActualTheme Light, Linux x86_64 / Uno Skia X11.
- Frontend.UnitTests: 256 passed, 0 failed, 0 skipped. Windows native build/runtime: unverified on Linux.
- Owner UI acceptance remains pending and must be recorded by the owner after reviewing the final build/hash/target. Do not infer acceptance from these checks.
