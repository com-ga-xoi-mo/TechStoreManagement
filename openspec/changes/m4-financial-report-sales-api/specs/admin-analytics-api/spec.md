# Spec Delta

## ADDED Requirements

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

#### Scenario: The M3 Dashboard retains its existing behavior
- **WHEN** caller dùng `GET /api/analytics/dashboard`
- **THEN** endpoint, các kỳ, DTO và KPI M3 giữ nguyên, kể cả tiêu chí doanh thu Dashboard loại đơn hoàn

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
- **WHEN** caller đọc báo cáo Sprint 1
- **THEN** tổng kỳ và mỗi ngày luôn có các trường `refundPaidAmount`, `refundedSalesExcludingTax`, `restockedCost`, `warrantyCost`, `netSalesExcludingTax`, `netCostOfGoodsSold` và `netGrossProfit` với giá trị JSON `null`, kể cả ngày không có dữ liệu

### Requirement: Financial report ranks products and groups cashier performance
Báo cáo SHALL trả tối đa 10 sản phẩm cho mỗi bảng xếp hạng, gom mọi biến thể theo `ProductId`. Bảng số lượng sắp theo số lượng giảm dần, rồi doanh thu chưa thuế giảm dần. Bảng doanh thu dùng thứ tự hai chỉ số ngược lại, đều giảm dần. Hòa cả hai chỉ số dùng `ProductId` tăng dần. Hiệu suất SHALL gom theo `CashierUserId` trên đơn bán đủ điều kiện.

#### Scenario: Ties and historical cashier attribution
- **WHEN** hai sản phẩm hòa cả hai chỉ số hoặc nhiều nhân viên có cùng tên hiển thị
- **THEN** hai bảng dùng thứ tự ID ổn định và mỗi ID thu ngân giữ một dòng riêng, có số đơn giao đếm duy nhất theo ID đơn, số lượng, tiền hàng chưa thuế, giá vốn và lãi gộp phần bán

#### Scenario: No delivered sales
- **WHEN** kỳ không có đơn giao đủ điều kiện
- **THEN** hai bảng xếp hạng và danh sách hiệu suất là mảng rỗng
