## ADDED Requirements

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
