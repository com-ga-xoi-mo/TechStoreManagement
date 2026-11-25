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

## Change 3 — Báo cáo tài chính desktop

Ngày 25/11/2026, Windows build `10.0.26200.0`, .NET SDK `10.0.404`. Backend và client dùng HEAD `a037d619babf647555cc8edd2d7d65f5dd7ddcea`. Bản client được build Debug với phần Dashboard/Analytics của change này trong worktree. API chạy qua HTTP loopback trên PostgreSQL 16.15, dùng database thử riêng với demo seed và migration hiện hành.

### Headless và đối chiếu với PostgreSQL

- `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj --no-restore` đạt **1.202/1.202** (63/63 cho `FinancialReport|DashboardViewModel`, cập nhật sau chỉnh sửa 2-way tab state, reset nháp khi mở lại, strict JSON `cashByShift`, và KPI/section layout). Fixture JSON phần bán và fixture mở rộng kiểm tra `asOf`, ngày 0, giá trị thiếu, tiền âm/phần lẻ, trường JSON bắt buộc, thuế hoàn, Cash/QR, ca và bảo hành. Fake transport trả muộn sau hủy xác nhận yêu cầu cũ không ghi đè kỳ mới, không xóa trạng thái tải của yêu cầu mới và không hiện dữ liệu sau khi đổi hoặc hết phiên. Ca retry dùng đúng kỳ đã áp dụng sau khi ngày nháp thay đổi.
- `TechStore.Client.IntegrationTests` lọc `FinancialReportApiIntegrationTests` với `TECHSTORE_API_BASE_URL` trỏ tới API local (database thử với demo seed ở lượt đối chiếu ban đầu) đạt **1/1**. Với Admin và Manager, test so sánh kỳ, `asOf`, tổng kỳ, cả bảy dòng ngày và thứ tự hai bảng bán chạy, người bán, tiền mặt theo ca với chính HTTP response mà repository nhận. Luồng bị chặn cho SalesStaff/WarehouseStaff không gửi query từ ViewModel. Endpoint trả 403 khi hai role gọi trực tiếp. *(Ghi chú: chạy thử với database local đang có sẵn của máy dev ở phiên sau trả 401 khi login tài khoản seed mẫu vì khác dữ liệu mật khẩu; backend API không thay đổi sau commit `a037d61`.)*
- `TechStore.UnitTests` lọc `FinancialReportPostgresHttpTests` và `DashboardLiveFlowPostgresHttpTests` đạt **7/7**, 0 skip. API live build SHA-256 `2C094BE55BC17AE7B23FA53EA895C5B555B8844E2D32420CE23CFACCD223968C`.
- Kỳ demo 15–21/10/2026, UTC+07. Tiền hàng bán chưa thuế theo ngày lần lượt là 14.980.000 ₫, 22.980.000 ₫, 19.680.000 ₫, 27.980.000 ₫, 0 ₫, 0 ₫ và 0 ₫, tổng 85.620.000 ₫. Thu Cash 88.630.000 ₫, thu VietQR 33.970.000 ₫. Kỳ có bảy sản phẩm trong bảng doanh thu, một người bán và một ca tiền mặt. `asOf` của lần gọi Admin trực tiếp là `2026-11-25T09:59:34.295404Z`.
- **Chưa kiểm tra — thu QR đầu cuối và callback/đối soát tiền tới muộn.** Owner Đạt. Chạy lại khi có luồng xác nhận thu và chứng từ hợp lệ từ ngân hàng hoặc sandbox. Số thu QR ở trên đến từ demo seed.

### Skia desktop

- `dotnet build TechStore.sln` (bao gồm `frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`) đạt, 0 cảnh báo. SHA-256 `TechStore.Client.dll` mới nhất: `B91CD91EF37A924666A2E7C2A7F9BA311278E88780C3F5BF30EB8DB5B11BED0C` (bản trước khi chỉnh layout là `1B7F307188B1B27AF66FADC163DF99E10FE0ECCD1B9245B2CB13EE1740449895`).
- Trên bản build `1B7F3071...` chạy từ `bin/Debug/net10.0-desktop` (`TECHSTORE_API_BASE_URL` loopback, `TECHSTORE_DASHBOARD_DEMO=0`, Admin):
  - Kỳ 15–21/10/2026 hiển thị `85.620.000 ₫` tiền hàng bán chưa thuế, `88.630.000 ₫` thu Cash và `33.970.000 ₫` thu VietQR; nhãn tiếng Việt, kỳ và giờ UTC+07 hiển thị trong UI Automation; mở được chi tiết ngày 20/10 qua `ExpandCollapsePattern`.
  - Tổng quan được chọn mặc định và tiếp tục dùng kỳ “Hôm nay” sau khi chuyển tab; panel ẩn rời cây accessibility; mở lại báo cáo lấy kỳ 15–21/10 đã áp dụng. Phím Space chọn cả hai tab, focus ở tab vừa chọn, và Tab đi từ Từ ngày tới Đến ngày, Áp dụng, Làm mới; ngắt kết nối chuyển nút sang “Thử lại” và giữ focus khi tải lại.
  - Resize ở 800×600, 1024×768 và 1440×1000 DIP (DPI 120): giữ số liệu và truy cập được các mục. *(Khi chạy từ shell nâng quyền ở phiên đó, Skia không đọc được theme hệ thống vừa đổi.)*
- Trên bản build `B91CD91E...` sau khi chỉnh giao diện (`dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` từ terminal người dùng thường, Admin `System Administrator`, kỳ mặc định `19–25/11/2026`, `http://localhost:5176`):
  - Hiển thị đầy đủ tab pill `Tổng quan` / `Báo cáo tài chính`, thanh lọc ngày 1 hàng, thông báo kỳ không phát sinh, 4 thẻ KPI (`0 ₫`), lưới 2 cột `Tổng kỳ chi tiết` và định dạng tiếng Việt / `₫` / `(UTC+07)`.
  - **Đổi Sáng/Tối live đạt:** Khi đổi chế độ Windows (`Settings → Personalization → Colors`, Sáng ↔ Tối) lúc panel Báo cáo tài chính đang mở, giao diện Skia cập nhật bảng màu Sáng/Tối trực tiếp trên cùng panel, giữ nguyên kỳ và không tải lại dữ liệu. *(Các case UIA bàn phím/resize tự động cho bố cục lưới mới chưa chạy lại script UIA riêng; mới kiểm lại trực quan màn hình mặc định và Sáng/Tối.)*
- Lần chạy lại ngày 25/11/2026 trên Windows 11, target `net10.0-desktop`, mã nguồn `2bd3c15` (Admin `System Administrator`, kỳ 15–25/10/2026, cập nhật 22:59 UTC+07): báo cáo hiển thị doanh thu thuần chưa thuế `85.620.000 ₫`, lãi gộp sau hoàn `14.070.000 ₫`, tiền đã thu `122.600.000 ₫` (Cash `88.630.000 ₫`, VietQR `33.970.000 ₫`), nhãn tiếng Việt, định dạng `₫` và `(UTC+07)`. Đổi Sáng/Tối khi panel đang mở đạt, do người dùng kiểm tra trực tiếp. Tab/focus và resize 800×600, 1024×768, 1440×1000 DIP trên bố cục mới cũng đạt, do người dùng kiểm tra trực tiếp.

### Windows native

- `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-windows10.0.26100` (qua `dotnet build TechStore.sln`) đạt, 0 cảnh báo. SHA-256 `TechStore.Client.dll` mới nhất: `21C696FE3781A6F92CAF952F5CA6242827553822D5B920F86455E3D696B36DB9` (bản trước khi chỉnh layout là `A2561526F69F31EA361D70CEA5A2EE589EFD8B89109B7EE5692904BE79F65F85`).
- **Lần chạy trước (bản build cũ): chưa kiểm tra giao diện.** Host thoát trước đăng nhập với `Microsoft.UI.Xaml.dll` 3.1.7.0, exception `0xc000027b`. Đã thử `dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-windows10.0.26100 --no-build --launch-profile "TechStore.Client (WinAppSDK Unpackaged)"`, lần chạy này CLI kích hoạt bản packaged. Thử lại với `-p:WindowsPackageType=None` cũng ghi cùng lỗi ở executable unpackaged.
- **Lần chạy lại ngày 25/11/2026: đạt.** Windows 11, mã nguồn `2bd3c15`, build Release `net10.0-windows10.0.26100` đạt (0 lỗi, SHA-256 `TechStore.Client.dll` `FE5EA145BDB6FD7B8CBCE668D8FD5BF569EA1A68D1CF38ABF1A89BC8A8078A6F`). Người dùng chạy client native tới màn hình đăng nhập và kiểm tra báo cáo tài chính với Admin, kỳ 15–25/10/2026: tiếng Việt/VND, Tab và focus, resize 800×600, 1024×768, 1440×1000 DIP và đổi Sáng/Tối khi báo cáo đang mở đều đạt. Kết quả do người dùng xác nhận trực tiếp, không có log runner đi kèm.

