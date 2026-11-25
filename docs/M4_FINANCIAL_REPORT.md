# M4 Sprint 1 — Báo cáo tài chính phần bán

## Baseline

- Nhánh `feat/m4-financial-reports`.
- `git fetch origin dev` thành công. `git merge origin/dev` báo `Already up to date`.
- `origin/dev` và HEAD sau merge cùng SHA `6c3187045ab1e5f696ea6fc0d2d4cc483c222e5a`. Kiểm tra ancestry thành công.

## Kết quả nghiệm thu

Ngày 22/11/2026. ASP.NET TestServer trên Windows dùng pipeline JWT thật và PostgreSQL 16.16 trong container local riêng. Fixture dùng database được tạo riêng, không dùng dữ liệu cửa hàng. Ca kiểm tra `asOf` và ngày tương lai dùng đồng hồ cố định, vẫn chạy truy vấn thật trên PostgreSQL.

| Ca | Expected | Actual | Kết quả |
| --- | --- | --- | --- |
| FR-01 Quyền đọc | Admin/Manager 200 với cùng số liệu toàn cửa hàng. SalesStaff/WarehouseStaff 403. Thiếu JWT hoặc JWT sai 401. | 200/200, 403/403, 401/401. Cùng summary, daily và hiệu suất. Lỗi giữ Bearer challenge cho 401 và không lộ dữ liệu báo cáo. Baseline route độc lập khớp. | Pass |
| FR-02 Kỳ tùy chọn | 1 và 31 ngày trả đúng số dòng. 17 tổ hợp kỳ lỗi cú pháp/biên trả 400 theo trường, chưa đọc snapshot. Ngày cuối tương lai trả 400, chưa tổng hợp. | 200 cho hai ca hợp lệ. 400 cho cả 18 ca sai, đúng trường. Bộ đếm truy vấn không tăng cho bất kỳ ca sai nào. | Pass |
| FR-03 Biên ngày và số bán | Kỳ 12–14/10/2024 UTC+07 gồm đúng biên đầu và trước biên cuối. Đơn hoàn giữ số gốc, đơn Processing chỉ vào tiền thu theo ngày thanh toán. Ngày 13 có dòng 0. | Bảng đối chiếu bên dưới khớp cả ba ngày và tổng kỳ. Đơn đúng biên cuối xuất hiện ở kỳ 15/10. Mốc đúng `asOf` và sau `asOf` bị loại trong ca ngày hiện tại. | Pass |
| FR-04 Giá vốn và phân bổ | Thiếu giá vốn làm giá vốn/lãi gộp phụ thuộc là `null`. Giá vốn 0 đã biết vẫn trả 0. Đơn phân bổ lệch được đếm một lần. Bảy trường change 2 có mặt và là JSON `null`. | Khớp bảng đối chiếu. Ca chỉ có đơn kiểu C ngày 16/10 trả giá vốn 0, lãi gộp 120 và bộ đếm lệch 1. JSON tổng kỳ và từng ngày đều có đủ trường nullable. | Pass |
| FR-05 Xếp hạng và hiệu suất | Gom biến thể theo sản phẩm, tối đa 10 cho mỗi thứ tự độc lập và ổn định khi hòa. Gom thu ngân theo ID dù trùng tên hoặc đã vô hiệu hóa. Đơn nhiều dòng chỉ tính tiền thu một lần. | Hai bảng trả đúng top 10 và tên snapshot, kỳ rỗng trả mảng rỗng. Ca nhiều dòng có 12 đơn, 31 đơn vị hàng và 260 tiền thu. Hai ID thu ngân được tách riêng, chỉ nhóm thiếu giá vốn trả `null`. | Pass |
| FR-06 Hồi quy M3 | Dashboard giữ KPI cũ, gồm việc loại đơn hoàn khỏi doanh thu. | Cùng fixture FR-03, Dashboard trả doanh thu 122. Các bộ test Analytics, Dashboard live flow và PostgreSQL seed/analytics đều đạt. | Pass |

Expected phần bán tính độc lập từ fixture trong `design.md`. Các ô ghi Expected → Actual, theo VND với các cột tiền.

| Ngày UTC+07 | Đơn giao | Số lượng | Tiền hàng chưa thuế | Thuế | Tiền thu | Giá vốn | Lãi gộp bán | Đơn lệch phân bổ |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 12/10 | 1 → 1 | 2 → 2 | 180 → 180 | 18 → 18 | 248 → 248 | 120 → 120 | 60 → 60 | 0 → 0 |
| 13/10 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| 14/10 | 2 → 2 | 3 → 3 | 300 → 300 | 18 → 18 | 122 → 122 | null → null | null → null | 1 → 1 |
| Cả kỳ | 3 → 3 | 5 → 5 | 480 → 480 | 36 → 36 | 370 → 370 | null → null | null → null | 1 → 1 |

### Build đã kiểm chứng

- .NET SDK `10.0.404`, cấu hình Debug, Git HEAD `6c3187045ab1e5f696ea6fc0d2d4cc483c222e5a`, worktree có implementation và test M4 chưa commit.
- SHA-256 `TechStore.Api.dll`: `E34ACADF06A6FA797BF81358CA6FE2B479B15E7E7CDC1F343AC50803702A0429`.
- SHA-256 `TechStore.UnitTests.dll`: `743481CB3B2868FB4C846BB973AF75739E0A5B244146DEF9268A192E22C2C558`.
- SHA-256 `TechStore.Backend.IntegrationTests.dll`: `EDBBBCC9CE44838D4A130FE2D367F1DE15F728C9EBD8F2C4F76D2823BF1FD1B7`.
- `TechStore.UnitTests` lọc Analytics, FinancialReport, ApiAuthorizationCoverage và DashboardLiveFlowPostgresHttpTests: 41 Pass, 0 Fail, 0 Skip.
- `TechStore.Backend.IntegrationTests` lọc AnalyticsPostgresIntegration và DataSeederPostgresIntegration: 4 Pass, 0 Fail, 0 Skip.
- Runner Windows ghi chẩn đoán DPAPI/Event Log trong lượt test hồi quy. Assertion và mã thoát của hai lượt test đều đạt. Container cùng volume PostgreSQL test đã được dọn sau nghiệm thu.

## Giới hạn dữ liệu

Tiền thu theo `PaidAt` và tổng đơn đã lưu, chưa đối soát sổ ca hoặc giao dịch ngân hàng. Đơn cũ giữ nguyên số phân bổ 0 trên dòng và có bộ đếm khi lệch đầu đơn. Các nguồn hoàn, nhập lại và bảo hành chờ change 2.
