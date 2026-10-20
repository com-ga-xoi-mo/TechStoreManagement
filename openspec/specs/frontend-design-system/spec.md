# frontend-design-system Specification

## Purpose

Thống nhất hình thức, tài nguyên và khả năng tiếp cận của giao diện TechStore để các màn hình sử dụng cùng ngôn ngữ thiết kế và hiển thị đúng trên desktop được hỗ trợ.

## Requirements

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

### Requirement: Offline Vietnamese assets
Shared UI SHALL hiển thị đầy đủ chữ tiếng Việt, ký hiệu tiền và icons khi không có mạng hoặc font hệ thống của Windows. Asset đi kèm SHALL có nguồn và license cho phép phân phối được ghi nhận.

#### Scenario: Offline first launch
- **WHEN** chạy gallery trên Windows hoặc Skia không kết nối mạng
- **THEN** các font/icon cần thiết vẫn hiện, không có ô vuông hay chữ ligature thay icon, và chuỗi “Nguyễn Thị Mỹ — 34.990.000 ₫” đọc được đầy đủ

#### Scenario: Enlarged text
- **WHEN** hiển thị “Ắ ằ ễ ộ ự ỹ”, nhãn dài và IMEI tại scale 100%,150%,200%
- **THEN** không cắt dấu hoặc mất thông tin và hành động do khung chữ cố định; text có thể wrap và nội dung vẫn truy cập được

### Requirement: Live theme adaptation
Shared UI SHALL thích ứng System, Light và Dark khi cửa sổ đang mở. Trên Windows Contrast theme, UI SHALL dùng màu hệ thống phù hợp và giữ nghĩa trạng thái bằng chữ, không chỉ bằng màu.

#### Scenario: Theme changes in place
- **WHEN** người dùng đổi Light sang Dark hoặc trả về System trong gallery
- **THEN** controls đang hiển thị cập nhật surface/text/stroke/focus mà không cần tạo lại cửa sổ, query và state hiện tại không bị reset

#### Scenario: Windows contrast theme
- **WHEN** Windows đang dùng Contrast theme và gallery được mở hoặc cập nhật theme
- **THEN** text, focus, controls và status labels đọc được với màu hệ thống, không biến mất vì fill/opacity cố định

### Requirement: Accessible interaction states
Interactive UI SHALL thể hiện default, hover, pressed, focused, disabled và validation error ở nơi áp dụng. Controls SHALL có visible label hoặc tên accessibility có nghĩa, keyboard focus rõ và contrast phù hợp.

#### Scenario: Keyboard and disabled behavior
- **WHEN** người dùng di chuyển bằng Tab/Shift+Tab và kích hoạt action bằng Enter/Space
- **THEN** thứ tự focus hợp lý, focus nhìn thấy, action khả dụng hoạt động; action disabled không thực thi

#### Scenario: Validation is more than color
- **WHEN** form specimen chuyển sang lỗi validation
- **THEN** có thông báo tiếng Việt liên kết với input để người dùng và công cụ accessibility nhận diện; sửa input hợp lệ thì lỗi được xóa

#### Scenario: Readable foreground and indicators
- **WHEN** kiểm tra các cặp màu Light/Dark trong trạng thái sử dụng bình thường
- **THEN** chữ thường đạt contrast ít nhất 4.5:1, chữ lớn ít nhất 3:1 và indicator focus/control cần nhận diện đạt ít nhất 3:1 với vùng kề

### Requirement: Responsive shared presentation
Shared UI SHALL thích ứng vùng hiển thị desktop 800×600 đến 1440×1000 DIP, giữ nội dung và thao tác truy cập được. Phần đọc dữ liệu dạng bảng SHALL giữ các cột truy cập được khi vùng hiển thị hẹp.

#### Scenario: Narrow gallery
- **WHEN** resize gallery từ 1440×1000 xuống 1024×768 rồi 800×600 DIP
- **THEN** sections/cards reflow, nội dung có thể cuộn tới, không đè nhau hoặc cắt action; dữ liệu bảng có thể cuộn ngang trong vùng riêng khi cần

### Requirement: Pixel-accurate Login View from Stitch Design
Giao diện Đăng nhập `LoginPage.cs` SHALL được triển khai bằng C# Markup tái hiện chính xác 100% bản thiết kế Stitch tại `stitch_techstore_retail_admin_dashboard/ng_nh_p_techstore_management_system/` (chia 2 cột: Cột trái Branding Panel gradient `#0B1528` - `#0F1E3D` - `#0A2558`, logo TechStore, badge "Nền tảng vận hành POS & Kho đa kênh", mô tả lợi ích kiểm soát 15 số IMEI và thanh toán; Cột phải Form đăng nhập nền `#F8FAFC`, thẻ form trắng căn giữa 440px có tiêu đề, ô nhập tên đăng nhập kèm icon User, ô nhập mật khẩu kèm icon Lock và nút ẩn/hiện mắt, checkbox ghi nhớ đăng nhập, các khối thông báo lỗi đỏ nhạt/cam theo trạng thái xác thực và nút "Đăng nhập" `#0066FF` có hiệu ứng loading).

