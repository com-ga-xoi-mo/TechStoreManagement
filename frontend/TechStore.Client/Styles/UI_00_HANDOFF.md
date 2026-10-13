# UI-00 — Biên bản bàn giao theme foundation

**Change:** `ui-00-theme-foundation`  
**Ngày nghiệm thu:** 09/10/2026  
**Kết quả:** Chủ dự án xác nhận preview UI-00 trên Uno Skia Desktop (Linux) hiển thị, cuộn và tương tác ổn, bao gồm các trạng thái nút, ô nhập và chữ lỗi. Ảnh chụp và số đo cửa sổ không bắt buộc để nghiệm thu.

## Phạm vi và file thay đổi

- `Styles/TechStoreTheme.xaml`: token D31, style nút, ô nhập, card, badge và trạng thái phản hồi.
- `Styles/README.md`: quy ước resource, badge theo D31, font và icon/ảnh dự phòng.
- `Styles/UI_00_HANDOFF.md`: biên bản này.
- `Converters/CurrencyConverter.cs`: định dạng VND và giá trị không có dữ liệu.
- `ViewModels/Theme/ThemePreviewViewModel.cs`: dữ liệu demo và lệnh Thử Lại cục bộ bằng CommunityToolkit.Mvvm.
- `Views/Theme/ThemePreviewView.xaml` và `.xaml.cs`: bảng preview, binding và các trạng thái mẫu.
- `App.xaml`, `App.xaml.cs`, `MainPage.xaml`: nạp resource, đăng ký/resolve ViewModel qua DI và đặt preview trong shell hiện tại.

Preview chỉ dùng dữ liệu minh họa cục bộ: **UI preview only / API not applicable**. Không truy cập backend, repository hay cơ sở dữ liệu. Font là sans-serif hệ thống hỗ trợ tiếng Việt, chưa đóng gói Inter; icon/ảnh thiếu dùng `SymbolIcon` và placeholder native.

## Xác nhận trực quan và tương tác của chủ dự án

Ngày 09/10/2026, chủ dự án mở ứng dụng Uno và gửi hai ảnh chụp phần trên/dưới của preview. Chủ dự án xác nhận giao diện ổn, sau đó trả lời **“Đã thử đủ, tất cả đều ổn”** khi được hỏi riêng về cửa sổ thu hẹp, Tab/focus, nhập vào ô và nút **Thử Lại**. Khi được hỏi tiếp về hover/nhấn các nút Chính/Phụ/Xóa, nút Vô Hiệu Hóa và chữ lỗi, chủ dự án xác nhận **“đã kiểm tra rồi và hoàn thành”**. Đây là xác nhận thủ công của chủ dự án, không phải kết quả kiểm thử tự động.

| Mục kiểm tra | Kết quả ghi nhận |
| --- | --- |
| Cửa sổ bình thường; palette, typography, chữ tiếng Việt, nút, badge và tổng thể so với thiết kế D31/D01 | Chủ dự án xác nhận giao diện ổn. Ảnh đã gửi cho thấy màu sắc, chữ, nút, badge và feedback. UI-00 là preview thành phần tối thiểu, không phải màn nghiệp vụ D01 hoàn chỉnh. |
| Cửa sổ thu hẹp và cuộn từ trên xuống dưới | Chủ dự án xác nhận đã thử và tất cả ổn; các swatch, nút, badge, hai ô nhập và bốn trạng thái feedback có thể đọc/tiếp cận mà không báo tràn ngang. |
| Nút primary, secondary, danger, disabled; hover/press và Tab focus | Preview dựa vào visual states native của WinUI/Uno cho hover, press, disabled và focus. Chủ dự án xác nhận đã kiểm tra hover/nhấn các nút, nút Vô Hiệu Hóa và Tab/focus, đều ổn. Không đo riêng màu render bằng công cụ tự động. |
| Ô nhập bình thường, ô lỗi và văn bản validation | Chủ dự án xác nhận Tab/focus, nhập liệu và chữ lỗi đều ổn; ảnh cho thấy viền lỗi đỏ và chú thích lỗi. |
| Loading, empty, error, success và Thử Lại | Hai ảnh chụp cho thấy cả bốn mẫu. Chủ dự án xác nhận nút Thử Lại hoạt động; lệnh chỉ cập nhật thông báo demo trong ViewModel. |

Layout swatch/button/badge tối đa hai cột, còn input/feedback/mẫu tiền và ảnh dự phòng xếp dọc. Không yêu cầu ảnh theo tổ hợp kích thước hoặc scale cố định. Các ảnh trước đó trong `openspec/changes/archive/2026-10-09-ui-00-theme-foundation/evidence/` chỉ để tham khảo, không thay thế xác nhận thủ công trên.

## Tương phản badge

Tỷ lệ tính từ cặp màu sRGB cuối cùng trong `TechStoreTheme.xaml` theo công thức độ chói tương đối WCAG:

| Badge | Chữ | Nền | Tỷ lệ |
| --- | --- | --- | ---: |
| Success | `#065F46` | `#ECFDF5` | 7.29:1 |
| Warning | `#92400E` | `#FFFBEB` | 6.84:1 |
| Danger | `#991B1B` | `#FEF2F2` | 7.60:1 |
| Neutral | `#334155` | `#F1F5F9` | 9.45:1 |

Cả bốn cặp vượt 4.5:1 cho chữ thường.

## Build và chạy thử

- Build cuối trên Linux: `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` — thành công, **0 warning, 0 error** sau thay đổi layout cuối cùng.
- Chạy thử từ phía triển khai: `dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --launch-profile "TechStore.Client (Desktop)"` — tiến trình ứng dụng vẫn hoạt động sau khi mở; `xwininfo` xác nhận cửa sổ `TechStore.Client` 1024×640. Đã đóng lượt chạy thử bằng Ctrl+C sau khi kiểm tra; không dùng việc tiến trình thoát làm bằng chứng ứng dụng chạy đúng.
- Chủ dự án chạy `dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --launch-profile "TechStore.Client (Desktop)"`, gửi ảnh cửa sổ đang mở và xác nhận nhập liệu/Thử Lại hoạt động. Đây là bằng chứng runtime sau thay đổi UI-00 cuối cùng, không suy ra chỉ từ build hoặc việc tiến trình thoát theo thời gian.
- Console có thông báo Uno DevServer/Hot Design không kết nối, cảnh báo spell-checking và X11 presenter; cửa sổ ứng dụng vẫn mở và các tương tác được xác nhận hoạt động. Chưa có dấu hiệu crash ứng dụng từ các thông báo này.

## Giới hạn đã biết

- Target Windows native `net10.0-windows10.0.26100` **chưa được build/chạy trên Windows** vì môi trường hiện tại là Linux. Không coi build Skia là xác nhận cho Windows native.
- Không đo pixel, màu hover/press thực tế hoặc trải nghiệm ở mọi tỷ lệ scale bằng công cụ tự động; nghiệm thu UI dựa trên quan sát và xác nhận của chủ dự án trong môi trường hiện có.
- Tích hợp API, dữ liệu thật và các màn nghiệp vụ nằm ngoài UI-00.
