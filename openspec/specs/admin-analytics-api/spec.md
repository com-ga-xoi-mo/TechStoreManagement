# admin-analytics-api Specification

## Purpose

Cung cấp dashboard analytics có contract ổn định cho Admin, phản ánh doanh thu, đơn hàng, danh mục sản phẩm và tồn kho theo kỳ Việt Nam với metadata thời điểm đọc để có thể đối soát.

## Requirements

### Requirement: Authorized dashboard analytics endpoint

API SHALL cung cấp `GET /api/analytics/dashboard` với các kỳ `today`, `last7Days`, `thisMonth` và `custom`. Khi bỏ trống kỳ và không có tham số ngày, mặc định là `last7Days`. Endpoint SHALL yêu cầu JWT hợp lệ và cấp cùng phạm vi đọc toàn cửa hàng cho Admin và Manager.

#### Scenario: Admin reads dashboard

- **WHEN** Admin gửi request với kỳ hợp lệ
- **THEN** API trả HTTP 200 với summary, daily revenue, recent orders, low-stock balances và snapshot metadata

#### Scenario: Manager reads dashboard

- **WHEN** Manager gửi request với kỳ hợp lệ
- **THEN** API trả HTTP 200 cùng phạm vi dữ liệu toàn cửa hàng và contract response như Admin

#### Scenario: Authentication and role enforcement

- **WHEN** request thiếu hoặc có JWT không hợp lệ, hoặc SalesStaff hay WarehouseStaff có JWT hợp lệ gọi endpoint
- **THEN** API lần lượt trả HTTP 401 cho lỗi xác thực hoặc HTTP 403 cho role bị cấm và không trả dữ liệu analytics

#### Scenario: Invalid period

- **WHEN** người có quyền gửi period không được hỗ trợ
- **THEN** API trả HTTP 400 với lỗi validation theo trường `period`, không tự chọn kỳ mặc định

### Requirement: Vietnam period boundaries and snapshot metadata

Mỗi response SHALL nêu `period`, `startDate`, `endDate`, `timeZone` là UTC+07:00 và `asOf` UTC tại thời điểm đọc. Ngày báo cáo là ngày Việt Nam. Truy vấn theo kỳ dùng khoảng `[fromUtc, endExclusiveUtc)`, với đầu kỳ là 00:00 UTC+07 của `startDate` và cuối kỳ là mốc sớm hơn giữa 00:00 UTC+07 của ngày sau `endDate` và `asOf`. Các phần của response SHALL dùng cùng database snapshot. Sản phẩm hoạt động và tồn thấp SHALL phản ánh thời điểm đọc.

#### Scenario: Period boundaries at UTC rollover

- **WHEN** một request được đọc quanh nửa đêm UTC hoặc UTC+07
- **THEN** start/end date và các sự kiện được tính theo ngày UTC+07, với biên đầu bao gồm và biên cuối loại trừ

#### Scenario: Partial current day

- **WHEN** một kỳ kết thúc vào ngày Việt Nam hiện tại và được đọc trước nửa đêm
- **THEN** các tổng theo kỳ chỉ bao gồm dữ liệu trước `asOf` và daily series có một điểm cho mỗi ngày tới `endDate`, gồm cả ngày doanh thu bằng 0

#### Scenario: Coherent snapshot under concurrent writes

- **WHEN** order, product hoặc stock thay đổi trong lúc dashboard đang được đọc
- **THEN** mọi trường trong response biểu diễn cùng một database snapshot và `asOf` là thời điểm đọc snapshot đó

#### Scenario: Existing relative periods still include the current day

- **WHEN** caller chọn `today`, `last7Days` hoặc `thisMonth`
- **THEN** `endDate` là ngày Việt Nam tại `asOf`, còn `startDate` lần lượt là cùng ngày, sáu ngày trước hoặc ngày đầu tháng hiện tại

#### Scenario: Historical range keeps its report dates

