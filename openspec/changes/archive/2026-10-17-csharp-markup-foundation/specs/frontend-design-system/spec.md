# Spec Delta

## ADDED Requirements

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
