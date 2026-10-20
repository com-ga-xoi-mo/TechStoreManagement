# frontend-api-access Specification

## Purpose

Định nghĩa hành vi giao tiếp JSON qua API và vòng đời token/session dùng chung cho các repository frontend, tránh mỗi feature tự xử lý transport hoặc diễn giải lỗi khác nhau.

## Requirements

### Requirement: Configured API boundary
Client SHALL gửi request đến API origin đã cấu hình, giữ route/query hợp lệ và không cho caller chuyển bearer token sang origin khác. Client MUST NOT tự retry thao tác ghi hoặc tự theo redirect HTTP.

#### Scenario: Relative route with query
- **WHEN** caller gửi GET với route tương đối và query đã encode
- **THEN** API nhận đúng route/query trên base URL đã cấu hình

#### Scenario: Foreign destination or redirect
- **WHEN** caller cung cấp absolute URL/network-path khác origin hoặc server trả redirect
- **THEN** client không gửi token đến đích đó và trả lỗi thay vì tự theo redirect

#### Scenario: Failed write
- **WHEN** POST thất bại sau lần gửi đầu tiên
- **THEN** client trả lỗi và không tự gửi lại POST

### Requirement: JSON success responses
Client SHALL serialize/deserialize JSON theo contract HTTP của backend, giữ metadata phân trang và kiểu dữ liệu JSON động. Danh sách rỗng hợp lệ SHALL được trả như dữ liệu thành công; field bổ sung không biết SHALL được bỏ qua.

#### Scenario: Paged response
- **WHEN** API trả JSON phân trang hợp lệ với items và metadata
- **THEN** caller nhận đủ envelope, metadata và giá trị DTO, không bị chuyển thành mảng đơn

#### Scenario: Empty list and dynamic fields
- **WHEN** API trả danh sách rỗng hợp lệ hoặc specs JSON chứa string, number, boolean và null
- **THEN** client giữ danh sách rỗng và kiểu dữ liệu JSON tương ứng

#### Scenario: Request and additive response field
- **WHEN** caller gửi object request và API trả object có field bổ sung ngoài contract
- **THEN** request được gửi dưới dạng JSON và response vẫn deserialize thành công

### Requirement: Explicit response body expectations
Client SHALL phân biệt operation cần JSON body với operation không cần body. Body rỗng, JSON null, JSON malformed hoặc kiểu JSON không tương thích ở operation cần body MUST trả lỗi invalid response, không được trả default DTO hoặc dữ liệu demo.

#### Scenario: Body required but missing or invalid
- **WHEN** operation cần body nhận 204, body rỗng, JSON null, JSON sai cú pháp hoặc kiểu không tương thích
- **THEN** caller nhận lỗi invalid response kèm HTTP status thực tế

#### Scenario: Successful operation without body
- **WHEN** operation được khai báo không cần body nhận status thành công, bao gồm 204
- **THEN** operation hoàn tất thành công mà không cố deserialize JSON

### Requirement: Normalized HTTP errors
Client SHALL trả lỗi thống nhất cho status không thành công, giữ HTTP status thực tế, ProblemDetails và validation theo field khi hợp lệ. Với body rỗng, không phải JSON hoặc schema lỗi không nhận diện, client SHALL trả thông báo fallback an toàn, không trả body thô hoặc dữ liệu thành công.

#### Scenario: Validation and conflict
- **WHEN** API trả 400 với validation errors hoặc 409 với ProblemDetails
- **THEN** caller nhận status, thông tin lỗi có cấu trúc và field errors/trace identifier nếu có

#### Scenario: Invalid error payload
- **WHEN** API trả 500 với HTML, body rỗng, JSON sai hoặc status trong JSON khác HTTP status
- **THEN** lỗi giữ HTTP status thực tế và thông báo an toàn, không phát sinh lỗi JSON làm mất lỗi HTTP gốc

### Requirement: Cancellation and transport failure
Client SHALL phân biệt cancellation do caller, timeout và network failure. Cancellation SHALL được truyền đến transport và giữ ngữ nghĩa cancellation; timeout/network failure SHALL trả lỗi phân loại rõ ràng. Response/request resources MUST được giải phóng sau success hoặc failure.

#### Scenario: Caller cancels request
- **WHEN** caller hủy token trong khi request đang chờ
- **THEN** transport nhận cancellation và caller nhận cancellation thay vì timeout hoặc HTTP error

#### Scenario: Timeout or network failure
- **WHEN** request hết timeout hoặc transport không kết nối được
- **THEN** caller nhận loại lỗi timeout hoặc network tương ứng, không có status HTTP giả

#### Scenario: Response lifecycle
- **WHEN** xử lý response hoàn tất thành công hoặc thất bại
- **THEN** request/response resources được giải phóng

### Requirement: Current bearer token
Client SHALL lấy access token hiện hành riêng cho mỗi request, gửi đúng một Bearer header khi có token và bỏ header khi không có. Token/session SHALL chỉ tồn tại trong bộ nhớ trong change này; khởi động lại client bắt đầu không có phiên.

#### Scenario: Session replacement or clear
- **WHEN** token A được thay bằng token B hoặc phiên bị xóa trước request kế tiếp
- **THEN** request kế tiếp chỉ dùng token B hoặc không gửi Authorization

#### Scenario: Application restart
- **WHEN** client đóng rồi khởi động lại
- **THEN** client không phục hồi token của lần chạy trước

### Requirement: Authentication failure respects session identity
401 của request đã dùng token hiện hành SHALL đánh dấu phiên hết hạn, xóa token đó và thông báo thay đổi phiên; 401 của request thuộc phiên cũ MUST NOT thay đổi phiên mới. 403 SHALL giữ phiên và báo thiếu quyền. Client MUST NOT gọi login/refresh tự động.

#### Scenario: Current session unauthorized
- **WHEN** request của phiên hiện hành nhận 401
- **THEN** caller nhận lỗi unauthorized, token hiện hành bị xóa và session-expired được thông báo

#### Scenario: Late unauthorized response
- **WHEN** request dùng token A nhận 401 sau khi phiên đã được thay bằng token B
- **THEN** caller nhận lỗi unauthorized và phiên B vẫn hoạt động với token B

#### Scenario: Forbidden or anonymous unauthorized
- **WHEN** request nhận 403 hoặc request không có token nhận 401
- **THEN** client trả lỗi đúng status mà không xóa phiên mới hoặc tự thực hiện auth/refresh

### Requirement: Partial update requests
Client SHALL hỗ trợ gửi PATCH có thân JSON và nhận thân JSON thành công, với cùng ràng buộc như các thao tác ghi khác: chỉ gửi đến origin đã cấu hình, chuẩn hóa lỗi HTTP, hết thời gian chờ và lỗi mạng theo cùng quy tắc, và MUST NOT tự gửi lại.

#### Scenario: Successful PATCH
- **WHEN** caller gửi PATCH với route tương đối và thân JSON hợp lệ, server trả 200 kèm JSON
- **THEN** caller nhận đối tượng đã giải mã; yêu cầu dùng đúng phương thức PATCH, kiểu nội dung JSON và base URL đã cấu hình

#### Scenario: Failed PATCH
- **WHEN** server trả 409 hoặc 400 kèm ProblemDetails cho một PATCH
- **THEN** client ném lỗi HTTP đã chuẩn hóa mang mã trạng thái, `detail` và lỗi theo trường, và không gửi lại yêu cầu

#### Scenario: Foreign destination
- **WHEN** caller truyền URL tuyệt đối hoặc network-path khác origin cho PATCH
- **THEN** client từ chối trước khi gửi và không gắn bearer token vào đích đó
