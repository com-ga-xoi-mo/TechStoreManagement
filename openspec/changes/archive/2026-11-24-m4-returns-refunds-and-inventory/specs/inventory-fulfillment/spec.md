## ADDED Requirements

### Requirement: Giải phóng giữ chỗ đúng chủ thể

Inventory SHALL giải phóng đúng phần hàng đang giữ cho giao dịch được chỉ định trong UnitOfWork của bên gọi. Khóa IMEI rồi khóa tồn theo thứ tự hiện hành, xử lý lặp theo định danh nghiệp vụ.

#### Scenario: Hủy giữ chỗ hai lần
- **WHEN** Đạt gọi giải phóng lại cùng một lượt giữ QR hoặc An thử lại hoàn đơn chưa giao
- **THEN** Reserved trở về InStock và reserved_quantity giảm đúng một lần, quantity giữ nguyên, không phát sinh movement nhập hàng hay giải phóng phần của đơn khác.

### Requirement: Nhận và kiểm định từng phần hàng trả

Inventory SHALL nhận đúng IMEI đã bán vào Returned. Hàng đã nhận chờ kiểm định chưa khả dụng. Khi Warehouse phân loại, chỉ phần bán lại được mới cộng tồn, ghi CustomerReturn, thời điểm thực nhập và IsRestocked cho phần tương ứng trong một giao dịch.

#### Scenario: Lô trả có nhiều kết quả
- **WHEN** kho nhận ba đơn vị rồi xác nhận một đơn vị bán lại được, một lỗi và một cần sửa
- **THEN** tồn tăng một, chỉ phần được nhập có movement và đánh dấu nhập lại, hai phần còn lại ở trạng thái Defective hoặc UnderRepair. Gửi lại cùng phiếu kiểm định không tăng tồn.

### Requirement: Thao tác thiết bị cho bảo hành

Inventory SHALL nhận/trả máy sửa và xuất máy thay theo quyết định của luồng bảo hành trong transaction của Q. Vinh. Số tồn vật lý cho hàng serial bằng số IMEI InStock + Reserved sau mỗi lần commit.

#### Scenario: Nhận máy sửa rồi trả khách
- **WHEN** máy Sold được nhận sửa và trả đúng khách
- **THEN** IMEI đi qua UnderRepair rồi về Sold, giữ hạn bảo hành cũ, không tăng tồn hay ghi bán/hoàn.

#### Scenario: Xuất máy thay
- **WHEN** hồ sơ được duyệt đổi và máy thay InStock khớp sản phẩm/biến thể
- **THEN** máy cũ thành Defective, máy thay thành Sold với hạn bảo hành còn lại, tồn giảm một, trả mã biến động và giá vốn máy thay cho hồ sơ liên kết hai IMEI với đơn gốc.

#### Scenario: Hai luồng tranh cùng thiết bị
- **WHEN** yêu cầu trả và yêu cầu bảo hành cùng nhận một IMEI, hoặc hai hồ sơ cùng chọn một máy thay
- **THEN** chỉ một giao dịch nhận được thiết bị, giao dịch xung đột không để lại thay đổi kho.

#### Scenario: Quyền nhận bảo hành theo dòng bán
- **WHEN** Admin hoặc Manager mở hồ sơ nhận một IMEI trong thời hạn bảo hành
- **THEN** hệ thống kiểm tra khách, dòng đã giao và thiết bị khách còn giữ. Hệ thống từ chối nhận lại thiết bị mà khách đã trả, kể cả khi đó là dòng trên chứng từ hoàn cũ.

#### Scenario: Tra cứu và thử lại quyết định bảo hành
- **WHEN** người quản lý gửi lại quyết định đã thực hiện với cùng mã hồ sơ và máy thay
- **THEN** nhận lại hồ sơ đã ghi, gồm hạn bảo hành gốc và mã biến động kho. Một quyết định khác trên cùng hồ sơ đã đóng trả 409.