## Sprint 2 live reconciliation

### API and PostgreSQL

- API source SHA `a037d619babf647555cc8edd2d7d65f5dd7ddcea`. The worktree contained the desktop changes, planning edits, the new acceptance tests and uncommitted English rewrites of archived OpenSpec documents that are outside this change. The API used the checked-in production routes. TestServer ran on Windows with .NET SDK 10.0.404 and PostgreSQL 16.15 from the local container. `IdentityPostgresApi` applied migrations to generated Npgsql databases and used real JWTs. The test fixture fixes transaction event times and issues JWTs through the original identity host with the real clock.
- Run `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --no-restore --filter "FullyQualifiedName~FinancialReportLiveReconciliationPostgresHttpTests|FullyQualifiedName~DashboardLiveFlowPostgresHttpTests|FullyQualifiedName~VietQrClose_"` with `TECHSTORE_TEST_CONNECTION` pointing to the authorized local test PostgreSQL instance. The fixture creates and drops its own databases.
- Baseline **Pass**. Manager's pre-activity report for 2025-10-28 (UTC+07) returned one day with zero known amounts and no cash-shift rows. Admin/Manager read the same report data with HTTP 200, SalesStaff/WarehouseStaff received 403, missing and invalid JWT requests received 401, and reversed dates with valid Manager JWT received 400. The 20 October 2026 M3 demo orders matched `DemoSeedManifest.Orders` exactly by order code, status, payment method, `PaidAt` and `CompletedAt`. Both unchanged Dashboard live-flow tests passed on PostgreSQL.
- The final PostgreSQL run on 2026-11-25 passed **15/15** (nine new reconciliation tests, two unchanged Dashboard tests and four unchanged VietQR hold tests). The three race tests then passed three more consecutive runs. API SHA-256 `2C094BE55BC17AE7B23FA53EA895C5B555B8844E2D32420CE23CFACCD223968C`.

#### Cash example

Expected equals Actual for each amount below, in thousands of VND. The reporting calendar is UTC+07. The purchase receipt, sale, settlements and inspections committed through their HTTP endpoints.

| Date | Sales excl. VAT | Sales VAT | Receipt | Refund | Refund VAT | Sales reversed | Original cost | Restock cost | Net sales | Net cost | Net gross profit |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 2025-10-28 | 1,800 | 180 | 1,980 | 0 | 0 | 0 | 1,200 | 0 | 1,800 | 1,200 | 600 |
| 2025-10-29 | 0 | 0 | 0 | 990 | 90 | 900 | 0 | 0 | -900 | 0 | -900 |
| 2025-10-30 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 600 | 0 | -600 | 600 |
| 2025-10-31 | 0 | 0 | 0 | 990 | 90 | 900 | 0 | 0 | -900 | 0 | -900 |
| Full period | 1,800 | 180 | 1,980 | 1,980 | 180 | 1,800 | 1,200 | 600 | 0 | 600 | -600 |

- **Pass**. Two units at VND 1,000,000, VND 600,000 unit cost, VND 200,000 order discount and 10% VAT produced VND 1,980,000 due and VND 20,000 change from VND 2,000,000 tendered. Purchase movement `d4b62674-a7fa-4445-a3ac-27f2a2a971af` received two units once for receipt `cde6fb5d-8155-4de7-8d04-23120e5fd1dd`. Order `8305b94f-0ba0-453f-a80b-6f4ee1904118` and sale line `a134512c-c27f-40a1-8f6d-57898fad0f0b` kept the original price, tax, allocations, cost and ranking after refunds. Loyalty moved by +18, -9, -9, ending at zero.
- **Pass**. The paid lines `673b8fbd-d6f6-42bd-9cb1-707a0bb6ba5d` and `3ee75453-cc03-4bb5-9c09-ed8dcae23114` each paid VND 990,000 with VND 90,000 VAT and VND 100,000 of original discount allocation. The first payout occurred at `2025-10-29T16:55:00Z`. Restock movement `8fd58a59-13a8-44a6-aa5e-2c8f2724dbd9` returned one unit at `2025-10-29T17:05:00Z`. Cumulative gross profit was VND -300,000 before restock and VND 300,000 after it. The second item remained defective. Final sellable quantity was one. Each one-day report matched the corresponding period row, all known daily amounts summed to the period, and November 1 had known zeros.
- **Pass**. Selling shift `b7a6044a-67f9-46ee-864a-6dc16e1b532b` received VND 1,980,000 and closed at VND 3,980,000. Manager shift `464c5927-41fe-49bc-9ba4-ebacf1a1229c` paid VND 1,980,000 and closed at VND 1,020,000. Both closing counts matched expected cash and the period report matched each ledger. Same-key purchase, checkout, return, settlement and inspection replays produced no duplicate effects.

#### Additional workflows and races

| Case | Expected and observed | Status and rerun condition |
| --- | --- | --- |
| Undelivered order and manual QR payout | Paid `Processing` order `834b1355-c90a-492a-8f60-70d6e730af6b` received VND 1,100,000. Pending payout was zero. Manager manually reconciled VND 1,100,000 with VND 100,000 refunded VAT at `2025-10-29T03:00:00Z`, using test reference `recon-test-bank-001`. Receipt, refund, release of reserved stock and independent payout date matched. Sales and cost reversal were zero. Cash drawer and loyalty stayed unchanged on payment and replay. The order was paid as Cash through `/api/orders` without a POS shift, so D1 reports `paymentReceivedAmount` 1,100,000 with `cashReceivedAmount` 0 and no `cashByShift` row. Cash plus QR therefore does not equal total receipts for that day. This follows the after-sales design, which counts Cash receipts only for orders with a `PosSessionId`. | **Pass** for the real payout API with simulated bank proof. The channel gap is documented behavior, not a defect. |
| QR receipts and held payments | The October 15–21 M3 confirmed-payment fixture reported VND 33,970,000 of receipts grouped by `ConfirmedAt`, alongside VND 88,630,000 cash. Four existing hold tests covered cancellation, expiry, replay and rollback on PostgreSQL. | **Pass** for fixture Analytics and hold APIs. **Not verified** for a new live QR receipt, provider sandbox or webhook. Đạt supplies the integrated confirmation path, provider proof and replay or late-payment inputs. An supplies actual payout reconciliation proof for a provider run. |
| Warranty and repair handback | Replacement case `f12dc060-4e10-4d08-be17-24a755913fba` issued one linked movement `900bd102-1979-4e47-82cc-fd22a22596c6` and VND 500,000 cost on the local issue day. The original IMEI became defective, the spare became sold, and the repair handback generated no replacement cost. Gross profit remained VND 800,000. Replay preserved the result. | **Pass**. |
| Failed refund and defective IMEI | A test-only PostgreSQL trigger rejected the paid-line insert with HTTP 500. The claim, order report, IMEI, cash payout, drawer, loyalty and voucher counter retained their prior values. Settlement succeeded after removing the trigger. Inspection of the returned IMEI marked it defective. A new checkout returned 409 with no extra order or report effects. | **Pass**. |
| Same IMEI return and warranty, warranty queued first | A test transaction (PID 40701) held the order row. Warranty intake queued first, then the refund. Waiters 40704 and 40710 were in distinct open transactions from `12:35:27.430356Z` (later `xact_start`) to `12:35:27.447060Z` (per-row `clock_timestamp()`) on 2026-11-25. The wait chain was `40710 → 40704 → 40701`. Warranty intake won with 200. Refund returned 409 with no payout or report delta. | **Pass**. |
| Same IMEI return and warranty, refund queued first | The blocker was PID 40937. Waiters 40942 and 40946 overlapped `12:35:35.386641Z`–`12:35:35.421516Z`. The wait chain was `40946 → 40942 → 40937`. The refund won with 200 and paid VND 1,100,000 from the Manager shift. Warranty intake returned 409 and created no case. The IMEI became `Returned`. | **Pass**. |
| Last sellable stock | The blocker was PID 40628. `inventory_stocks` waiters 40634 and 40641 overlapped `12:35:24.847656Z`–`12:35:24.899054Z` on 2026-11-25. The wait chain was `40641 → 40634 → 40628`. Checkout outcomes were 201 and 409. One receipt, one movement, one points accrual and one order committed. | **Pass**. |
| Voucher use, restoration and final slot | The M3 demo voucher counter remained zero across the non-voucher flows. The inspected checkout and order requests expose no voucher redemption, reservation or restoration path for these tests. | **Not verified**. Vy and Đạt supply the integrated voucher APIs and a voucher with one remaining slot. Then verify use or restoration and capture overlapping PostgreSQL transactions in the final-slot race. |

