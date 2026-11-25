# Design

## Context

`AnalyticsController` đã áp dụng role Admin/Manager. Dashboard M3 đọc trong transaction `RepeatableRead`, dùng `statement_timestamp()` và gom ngày UTC+07 tại PostgreSQL. KPI Dashboard lấy `Order.TotalAmount` của đơn đang `Completed`.

`OrderService` lưu số đã định giá lên đơn và từng dòng. `PaidAt` có thể có trước `CompletedAt`. Cash checkout POS ghi hai mốc cùng lúc, lưu giá vốn theo dòng sau khi xuất kho và ghi tổng đơn vào ca. Migration `AddOrderPricingAllocation` thêm phân bổ thuế và giảm giá mặc định 0, không backfill. `OrderReturn` hiện có `RefundAmount`, `RefundedAt` và `IsRestocked`. Dữ liệu đó chưa mô tả đầy đủ tiền hàng chưa thuế hoàn, ngày nhập lại hay chi phí bảo hành theo yêu cầu M4.

## Goals / Non-Goals

- **Goals** — API đọc có hợp đồng ổn định cho T. Vinh, đối chiếu được theo ngày, theo kỳ và với snapshot dòng bán. Biểu diễn rõ số 0, giá vốn thiếu và nguồn chưa tích hợp.
- **Non-Goals** — UI, Gemini tool, sửa dữ liệu cũ, thay đổi định giá, triển khai nguồn hoàn/nhập lại/bảo hành và đối soát số dư ca. Dashboard M3 tiếp tục dùng hợp đồng và KPI hiện tại.

## Decisions

### D1. Kỳ tùy chọn bắt buộc và quyền truy cập

Ví dụ request: `GET /api/analytics/financial-report?period=custom&from=2024-10-12&to=2024-10-14`.

- Sprint 1 chỉ nhận đúng một `period=custom`, một `from` và một `to`. Tái sử dụng quy tắc parse invariant `yyyy-MM-dd`, `AnalyticsLimits.MaxCustomRangeDays` (31 ngày bao gồm hai đầu) và UTC+07 của Dashboard. Các preset và mặc định của Dashboard tiếp tục thuộc Dashboard.
- Từ chối 400 `ValidationProblemDetails` với lỗi theo `period`, `from` hoặc `to` cho giá trị rỗng/thiếu/lặp, kỳ không hỗ trợ, ngày không hợp lệ hoặc không biểu diễn được biên UTC, khoảng đảo ngược, quá dài hoặc ngày cuối ở tương lai theo UTC+07. Kiểm tra cú pháp trước khi đọc DB. Dùng giờ snapshot DB để kiểm tra ngày tương lai trước các truy vấn tổng hợp.
- Dùng `[fromUtc, toExclusiveUtc)`. Hai mốc là nửa đêm UTC+07 đầu kỳ và nửa đêm sau ngày cuối, chặn mốc cuối tại `asOf`. Lấp đầy ngày thiếu theo lịch Việt Nam, gồm cả ngày hiện tại nếu trong kỳ.
- Dùng `[Authorize(Roles = "Admin,Manager")]` sẵn có tại controller và pipeline JWT hiện hành. Admin/Manager cùng phạm vi đọc toàn cửa hàng. SalesStaff/WarehouseStaff nhận 403, thiếu hoặc sai JWT nhận 401, kể cả khi kỳ sai. Chỉ trả dữ liệu tổng hợp cho request thành công.

### D2. Ghi nhận bán và tiền thu theo hai mốc

| Chỉ số | Nguồn và phép tính |
| --- | --- |
| Đơn giao, số lượng | Đơn có `CompletedAt` trong kỳ và status thuộc `Completed`, `PartiallyRefunded`, `Refunded`. Đếm mỗi ID đơn một lần và cộng `Quantity` của dòng. |
| Tiền hàng chưa thuế, thuế bán | Cộng `OrderItem.TotalPrice − OrderItem.OrderDiscountAmount` và `OrderItem.TaxAmount` thành hai trường riêng, theo ngày `CompletedAt`. `TotalPrice` đã gồm giảm giá dòng. |
| Giá vốn bán, lãi gộp phần bán | Giá vốn dòng = `UnitCost × Quantity`. Nếu có dòng thiếu giá vốn, cả hai số của nhóm tương ứng là `null`. Nếu đủ giá vốn, lãi gộp phần bán = tiền hàng chưa thuế − giá vốn bán, có thể âm. |
| Tiền thu gốc | Cộng `Order.TotalAmount` một lần theo `PaidAt` trong kỳ, độc lập với status giao và dòng hàng. Số thu gồm thuế theo tổng đã lưu. Cash POS lưu `CashTendered − ChangeGiven` bằng tổng đơn. |

