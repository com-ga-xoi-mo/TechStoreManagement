# frontend-design-system Specification

## Purpose

Thống nhất hình thức, tài nguyên và khả năng tiếp cận của giao diện TechStore để các màn hình sử dụng cùng ngôn ngữ thiết kế và hiển thị đúng trên desktop được hỗ trợ.

## Requirements

### Requirement: Consistent visual language
Shared UI SHALL dùng cùng hệ thống màu semantic, typography, spacing và hình dạng theo baseline đã thống nhất từ mẫu TechStore, được định nghĩa và áp dụng thông qua C# Markup ThemeResources/Extensions thay vì XAML ResourceDictionaries. Cùng vai trò hiển thị SHALL có cùng hình thức trong gallery và khi được tái sử dụng.

#### Scenario: Light reference presentation
- **WHEN** gallery được mở ở Light theme với kích thước chuẩn đối chiếu
- **THEN** nền sáng, surface trắng, primary xanh, font Inter và nhịp spacing/radius tuân baseline style guide; metric cards giữ hierarchy của Dashboard, không lặp lỗi clipping trong ảnh mẫu

#### Scenario: Shared component reuse
- **WHEN** hai consumer hiển thị cùng loại button, input hoặc badge trong cùng theme
- **THEN** các trạng thái tương ứng dùng cùng typography, màu và hình dạng được cấp từ C# Design Tokens

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
