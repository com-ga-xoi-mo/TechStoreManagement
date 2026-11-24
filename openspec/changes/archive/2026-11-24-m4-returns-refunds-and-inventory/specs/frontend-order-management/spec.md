## ADDED Requirements

### Requirement: Màn hình trả hàng và lịch sử theo vai trò

Client SHALL cung cấp màn hình trả hàng riêng cho Sales, Admin và Manager, hiển thị số lượng và tiền còn được hoàn từ server, trạng thái chờ/đã chi/chờ đối soát và lịch sử từng lần. Warehouse SHALL có màn hình kiểm định phần hàng chờ theo số lượng hoặc IMEI. Quyền trên giao diện phản ánh quyền server.

#### Scenario: Sales gửi và tra cứu
- **WHEN** Sales chọn dòng bán và lý do trả
- **THEN** màn hình hiển thị kết quả server hoặc lỗi đúng trường, cho xem lịch sử đơn của mình và ẩn hành động chi tiền.

#### Scenario: Mất phản hồi lúc duyệt
- **WHEN** Admin hoặc Manager mất kết nối sau khi gửi lệnh chi
- **THEN** màn hình tra lại kết quả bằng mã cũ trước khi cho thử lại, hiển thị trạng thái đối soát nếu ngân hàng chưa có kết luận.

#### Scenario: Kiểm định tiếp phần còn lại
- **WHEN** Warehouse mở lại lô đã nhập kho một phần
- **THEN** màn hình hiển thị riêng lượng còn chờ và kết quả từng phần đã xử lý.

#### Scenario: Chọn máy có IMEI và lô hàng theo số lượng
- **WHEN** Warehouse chọn một dòng kiểm định
- **THEN** màn hình hiển thị SKU gốc khi có. Với máy theo serial/IMEI, giao diện hiển thị mã thiết bị và cố định lượng kiểm định là 1. Với lô hàng, người dùng nhập được từ 1 đến lượng còn chờ.

#### Scenario: Hai biến thể cùng tên và giá
- **WHEN** Sales chọn dòng bán để trả hoặc quản lý xem lại lịch sử
- **THEN** màn hình hiển thị SKU và tên biến thể từ dòng bán gốc khi có dữ liệu.

#### Scenario: Tải lại danh sách kiểm định sau lỗi
- **WHEN** lần tải đầu bị lỗi kết nối
- **THEN** trang báo lỗi và cho tải lại. Thông báo danh sách trống chỉ xuất hiện sau khi API trả thành công với 0 dòng.
