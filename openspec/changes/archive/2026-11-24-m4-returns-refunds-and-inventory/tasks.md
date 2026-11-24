# Tasks

## 1. Sprint 1 — Bàn giao hợp đồng và migration

- [x] 1.1 Bàn giao cho Đạt/Q. Vinh chữ ký thao tác Inventory, thứ tự khóa và mẫu kết quả release, nhận sửa, trả máy, xuất thay. Mỗi caller có ví dụ thành công, conflict và replay với cùng mã nghiệp vụ.
- [x] 1.2 Đối chiếu với Vy/Đạt hợp đồng đảo điểm, ghi chi Cash theo người hoàn và mã lần hoàn. Ghi fixture cho Manager có/thiếu ca, khách lẻ và đơn Processing chưa cộng điểm. Xác nhận đường mở ca riêng cho Manager với Đạt.
- [x] 1.3 Bàn giao cho Bình fixture hoàn một phần có tiền hàng, giảm giá, thuế, RefundedAt, lượng nhập lại, giá vốn gốc và thời điểm movement. Đối chiếu cả trường hợp hai mốc khác ngày và giá vốn thiếu.
- [x] 1.4 Tạo persistence cho yêu cầu chờ, phân bổ khoản hoàn, đối soát, kết quả kiểm định và liên kết đổi hàng. Kiểm tra migration và DBML trên DB sạch lẫn dữ liệu M3, kể cả đơn cũ có phân bổ mặc định bằng 0 và khoản hoàn đã tồn tại.

## 2. Sprint 1 — Thao tác kho và IMEI dùng chung

- [x] 2.1 Mở rộng Inventory để giải phóng giữ chỗ theo đúng lượt giữ. Test IMEI và hàng theo lượng, hai đơn giữ cùng SKU, gọi lặp và rollback. Đối chiếu reserved_quantity, quantity và ledger.
- [x] 2.2 Thêm nhận hàng trả, kiểm định từng phần và nhập lại trong transaction của caller. Test ba kết quả bán lại/lỗi/sửa, đúng CustomerReturn, IsRestocked, thời điểm và số lượng sau khi thử lại.
- [x] 2.3 Thêm nhận sửa, trả máy và xuất máy thay cho Q. Vinh. Test giữ hạn bảo hành, máy thay sai biến thể, xuất lặp, máy lỗi không bán được và số tồn bằng InStock + Reserved. Trả mã movement và giá vốn cho hồ sơ.
- [x] 2.4 Kiểm tra đường báo lỗi/khôi phục IMEI hiện có với máy của khách đang sửa hoặc đã đổi bảo hành. Test việc nhập lại không bỏ qua quyết định kiểm định hoặc làm tăng tồn trùng.

## 3. Sprint 1 — API trả hàng và hoàn tiền

- [x] 3.1 Tạo API yêu cầu/tra cứu lịch sử với lý do, dòng bán, IMEI, lượng và khóa chống lặp. Test thiếu token, sai role, Sales truy cập đơn người khác, lượng 0/âm/quá còn lại, sai IMEI và cùng mã khác payload.
- [x] 3.2 Tính tiền hoàn từ snapshot từng dòng. Test trả nhiều lần có phần lẻ, lượt cuối, dòng giá 0 và đơn cũ thiếu phân bổ. Tổng hoàn hàng/thuế khớp số gốc, không vượt số đã trả.
- [x] 3.3 Triển khai chốt Cash trong một UnitOfWork với các service của Đạt/Vy và Inventory. Test Completed → PartiallyRefunded → Refunded, Processing chỉ hoàn hết và giải phóng giữ chỗ, rollback khi một service lỗi. Kiểm tra PaidAt, CompletedAt, dòng gốc, điểm và lượt voucher.
- [x] 3.4 Lưu ý định chi ngân hàng, gọi ngoài transaction, xác minh mã tham chiếu và đối soát. Test ngân hàng từ chối, mất phản hồi, DB lỗi sau khi ngân hàng thành công và hai lần xác nhận cùng mã. Đếm đúng một lần chi và một lần ghi kết quả.
- [x] 3.5 Liên kết lần hoàn thương mại với đơn mới do checkout tạo. Test checkout lỗi sau khi hoàn, tiếp tục bằng mã cũ, không hoàn lại lần hai.
- [x] 3.6 Công bố API kho tra cứu/ghi kiểm định và cập nhật bảng quyền, baseline endpoint và DTO. Kiểm tra Warehouse thao tác trên phần hàng chờ, không được duyệt chi hay đọc dữ liệu tài chính của đơn qua API kho.

## 4. Sprint 1 — Giao diện cho Sales, quản lý và kho

- [x] 4.1 Thêm client repository, ViewModel và màn hình gửi yêu cầu/lịch sử. Test headless phạm vi đơn, số liệu từ server, lỗi theo dòng, hủy tải khi đổi phiên và tra cứu lại yêu cầu sau mất kết nối.
- [x] 4.2 Thêm thao tác duyệt Cash/ngân hàng, trạng thái đối soát và liên kết đổi hàng cho người có quyền. Kiểm tra trên ứng dụng rằng Sales không thấy nút chi, Manager thiếu ca thấy lý do và bấm lặp không tạo mã chi mới.
- [x] 4.3 Thêm giao diện Warehouse kiểm định theo IMEI hoặc số lượng. Chạy trên Skia, kiểm tra chọn đúng SKU/IMEI, binding, bàn phím, Light/Dark và mở lại phiếu xử lý dở để xác nhận đúng lượng còn lại.

## 5. Sprint 2 — Tích hợp và nghiệm thu trên dữ liệu thật

- [x] 5.1 Thay các điểm nối tạm bằng service thật của Đạt/Vy. Chạy luồng bán hai món, thu 1.980.000đ, hoàn hai lần 990.000đ và đảo mỗi lần 9 điểm. Đối chiếu với Bình mức lãi 300.000đ khi nhập lại một món và −300.000đ khi chưa nhập, tách theo ngày thực nhập.
- [x] 5.2 Chạy HTTP trên PostgreSQL test riêng bằng các kết nối độc lập cho hai lần hoàn tranh lượng cuối, trả và bảo hành tranh IMEI, chi Cash tranh đóng ca và hoàn Processing tranh giao hàng. Đối chiếu số lượng, ledger, ca, điểm, lịch sử sau commit/rollback.
- [x] 5.3 Cùng Đạt và Q. Vinh chạy QR hết hạn/hủy, nhận sửa/trả/đổi máy, đối soát ngân hàng và gửi lại sau mất phản hồi qua UI/API. Ghi kết quả từng ca và chạy lại lỗi sửa trên cùng build.

## 6. Sprint 3 — Việc An tổng hợp để bàn giao

- [x] 6.1 Ghép migration, seed và dữ liệu demo của sáu thành viên, giữ fixture tháng 10/2026. Cài DB sạch và seed hai lần, đối chiếu tối thiểu 20 sản phẩm, 5 danh mục, 10 khách, 20 đơn, bốn role và đủ giao dịch Cash/QR/hoàn/bảo hành trong kỳ báo cáo.
- [x] 6.2 Tổng hợp báo cáo hồi quy M2–M4 gồm test ID, input, expected/actual, trạng thái, build/OS, bằng chứng và kết quả chạy lại. Kiểm tra mẫu số tỷ lệ pass và ghi riêng trường hợp skipped/chưa chạy. Đối chiếu luồng từ đăng nhập đến doanh thu và ma trận quyền server.
