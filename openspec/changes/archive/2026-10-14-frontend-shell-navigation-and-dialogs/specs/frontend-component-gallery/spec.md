# Spec Delta

## MODIFIED Requirements

### Requirement: Explicit gallery launch
Client SHALL chỉ vào gallery khi chế độ preview được bật tường minh. Gallery SHALL được ghi nhãn dữ liệu minh họa và hoạt động không cần backend/database; chế độ này MUST NOT giả lập đăng nhập hoặc thay dữ liệu API thật bằng fixture.

#### Scenario: Default launch
- **WHEN** ứng dụng khởi chạy với cấu hình foundation hợp lệ và gallery không được bật
- **THEN** Shell phát triển mở ở Tổng quan, không có fixture gallery tự xuất hiện và không tạo phiên đăng nhập giả

#### Scenario: Preview launch
- **WHEN** ứng dụng khởi chạy với gallery bật và backend/database tắt
- **THEN** gallery mở, có nhãn “Thư viện giao diện — dữ liệu minh họa”, tương tác được và không phát request nghiệp vụ

#### Scenario: Invalid preview flag
- **WHEN** cấu hình chế độ gallery có giá trị không được hỗ trợ
- **THEN** ứng dụng báo cấu hình không hợp lệ một cách an toàn trước khi hiển thị gallery, không âm thầm chọn preview
