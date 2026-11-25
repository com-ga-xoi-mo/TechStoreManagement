# Spec Delta

## MODIFIED Requirements

### Requirement: Thao tác thiết bị cho bảo hành

Inventory SHALL nhận/trả máy sửa và xuất máy thay theo quyết định của luồng bảo hành trong transaction của Q. Vinh. Số tồn vật lý cho hàng serial bằng số IMEI InStock + Reserved sau mỗi lần commit.

#### Scenario: Nhận máy sửa rồi trả khách
- **WHEN** máy Sold được nhận sửa và trả đúng khách
- **THEN** IMEI đi qua UnderRepair rồi về Sold, giữ hạn bảo hành cũ, không tăng tồn hay ghi bán/hoàn.

#### Scenario: Xuất máy thay
- **WHEN** hồ sơ được duyệt đổi và máy thay InStock khớp sản phẩm/biến thể
- **THEN** máy cũ thành Defective, máy thay thành Sold và nhận `warranty_months`, `warranty_start_at`, `warranty_end_at` của máy cũ (hạn bảo hành còn lại, không kéo dài), tồn giảm một, trả mã biến động và giá vốn máy thay cho hồ sơ liên kết hai IMEI với đơn gốc.

#### Scenario: Hai luồng tranh cùng thiết bị
- **WHEN** yêu cầu trả và yêu cầu bảo hành cùng nhận một IMEI, hoặc hai hồ sơ cùng chọn một máy thay
- **THEN** chỉ một giao dịch nhận được thiết bị, giao dịch xung đột không để lại thay đổi kho.

#### Scenario: Quyền nhận bảo hành theo dòng bán
- **WHEN** nhân viên được phép tiếp nhận bảo hành (Admin, Manager hoặc SalesStaff) mở hồ sơ nhận một IMEI trong thời hạn bảo hành
- **THEN** hệ thống kiểm tra khách, dòng đã giao và thiết bị khách còn giữ. Hệ thống từ chối nhận lại thiết bị mà khách đã trả, kể cả khi đó là dòng trên chứng từ hoàn cũ.

#### Scenario: Tra cứu và thử lại quyết định bảo hành
- **WHEN** người dùng gửi lại quyết định đã thực hiện với cùng mã hồ sơ và máy thay
- **THEN** nhận lại hồ sơ đã ghi, gồm hạn bảo hành gốc và mã biến động kho. Một quyết định khác trên cùng hồ sơ đã đóng trả 409.
