# Proposal

## Why

Bộ seed phục vụ demo M3 đã có danh mục, đơn hàng và giá vốn nhưng chưa có ảnh sản phẩm mở được. Bình cần một bộ số liệu tính tay kiểm chứng qua Analytics API để phân biệt kết quả cố định của fixture với số liệu live tại ngày demo.

## What Changes

- Xác nhận tối thiểu 20 sản phẩm có giá vốn, 5 danh mục, 10 khách hàng, 20 đơn nhiều trạng thái và bốn tài khoản demo `admin`, `manager1`, `cashier1`, `warehouse1`. Giữ 50 IMEI trong fixture hiện tại như một mốc hồi quy.
- Bổ sung ảnh bìa cho các SKU demo còn thiếu. Nhóm tự tạo ảnh placeholder theo danh mục và đưa file mẫu vào repo. `DataSeeder` tải ảnh lên qua provider `IObjectStorage` đang cấu hình và lưu tham chiếu qua `product_images`. Local là đường demo mặc định. Bàn giao hướng dẫn R2 riêng với database và storage demo cùng cấu hình.
- Mở rộng các ca seeding PostgreSQL hiện có để kiểm tra DB mới, rerun, seed thiếu một phần và DB cũ. Kiểm tra dữ liệu người dùng, khóa tự nhiên, timestamp, rollback và liên kết đơn/VietQR/kho/IMEI/ca.
- Đối chiếu bốn kỳ Analytics, gồm khoảng ngày tùy chọn đã có, với bảng Expected/Actual tính độc lập tại `2026-10-18T05:00:00Z`. Hướng dẫn demo thêm cách đọc snapshot live theo kỳ lịch sử trên DB seed.
- Ghi rõ giá trị thuế/giảm giá bằng 0 là thuộc tính của fixture. Đối chiếu công thức tiền của Vy trên đơn POS thật thuộc `m3-dashboard-live-flow`.

## Capabilities

### New Capabilities

Không có capability mới.

### Modified Capabilities

- `data-seeding`: Bổ sung ảnh demo qua storage, kiểm chứng giá vốn và dữ liệu seed, cập nhật vai trò của mốc 50 IMEI và kiểm chứng Analytics theo contract hiện có.
- `frontend-dashboard-preview`: Đồng bộ mô tả chỉ tiêu seed trong bàn giao Dashboard với kế hoạch M3 và làm rõ 50 IMEI là số lượng fixture hiện tại.

## Impact

- Code apply tập trung vào ảnh seed trong `backend/Common/Data`, các port Catalog/storage hiện có và asset mẫu trong backend. Giữ tệp manifest hiện có theo quy tắc chỉ nối thêm dòng.
- Mở rộng `DemoSeed*Tests`, `DataSeederTests` và `DataSeederPostgresIntegrationTests`. Nghiệm thu dùng PostgreSQL và Local storage cô lập, kèm hướng dẫn kiểm tra R2 khi có cấu hình test.
- Đầu ra bàn giao gồm bảng đối chiếu, lệnh chạy lại và hướng dẫn demo. Cách tính KPI và API/DTO đang chạy của Analytics được giữ nguyên.
