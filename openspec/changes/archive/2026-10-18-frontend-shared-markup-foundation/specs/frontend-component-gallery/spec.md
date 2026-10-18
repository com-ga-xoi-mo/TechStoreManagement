# Spec Delta

## ADDED Requirements

### Requirement: Observable shared contract specimens
Gallery SHALL cung cấp specimens tương tác để kiểm tra optional values, badge tone, command replacement và CanExecute changes của shared controls; counters và labels SHALL cho thấy action/state thực tế. Các specimens MUST hoạt động offline và không làm thay đổi dữ liệu hoặc phiên nghiệp vụ.

#### Scenario: Inspect command replacement and disabled actions
- **WHEN** người dùng thay command mẫu, đổi CanExecute và thử kích hoạt action
- **THEN** specimen cho thấy command hiện hành được gọi đúng một lần khi enabled, không gọi khi disabled và không gọi command cũ

#### Scenario: Inspect changing optional and themed values
- **WHEN** người dùng đổi metric sang 0, xóa optional text/icon, đổi badge label/tone rồi đổi theme
- **THEN** mẫu phản ánh đúng state mới, giữ input và có thể quan sát nghĩa trạng thái bằng chữ mà không cần backend

### Requirement: Reusable gallery lifecycle
Gallery SHALL giữ hành vi diagnostics, dialogs, theme và focus sau nhiều lần hiển thị/rời trang. Công việc diagnostic thuộc lần hiển thị cũ MUST được hủy hoặc bỏ kết quả; action và dialog MUST NOT phát sinh nhiều lần do subscription còn sót.

#### Scenario: Leave during diagnostic operation
- **WHEN** bắt đầu diagnostic đang chờ rồi rời gallery và mở lại
- **THEN** công việc cũ không cập nhật trang mới, presenter cũ không còn gắn với trang đã rời và một action mới chỉ mở một dialog

#### Scenario: Dialog closes after repeated visits
- **WHEN** mở và đóng dialog trên gallery sau nhiều lần mount/unmount
- **THEN** focus trở lại action phù hợp trên trang hiện hành, keyboard tiếp tục sử dụng được và theme/input vẫn đúng
