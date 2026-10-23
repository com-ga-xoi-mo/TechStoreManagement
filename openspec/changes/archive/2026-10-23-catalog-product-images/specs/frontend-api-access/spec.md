# Spec Delta

## ADDED Requirements

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
