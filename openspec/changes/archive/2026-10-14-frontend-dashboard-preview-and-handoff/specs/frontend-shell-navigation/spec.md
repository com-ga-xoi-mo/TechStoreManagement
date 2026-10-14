# Spec Delta

## MODIFIED Requirements

### Requirement: Offline development shell
Client SHALL mở Shell với cấu hình hợp lệ khi không bật gallery, không cần backend/database. Shell MUST hiển thị trạng thái phát triển chưa đăng nhập và MUST NOT tạo phiên xác thực hoặc request nghiệp vụ để hiển thị trang khung hoặc Dashboard demo. Tổng quan SHALL chỉ có fixture khi demo được bật tường minh; mặc định báo chưa tích hợp Analytics.

#### Scenario: Default offline startup
- **WHEN** khởi chạy với backend tắt, cấu hình hợp lệ và gallery không bật
- **THEN** Shell mở ở Tổng quan, có nhãn “Bản phát triển — chưa đăng nhập”, không có token/session giả hoặc request nghiệp vụ

#### Scenario: Explicit dashboard preview
- **WHEN** Dashboard demo bật và gallery tắt
- **THEN** Shell vẫn mở offline ở Tổng quan với banner dữ liệu minh họa, không tạo phiên đăng nhập hoặc request Analytics

### Requirement: Six coherent admin destinations
Shell SHALL có đúng sáu điểm vào Tổng quan, Sản phẩm, Danh mục và Kho, Đơn hàng, Khách hàng, Cài đặt. Selected menu, tiêu đề và nội dung SHALL cùng phản ánh route đã chuyển thành công. Sản phẩm SHALL mở Catalog đọc thật và chứa route chi tiết theo productId; Tổng quan SHALL thể hiện preview có nhãn khi demo bật, hoặc chưa tích hợp khi tắt; các trang nghiệp vụ chưa triển khai SHALL ghi rõ trạng thái khung thay vì bịa dữ liệu.

#### Scenario: Visit every destination
- **WHEN** lần lượt chọn sáu điểm vào bằng chuột hoặc keyboard
- **THEN** nội dung tương ứng hiển thị, Sản phẩm tải Catalog thật, selection/title đúng, Cài đặt chỉ xuất hiện một lần ở footer và không có mục POS/Identity bổ sung

#### Scenario: Same destination selected again
- **WHEN** chọn lại route hiện hành với cùng tham số
- **THEN** nội dung/state giữ nguyên, không tạo trang hoặc load lần nữa

#### Scenario: Unknown or unavailable destination
- **WHEN** yêu cầu route không hỗ trợ, productId rỗng hoặc không tạo được trang đích
- **THEN** giữ trang/title/selection trước đó và báo lỗi an toàn, không để Shell trống hoặc selection sai

#### Scenario: Read-only detail navigation
- **WHEN** mở card A rồi chuyển tới detail B trong nhánh Sản phẩm
- **THEN** truyền đúng id từng sản phẩm, tải chi tiết tương ứng, hủy activation trước và giữ Sản phẩm selected; detail không tạo mục menu thứ bảy

#### Scenario: Preview stays within dashboard
- **WHEN** mở Dashboard demo rồi chọn Sản phẩm hoặc mục khác
- **THEN** vẫn có đúng sáu mục, Catalog tiếp tục dùng API thật và các mục chưa triển khai giữ trạng thái khung

### Requirement: Predictable presentation state
Trang SHALL thể hiện Loading, Empty, Content hoặc Error theo kết quả hiện hành và cho retry lỗi hiện hành; Catalog detail có thêm NotFound. Dashboard SHALL phân biệt trạng thái chưa tích hợp với kết quả demo rỗng và chỉ tải demo khi bật tường minh. Activation mới SHALL reset dữ liệu trang; Back từ detail Catalog chỉ giữ query/page dạng giá trị và tải lại kết quả. Resize, theme và dialog SHALL không tự load lại hoặc xóa state trang đang mở.

#### Scenario: Failure and retry
- **WHEN** load hiện hành lỗi rồi người dùng retry
- **THEN** Error chuyển Loading rồi sang kết quả mới; không chạy chồng retry cùng request hoặc hiển thị dữ liệu giả để che lỗi

#### Scenario: Placeholder and revisit
- **WHEN** mở trang nghiệp vụ khung chưa triển khai rồi rời và quay lại
- **THEN** trang mới bắt đầu lại lifecycle, kết thúc ở Empty giải thích chưa triển khai, không giữ input từ activation trước

#### Scenario: Presentation-only change
- **WHEN** trang có state hiện hành rồi resize, đổi theme hoặc mở/đóng dialog
- **THEN** state vẫn giữ và không có lần load mới chỉ vì các thao tác này

#### Scenario: Back from product detail
- **WHEN** người dùng quay lại danh sách từ chi tiết
- **THEN** tạo activation list mới với query/page đã áp dụng trước khi mở detail, tải lại API và phục hồi focus về card nếu còn hoặc heading danh sách

#### Scenario: Leave catalog branch
- **WHEN** rời Sản phẩm sang mục Shell khác rồi mở lại Sản phẩm
- **THEN** query/page khởi đầu mặc định, dữ liệu và input từ activation Catalog trước không tự phục hồi

#### Scenario: Dashboard revisit
- **WHEN** rời Dashboard rồi quay lại
- **THEN** activation mới bắt đầu ở kỳ mặc định; demo bật tải snapshot fixture mới, demo tắt tiếp tục báo chưa tích hợp, không khôi phục state cũ