### Desktop UI

- Loopback API input prepared on a separate migrated database with `dotnet run --project backend/TechStore.Api.csproj --no-build --no-restore --no-launch-profile`. Warehouse received two VND 600,000 units, the seller checked out with the VND 200,000 discount and Manager paid both Cash refunds. The first unit was restocked and the second was marked defective. The calendar window was November 24–25, 2026 (UTC+07), with all activity on November 25. Manager received a zero first day and a final total of VND 1,800,000 sales before VAT, VND 180,000 sale and refund VAT each, VND 1,980,000 received and paid back each, VND 600,000 restocked cost and VND -600,000 net gross profit. The response snapshot was `2026-11-25T12:04:22.629635Z` for order `dbda4f32-369b-494c-8a2a-366a64296e69`.
- **Pass, automated client coverage only**. The existing `FinancialReportApiIntegrationTests` passed 1/1 against this loopback API, but for the October 15–21 M3 fixture window. It did not compare the November 24–25 reconciliation range above, so the client has not yet been checked against this run's figures. The existing frontend unit tests filtered by `FullyQualifiedName~FinancialReport|FullyQualifiedName~DashboardViewModel` passed 63/63, including applied range, Vietnamese currency, role gating, retry and late results after changing sessions. Run `dotnet test tests/Frontend.IntegrationTests/TechStore.Client.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~FinancialReportApiIntegrationTests` with `TECHSTORE_API_BASE_URL` set to the isolated API. Unit command `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj --no-restore --filter "FullyQualifiedName~FinancialReport|FullyQualifiedName~DashboardViewModel"`.
- **Skia live UI not verified for this run.** Client SHA-256 `B91CD91EF37A924666A2E7C2A7F9BA311278E88780C3F5BF30EB8DB5B11BED0C` on Windows. The Windows profile had a desktop process and a persisted TechStore session, and the client uses a shared profile path for its session file. A process-local `LOCALAPPDATA` override did not isolate that path. Bình can rerun in a separate desktop profile with `TECHSTORE_API_BASE_URL` pointed at the test API, then compare the acceptance order and range in Overview and exercise account switching during load and connection failure retry.
- **Windows native live UI not verified for this run.** Client SHA-256 `21C696FE3781A6F92CAF952F5CA6242827553822D5B920F86455E3D696B36DB9` on Windows. The prior runtime attempt recorded `Microsoft.UI.Xaml.dll` error `0xc000027b` before login. Recheck after the client can start in an isolated profile, then run the same role, period, result and retry checks.

### AI

- **Not verified** on API SHA `a037d619babf647555cc8edd2d7d65f5dd7ddcea`. The live OpenAPI document had no AI, assistant or chat route, and the financial report tool was absent from the inspected worktree. The intended input is a request for sales, refunds, VAT and net gross profit for the same UTC+07 range used by the API. T. Vinh supplies a build with the report tool and its test configuration. Then run the prompt as Admin and Manager, compare tool output with the captured report, and verify SalesStaff and WarehouseStaff receive no financial figures. Record the prompt, tool trace and HTTP result for all four roles.

## Sprint 3 financial report release handoff

This section is Bình’s contribution for An’s team QA report, README and video. The Sprint 1 and 2 sections retain their original build identities.

### Release identity and evidence rules

| Run group | Source and environment | Use in the handoff |
| --- | --- | --- |
| R0 | On November 25, 2026, `git -c credential.interactive=never ls-remote --heads --tags origin` returned `refs/heads/dev` at `6b58ccdc9bb710c638c75d3db503f1930bd3236a` and `refs/heads/main` at `e173a0e9f6ea4c688ab0c91daca6b75d5d78137f`. The queried release/Sprint 3 refs and `feat/m4-financial-reports` were absent. Inspection of the retrieved `dev` tree found no `financial-report` route under `backend/Analytics`, and the local reconciliation commit was outside its ancestry. | **Not verified**. No pushed integrated reporting SHA was identified. An or the integration owner provides the pushed branch/tag containing the integrated report API, client and tests. Match `git rev-parse HEAD` to the remote SHA in a clean checkout before opening a release run. |
| D | Local `feat/m4-financial-reports`, HEAD `d2c261bd20c97355750a7f10dfd911afc9ca69d6` for D1–D8. Commit `44ab7ad` then changed the report view, view model and their headless tests, so D9–D11 reran the affected client checks on HEAD `7466cc1bcc42b4e528f09c4c72d03f2c5b012480`. Backend code is unchanged between the two SHAs (the only shared change is a `FinancialReportDto` doc comment), so D1–D3, D5 and D8 still apply. Windows NT `10.0.26200.0` x64, .NET SDK `10.0.404`, configuration `Release`. The local history contains backend reconciliation, desktop reporting and the warranty precision fix. | Local diagnostics. D run results and original Sprint 1 or 2 evidence remain attributable to their own builds. |

**Post-merge ref check.** The current branch is `feat/m4-financial-report` (singular), with clean merge HEAD `2bd3c15a9a39b6adf433ab567edffa44b516469c` at the start of the rerun. A direct GitHub ref lookup returned `refs/heads/feat/m4-financial-report` at `74fc2dd1f8230b03c2dbfb7961520c9f71a011a9`. `git rev-list --left-right --count 74fc2dd1f8230b03c2dbfb7961520c9f71a011a9...HEAD` reported 0 and 26. The pushed branch predates the merge that includes the voucher and loyalty changes from `dev`. The release gate stays open until the merged candidate or another designated integrated ref is on the remote.

**Pinned candidate.** On November 25, 2026, `git ls-remote origin refs/heads/feat/m4-financial-report` returned `01ed5d2531a163ea9b927d901e802bb64fef2cfc`, and `git rev-parse HEAD` in the clean checkout returned the same SHA (`git status` empty). The ref contains the report API (`backend/Analytics/AnalyticsController.cs`, `Services/AnalyticsService.FinancialReport.cs`, `Repositories/AnalyticsRepository.FinancialReport.cs`), the desktop client (`FinancialReportView`, `FinancialReportViewModel`) and the scoped suites (`FinancialReportPostgresHttpTests`, `FinancialReportLiveReconciliationPostgresHttpTests`, `FinancialReportViewModelTests`, `FinancialReportApiIntegrationTests`). Environment: Windows NT `10.0.26200.0` x64, .NET SDK `10.0.404`, configuration `Release`. Build command: `dotnet build TechStore.sln -c Release`. Build identities: `TechStore.Api.dll` SHA-256 `1FAE99218303510F26EBDF218671D76A4099C9FE69939C830DBE279BA8BCCCE8`; Skia and Windows native client DLL hashes are those of M6 and M7. The code differs from the tested `2bd3c15` tree only by `WarrantyCaseService.cs` restored to the `dev` version, which M12 already covers. The M12 runner results therefore apply to `01ed5d2`; the commit itself only adds documentation and OpenSpec files.

The acceptance table records results on the pushed integrated SHA. `R0` in the SHA column means the candidate SHA is pending. **Pass** requires observed agreement with Expected. **Fail** records a mismatch on an executed case. **Skipped** records an explicit skip and its reason. **Not verified** records the absence of usable release evidence. Retain the original failure, fix or decision and rerun result with their run IDs when a case is rerun.

### Acceptance case matrix

All calendar inputs use UTC+07. Diagnostic mappings below identify the runner or manual steps to use. Each row is a distinct acceptance case.

