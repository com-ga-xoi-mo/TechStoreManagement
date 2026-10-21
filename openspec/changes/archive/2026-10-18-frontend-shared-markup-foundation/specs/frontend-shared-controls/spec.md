# Spec Delta

## ADDED Requirements

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
