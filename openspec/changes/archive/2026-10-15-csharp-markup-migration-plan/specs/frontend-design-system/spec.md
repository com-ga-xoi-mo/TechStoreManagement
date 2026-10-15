# Spec Delta: frontend-design-system

## MODIFIED Requirements

### Requirement: Consistent visual language
Shared UI SHALL dùng cùng hệ thống màu semantic, typography, spacing và hình dạng theo baseline đã thống nhất từ mẫu TechStore, được định nghĩa và áp dụng thông qua C# Markup ThemeResources/Extensions thay vì XAML ResourceDictionaries. Cùng vai trò hiển thị SHALL có cùng hình thức trong gallery và khi được tái sử dụng.

#### Scenario: Light reference presentation
- **WHEN** gallery được mở ở Light theme với kích thước chuẩn đối chiếu
- **THEN** nền sáng, surface trắng, primary xanh, font Inter và nhịp spacing/radius tuân baseline style guide; metric cards giữ hierarchy của Dashboard, không lặp lỗi clipping trong ảnh mẫu

#### Scenario: Shared component reuse
- **WHEN** hai consumer hiển thị cùng loại button, input hoặc badge trong cùng theme
- **THEN** các trạng thái tương ứng dùng cùng typography, màu và hình dạng được cấp từ C# Design Tokens
