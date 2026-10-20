# Spec Delta

## MODIFIED Requirements

### Requirement: Live read-only catalog
Client SHALL lấy danh sách từ `GET /api/products`, chi tiết từ `GET /api/products/{id}` và lựa chọn danh mục từ `GET /api/categories/tree`. Việc duyệt Catalog (danh sách, lọc, chi tiết) MUST NOT gửi mutation, truy cập database trực tiếp hoặc tự thay dữ liệu thật bằng fixture khi có lỗi. Thao tác ghi chỉ xảy ra khi người dùng chủ động dùng các hành động quản lý sản phẩm thuộc capability `frontend-catalog-management`.

#### Scenario: Enter products
- **WHEN** người dùng mở Sản phẩm từ Shell và API khả dụng
- **THEN** danh sách hiển thị kết quả thật cùng metadata phân trang; chi tiết chưa được gọi cho từng card

#### Scenario: API unavailable
- **WHEN** API không thể kết nối khi mở Catalog
- **THEN** trang báo lỗi có retry, không hiển thị dữ liệu demo hoặc empty như một lần tải thành công

#### Scenario: Browsing sends no writes
- **WHEN** người dùng tìm kiếm, lọc, sắp xếp, phân trang, mở chi tiết và quay lại mà không bấm hành động quản lý nào
- **THEN** client chỉ gửi các yêu cầu `GET`
