# warranty-cases Specification

## Purpose

Quản lý bảo hành máy đã bán theo Serial/IMEI qua `/api/warranties`: tra cứu điều kiện, tiếp nhận, kiểm định, sửa, đổi máy, trả máy, lịch sử và chi phí máy thay, mà không tạo doanh thu, điểm hay giao dịch hoàn tiền.

## Requirements

### Requirement: Tra cứu máy theo IMEI hoặc Serial

`GET /api/warranties/lookup` SHALL nhận `code` (IMEI hoặc Serial, bắt buộc), `orderCode` và `customerId` (không bắt buộc). Kết quả SHALL luôn có thông tin máy, hạn bảo hành, `eligible` và mã lý do. Mã đơn, ngày mua, khách hoặc "khách lẻ" và lịch sử hồ sơ SHALL chỉ có khi đã chứng minh được lần bán. Tra cứu SHALL NOT thay đổi dữ liệu. Không thấy máy trả 404, `code` trống trả 400.

#### Scenario: Số điện thoại trong tra cứu theo vai trò
- **WHEN** các vai trò tra cùng máy và chứng minh được lần bán của khách có số điện thoại
- **THEN** Admin, Manager và SalesStaff nhận số trong `customerPhone`; WarehouseStaff nhận `customerPhone = null`

#### Scenario: Máy đủ điều kiện
- **WHEN** nhân viên tra IMEI của máy `Sold`, còn hạn, thuộc đơn `Completed` của khách K và gửi `customerId = K`
- **THEN** API trả 200, `eligible = true`, kèm mã đơn, ngày mua, tên khách và hạn bảo hành

#### Scenario: Chưa chứng minh lần bán thì không lộ mã đơn
- **WHEN** nhân viên chỉ nhập IMEI của máy thuộc đơn khách lẻ, không gửi `orderCode`
- **THEN** API trả 200 với thông tin máy, hạn bảo hành, `eligible = false`, lý do `OrderCodeRequired`; mã đơn, ngày mua, tên khách và lịch sử đều trống

#### Scenario: Mã không tồn tại
- **WHEN** tra một IMEI không có trong hệ thống
- **THEN** API trả 404 và không ghi dữ liệu

### Requirement: Xác định lần bán đang bảo hành

Lần bán đang bảo hành SHALL là dòng đơn mà máy đang thuộc về theo `serial_imeis.sale_order_item_id`. Dòng đơn này được ghi khi bán và khi máy được xuất làm máy thay, và trỏ tới dòng đơn gốc. Khi cột trống (máy bán từ M3), lần bán SHALL là dòng đơn giao gần nhất theo `completed_at`. Đơn của lần bán SHALL ở `Completed` hoặc `PartiallyRefunded`, nếu không thì lý do là `NoCoveringSale`.

#### Scenario: Máy đã được bán lại
- **WHEN** một IMEI từng bán cho khách X, được trả, nhập lại và bán cho khách Y
- **THEN** tra cứu trả về đơn của khách Y

#### Scenario: Máy thay được tra theo đơn gốc
- **WHEN** máy 333 từng bán cho khách Z, được trả và nhập lại, rồi được xuất làm máy thay cho hồ sơ của khách Lan
- **THEN** tra cứu 333 trả về đơn gốc và khách Lan, cùng hạn bảo hành máy 333 đã nhận

#### Scenario: Máy bán từ M3
- **WHEN** máy `Sold` có `sale_order_item_id` trống và nằm trong một đơn đã hủy và một đơn đã giao sau đó
- **THEN** tra cứu trả về đơn đã giao

### Requirement: Điều kiện bảo hành

Máy SHALL đủ điều kiện khi đồng thời thỏa:
- đang `Sold` và có lần bán đang bảo hành;
- `warranty_start_at <= lúc kiểm tra < warranty_end_at`;
- dòng đơn chưa trả hết và không có yêu cầu trả đang chặn;
- không có hồ sơ mở;
- đã chứng minh lần bán.

