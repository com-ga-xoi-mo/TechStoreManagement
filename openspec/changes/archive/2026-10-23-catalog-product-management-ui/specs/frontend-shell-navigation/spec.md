# Spec Delta

## ADDED Requirements

### Requirement: Products destination hosts editor routes
Mục Sản phẩm trên Shell SHALL chứa thêm route tạo sản phẩm và route sửa sản phẩm (mang id sản phẩm) bên cạnh danh sách và chi tiết. Điều hướng giữa các route này MUST NOT đổi mục đang chọn trên thanh điều hướng. Route tạo và sửa SHALL chỉ hợp lệ dưới mục Sản phẩm và SHALL mang theo ngữ cảnh truy vấn danh sách để quay lại.

#### Scenario: Open the create page
- **WHEN** người dùng bấm Thêm sản phẩm trên danh sách đang lọc theo từ khóa "pixel" ở trang 2
- **THEN** Shell hiển thị màn soạn trống, mục Sản phẩm vẫn được chọn, và Hủy đưa về danh sách "pixel" trang 2

#### Scenario: Open the edit page and come back
- **WHEN** người dùng bấm Sửa trên chi tiết một sản phẩm rồi Lưu thành công hoặc Hủy
- **THEN** Shell hiển thị lại chi tiết của đúng sản phẩm đó, tải lại từ API, và Quay lại từ chi tiết vẫn về danh sách với truy vấn cũ

#### Scenario: Editor routes cannot leave Products
- **WHEN** mã tạo route tạo hoặc sửa sản phẩm dưới một mục Shell khác, hoặc route sửa không có id hợp lệ
- **THEN** việc dựng route bị từ chối

#### Scenario: Existing routes unchanged
- **WHEN** người dùng điều hướng danh sách, chi tiết, quay lại trong Sản phẩm hoặc dùng các route của Đơn hàng
- **THEN** hành vi giữ nguyên như trước, kể cả ngữ cảnh truy vấn và trả focus về thẻ sản phẩm
