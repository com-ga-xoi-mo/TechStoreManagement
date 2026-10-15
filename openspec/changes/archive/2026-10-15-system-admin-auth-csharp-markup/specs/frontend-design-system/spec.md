# Spec Delta

## MODIFIED Requirements

### Requirement: Consistent visual language
Shared UI SHALL dùng cùng hệ thống màu semantic, typography, spacing và hình dạng theo baseline chuẩn đã được thiết kế chi tiết tại `stitch_techstore_retail_admin_dashboard/` (Google Stitch prototypes bao gồm `techstore_design_system`, `techstore_style_guide_system_specification_board`, `ng_nh_p_techstore_management_system`, `c_i_t_tab_1_c_a_h_ng_l_u_thay_i`, `admin_dashboard_techstore_management_system`). Toàn bộ View và Dialog SHALL được định nghĩa và áp dụng thông qua C# Markup ThemeResources/Extensions (`Uno.Extensions.Markup`) kết hợp các thư viện UI WinUI hiện đại (CommunityToolkit Controls/SettingsControls) để tái tạo chính xác từng tỷ lệ layout, màu sắc, font chữ, hiệu ứng hover/active và biểu tượng, tuyệt đối không code chay lệch chuẩn hoặc dùng component tạm bợ.

#### Scenario: Light reference presentation
- **WHEN** giao diện hoặc trang cấu hình được hiển thị ở Light theme
- **THEN** các bảng màu nền `#F8FAFC`, bề mặt thẻ trắng `#FFFFFF` bo góc `12px`/`16px` viền `#E2E8F0`, màu chủ đạo primary xanh dương `#0066FF` (hover `#0052CC`, pressed `#0040A8`, background `#EBF3FF`) tuân thủ chính xác từng mã màu của Stitch design system

#### Scenario: Shared component reuse
- **WHEN** hai consumer hiển thị cùng loại button, input hoặc badge trong cùng theme
- **THEN** các trạng thái tương ứng dùng cùng typography, màu và hình dạng được cấp từ C# Design Tokens theo mẫu Stitch

#### Scenario: Modern UI library integration with C# Markup
- **WHEN** hiển thị các nhóm thiết lập hoặc form cấu hình quản trị trong C# Markup
- **THEN** sử dụng `SettingsCard`, `SettingsExpander` từ CommunityToolkit với Fluent styling và icon nhất quán, kế thừa đúng các trường và layout của bản thiết kế Stitch `c_i_t_tab_1_c_a_h_ng_l_u_thay_i`

## ADDED Requirements

### Requirement: Pixel-accurate Login View from Stitch Design
Giao diện Đăng nhập `LoginPage.cs` SHALL được triển khai bằng C# Markup tái hiện chính xác 100% bản thiết kế Stitch tại `stitch_techstore_retail_admin_dashboard/ng_nh_p_techstore_management_system/` (chia 2 cột: Cột trái Branding Panel gradient `#0B1528` - `#0F1E3D` - `#0A2558`, logo TechStore, badge "Nền tảng vận hành POS & Kho đa kênh", mô tả lợi ích kiểm soát 15 số IMEI và thanh toán; Cột phải Form đăng nhập nền `#F8FAFC`, thẻ form trắng căn giữa 440px có tiêu đề, ô nhập tên đăng nhập kèm icon User, ô nhập mật khẩu kèm icon Lock và nút ẩn/hiện mắt, checkbox ghi nhớ đăng nhập, các khối thông báo lỗi đỏ nhạt/cam theo trạng thái xác thực và nút "Đăng nhập" `#0066FF` có hiệu ứng loading).

#### Scenario: Render accurate Login Page layout
- **WHEN** người dùng mở ứng dụng và điều hướng tới trang đăng nhập
- **THEN** màn hình hiển thị chính xác bố cục 2 cột (branding trái và form phải) với typography Inter/Plus Jakarta Sans, màu sắc và icon chuẩn xác theo nguyên mẫu Stitch

#### Scenario: Visual validation and error feedback matching Stitch
- **WHEN** xảy ra lỗi đăng nhập (sai thông tin, tài khoản bị khóa, hoặc để trống ô)
- **THEN** form hiển thị các banner thông báo lỗi và text lỗi đỏ/cam ngay phía trên nút bấm hoặc dưới ô nhập liệu đúng theo thiết kế Stitch
