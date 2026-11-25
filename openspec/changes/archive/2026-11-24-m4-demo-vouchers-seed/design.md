# Design

## D1. Mỗi trạng thái một voucher, mã bắt đầu bằng `DEMO-`

Màn Voucher cần thấy cả bốn trạng thái (Đang hoạt động, Hết hạn, Hết lượt, Đã tắt) và kiểm tra khi bán cần cả voucher phần trăm có trần lẫn số tiền cố định. Mã `DEMO-*` để Admin nhận ra dữ liệu mẫu và để test đếm được.

- `DEMO-EXPIRED` hết hạn lúc neo − 1 ngày (17/10/2026), nên luôn hết hạn với mọi ngày chạy, không phụ thuộc đồng hồ.
- Hai voucher hết hạn/hết lượt có `UsedCount` cố định mà không có dòng `voucher_redemptions`. Ràng buộc schema chỉ yêu cầu `used_count + reserved_count ≤ usage_limit`; nối với đơn thật là việc của luồng bán (Đạt). README ghi rõ giới hạn này.
- Giá trị của `DEMO-OCT26` giữ nguyên như seed cũ, để DB đã seed không đổi nghĩa.

## D2. Voucher đã có thì giữ nguyên, không báo xung đột

Seed cũ báo xung đột khi `DEMO-OCT26` đang tắt, đổi giá trị hoặc `UsedCount ≠ 0`. Đó là mô hình "seed sở hữu dữ liệu". Với voucher thì ngược lại: Admin/Manager sở hữu sau khi được tạo, và dùng voucher khi bán làm `UsedCount` tăng. Giữ luật cũ khiến seed hỏng ngay lần bán đầu tiên.

Quyết định: khóa nhận dạng duy nhất là mã. Có dòng thì bỏ qua, không đọc giá trị. Hai dòng cùng mã (không phân biệt hoa thường, bỏ khoảng trắng) vẫn ném lỗi rõ ràng như các thực thể khác. Dòng bị xóa được thêm lại ở lần chạy sau (cùng cách với mọi dòng seed khác).

Đã cân nhắc: chỉ nới `IsActive`/`UsedCount` mà vẫn so `DiscountValue`. Bỏ, vì màn Sửa voucher cho đổi giá trị nên vẫn gây xung đột.

## D3. Khách hàng demo không đổi

Kế hoạch yêu cầu giữ số dư ban đầu và lịch sử điểm cũ. Thêm khách (ví dụ một khách Vàng và một Bạch kim cho đủ bậc VIP) sẽ làm sai các test đếm của người khác (`Customers.Should().HaveCount(10)`, số dòng sổ `7`), nên không làm trong change này.

Phát hiện ghi lại để người chủ seed quyết định: hạng của sáu trong tám khách `Demo Customer 01–08` lệch với ngưỡng điểm (ví dụ `Demo Customer 04` là Vàng với 400 điểm, ngưỡng Vàng là 1.500). Hạng sẽ tự về đúng ở lần đổi điểm đầu tiên vì `LoyaltyRules.TierFor` tính lại từ điểm.

## Việc về sau

- Quyết định có sửa hạng của khách demo cho khớp ngưỡng và thêm khách Vàng/Bạch kim hay không (đụng test đếm của người khác).
