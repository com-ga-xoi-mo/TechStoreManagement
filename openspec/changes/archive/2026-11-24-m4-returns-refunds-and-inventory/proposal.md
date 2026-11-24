# Proposal

## Why

M3 đã lưu giá bán, giảm giá, thuế từng dòng và IMEI đã bán. M4 cần dùng các số liệu đó để hoàn đúng số tiền khách đã trả và xác định lúc nào hàng trả thực sự có thể bán lại.

## What Changes

- Sales lập yêu cầu trả cho đơn thuộc phạm vi của mình. Admin/Manager xử lý hoàn từng phần hoặc toàn bộ qua Cash/ngân hàng. Màn hình trả hàng hiển thị lịch sử yêu cầu, kết quả chi và số lượng còn được trả.
- An điều phối hoàn tiền, ghi nhận hàng trả, giải phóng giữ chỗ với đơn chưa giao và liên kết đơn bán mới khi khách đổi hàng thương mại.
- Bổ sung thao tác Inventory để Đạt giải phóng giữ chỗ và Q. Vinh nhận/trả máy sửa, xuất máy thay. Warehouse kiểm định từng phần hàng trả trước khi nhập lại.
- Bàn giao số tiền hoàn, thuế, thời điểm chi và thời điểm nhập kho cho Bình. Sprint 2 đối chiếu với ca của Đạt và điểm của Vy. Trong sprint 3, An ghép migration, dữ liệu demo và tổng hợp kết quả kiểm thử hồi quy.

## Capabilities

### New Capabilities

- `order-returns` — yêu cầu, duyệt chi, lịch sử trả hàng, đối soát ngân hàng, đổi hàng thương mại và dữ liệu đối chiếu.

### Modified Capabilities

- `inventory-fulfillment` — giải phóng giữ chỗ, nhận và phân loại hàng trả, nhận/trả máy sửa và xuất máy thay trong giao dịch của bên gọi.
- `pos-shift-management`, `api-role-authorization` — cho Manager mở ca hoàn Cash, trừ tiền đã chi khi đóng ca và kiểm soát quyền kết thúc lượt giữ QR.
- `frontend-order-management` — bổ sung màn hình nghiệp vụ trả/hoàn theo vai trò, truy cập độc lập với thao tác đổi trạng thái bán.
- `admin-analytics-api`, `customer-management` — giữ doanh số gốc và lịch sử mua theo ngày giao khi đơn chuyển sang trạng thái đã hoàn.

## Impact

Phạm vi gồm Orders, Inventory, hợp đồng DTO dùng chung, client Uno và migration PostgreSQL. Đạt sở hữu ca/checkout, Vy sở hữu điểm và phân bổ khi bán, Q. Vinh sở hữu quyết định bảo hành, Bình sở hữu báo cáo. An bàn giao và kiểm thử các điểm nối theo [phân công M4](../../../../docs/MILESTONE_4_PLAN.md).

Theo quyết định triển khai khi các nhánh M4 liên quan chưa có, thay đổi này bao gồm cả service ghi chi Cash, ghi và đảo điểm, kết thúc lượt giữ VietQR đã tồn tại và API bảo hành cần cho luồng của An. Hoàn chuyển khoản ghi nhận khoản chi bên ngoài bằng mã tham chiếu, số tiền và thời điểm chi đã xác minh.
