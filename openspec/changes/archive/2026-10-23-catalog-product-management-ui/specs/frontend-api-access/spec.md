# Spec Delta

## ADDED Requirements

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
