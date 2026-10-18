# Spec Delta

## Purpose

Cung cấp dashboard analytics có contract ổn định cho Admin, phản ánh doanh thu, đơn hàng, danh mục sản phẩm và tồn kho theo kỳ Việt Nam với metadata thời điểm đọc để có thể đối soát.

## ADDED Requirements

### Requirement: Admin-only dashboard analytics endpoint
API SHALL cung cấp `GET /api/analytics/dashboard?period={period}` với `period` là `today`, `last7Days` hoặc `thisMonth`, mặc định `last7Days`. Chỉ Admin được truy cập; endpoint SHALL yêu cầu JWT hợp lệ.

#### Scenario: Admin reads dashboard
- **WHEN** Admin gửi request với một period hợp lệ
- **THEN** API trả HTTP 200 cùng một dashboard response có summary, daily revenue, recent orders, low-stock balances và snapshot metadata

#### Scenario: Authentication and role enforcement
- **WHEN** request thiếu/không hợp lệ JWT hoặc người dùng có role khác Admin gọi endpoint
- **THEN** API trả HTTP 401 hoặc HTTP 403 tương ứng và không trả dữ liệu analytics

#### Scenario: Invalid period
- **WHEN** request gửi một period không được hỗ trợ
- **THEN** API trả HTTP 400 với lỗi validation có thể nhận diện, không âm thầm chọn kỳ mặc định

### Requirement: Vietnam period boundaries and snapshot metadata
Mỗi response SHALL nêu period, `startDate`, `endDate`, `timeZone` là UTC+07:00 và `asOf` UTC. Các ngày là ngày lịch Việt Nam; truy vấn dùng khoảng `[fromUtc, asOfUtc)` trong đó fromUtc là 00:00 UTC+07 của `startDate`, `endDate` là ngày Việt Nam tại `asOf`. `today` là hôm nay; `last7Days` gồm hôm nay và sáu ngày trước; `thisMonth` bắt đầu ngày 1 tháng hiện tại. Snapshot SHALL nhất quán giữa các phần response.

#### Scenario: Period boundaries at UTC rollover
- **WHEN** một request được đọc quanh nửa đêm UTC hoặc UTC+07
- **THEN** start/end date và các sự kiện được tính theo ngày UTC+07, với biên đầu bao gồm và `asOf` cuối loại trừ

#### Scenario: Partial current day
- **WHEN** period hiện tại kết thúc trước nửa đêm UTC+07
- **THEN** các tổng chỉ bao gồm dữ liệu đến `asOf`, nhưng daily series vẫn có điểm zero cho mọi ngày lịch trong kỳ tới `endDate`

#### Scenario: Coherent snapshot under concurrent writes
- **WHEN** order, product hoặc stock thay đổi trong lúc dashboard đang được đọc
- **THEN** mọi trường trong response biểu diễn cùng một database snapshot và `asOf` chỉ thời điểm snapshot đó

### Requirement: KPI definitions and unavailable comparisons
Revenue SHALL là tổng `TotalAmount` của order có `Status=Completed` và `CompletedAt` trong kỳ; đây là gross completed revenue trước hoàn tiền. Order count SHALL đếm mọi order theo `CreatedAt` trong kỳ, không phụ thuộc status. Active product count SHALL đếm product có `IsActive=true` tại snapshot. Revenue comparison và order-count comparison SHALL là null khi chưa có comparison contract.

#### Scenario: Completed time differs from order creation
- **WHEN** order được tạo ở kỳ trước nhưng hoàn tất trong kỳ hiện tại
- **THEN** order không tăng order count kỳ hiện tại nhưng `TotalAmount` được tính vào revenue kỳ hiện tại

#### Scenario: Refunded order is excluded from gross completed revenue
- **WHEN** order ở trạng thái `PartiallyRefunded` hoặc `Refunded`
- **THEN** order không được cộng vào revenue Completed của kỳ

#### Scenario: Comparison is unavailable
- **WHEN** caller đọc bất kỳ period nào trong phiên bản đầu
- **THEN** comparison fields được serialize thành null, không trả 0%, NaN, infinity hoặc tỷ lệ suy đoán

### Requirement: Daily revenue reconciles to period revenue
Response SHALL chứa đúng một điểm daily revenue cho mỗi ngày từ `startDate` tới `endDate`, kể cả ngày không có Completed order; doanh thu theo ngày và tổng được tính theo cùng tiêu chí Completed/CompletedAt. Tổng các điểm SHALL bằng summary revenue.

#### Scenario: Zero-sale days and reconciliation
- **WHEN** trong kỳ có ngày không có order Completed và có ngày có doanh thu
- **THEN** ngày không bán có revenue 0, các ngày được sắp tăng dần và tổng daily revenue bằng chính xác summary revenue

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
