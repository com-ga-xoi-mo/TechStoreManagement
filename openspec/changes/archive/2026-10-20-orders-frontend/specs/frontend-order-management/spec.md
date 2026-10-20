## ADDED Requirements

### Requirement: Live order list
Client SHALL lấy danh sách đơn từ `GET /api/orders` và chi tiết từ `GET /api/orders/{id}`. Màn hình Đơn hàng MUST NOT thay dữ liệu thật bằng fixture hoặc hiển thị rỗng như một lần tải thành công khi API lỗi.

#### Scenario: Open orders
- **WHEN** người dùng mở mục Đơn hàng và API khả dụng
- **THEN** trang hiển thị các đơn của trang 1 cùng tổng số đơn và số trang từ envelope của server; chi tiết từng đơn chưa được gọi

#### Scenario: API unavailable
- **WHEN** API không kết nối được hoặc trả lỗi khi mở mục Đơn hàng
- **THEN** trang báo lỗi có nút thử lại, không hiển thị trạng thái "chưa có đơn hàng"

#### Scenario: No orders yet
- **WHEN** API trả danh sách rỗng với `totalCount` bằng 0
- **THEN** trang hiển thị trạng thái rỗng kèm lối vào "Lập đơn mới", phân biệt với lỗi tải

### Requirement: Submitted list criteria
Client SHALL hỗ trợ lọc theo trạng thái (Tất cả hoặc một trong sáu trạng thái), từ khóa `q` (mã đơn, tên hoặc số điện thoại khách), sắp xếp `createdAt` hoặc `totalAmount` theo `asc`/`desc`, kích thước trang 20/50/100. Mặc định `createdAt:desc`, trang 1, kích thước 20. Từ khóa SHALL chỉ áp dụng khi người dùng bấm tìm hoặc nhấn Enter; đổi trạng thái, sắp xếp hoặc kích thước trang SHALL áp dụng ngay và quay về trang 1 mà vẫn giữ từ khóa đã áp dụng.

#### Scenario: Draft versus applied criteria
- **WHEN** người dùng gõ vào ô tìm kiếm nhưng chưa gửi
- **THEN** danh sách vẫn thuộc từ khóa đã áp dụng; khi gửi, từ khóa mới được áp dụng từ trang 1 và `q` rỗng bị bỏ khỏi truy vấn

#### Scenario: Selection controls apply at once
- **WHEN** người dùng chọn một trạng thái, thứ tự sắp xếp hoặc kích thước trang khác
- **THEN** client tải lại từ trang 1 với lựa chọn mới và từ khóa đã áp dụng trước đó, bỏ qua chữ đang gõ chưa gửi

#### Scenario: Keyword with reserved characters
- **WHEN** từ khóa chứa dấu cách, chữ có dấu, `&`, `+`, `%` hoặc `#`
- **THEN** giá trị được mã hóa riêng trong query string và API nhận đúng từ khóa

#### Scenario: Newer search replaces older one
- **WHEN** người dùng gửi tiêu chí mới trong lúc yêu cầu trước chưa trả về
- **THEN** yêu cầu trước bị hủy và chỉ kết quả của yêu cầu mới nhất được hiển thị

#### Scenario: Page beyond the end
- **WHEN** trang yêu cầu lớn hơn `totalPages` (ví dụ sau khi hủy đơn cuối của trang)
- **THEN** client quay về trang 1 đúng một lần, không lặp yêu cầu

### Requirement: Faithful order data
Client SHALL giữ nguyên mã đơn, trạng thái, phương thức thanh toán, tên khách, số dòng, các mốc thời gian và mọi số tiền theo API. Tiền SHALL được giữ bằng `decimal` và hiển thị theo định dạng Việt Nam (ví dụ `15.500.000 ₫`). Đơn không có khách SHALL hiển thị "Khách lẻ". Giá vốn MUST NOT được lưu hay hiển thị.

#### Scenario: Status and payment labels
- **WHEN** đơn có trạng thái `Pending`, `Processing`, `Completed`, `Cancelled`, `PartiallyRefunded` hoặc `Refunded`
- **THEN** mỗi trạng thái có nhãn tiếng Việt và màu `StatusBadge` riêng, và phương thức `Cash`, `VietQr`, `Card` có nhãn tương ứng

