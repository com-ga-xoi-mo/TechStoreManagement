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
Shared controls (`StatCard`, `SearchBoxControl`, `EmptyStateControl`, `StatusBadge`) SHALL được hiện thực hóa bằng mô hình C# Markup code-first, chỉ trình bày state và chuyển thao tác người dùng tới consumer thông qua MVVM bindings. Control MUST NOT tự giữ phiên đăng nhập, gọi API hoặc thay đổi dữ liệu nghiệp vụ. Tên, kiểu và giá trị mặc định của các thuộc tính công khai SHALL giữ nguyên để consumer XAML hiện có và consumer C# Markup cùng sử dụng được.

#### Scenario: Offline reuse with changing state
- **WHEN** controls được đặt trong gallery offline với state và commands mới từ consumer
- **THEN** controls cập nhật đúng qua C# markup bindings, không cần backend/session và không tạo request nghiệp vụ để render hoặc xử lý input

#### Scenario: Existing XAML consumers keep working
- **WHEN** các màn hình XAML hiện có đặt `StatCard`, `SearchBoxControl`, `EmptyStateControl` hoặc `StatusBadge` với các thuộc tính đang dùng (ví dụ `Title`, `ValueText`, `Tone`, `Text`, `SearchCommand`, `ActionCommand`, `Label`)
- **THEN** màn hình tải và hiển thị như trước mà không phải sửa khai báo; thuộc tính không được đặt nhận giá trị mặc định cũ (ví dụ nhãn tìm kiếm “Tìm kiếm”, tone `Neutral`)

#### Scenario: Code-first consumer binds declaratively
- **WHEN** một View C# Markup đặt shared control và bind thuộc tính, command của control với ViewModel
- **THEN** giá trị đổi trong ViewModel hiện trên control và thao tác trên control tới ViewModel mà View không phải tự đăng ký sự kiện để đồng bộ

#### Scenario: Tone colors follow the active theme
- **WHEN** `StatusBadge` hoặc `StatCard` đang hiển thị một tone và người dùng đổi Light, Dark hoặc HighContrast
- **THEN** nền, viền và chữ của tone cập nhật theo theme mới tại chỗ, nhãn vẫn đọc được và nghĩa trạng thái không chỉ dựa vào màu

### Requirement: Public control compatibility across consumers
Bốn shared controls SHALL giữ public type identity, constructor không tham số, DependencyProperty names/types/defaults và command arguments hiện hành. Consumer XAML và C# SHALL bind cùng API, nhận thay đổi hai chiều nơi đã hỗ trợ, và không cần wrapper riêng cho mỗi loại View.

#### Scenario: Existing XAML consumer binds migrated controls
- **WHEN** Catalog hoặc Dashboard XAML hiện có tạo control và bind qua các property names cũ
- **THEN** control được tạo thành công, nhận state mới và cập nhật consumer theo binding mode hiện có mà không sửa consumer contract

#### Scenario: Search command arguments remain compatible
- **WHEN** người dùng submit query hoặc clear từ consumer C# hay XAML
- **THEN** search nhận string hiện hành đúng một lần, clear nhận null đúng một lần khi command khả dụng, clear không submit và focus quay về ô nhập

#### Scenario: Optional values and zero remain distinct
- **WHEN** consumer đổi metric từ thiếu sang 0, xóa detail/icon hoặc đổi badge tone sang giá trị không nhận diện
- **THEN** metric 0 vẫn hiển thị, phần tùy chọn bị ẩn phù hợp và badge có label dùng Neutral, không tạo dữ liệu nghiệp vụ giả

### Requirement: Command and theme updates survive control reuse
Shared controls SHALL phản ánh command replacement, CanExecute changes và theme changes trong khi đang hiển thị. Việc mount/unmount lặp MUST NOT nhân đôi action, giữ subscription của command cũ hoặc làm mất input/state.

#### Scenario: Command replaced while displayed
- **WHEN** consumer thay search/action command và phát CanExecuteChanged
- **THEN** control phản ánh trạng thái enabled của command mới, action bị chặn khi không thể thực thi và command cũ không còn được gọi

#### Scenario: Repeated mounting and Vietnamese input
- **WHEN** control được mount/unmount nhiều lần rồi người dùng nhập tiếng Việt và gửi query
- **THEN** composition không tự submit, một thao tác gửi gọi đúng một command và không có handler cũ gọi thêm

#### Scenario: Theme change while state is present
- **WHEN** shared controls có input/value/label đang hiển thị và đổi theme hoặc Windows Contrast
- **THEN** text, icon, border, focus và status indicators cập nhật theo semantic resources, giữ state và khả năng đọc nhãn
