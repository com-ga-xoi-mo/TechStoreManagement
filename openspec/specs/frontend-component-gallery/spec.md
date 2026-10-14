# frontend-component-gallery Specification

## Purpose

Cho phép developer và chủ dự án xem, tương tác và nghiệm thu design system bằng fixture có thể tái lập mà không phụ thuộc API hay lẫn với dữ liệu nghiệp vụ thật.

## Requirements

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

### Requirement: Reproducible component specimens
Gallery SHALL có mẫu palette/typography/icons, form/table/feedback và bốn shared controls. Fixtures SHALL có thể lặp lại và bao phủ trạng thái content, loading, error, empty, disabled và focused tại thành phần phù hợp.

#### Scenario: Inspect and change component state
- **WHEN** developer đổi state, text/value hoặc trạng thái command trong gallery
- **THEN** specimen đang hiển thị cập nhật; query/action counters chứng minh binding/command hoạt động mà không phải tải dữ liệu hoặc khởi động lại

#### Scenario: Repeat preview session
- **WHEN** đóng gallery rồi mở lại cùng cấu hình
- **THEN** fixture trở về baseline xác định, không cần backend và không phục hồi dữ liệu/session của người dùng trước

### Requirement: Theme and viewport preview
Gallery SHALL cho kiểm tra System/Light/Dark, keyboard và resize trong cùng phiên; vùng preview SHALL giữ nội dung có thể đọc và thao tác tại các kích thước nghiệm thu.

#### Scenario: Theme preview preserves input
- **WHEN** query đã nhập và người dùng chuyển theme trong gallery
- **THEN** query/state giữ nguyên, controls đổi theme và focus/action còn sử dụng được

#### Scenario: Keyboard access to all sections
- **WHEN** người dùng chỉ dùng keyboard ở cửa sổ 800×600 DIP
- **THEN** có thể tới mọi action khả dụng và cuộn tới section tương ứng, không bị trap trong control hoặc vùng scroll

### Requirement: Reviewable acceptance evidence
Gallery SHALL hỗ trợ thu bằng chứng đối chiếu mẫu trên Windows và Skia với build/target/theme/kích thước xác định. Kết quả runtime và xác nhận của chủ dự án MUST được ghi riêng; compile hoặc screenshot đơn lẻ không thay thế owner acceptance.

#### Scenario: Runtime evidence is collected
- **WHEN** một lượt kiểm tra gallery hoàn tất
- **THEN** record có OS/renderer, build id, command, viewport/DPI, theme, expected/actual và ảnh/log tương ứng; nền tảng hoặc kiểm tra chưa chạy được ghi unverified

#### Scenario: Owner has not confirmed
- **WHEN** automated/runtime checks đã có nhưng chủ dự án chưa xác nhận tested build
- **THEN** task manual acceptance vẫn unchecked và change chưa được báo là đã nghiệm thu giao diện

#### Scenario: Owner confirms tested build
- **WHEN** chủ dự án xác nhận rõ build và target đã xem
- **THEN** acceptance record ghi ngày, build/commit hoặc build id, target và kết quả/ngoại lệ được nêu; chỉ khi đó task owner acceptance được tick