| Case | Input | Expected | Actual on pushed release | SHA / run | Status and rerun |
| --- | --- | --- | --- | --- | --- |
| FR-01 Report authorization | Admin, Manager, SalesStaff, WarehouseStaff, absent/invalid JWT | Admin/Manager get the same figures (200), restricted roles get 403, missing or invalid JWT gets 401. | M12-2 matched the role matrix: Admin and Manager 200 with equal figures, SalesStaff and WarehouseStaff 403, absent or invalid JWT 401. M13 repeated Manager 200, `cashier1` and `warehouse1` 403 and anonymous 401. | 01ed5d2 / M12-2, M13 | Pass. |
| FR-02 Custom range | Single day, 31 days, malformed, reversed or future range | Valid ranges have the expected daily rows. Invalid ranges produce field-specific 400 responses before aggregation. | M12-2 passed the custom-range tests, including the 400 validation responses. | 01ed5d2 / M12-2 | Pass. |
| FR-03 Sale and payment timing | UTC+07 rollover, completed and undelivered orders, as-of cutoff | Original sales stay on delivery day, receipts stay on payment day, the cutoff and zero-activity days are correct. | M12-2 passed the saved-sales and payment-timing fixtures. | 01ed5d2 / M12-2 | Pass. |
| FR-04 Cost and allocation | Known zero, missing unit cost, legacy allocation | The response preserves known zero, propagates unavailable cost, and identifies allocation mismatches. | M12-1 and M12-2 passed the known-zero, missing-cost and legacy-cost tests. | 01ed5d2 / M12-1, M12-2 | Pass. Vy still confirms legacy allocations (see dependencies). |
| FR-05 Ranking and cashier | Multiple items and tied sales | Top products have stable ordering and cashier figures retain stored attribution. | M12-2 passed the ranking and cashier fixtures. | 01ed5d2 / M12-2 | Pass. |
| FR-06 M3 Dashboard regression | October 2026 delivery and Cash checkout fixtures | Existing Dashboard KPIs, permission and Vietnam-day behavior still match the M3 tests. | M12-2 and M12-3 passed the October 2026 Dashboard and Cash fixtures; M13 matched the October 15–21 figures. | 01ed5d2 / M12-2, M12-3, M13 | Pass. |
| FR-07 Partial then full refund | Two VND 1,000,000 items, VND 200,000 discount, 10% VAT | Two VND 990,000 payouts, VND 90,000 refund VAT each and one reversal per paid line. Original sale stays recorded. | M12-2 matched the Sprint 2 Expected table: two VND 990,000 payouts with VND 90,000 refund VAT each. | 01ed5d2 / M12-2 | Pass. |
| FR-08 Sellable restock and defective return | Refund before 17:00Z, physical restock after 17:00Z | VND 600,000 cost reversal on the next local day. Defective unit retains cost and its IMEI is unavailable for sale when tracked. | M12-2 matched the VND 600,000 next-day cost reversal and the defective-unit rejection. | 01ed5d2 / M12-2 | Pass. |
| FR-09 Warranty | Replacement with a known-cost spare and a separate repair handback | One issue movement contributes replacement cost only. Repair handback adds zero cost. Gross profit stays separate. | M12-2 matched VND 500,000 warranty cost with net gross profit VND 800,000; the repair handback added zero cost. | 01ed5d2 / M12-2 | Pass. Replacement fixture ownership stays with Q. Vinh. |
| FR-10 Shift and payment channels | Different collecting and paying shifts, seeded or manually reconciled QR | Cash receipts, payouts and closing balances match the ledgers. VietQR remains outside the drawer. | M12-2 matched Cash receipts, payouts and closing balances. The seeded QR payout stays outside the drawer; the manual QR refund kept sales and the drawer at zero. | 01ed5d2 / M12-2 | Pass for seeded and manual modes. Provider or webhook proof stays under FR-QR01. |
| FR-11 Daily and period reconciliation | Complete 2025 four-day Cash example and an empty day | Known daily amounts sum to the period. Cumulative gross profit is VND -300,000 before restock and VND 300,000 after restock. | M12-2 matched daily-to-period sums and cumulative gross profit VND -300,000 then VND 300,000. | 01ed5d2 / M12-2 | Pass. |
| FR-12 Replay and rollback | Idempotent request keys, invalid operation, database failure | Replays and rejected operations add no duplicate effects. Transactional failure leaves committed records unchanged. | M12-2 passed replay, failed-refund rollback and defective-IMEI rejection. | 01ed5d2 / M12-2 | Pass. |
| M3-SEED-01 Seed regression | Clean migrated database, then repeat seed | October 2026 report fixtures, roles and referential links survive rerun without duplicate seed data. | M12-3 passed the three seed tests (empty and repeated seed, legacy identities, partial-seed rollback). | 01ed5d2 / M12-3 | Pass. |
| FR-C01 Client snapshot | Authorized and restricted sessions, valid period | Client matches the captured report and keeps privileged figures hidden for restricted roles. | M12-4 passed 90/90 headless tests and M12-5 matched the authorized client figures with the live API. | 01ed5d2 / M12-4, M12-5 | Pass. |
| FR-R01 Last available unit | Two concurrent checkout requests for one remaining unit | One checkout commits and PostgreSQL shows overlapping transactions. | M8 recorded PostgreSQL PIDs 68177 and 68185 overlapping 15:04:56.752315Z–15:04:56.783161Z with HTTP 201 and 409 and one persisted sale. M12-2 passed the same suite. | 01ed5d2 / M8, M12-2 | Pass. |
| FR-R02 Same IMEI | Concurrent refund and warranty intake | Exactly one succeeds, with PostgreSQL overlap evidence. | M8 recorded warranty-first PIDs 68254 and 68260 (15:04:59.799668Z–15:04:59.820454Z) and refund-first PIDs 68486 and 68490 (15:05:08.501033Z–15:05:08.543711Z), each with HTTP 200 and 409. M12-2 passed the same suite. | 01ed5d2 / M8, M12-2 | Pass. |
| FR-QR01 Provider receipt | New confirmation, callback replay or late payment | Observed provider proof and bank event date reconcile to the report. | No release provider run | R0 | Not verified. Đạt provides provider setup and confirmation. An provides payout proof where applicable. |
| FR-V01 Voucher | Redemption, restoration where applicable and two users competing for the last slot | Correct balances and one winning use with PostgreSQL overlap evidence. | No release voucher run | R0 / M2 pricing, M9 service checks | Not verified. Vy and Đạt provide compatible flow and final-slot fixture. |
| FR-AI01 Assistant access | Same financial question and range for all four roles | Authorized values match the captured report. Restricted roles receive no financial figures. | No release AI run | R0 | Not verified. T. Vinh provides integrated tool and test setup. |
| FR-S01 Skia build | `net10.0-desktop`, Release | Build succeeds for the pinned SHA. | Release build of `net10.0-desktop` succeeded with 0 errors; client DLL SHA-256 `DF33F4BFA7ACE33DF859D81C4168B5FBF2CA8000396E19A1570E249B6B94F8D1`. | 01ed5d2 / M6 | Pass. |
| FR-S02 Skia startup | Isolated profile, release API | Application opens and authenticates against the selected API. | User-reported: the Release client opened and signed in against the local API. | 01ed5d2 / user run | Pass (user-reported, no log). |
| FR-S03 Skia data and roles | Manager, one restricted role, day and period | Live values and Vietnamese/VND format match HTTP; restricted role cannot read the report. | User-reported: figures and Vietnamese/VND format matched the API. Restricted-role denial is covered by FR-01 over HTTP. | 01ed5d2 / user run | Pass (user-reported, no log). |
| FR-S04 Skia session and retry | Connection loss, change session during load | Retry uses the applied range and stale responses cannot appear in another session. | User-reported pass for retry and session change. | 01ed5d2 / user run | Pass (user-reported, no log). |
| FR-S05 Skia accessibility and size | Keyboard, focus, 800×600, 1024×768, 1440×1000 DIP, text scale | Controls and report data remain navigable and readable. | User-reported pass for keyboard, focus and the three sizes. | 01ed5d2 / user run | Pass (user-reported, no log). |
| FR-S06 Skia theme | Live Light/Dark/System changes with report open | Same panel refreshes its theme and preserves the snapshot and range. | User-reported: live Light/Dark change with the report open passed. | 01ed5d2 / user run | Pass (user-reported, no log). |
| FR-W01 Native build | `net10.0-windows10.0.26100`, Release | Build succeeds for the pinned SHA. | Release build of `net10.0-windows10.0.26100` succeeded with 0 errors; client DLL SHA-256 `FE5EA145BDB6FD7B8CBCE668D8FD5BF569EA1A68D1CF38ABF1A89BC8A8078A6F`. | 01ed5d2 / M7 | Pass. |
| FR-W02 Native startup | Windows launch profile and isolated release API | Application opens and reaches login. | User-reported: the native client opened and signed in against the local API. | 01ed5d2 / user run | Pass (user-reported, no log). |
| FR-W03 Native data and roles | Manager, one restricted role, day and period | Live values and Vietnamese/VND format match HTTP; restricted role cannot read the report. | User-reported: figures and Vietnamese/VND format matched the API. Restricted-role denial is covered by FR-01 over HTTP. | 01ed5d2 / user run | Pass (user-reported, no log). |
| FR-W04 Native session and retry | Connection loss, change session during load | Retry uses the applied range and stale responses cannot appear in another session. | User-reported pass for retry and session change. | 01ed5d2 / user run | Pass (user-reported, no log). |
| FR-W05 Native accessibility and size | Keyboard, focus, 800×600, 1024×768, 1440×1000 DIP, text scale | Controls and report data remain navigable and readable. | User-reported pass for keyboard, focus and the three sizes. | 01ed5d2 / user run | Pass (user-reported, no log). |
| FR-W06 Native theme | Live Light/Dark/System and High Contrast where supported | Same panel refreshes its theme and preserves the snapshot and range. | User-reported: live Light/Dark change with the report open passed. High Contrast was not reported. | 01ed5d2 / user run | Pass (user-reported, no log). High Contrast not tested. |

