# Spec Delta

## ADDED Requirements

### Requirement: Financial report opens inside the live Overview
Ứng dụng desktop SHALL cung cấp chế độ “Báo cáo tài chính” trong route Tổng quan hiện có cho Admin và Manager có phiên Active ở chế độ live. Dashboard hiện tại SHALL là nội dung mặc định và giữ nguyên nguồn dữ liệu, kỳ, KPI, trình bày, preview và điều hướng.

#### Scenario: Authorized user opens the report
- **WHEN** Admin hoặc Manager mở Tổng quan rồi chọn “Báo cáo tài chính”
- **THEN** client tải báo cáo tại chỗ, không tạo route hay mục menu mới
- **AND** khi quay về nội dung Tổng quan, các lựa chọn kỳ và trạng thái Dashboard của activation hiện tại vẫn còn

#### Scenario: Unavailable role, preview or signed-out session
- **WHEN** role là SalesStaff, WarehouseStaff hoặc chưa có phiên đủ quyền, hay Dashboard đang ở chế độ preview
- **THEN** lựa chọn báo cáo không xuất hiện và client không gọi endpoint báo cáo, kể cả khi lệnh mở báo cáo được gọi gián tiếp

### Requirement: Financial report uses an explicit Vietnam calendar range
Báo cáo SHALL có hai ngày theo lịch Việt Nam và nút “Áp dụng”. Yêu cầu gửi `period=custom` với `from`, `to` dạng `yyyy-MM-dd`, bao gồm hai đầu và tối đa 31 ngày. Kỳ mặc định khi mở lần đầu của activation là bảy ngày tính đến hôm nay theo UTC+07.

#### Scenario: Unsubmitted or invalid dates
- **WHEN** người dùng sửa ngày nháp, nhập sai định dạng, chọn kỳ ngược, hơn 31 ngày hoặc có ngày cuối sau hôm nay theo UTC+07
- **THEN** client không gửi query mới cho đến khi khoảng hợp lệ được áp dụng, báo lỗi tại trường liên quan và giữ nhãn kỳ của snapshot hay request đang áp dụng

#### Scenario: Refresh and retry after editing draft dates
- **WHEN** người dùng đổi nội dung nháp nhưng chưa áp dụng rồi bấm “Làm mới” hoặc “Thử lại”
- **THEN** client dùng đúng hai ngày đã áp dụng cho yêu cầu hoặc snapshot hiện tại

### Requirement: Financial report presents server-calculated figures with snapshot metadata
Desktop SHALL hiển thị trực tiếp kỳ, `asOf` UTC+07, tổng kỳ và từng ngày từ API báo cáo. Các chỉ số doanh thu, hoàn, thuế, dòng tiền, giá vốn, lãi gộp và bảo hành SHALL đọc từ trường API tương ứng. Desktop SHALL giữ thứ tự xếp hạng và các dòng hiệu suất thu ngân do server trả.

#### Scenario: Sales and after-sale numbers appear separately
- **WHEN** API trả số bán gốc và số liệu hậu mãi
- **THEN** báo cáo tách tiền hàng bán, tiền hàng hoàn, thuế bán/hoàn, tiền thu/chi, Cash/QR khi có, giá vốn bán/nhập lại/thuần, lãi gộp trước và sau hoàn, và chi phí bảo hành riêng
- **AND** hiển thị hai bảng bán chạy theo số lượng và doanh thu, hiệu suất người bán và các dòng Cash theo ca nếu API cung cấp
- **AND** client không tự tính số thuần, giá vốn, lãi, tiền két hoặc sắp lại top từ các dòng

#### Scenario: Financial values are readable without relying on color
- **WHEN** báo cáo có số 0, âm hoặc phần lẻ hợp lệ
- **THEN** tiếng Việt, định dạng VND, dấu âm và phần lẻ cần thiết đều được giữ khi đọc bảng và tổng kỳ

### Requirement: Financial report distinguishes emptiness, unavailable amounts and failure
Báo cáo SHALL có trạng thái tải, hiển thị, rỗng và lỗi độc lập với Dashboard. Client SHALL hiển thị số 0 thành 0 ₫, số nullable thiếu thành “Chưa tính được”, danh sách rỗng bằng thông báo riêng, và lỗi tải bằng thông báo an toàn có thể phục hồi khi còn đủ quyền.

#### Scenario: Successful empty range or legacy response
- **WHEN** kỳ thành công có toàn bộ chỉ số hoạt động khả dụng bằng 0 và danh sách rỗng
- **THEN** báo cáo cho biết không có số liệu phát sinh khác 0, vẫn giữ kỳ, `asOf` và các dòng ngày 0
- **AND** khi có trường chưa khả dụng, báo cáo giữ phần đã biết và đánh dấu thiếu, không suy ra kỳ không có giao dịch từ trường thiếu

#### Scenario: Current request fails
- **WHEN** API trả lỗi quyền, lỗi kỳ, dữ liệu sai cấu trúc, lỗi mạng hoặc timeout
- **THEN** báo cáo nêu lỗi phù hợp trong chế độ báo cáo, không nạp fixture, không gắn số cũ vào kỳ mới và không chuyển thành kỳ rỗng
- **AND** 403 giữ phiên hiện tại, 401 được xử lý theo luồng xác thực hiện có

### Requirement: Financial requests respect the selected period and session lifetime
Report SHALL cho phép áp dụng kỳ hợp lệ mới khi đang tải và hủy yêu cầu trước. Chỉ yêu cầu mới nhất thuộc activation, chế độ báo cáo và generation phiên còn hợp lệ SHALL thay đổi dữ liệu, lỗi hoặc loading. Yêu cầu bị hủy dự kiến SHALL không hiển thị lỗi.

#### Scenario: Older success or failure arrives after a newer request
- **WHEN** request kỳ A vẫn trả thành công hoặc lỗi sau khi người dùng đã gửi kỳ B
- **THEN** nhãn kỳ, snapshot, lỗi và loading chỉ phản ánh B, kể cả khi transport bỏ qua cancellation

#### Scenario: Logout, account replacement, route exit or app close
- **WHEN** generation phiên đổi, người dùng rời báo cáo, rời trang hoặc đóng app trong lúc tải
- **THEN** request báo cáo bị hủy và kết quả muộn không hiển thị. Logout hoặc đổi tài khoản xóa kỳ và số liệu cũ. Tài khoản mới vào Tổng quan ở trạng thái mặc định
- **AND** đổi chế độ, retry hoặc tải báo cáo không hủy hoặc thay đổi request riêng của Dashboard

### Requirement: Financial report remains accessible across desktop targets
Báo cáo SHALL hỗ trợ thao tác bằng bàn phím, focus nhìn rõ và bố cục đọc được khi đổi kích thước hoặc theme trên Skia desktop và Windows native. Thay đổi trình bày SHALL không tải lại dữ liệu hay đổi kỳ đang xem.

#### Scenario: Keyboard, narrow window and live theme switch
- **WHEN** người dùng chọn báo cáo, nhập ngày, retry và đọc số liệu bằng bàn phím ở kích thước 800×600, 1024×768 hoặc 1440×1000 DIP rồi đổi Sáng/Tối khi trang vẫn mở
- **THEN** focus không rơi vào panel ẩn, toàn bộ hàng/cột cần thiết còn đọc được và màu cập nhật ngay trên cùng snapshot
