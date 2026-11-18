# Spec Delta

## RENAMED Requirements

- FROM: `### Requirement: Admin-only dashboard analytics endpoint`
- TO: `### Requirement: Authorized dashboard analytics endpoint`

## MODIFIED Requirements

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

## ADDED Requirements

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
