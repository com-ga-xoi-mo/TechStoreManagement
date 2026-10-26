## MODIFIED Requirements

### Requirement: Category filter availability
Client SHALL hiển thị tên và phân cấp danh mục từ cây API, không bịa danh mục hoặc counts. Danh mục ẩn (`isActive` false) vẫn có trong bộ lọc để tìm sản phẩm cũ, nhưng nhãn SHALL có hậu tố "(đang ẩn)". Lỗi tải cây SHALL có thông báo và retry riêng, không biến kết quả sản phẩm thành rỗng hay chặn tìm kiếm không dùng danh mục.

#### Scenario: Parent category selected
- **WHEN** chọn một danh mục cha và áp dụng
- **THEN** gửi đúng categoryId của nó, không tự thêm sản phẩm thuộc danh mục con; nhãn giải thích phạm vi lọc trực tiếp

#### Scenario: Category lookup failure
- **WHEN** danh sách tải thành công nhưng cây danh mục lỗi
- **THEN** kết quả vẫn dùng được, bộ chọn danh mục báo chưa tải được và cho retry, không lấy category facets từ một trang sản phẩm

#### Scenario: Hidden category in the filter
- **WHEN** cây danh mục có một danh mục ẩn
- **THEN** bộ lọc vẫn liệt kê danh mục đó với hậu tố "(đang ẩn)" và chọn nó gửi đúng categoryId của nó
