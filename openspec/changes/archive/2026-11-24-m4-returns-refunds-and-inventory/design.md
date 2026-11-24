# Design

## Context

`OrderItem` đã lưu `TotalPrice` sau giảm dòng, `OrderDiscountAmount`, `TaxAmount`, `UnitCost` và IMEI. `OrderReturn` hiện ghi một dòng đã hoàn, gồm số lượng, tiền, ca chi, thời điểm và cờ nhập lại. `OrderStatusTransitions` chỉ cho PATCH trạng thái bán. `IInventoryFulfillmentService` đã có kiểm tra, giữ và xác nhận bán trong transaction do caller sở hữu.

## Goals / Non-Goals

An sở hữu giao dịch hoàn và các thao tác tài sản dùng chung. Đạt mở transaction checkout, Q. Vinh mở transaction xử lý bảo hành và quyết định đủ điều kiện sửa/đổi. Vy tính điểm trên phần hàng khách còn giữ. Bình quyết định cách trình bày báo cáo từ dữ liệu đã ghi nhận.

## Decisions

1. **Tách yêu cầu khỏi khoản đã chi.** API đề xuất gồm tạo/tra cứu tại `/api/returns`, chốt chi theo mã yêu cầu, và tra cứu/kiểm định hàng trả qua API Inventory. JWT cung cấp người thao tác. Sales bị giới hạn theo cashier của đơn gốc. DTO cho kho chỉ mang mã phiếu, dòng hàng, IMEI, số lượng và kết quả kiểm định. Tra cứu lịch sử sau khi mất kết nối dùng định danh do client giữ. Endpoint PATCH trạng thái bán vẫn từ chối đích Refunded.
2. **Dùng số liệu đã lưu.** Tiền hàng sau giảm của dòng bằng `TotalPrice - OrderDiscountAmount`. Với từng thành phần, lấy phần lũy kế theo số lượng trả trên lượng bán, làm tròn hai chữ số rồi trừ phần đã hoàn. Khi trả hết, dùng số gốc trừ lũy kế để xử lý phần dư. Xét cả lượng còn lại ở dòng giá 0 khi quyết định hoàn toàn bộ. Với đơn cũ, đối chiếu tổng dòng với tổng đơn và các lượt đã hoàn trước khi chi. Nếu thiếu phân bổ và không đối chiếu được, giữ lại để kiểm tra chứng từ.
3. **Khóa trước khi chi.** Khóa ca chi theo giao ước với Đạt khi cần, khóa đơn, các dòng theo ID rồi IMEI và tồn theo thứ tự của Inventory. Rà cùng caller thứ tự khóa ở đường checkout/đóng ca/bảo hành. Chốt Cash kiểm tra lại phạm vi, số dư và trạng thái, ghi chi của Đạt, nhận hàng đã giao hoặc giải phóng hàng chưa giao, đảo điểm của Vy nếu có, ghi OrderReturn và trạng thái trong một UnitOfWork. Giữ định danh duy nhất theo thao tác và so khớp payload khi replay.
4. **Lưu ý định chuyển khoản trước khi gọi ngân hàng.** Trong DB, giữ quyền xử lý phần số lượng, tiền và IMEI bằng mã lần hoàn trước khi gửi. Các đường trả hàng, giao đơn, bảo hành phải kiểm tra phần đang giữ trong cùng quy ước khóa. Gọi ngân hàng ngoài transaction với cùng khóa chống lặp. Lỗi dứt khoát giải phóng phần đang giữ. Mất phản hồi giữ trạng thái chờ đối soát. Tra cứu mã tham chiếu và số tiền thực chi để hoàn tất transaction nội bộ. Nếu kênh chỉ hỗ trợ đối soát thủ công, lưu chứng từ/mã đã xác minh và ghi rõ chế độ khi demo.
5. **Nhận hàng và nhập lại có hai mốc.** Khi nhận hàng đã giao, ghi nhận số lượng hoặc chuyển IMEI Sold → Returned. Kiểm định có mã thao tác riêng. Với lô có nhiều kết quả, tách phần số lượng theo quy tắc mỗi bản ghi có một kết quả nhập lại của `schema.dbml`, giữ chung mã lần hoàn và bảo toàn tổng tiền/thuế/số lượng cả lô. Chỉ phần nhập lại tạo movement CustomerReturn liên kết dòng trả, ghi IsRestocked và thời điểm cùng transaction. Chuyển UnderRepair → Sold để trả khách chỉ áp dụng cho máy thuộc hồ sơ bảo hành của khách. Kiểm tra nguồn gốc trước khi cho phép restore thiết bị lỗi qua API kho hiện tại.
6. **Bàn giao contract sớm.** Đạt nhận mã lượt giữ và các dòng để release, trả kết quả thành công hoặc conflict theo dòng. Q. Vinh gửi mã hồ sơ, đơn/dòng, IMEI cũ/mới và quyết định, nhận mã movement và giá vốn có thể null. Vy nhận mã lần hoàn, đơn/khách và số tiền hàng còn giữ chưa thuế. Đạt nhận mã lần hoàn, người chi và số tiền Cash. Bình nhận số liệu theo hai mốc RefundedAt và thời điểm movement, cùng UnitCost gốc. Giá vốn chỉ truyền nội bộ. Đổi hàng thương mại dùng mã lần hoàn làm liên kết khi gọi checkout đơn mới, cho phép tiếp tục nếu hoàn đã xong nhưng checkout chưa thành công.

## Risks / Trade-offs

- **Manager và ca Cash.** Theo quyết định tích hợp ngày 24/11, module Sales cấp quyền mở/đọc ca riêng cho Manager, ghi chi một lần theo mã phiếu và trừ khoản chi khi đối soát. Hai đường bán POS vẫn dành cho Admin/Sales. Module Customers khóa khách hàng, ghi cộng điểm theo mã đơn và đảo theo mã lần hoàn. An dùng hai service này trong cùng transaction.
- **Tồn giữ theo tổng.** Chỉ kiểm tra reserved_quantity có thể giải phóng nhầm giữ chỗ của đơn khác. Dùng dòng đơn/lượt giữ bền vững của checkout để xác nhận quyền sở hữu và lượng chưa giải phóng, kể cả với hàng không có IMEI.
- **Thiết bị đã bán lại.** Cùng IMEI có thể xuất hiện trên một đơn mới. Kiểm tra liên kết lần bán, lượng từng dòng đã trả, hồ sơ đang mở và trạng thái IMEI dưới khóa trước khi nhận.

## Migration Plan

Bổ sung bảng yêu cầu/lần chi, định danh đối soát, liên kết đổi hàng và dữ liệu phân bổ/kiểm định còn thiếu. Dùng khóa ngoại có index, ràng buộc duy nhất chống lặp, decimal(18,2) và UTC. Giữ các khoản hoàn, lịch sử điểm, dữ liệu seed và đơn cũ. Kiểm tra migration trên DB sạch và bản M3 có dữ liệu, đối chiếu trước/sau rồi cập nhật `docs/database/schema.dbml`. Khi cần dừng phát hành, ngừng nhận yêu cầu mới, bảo toàn chứng từ và xử lý các khoản đang đối soát trước khi quay về phiên bản ứng dụng tương thích.
