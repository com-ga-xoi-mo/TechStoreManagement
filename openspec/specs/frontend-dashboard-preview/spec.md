# frontend-dashboard-preview Specification

## Purpose

Cung cấp Dashboard minh họa có thể nghiệm thu và bàn giao độc lập với backend Analytics, với nguồn dữ liệu, kỳ thống kê, trạng thái và giới hạn tích hợp được thể hiện rõ ràng.

## Requirements

### Requirement: Explicit dashboard demo selection
Dashboard SHALL chỉ hiển thị fixture khi chế độ demo được bật tường minh; mặc định SHALL báo thống kê chưa tích hợp. Demo MUST có nhãn liên tục, không tạo phiên đăng nhập, không gọi Analytics API hay truy cập database và không trở thành fallback của bất kỳ API thật nào.

#### Scenario: Default unavailable dashboard
- **WHEN** mở Tổng quan với cấu hình hợp lệ và demo không bật
- **THEN** hiển thị “Thống kê chưa tích hợp”, KPI chưa có giá trị thay vì số 0; không có fixture hoặc request Analytics, bộ chọn kỳ và refresh không khả dụng

#### Scenario: Explicit demo launch
- **WHEN** bật Dashboard demo và gallery tắt, kể cả khi backend không chạy
- **THEN** Tổng quan hiển thị fixture với nhãn “Dữ liệu minh họa — chưa kết nối Analytics API”, ngày tham chiếu và kỳ cụ thể, không giả xác thực Admin

#### Scenario: Invalid flag or simultaneous gallery selection
- **WHEN** cờ demo có giá trị khác unset/0/1 hoặc cả gallery và demo đều bằng 1
- **THEN** cấu hình sai bị từ chối an toàn trước hiển thị; trường hợp cả hai hợp lệ thì gallery vẫn được ưu tiên và không tải Dashboard ngầm

#### Scenario: Catalog remains live
- **WHEN** rời Dashboard demo sang Sản phẩm và API Catalog lỗi
- **THEN** Catalog báo lỗi thật, không dùng fixture hoặc dữ liệu Analytics thay thế

### Requirement: Reproducible and coherent preview data
Dashboard demo SHALL dùng dữ liệu xác định, tiền chính xác và metadata kỳ/as-of rõ ràng. Doanh thu và chuỗi ngày SHALL nhất quán; các tổng KPI MUST NOT được suy từ năm dòng preview. Số sản phẩm SHALL được phân biệt với số máy tồn kho.

#### Scenario: Repeat the same period
- **WHEN** chọn cùng kỳ hoặc khởi động lại cùng fixture version
- **THEN** số liệu và ngày tham chiếu giữ nguyên; tổng điểm doanh thu ngày bằng KPI doanh thu của kỳ, ngày không bán có giá trị 0

#### Scenario: Partial lists and unavailable comparisons
- **WHEN** tổng số cảnh báo/đơn lớn hơn số dòng preview hoặc không có kỳ so sánh hợp lệ
- **THEN** KPI giữ tổng fixture, danh sách chỉ có tối đa năm dòng; tỷ lệ thiếu không bị biến thành 0% hoặc vô cực

#### Scenario: Legitimate zero and empty data
- **WHEN** fixture thành công chứa KPI bằng 0, không đơn hoặc không tồn thấp
- **THEN** thể hiện số 0 hợp lệ và empty cho từng danh sách, không báo Analytics chưa tích hợp hoặc lỗi

### Requirement: Meaningful period selection
Dashboard demo SHALL có Hôm nay, 7 ngày qua và Tháng này dựa trên ngày tham chiếu fixture được công bố, mặc định 7 ngày qua. Doanh thu, đơn và trend SHALL theo cùng kỳ; số sản phẩm và tồn thấp SHALL ghi rõ là snapshot tại thời điểm dữ liệu.

#### Scenario: Change period
- **WHEN** chọn kỳ khác
- **THEN** hiển thị loading, bỏ nội dung kỳ cũ và commit toàn bộ kết quả của kỳ mới cùng nhau; không đặt nhãn kỳ mới trên KPI cũ

#### Scenario: Fixed reference date
- **WHEN** mở cùng demo vào một ngày máy khác
- **THEN** kỳ fixture không âm thầm đổi, UI cho biết khoảng ngày cụ thể và ngày tham chiếu minh họa

### Requirement: Recoverable and cancellable dashboard loading
Dashboard SHALL phân biệt loading, content, empty từng phần, lỗi và chưa tích hợp; retry SHALL dùng kỳ thất bại. Chỉ request hiện hành được cập nhật nội dung/lỗi/loading. Rời trang hoặc đóng app SHALL hủy công việc và không hiển thị cancellation dự kiến như lỗi.

