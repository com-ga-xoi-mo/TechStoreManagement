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
