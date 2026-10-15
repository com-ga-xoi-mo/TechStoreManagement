# Spec Delta

## MODIFIED Requirements

### Requirement: Six coherent admin destinations
Shell SHALL có đúng sáu điểm vào Tổng quan, Sản phẩm, Danh mục và Kho, Đơn hàng, Khách hàng, Cài đặt, được cấu trúc và khởi tạo hoàn toàn bằng C# Markup Fluent UI (`NavigationView`) tuân thủ nghiêm ngặt theo layout thiết kế chuẩn tại `stitch_techstore_retail_admin_dashboard/admin_dashboard_techstore_management_system/`. Selected menu, tiêu đề và nội dung SHALL cùng phản ánh route đã chuyển thành công. Đồng thời Pane Footer của Shell SHALL hiển thị khối profile tài khoản quản trị viên (Avatar tròn chữ lồng TS/Initials, FullName, Role tiếng Việt) bám sát mẫu thiết kế sidebar của Stitch khi có phiên đăng nhập, hoặc trạng thái "Tài khoản phát triển - Chưa đăng nhập" khi chưa có phiên. Sản phẩm SHALL mở Catalog đọc thật và chứa route chi tiết theo productId; Tổng quan SHALL thể hiện preview có nhãn khi demo bật, hoặc chưa tích hợp khi tắt; các trang nghiệp vụ chưa triển khai SHALL ghi rõ trạng thái khung thay vì bịa dữ liệu.

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

#### Scenario: Shell reflects active user session matching Stitch design
- **WHEN** Quản trị viên đăng nhập thành công với tài khoản "admin"
- **THEN** Pane footer của Shell cập nhật tên người dùng và vai trò "Quản trị viên", avatar và bố cục thông tin khớp nguyên mẫu Stitch sidebar

### Requirement: Session local settings
Cài đặt SHALL cho chọn System/Light/Dark, xem thông tin ứng dụng không nhạy cảm, đồng thời cho phép Quản trị viên xem và chỉnh sửa thông tin cấu hình cửa hàng TechStore (tên cửa hàng, địa chỉ, hotline, email, mã số thuế) thông qua C# Markup với CommunityToolkit SettingsControls, tái hiện trung thực form giao diện từ thiết kế Stitch `c_i_t_tab_1_c_a_h_ng_l_u_thay_i/code.html` (các tab cấu hình: Cửa hàng, Nhân viên & phân quyền, Thanh toán, Tài khoản của tôi; banner thông báo lưu thành công màu xanh lá `#ECFDF5`/`#065F46`; thẻ form trắng tối đa 680px, các trường nhãn có dấu sao đỏ, chú thích phụ bên phải, badge 'Đang kích hoạt', và nút 'Lưu thay đổi' `#0066FF`).

#### Scenario: Theme survives navigation
- **WHEN** chọn Dark, chuyển Sản phẩm rồi trở lại Cài đặt
- **THEN** Shell và dialogs dùng Dark, lựa chọn còn Dark; theme không phát request và Catalog chỉ gọi API cho dữ liệu của activation

#### Scenario: New process
- **WHEN** đóng rồi khởi chạy client lần mới
- **THEN** theme bắt đầu System, không tuyên bố đã lưu tùy chọn lâu dài

#### Scenario: Store configuration view and update matching Stitch prototype
- **WHEN** Quản trị viên mở trang Cài đặt tab "Cửa hàng"
- **THEN** Giao diện hiển thị đúng cấu trúc form của Stitch (Tên cửa hàng, Địa chỉ, Hotline, Email, Mã số thuế), cho phép nhập liệu, bấm Lưu thay đổi và xuất hiện banner thông báo thành công xanh lá