`Processing` có thể có tiền thu và chưa có doanh thu. Hoàn một phần/toàn bộ giữ số bán ở ngày giao và tiền thu gốc ở ngày thanh toán. Chỉ số tiền thu dựa vào mốc thanh toán của đơn, chưa phải đối soát ngân hàng hay sổ ca. Truy vấn bán và thu độc lập để tránh nhân bản tiền thu khi đơn có nhiều dòng hoặc nhiều lần hoàn. Dùng decimal/VND và số chụp đã lưu. Bình không gọi lại `OrderPricingHelper` hay áp dụng giá, thuế, voucher hiện tại.

### D3. Phân bổ 0 và dữ liệu thiếu

Dùng đúng các giá trị dòng đã lưu, kể cả khi tất cả phân bổ bằng 0. Trường hợp 0 hợp lệ vẫn đối chiếu được với đầu đơn. Đếm một lần mỗi đơn giao trong `allocationMismatchOrderCount` nếu tổng thuế dòng khác `Order.TaxAmount` hoặc tổng tiền hàng chưa thuế dòng khác `Order.TotalAmount − Order.TaxAmount`. Kiểm tra trên từng đơn trước khi cộng theo ngày/kỳ để các chênh lệch không triệt tiêu nhau. Số theo dòng có thể lệch với đầu đơn cũ. Việc lấy đầu đơn thay dòng hoặc tái phân bổ sẽ làm mất cơ sở đối soát theo sản phẩm.

Khi PostgreSQL tổng hợp giá vốn, kiểm tra riêng số dòng `UnitCost IS NULL` vì `SUM` bỏ qua null. Ngày không có bán trả 0 cho các số phần bán, còn ngày không có thanh toán trả tiền thu 0. Tổng kỳ đối chiếu từng trường cộng được với tổng ngày. Với giá vốn và lãi gộp phần bán, nếu bất kỳ ngày nào mang `null` thì tổng kỳ tương ứng cũng là `null`. Quy tắc tương tự áp dụng theo thu ngân.

### D4. Sản phẩm và thu ngân

- Gom tất cả biến thể theo `ProductId`. Lấy tên snapshot của dòng có `CompletedAt` mới nhất, hòa thì dùng `OrderItem.Id` giảm dần. Hai bảng cùng tiêu chí dòng hợp lệ với tổng kỳ. Trả tối đa 10 dòng/bảng, theo thứ tự `quantity DESC, salesExcludingTax DESC, ProductId ASC` hoặc `salesExcludingTax DESC, quantity DESC, ProductId ASC`. Áp dụng thứ tự UUID của PostgreSQL. Xếp hạng trên số bán gốc, giữ dòng của đơn đã hoàn.
- Gom toàn bộ đơn giao đủ điều kiện theo `CashierUserId`, kể cả tài khoản đã vô hiệu hóa hoặc trùng tên. Tên hiển thị lấy từ `User.FullName` trong snapshot và dùng ID nếu tên trống. Trả số đơn giao, số lượng, doanh thu chưa thuế, giá vốn và lãi gộp phần bán. Sắp doanh thu giảm dần, số đơn giao giảm dần rồi ID tăng dần. Mảng rỗng khi không có bán. Không đưa số theo `PaidAt` vào bảng hiệu suất giao hàng.

### D5. DTO và JSON cho T. Vinh

DTO đặt trong file mới `shared/DTOs/FinancialReportDto.cs` theo quy ước chung (như `AdminDashboardDto`), để API, desktop ở `m4-financial-report-desktop` và test dùng cùng một contract. Tên JSON camelCase. GUID là chuỗi, `DateOnly` là `yyyy-MM-dd`, `asOf` là UTC có hậu tố `Z`, số tiền là JSON number theo VND. Các trường nullable phải xuất hiện cả khi giá trị `null`.

| DTO | Shape |
| --- | --- |
| `FinancialReportDto` | `period: string`, `startDate/endDate: DateOnly`, `timeZone: string`, `asOf: DateTime`, `summary: FinancialReportTotalsDto`, `daily: FinancialReportDayDto[]`, `topProductsByQuantity/topProductsByRevenue: FinancialReportProductDto[]`, `cashierPerformance: FinancialReportCashierDto[]` |
| `FinancialReportTotalsDto` | `completedOrderCount/quantity/allocationMismatchOrderCount: long`, `salesExcludingTax/salesTax/paymentReceivedAmount: decimal`, `costOfGoodsSold/salesGrossProfit: decimal?` và bảy trường change 2 kiểu `decimal?` bên dưới |
| `FinancialReportDayDto` | `date: DateOnly`, `totals: FinancialReportTotalsDto` |
| `FinancialReportProductDto` | `productId: Guid`, `productName: string`, `quantity: long`, `salesExcludingTax: decimal` |
| `FinancialReportCashierDto` | `cashierUserId: Guid`, `cashierName: string`, `completedOrderCount/quantity: long`, `salesExcludingTax: decimal`, `costOfGoodsSold/salesGrossProfit: decimal?` |

