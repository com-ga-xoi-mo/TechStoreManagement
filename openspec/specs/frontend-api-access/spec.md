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
The client SHALL use the current access token for each request, send exactly one Bearer header when a session exists and none otherwise, and never send it to a foreign origin. Successful login SHALL retain the token pair and current profile in memory. RememberMe=true SHALL persist the existing session record for restart; RememberMe=false SHALL clear any prior persisted session and retain the new session only in memory. Logout or terminal authentication failure SHALL clear both in-memory and persisted credentials.

#### Scenario: Session replacement or clear
- **WHEN** token A is replaced by token B or the session is cleared before the next request
- **THEN** the request uses only token B or has no Authorization header

#### Scenario: Application restart
- **WHEN** the client restarts with a remembered session
- **THEN** it validates or refreshes that session before using protected data and expired/revoked credentials do not produce a successful login state

#### Scenario: Non-remembered restart
- **WHEN** login succeeded with RememberMe=false and the process restarts
- **THEN** the prior user's session is not restored

### Requirement: Authentication failure respects session identity
A 401 from a request using the current session SHALL expire only that matching session and clear its credentials; a 401 from an older session SHALL NOT affect a newer session. A 403 SHALL preserve the session and report access denied. The client SHALL NOT automatically log in, refresh in reaction to a 401, replay a failed business request or automatically replay a refresh with an uncertain network outcome. Pre-request refresh is governed by the bounded refresh requirement.

#### Scenario: Current session unauthorized
- **WHEN** a request using the current session receives 401
- **THEN** the caller receives unauthorized and only the matching session is expired and notified

#### Scenario: Late unauthorized response
- **WHEN** a token-A request receives 401 after the session has been replaced by token B
- **THEN** the error is returned and session B remains active

#### Scenario: Forbidden or anonymous unauthorized
- **WHEN** a request receives 403 or an anonymous request receives 401
- **THEN** the correct error is returned without changing a newer session or performing login/refresh

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

### Requirement: Delete requests
Client SHALL hỗ trợ gửi DELETE không có thân yêu cầu và không cần thân phản hồi. Mọi status thành công, kể cả 204, SHALL hoàn tất mà không deserialize JSON. DELETE SHALL dùng cùng ràng buộc với các thao tác ghi khác: chỉ gửi đến origin đã cấu hình, chuẩn hóa lỗi HTTP, timeout và lỗi mạng theo cùng quy tắc, và MUST NOT tự gửi lại.

#### Scenario: Successful DELETE
- **WHEN** caller gửi DELETE với route tương đối và server trả 204 không có body
- **THEN** thao tác hoàn tất thành công; yêu cầu dùng đúng phương thức DELETE, không có body và dùng base URL đã cấu hình

#### Scenario: Failed DELETE
- **WHEN** server trả 409 hoặc 404 kèm ProblemDetails cho một DELETE
- **THEN** client ném lỗi HTTP đã chuẩn hóa mang mã trạng thái, `detail` và lỗi theo trường, và chỉ có đúng một yêu cầu được gửi

#### Scenario: Foreign destination
- **WHEN** caller truyền URL tuyệt đối hoặc network-path khác origin cho DELETE
- **THEN** client từ chối trước khi gửi và không gắn bearer token vào đích đó

### Requirement: File upload requests
Client SHALL gửi được một file bằng POST `multipart/form-data`, gồm tên trường, tên file, loại nội dung, nội dung file và các trường chữ kèm theo, rồi đọc thân phản hồi JSON như các yêu cầu khác. Yêu cầu này SHALL dùng cùng ràng buộc với các thao tác ghi khác: chỉ gửi đến origin đã cấu hình, gắn bearer token hiện tại, chuẩn hóa lỗi HTTP, timeout, lỗi mạng, và MUST NOT tự gửi lại.

#### Scenario: Upload one file
- **WHEN** caller gửi file `a.jpg` (image/jpeg) ở trường `file` kèm trường `variantId` tới route tương đối và server trả 201 kèm JSON
- **THEN** yêu cầu là POST multipart có đúng một phần file và một phần `variantId`, và caller nhận object đã deserialize

#### Scenario: Rejected upload
- **WHEN** server trả 400 ProblemDetails có lỗi cho `file`
- **THEN** client ném lỗi HTTP đã chuẩn hóa mang mã 400 và lỗi theo trường `file`, và chỉ có đúng một yêu cầu được gửi

#### Scenario: Foreign destination
- **WHEN** caller truyền URL tuyệt đối khác origin
- **THEN** client từ chối trước khi gửi và không gắn bearer token

### Requirement: Refresh is bounded and respects the current session
Before sending a business request with an expired or soon-expiring access token, the client SHALL perform at most one shared refresh operation for that session generation. Concurrent callers SHALL await that operation and use its replacement pair only if the originating session is still current. No business write SHALL be sent before refresh succeeds or replayed afterward. Refresh transport SHALL use only the configured origin, avoid redirects and bypass recursive bearer-refresh handling. A definite refresh rejection SHALL clear only the matching session; an uncertain network failure SHALL be shown as a session-recovery error requiring login without automatically reusing the old refresh token. Cancellation by one waiting caller SHALL NOT cancel refresh needed by other callers. Token renewal SHALL preserve the RememberMe choice.

#### Scenario: Several simultaneous requests need refresh
- **WHEN** multiple requests share a soon-expiring session
- **THEN** one refresh is sent, the pair is replaced once and each uncancelled business request is sent at most once

#### Scenario: Session replaced while refresh is in flight
- **WHEN** user A's refresh completes after logout or user B logs in
- **THEN** A's result cannot restore A's session, overwrite B's tokens or save A's credentials to disk

#### Scenario: Refresh fails before a write
- **WHEN** refresh is rejected or has an uncertain network outcome before a product/order/account write
- **THEN** the write is not sent and the client reports the authentication or recovery error

### Requirement: Late responses stay in their session
A business response that arrives after the client session generation changed (logout, expiry or another account signing in) SHALL be discarded by the transport and reported to the caller as a session-changed failure; its body SHALL NOT reach any ViewModel. Login and refresh requests are exempt.

#### Scenario: Logout while a request is in flight
- **WHEN** a stock list request is sent, the user logs out and the response arrives afterwards
- **THEN** the caller receives the session-changed failure and no stock rows are returned

#### Scenario: Another account signs in before the response
- **WHEN** Admin's request is in flight, Admin logs out and `cashier1` signs in before the response arrives
- **THEN** Admin's data is not shown in `cashier1`'s session

#### Scenario: Same session
- **WHEN** the session generation is unchanged when the response arrives (including a silent token refresh)
- **THEN** the response is returned as before
