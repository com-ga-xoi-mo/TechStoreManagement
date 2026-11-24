# Spec Delta

## Purpose

Màn hình "Bảo hành" trên client để nhân viên tra cứu, tiếp nhận và theo dõi hồ sơ bảo hành, với nút thao tác theo trạng thái hồ sơ và role, chỉ hiển thị kết quả do server trả về.

## ADDED Requirements

### Requirement: Danh sách hồ sơ bảo hành

Trang "Bảo hành" SHALL hiển thị danh sách hồ sơ từ API với ô tìm kiếm, bộ lọc trạng thái và phân trang. Mỗi dòng SHALL hiện mã hồ sơ, sản phẩm, IMEI/Serial, khách hoặc "Khách lẻ", trạng thái bằng nhãn tiếng Việt và ngày tiếp nhận. Trang SHALL có các trạng thái Loading, Empty, Content và Error, có nút thử lại.

#### Scenario: Mở trang
- **WHEN** Thủ kho mở mục Bảo hành
- **THEN** danh sách hồ sơ mới nhất hiển thị với nhãn trạng thái như "Chờ kiểm định", "Đang sửa", "Chờ duyệt đổi", "Đã sửa, chờ trả", "Từ chối bảo hành", "Đã trả khách", "Đã đổi máy"

#### Scenario: Lỗi mạng
- **WHEN** tải danh sách thất bại do mất kết nối
- **THEN** trang hiện lỗi an toàn và nút thử lại, không hiện dữ liệu cũ của phiên khác

### Requirement: Hộp thoại tiếp nhận

Nút "Tiếp nhận" SHALL chỉ hiện cho Admin, Manager và SalesStaff, và mở hộp thoại hai bước:
1. Nhập IMEI/Serial, mã đơn hoặc chọn khách rồi kiểm tra; lý do không đủ điều kiện hiện bằng tiếng Việt từ mã lý do của server.
2. Khi đủ điều kiện, nhập lỗi khách báo, tên và số điện thoại liên hệ (điền sẵn từ khách nếu có, sửa được).

Mỗi lần mở hộp thoại, client SHALL tạo một `caseId` mới và giữ nó tới khi tiếp nhận xong hoặc đóng hộp thoại.

#### Scenario: Máy không đủ điều kiện
- **WHEN** tra một máy đã hết hạn
- **THEN** hộp thoại hiện "Đã hết hạn bảo hành" kèm hạn bảo hành và không cho sang bước nhập thông tin

#### Scenario: Gửi lại sau lỗi mạng
- **WHEN** gửi tiếp nhận bị lỗi mạng và người dùng bấm gửi lại
- **THEN** client gửi lại cùng `caseId` đã tạo cho lần tiếp nhận đó, nên server trả hồ sơ đã có và không tạo hồ sơ thứ hai

#### Scenario: Tiếp nhận xong
- **WHEN** server trả 201 (hoặc 200 khi là lần gửi lại)
- **THEN** hộp thoại đóng, danh sách làm mới và hồ sơ mới nằm đầu danh sách

### Requirement: Hộp thoại chi tiết hồ sơ

Chi tiết hồ sơ SHALL hiện thông tin máy, đơn gốc, hạn bảo hành, liên hệ, kết luận kiểm định, máy thay (nếu có) và lịch sử theo thứ tự thời gian. Giá vốn máy thay SHALL chỉ hiện khi server trả giá trị.

#### Scenario: Xem hồ sơ đã đổi máy
- **WHEN** Manager mở hồ sơ `Replaced`
- **THEN** hộp thoại hiện IMEI máy cũ, IMEI máy thay, giá vốn máy thay và người duyệt

#### Scenario: Sales xem hồ sơ đã đổi máy
- **WHEN** SalesStaff mở cùng hồ sơ đó
- **THEN** hộp thoại hiện IMEI máy thay nhưng không có dòng giá vốn

### Requirement: Nút thao tác theo trạng thái và role

Chi tiết hồ sơ SHALL chỉ hiện nút mà cả trạng thái lẫn role cho phép:
- kiểm định khi `Received` (A W);
- báo sửa xong khi `Repairing` (A W);
- duyệt hoặc từ chối đổi khi `PendingReplacement` (A M);
- trả máy khi `Repaired`/`Rejected` (A M S).

Kết quả hiển thị SHALL lấy từ phản hồi server. Lỗi 409 SHALL hiện thông báo server và tải lại hồ sơ.

#### Scenario: Thủ kho xem hồ sơ chờ duyệt
- **WHEN** WarehouseStaff mở hồ sơ `PendingReplacement`
- **THEN** không có nút thao tác nào và lịch sử vẫn hiện đủ

#### Scenario: Người khác đã thao tác trước
- **WHEN** Manager bấm duyệt nhưng server trả 409 vì hồ sơ đã `Replaced`
- **THEN** hộp thoại hiện thông báo của server và tải lại hồ sơ ở trạng thái mới

### Requirement: Chọn máy thay

Khi duyệt đổi, client SHALL chỉ cho chọn trong danh sách máy `InStock` cùng sản phẩm và biến thể với máy cũ, lấy từ API Serial/IMEI hiện có. Nếu không có máy nào, client SHALL báo "Không còn máy cùng loại trong kho" và vẫn cho phép từ chối đổi.

#### Scenario: Hết máy cùng loại
- **WHEN** Manager mở bước duyệt cho một hồ sơ mà kho không còn máy cùng biến thể
- **THEN** nút duyệt bị tắt, có thông báo hết hàng và nút từ chối đổi vẫn dùng được
