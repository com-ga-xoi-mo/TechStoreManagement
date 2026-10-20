# Spec Delta

## MODIFIED Requirements

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
