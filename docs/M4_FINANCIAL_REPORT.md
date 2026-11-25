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
| FR-06 Hồi quy Dashboard | Dashboard theo quy tắc hiện hành sau PR #31 (An): doanh thu cộng `TotalAmount` gốc của đơn đã giao, gồm cả đơn `PartiallyRefunded`/`Refunded`. | Cùng fixture FR-03, Dashboard trả doanh thu 518 (A 198 + B 198 + C 122). Các bộ test Analytics, Dashboard live flow và PostgreSQL seed/analytics đều đạt. | Pass |

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

## Change 2 — Hoàn tiền, nhập lại và bảo hành

### Baseline và bàn giao

- Ngày 25/11/2026, `origin/dev` trên GitHub và ref local cùng SHA `f5756b07afa366bab6690287fdc8fd28d9078aa4`. Nhánh `feat/m4-financial-reports` ở `09abf9b9b3fe33986f9df0ab2386e99a15c676e5` đã chứa baseline này, API phần bán và các migration hậu mãi.
- Build Debug đầu tiên với implementation trên worktree của HEAD trên đã thành công. SHA-256 `TechStore.Api.dll`: `742D6D3F7EEAC3A3C0815B63B6FC80D23C8909C14E0D5BB7133A3496499B1D2B`.
- An đã có dòng hoàn chốt, giá trị phân bổ và movement nhập lại. Q. Vinh đã có hồ sơ bảo hành, movement và snapshot vốn máy thay. Đạt đã có chứng từ Cash theo ca và schema QR. An đã có đối soát hoàn QR thủ công. Vy đã có snapshot phân bổ bán.
- **Chưa kiểm tra — thu QR đầu cuối và callback/đối soát tiền tới muộn.** Owner Đạt. Chạy lại khi build tích hợp có luồng xác nhận thu và chứng từ hợp lệ từ ngân hàng hoặc sandbox. Fixture QR đã xác nhận chỉ kiểm tra phía Analytics.

### Nghiệm thu qua HTTP trên PostgreSQL 16.16

Fixture D6 dùng ngày 28–31/10/2025 để tránh trùng demo 2026 đã seed. Các ngày và mốc qua 17:00Z tương đương design. Luồng Cash checkout, hoàn Cash, đối soát QR hoàn thủ công, kiểm định và bảo hành đi qua API với JWT thật trên database test. Riêng khoản QR thu dùng bản ghi xác nhận fixture. Giá trị kỳ vọng được tính độc lập trước khi đọc API.

Expected = Actual, đơn vị nghìn đồng. Tiền hàng và vốn gốc giữ ở ngày giao.

| Ngày UTC+07 | Bán chưa thuế | Vốn bán | Thực chi hoàn | Thuế hoàn | Giảm doanh thu | Vốn nhập lại | Doanh thu thuần | Vốn thuần | Lãi gộp | Bảo hành |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 28/10 | 1.800 | 1.200 | 0 | 0 | 0 | 0 | 1.800 | 1.200 | 600 | 0 |
| 29/10 | 0 | 0 | 990 | 90 | 900 | 0 | -900 | 0 | -900 | 0 |
| 30/10 | 0 | 0 | 0 | 0 | 0 | 600 | 0 | -600 | 600 | 500 |
| 31/10 | 0 | 0 | 1.540 | 140 | 900 | 0 | -900 | 0 | -900 | 0 |
| Cả kỳ | 1.800 | 1.200 | 2.530 | 230 | 1.800 | 600 | 0 | 600 | -600 | 500 |

