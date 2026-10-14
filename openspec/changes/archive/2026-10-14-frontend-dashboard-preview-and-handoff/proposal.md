# Proposal

## Why

Bốn change foundation đã được archive; Catalog có luồng đọc API thật nhưng Dashboard vẫn là trang khung với diagnostics. Dev 6 cần Dashboard preview có nguồn dữ liệu minh bạch và bộ bàn giao đủ để Dev 1–5 tiếp tục Sprint 2, đồng thời phân biệt rõ chuẩn bị Analytics/seed với nghiệp vụ đã tích hợp.

## What Changes

- Dựng Dashboard theo `UI/stitch_techstore_retail_admin_dashboard/admin_dashboard_techstore_management_system/`: bốn KPI, bộ chọn kỳ, xu hướng doanh thu, tồn thấp và năm đơn gần nhất; tái sử dụng Shell, tokens, StatCard, EmptyStateControl và StatusBadge.
- Thêm Models Analytics và `IAnalyticsRepository` với fixture xác định, chỉ bật qua chế độ Dashboard demo tường minh. Chế độ thường hiển thị chưa tích hợp, không có số giả, request Analytics hoặc fallback fixture.
- Tái sử dụng activation/cancellation và DI hiện có; chuyển các nút kiểm tra Shell khỏi nội dung Dashboard nghiệp vụ sang khu phát triển được bật rõ ràng, giữ kiểm thử dialog/lifecycle.
- Chuẩn bị ma trận nhu cầu UC-STAT-01/02; ghi xu hướng ngày là preview phục vụ UC-STAT-03 ở Sprint 2. Tách quy ước fixture khỏi DTO/quy tắc backend chưa xác nhận.
- Chuẩn bị yêu cầu UC-SEED-01, đối chiếu seeder/spec hiện tại với mục tiêu Sprint 2; không sửa seeder hoặc schema.
- Bàn giao hướng dẫn mở rộng foundation, traceability cho năm change và sổ phụ thuộc backend có owner/trạng thái. Tách tests, Linux runtime, Windows verification và manual visual acceptance trên build cụ thể.

## Capabilities

### New Capabilities

- `frontend-dashboard-preview`: Dashboard demo có chủ đích, dữ liệu mẫu nhất quán, trạng thái tải/hủy, trình bày theo mẫu và bằng chứng bàn giao phân biệt preview với tích hợp thật.

### Modified Capabilities

- `frontend-shell-navigation`: Tổng quan hỗ trợ demo được bật rõ ràng hoặc trạng thái chưa tích hợp; giữ startup offline, sáu điểm vào và lifecycle/reset hiện có.

## Impact

- Client: `Models/Analytics`, `Repositories/Analytics`, `Services/Development`, Dashboard View/ViewModel, composition root và khu diagnostics; không đưa UI controls vào ViewModel.
- Tests: mở rộng suite headless bằng source production và fake repositories; regression navigation/dialog/Catalog; không có Analytics API integration giả.
- Docs ở giai đoạn apply: `FRONTEND_DASHBOARD_HANDOFF.md`, `ANALYTICS_DATA_REQUIREMENTS.md`, `DEMO_SEED_REQUIREMENTS.md`, cập nhật `FRONTEND_FOUNDATION_GUIDE.md` làm đầu mối cho Dev 1–6.
- Kiến trúc theo `docs/MVVM_REPOSITORY_BASE_PROJECT.md`, ưu tiên hơn Feature-First cũ. Giữ .NET 10, Uno SDK 6.7.30 và CPM. Ưu tiên controls WinUI/Community Toolkit hiện có; không coi kết quả Skia là xác minh Windows.
- Không Analytics endpoint/shared DTO mới, backend/seeder, login/RBAC thật, CRUD, client-side tổng hợp đơn hàng thật hoặc nâng SDK. Đây chỉ là planning artifacts; implementation bắt đầu ở lượt apply riêng.
