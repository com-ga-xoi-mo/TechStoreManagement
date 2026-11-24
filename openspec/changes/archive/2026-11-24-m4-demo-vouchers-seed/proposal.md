# Proposal

## Why

M4 Sprint 3 (dữ liệu demo): "Có … voucher/điểm … khớp README. Seed từ môi trường sạch và chạy lại không phá dữ liệu/mật khẩu; mỗi người bổ sung phần mình." Seed hiện chỉ có một voucher (`DEMO-OCT26`, còn dùng được), nên màn Voucher và luật kiểm tra khi bán (hết hạn, hết lượt, đang tắt, theo phần trăm) không có gì để thử.

Có thêm một lỗi tiềm ẩn: seed báo xung đột và dừng khi `DEMO-OCT26` đang tắt, đã đổi giá trị hoặc đã có lượt dùng. Từ khi có màn Voucher, Admin/Manager làm được cả ba việc đó, và khi Đạt nối checkout thì `UsedCount` sẽ khác 0 sau lần bán đầu tiên. Khi đó mỗi lần backend khởi động, seed bị rollback toàn bộ (kể cả phần thêm mới của người khác).

## What Changes

- `DemoSeedManifest.Vouchers`: năm voucher, mỗi trạng thái một cái: `DEMO-OCT26` (100.000đ, giữ nguyên giá trị cũ), `DEMO-VIP10` (10%, có trần), `DEMO-EXPIRED` (hết hạn), `DEMO-SOLDOUT` (20/20 lượt), `DEMO-OFF` (đã tắt).
- `DemoSeedRepository.EnsureVouchersAsync`: chỉ thêm voucher còn thiếu theo mã. Voucher đã có được giữ nguyên, không so sánh giá trị, trạng thái hay lượt dùng. Hai dòng cùng mã vẫn là xung đột.
- README module Customers: bảng voucher demo.
- **Không làm**: thêm hay đổi khách hàng demo (xem design).

## Capabilities

### Modified Capabilities

- `data-seeding`: dữ liệu voucher đủ trạng thái; voucher đã seed do Admin/Manager quản lý nên không gây xung đột.

## Impact

- `backend/Common/Data/DemoSeedManifest.cs`, `DemoSeedRepository.cs`; `backend/Customers/README.md`.
- Tests: `tests/Backend.UnitTests/DemoSeedVoucherTests.cs` (mới), một dòng kiểm trong `tests/Backend.IntegrationTests/DataSeederPostgresIntegrationTests.cs`.
- Ảnh hưởng người khác: file seed là của nhóm; chỉ thay một hàm và thêm một danh sách. Số khách và số dòng sổ điểm không đổi nên các test đếm của người khác vẫn đúng.