| Ca | Expected | Actual | Kết quả |
| --- | --- | --- | --- |
| FR-07 Hoàn một phần rồi hết | Hai lần hoàn đơn đã giao, mỗi lần chi 990.000đ, giảm doanh thu 900.000đ và hoàn thuế 90.000đ. Phiếu chờ hoặc thất bại chưa có khoản chi. Đơn chưa giao hoàn 550.000đ với thuế 50.000đ và không giảm doanh thu. | Khớp cả bốn ngày theo bảng. Gửi lại lần Cash và hai lượt đối soát QR giữ nguyên số đã đọc. | Pass |
| FR-08 Kiểm định và nhập lại | Hoàn trước 17:00Z, nhập sau 17:00Z. Lỗi/chờ sửa và cờ không có movement không giảm vốn. Tách lô đã chi vẫn nguyên số hoàn, chỉ phần thực nhập làm giảm vốn. | Thực nhập vào ngày Việt Nam 30/10 giảm đúng 600.000đ. Kiểm định lặp giữ nguyên số. Lô ba món tách thành ba dòng giữ chi 3.300.000đ và thuế 300.000đ, một món vào tồn. Món thiếu vốn trả `null`, món vốn 0 trả 0. | Pass |
| FR-09 Bảo hành | Xuất máy thay 500.000đ một lần, nhận sửa/trả máy cũ không tăng chi phí máy thay. Vốn máy thay chưa biết tách khỏi 0 và khỏi lãi gộp. | API sau xuất và sau replay cùng trả 500.000đ; lãi gộp ngày xuất vẫn 600.000đ. Hai ca giá vốn thiếu/0 trả đúng `null` và 0. | Pass |
| FR-10 Dòng tiền theo ca/kênh | Ca bán thu Cash 1.980.000đ. Ca Manager chi 990.000đ, mở 2.000.000đ thì dự kiến đóng 1.010.000đ. QR không vào két. | Đúng ca, đúng số dự kiến đóng và chênh lệch 0. Kỳ chỉ ngày chi chỉ trả ca chi. QR fixture thu 550.000đ ngày 30/10, hoàn QR tổng 1.540.000đ ngày 31/10. Legacy thiếu chứng từ ca trả `null` cho chi Cash, vẫn có tổng thực chi độc lập. | Pass |
| FR-11 Tổng kỳ, cutoff và tương thích | Cộng từng chỉ số ngày bằng kỳ khi đủ dữ liệu, lan `null` đúng nhóm. Loại sự kiện đúng `asOf`. DTO cũ vẫn đọc được. | Khớp 14 cột độc lập, gồm 4 chỉ số Cash/QR. Khoản hoàn, movement nhập và xác nhận QR đúng mốc bị loại. Doanh thu thuần cả kỳ 0, vốn thuần 600.000đ, lãi gộp −600.000đ. DTO shared cũ và response bổ sung đều đọc được. | Pass |
| FR-12 Hồi quy | Admin/Manager 200, Sales/Warehouse 403, JWT sai/thiếu 401, kỳ sai 400. Dashboard vẫn đếm doanh thu gốc đơn đã giao kể cả hoàn. | Hồi quy quyền, kỳ, Dashboard, xếp hạng và hiệu suất phần bán đều đạt. | Pass |

### Build và phạm vi kiểm chứng

- Windows, .NET SDK `10.0.404`, Debug, HEAD `09abf9b9b3fe33986f9df0ab2386e99a15c676e5`. Worktree chứa code và test của change 2 chưa commit.
- SHA-256 `TechStore.Api.dll`: `742D6D3F7EEAC3A3C0815B63B6FC80D23C8909C14E0D5BB7133A3496499B1D2B`.
- SHA-256 `TechStore.UnitTests.dll`: `BBB697484E05FCDBCFB2900365CFF60DDF20ABEC442F26CF60C4E19C665A3ABC`.
- SHA-256 `TechStore.Backend.IntegrationTests.dll`: `11FC907D3A6D7A405D42E667EA711BEC501D18B84ABCD12EC14837AD60E75EA8`.
- `TechStore.UnitTests` lọc FinancialReport, Analytics, ApiAuthorizationCoverageTests, DashboardLiveFlowPostgresHttpTests: 45 Pass, 0 Fail, 0 Skip.
- `TechStore.Backend.IntegrationTests` lọc AnalyticsPostgresIntegrationTests và DataSeederPostgresIntegrationTests: 4 Pass, 0 Fail, 0 Skip.
- Các giới hạn đã kiểm tra bằng fixture gồm legacy thiếu phân bổ, giá vốn hoặc chứng từ. Việc bổ sung dữ liệu lịch sử cần An và Đạt xác nhận nguồn. Chi phí công sửa/phụ tùng chưa có nguồn trong phạm vi chi phí máy thay của change này.