Mỗi điều kiện không đạt SHALL có mã lý do riêng (`NotSold`, `NoCoveringSale`, `Expired`, `LineReturned`, `PendingReturn`, `OpenCase`, `OrderCodeRequired`, `OrderMismatch`, `CustomerMismatch`).

#### Scenario: Hết hạn bảo hành
- **WHEN** tra máy có `warranty_end_at` đúng bằng thời điểm hiện tại
- **THEN** `eligible = false` với lý do `Expired`

#### Scenario: Dòng hàng đã trả
- **WHEN** toàn bộ số lượng của dòng đơn đã có bản ghi trả hàng
- **THEN** `eligible = false` với lý do `LineReturned`

#### Scenario: Đang có yêu cầu trả hàng
- **WHEN** dòng đơn có yêu cầu trả hàng `Pending` chưa chi tiền, hoặc `AwaitingBank` chờ chuyển khoản
- **THEN** `eligible = false` với lý do `PendingReturn`

#### Scenario: Phiếu trả đang chờ của máy cũ sau khi đổi
- **WHEN** thiết bị là máy thay đã xác nhận qua hồ sơ `Replaced` của chính dòng bán và dòng còn yêu cầu `Pending` thuộc máy cũ
- **THEN** yêu cầu `Pending` không chặn bảo hành máy thay, yêu cầu `AwaitingBank` vẫn chặn

#### Scenario: Máy đang có hồ sơ mở
- **WHEN** tra máy đang `UnderRepair` thuộc một hồ sơ chưa đóng
- **THEN** `eligible = false` với lý do `OpenCase`

#### Scenario: Máy không thuộc khách
- **WHEN** tra máy đang `InStock`, `Reserved`, `Returned` hoặc `Defective`
- **THEN** `eligible = false` với lý do `NotSold`

### Requirement: Chứng minh lần bán

Với đơn có khách, người gửi SHALL cung cấp `customerId` trùng khách của đơn hoặc `orderCode` trùng mã đơn. Với đơn khách lẻ, người gửi SHALL cung cấp `orderCode` trùng mã đơn. So khớp mã đơn SHALL không phân biệt hoa thường và bỏ khoảng trắng hai đầu.

#### Scenario: Khách lẻ thiếu mã đơn
- **WHEN** máy thuộc đơn khách lẻ và không có `orderCode`
- **THEN** lý do `OrderCodeRequired`

#### Scenario: Mã đơn không khớp
- **WHEN** gửi `orderCode` khác mã đơn của lần bán
- **THEN** lý do `OrderMismatch`

#### Scenario: Sai khách
- **WHEN** máy thuộc đơn của khách K, gửi `customerId` của khách khác và không gửi `orderCode`
- **THEN** lý do `CustomerMismatch`

#### Scenario: Khách có hồ sơ chỉ cần chọn khách
- **WHEN** máy thuộc đơn của khách K và chỉ gửi `customerId = K`
- **THEN** điều kiện chứng minh lần bán đạt

### Requirement: Tiếp nhận bảo hành

`POST /api/warranties/{caseId}/receive` SHALL nhận `code`, `orderCode`, `customerId`, `issueDescription` (≤1000), `contactName` (≤100) và `contactPhone` (≤20); ba trường cuối bắt buộc. `caseId` do client tạo. Khi máy đủ điều kiện, server SHALL trong một transaction tạo hồ sơ `Received` có mã `BH-yyyyMM-NNNN`, chụp lại hạn bảo hành, chuyển máy `Sold → UnderRepair` qua thao tác kho, ghi sự kiện đầu tiên và trả 201.

#### Scenario: Tiếp nhận thành công
- **WHEN** Sales gửi yêu cầu hợp lệ cho máy đủ điều kiện
- **THEN** API trả 201 với hồ sơ `Received`; máy `UnderRepair`; tồn kho, đơn hàng và điểm khách không đổi

#### Scenario: Thiếu thông tin liên hệ
- **WHEN** gửi yêu cầu thiếu `contactPhone`
- **THEN** API trả 400 với lỗi ở `contactPhone` và không ghi dữ liệu