**Release case totals** — 31 planned, 28 Pass, 0 Fail, 0 Skipped, 3 Not verified (FR-QR01, FR-V01, FR-AI01). Pass coverage is `28/31`. Executed-only pass and fail rates are `28/28` and `0/28`. Of the 28 passes, 10 desktop rows (FR-S02–06 and FR-W02–06) are user-reported without a runner log, and 18 rest on runner output, build results or API checks. The diagnostic runner denominators are reported separately below.

### Local Release diagnostics

The D runs are tied to the local code and `Release` build configuration above. The loopback API was built with SHA-256 `AF6EE3BCEE8DD10EDFE180B142FBA8AC9358032E39CDD4CFD4C8CE1BEF26EE8F` for `TechStore.Api.dll`. PostgreSQL was 16.15. The HTTP runner generated disposable databases. The Analytics reset and seed-schema runs used separate disposable database names containing `test`, with each runner's required opt-in enabled.

| Run ID | Group | Pass | Fail | Skipped | Total | Executed | Exit and observation |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| D1 | Backend unit and endpoint policy coverage | 40 | 0 | 1 | 41 | 40 | 0. The live anonymous/restricted-role test skipped because `TECHSTORE_TEST_CONNECTION` was unset for this invocation. |
| D2 | Financial report, reconciliation and M3 Dashboard HTTP | 16 | 0 | 0 | 16 | 16 | 0. `TECHSTORE_TEST_CONNECTION` set for the disposable PostgreSQL fixture. |
| D3 | Analytics and seed PostgreSQL integration | 4 | 0 | 0 | 4 | 4 | 0. Analytics reset opt-in and seed schema opt-in enabled on separate test databases. |
| D4 | Frontend headless | 88 | 0 | 0 | 88 | 88 | 0 on rerun. The first build stopped before test discovery with CS2012 on the shared DLL during a competing build. The sequential rerun eliminated the file lock. |
| D5 | Client-to-API fixture | 1 | 0 | 0 | 1 | 1 | 0. Isolated API `http://127.0.0.1:55176/`, October 15–21 demo window. |
| D8 | Rerun of the D1 skipped authorization test | 1 | 0 | 0 | 1 | 1 | 0 after enabling `TECHSTORE_TEST_CONNECTION` on the generated test database. |
| D9 | Frontend headless rerun on `7466cc1` | 90 | 0 | 0 | 90 | 90 | 0 on November 25, 2026 at 13:07Z. Same filter as D4. The two added tests come from `44ab7ad`. |

For D1–D5, runners reported 149 Pass, 0 Fail and 1 Skipped out of 150 test instances. The executed-only rates were `149/149` Pass and `0/149` Fail. D8 is a separate `1/1` recorded rerun of the skipped D1 test. D9 is a separate `90/90` rerun of the D4 filter on `7466cc1`, and it supersedes D4 for the current local client code. The initial D4 compilation failure had no runner denominator. The selected release has zero runner invocations pending R0's replacement. Its runner rates are N/A.

Commands used for these diagnostics:

```powershell
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj -c Release --no-restore --filter 'FullyQualifiedName~AnalyticsControllerTests|FullyQualifiedName~AnalyticsServiceTests|FullyQualifiedName~AnalyticsQueryTranslationTests|FullyQualifiedName~ApiAuthorizationCoverageTests|FullyQualifiedName~DataSeederTests' -v quiet
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~FinancialReportPostgresHttpTests|FullyQualifiedName~FinancialReportLiveReconciliationPostgresHttpTests|FullyQualifiedName~DashboardLiveFlowPostgresHttpTests' --logger 'console;verbosity=detailed'
dotnet test tests/Backend.IntegrationTests/TechStore.Backend.IntegrationTests.csproj -c Release --no-restore --filter 'FullyQualifiedName~AnalyticsPostgresIntegrationTests|FullyQualifiedName~DataSeederPostgresIntegrationTests' --logger 'console;verbosity=normal'
dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj -c Release --no-restore --filter 'FullyQualifiedName~FinancialReport|FullyQualifiedName~DashboardViewModelTests|FullyQualifiedName~AnalyticsApiRepositoryTests|FullyQualifiedName~FixtureAnalyticsRepositoryTests|FullyQualifiedName~DashboardPreviewOptionsTests' -v quiet
dotnet test tests/Frontend.IntegrationTests/TechStore.Client.IntegrationTests.csproj -c Release --no-restore --filter FullyQualifiedName~FinancialReportApiIntegrationTests -p:BuildProjectReferences=false -v quiet
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~RegisteredEndpoints_AnonymousAndDisallowedRoles_ReturnSafe401And403 -v quiet
```

`TECHSTORE_ANALYTICS_TEST_POSTGRES` and `TECHSTORE_ANALYTICS_ALLOW_DATABASE_RESET=YES` were set for the reset suite. `TECHSTORE_SEED_TEST_POSTGRES` and `TECHSTORE_SEED_TEST_ALLOW_SCHEMA=YES` were set for the seed suite. D5 used `TECHSTORE_API_BASE_URL` for the loopback API. D1–D4 and the D5 tests target `net10.0`. D5 reused the already built Release shared assembly.

- **D1–D3 case mapping**. FR-01 observed 200 for Admin/Manager, 403 for SalesStaff/WarehouseStaff and 401 with missing or invalid JWT. FR-02 observed HTTP 400 for invalid ranges. FR-03 through FR-05 passed the original sales, cutoff, legacy cost and ranking fixtures. FR-06 passed both existing Dashboard live-flow checks. M3-SEED-01 passed clean seed, repeat seed, legacy identity preservation, partial seed rollback and repository regression checks.
- **D2 Cash and after-sales observations**. FR-07 through FR-12 agreed with the Expected table in the Sprint 2 Cash example. Order `a1d815d3-ac13-49f6-b5b8-90951f6551a7` reconciled at `2026-11-25T12:50:05.433579Z`. Period receipt and payout were VND 1,980,000 each. Final net cost was VND 600,000 and net gross profit was VND -600,000. The replacement and handback, defective IMEI rejection, failed settlement rollback, undelivered manual QR payout and seeded confirmed QR receipt tests also passed.
- **D2 contention observations**. FR-R01 had PIDs 52248 and 52256 overlapping `12:49:55.712478Z`–`12:49:55.726775Z` on November 25, 2026, with checkout HTTP 201 and 409. FR-R02 had PIDs 52325/52331 overlapping `12:49:59.149184Z`–`12:49:59.201070Z` and PIDs 52564/52568 overlapping `12:50:08.658260Z`–`12:50:08.698467Z`. Each queue order produced one 200 and one 409, matching the expected winner and persisted effects.
- **D4–D5 client observations**. FR-C01 passed the selected headless cases, including Dashboard regression, role/range, VND, retry and session changes. D9 repeated the headless cases on `7466cc1` after the focus and range-label changes, with 90 Pass. The authorized client matched the captured live demo response in D5. Desktop runtime evidence is tracked separately below.

