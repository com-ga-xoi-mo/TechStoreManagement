# Proposal

## Why

Admin và Manager cần xem báo cáo tài chính trong desktop để đối chiếu bán, hoàn, thuế, tiền thu/chi và giá vốn từ cùng snapshot của API. Tổng quan hiện có là điểm vào phù hợp để bổ sung báo cáo mà vẫn giữ luồng sử dụng Dashboard M3.

## What Changes

- Thêm chế độ **Báo cáo tài chính** bên cạnh nội dung Tổng quan bên trong `DashboardPage`. Mặc định vẫn mở Dashboard hiện tại. Báo cáo chỉ hiện với Admin/Manager đang đăng nhập ở chế độ live.
- Hiển thị kỳ, giờ cập nhật UTC+07, tổng kỳ, theo ngày, sản phẩm bán chạy và hiệu suất người bán. Dùng số API cho bán/hoàn, thuế, dòng tiền, giá vốn, lãi gộp và bảo hành, với hiển thị riêng cho số 0, chưa tính được, kỳ rỗng và lỗi tải.
- Tách kỳ nháp khỏi kỳ đã áp dụng. Cho phép thay yêu cầu đang tải bằng kỳ mới, retry đúng kỳ, và bỏ kết quả cũ khi đổi phiên hoặc rời trang.
- Kiểm thử headless từ DTO/JSON mẫu phần bán và nghiệm thu live với build tích hợp change 2. Ghi riêng bằng chứng Skia và Windows native trong `docs/M4_FINANCIAL_REPORT.md`.

## Capabilities

### New Capabilities

### Modified Capabilities

- `frontend-dashboard-preview` — bổ sung chế độ báo cáo live, trạng thái tải/rỗng/lỗi và quy tắc trình bày trong Tổng quan hiện có.

## Impact

- Phạm vi apply gồm View/ViewModel Dashboard, model/repository và đăng ký DI chuyên biệt cho Dashboard/Analytics, test của Bình và `docs/M4_FINANCIAL_REPORT.md`.
- Dùng lại `GET /api/analytics/financial-report`, hợp đồng phần bán và trường bổ sung của change 2. Dashboard M3, Shell/menu/route, quyền server, API và shared DTO giữ hợp đồng hiện hành.