#### Scenario: Máy không đủ điều kiện
- **WHEN** gửi yêu cầu cho máy đã hết hạn
- **THEN** API trả 409 `ProblemDetails` có `reasons` chứa `Expired`, và máy vẫn `Sold`

#### Scenario: Tiếp nhận mã thiết bị không tồn tại
- **WHEN** gửi yêu cầu hợp lệ với IMEI hoặc Serial chưa có trong hệ thống
- **THEN** API trả 404 và không ghi hồ sơ hay sự kiện

### Requirement: Chống tiếp nhận lặp

Gửi lại cùng `caseId` cho cùng máy SHALL trả 200 với hồ sơ đã tạo, không tạo hồ sơ thứ hai. Dùng lại `caseId` cho máy khác SHALL trả 409. Một máy SHALL có tối đa một hồ sơ mở (mọi trạng thái trừ `ReturnedToCustomer` và `Replaced`), kể cả khi hai yêu cầu với hai `caseId` khác nhau chạy đồng thời.

#### Scenario: Gửi lại do mạng lỗi
- **WHEN** client gửi lại tiếp nhận với cùng `caseId` và cùng IMEI
- **THEN** API trả 200 với đúng hồ sơ đã tạo; chỉ có một hồ sơ và một sự kiện tiếp nhận

#### Scenario: Hai quầy cùng tiếp nhận một máy
- **WHEN** hai giao dịch đồng thời tiếp nhận cùng một máy với hai `caseId` khác nhau
- **THEN** đúng một hồ sơ được tạo, yêu cầu còn lại nhận 409 và máy chỉ chuyển `UnderRepair` một lần

### Requirement: Kiểm định hồ sơ

`POST /api/warranties/{caseId}/inspect` SHALL nhận `outcome` (`Repair`, `Replace` hoặc `Reject`) và `note` (bắt buộc, ≤1000). Request chỉ hợp lệ khi hồ sơ đang `Received`. `Repair` chuyển hồ sơ sang `Repairing`, `Replace` sang `PendingReplacement`, `Reject` sang `Rejected` và bắt buộc `rejectionReason` (≤500). Máy SHALL vẫn ở `UnderRepair`.

#### Scenario: Kết luận sửa được
- **WHEN** Thủ kho kiểm định hồ sơ `Received` với `outcome = Repair`
- **THEN** hồ sơ chuyển `Repairing` và có sự kiện ghi kết luận

#### Scenario: Từ chối do lỗi người dùng
- **WHEN** kiểm định với `outcome = Reject` và lý do "Máy vào nước"
- **THEN** hồ sơ chuyển `Rejected` với lý do đó; máy vẫn `UnderRepair` chờ trả khách

#### Scenario: Từ chối thiếu lý do
- **WHEN** kiểm định với `outcome = Reject` mà không có `rejectionReason`
- **THEN** API trả 400 và hồ sơ không đổi

### Requirement: Báo sửa xong

`POST /api/warranties/{caseId}/repaired` SHALL chuyển hồ sơ `Repairing → Repaired`, nhận `note` (không bắt buộc, ≤1000). Máy SHALL vẫn ở `UnderRepair` cho tới khi trả khách.

#### Scenario: Sửa xong
- **WHEN** Thủ kho báo sửa xong một hồ sơ `Repairing`
- **THEN** hồ sơ chuyển `Repaired` và có sự kiện tương ứng

### Requirement: Duyệt đổi máy

`POST /api/warranties/{caseId}/replace` SHALL nhận `replacementDeviceId` và chỉ hợp lệ khi hồ sơ đang `PendingReplacement`. Server SHALL gọi thao tác đổi máy của kho rồi, trong cùng transaction, lưu máy thay, mã biến động kho, giá vốn máy thay (null nếu máy không có giá), người duyệt (`closed_by_id`), thời điểm đổi (`closed_at`), và chuyển hồ sơ sang `Replaced`.

