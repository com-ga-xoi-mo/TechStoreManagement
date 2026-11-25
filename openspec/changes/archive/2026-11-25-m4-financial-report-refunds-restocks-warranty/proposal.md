# Proposal

## Why

Báo cáo tài chính của Bình đã có số bán và các trường dành cho hậu mãi. M4 cần nối khoản thực chi, hàng thực nhập lại và giá vốn máy thay để đối chiếu doanh thu thuần, vốn thuần, lãi gộp và tiền két theo chứng từ đã lưu.

## What Changes

- Điền các trường hoàn, nhập lại, bảo hành và số thuần của `GET /api/analytics/financial-report`. Tiền hoàn theo ngày chi thực tế. Đơn thanh toán rồi hoàn trước khi giao chỉ ảnh hưởng dòng tiền.
- Ghi giảm giá vốn vào ngày hàng quay lại tồn bán được. Hiển thị giá vốn máy thay bảo hành riêng. Giữ số bán gốc, quyền truy cập, kỳ báo cáo và KPI Dashboard hiện hành.
- Bổ sung thuế hoàn và số thu/chi Cash theo ca, tách dòng QR khỏi két. Giữ tương thích tên, kiểu và cấu trúc các trường hiện có.
- Đối chiếu Expected tính tay với HTTP trên PostgreSQL. Ghi kết quả tiếp vào `docs/M4_FINANCIAL_REPORT.md`, gồm owner và điều kiện chạy lại cho phần chưa kiểm chứng.

## Capabilities

### New Capabilities

### Modified Capabilities

- `admin-analytics-api` — mở rộng báo cáo tài chính sang số liệu hậu mãi và dòng tiền theo kênh/ca, thay quy tắc các trường hậu mãi luôn rỗng.

## Impact

- Khi apply, giới hạn thay đổi ở `backend/Analytics/`, test báo cáo của Bình và `docs/M4_FINANCIAL_REPORT.md`. Nguồn dữ liệu thuộc Orders, Inventory và Sales được đọc từ cùng snapshot.
- Luồng hoàn, kho, bảo hành, ghi thu/chi, giá bán, điểm/voucher, migration, UI và Gemini nằm ngoài phạm vi. Việc đối soát QR đầu cuối phụ thuộc nguồn xác nhận của Đạt.