- **WHEN** caller chọn một kỳ tùy chọn kết thúc trước ngày Việt Nam hiện tại
- **THEN** response có `period=custom`, ngày đầu và cuối đúng với `from` và `to`, và `asOf` vẫn là thời điểm đọc hiện tại. Truy vấn doanh thu và đơn hàng bao gồm ngày `to` đến trước nửa đêm Việt Nam kế tiếp và bỏ qua các đơn ngoài kỳ theo tiêu chí của từng KPI

### Requirement: KPI definitions and unavailable comparisons

Revenue SHALL cộng `TotalAmount` gốc (gồm thuế) cho đơn đã giao có `CompletedAt` trong kỳ và ở trạng thái `Completed`, `PartiallyRefunded` hoặc `Refunded`. Đơn hoàn khi còn Processing có `CompletedAt` rỗng và không có doanh thu giao hàng. Order count SHALL đếm mọi order theo `CreatedAt` trong kỳ, không phụ thuộc status. Active product count SHALL đếm product có `IsActive=true` tại snapshot. Revenue comparison và order-count comparison SHALL là null khi chưa có comparison contract.

#### Scenario: Completed time differs from order creation

- **WHEN** order được tạo ở kỳ trước nhưng hoàn tất trong kỳ hiện tại
- **THEN** order không tăng order count kỳ hiện tại nhưng `TotalAmount` được tính vào revenue kỳ hiện tại

#### Scenario: Đã giao rồi hoàn tiền

- **WHEN** đơn đã giao với `TotalAmount` 1.980.000đ được hoàn một phần rồi hoàn hết
- **THEN** dashboard vẫn ghi đủ 1.980.000đ doanh thu gốc vào ngày giao. Ngày chi hoàn không phát sinh thêm doanh thu gốc.

#### Scenario: Refunded order is excluded from gross completed revenue

- **WHEN** đơn Processing đã thanh toán được hoàn hết trước khi giao
- **THEN** dashboard đếm đơn vào kỳ tạo và không ghi doanh thu gốc của đơn vào bất kỳ ngày nào

#### Scenario: Comparison is unavailable

- **WHEN** caller đọc bất kỳ period nào trong phiên bản đầu
- **THEN** comparison fields được serialize thành null, không trả 0%, NaN, infinity hoặc tỷ lệ suy đoán

### Requirement: Daily revenue reconciles to period revenue

Response SHALL chứa đúng một điểm daily revenue cho mỗi ngày từ `startDate` tới `endDate`. Mỗi điểm SHALL cộng doanh thu gốc của các đơn đã giao theo ngày Việt Nam của `CompletedAt`, kể cả khi sau đó đơn được hoàn một phần hoặc toàn bộ. Tổng các điểm SHALL bằng summary revenue.

#### Scenario: Zero-sale days and reconciliation

- **WHEN** trong kỳ có ngày giao hàng và ngày chỉ chi hoàn cho giao dịch trước đó
- **THEN** ngày chỉ chi hoàn có revenue 0, các ngày được sắp tăng dần và tổng daily revenue bằng chính xác summary revenue

### Requirement: Low-stock balances and available quantity
Low stock SHALL được tính theo từng inventory balance row có `Quantity <= MinStockAlert`, không đổi điều kiện theo ReservedQuantity. Summary count SHALL là tổng số balance rows thỏa điều kiện; response trả tối đa 5 dòng theo Quantity tăng dần rồi tên sản phẩm/variant và id ổn định, gồm `Quantity`, `ReservedQuantity`, `AvailableQuantity = Quantity - ReservedQuantity` và `MinStockAlert` riêng biệt.

#### Scenario: Reserved stock does not change low-stock threshold
- **WHEN** balance có `Quantity` lớn hơn `MinStockAlert` nhưng `AvailableQuantity` nhỏ hơn ngưỡng do có reservation
- **THEN** balance không bị tính low stock chỉ vì available quantity thấp

#### Scenario: Low stock list is partial but count is total
- **WHEN** có hơn năm balance rows thỏa điều kiện
- **THEN** summary count vẫn đếm toàn bộ rows, danh sách chứa tối đa năm dòng có thứ tự xác định và mỗi dòng thể hiện riêng quantity, reserved, available và threshold