#### Scenario: Unknown enum value
- **WHEN** API trả trạng thái hoặc phương thức thanh toán không thuộc tập đã biết
- **THEN** repository báo lỗi phản hồi không hợp lệ thay vì tự đoán giá trị

#### Scenario: Malformed order payload
- **WHEN** thiếu trường bắt buộc, id rỗng, số tiền âm hoặc `totalPages` không khớp `totalCount` và `pageSize`
- **THEN** repository báo lỗi phản hồi không hợp lệ và danh sách không hiển thị dữ liệu sai

### Requirement: Status actions follow the order lifecycle
Mỗi hàng đơn SHALL chỉ hiển thị hành động hợp lệ với trạng thái hiện tại: `Pending` có Xác nhận thanh toán (→ `Processing`), Hoàn tất (→ `Completed`) và Hủy đơn (→ `Cancelled`); `Processing` chỉ có Hoàn tất; các trạng thái còn lại không có hành động. Mọi hành động SHALL được xác nhận qua hộp thoại trước khi gửi `PATCH /api/orders/{id}/status`, và server vẫn là nơi quyết định tính hợp lệ.

#### Scenario: Confirmed transition
- **WHEN** người dùng xác nhận một hành động hợp lệ
- **THEN** client gửi `targetStatus` tương ứng, rồi tải lại trang hiện tại và hàng hiển thị trạng thái mới

#### Scenario: Declined confirmation
- **WHEN** người dùng đóng hộp thoại xác nhận mà không đồng ý
- **THEN** không có yêu cầu nào được gửi và đơn giữ nguyên

#### Scenario: Server rejects the transition
- **WHEN** server trả 409 (đơn đã đổi trạng thái hoặc thiết bị không còn khả dụng)
- **THEN** client hiển thị lý do `detail` của server, không tự thử lại và tải lại danh sách để phản ánh trạng thái thật

#### Scenario: Order no longer exists
- **WHEN** server trả 404 cho thao tác đổi trạng thái
- **THEN** client thông báo đơn không còn tồn tại và tải lại danh sách

### Requirement: Read-only order detail dialog
Người dùng SHALL xem chi tiết một đơn trong hộp thoại chỉ đọc gồm mã đơn, trạng thái, thanh toán, khách, ghi chú, các mốc tạo/thanh toán/hoàn tất, từng dòng hàng (tên sản phẩm, biến thể, SKU, đơn giá, số lượng, giảm giá, thành tiền) kèm IMEI hoặc serial của máy đã chọn, và tổng tiền. Hộp thoại MUST NOT chứa hành động đổi trạng thái.

#### Scenario: Order with serial-tracked line
- **WHEN** người dùng mở chi tiết đơn có dòng máy iPhone
- **THEN** dòng đó hiển thị IMEI của máy; dòng không theo dõi IMEI không hiển thị trường IMEI trống giả

#### Scenario: Detail load fails
- **WHEN** tải chi tiết gặp lỗi hoặc đơn không còn tồn tại
- **THEN** danh sách hiển thị thông báo lỗi, không mở hộp thoại rỗng

#### Scenario: Another dialog is open
- **WHEN** đang có một hộp thoại khác mở
- **THEN** yêu cầu mở chi tiết bị bỏ qua và không làm hỏng hộp thoại đang mở

### Requirement: Building an order draft
Màn hình lập đơn SHALL cho tìm sản phẩm đang hoạt động, chọn biến thể đang hoạt động khi sản phẩm có biến thể, và thêm dòng vào đơn. Sản phẩm theo dõi IMEI SHALL yêu cầu chọn đúng một máy `InStock` cho mỗi dòng với số lượng cố định 1, và một máy chỉ xuất hiện một lần trong đơn. Sản phẩm không theo dõi IMEI SHALL cho nhập số lượng nguyên dương. Giảm giá dòng SHALL nằm trong khoảng từ 0 đến thành tiền của dòng. Tổng tiền SHALL được tính lại tức thì mỗi khi dòng thay đổi và gắn nhãn là tạm tính.

#### Scenario: Add a serial-tracked product
- **WHEN** người dùng chọn một sản phẩm theo dõi IMEI và một biến thể
- **THEN** client tải các máy `InStock` của sản phẩm và biến thể đó, người dùng chọn một máy, và dòng được thêm với đơn giá biến thể và số lượng 1

#### Scenario: Device already used in the draft
- **WHEN** người dùng chọn lại máy đã có ở một dòng khác trong đơn
- **THEN** client từ chối thêm và nêu lý do tại dòng, không thêm bản sao

