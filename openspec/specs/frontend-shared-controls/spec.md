# frontend-shared-controls Specification

## Purpose

Cung cấp các thành phần hiển thị số liệu, tìm kiếm, trạng thái rỗng và nhãn trạng thái có hành vi thống nhất để các feature TechStore tái sử dụng mà không tự xử lý nghiệp vụ bên trong control.

## Requirements

### Requirement: Metric presentation
Metric card SHALL hiển thị tiêu đề, giá trị, đơn vị và chi tiết/trend tùy chọn do consumer cung cấp; cập nhật khi dữ liệu đổi. Card MUST NOT tự tính KPI hoặc lấy dữ liệu nghiệp vụ.

#### Scenario: Value and optional detail update
- **WHEN** consumer đổi giá trị hoặc xóa trend/detail sau khi card đã xuất hiện
- **THEN** card hiển thị dữ liệu mới ngay, phần tùy chọn rỗng không để khoảng trống dư và giá trị 0 không bị hiểu thành thiếu dữ liệu

#### Scenario: Loading and failure
- **WHEN** consumer đánh dấu loading hoặc cung cấp lỗi cho card
- **THEN** loading được ưu tiên hơn lỗi, lỗi được ưu tiên hơn content; card không hiển thị số giả thay kết quả, và chỉ trở lại content khi loading/lỗi đã hết

#### Scenario: Missing and long value
- **WHEN** giá trị chưa được cung cấp hoặc giá trị/tiêu đề dài
- **THEN** giá trị thiếu hiện “—”, còn giá trị có thật vẫn đọc đủ qua layout thích ứng, không bị đổi thành 0 hoặc cắt mất phần quan trọng

### Requirement: Search editing and submission
Search control SHALL có nhãn nhìn thấy, cập nhật query của consumer theo mỗi thay đổi text và cho gửi tìm kiếm bằng Enter hoặc action tìm kiếm. Một lần submit SHALL phát đúng một command với query hiện tại, không tự gọi network hoặc lập lịch tìm kiếm.

#### Scenario: Immediate query update
- **WHEN** người dùng nhập “điện thoại & phụ kiện” mà chưa rời focus
- **THEN** consumer đã nhận query hiện tại đầy đủ dấu, khoảng trắng và ký tự đặc biệt

#### Scenario: Single explicit submission
- **WHEN** người dùng nhấn Enter hoặc click action tìm kiếm sau khi hoàn tất nhập
- **THEN** consumer nhận đúng một submit với text hiện tại; việc chỉ nhập/kết thúc composition tiếng Việt không tạo thêm lần submit

#### Scenario: Clear and disabled search
- **WHEN** người dùng kích hoạt clear khi đang có query
- **THEN** query trở thành rỗng, clear command tùy chọn chạy một lần, focus trở về input và không tự phát search
- **AND** khi control disabled hoặc search command không thể thực thi, action tìm kiếm không được gửi

### Requirement: Empty state with optional action
Empty state SHALL hiển thị title, description/icon tùy chọn và action do consumer cung cấp. Nó MUST NOT tự chuyển lỗi/loading thành trạng thái rỗng hoặc tự tải dữ liệu.

#### Scenario: Action is available
- **WHEN** consumer cung cấp cả nhãn action và command có thể thực thi
- **THEN** action hiển thị, kích hoạt được bằng keyboard/click và gọi đúng một lần với mỗi thao tác

#### Scenario: Missing or disabled action
- **WHEN** action thiếu nhãn/command hoặc command không thể thực thi
- **THEN** action thiếu bị ẩn; action không thể thực thi hiển thị disabled và không chạy; nội dung empty vẫn đọc được

### Requirement: Semantic status badge
Status badge SHALL hiển thị nhãn do consumer cung cấp với tone semantic và không suy diễn trạng thái nghiệp vụ từ nhãn. Badge SHALL luôn truyền nghĩa bằng chữ, giữ đọc được trong theme và không tạo tab stop như một button.

#### Scenario: Label and tone change
- **WHEN** consumer đổi nhãn hoặc tone của badge đang hiện
- **THEN** cả chữ và hình thức cập nhật; nhãn “Đang sửa chữa” hoặc “Chờ thanh toán” không bị cắt dấu

#### Scenario: Unknown tone or missing label
- **WHEN** tone không nhận diện hoặc label không có nội dung
- **THEN** tone lạ dùng Neutral khi có label; label rỗng không tạo badge trống hoặc tự bịa trạng thái

### Requirement: Consumer owned behavior
Shared controls SHALL chỉ trình bày state và chuyển thao tác người dùng tới consumer. Control MUST NOT tự giữ phiên đăng nhập, gọi API hoặc thay đổi dữ liệu nghiệp vụ.

#### Scenario: Offline reuse with changing state
- **WHEN** controls được đặt trong gallery offline với state và commands mới từ consumer
- **THEN** controls cập nhật đúng, không cần backend/session và không tạo request nghiệp vụ để render hoặc xử lý input