### Requirement: Recent orders are bounded and deterministic
Response SHALL trả tối đa năm order có `CreatedAt` trong kỳ, thuộc mọi trạng thái, sắp `CreatedAt` giảm dần rồi id giảm dần. Mỗi dòng SHALL giữ order code, customer name hoặc nhãn khách lẻ, summary mặt hàng từ order item snapshot, total, status và `CreatedAt` UTC; không trả demo identifier hoặc thay đổi trạng thái.

#### Scenario: Recent order limit and tie ordering
- **WHEN** có nhiều hơn năm order trong kỳ hoặc hai order có cùng `CreatedAt`
- **THEN** response chỉ có năm order mới nhất, hòa thời gian được phân định bằng id giảm dần

#### Scenario: Guest and non-completed orders
- **WHEN** một trong năm order mới nhất là khách lẻ hoặc có status chưa hoàn tất
- **THEN** response giữ nhãn khách lẻ và status thật, không loại order hoặc tự gán Completed

### Requirement: Validated custom dashboard period

Với `period=custom`, API SHALL yêu cầu đúng một giá trị cho mỗi tham số `from` và `to` theo lịch Việt Nam, định dạng `yyyy-MM-dd`. Khoảng ngày tính cả hai đầu SHALL dài từ 1 đến 31 ngày và kết thúc muộn nhất vào ngày Việt Nam tại `asOf`. Ngày phải chuyển được thành biên UTC hợp lệ. Các kỳ khác chỉ nhận tham số `period`.

#### Scenario: One day and maximum length are allowed

- **WHEN** người có quyền chọn cùng một ngày cho `from` và `to`, hoặc nhập khoảng đúng 31 ngày, với các biên hợp lệ và ngày cuối không ở tương lai
- **THEN** API trả HTTP 200 và một điểm daily revenue cho mỗi ngày của kỳ

#### Scenario: Missing, malformed or unsupported dates

- **WHEN** người có quyền gửi thiếu ngày, ngày rỗng, ngày lặp tham số, định dạng sai, ngày không tồn tại hay không chuyển được sang biên UTC, `from > to`, kỳ trên 31 ngày, hoặc ngày kết thúc trong tương lai theo UTC+07
- **THEN** API trả HTTP 400 với ValidationProblemDetails theo trường liên quan và không chạy truy vấn tổng hợp

#### Scenario: Date parameters require the custom period

- **WHEN** người có quyền gửi `from` hoặc `to` mà không chọn chính xác `period=custom`
- **THEN** API trả HTTP 400 và chỉ rõ lỗi kết hợp tham số

### Requirement: Financial report uses a validated Vietnam date range
API SHALL cung cấp `GET /api/analytics/financial-report` với đúng một `period=custom`, một `from` và một `to` dạng `yyyy-MM-dd`. Kỳ bao gồm cả hai ngày, dài 1–31 ngày và kết thúc muộn nhất vào ngày UTC+07 tại `asOf`. Response SHALL trả `period`, `startDate`, `endDate`, `timeZone` và `asOf` UTC, với tất cả số liệu từ cùng snapshot.

#### Scenario: UTC+07 boundaries and current-day cutoff
- **WHEN** caller đọc kỳ hợp lệ
- **THEN** biên đầu là nửa đêm UTC+07 của `from`, được tính. Biên cuối là mốc sớm hơn giữa nửa đêm UTC+07 của ngày sau `to` và `asOf`, bị loại trừ
- **AND** daily có đúng một điểm mỗi ngày theo thứ tự tăng dần

#### Scenario: Invalid report period
- **WHEN** caller có quyền gửi tham số bắt buộc bị thiếu, rỗng, lặp, period khác `custom`, ngày sai định dạng hoặc không chuyển được thành biên UTC, kỳ đảo ngược, dài hơn 31 ngày hoặc kết thúc trong tương lai
- **THEN** API trả 400 `ValidationProblemDetails` theo trường liên quan và không tổng hợp báo cáo

