# Spec Delta: frontend-shared-controls

## MODIFIED Requirements

### Requirement: Consumer owned behavior
Shared controls (`StatCard`, `SearchBoxControl`, `EmptyStateControl`, `StatusBadge`) SHALL được hiện thực hóa bằng mô hình C# Markup code-first, chỉ trình bày state và chuyển thao tác người dùng tới consumer thông qua MVVM bindings. Control MUST NOT tự giữ phiên đăng nhập, gọi API hoặc thay đổi dữ liệu nghiệp vụ.

#### Scenario: Offline reuse with changing state
- **WHEN** controls được đặt trong gallery offline với state và commands mới từ consumer
- **THEN** controls cập nhật đúng qua C# markup bindings, không cần backend/session và không tạo request nghiệp vụ để render hoặc xử lý input