#### Desktop build identities

| Run | Target | Build result | Client assembly SHA-256 |
| --- | --- | --- | --- |
| D6 | `net10.0-desktop` (Skia on Windows x64) | Pass, exit 0 with zero warnings and errors | `BD06804DA1A46402DEDB11D7A2BA3818561C06A8BF3559FAA7ABCA1F5070FF3B` |
| D7 | `net10.0-windows10.0.26100` (Windows native x64) | Pass, exit 0 with zero warnings and errors | `4A755282A1241EB160BA1C49943A5D0C2BB9B942396B4E82925AFFD5EA1E4240` |
| D10 | `net10.0-desktop` (Skia on Windows x64), HEAD `7466cc1` | First attempt at 13:07Z: **Fail**, exit 1. MSB3021/MSB3027 could not copy `TechStore.Shared.dll` into `bin/Release/net10.0-desktop` because a running `TechStore.Client` process (PID 41320) held it. This is an environment lock, not a compile error. Rerun with a separate `OutDir`: **Pass**, exit 0 with zero errors at 13:09Z. | `75603680E13C142FBF277C3551E86B822A00BC2258E4EB03B1E4657CE4D23AAA` (identical to the `obj/Release/net10.0-desktop` assembly) |
| D11 | `net10.0-windows10.0.26100` (Windows native x64), HEAD `7466cc1` | Pass, exit 0 with zero warnings and errors at 13:08Z | `7914A0CA4E09C3CE121EABC365FC81C2EF559F4F7E405B2B746120DEFD586E22` (`bin/Release/net10.0-windows10.0.26100/TechStore.Client.dll`; the `AppX/` copy was not refreshed by `dotnet build` and still carries the D7 hash) |

```powershell
dotnet build frontend/TechStore.Client/TechStore.Client.csproj -c Release -f net10.0-desktop -p:BuildProjectReferences=false -v quiet
dotnet build frontend/TechStore.Client/TechStore.Client.csproj -c Release -f net10.0-windows10.0.26100 -p:BuildProjectReferences=false -v quiet
```

D10's rerun added `-p:OutDir=<separate folder>` to the first command. D6 and D7 built `d2c261b`. D10 and D11 built `7466cc1`. Close any running desktop client before building into `bin/` for release runs.

### Integrated local rerun

All M runs below used source SHA `2bd3c15a9a39b6adf433ab567edffa44b516469c` on November 25, 2026. The checkout was clean at the initial verification, and only handoff evidence was edited during the reruns. The host was Windows NT `10.0.26200.0` x64 with .NET SDK `10.0.404`. Test target was `net10.0`, configuration `Release`. The standalone loopback API used Development hosting for demo seed and the Release assembly SHA-256 `304CFB330347EFE3F384F4DB8FAF0B24B3EA34C7905583CB256B4C45A693DA1D`.

| Run | Group | Pass | Fail | Skipped | Total | Executed | Exit and scope |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| M1 | Backend unit and endpoint policy coverage | 40 | 0 | 1 | 41 | 40 | 0. The endpoint-wide HTTP authorization check requires `TECHSTORE_TEST_CONNECTION` and was rerun in M2. |
| M2 | Financial report, reconciliation, M3 Dashboard, authorization and voucher pricing HTTP | 18 | 0 | 0 | 18 | 18 | 0. PostgreSQL 16.15 with `TECHSTORE_TEST_CONNECTION` pointed to the isolated server for generated disposable databases. |
| M3 | Analytics repository and seed integration | 4 | 0 | 0 | 4 | 4 | 0. PostgreSQL 16.15, disposable databases with `test` in their names, one per runner. Analytics reset and seed schema opt-ins were both enabled. |
| M4 | Frontend headless | 90 | 0 | 0 | 90 | 90 | 0. Same report, Dashboard, Analytics repository and preview filter as D9. |
| M5 | Frontend client-to-API | 1 | 0 | 0 | 1 | 1 | 0. `TECHSTORE_API_BASE_URL=http://127.0.0.1:55176/` against a separate migrated and seeded PostgreSQL test database. |

M1–M5 reported 153 Pass, 0 Fail and 1 Skipped out of 154 test instances. The executed-only rates were `153/153` Pass and `0/153` Fail. M2 passed the endpoint authorization test that M1 skipped. These totals describe the merged local checkout.

```powershell
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj -c Release --no-restore --filter 'FullyQualifiedName~AnalyticsControllerTests|FullyQualifiedName~AnalyticsServiceTests|FullyQualifiedName~AnalyticsQueryTranslationTests|FullyQualifiedName~ApiAuthorizationCoverageTests|FullyQualifiedName~DataSeederTests' -v quiet
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~FinancialReportPostgresHttpTests|FullyQualifiedName~FinancialReportLiveReconciliationPostgresHttpTests|FullyQualifiedName~DashboardLiveFlowPostgresHttpTests|FullyQualifiedName~RegisteredEndpoints_AnonymousAndDisallowedRoles_ReturnSafe401And403|FullyQualifiedName~CreateOrder_VoucherVipAndManual_PriceExactlyOneSourceAndNeverConsumeAVoucherUse' --logger 'console;verbosity=detailed'
dotnet test tests/Backend.IntegrationTests/TechStore.Backend.IntegrationTests.csproj -c Release --no-restore -p:BuildProjectReferences=false --filter 'FullyQualifiedName~AnalyticsPostgresIntegrationTests|FullyQualifiedName~DataSeederPostgresIntegrationTests' --logger 'console;verbosity=normal'
dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj -c Release --no-restore --filter 'FullyQualifiedName~FinancialReport|FullyQualifiedName~DashboardViewModelTests|FullyQualifiedName~AnalyticsApiRepositoryTests|FullyQualifiedName~FixtureAnalyticsRepositoryTests|FullyQualifiedName~DashboardPreviewOptionsTests' -v quiet
dotnet test tests/Frontend.IntegrationTests/TechStore.Client.IntegrationTests.csproj -c Release --no-restore --filter FullyQualifiedName~FinancialReportApiIntegrationTests -p:BuildProjectReferences=false -v quiet
```

M3 used `TECHSTORE_ANALYTICS_TEST_POSTGRES` and `TECHSTORE_ANALYTICS_ALLOW_DATABASE_RESET=YES` for Analytics. The seed run used `TECHSTORE_SEED_TEST_POSTGRES` and `TECHSTORE_SEED_TEST_ALLOW_SCHEMA=YES`.

- **FR-01 through FR-06 and M3-SEED-01.** M2 matched the 200/403/401 role matrix and the 400 range validation, and passed the saved-sales, timing, legacy cost, ranking and October 2026 Dashboard fixtures. M3 passed repository reconciliation and the three seed tests for empty and repeated seed, legacy identities and partial-seed rollback.
- **FR-07, FR-08, FR-10 through FR-12.** M2 matched the Sprint 2 Expected table for order `eb820e5b-ea95-4c23-8132-6de9f25b5011` at `2026-11-25T14:51:37.841556Z`. Across October 28–31, 2025, Cash received was VND 1,980,000, the two payouts were VND 990,000 each with VND 90,000 VAT each, and the October 30 restock reversed VND 600,000 cost. Cumulative gross profit moved from VND -300,000 to VND 300,000 and ended at VND -600,000 following the defective return. Daily amounts reconciled with the period. The manual QR refund of VND 1,100,000 for an undelivered order kept sales and the drawer movement at zero. The original M3 confirmed QR fixture supplied the seeded receipt proof. Replay, failed refund rollback and defective IMEI rejection all passed.
- **FR-09.** The warranty replacement reported VND 500,000 as warranty cost and held net gross profit at VND 800,000. The separate repair handback added no warranty cost.
- **FR-R01.** PostgreSQL PIDs 65796 and 65805 overlapped `14:51:26.342024Z`–`14:51:26.384587Z`, with HTTP 201 and 409 and one persisted sale. **FR-R02.** The warranty-first ordering overlapped `14:51:30.486048Z`–`14:51:30.526570Z` (PIDs 65874 and 65881). The refund-first ordering overlapped `14:51:41.046237Z`–`14:51:41.075630Z` (PIDs 66114 and 66118). Both yielded HTTP 200 and 409 and exactly one committed outcome. The intervals are on November 25, 2026.
- **FR-C01.** M4 confirmed role scope, formatting, range and retry behavior, empty and unavailable amounts, and rejection of stale responses across range or session changes. M5 matched the authorized client's fixture figures with the live API.
- **FR-V01.** The merged API exposes voucher management. M2 verified mutually exclusive voucher, VIP and manual pricing sources, with voucher use count unchanged during pricing. Redemption, restoration and the final-slot race still require their own evidence.

