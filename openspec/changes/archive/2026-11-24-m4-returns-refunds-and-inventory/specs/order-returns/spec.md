## Purpose

Quản lý yêu cầu trả hàng và ghi nhận khoản thực chi để nhân viên, kho và báo cáo đối chiếu được từng lần hoàn với đơn gốc.

## ADDED Requirements

### Requirement: Yêu cầu trả theo phạm vi đơn

API SHALL cho SalesStaff gửi/xem yêu cầu trên đơn mình bán và cho Admin/Manager tra cứu, xử lý hoàn. Yêu cầu phải có mã chống lặp, lý do, dòng bán, số lượng dương và đúng IMEI của dòng có serial. Phạm vi đọc lịch sử tuân theo quyền đọc đơn. WarehouseStaff chỉ truy cập dữ liệu nghiệp vụ kho cần để kiểm định.

#### Scenario: Yêu cầu đang chờ
- **WHEN** Sales gửi yêu cầu hợp lệ trên đơn của mình
- **THEN** lịch sử có mã yêu cầu và trạng thái chờ, tiền thực chi và số dư kho chưa đổi.

#### Scenario: Sai quyền hoặc dữ liệu
- **WHEN** Sales truy cập đơn người khác, gọi API duyệt chi, hoặc gửi sai IMEI trên đơn của mình
- **THEN** server trả lần lượt 404, 403 hoặc lỗi theo dòng và không phát sinh khoản chi.

### Requirement: Số tiền và trạng thái xuất phát từ giao dịch gốc

Server SHALL hoàn phần còn lại của đơn Completed/PartiallyRefunded theo số lượng và phân bổ tiền hàng, thuế, giảm giá đã lưu. Mỗi thành phần làm tròn hai chữ số, lần cuối nhận toàn bộ phần dư. Server SHALL giữ nguyên dòng bán, tổng gốc, PaidAt và CompletedAt, đổi trạng thái theo phần đã hoàn và gọi nghiệp vụ đảo điểm theo mã lần hoàn.

#### Scenario: Trả hai lần
- **WHEN** khách trả lần lượt từng món của đơn hai món giá 1.000.000đ/món, giảm tổng 200.000đ, thuế 10%, đã cộng 18 điểm
- **THEN** mỗi lần hoàn 900.000đ tiền hàng và 90.000đ thuế, đảo 9 điểm, trạng thái lần lượt là PartiallyRefunded và Refunded.

#### Scenario: Đã trả tiền nhưng chưa giao
- **WHEN** đơn Processing được yêu cầu hoàn một phần rồi được yêu cầu hoàn toàn bộ
- **THEN** yêu cầu một phần bị từ chối, hoàn toàn bộ thành công giải phóng đúng lượng giữ, giữ nguyên tồn vật lý và điểm, đơn chuyển Refunded.

#### Scenario: Tranh phần còn lại
- **WHEN** hai yêu cầu cùng đòi đơn vị cuối hoặc giao hàng và hoàn toàn bộ đồng thời trên đơn Processing
- **THEN** chỉ thao tác còn hợp lệ tại thời điểm chốt được thực hiện, không chi vượt tiền, hoàn vượt lượng hoặc giải phóng hàng đã giao.

### Requirement: Chi tiền đúng một lần và đối soát được

Chỉ Admin/Manager SHALL được chốt chi. Cash cần ca đang mở của chính người hoàn. Hoàn ngân hàng cần mã giao dịch có thể kiểm chứng. Mỗi lần hoàn SHALL có một định danh dùng xuyên suốt chi tiền, ghi sổ, đảo điểm và thử lại. Chỉ giao dịch đã xác nhận chi mới tạo OrderReturn.

#### Scenario: Đóng ca hoặc lỗi khi chốt
- **WHEN** ca chi đã đóng hoặc một bước ghi chi, kho, điểm hay lưu đơn thất bại trong giao dịch Cash
- **THEN** mọi thay đổi của lần chốt được rollback, không ghi một khoản hoàn thành công.

#### Scenario: Ngân hàng thành công, lưu kết quả thất bại
- **WHEN** ngân hàng đã chuyển tiền nhưng hệ thống mất phản hồi hoặc chưa lưu được kết quả
- **THEN** giữ yêu cầu và phần tiền/hàng liên quan chờ đối soát, dùng cùng mã tra cứu để hoàn tất đúng một lần, không phát lệnh chi mới khi kết quả còn chưa rõ.

#### Scenario: Gửi lại mã lần hoàn
- **WHEN** người dùng lặp lại cùng thao tác thành công với cùng mã
- **THEN** nhận lại kết quả cũ, không ghi thêm chi, điểm, hàng trả hay biến động kho. Cùng mã với dữ liệu khác bị từ chối.

### Requirement: Dữ liệu bàn giao và liên kết đổi hàng

Hệ thống SHALL cung cấp nội bộ cho báo cáo mã lần hoàn, dòng gốc, số lượng, tiền hàng sau giảm, phần giảm, thuế, kênh chi, RefundedAt, và từng lần thực nhập theo thời điểm, số lượng, giá vốn gốc có thể thiếu. Đổi thương mại SHALL liên kết lần hoàn với đơn mới qua checkout.

#### Scenario: Nhập kho khác ngày chi tiền
- **WHEN** hoàn ngày thứ nhất và chỉ nhập lại một phần vào ngày thứ hai
- **THEN** báo cáo đọc được khoản hoàn ở ngày thứ nhất và phần vốn nhập lại ở ngày thứ hai theo đúng số lượng thực nhập.

#### Scenario: Đổi sang sản phẩm mới
- **WHEN** checkout đơn thay thế thành công
- **THEN** tra cứu được đơn mới từ lần hoàn, đơn gốc giữ các dòng bán ban đầu.

#### Scenario: Đối soát chứng từ trước và sau khi duyệt chi
- **WHEN** quản lý xác nhận đã chuyển tiền
- **THEN** hệ thống chỉ ghi khoản chi khi thời điểm trên chứng từ từ lúc duyệt chuyển khoản đến hiện tại. Duyệt lại giữ nguyên mốc đã lưu. Với phiếu cũ còn chờ và thiếu thời điểm duyệt, quản lý phải cung cấp mốc đã kiểm tra từ chứng từ duyệt gốc.
