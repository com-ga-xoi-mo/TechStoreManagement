# Proposal

## Why

Bình cần chứng minh Dashboard Manager phản ánh một đơn POS Cash vừa phát sinh từ luồng nhập và bán thật trên PostgreSQL. Hai change trước đã kiểm chứng truy cập Dashboard và đối soát fixture cố định, nên lần này tập trung vào thay đổi số liệu sau commit và sau lỗi checkout.

## What Changes

- Chạy luồng nhận hàng có IMEI → mở ca → tìm hàng, chọn khách → checkout Cash → đóng ca → Manager đọc lại Dashboard. Kiểm tra số đơn tăng đúng 1, doanh thu tăng đúng `TotalAmount` đã lưu, đơn xuất hiện trong 5 đơn gần nhất và doanh thu vào đúng ngày UTC+07.
- Chạy ca checkout IMEI đã Sold với khóa giao dịch mới và ca còn mở. Xác nhận HTTP 409 và đối chiếu kho, ca, đơn, lịch sử và Dashboard trước/sau.
- Đối chiếu dữ liệu phiếu nhập, đơn, IMEI, hàng giữ chỗ, lịch sử kho, bảo hành, giá vốn và tiền ca theo contract hiện có. Ghi sai lệch theo owner để Đạt, Q. Vinh hoặc Vy xử lý phần phụ trách.
- Chuẩn bị luồng POS/Dashboard có thể chạy với thiết bị `InStock` đã có trong fixture để kiểm chứng trong khi chờ receiving. Bằng chứng nhận hàng đầu-cuối chờ API và contract bàn giao của Q. Vinh có mặt trong build chạy test.
- Lưu đầu vào, Expected/Actual, status, build SHA và log cho từng ca. Ghi riêng bằng chứng HTTP và thao tác desktop. Dùng bằng chứng phân quyền từ `m3-manager-dashboard-access`.

## Capabilities

### New Capabilities

Không có. Đây là change kiểm thử và bàn giao.

### Modified Capabilities

Không có thay đổi contract. Tiêu chí kiểm chứng lấy từ `admin-analytics-api`, `pos-cash-checkout`, `pos-shift-management`, `inventory-fulfillment` và hợp đồng nhập hàng của Q. Vinh sau khi bàn giao. Change khai báo `skip_specs: true`.

## Impact

- Thêm lớp test riêng của Bình `tests/Backend.UnitTests/DashboardLiveFlowPostgresHttpTests.cs` để đối chiếu Dashboard sau giao dịch POS, dùng database riêng và JWT từ login thật. Tổng hợp bằng chứng trong `docs/M3_DASHBOARD_LIVE_FLOW.md`, liên kết từ `docs/M3_DASHBOARD_DATA_TEST_HANDOFF.md`.
- Chỉ thêm hoặc sửa file thuộc phạm vi Bình: lớp test mới, helper test riêng nếu cần và hai tài liệu trên. Không sửa file của owner khác, gồm `PosCheckoutPostgresHttpTests` và code POS/ca của Đạt, receiving/kho của Q. Vinh, Customers/công thức tiền của Vy, Catalog search của T. Vinh và helper Identity/test của An như `IdentityPostgresApi`. Các file đó chỉ được gọi lại.
- Build chạy đầu-cuối cần ghép Dashboard Manager đã apply, POS/ca của Đạt, receiving của Q. Vinh và API tìm khách của Vy. Kiểm chứng giảm cấp đơn trên POS còn phụ thuộc Đạt nối checkout với contract của Vy.
- Phạm vi của Bình là test và bàn giao. Receiving, nghiệp vụ kho, checkout, công thức tiền và tính năng desktop của các owner được kiểm chứng qua các đường hiện có.
