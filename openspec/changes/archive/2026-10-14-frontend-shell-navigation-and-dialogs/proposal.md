# Proposal

## Why

Core/API foundation và design system đã hoàn thành, nhưng MainPage hiện chỉ có menu placeholder, chưa cung cấp điều hướng và lifecycle dùng chung. Dev 6 cần Shell có thể gắn feature theo MVVM + Repository trước khi triển khai Catalog reference flow ở Change 4.

## What Changes

- Thay trang khởi động thông thường bằng Shell phát triển offline, lấy sidebar/header của mẫu `admin_dashboard_techstore_management_system` làm chuẩn, tái sử dụng tokens, font/icons và shared controls.
- Cung cấp đúng sáu điểm vào: Tổng quan, Sản phẩm, Danh mục và Kho, Đơn hàng, Khách hàng, Cài đặt; mỗi trang có ViewModel khung qua constructor DI.
- Thêm navigation adapter/page factory, lifecycle load/cancel và quy tắc state/revisit/late results có thể kiểm thử headless.
- Thêm dialog service với contract thuần C#, quản lý XamlRoot ở UI adapter, tuần tự dialog, cancellation và phục hồi focus.
- Dựng Settings bằng SettingsCard/SettingsExpander đã được chấp nhận ở Change 2, cho đổi theme trong phiên và xem thông tin ứng dụng.
- Giữ gallery opt-in; sửa requirement cũ đang chỉ định MainPage để phản ánh Shell startup. Không giả lập xác thực, không triển khai nghiệp vụ Dashboard, đăng nhập, CRUD, API/repository mới hoặc refresh token.
- Kiểm tra Windows/Skia, navigation/state/cancellation/dialog/resize và nghiệm thu chủ dự án trên build xác định, riêng với kết quả kỹ thuật.

## Capabilities

### New Capabilities

- `frontend-shell-navigation`: Shell sáu điểm vào, DI navigation, vòng đời trang, state, Settings và giao diện thích ứng.
- `frontend-dialogs`: Dialog thuần contract, tuần tự hóa, cancellation, root ownership và keyboard/focus.

### Modified Capabilities

- `frontend-component-gallery`: Default launch vào Shell thay MainPage; giữ nguyên opt-in gallery, offline preview và validation flag.

## Impact

Khi apply: thay wiring startup tại `App.xaml.cs`, mở rộng `UiServiceCollectionExtensions.cs`, thêm `Views/`, `ViewModels/`, `Services/Navigation`, `Services/Dialog` và Settings presentation service khi cần; mở rộng `tests/Frontend.UnitTests` cùng bằng chứng nghiệm thu. Tuân thủ `docs/MVVM_REPOSITORY_BASE_PROJECT.md` thay hướng dẫn Feature-First cũ. Giữ .NET 10, Uno 6.7.30, CPM và SettingsControls 8.2.250402; không thêm dependency navigation framework/Material, không đổi backend/shared contracts. Proposal này chỉ tạo planning artifacts.