### Requirement: Financial report recognizes original delivered sales
Theo `CompletedAt` trong kỳ, báo cáo SHALL tính đơn `Completed`, `PartiallyRefunded` hoặc `Refunded`. Tiền hàng chưa thuế của dòng là `TotalPrice − OrderDiscountAmount`, thuế là `TaxAmount` đã lưu trên dòng. Tổng kỳ và các nhóm theo ngày, sản phẩm, thu ngân SHALL dùng cùng tập dòng bán.

#### Scenario: Original sale survives a refund
- **WHEN** một đơn được giao trong kỳ rồi hoàn một phần hoặc toàn bộ, hoặc đơn đã thu tiền vẫn ở `Processing`
- **THEN** đơn đã giao vẫn đóng góp số bán gốc vào ngày giao, còn đơn `Processing` không đóng góp số bán dù đã có `PaidAt`

#### Scenario: Legacy line allocations remain as stored
- **WHEN** phân bổ lưu trên dòng bằng 0
- **THEN** báo cáo dùng đúng 0. Nếu tổng thuế dòng lệch `Order.TaxAmount` hoặc tổng tiền hàng chưa thuế dòng lệch `Order.TotalAmount − Order.TaxAmount`, đếm đơn đó một lần trong `allocationMismatchOrderCount` của ngày và kỳ
- **AND** không phân bổ lại phần chênh, không tra giá/thuế/ưu đãi hiện tại

#### Scenario: The Dashboard keeps its own contract
- **WHEN** caller dùng `GET /api/analytics/dashboard`
- **THEN** endpoint, các kỳ và DTO Dashboard giữ nguyên, doanh thu Dashboard theo đúng quy tắc hiện hành của requirement KPI trong spec này (cộng `TotalAmount` gốc của đơn đã giao ở trạng thái `Completed`, `PartiallyRefunded` hoặc `Refunded`), và báo cáo tài chính không thay đổi số liệu Dashboard

### Requirement: Financial report separates receipts from sales
`paymentReceivedAmount` SHALL là tổng `Order.TotalAmount` của đơn có `PaidAt` trong kỳ, đếm một lần mỗi đơn, gom ngày UTC+07 của `PaidAt`. Tiền thu gốc SHALL có trường riêng với doanh thu chưa thuế, thuế và hoàn tiền.

#### Scenario: Payment and delivery fall on different days
- **WHEN** đơn được thanh toán trước ngày giao, kể cả đơn còn `Processing`
- **THEN** tiền thu gốc được ghi vào ngày thanh toán và chỉ lần giao đủ điều kiện mới đóng góp doanh thu vào ngày giao

### Requirement: Financial report distinguishes zero and unavailable amounts
Giá vốn bán SHALL là tổng `UnitCost × Quantity`. Trong tổng kỳ, từng ngày và từng thu ngân, chỉ một dòng có `UnitCost=null` cũng làm giá vốn và lãi gộp phần bán của nhóm đó là `null`. Khi biết đủ giá vốn, lãi gộp phần bán SHALL bằng tiền hàng chưa thuế trừ giá vốn bán.

#### Scenario: Empty, zero and missing costs
- **WHEN** kỳ có một ngày trống, một ngày chỉ có giá vốn 0 đã biết và một ngày thiếu giá vốn
- **THEN** ngày trống trả 0 cho các chỉ số phần bán và tiền thu, ngày có giá vốn 0 giữ số 0, ngày thiếu giá vốn và tổng kỳ trả `null` cho chỉ số phụ thuộc
- **AND** mỗi chỉ số cộng được ở ngày khớp tổng kỳ, với cùng quy tắc lan truyền `null`

#### Scenario: Change 2 amounts are present but unavailable
- **WHEN** nguồn cho một chỉ số hậu mãi hoặc dòng tiền chưa khả dụng, hoặc một sự kiện cần tính thiếu giá trị phân bổ, giá vốn hay chứng từ cần thiết
- **THEN** trường đó xuất hiện với JSON `null` trong nhóm bị ảnh hưởng và lan sang tổng kỳ cùng các chỉ số phụ thuộc. Các trường độc lập, như tiền đã chi khi số phân bổ bị thiếu, vẫn trả giá trị đã biết

