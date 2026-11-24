## MODIFIED Requirements

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
