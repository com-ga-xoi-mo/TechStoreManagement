# frontend-shell-navigation Specification

## Purpose

Cung cấp khung điều hướng TechStore có vòng đời trang và state rõ ràng để các phân hệ gắn vào, kiểm thử độc lập và nghiệm thu trên desktop được hỗ trợ.

## Requirements

### Requirement: Offline development shell
Client SHALL mở Shell với cấu hình hợp lệ khi không bật gallery, không cần backend/database. Shell MUST hiển thị trạng thái phát triển chưa đăng nhập và MUST NOT tạo phiên xác thực hoặc request nghiệp vụ để hiển thị trang khung hoặc Dashboard demo. Tổng quan SHALL chỉ có fixture khi demo được bật tường minh; mặc định báo chưa tích hợp Analytics.

#### Scenario: Default offline startup
- **WHEN** khởi chạy với backend tắt, cấu hình hợp lệ và gallery không bật
- **THEN** Shell mở ở Tổng quan, có nhãn “Bản phát triển — chưa đăng nhập”, không có token/session giả hoặc request nghiệp vụ

#### Scenario: Explicit dashboard preview
- **WHEN** Dashboard demo bật và gallery tắt
- **THEN** Shell vẫn mở offline ở Tổng quan với banner dữ liệu minh họa, không tạo phiên đăng nhập hoặc request Analytics

### Requirement: Six coherent admin destinations
Shell SHALL có đúng sáu điểm vào Tổng quan, Sản phẩm, Danh mục và Kho, Đơn hàng, Khách hàng, Cài đặt, được cấu trúc và khởi tạo bằng C# Markup Fluent UI (`NavigationView`). Selected menu, tiêu đề và nội dung SHALL cùng phản ánh route đã chuyển thành công. Sản phẩm SHALL mở Catalog đọc thật và chứa route chi tiết theo productId; Tổng quan SHALL thể hiện preview có nhãn khi demo bật, hoặc chưa tích hợp khi tắt; các trang nghiệp vụ chưa triển khai SHALL ghi rõ trạng thái khung thay vì bịa dữ liệu.

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

### Requirement: Cancellable page activation
Trang SHALL bắt đầu load một lần mỗi activation, hủy load khi rời trang hoặc đóng cửa sổ và bỏ mọi kết quả thuộc activation cũ. Chuyển trang SHALL không phải đợi load cũ hoàn tất; cancellation dự kiến MUST NOT hiện thành lỗi người dùng.

#### Scenario: Rapid navigation and late completion
- **WHEN** chuyển A sang B rồi về A trong khi load A cũ chưa xong, sau đó load cũ thành công hoặc lỗi
- **THEN** trang A mới giữ state của activation mới, kết quả/lỗi/loading-finalization cũ không thay đổi nó hoặc trang B

#### Scenario: Window closes while loading
- **WHEN** đóng cửa sổ khi load đang chạy
- **THEN** load nhận cancellation, không cập nhật UI đã đóng, không nhận thao tác mới và tài nguyên được giải phóng đúng một lần khi công việc kết thúc

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

### Requirement: Session local settings
Cài đặt SHALL cho chọn System/Light/Dark và xem thông tin ứng dụng không nhạy cảm. Theme SHALL áp dụng ngay trong Shell/dialog, giữ khi chuyển trang trong phiên và mặc định System ở phiên mới. Thao tác theme không cần backend; riêng Catalog gọi API khi được mở.

#### Scenario: Theme survives navigation
- **WHEN** chọn Dark, chuyển Sản phẩm rồi trở lại Cài đặt
- **THEN** Shell và dialogs dùng Dark, lựa chọn còn Dark; theme không phát request và Catalog chỉ gọi API cho dữ liệu của activation

#### Scenario: New process
- **WHEN** đóng rồi khởi chạy client lần mới
- **THEN** theme bắt đầu System, không tuyên bố đã lưu tùy chọn lâu dài

### Requirement: Responsive reference shell
Shell SHALL giữ hierarchy sidebar/thương hiệu/footer/header của mẫu Dashboard và tái sử dụng design system. UI SHALL dùng được từ 800×600 đến 1440×1000 DIP, có keyboard focus rõ, chữ tiếng Việt đầy đủ và hỗ trợ Light/Dark/System cùng Windows Contrast.

#### Scenario: Reference and narrow viewport
- **WHEN** chạy ở 1440×1000 rồi resize thật xuống 1024×768 và 800×600 DIP
- **THEN** sidebar rộng chuyển compact với pane mở được, sáu mục và hành động vẫn truy cập được, content cuộn/reflow không che action, nhãn dài không mất dấu

#### Scenario: Offline keyboard and theme
- **WHEN** không có mạng, dùng Tab/Shift+Tab/Enter/Space, đổi theme và text scale 100%,150%,200%
- **THEN** font/icon có sẵn, focus và selected state phân biệt được, nhãn “Danh mục và Kho” cùng dấu tiếng Việt đọc được, không có keyboard trap ngoài modal

### Requirement: Isolated behavioral verification
Navigation, lifecycle, dialog contracts và ViewModel state SHALL kiểm thử được qua production logic không cần UI thread, API hay database. ViewModel MUST NOT sở hữu UI controls hoặc thực hiện HTTP trực tiếp.

#### Scenario: Headless run
- **WHEN** chạy suite frontend với fake activation/presenter có completion điều khiển được
- **THEN** kiểm chứng được route/state/cancellation/late results mà không tạo cửa sổ hay network request

### Requirement: Build specific shell acceptance
Shell SHALL có bằng chứng runtime trên Windows native và Skia Linux gắn với build cụ thể. Chủ dự án SHALL nghiệm thu giao diện bằng task riêng; acceptance predecessor MUST NOT tự động áp dụng cho Shell mới.

#### Scenario: Technical evidence
- **WHEN** chạy kiểm tra navigation/dialog/resize/theme
- **THEN** record ghi build id/hash hoặc commit kèm dirty diff, OS/target, command, actual viewport DIP/scale, expected/actual và ảnh/log; kiểm tra chưa chạy ghi unverified

#### Scenario: Owner confirmation
- **WHEN** kỹ thuật đã kiểm tra nhưng chủ dự án chưa xác nhận build/target đã xem
- **THEN** task owner acceptance vẫn unchecked; chỉ tick khi có xác nhận kèm ngày, build/target và kết quả hoặc ngoại lệ
