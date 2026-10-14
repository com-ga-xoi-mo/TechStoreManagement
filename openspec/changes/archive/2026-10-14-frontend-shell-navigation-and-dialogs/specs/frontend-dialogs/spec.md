# Spec Delta

## Purpose

Cung cấp xác nhận và thông báo modal thống nhất cho frontend, bảo đảm kết quả, cancellation và keyboard/focus đúng trong vòng đời cửa sổ và trang.

## ADDED Requirements

### Requirement: UI independent dialog requests
Consumer SHALL yêu cầu dialog bằng nội dung/nhãn và nhận kết quả Confirmed, Cancelled hoặc Dismissed mà không truyền UI controls. Cancellation của tác vụ SHALL phân biệt với thao tác Hủy của người dùng.

#### Scenario: Explicit user decision
- **WHEN** người dùng xác nhận, bấm Hủy hoặc đóng dialog thông báo
- **THEN** caller nhận đúng một kết quả tương ứng và không có thay đổi nghiệp vụ do service tự thực hiện

#### Scenario: Caller cancels
- **WHEN** token của request bị hủy
- **THEN** task hoàn tất với cancellation, không báo Confirmed hay giả thành thao tác người dùng

### Requirement: Serialized window dialogs
Mỗi cửa sổ SHALL chỉ hiển thị một modal tại một thời điểm, xếp hàng FIFO cho yêu cầu đồng thời và chỉ mở trên root đang hoạt động.

#### Scenario: Concurrent requests
- **WHEN** hai consumer yêu cầu dialog đồng thời
- **THEN** dialog thứ hai chỉ mở sau khi dialog đầu đóng, mỗi caller nhận đúng kết quả của mình

#### Scenario: Root is not ready
- **WHEN** yêu cầu dialog trước khi UI root sẵn sàng
- **THEN** caller nhận lỗi availability có kiểm soát, không treo hoặc mở dialog không có owner

### Requirement: Dialog lifecycle cancellation
Dialog queued hoặc visible SHALL bị hủy khi caller lifetime kết thúc. Khi window teardown, mọi request SHALL kết thúc, request mới bị từ chối bằng cancellation và không mở dialog trên root cũ.

#### Scenario: Cancel queued request
- **WHEN** một request đang chờ bị hủy
- **THEN** request đó không xuất hiện, không làm mất lượt hoặc kết quả của request còn hiệu lực

#### Scenario: Leave page or close window
- **WHEN** rời trang sở hữu dialog đang mở hoặc đóng window
- **THEN** dialog thuộc lifetime hết hạn đóng, task kết thúc một lần; không có dialog mồ côi hoặc lỗi chưa observe

### Requirement: Modal keyboard and focus recovery
Dialog SHALL có nhãn tiếng Việt, keyboard focus nhìn thấy, hành vi Enter/Escape rõ ràng và giữ Tab trong modal. Khi đóng SHALL phục hồi focus tới nguồn gọi còn hợp lệ, hoặc vùng nội dung trang hiện hành nếu nguồn đã bị tháo.

#### Scenario: Keyboard decision
- **WHEN** mở dialog bằng keyboard, dùng Tab/Shift+Tab và Enter hoặc Escape
- **THEN** focus không lọt ra sau modal; Enter thực thi button mặc định được chỉ định, Escape hủy/đóng và không xác nhận ngoài ý muốn

#### Scenario: Original page remains
- **WHEN** đóng dialog mà phần tử gọi vẫn ở trang hiện hành
- **THEN** focus trở lại phần tử đó và tiếp tục điều hướng bằng keyboard được

#### Scenario: Original page removed
- **WHEN** dialog bị đóng do chuyển trang và phần tử gọi đã detached
- **THEN** focus về vùng nội dung/heading trang mới, không quay về phần tử cũ