#### Scenario: Render accurate Login Page layout
- **WHEN** người dùng mở ứng dụng và điều hướng tới trang đăng nhập
- **THEN** màn hình hiển thị chính xác bố cục 2 cột (branding trái và form phải) với typography Inter/Plus Jakarta Sans, màu sắc và icon chuẩn xác theo nguyên mẫu Stitch

#### Scenario: Visual validation and error feedback matching Stitch
- **WHEN** xảy ra lỗi đăng nhập (sai thông tin, tài khoản bị khóa, hoặc để trống ô)
- **THEN** form hiển thị các banner thông báo lỗi và text lỗi đỏ/cam ngay phía trên nút bấm hoặc dưới ô nhập liệu đúng theo thiết kế Stitch

### Requirement: Single source of design tokens
Shared controls và màn hình dùng token dùng chung (màu semantic, typography, spacing, radius, icon) SHALL nhận giá trị từ một nguồn duy nhất. Màn hình XAML chưa chuyển đổi và màn hình C# Markup SHALL nhận cùng giá trị cho cùng vai trò trong cùng theme. Giá trị Light SHALL theo bảng màu Stitch đã nêu trong yêu cầu "Consistent visual language". Màn hình mới MUST NOT dùng nguồn màu riêng ngoài token dùng chung.

#### Scenario: Same role, same value across view technologies
- **WHEN** một màn hình XAML và một màn hình C# Markup cùng hiển thị nút primary, nền canvas và viền surface ở Light theme
- **THEN** cả hai dùng primary `#0066FF` (hover `#0052CC`, pressed `#0040A8`), canvas `#F8FAFC` và viền `#E2E8F0`, không có màn hình nào giữ primary `#2563EB` cũ

#### Scenario: One change propagates everywhere
- **WHEN** developer đổi giá trị một token trong nguồn token duy nhất
- **THEN** mọi màn hình và shared control dùng token đó nhận giá trị mới mà không phải sửa từng màn hình

#### Scenario: Known legacy color source is tracked, not extended
- **WHEN** kiểm tra các nguồn màu trong client sau change này
- **THEN** nguồn màu tĩnh cũ (`StitchTokens`) chỉ còn được dùng bởi Đăng nhập, Shell và Cài đặt, được ghi nhận là việc tồn đọng cần chuyển sang token dùng chung, và không màn hình hay control mới nào tham chiếu tới nó

### Requirement: Token compatibility for unmigrated screens
Trong giai đoạn chuyển đổi, mọi resource dùng chung mà màn hình hoặc control XAML hiện tham chiếu theo tên SHALL tiếp tục tồn tại với cùng tên và cùng loại giá trị trong Light, Dark và HighContrast. Việc gỡ các file định nghĩa resource dạng XAML MUST NOT làm màn hình chưa chuyển đổi lỗi tải hoặc mất style.

#### Scenario: Unmigrated screen still renders
- **WHEN** các màn hình XAML hiện có (Sản phẩm, Chi tiết sản phẩm, Tổng quan, Kho, Đơn hàng, Khách hàng) được mở sau khi resource được định nghĩa bằng code
- **THEN** màn hình tải không có lỗi resource, giữ màu, font, spacing và style nút/ô nhập như trước, ngoại trừ nhóm màu primary đổi theo bảng Stitch

#### Scenario: Theme dictionaries remain complete
- **WHEN** kiểm tra định nghĩa token màu dùng chung cho từng theme Light, Dark và HighContrast
- **THEN** mỗi key màu có giá trị trong cả ba theme; giá trị HighContrast trỏ tới màu hệ thống thay vì màu cố định

#### Scenario: Runtime theme switch on supported desktop
- **WHEN** ứng dụng chuyển giữa Light và Dark trong lúc đang chạy
- **THEN** mỗi resource màu dùng chung lấy giá trị của theme đang áp dụng; kiểm tra Contrast theme khi chạy thật cần Windows và được ghi riêng là chưa kiểm nếu chưa thực hiện

### Requirement: Theme-bound colors in code-first views
View và control viết bằng C# Markup SHALL lấy màu từ token theo theme, không dùng giá trị màu cố định. Khi theme đổi trong lúc cửa sổ đang mở, surface, text, stroke và màu trạng thái của chúng SHALL cập nhật tại chỗ mà View không phải tự xử lý sự kiện đổi theme.

#### Scenario: Live theme switch on a code-first view
- **WHEN** người dùng đổi từ Light sang Dark rồi về theo hệ thống khi một màn hình C# Markup đang hiển thị
- **THEN** nền, chữ, viền và badge của màn hình đổi theo theme ngay, dữ liệu và nội dung đã nhập không bị reset

#### Scenario: No fixed color in new code-first views
- **WHEN** kiểm tra mã nguồn của View và control C# Markup được thêm theo chuẩn mới
- **THEN** không có màu khai báo bằng giá trị cố định; mọi ngoại lệ được ghi lý do trong tài liệu thiết kế của change
