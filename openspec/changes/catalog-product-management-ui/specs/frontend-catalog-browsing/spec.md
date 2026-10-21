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

### Requirement: Submitted catalog criteria
Client SHALL cho lọc trên giao diện theo từ khóa q, categoryId, isActive và các spec key:value. Từ khóa là ô tìm chung: API khớp tên, SKU, hãng và barcode, nên giao diện không có ô thương hiệu riêng. Giao diện không có ô khoảng giá; repository vẫn gửi được brand, minPrice và maxPrice theo API khi được truyền vào. Bộ lọc thông số SHALL được thu gọn mặc định, mở bằng một nút, và gồm các dòng điều kiện: chọn tên rồi chọn giá trị từ `GET /api/products/spec-keys` theo danh mục đang chọn; mỗi dòng gửi đúng `filterValue` do API trả về. Bộ lọc tự mở khi truy vấn đang áp dụng có thông số hoặc khi điều kiện chưa hợp lệ. Search/filter chỉ áp dụng khi submit bằng nút hoặc Enter. Category chỉ lọc trực tiếp và các điều kiện kết hợp AND theo API.

#### Scenario: Unicode and reserved characters
- **WHEN** submit từ khóa có dấu, khoảng trắng, `&`, `+`, `%`, `#` hoặc spec value chứa dấu hai chấm
- **THEN** mỗi query value được encode riêng; API nhận đúng giá trị, không sinh thêm tham số ngoài ý muốn

#### Scenario: Draft versus applied criteria
- **WHEN** sửa bộ lọc nhưng chưa submit
- **THEN** kết quả vẫn ghi nhận truy vấn đã áp dụng; submit hợp lệ bắt đầu trang 1, bỏ q rỗng và isActive ở lựa chọn Tất cả

#### Scenario: Search by brand
- **WHEN** người dùng nhập `logitech` vào ô tìm kiếm và submit
- **THEN** client gửi `q=logitech` và không gửi `brand`; danh sách gồm các sản phẩm có hãng Logitech

#### Scenario: Pick spec conditions from stored values
- **WHEN** người dùng mở lọc thông số, chọn `ram_gb` = `16` và `bluetooth` = `5.2` (giá trị chữ) rồi submit
- **THEN** client gửi `spec=ram_gb:16` và `spec=bluetooth:"5.2"` đúng như `filterValue` của API

#### Scenario: Suggestions follow the category
- **WHEN** người dùng đổi danh mục trong bộ lọc
- **THEN** danh sách tên thông số được tải lại cho danh mục đó; nếu tải lỗi thì hiện thông báo kèm nút Tải lại và tìm kiếm khác vẫn dùng được

#### Scenario: Invalid filter input
- **WHEN** một dòng đã chọn tên nhưng chưa chọn giá trị, hoặc hai dòng cùng tên
- **THEN** bộ lọc thông số mở ra và hiển thị lỗi, không gửi truy vấn; dòng chưa chọn tên được bỏ qua; lỗi validation từ API vẫn được xử lý nếu server từ chối