#### Integrated local desktop checks

M6 and M7 ran the Skia and Windows native build commands above with exit 0 and zero errors. The startup diagnostics set `TECHSTORE_DASHBOARD_DEMO=0` and `TECHSTORE_API_BASE_URL=http://127.0.0.1:55176/` with no saved TechStore session. Each process was started directly from its target directory.

| Run | Case | Client DLL SHA-256 | Startup and rerun evidence |
| --- | --- | --- | --- |
| M6 | FR-S01 and FR-S02–06, `net10.0-desktop` | `DF33F4BFA7ACE33DF859D81C4168B5FBF2CA8000396E19A1570E249B6B94F8D1` | Build **Pass**. The window displayed the Vietnamese login controls. The process stopped responding before Manager sign-in could be confirmed. Report interaction, VND, keyboard and focus, resize and theme are **Not verified** for this target. Rerun on a responsive interactive host. |
| M7 | FR-W01 and FR-W02–06, `net10.0-windows10.0.26100` | `FE5EA145BDB6FD7B8CBCE668D8FD5BF569EA1A68D1CF38ABF1A89BC8A8078A6F` | Build **Pass**. Both direct launches opened a window. The second launch exposed no sign-in controls to UI Automation, and the process was stopped after inspection. Windows startup through login and the report runtime matrix are **Not verified**. The `AppX/` DLL still had the D7 hash, so rerun with the verified release package or current direct build and capture a successful login first. |

#### Pre-push rerun on the integrated local build

- **M8.** Repeated the five M1–M5 commands using `--no-build --no-restore` and the same filters and opt-ins on `2bd3c15a9a39b6adf433ab567edffa44b516469c`. The standalone API ran from the verified Release executable on loopback with a newly migrated and seeded test database. Backend, PostgreSQL/HTTP, PostgreSQL Analytics/seed, frontend headless and client-to-API passed `40/41`, `18/18`, `4/4`, `90/90` and `1/1`. Total was 153 Pass, 0 Fail and 1 Skipped out of 154. The one authorization test skipped without the opt-in passed in the HTTP run. Manager's demo report matched the two documented October 2026 windows at `2026-11-25T15:06:32.964309Z` and `2026-11-25T15:06:33.044872Z`.
- **M8 concurrency.** FR-R01 recorded PostgreSQL PIDs 68177 and 68185 overlapping `15:04:56.752315Z`–`15:04:56.783161Z` with HTTP 201 and 409. FR-R02 recorded warranty-first PIDs 68254 and 68260 overlapping `15:04:59.799668Z`–`15:04:59.820454Z`, and refund-first PIDs 68486 and 68490 overlapping `15:05:08.501033Z`–`15:05:08.543711Z`. Each order yielded HTTP 200 and 409 on November 25, 2026.
- **M9.** Ran the existing voucher and loyalty coverage on the same SHA with `TECHSTORE_TEST_CONNECTION` and generated disposable PostgreSQL databases. All 10 tests passed. Voucher management and loyalty access passed over HTTP. Service tests confirmed one successful reservation for the last slot, single consumption, release of an unpaid hold and retention of a used redemption. FR-V01 still requires the checkout and refund workflow and timestamped transaction overlap. M8 and M9 together had 163 Pass, 0 Fail and 1 Skipped out of 164 runner instances. Executed-only rates were `163/163` Pass and `0/163` Fail.
- **M11 warranty file check.** Commit `8249074` (microsecond truncation in `WarrantyCaseService.cs`) belongs to Q. Vinh's warranty scope, so the working tree reverted that file to the `origin/dev` version. With that version, the five M2 filters again passed `18/18`, so report, reconciliation and FR-09 evidence do not depend on the truncation. The two `PosCheckoutPostgresHttpTests` warranty cases `WarrantyCase_WalkInReplacement_ReplayKeepsOneMovementAndInternalCost` and `WarrantyCase_MemberRepair_RequiresCustomerAndActiveCoverageThenReturnsSameDevice` fail on `origin/dev` on Windows (2 of 4 `WarrantyCase_*` tests) and pass with the truncation. Q. Vinh owns that rerun and fix.
- **M12 Release rerun on the tree to push.** Tested tree: `2bd3c15a9a39b6adf433ab567edffa44b516469c` plus `WarrantyCaseService.cs` restored to the `origin/dev` version; the only other edits are this document and the change's `tasks.md`. Windows NT `10.0.26200.0`, .NET SDK `10.0.404`, `Release`, PostgreSQL 16.15, November 25, 2026.

  | Run | Group | Pass | Fail | Skipped | Total |
  | --- | --- | ---: | ---: | ---: | ---: |
  | M12-1 | Backend unit and endpoint policy | 40 | 0 | 1 | 41 |
  | M12-2 | Report, reconciliation, M3 Dashboard, authorization and voucher pricing HTTP | 18 | 0 | 0 | 18 |
  | M12-3 | Analytics repository and seed integration | 4 | 0 | 0 | 4 |
  | M12-4 | Frontend headless | 90 | 0 | 0 | 90 |
  | M12-5 | Frontend client-to-API | 1 | 0 | 0 | 1 |

  M12-1 to M12-5 reported 153 Pass, 0 Fail and 1 Skipped out of 154 test instances, and the executed-only rates were `153/153` Pass and `0/153` Fail. The skipped authorization check needs `TECHSTORE_TEST_CONNECTION` and passed inside M12-2. Commands and filters match M1–M5 and use the same opt-ins. M12-3 used two newly created disposable databases (`techstore_analytics_test_r3`, `techstore_seed_test_r3`) with `test` in their names, and M12-5 ran against a Release API on `http://127.0.0.1:55176/` backed by a fresh migrated and seeded `techstore_api_test_r3` database with a throwaway JWT key. The databases and the API process were removed afterward. Concurrency rows keep the PostgreSQL overlap evidence recorded in M8. The FR-V01 and desktop rows are unchanged.
- **Desktop rerun.** The preflight found an existing saved TechStore session in the current Windows profile, so the isolated UI retry did not launch either client. M6 and M7 remain the latest startup observations. Retry the desktop cases in a clean profile on the pushed build with the test API.

```powershell
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~LoyaltyVoucherServicesPostgresTests|FullyQualifiedName~Vouchers_ManagementRulesRolesAndStatus_WorkThroughTheApi|FullyQualifiedName~LoyaltySummary_ReadsTierAndNextTier_ForCounterRolesOnly' --logger 'console;verbosity=normal'
```

### Demo data and run instructions

The documented inputs below were checked on the isolated local API built in Release configuration. The merged M5 API returned the same seven-row sales and receipt totals at `2026-11-25T14:51:53.790321Z`, and the three-row sale, refund, restock and gross profit totals at `2026-11-25T14:51:53.970256Z`. Verify them again on the pushed candidate before using them for release footage.

**M10 API rehearsal.** On local SHA `2bd3c15a9a39b6adf433ab567edffa44b516469c`, a fresh PostgreSQL demo database reproduced the seven M3 rows at `2026-11-25T15:12:22.336280Z` and all three M4 rows at `2026-11-25T15:12:22.474964Z` with `UTC+07:00`. Manager's October 25 sale, October 26 seeded QR payout and October 27 restock matched the figures in the table below. Eight daily-to-period amount comparisons agreed. `GET /api/pos/shifts/f7161764-6b1e-6fac-26c9-a25990d7199a` returned VND 100,000 opening cash, VND 1,980,000 Cash receipts, zero Cash refunds and VND 2,080,000 closing cash. The October 25–27 report returned HTTP 403 for SalesStaff. These observations cover the data and role steps in the video script. Record the desktop footage after the chosen target renders the same range on the pushed candidate.