Cả `summary` và `daily[].totals` có sẵn bảy trường luôn `null` ở change này.

| Trường change 2 | Ý nghĩa dự kiến |
| --- | --- |
| `refundPaidAmount` | Tiền thực chi trả lại khách theo ngày `RefundedAt` |
| `refundedSalesExcludingTax` | Tiền hàng chưa thuế hoàn theo ngày `RefundedAt` |
| `restockedCost` | Giá vốn gốc được ghi giảm khi thực nhập lại, theo ngày nhập lại |
| `warrantyCost` | Chi phí bảo hành trong kỳ, trình bày riêng |
| `netSalesExcludingTax` | Tiền hàng bán chưa thuế trừ tiền hàng chưa thuế hoàn |
| `netCostOfGoodsSold` | Giá vốn bán trừ giá vốn thực nhập lại |
| `netGrossProfit` | Doanh thu thuần chưa thuế trừ giá vốn thuần |

Mẫu 200 cho fixture ở D7. `salesGrossProfit` chỉ phản ánh bán trước các điều chỉnh của change 2.

```json
{
  "period": "custom",
  "startDate": "2024-10-12",
  "endDate": "2024-10-14",
  "timeZone": "UTC+07:00",
  "asOf": "2024-10-15T02:00:00Z",
  "summary": {
    "completedOrderCount": 3, "quantity": 5, "allocationMismatchOrderCount": 1,
    "salesExcludingTax": 480, "salesTax": 36, "paymentReceivedAmount": 370,
    "costOfGoodsSold": null, "salesGrossProfit": null,
    "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
    "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
  },
  "daily": [
    {
      "date": "2024-10-12",
      "totals": {
        "completedOrderCount": 1, "quantity": 2, "allocationMismatchOrderCount": 0,
        "salesExcludingTax": 180, "salesTax": 18, "paymentReceivedAmount": 248,
        "costOfGoodsSold": 120, "salesGrossProfit": 60,
        "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
        "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
      }
    },
    {
      "date": "2024-10-13",
      "totals": {
        "completedOrderCount": 0, "quantity": 0, "allocationMismatchOrderCount": 0,
        "salesExcludingTax": 0, "salesTax": 0, "paymentReceivedAmount": 0,
        "costOfGoodsSold": 0, "salesGrossProfit": 0,
        "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
        "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
      }
    },
    {
      "date": "2024-10-14",
      "totals": {
        "completedOrderCount": 2, "quantity": 3, "allocationMismatchOrderCount": 1,
        "salesExcludingTax": 300, "salesTax": 18, "paymentReceivedAmount": 122,
        "costOfGoodsSold": null, "salesGrossProfit": null,
        "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
        "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
      }
    }
  ],
  "topProductsByQuantity": [
    { "productId": "00000000-0000-0000-0000-000000000001", "productName": "Mẫu A", "quantity": 2, "salesExcludingTax": 180 },
    { "productId": "00000000-0000-0000-0000-000000000002", "productName": "Mẫu B", "quantity": 2, "salesExcludingTax": 180 },
    { "productId": "00000000-0000-0000-0000-000000000003", "productName": "Mẫu C", "quantity": 1, "salesExcludingTax": 120 }
  ],
  "topProductsByRevenue": [
    { "productId": "00000000-0000-0000-0000-000000000001", "productName": "Mẫu A", "quantity": 2, "salesExcludingTax": 180 },
    { "productId": "00000000-0000-0000-0000-000000000002", "productName": "Mẫu B", "quantity": 2, "salesExcludingTax": 180 },
    { "productId": "00000000-0000-0000-0000-000000000003", "productName": "Mẫu C", "quantity": 1, "salesExcludingTax": 120 }
  ],
  "cashierPerformance": [
    { "cashierUserId": "10000000-0000-0000-0000-000000000001", "cashierName": "Thu ngân 1", "completedOrderCount": 2, "quantity": 3, "salesExcludingTax": 300, "costOfGoodsSold": 120, "salesGrossProfit": 180 },
    { "cashierUserId": "10000000-0000-0000-0000-000000000002", "cashierName": "Thu ngân 2", "completedOrderCount": 1, "quantity": 2, "salesExcludingTax": 180, "costOfGoodsSold": null, "salesGrossProfit": null }
  ]
}
```

### D6. Tích hợp trong Analytics

