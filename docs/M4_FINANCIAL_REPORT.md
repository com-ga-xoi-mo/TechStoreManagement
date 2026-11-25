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