#### Scenario: No devices in stock
- **WHEN** sản phẩm theo dõi IMEI không còn máy `InStock`
- **THEN** client báo không còn máy khả dụng và không cho thêm dòng

#### Scenario: Estimated total
- **WHEN** đơn có hai dòng, một dòng 2 × 500.000 ₫ giảm 100.000 ₫ và một dòng 1 × 30.000.000 ₫
- **THEN** tổng tạm tính hiển thị 30.900.000 ₫

#### Scenario: Invalid discount or quantity
- **WHEN** người dùng nhập giảm giá âm, lớn hơn thành tiền dòng, hoặc số lượng bằng 0, lớn hơn 10.000 hay không phải số nguyên
- **THEN** client báo lỗi ngay tại khu vực thêm dòng và không thêm dòng vào đơn

#### Scenario: Same product added twice
- **WHEN** người dùng thêm lại cùng một sản phẩm và biến thể không theo dõi IMEI
- **THEN** client gộp vào dòng đã có bằng cách cộng số lượng và giảm giá, và từ chối nếu tổng vượt giới hạn số lượng hoặc giảm giá vượt thành tiền

#### Scenario: Inactive product or variant
- **WHEN** sản phẩm đã ngừng kinh doanh, không còn tồn tại, hoặc mọi biến thể đều ngừng hoạt động
- **THEN** client không cho chọn sản phẩm đó và nêu lý do

### Requirement: Submitting an order
Màn hình lập đơn SHALL có hai hành động. "Lưu đơn nháp" gửi `POST /api/orders` và dừng ở trạng thái `Pending`. "Thanh toán & hoàn tất" gửi `POST` rồi `PATCH` sang `Completed`. Đơn rỗng SHALL không được gửi. Client MUST NOT tự gửi lại yêu cầu ghi, và SHALL ngăn gửi trùng khi một đơn đã được tạo.

#### Scenario: Save as draft
- **WHEN** người dùng bấm "Lưu đơn nháp" với đơn hợp lệ
- **THEN** server tạo đơn `Pending`, client thông báo mã đơn và quay về danh sách có đơn mới

#### Scenario: Pay and complete
- **WHEN** người dùng bấm "Thanh toán & hoàn tất" với đơn hợp lệ và server chấp nhận cả hai bước
- **THEN** đơn kết thúc ở trạng thái `Completed` và client quay về danh sách

#### Scenario: Complete step fails after create
- **WHEN** `POST` thành công nhưng bước chuyển sang `Completed` bị server từ chối
- **THEN** client giữ nguyên trang, hiển thị mã đơn đã tạo cùng lý do, khóa nút gửi để không tạo đơn thứ hai và cho quay về danh sách

#### Scenario: Server validation errors
- **WHEN** server trả 400 với lỗi dưới `items[1].serialImeiId` và lỗi `customerId`
- **THEN** lỗi thứ nhất hiển thị tại dòng thứ hai của đơn, lỗi còn lại hiển thị ở khu vực lỗi chung, đơn nháp của người dùng được giữ nguyên

#### Scenario: Device taken meanwhile
- **WHEN** máy đã chọn bị đơn khác chốt trước khi gửi và server trả lỗi 400 hoặc 409
- **THEN** client hiển thị lý do và cho chọn máy khác mà không mất các dòng còn lại

### Requirement: Orders views are C# Markup
`OrderListPage`, `OrderCreatePage` và `OrderDetailDialog` SHALL được dựng bằng C# Markup, màu chủ đề đặt trực tiếp lên phần tử bằng token `AppColors`, và logic trạng thái nằm trong ViewModel hoặc helper thuần. Các tệp `OrderListPage.xaml` và `OrderListPage.xaml.cs` SHALL bị xóa.

#### Scenario: Theme switch at runtime
- **WHEN** người dùng đổi giao diện Sáng/Tối khi đang mở danh sách đơn, trang lập đơn hoặc hộp thoại chi tiết
- **THEN** chữ, panel, nút và badge vẫn đọc được trong cả hai chủ đề

#### Scenario: No legacy placeholder
- **WHEN** mã nguồn client được biên dịch
- **THEN** không còn `OrderListPage.xaml` hay `OrderListPage.xaml.cs`, và mục Đơn hàng vẫn mở được qua điều hướng của Shell
