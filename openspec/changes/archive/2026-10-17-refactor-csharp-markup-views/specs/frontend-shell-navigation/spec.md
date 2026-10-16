# Spec Delta

## MODIFIED Requirements

### Requirement: Six coherent admin destinations
Shell SHALL có đúng sáu điểm vào Tổng quan, Sản phẩm, Danh mục và Kho, Đơn hàng, Khách hàng, Cài đặt, được cấu trúc và khởi tạo bằng C# Markup Fluent API (`Uno.Extensions.Markup.WinUI` trên `NavigationView`). Selected menu, tiêu đề, và nội dung SHALL cùng phản ánh route đã chuyển thành công. Đồng thời Pane Footer của Shell SHALL hiển thị trạng thái tài khoản đang đăng nhập (FullName, Role) và nút Đăng xuất thông qua declarative bindings của C# Markup thay vì manual PropertyChanged handlers. Sản phẩm SHALL mở Catalog đọc thật và chứa route chi tiết theo productId; Tổng quan SHALL thể hiện preview có nhãn khi demo bật, hoặc chưa tích hợp khi tắt; các trang nghiệp vụ chưa triển khai SHALL ghi rõ trạng thái khung thay vì bịa dữ liệu.

#### Scenario: Visit every destination
- **WHEN** lần lượt chọn sáu điểm vào bằng chuột hoặc keyboard
- **THEN** nội dung tương ứng hiển thị, Sản phẩm tải Catalog thật, selection/title đúng, Cài đặt chỉ xuất hiện một lần ở footer và không có mục POS/Identity bổ sung

#### Scenario: Same destination selected again
- **WHEN** chọn lại route hiện hành với cùng tham số
- **THEN** nội dung/state giữ nguyên, không tạo trang hoặc load lần nữa

#### Scenario: Unknown or unavailable destination
- **WHEN** yêu cầu route không hỗ trợ, productId rỗng hoặc không tạo được trang đích
- **THEN** giữ trang/title/selection trước đó và báo lỗi an toàn, không để Shell trống hoặc selection sai

#### Scenario: Read-only detail navigation
- **WHEN** mở card A rồi chuyển tới detail B trong nhánh Sản phẩm
- **THEN** truyền đúng id từng sản phẩm, tải chi tiết tương ứng, hủy activation trước và giữ Sản phẩm selected; detail không tạo mục menu thứ bảy

#### Scenario: Preview stays within dashboard
- **WHEN** mở Dashboard demo rồi chọn Sản phẩm hoặc mục khác
- **THEN** vẫn có đúng sáu mục, Catalog tiếp tục dùng API thật và các mục chưa triển khai giữ trạng thái khung

#### Scenario: Shell reflects active user session
- **WHEN** Quản trị viên đăng nhập thành công với tài khoản "admin"
- **THEN** Pane footer của Shell cập nhật tên người dùng và vai trò "Quản trị viên", nhãn trạng thái chuyển thành "Đã đăng nhập"

### Requirement: Session local settings
Cài đặt SHALL cho chọn System/Light/Dark, xem thông tin ứng dụng không nhạy cảm, đồng thời cho phép Quản trị viên xem và chỉnh sửa thông tin cấu hình cửa hàng TechStore (tên cửa hàng, địa chỉ, hotline, mã số thuế) thông qua Uno C# Markup Fluent API (`Uno.Extensions.Markup`) kết hợp với CommunityToolkit SettingsControls. Toàn bộ form fields, thông báo lưu thành công và nút lưu SHALL sử dụng declarative two-way bindings. Theme SHALL áp dụng ngay trong Shell/dialog, giữ khi chuyển trang trong phiên và mặc định System ở phiên mới. Thao tác theme không cần backend; các thay đổi cấu hình cửa hàng phát sinh lưu trữ qua API cấu hình.

#### Scenario: Theme survives navigation
- **WHEN** chọn Dark, chuyển Sản phẩm rồi trở lại Cài đặt
- **THEN** Shell và dialogs dùng Dark, lựa chọn còn Dark; theme không phát request và Catalog chỉ gọi API cho dữ liệu của activation

#### Scenario: New process
- **WHEN** đóng rồi khởi chạy client lần mới
- **THEN** theme bắt đầu System, không tuyên bố đã lưu tùy chọn lâu dài

#### Scenario: Store configuration view and update
- **WHEN** Quản trị viên mở trang Cài đặt và cập nhật hotline cửa hàng
- **THEN** Giao diện hiển thị form thông tin cửa hàng sử dụng SettingsCard/SettingsExpander khai báo bằng C# Markup fluent bindings, lưu dữ liệu thành công và thông báo kết quả
