# Spec Delta

## ADDED Requirements

### Requirement: C# Markup reference specimen
Gallery SHALL cung cấp một trang mẫu tham chiếu viết bằng C# Markup, mở được từ gallery khi chế độ gallery bật. Trang mẫu dùng dữ liệu minh họa có nhãn, không gọi backend, và SHALL thể hiện binding hai chiều, command, các trạng thái tải/rỗng/lỗi/nội dung, bốn shared controls và màu theo theme để làm chuẩn cho các màn hình khác.

#### Scenario: Open the reference specimen
- **WHEN** ứng dụng chạy với gallery bật, backend/database tắt, và developer chọn mở trang mẫu C# Markup từ gallery
- **THEN** trang mẫu hiển thị với nhãn dữ liệu minh họa, tương tác được, không phát request nghiệp vụ và có thể quay lại gallery

#### Scenario: Not reachable in normal launch
- **WHEN** ứng dụng khởi chạy với gallery không bật
- **THEN** trang mẫu không xuất hiện trong Shell hay luồng đăng nhập và không có điểm vào nào tới nó

#### Scenario: Two-way binding and command
- **WHEN** developer nhập “điện thoại & phụ kiện” vào ô tìm kiếm của trang mẫu rồi nhấn Enter, sau đó dùng action đặt lại
- **THEN** text nhập hiện ngay ở vùng hiển thị giá trị của ViewModel, submit được ghi nhận đúng một lần với text đầy đủ dấu, và đặt lại xóa cả ô nhập lẫn giá trị trong ViewModel

#### Scenario: Content states
- **WHEN** developer chuyển trang mẫu lần lượt qua tải, rỗng, lỗi và nội dung
- **THEN** mỗi lúc chỉ một trạng thái hiển thị; trạng thái tải có chỉ báo, rỗng dùng empty state, lỗi có thông báo và action thử lại, nội dung hiển thị metric cards và status badges

#### Scenario: Theme change keeps specimen state
- **WHEN** trang mẫu đang có text nhập và trạng thái nội dung, và người dùng đổi Light, Dark rồi theo hệ thống
- **THEN** màu của trang và các controls đổi theo theme, text nhập và trạng thái hiện tại giữ nguyên
