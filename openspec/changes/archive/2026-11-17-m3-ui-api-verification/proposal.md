# Proposal

## Why

M3 Sprint 5 (T. Vinh): "Kiểm tra HTTP methods, JSON và mã lỗi 200/201/204/400/401/403/404/409/500 phù hợp; thử lỗi mạng và phục hồi giao diện" và "Chạy UI trên Windows native và Skia. Kiểm tra Light/Dark/System, resize, bàn phím và binding; ghi build, hệ điều hành, ảnh hoặc log kết quả." Đồng thời điều kiện hoàn thành: "Các luồng M2 vẫn hoạt động; không còn lỗi chặn bán hàng, sai tiền, sai tồn hoặc vượt quyền."

## What Changes

- Không thêm tính năng. Thêm báo cáo `docs/M3_UI_API_VERIFICATION.md` ghi bằng chứng thật theo build: lệnh, kết quả mong đợi/thực tế, Pass/Fail/Chưa kiểm tra.
- Bổ sung test còn thiếu nếu phát hiện mã lỗi sai hoặc UI không phục hồi; sửa lỗi thuộc phần T. Vinh, lỗi phần người khác ghi lại cho chủ phần.

## Capabilities

### Modified Capabilities

- `frontend-shell-navigation`: Build specific shell acceptance (thêm bằng chứng role/HTTP).

## Impact

- Docs: báo cáo mới; tick `docs/MILESTONE_3_PLAN.md` phần T. Vinh đã đạt.