**M13 pushed-candidate rehearsal.** On `01ed5d2531a163ea9b927d901e802bb64fef2cfc`, the Release API ran on a fresh migrated and seeded PostgreSQL 16.15 database (`http://127.0.0.1:55176`, Development hosting) and was removed afterward. Manager `GET /api/analytics/financial-report?period=custom` returned: **October 15–21** seven daily rows, sales excluding VAT VND 85,620,000, cost of goods sold VND 71,550,000, Cash received VND 88,630,000, VietQR received VND 33,970,000 and gross profit VND 14,070,000. **October 25–27** three daily rows with sales VND 1,800,000, VAT VND 180,000, cost VND 1,200,000, Cash received VND 1,980,000, VietQR refund paid VND 1,980,000, restocked cost VND 600,000, net sales VND 0 and net gross profit VND -600,000, with timezone `UTC+07:00`. Warranty cost was VND 0 in both windows. SalesStaff-equivalent counter role `cashier1` and `warehouse1` received HTTP 403 and an anonymous call received 401. The shift drawer totals recorded in M10 were not re-queried on this SHA. Modes: the Cash sale and the QR payout are seeded records, not provider or webhook payments.

**Desktop startup on the pinned candidate.** On November 25, 2026, the user launched the Skia (`net10.0-desktop`) and Windows native (`net10.0-windows10.0.26100`) clients at `01ed5d2` and signed in on both. The first launch did not record the build configuration. The user then reran both targets with the Release commands (`-c Release -f net10.0-desktop` and `-c Release -f net10.0-windows10.0.26100`) against the local API and reported that the runtime checks passed on both. This is a user-reported observation without runner log or screenshots. The acceptance matrix and its totals above record these results.

1. Provide a disposable PostgreSQL database and configure the API using the existing local environment or user-secrets mechanism. Set the database connection and JWT configuration privately. Use the Development hosting profile on the Release build to apply existing migrations and demo seed, for example `dotnet run --project backend/TechStore.Api.csproj -c Release --launch-profile http`. The diagnostic substituted `--no-build --no-restore --no-launch-profile` and set `ASPNETCORE_ENVIRONMENT=Development` and `ASPNETCORE_URLS=http://127.0.0.1:55176` for isolation. D3 checks clean and repeated seed on the dedicated test target.
2. Set `TECHSTORE_API_BASE_URL` to this API and `TECHSTORE_DASHBOARD_DEMO=0` for live reporting. Use `dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -c Release -f net10.0-desktop` for Skia or the same command with `-f net10.0-windows10.0.26100` for Windows native. Use an isolated desktop profile. Use the Manager demo identity and the restricted SalesStaff or WarehouseStaff demo identity according to the project setup. Use the agreed demo credentials during recording without showing their values.
3. Select Overview → Financial Report and use `period=custom` with the same `from` and `to` dates in `GET /api/analytics/financial-report`. Compare report figures and the displayed UTC+07 snapshot time against the matching API run. All demonstration dates are in October 2026.

| Window and reference | Expected and observed in the local demo API | Mode and limitation |
| --- | --- | --- |
| October 15–21, M3 sales fixture | Seven daily rows, VND 85,620,000 sales excluding VAT, VND 88,630,000 Cash receipts and VND 33,970,000 VietQR receipts. Local API snapshot `2026-11-25T12:51:31.420745Z`. | Confirmed QR records from the seed. Show the report channel breakdown as fixture data. |
| October 25–27, `M4-202610-0001`, `M4-CABLE-PACK`, `PO-M4-202610-0001` and linked closed sales shift | October 25 shows VND 1,800,000 sales, VND 180,000 VAT, VND 1,980,000 Cash received and VND 1,200,000 original cost. October 26 shows VND 1,980,000 payout and VND 180,000 refunded VAT. October 27 shows VND 600,000 restocked cost. The period shows VND -600,000 net gross profit. The seeded drawer has a VND 100,000 opening balance, VND 1,980,000 receipts and VND 2,080,000 closing balance. Local API snapshot `2026-11-25T12:51:31.644594Z`. | Seeded Cash sale and seeded QR payout. The seed places both refund slices on October 26. The repair handback contributes zero replacement cost. |
| October 28–31, 2025, Sprint 2 controlled HTTP case | Two Cash payouts of VND 990,000 on different days. Gross profit for the cumulative window changes from VND -300,000 to VND 300,000 after restock, with period profit VND -600,000 after the final defective return. | Real transactions on the PostgreSQL test fixture. Use the 2026 seed for recorded walkthroughs when the test host is unavailable to the desktop. |

### Report video segment for An

| Approx. time | Screen action | English narration and evidence to cite |
| --- | --- | --- |
| 0:00–0:10 | Show the verified branch/tag, abbreviated release SHA and application target. Sign in as Manager. | “This is the financial report on our integrated M4 release. The report uses Vietnam calendar days.” Cite the pinned candidate `01ed5d2` and the desktop startup rows FR-S02 or FR-W02. |
| 0:10–0:35 | Open the live Financial Report tab. Select October 25–27, then expand October 25. | “On October 25, sales before VAT were 1.8 million dong. Cash received was 1.98 million and original cost was 1.2 million.” Cite FR-03 and the confirmed desktop data row. |
| 0:35–0:55 | Show October 26 refunds and October 27 cost reversal. Return to the period summary. | “Both seeded refunds are dated October 26. The following day's physical restock restores 600,000 dong of cost. Period gross profit is minus 600,000.” Cite FR-07, FR-08 and FR-11. |
| 0:55–1:15 | Show Cash and QR channels and the shift, then demonstrate the restricted role or show its recorded HTTP 403. | “Cash and bank movements are separate. SalesStaff receives 403 for the financial report.” Cite FR-01 and FR-10. Label the QR amounts as seeded unless a verified provider run supplies replacement footage. |
| 1:15–1:25 | Display the matching case IDs and still-open limitations. | “The QA handoff records the tested release and outstanding checks: 28 of 31 cases pass, and provider QR, voucher redemption and the AI assistant are still open.” Cite FR-QR01, FR-V01 and FR-AI01. An assembles this segment into the team video. |

**Script check and footage status.** Every figure in the script (October 25–27 sale, VAT, cost, Cash, QR payout, restock, net gross profit, seeded shift and the SalesStaff 403) matches the M13 API rehearsal on `01ed5d2` with the seeded Manager and restricted-role accounts. The desktop targets pass FR-S03–06 and FR-W03–06 (user-reported). No screen footage is recorded yet. Footage owner: Bình records the Skia screens on `01ed5d2` using the Manager account and the October 25–27 range; An adds the Windows native capture if the team wants it. Rerun condition: record again if the candidate SHA changes. Limits to state on camera: the QR amounts are seeded, High Contrast was not tested, and FR-QR01, FR-V01 and FR-AI01 are Not verified.

### Dependencies and handoff notes

| Affected cases | Owner and rerun condition | Acceptance impact |
| --- | --- | --- |
| All release rows | An or the integration owner provides a pushed Sprint 3 ref containing reporting, the desktop client and the scoped tests. Bình verifies and reruns on that SHA. | The diagnostic and historical evidence supports review. Release acceptance remains open. |
| FR-QR01 and provider portions of FR-10 | Đạt provides a provider confirmation or webhook path plus sandbox or verified payment proof. An provides bank payout evidence where applicable. | Seeded confirmations and manual payout checks support their own evidence. Live receipt, delayed payment and callback claims stay open. |
| FR-V01 | Vy and Đạt provide the checkout and refund setup for a redeemable voucher and a separate voucher with one redemption left. Verify committed use, eligible restoration and the final-slot race. Capture database overlap and both responses. | The local merge passed pricing, management and service-level voucher tests. The checkout/refund workflow and timestamped last-slot acceptance stay open. |
| FR-09 and any legacy allocation gap in FR-04 | Q. Vinh supplies the warranty issue and stored cost. An supplies linked restock/payout records. Vy confirms applicable historical allocations when needed. | A known-cost replacement can be accepted for its own case. An absent or unreconciled source limits evidence for the affected metric. |
| FR-S02–06 and FR-W02–06 | Bình reruns each target on an isolated profile with the selected API. For native, capture successful startup before the runtime matrix and retain the error if startup fails. | Report interaction, options and video capture are target-specific. Build output and headless checks have separate rows. |
| FR-AI01 | T. Vinh provides the integrated report tool, setup and all four test roles. Verify tool output or denial against the same report range. | The current loopback OpenAPI has no assistant route. Assistant acceptance remains separate from required report API and desktop evidence. |
