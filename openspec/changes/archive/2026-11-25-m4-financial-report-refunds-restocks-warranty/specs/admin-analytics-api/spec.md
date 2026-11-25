# Spec Delta

## ADDED Requirements

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

## MODIFIED Requirements

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