#### Scenario: Duyệt đổi thành công
- **WHEN** Manager duyệt hồ sơ `PendingReplacement` với máy `InStock` cùng sản phẩm/biến thể
- **THEN** hồ sơ `Replaced`; máy thay `Sold` với cùng `warranty_months`, `warranty_start_at`, `warranty_end_at` như máy cũ; máy cũ `Defective`; tồn giảm 1; có đúng một biến động `WarrantyReplacement`

#### Scenario: Máy thay không hợp lệ
- **WHEN** máy thay khác biến thể, đã bán hoặc đang `Reserved`
- **THEN** API trả 409 và hồ sơ vẫn `PendingReplacement`, kho không đổi

#### Scenario: Duyệt từ trạng thái khác
- **WHEN** gọi duyệt đổi cho hồ sơ `Repairing`
- **THEN** API trả 409 và không có thay đổi

#### Scenario: Hai người duyệt cùng lúc
- **WHEN** hai yêu cầu duyệt cùng một hồ sơ chạy đồng thời với hai máy thay khác nhau
- **THEN** đúng một yêu cầu thành công, yêu cầu kia nhận 409; chỉ một máy thay rời kho và tồn chỉ giảm 1

#### Scenario: Máy thay không có giá vốn
- **WHEN** máy thay không có `unit_cost`
- **THEN** việc đổi vẫn thành công và giá vốn máy thay của hồ sơ để trống (không thay bằng 0)

### Requirement: Từ chối đổi máy

`POST /api/warranties/{caseId}/reject-replacement` SHALL bắt buộc `note` (≤1000) và chuyển hồ sơ `PendingReplacement → Repairing`. Kho và máy SHALL không đổi.

#### Scenario: Quản lý yêu cầu sửa thay vì đổi
- **WHEN** Admin từ chối đổi với ghi chú "Sửa được, thay màn hình"
- **THEN** hồ sơ quay về `Repairing`, có sự kiện ghi người từ chối và ghi chú; máy vẫn `UnderRepair`

### Requirement: Trả máy cho khách

`POST /api/warranties/{caseId}/return` SHALL chỉ hợp lệ khi hồ sơ đang `Repaired` hoặc `Rejected`. Server SHALL gọi thao tác trả máy của kho (`UnderRepair → Sold`, giữ hạn bảo hành, không đổi tồn), chuyển hồ sơ sang `ReturnedToCustomer` và ghi người, thời điểm đóng. Gửi lại khi hồ sơ đã `ReturnedToCustomer` SHALL trả 200 với hồ sơ đã ghi.

#### Scenario: Trả máy đã sửa
- **WHEN** Sales trả máy của hồ sơ `Repaired`
- **THEN** hồ sơ `ReturnedToCustomer`; máy `Sold` với hạn bảo hành như trước khi tiếp nhận

#### Scenario: Trả máy chưa sửa xong
- **WHEN** gọi trả máy cho hồ sơ `Repairing`
- **THEN** API trả 409 và hồ sơ, máy không đổi

#### Scenario: Bảo hành lại sau khi trả
- **WHEN** máy vừa được trả, còn hạn, được mang tới lần nữa
- **THEN** tiếp nhận tạo được hồ sơ mới vì hồ sơ cũ đã đóng

### Requirement: Chuyển trạng thái được bảo vệ

Mọi thao tác trên hồ sơ SHALL khóa hồ sơ trước khi kiểm tra trạng thái. Thao tác không hợp lệ với trạng thái hiện tại SHALL trả 409 kèm trạng thái hiện tại. Hồ sơ không tồn tại SHALL trả 404. Lỗi ở bất kỳ bước nào SHALL rollback toàn bộ thay đổi của hồ sơ, sự kiện, máy, tồn và biến động kho.

#### Scenario: Thao tác trên hồ sơ đã đóng
- **WHEN** gọi kiểm định cho hồ sơ `ReturnedToCustomer` hoặc `Replaced`
- **THEN** API trả 409 và không ghi sự kiện

#### Scenario: Thao tác kho thất bại
- **WHEN** thao tác tiếp nhận, đổi máy hoặc trả máy của kho trả conflict
- **THEN** API trả 409 với `detail` tiếng Việt tương ứng với thao tác, và rollback toàn bộ thay đổi

