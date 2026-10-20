## ADDED Requirements

### Requirement: Orders destination hosts list and create routes
Mục Đơn hàng trên Shell SHALL chứa trang danh sách đơn và trang lập đơn như hai route của cùng một destination. Điều hướng từ danh sách sang lập đơn và ngược lại MUST NOT làm đổi mục đang chọn trên thanh điều hướng, và route lập đơn SHALL chỉ hợp lệ dưới destination Đơn hàng.

#### Scenario: Open the create page
- **WHEN** người dùng bấm "Lập đơn mới" trên danh sách đơn
- **THEN** Shell hiển thị trang lập đơn, mục Đơn hàng vẫn được chọn và trang lập đơn có vòng đời kích hoạt/hủy như mọi trang khác

#### Scenario: Return to the list
- **WHEN** người dùng bấm quay về danh sách hoặc một đơn được tạo thành công
- **THEN** Shell hiển thị lại danh sách đơn, mục Đơn hàng vẫn được chọn và danh sách được tải lại để có đơn mới

#### Scenario: Create route under a wrong destination
- **WHEN** mã cố tạo route lập đơn dưới destination khác Đơn hàng
- **THEN** việc tạo route bị từ chối và không có trang nào được dựng

#### Scenario: Catalog routes unchanged
- **WHEN** người dùng điều hướng trong Sản phẩm (danh sách, chi tiết, quay lại)
- **THEN** hành vi giữ nguyên như trước, kể cả ngữ cảnh truy vấn và sản phẩm được tô nổi
