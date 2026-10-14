# Spec Delta

## Purpose

Cung cấp nền khởi tạo client với cấu hình API, dependencies và logging nhất quán, cho phép các thành phần foundation chạy và được kiểm thử độc lập với giao diện.

## ADDED Requirements

### Requirement: Offline startup
Client SHALL mở cửa sổ hiện có khi cấu hình hợp lệ mà không yêu cầu API hoặc database đang chạy.

#### Scenario: Backend unavailable at startup
- **WHEN** người dùng khởi chạy client với cấu hình hợp lệ và backend đã dừng
- **THEN** cửa sổ hiện có mở và tương tác được, không phát sinh request kiểm tra backend bắt buộc

### Requirement: Validated API configuration
Client SHALL cho phép cấu hình base URL và timeout từ môi trường chạy, kiểm tra cấu hình trước khi gửi request và báo lỗi rõ ràng khi cấu hình không hợp lệ. Client MUST giữ kiểm tra chứng chỉ HTTPS mặc định.

#### Scenario: Environment override
- **WHEN** cấu hình môi trường cung cấp một base URL HTTP/HTTPS tuyệt đối hợp lệ và timeout dương
- **THEN** request dùng giá trị cấu hình đó thay cho mặc định development

#### Scenario: Invalid configuration
- **WHEN** base URL không hợp lệ hoặc timeout không phải số dương hữu hạn
- **THEN** startup thất bại có diagnostic an toàn và không gửi request

#### Scenario: Untrusted certificate
- **WHEN** kết nối HTTPS nhận chứng chỉ không được tin cậy
- **THEN** request thất bại thay vì bỏ qua xác thực chứng chỉ

### Requirement: Consistent session ownership
Các consumer trong cùng một lần chạy client SHALL quan sát cùng phiên hiện hành; mở consumer mới không được tạo bản sao token cũ. Các thành phần có tài nguyên SHALL được giải phóng khi client kết thúc.

#### Scenario: Session shared across consumers
- **WHEN** một consumer thay đổi phiên và consumer khác được tạo sau đó
- **THEN** consumer mới quan sát phiên đã thay đổi

#### Scenario: Application shutdown
- **WHEN** cửa sổ ứng dụng đóng
- **THEN** tài nguyên foundation được giải phóng một lần và không tiếp tục phục vụ hoạt động mới

### Requirement: Safe diagnostics
Client SHALL ghi diagnostic startup và lỗi transport có mức độ, loại lỗi, HTTP status và trace identifier khi có; MUST NOT ghi token, Authorization header, credentials, request/response body hoặc dữ liệu khách hàng vào log.

#### Scenario: Authenticated request fails
- **WHEN** request có token nhận lỗi API kèm trace identifier
- **THEN** log cho phép phân loại lỗi và tra trace identifier mà không chứa token hoặc payload

### Requirement: Headless foundation verification
Hành vi transport, session và dependency wiring SHALL kiểm thử được bằng thành phần production mà không cần UI thread, cửa sổ, database hoặc API đang chạy.

#### Scenario: Isolated test run
- **WHEN** chạy suite foundation trong môi trường không có desktop session và backend
- **THEN** các kiểm thử dùng transport giả thực thi hành vi production và hoàn tất độc lập
