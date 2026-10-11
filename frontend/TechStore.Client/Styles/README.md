# TechStore Design System & Styles (D31 Foundation)

Tài liệu này ghi nhận quyết định thiết kế và quy ước đặt tên Resource Keys cho hệ thống TechStore Client (Uno Platform WinUI 3), dựa trên bảng mẫu **D31 Style Guide** và tham chiếu bảng điều khiển **D01**.

---

## 1. Palette màu sắc & Brushes

Tất cả màu sắc được đặt tên tiền tố `TechStore` và có cả phiên bản `Color` lẫn `SolidColorBrush`:

| Key Name | Hex Value | Mục đích sử dụng |
| :--- | :--- | :--- |
| `TechStorePrimaryBrush` | `#2563EB` | Màu thương hiệu chính, nút hành động chính (Primary Button), active state |
| `TechStorePrimaryHoverBrush` | `#1D4ED8` | Trạng thái hover nút chính |
| `TechStorePrimaryTintBrush` | `#EFF6FF` | Nền nhạt cho vùng nhấn mạnh, selection highlight |
| `TechStoreCanvasBackgroundBrush` | `#F8FAFC` | Nền toàn trang (Page Canvas) |
| `TechStoreSurfaceBackgroundBrush` | `#FFFFFF` | Nền thẻ (Card), Modal Dialog, ô nhập liệu |
| `TechStoreTextPrimaryBrush` | `#0F172A` | Tiêu đề, chữ nội dung chính |
| `TechStoreTextSecondaryBrush` | `#64748B` | Mô tả phụ, subhead, icon nhạt |
| `TechStoreTextMutedBrush` | `#94A3B8` | Placeholder, chú thích mờ |
| `TechStoreBorderBrush` | `#E2E8F0` | Đường viền thẻ, viền input, ngăn cách dòng |
| `TechStoreBorderFocusBrush` | `#2563EB` | Viền khi nhận focus bàn phím |
| `TechStoreBorderErrorBrush` | `#EF4444` | Viền khi input có lỗi validation |
| `TechStoreSuccessBrush` | `#10B981` | Trạng thái thành công, hoàn thành, tồn kho tốt |
| `TechStoreSuccessTextBrush` | `#065F46` | Chữ badge thành công: 7.29:1 trên nền `#ECFDF5` |
| `TechStoreSuccessBgBrush` | `#ECFDF5` | Nền badge thành công |
| `TechStoreWarningBrush` | `#F59E0B` | Cảnh báo, sắp hết hàng, chờ xử lý |
| `TechStoreWarningTextBrush` | `#92400E` | Chữ badge cảnh báo: 6.84:1 trên nền `#FFFBEB` |
| `TechStoreWarningBgBrush` | `#FFFBEB` | Nền badge cảnh báo |
| `TechStoreDangerBrush` | `#EF4444` | Báo lỗi, hủy đơn, nút xóa |
| `TechStoreDangerTextBrush` | `#991B1B` | Chữ badge lỗi: 7.60:1 trên nền `#FEF2F2` |
| `TechStoreDangerBgBrush` | `#FEF2F2` | Nền badge lỗi / hủy |
| `TechStoreNeutralTextBrush` | `#334155` | Chữ badge trung tính: 9.45:1 trên nền `#F1F5F9` |
| `TechStoreNeutralBgBrush` | `#F1F5F9` | Nền badge trung tính / dự thảo |

---

## 2. Corner Radius & Spacing Tokens

Tuân thủ nghiêm ngặt quy định thiết kế D31:

- **Controls (Button, TextBox, Dialog):** `CornerRadius = 8` (`TechStoreControlCornerRadius`).
- **Cards (Thẻ chứa nội dung):** `CornerRadius = 8` (`TechStoreCardCornerRadius`).
- **Badges (Nhãn trạng thái):** `CornerRadius = 4` (`TechStoreBadgeCornerRadius`).  
  *Quyết định thiết kế:* HTML mẫu D31 có một class CSS dùng pill (viên thuốc bo tròn 9999px), nhưng bảng đồ họa thiết kế và thông số chi tiết D31 chỉ định rõ badge hình chữ nhật bo góc 4px. Hệ thống chuẩn hóa theo bảng thiết kế chữ nhật bo góc 4px (`Padding="8,3"`).
- **Khoảng cách Spacing:**
  - `TechStoreSpacingXs`: 4px
  - `TechStoreSpacingSm`: 8px
  - `TechStoreSpacingMd`: 12px
  - `TechStoreSpacingLg`: 16px
  - `TechStoreSpacingXl`: 24px

---

## 3. Hệ thống Typography

Theo thang phân cấp cỡ chữ của D31:

- `TechStoreHeadingTextBlockStyle`: `FontSize = 20`, `FontWeight = SemiBold` (Tiêu đề trang, tiêu đề card lớn).
- `TechStoreSubheadingTextBlockStyle`: `FontSize = 16`, `FontWeight = SemiBold` (Tiêu đề nhóm chức năng).
- `TechStoreBodyTextBlockStyle`: `FontSize = 13`, `FontWeight = Normal` (Nội dung dữ liệu bảng, văn bản thông thường).
- `TechStoreSecondaryTextBlockStyle`: `FontSize = 13`, `FontWeight = Normal`, `Foreground = TextSecondary`.
- `TechStoreCaptionTextBlockStyle`: `FontSize = 12`, `FontWeight = Normal` (Nhãn phụ, chú thích).
- `TechStoreMicroCaptionTextBlockStyle`: `FontSize = 11`, `FontWeight = Normal` (Nhãn nhỏ nhất, timestamp).
- `TechStoreErrorCaptionTextBlockStyle`: `FontSize = 12`, `Foreground = Danger` (Thông báo lỗi validation dưới input).

**Font chữ & Fallback Tiếng Việt:**  
Dự án sử dụng font hệ thống mặc định của nền tảng (Segoe UI trên Windows, San Francisco trên macOS, system sans-serif trên Linux) với hỗ trợ đầy đủ bộ ký tự có dấu tiếng Việt UTF-8. Không bundle font Inter ngoài luồng để đảm bảo tính tương thích và bản quyền.

---

## 4. Biểu tượng (Icons) & Ảnh dự phòng (Image Fallback)

- **Icons:** Sử dụng các glyph native của WinUI qua `SymbolIcon` hoặc vector SVG trong ứng dụng thay vì kéo thư viện bên thứ ba.
- **Image Fallback:** Khi sản phẩm hoặc avatar chưa có ảnh URL khả dụng, hiển thị khối `Border` màu nền `#F1F5F9` kèm `SymbolIcon` trung tính thay thế.