Thêm action, query/result và read models riêng cho báo cáo trong Analytics. Dùng đăng ký DI hiện có. Các cổng `IAnalyticsService` và `IAnalyticsRepository` cung cấp phương thức báo cáo riêng, được triển khai trong hai lớp production hiện có. Thêm phương thức thường vào interface, không dùng default interface method. Mọi lớp hiện thực khác đều là test double của Bình (`AnalyticsControllerTests`, `AnalyticsServiceTests`, `DataSeederPostgresIntegrationTests`, `DashboardLiveFlowPostgresHttpTests`), nên cập nhật trực tiếp các lớp đó; lớp bọc chỉ cần chuyển tiếp lời gọi. Kiểm chứng HTTP 200 qua DI thật để route dùng triển khai production.

Service quản lý transaction `RepeatableRead` qua `IUnitOfWork`, lấy giờ DB trước mọi đọc tổng hợp, tạo criteria UTC đã kiểm tra, lấp ngày trống và map DTO. Repository truy vấn không tracking và gom trong PostgreSQL. Giữ điều kiện timestamp trên cột gốc và chỉ đổi múi giờ ở biểu thức nhóm. Khi LINQ không dịch được nhóm ngày, dùng SQL tham số hóa theo mẫu Dashboard. Tổng hợp hai tập bán/thu tách biệt, dùng chung snapshot, truyền cancellation token, rollback khi lỗi hoặc hủy. Giữ số dòng trả về gọn bằng nhóm tại DB và giới hạn riêng hai bảng xếp hạng. Không đọc toàn bộ entities vào bộ nhớ.

### D7. Fixture PostgreSQL và Expected tính tay

Kỳ 12–14/10/2024 theo UTC+07, tức `[2024-10-11T17:00:00Z, 2024-10-14T17:00:00Z)`. Các mốc dưới đây đều UTC. Các đơn A, B, C có ID sản phẩm theo đúng thứ tự A < B < C. Chèn thêm các đơn nằm ngay trước biên đầu và đúng biên cuối, với cả ngày thanh toán/giao bên ngoài để xác nhận bị loại.

| Đơn | Status | CompletedAt | PaidAt | Thu ngân | Qty | TotalPrice | Phân bổ giảm/thuế dòng | UnitCost | Đầu đơn TotalAmount / TaxAmount |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| A | Refunded | 11/10 17:00 | 11/10 16:00 | 1 | 2 | 200 | 20 / 18 | 60 | 198 / 18 |
| B | PartiallyRefunded | 13/10 18:00 | 12/10 01:00 | 2 | 2 | 200 | 20 / 18 | null | 198 / 18 |
| C | Completed | 14/10 16:59:59 | 14/10 16:00 | 1 | 1 | 120 | 0 / 0 | 0 | 122 / 12 |
| D | Processing | null | 12/10 02:00 | 2 | 1 | 50 | 0 / 0 | 0 | 50 / 0 |

| Expected theo ngày Việt Nam | Đơn giao | Qty | Tiền hàng | Thuế | Tiền thu | Giá vốn | Lãi gộp phần bán | Đơn lệch phân bổ |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 12/10 | 1 | 2 | 180 | 18 | 248 | 120 | 60 | 0 |
| 13/10 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 14/10 | 2 | 3 | 300 | 18 | 122 | null | null | 1 |
| Cả kỳ | 3 | 5 | 480 | 36 | 370 | null | null | 1 |

Bổ sung fixture riêng chỉ có đơn C để kiểm tra giá vốn 0 đã biết, và ca nhiều dòng/biến thể để kiểm tra đếm đơn, gom sản phẩm, tiền thu không nhân bản, hai thứ tự xếp hạng khác nhau và giới hạn top 10. Kiểm tra 1 ngày, đúng 31 ngày, các lỗi kỳ và mốc `asOf` ngày hiện tại. So khớp HTTP thực tế với Expected trên PostgreSQL, chạy lại test Dashboard hiện có và lưu kết quả vào tài liệu bằng chứng duy nhất.

## Risks / Trade-offs

- Đơn cũ có thể lệch số thuế/tiền hàng ở đầu đơn → xuất bộ đếm lệch để người đọc biết giới hạn số liệu theo dòng.
- Giá vốn thiếu làm mất khả năng tính lãi ở nhóm → trả `null` cho đúng nhóm, giữ các số bán độc lập và các nhóm đủ dữ liệu.
- Dữ liệu bán và tiền thu thay đổi đồng thời → dùng cùng snapshot. Khoảng 31 ngày và top 10 giới hạn kỳ đọc và hai danh sách xếp hạng.

## Migration Plan

Apply bắt đầu bằng fetch và merge `origin/dev` vào `feat/m4-financial-reports`. Ghi SHA của cây mã đem build/test cùng trạng thái worktree nếu build trước commit. Bàn giao endpoint và quyền cùng bản build được kiểm chứng. Change không cần migration hoặc backfill. Khi cần rollback, hoàn nguyên thay đổi API, quyền và tài liệu đi kèm.