### Requirement: Lịch sử hồ sơ

Mỗi lần tạo hoặc đổi trạng thái hồ sơ SHALL ghi một sự kiện gồm trạng thái trước, trạng thái sau, người thao tác, ghi chú và thời điểm. Sự kiện SHALL chỉ được thêm, không sửa hoặc xóa qua API. Hồ sơ có từ trước change này SHALL có sự kiện tiếp nhận và sự kiện đóng (nếu đã đóng), sinh từ dữ liệu đã lưu.

#### Scenario: Vòng từ chối đổi máy được giữ lại
- **WHEN** hồ sơ đi `Received → PendingReplacement → Repairing → Repaired → ReturnedToCustomer`
- **THEN** chi tiết hồ sơ có năm sự kiện theo thứ tự thời gian, gồm người đã từ chối đổi

#### Scenario: Hồ sơ demo cũ
- **WHEN** mở chi tiết hồ sơ demo đã đóng do PR #31 tạo
- **THEN** hồ sơ có mã `BH-...` và hai sự kiện tiếp nhận, trả máy

### Requirement: Danh sách và chi tiết hồ sơ

`GET /api/warranties` SHALL trả danh sách phân trang (`page`, `pageSize` 1..100, mặc định 20), lọc theo `status` và `q` (mã hồ sơ, IMEI/Serial, mã đơn, số điện thoại liên hệ; tìm theo chữ, không coi `%`/`_` là ký tự đại diện), mới nhất trước. `GET /api/warranties/{caseId}` SHALL trả hồ sơ kèm lịch sử. Giá vốn máy thay SHALL theo quy tắc xem giá vốn trong `api-role-authorization`.

#### Scenario: Lọc hồ sơ chờ duyệt
- **WHEN** Manager lọc `status = PendingReplacement`
- **THEN** chỉ hồ sơ `PendingReplacement` được trả, kèm tổng số và số trang

#### Scenario: Tìm theo số điện thoại
- **WHEN** tìm `q = 0901` khi có hồ sơ có số liên hệ `0901234567`
- **THEN** hồ sơ đó có trong kết quả

### Requirement: Bảo hành không tạo doanh thu và không làm lệch tồn

Bảo hành SHALL NOT tạo đơn hàng, bản ghi trả hàng, thay đổi điểm khách hoặc tiền ca. Với hàng có Serial/IMEI, sau mọi thao tác bảo hành, `quantity` của dòng tồn SHALL bằng số máy `InStock` cộng `Reserved`; máy khách gửi sửa (`UnderRepair`) SHALL không được tính.

#### Scenario: Đối soát sau một vòng bảo hành
- **WHEN** chạy tiếp nhận, sửa, trả máy và một lần đổi máy trên PostgreSQL thật
- **THEN** câu SQL đối soát tồn trả 0 dòng, và không có đơn, bản ghi trả hàng hay điểm mới

### Requirement: Trả hàng và bảo hành không chồng nhau

Khi chi tiền trả hàng và tiếp nhận bảo hành chạy đồng thời trên cùng máy, đúng một bên SHALL thành công. Máy đang có hồ sơ mở SHALL không được chi tiền trả hàng. Dòng đơn đã đổi máy bảo hành SHALL không trả hàng hoàn tiền được nữa, nhưng máy thay vẫn được bảo hành tới hết hạn.

#### Scenario: Trả hàng đồng thời với tiếp nhận
- **WHEN** hai giao dịch đồng thời, một chi tiền trả hàng và một tiếp nhận bảo hành, trên cùng IMEI
- **THEN** đúng một giao dịch thành công; máy ở đúng một trạng thái (`Returned` hoặc `UnderRepair`)

#### Scenario: Trả hàng sau khi đã đổi máy
- **WHEN** chi tiền cho yêu cầu trả của dòng đơn có hồ sơ `Replaced`
- **THEN** việc chi tiền bị từ chối 409, không có bản ghi trả hàng, và máy thay vẫn tra được đủ điều kiện nếu còn hạn