#### Scenario: Available sources have no matching events
- **WHEN** nguồn đầy đủ cho nhóm nhưng kỳ hoặc ngày không có sự kiện hợp lệ
- **THEN** các trường tương ứng trong `summary` và `daily[].totals` trả số 0. API giữ bảy trường hậu mãi đã công bố trong `summary` và `daily[].totals`, bổ sung `refundTaxAmount`, `cashReceivedAmount`, `cashRefundPaidAmount`, `vietQrReceivedAmount`, `vietQrRefundPaidAmount` và mảng `cashByShift` ở cấp response

### Requirement: Financial report ranks products and groups cashier performance
Báo cáo SHALL trả tối đa 10 sản phẩm cho mỗi bảng xếp hạng, gom mọi biến thể theo `ProductId`. Bảng số lượng sắp theo số lượng giảm dần, rồi doanh thu chưa thuế giảm dần. Bảng doanh thu dùng thứ tự hai chỉ số ngược lại, đều giảm dần. Hòa cả hai chỉ số dùng `ProductId` tăng dần. Hiệu suất SHALL gom theo `CashierUserId` trên đơn bán đủ điều kiện.

#### Scenario: Ties and historical cashier attribution
- **WHEN** hai sản phẩm hòa cả hai chỉ số hoặc nhiều nhân viên có cùng tên hiển thị
- **THEN** hai bảng dùng thứ tự ID ổn định và mỗi ID thu ngân giữ một dòng riêng, có số đơn giao đếm duy nhất theo ID đơn, số lượng, tiền hàng chưa thuế, giá vốn và lãi gộp phần bán

#### Scenario: No delivered sales
- **WHEN** kỳ không có đơn giao đủ điều kiện
- **THEN** hai bảng xếp hạng và danh sách hiệu suất là mảng rỗng

### Requirement: Financial report dates settled refunds independently of delivery
Báo cáo SHALL ghi `refundPaidAmount` và `refundTaxAmount` từ khoản đã chi vào ngày UTC+07 của `RefundedAt`. `refundedSalesExcludingTax` SHALL chỉ cộng tiền hàng hoàn chưa thuế đã lưu của đơn đã giao trước hoặc tại thời điểm hoàn, kể cả ngày giao ở kỳ trước.

#### Scenario: Delivered order is partially and then fully refunded
- **WHEN** đơn đã giao được hoàn một phần rồi hoàn hết bằng hai lần chi đã xác nhận
- **THEN** doanh thu gốc giữ ở ngày giao, từng lần chi và điều chỉnh doanh thu vào ngày hoàn của nó
- **AND** yêu cầu chưa chi, chờ ngân hàng, thất bại và gửi lại cùng lần hoàn không làm tăng số hoàn

#### Scenario: Processing order is fully refunded before delivery
- **WHEN** toàn bộ tiền của đơn `Processing` được hoàn trước khi giao
- **THEN** số tiền và thuế hoàn đã lưu xuất hiện ở ngày chi. Tiền hàng giảm doanh thu và giá vốn nhập lại của đơn này bằng 0, kể cả khi đơn đã chuyển sang `Refunded`

#### Scenario: Inspection splits an already paid return
- **WHEN** khoản đã chi được tách thành nhiều dòng để kiểm định
- **THEN** tổng tiền hàng hoàn, thuế và tiền chi trên ngày hoàn vẫn giữ nguyên

### Requirement: Financial report reverses cost only on sellable restock
`restockedCost` SHALL cộng `QuantityChange × OrderItem.UnitCost` gốc cho từng movement dương loại `CustomerReturn` có liên kết `order_return_id` đến dòng trả của đơn đã giao. Ngày ghi giảm vốn lấy từ thời điểm movement. Mỗi movement được tính một lần.

