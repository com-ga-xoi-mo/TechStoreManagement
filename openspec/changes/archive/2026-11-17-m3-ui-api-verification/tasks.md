# Tasks

## 1. Kiểm thử thủ công (AI)

- [x] 1.1 HTTP/JSON: với API + PostgreSQL thật, gọi mỗi mã 200/201/204/400/401/403/404/409 bằng request thật (ghi method, path, role, status, `Content-Type`); 500 theo design D2.
- [x] 1.2 Lỗi mạng: tắt API khi client đang chạy, xác nhận trang hiện lỗi + Thử lại; bật lại API, Thử lại thành công (headless qua `ApiClient` + ViewModel thật nếu không điều khiển được UI).
- [x] 1.3 Bốn role trên app Skia: landing, menu, chặn route, Cài đặt; theme Sáng/Tối/Hệ thống, resize; ghi build, OS, log.
- [x] 1.4 Windows native: ghi "Chưa kiểm tra" nếu không có máy Windows.
- [x] 1.5 Chạy toàn bộ `dotnet test` backend + frontend unit; ghi Pass/Fail/Skipped.

## 2. Bàn giao

- [x] 2.1 Viết `docs/M3_UI_API_VERIFICATION.md`.
- [x] 2.2 Tick các mục T. Vinh trong `docs/MILESTONE_3_PLAN.md` đã có bằng chứng; mục còn thiếu giữ nguyên và ghi lý do trong báo cáo.

## Kết quả kiểm thử (AI, 2026-11-17)

- Môi trường: Ubuntu 26.04.1 LTS, Linux 7.0.0-43-generic x86_64, .NET SDK 10.0.101, Wayland `DISPLAY=:0`. Build code nghiệm chứng: `7240d3f` (sau 5 change triển khai).
- 1.4 **Chưa kiểm tra** Windows native/HighContrast: không có máy Windows. Checkbox này chỉ xác nhận đã ghi giới hạn, không phải đạt Windows.
- 1.3 **Chưa kiểm tra** thao tác UI bốn role, theme Sáng/Tối/Hệ thống, resize, bàn phím/focus và binding trực quan: không có công cụ quan sát/điều khiển Wayland; startup log và headless không thay thế UI. Giữ checkbox mở.
- 1.1 **Pass** cho các mã HTTP thực 200/201/204/400/401/403/404/409 qua curl, log `/tmp/m3-final-http.log`. Category thử `721bc4bf-dbd0-450c-b951-16ecc8d037f7` đã DELETE 204. JSON đọc được trên các response có body; 204 không body/không Content-Type. **Chưa kiểm tra 500**: không tìm thấy test backend middleware tương ứng, không cố gây lỗi trên DB thật (D2). Task tick xác nhận ma trận và giới hạn 500 đã được ghi, không chứng nhận 500 đạt.
- 1.5 Build `dotnet build TechStore.sln`: **Pass**, 0 warning/error (`/tmp/m3-final-build.log`). Backend unit với `TECHSTORE_TEST_CONNECTION`: **609 Pass / 0 Fail / 0 Skipped**, `/tmp/m3-final-backend.log`. Frontend unit: **975 Pass / 0 Fail / 0 Skipped**, `/tmp/m3-final-frontend.log`. Mỗi project test chạy riêng một lần ở bước cuối; không tính kết quả suite lỗi trước đó là đạt.
- 1.2 `dotnet run --project /tmp/m3-live-runner -- network`, dùng cùng ProductListViewModel/repository/ApiClient production và API thật cổng 5201: `Content` → tắt API → `Error`, Retry khả dụng → bật API/call Retry → `Content`, Admin generation 1 không đổi. **Pass headless**, `/tmp/m3-final-network.log`, runner exit 0; không fake backend/network và không tuyên bố đã thấy UI.
- 2.1–2.2 Báo cáo `docs/M3_UI_API_VERIFICATION.md` đã ghi build/OS, input/kết quả/Pass/Fail/Chưa kiểm tra và phụ thuộc owner. Chỉ tick kế hoạch 120–122; giữ mở 118–119/123, Sprint 5 152–153 và điều kiện 182 do thiếu UI/Windows/500/toàn luồng.
- Dọn môi trường: khôi phục store settings, DELETE category thử 204, xóa đúng user thử ngoài dự kiến và FK của nó; runner tạm đã xóa, API 5201 không còn listener, đã tắt API 5199 (curl sau dừng mã transport 000). Giữ DB test có sẵn. Không push/archive/PR.

- 1.3 (17/11): T. Vinh nghiệm thu UI Skia kịch bản A–G (landing, menu, nút, Cài đặt, theme, resize, Tab). Windows native vẫn Chưa kiểm tra. **Pass trên Skia**
