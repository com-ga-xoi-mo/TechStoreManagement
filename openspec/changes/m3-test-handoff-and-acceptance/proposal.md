# Proposal

## Why

Bình cần hoàn tất phần regression Sprint 5 và tập hợp bằng chứng M3 để An ghép bản bàn giao. Các kết quả hiện nằm ở nhiều build và loại kiểm thử, nên mỗi kết luận cần truy được về ca, owner và build đã chạy.

## What Changes

- Trước khi kiểm thử, fetch và merge `origin/dev` mới nhất vào `feat/m3-manager-dashboard-access`, build lại và ghi SHA thực chạy. Đối chiếu ba change Dashboard, seed và live flow đã archive.
- Cập nhật các mục của Bình đã có bằng chứng về Dashboard Manager, dữ liệu demo và seed chạy lại. Chạy regression Admin M2 cho sản phẩm, danh mục và khách hàng. Dùng lại ca tự động hiện có và bổ sung HTTP/PostgreSQL cho phần thiếu.
- Đối chiếu dữ liệu sau checkout thành công, xung đột 409 và lỗi đầu vào 400. Dùng bằng chứng live flow cho phạm vi đã phủ và ghi rõ kiểm tra tuần tự.
- Tổng hợp kết quả kiểm thử theo nhóm, build và owner trong `docs/M3_TEST_SUMMARY.md`. Các mục chờ receiving, POS hoặc Windows native có trạng thái và điều kiện chạy lại để An tiếp nhận.

## Capabilities

### New Capabilities

Không có.

### Modified Capabilities

Không có. Change chỉ kiểm thử và bàn giao theo hợp đồng hiện có, khai báo `skip_specs: true`.

## Impact

- Trong lượt apply, phạm vi file gồm `docs/M3_TEST_SUMMARY.md`, một mục liên kết trong `docs/M3_DASHBOARD_DATA_TEST_HANDOFF.md`, các checkbox thuộc Bình trong `docs/MILESTONE_3_PLAN.md` và lớp test mới của Bình nếu cần.
- Giữ nguyên code, test, helper và tài liệu của các owner khác. Khi phát hiện sai lệch, báo cáo lỗi, bằng chứng và owner xử lý. Kết quả chạy lại phải gắn với build đã sửa.
