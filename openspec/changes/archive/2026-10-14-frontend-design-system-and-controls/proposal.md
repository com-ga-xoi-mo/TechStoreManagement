# Proposal

## Why

Core/API foundation đã được archive nhưng client mới có MainPage và resources WinUI mặc định. Dev 6 cần một design system và gallery được nghiệm thu trước khi dựng Shell, Catalog và Dashboard, để các feature dùng chung hình thức và hành vi thay vì diễn giải khác nhau các mẫu UI đang có.

## What Changes

- Chuẩn hóa tokens từ style guide: Inter, font dữ liệu mono, màu semantic, typography, spacing, radius và trạng thái tương tác; ghi rõ cách xử lý khác biệt với Dashboard HTML và `fluent_retail_command/DESIGN.md`.
- Đóng gói font/icon cần thiết cùng nguồn/license; hỗ trợ tiếng Việt và ký hiệu tiền VND khi offline.
- Bổ sung theme resources Light/Dark/HighContrast và styles dùng lại cho button, input, form validation, table header/row, feedback; giữ nền WinUI/Fluent.
- Tạo `StatCard`, `SearchBoxControl`, `EmptyStateControl`, `StatusBadge` bằng cách ghép controls có sẵn; cung cấp binding/command rõ ràng, keyboard focus và nhãn accessibility.
- Ưu tiên Windows Community Toolkit 8.x khi phù hợp. Đánh giá `SettingsControls` cho bộ điều khiển theme/state của gallery; chỉ pin qua CPM sau kiểm tra restore/build/runtime trên Uno 6.7.30 và hai target. Không thêm package chỉ vì tên thư viện.
- Thêm gallery phát triển bật tường minh, dùng fixtures được ghi nhãn, chạy offline và không thay đường khởi động mặc định bằng dashboard giả.
- Chuẩn bị WinApp CLI 0.7+ ở giai đoạn apply để tra sample và API đúng project trước khi viết XAML. Nghiệm thu runtime, đối chiếu mẫu, tiếng Việt, keyboard, resize, theme và owner acceptance thành các task có bằng chứng riêng.

## Capabilities

### New Capabilities

- `frontend-design-system`: Hình thức nhất quán, tài nguyên offline, theme và accessibility của giao diện dùng chung.
- `frontend-shared-controls`: Hành vi hiển thị/binding/command của metric card, search, empty state và status badge.
- `frontend-component-gallery`: Môi trường phát triển để thử các trạng thái controls và thu bằng chứng nghiệm thu trực quan.

### Modified Capabilities

Không có. `frontend-runtime` và `frontend-api-access` tiếp tục giữ nguyên: khởi động không phụ thuộc backend, session và transport không bị thay đổi yêu cầu.

## Impact

- Dự kiến tác động `frontend/TechStore.Client/{Styles,Controls,Assets,Views/Development,ViewModels/Development}`, `App.xaml`, wiring gallery trong `App.xaml.cs`, extension đăng ký UI riêng, và CPM nếu toolkit được chọn.
- Bổ sung test state/command thuần C# trong `tests/Frontend.UnitTests` và hướng dẫn sử dụng design system tại apply. Không đưa UI registrations vào composition source đang được source-link bởi headless tests.
- Giữ .NET 10, Uno.Sdk 6.7.30, `net10.0-desktop`, `net10.0-windows10.0.26100`; tái sử dụng MVVM Toolkit implicit qua UnoFeatures.
- Kiến trúc tuân `docs/MVVM_REPOSITORY_BASE_PROJECT.md`, không quay lại Feature-First/MVUX. Change này không cần repository mới vì gallery không có nguồn dữ liệu nghiệp vụ.
- Ngoài phạm vi: Shell/navigation/dialog service, API feature, auth, CRUD, chart, dashboard thật, đổi theme toàn app sang Material. Đây chỉ là planning artifacts; không sửa code hoặc cài tool/package trong lượt proposal.