#### Scenario: One item is sellable and another is defective
- **WHEN** kho nhập lại một phần vào ngày khác ngày hoàn và phần còn lại bị lỗi hoặc đang sửa
- **THEN** chỉ phần thực nhập giảm giá vốn, đúng ngày movement. Cờ `IsRestocked` và sự kiện giữ/nhận/kiểm định hàng riêng lẻ không tạo khoản giảm vốn

#### Scenario: Original cost is missing or genuinely zero
- **WHEN** một movement đủ điều kiện có giá vốn gốc thiếu hoặc được lưu bằng 0
- **THEN** giá trị thiếu làm `restockedCost` và chỉ số phụ thuộc của nhóm là `null`, còn giá trị 0 cho khoản giảm vốn 0

### Requirement: Financial report displays replacement warranty cost separately
`warrantyCost` SHALL cộng giá vốn máy thay đã chụp trên hồ sơ `Replaced` có movement xuất máy bảo hành, theo ngày xuất. Mỗi movement được tính một lần. Chi phí này SHALL xuất riêng khỏi doanh thu, dòng tiền, vốn thuần và lãi gộp.

#### Scenario: Replacement is replayed and repair is handed back
- **WHEN** máy thay được xuất đúng một lần dù yêu cầu được gửi lại, hoặc hồ sơ sửa chỉ trả lại máy khách
- **THEN** lần xuất máy thay góp một khoản giá vốn trong kỳ xuất. Hồ sơ nhận sửa hoặc trả máy cũ không phát sinh chi phí máy thay. Giá vốn máy thay chưa biết trả `null`

### Requirement: Financial report reconciles cash by shift and separates VietQR
Báo cáo SHALL tách Cash đã thu/chi theo ca thực tế và QR đã thu/chi xác nhận. Cash thu thuộc ca bán tại ngày thanh toán. Cash hoàn thuộc ca đã chi tại ngày hoàn. QR SHALL không làm thay đổi tiền két. Số hợp lệ chỉ lấy từ chứng từ thanh toán đã lưu.

#### Scenario: Cash sale and refund use different shifts
- **WHEN** ca bán đã thu Cash và người khác chi hoàn bằng ca của mình
- **THEN** thu vào ca bán, chi vào ca chi; `summary` và `daily[].totals` ghi `cashReceivedAmount` và `cashRefundPaidAmount` theo ngày sự kiện. `cashByShift` trả `posSessionId`, `cashReceivedAmount`, `cashRefundPaidAmount` và `cashNetMovement` cho kỳ, với dòng tiền thuần bằng thu trừ chi khi đủ dữ liệu
- **AND** cùng khoản chi không bị cộng hai lần giữa chứng từ ca và các dòng hoàn

#### Scenario: Confirmed QR flows use bank timestamps
- **WHEN** thu QR đã xác nhận hoặc khoản hoàn QR đã có đối soát chứng từ thành công
- **THEN** `vietQrReceivedAmount` và `vietQrRefundPaidAmount` ở tổng kỳ và từng ngày dùng lần lượt ngày xác nhận thu và ngày thực chi. QR chờ, hết hạn, thất bại hoặc tiền đến muộn chưa đối soát không được ghi nhận

### Requirement: Financial report reconciles net amounts across dates
Báo cáo SHALL tính `netSalesExcludingTax = salesExcludingTax − refundedSalesExcludingTax`, `netCostOfGoodsSold = costOfGoodsSold − restockedCost` và `netGrossProfit = netSalesExcludingTax − netCostOfGoodsSold`. Từng luồng sự kiện sử dụng biên kỳ và snapshot chung của báo cáo.

#### Scenario: Only refund or restock occurs within the current period
- **WHEN** đơn được giao kỳ trước và kỳ hiện tại chỉ có hoàn hoặc thực nhập lại
- **THEN** sự kiện hậu mãi vẫn tính đúng ngày, các số thuần có thể âm và mỗi số cộng được từ các ngày bằng tổng kỳ khi đủ dữ liệu
- **AND** số bán gốc, bảng xếp hạng, hiệu suất thu ngân, Dashboard, kỳ và quyền truy cập giữ nguyên hợp đồng hiện hành