#### Scenario: Failure and retry
- **WHEN** nguồn preview kiểm thử báo lỗi rồi người dùng retry
- **THEN** báo lỗi rõ, không trả fixture khác hoặc empty giả; retry thành công xóa lỗi và hiển thị đúng kỳ đã yêu cầu

#### Scenario: Late response after period change or navigation
- **WHEN** request A kết thúc sau B hoặc sau khi người dùng rời Dashboard, dù A không hợp tác cancellation
- **THEN** success/error/finally của A không thay state mới; tài nguyên chỉ giải phóng sau khi công việc sử dụng chúng kết thúc

### Requirement: Accessible reference dashboard
Dashboard SHALL giữ hierarchy mẫu với bốn KPI, doanh thu ngày, tồn thấp và đơn gần nhất; dùng tiếng Việt/VND rõ ràng, theme và keyboard phù hợp. Các dòng demo SHALL chỉ đọc và MUST NOT mở bản ghi thật bằng id fixture hoặc cung cấp action nghiệp vụ giả.

#### Scenario: Responsive theme and text scaling
- **WHEN** resize giữa 1440×1000, 1024×768 và 800×600 DIP, đổi Light/Dark/System hoặc text scale 100/150/200%
- **THEN** panels reflow, nội dung và bộ chọn kỳ vẫn truy cập được, dấu tiếng Việt/focus rõ; trình bày không tải lại dữ liệu

#### Scenario: Chart values without hover
- **WHEN** sử dụng keyboard hoặc công nghệ hỗ trợ để đọc trend, kể cả chuỗi zero/rỗng/một điểm
- **THEN** có dữ liệu ngày–VND tương đương đọc được, không phụ thuộc màu hoặc hover và không lỗi chia cho 0 hay hình học sai

#### Scenario: Preview rows
- **WHEN** xem đơn gần nhất hoặc tồn thấp
- **THEN** dữ liệu có nhãn demo, trạng thái không bị tự chuyển thành Hoàn thành và không có link “Xem tất cả” hay drilldown chưa được triển khai

### Requirement: Explicit analytics and seed readiness
Bàn giao SHALL phân biệt nhu cầu Analytics/seed đề xuất với contract backend đã xác nhận. Preview MUST NOT được báo là hoàn thành UC-STAT-01/02/03, UC-SEED-01 hoặc Analytics API integration; client MUST NOT tải toàn bộ đơn thật để tự tính thống kê.

#### Scenario: Backend rules remain pending
- **WHEN** bàn giao mà định nghĩa KPI, thời gian, tồn thấp, DTO/route hoặc RBAC chưa được backend xác nhận
- **THEN** mỗi phụ thuộc có owner, trạng thái Pending và điều kiện cần để đóng; fixture policy vẫn được ghi là quy ước demo, không phải contract API

#### Scenario: Seed preparation
- **WHEN** rà soát UC-SEED-01
- **THEN** tài liệu so sánh baseline hiện tại với mục tiêu Sprint 2 gồm ít nhất 20 sản phẩm, 5 danh mục, 10 khách, 20 đơn và 50 IMEI, cùng yêu cầu idempotency/invariants/đối soát; không tuyên bố dữ liệu đó đã được seed hoặc tự sửa backend

### Requirement: Reviewable foundation handoff
Foundation SHALL có hướng dẫn mở rộng theo kiến trúc đã chọn và bằng chứng tách riêng tests, nền tảng, nghiệm thu giao diện và backend readiness. Mọi kết quả SHALL gắn với build/target cụ thể; kiểm tra chưa chạy MUST ghi unverified.

#### Scenario: Another developer extends a feature
- **WHEN** Dev 1–5 đọc bộ bàn giao
- **THEN** xác định được Model/Repository/VM/View, DI, lifecycle/errors/session, navigation/dialog/controls và ví dụ Catalog thật; biết điểm mở rộng cùng phụ thuộc Sprint 2 của mình

#### Scenario: Platform evidence differs
- **WHEN** có Linux Skia evidence nhưng chưa chạy Windows native hoặc chưa xác nhận backend
- **THEN** chỉ ghi Linux verified cho các checks đã chạy; Windows vẫn unverified và Analytics integration chưa hoàn tất

#### Scenario: Owner review
- **WHEN** chủ dự án xác nhận Dashboard của build và target đã xem
- **THEN** record có ngày, build/hash, target và kết quả/ngoại lệ; task owner acceptance mới được tick, không thừa kế acceptance bốn change trước
