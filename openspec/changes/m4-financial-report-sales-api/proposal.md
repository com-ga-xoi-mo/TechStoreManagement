# Proposal

## Why

Sprint 1 M4 cần một API báo cáo quản trị để Bình đối soát số bán đã giao, giá vốn và hiệu suất từ giao dịch đã lưu. Hợp đồng API và JSON mẫu giúp T. Vinh chuẩn bị tích hợp trước khi dữ liệu hoàn, nhập lại và bảo hành được nối ở change 2.

## What Changes

- Thêm `GET /api/analytics/financial-report` cho Admin và Manager, nhận kỳ `custom` theo ngày Việt Nam, trả tổng kỳ, số theo ngày, hai bảng sản phẩm bán chạy và hiệu suất theo thu ngân.
- Tính doanh thu gốc theo `CompletedAt`, giữ phần bán khi đơn chuyển sang `PartiallyRefunded` hoặc `Refunded`. Tách tiền hàng chưa thuế, thuế và tiền thu theo `PaidAt`. Giá vốn thiếu lan sang các chỉ số phụ thuộc.
- Công bố DTO với các trường nullable dành cho hoàn tiền, giá vốn nhập lại, lợi nhuận sau hoàn và chi phí bảo hành. Giữ nguyên contract và KPI của Dashboard M3.
- Nghiệm thu trên PostgreSQL với bảng Expected tính tay và kiểm tra HTTP bằng JWT. Ghi kết quả, SHA build và giới hạn dữ liệu trong `docs/M4_FINANCIAL_REPORT.md`.

## Capabilities

### New Capabilities

### Modified Capabilities

- `admin-analytics-api`: Bổ sung contract và quy tắc tính báo cáo tài chính cho phần bán.
- `api-role-authorization`: Thêm quyền đọc báo cáo tài chính toàn cửa hàng cho Admin và Manager.

## Impact

- Khi apply, phạm vi sửa gồm `backend/Analytics/`, test báo cáo và `ApiAuthorizationCoverageTests`, dòng API mới trong `docs/API_PERMISSIONS.md` và tài liệu bằng chứng của Bình. DTO đặt trong file mới `shared/DTOs/FinancialReportDto.cs` để desktop dùng chung contract.
- Dùng snapshot đơn/chi tiết đơn đã lưu. Công thức định giá, UI, Gemini, luồng bán/hoàn/bảo hành, đối soát sổ ca và DB schema nằm ngoài phạm vi change này.
