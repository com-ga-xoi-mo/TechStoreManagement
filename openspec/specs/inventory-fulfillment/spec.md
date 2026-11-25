# inventory-fulfillment Specification

## Purpose

Gives the Orders module a single, Inventory-owned way to check, reserve and sell devices and stock so that serial status, stock balances and the movement ledger stay consistent without Orders writing Inventory tables.

## Requirements

### Requirement: Staging-only operations
Every fulfilment operation that changes data SHALL only stage changes on the shared unit of work; it SHALL NOT save, begin, commit or roll back a transaction. The caller owns the transaction and SHALL open it before calling an operation that locks rows. Operations SHALL work on devices and stock rows locked with `SELECT ... FOR UPDATE`.

#### Scenario: Nothing persisted without the caller's save
- **WHEN** reserve or confirm-sale is called and the caller never saves
- **THEN** the database is unchanged

#### Scenario: Caller rollback undoes everything
- **WHEN** the caller rolls back after a successful reserve
- **THEN** no device status or stock quantity has changed

### Requirement: Read-only availability check
The service SHALL provide an availability check for a list of requested lines (product, optional variant, optional device, quantity) that reads without locking and returns one problem per failing line, keyed by line index, without changing any data. A serial-tracked line is available when its device exists, is `InStock`, has the same product and a variant equal to the line's (null equals null only), and the product / variant has a stock row with at least one available unit. A non-tracked line is available when its stock row exists and `quantity − reserved_quantity` covers the requested quantity.

#### Scenario: Available device
- **WHEN** a line references an `InStock` device of the same product and variant
- **THEN** the check reports no problem for that line

#### Scenario: Several failing lines
- **WHEN** two lines fail for different reasons
- **THEN** the check returns both problems in one call

### Requirement: Reserve devices and stock
Reserving lines SHALL lock the devices in ascending id order and then the stock rows in ascending product / variant order, verify every device is `InStock` with a matching product and variant and every stock row has available quantity, then stage: each device `InStock → Reserved` and each stock row's `reserved_quantity` increased by the total quantity reserved for it. It SHALL NOT change `quantity` and SHALL NOT write an inventory movement. If any verification fails it SHALL stage nothing and return a conflict describing the failing line.

#### Scenario: Reserve a device
- **WHEN** an `InStock` device is reserved
- **THEN** its status becomes `Reserved` and its stock row's `reserved_quantity` increases by 1 while `quantity` is unchanged

#### Scenario: Device already reserved or sold
- **WHEN** a requested device is `Reserved` or `Sold`
- **THEN** the operation reports a conflict and no change is staged for any line

#### Scenario: Insufficient available quantity
- **WHEN** a non-tracked line asks for more than `quantity − reserved_quantity`
- **THEN** the operation reports a conflict and no change is staged

#### Scenario: Several devices sharing one stock row
- **WHEN** three `InStock` devices of one product and variant are reserved together
- **THEN** the shared stock row's `reserved_quantity` increases by 3 and the available-quantity check uses the total, not each device separately

### Requirement: Confirm a sale
Confirming a sale SHALL require each device to be `Reserved` (and each non-tracked quantity to be covered by `reserved_quantity`) and SHALL stage: each device `Reserved → Sold` with `warranty_start_at` = the completion time and `warranty_end_at` = start plus `warranty_months`; each stock row's `quantity` and `reserved_quantity` both reduced by the line quantity; and one `InventoryMovement` per order line of type `Sale` with `quantity_change` = −quantity, `quantity_after` = the stock quantity after that line, the order item id and the acting user. It SHALL return each serial-tracked line's device `unit_cost` so the caller can snapshot costs. Lines SHALL be processed in a stable order so `quantity_after` values are monotonic per stock row.

#### Scenario: Sale of a reserved phone
- **WHEN** a `Reserved` device with `warranty_months` = 12 is sold at 2026-10-11T08:00:00Z
- **THEN** it becomes `Sold` with warranty from that instant to 2027-10-11T08:00:00Z, the stock `quantity` and `reserved_quantity` decrease by 1, and a `Sale` movement of −1 referencing the order line is staged

#### Scenario: Device not reserved
- **WHEN** confirm-sale is called for a device that is `InStock`
- **THEN** the operation reports a conflict and stages nothing

#### Scenario: Ledger consistency
- **WHEN** two lines of one order sell devices from the same stock row with quantity 10
- **THEN** their movements record `quantity_after` 9 and 8

### Requirement: Serial-tracked stock invariant preserved
After reserve and after confirm-sale, for a serial-tracked product the stock row's `quantity` SHALL equal the number of its devices that are `InStock` or `Reserved`, and `reserved_quantity` SHALL equal the number that are `Reserved`.

#### Scenario: Counts match after a sale
- **WHEN** one of 5 `InStock` devices is reserved and then sold
- **THEN** `quantity` is 4, `reserved_quantity` is 0 and 4 devices are `InStock`

### Requirement: Giải phóng giữ chỗ đúng chủ thể

Inventory SHALL giải phóng đúng phần hàng đang giữ cho giao dịch được chỉ định trong UnitOfWork của bên gọi. Khóa IMEI rồi khóa tồn theo thứ tự hiện hành, xử lý lặp theo định danh nghiệp vụ.

#### Scenario: Hủy giữ chỗ hai lần
- **WHEN** Đạt gọi giải phóng lại cùng một lượt giữ QR hoặc An thử lại hoàn đơn chưa giao
- **THEN** Reserved trở về InStock và reserved_quantity giảm đúng một lần, quantity giữ nguyên, không phát sinh movement nhập hàng hay giải phóng phần của đơn khác.

### Requirement: Nhận và kiểm định từng phần hàng trả

Inventory SHALL nhận đúng IMEI đã bán vào Returned. Hàng đã nhận chờ kiểm định chưa khả dụng. Khi Warehouse phân loại, chỉ phần bán lại được mới cộng tồn, ghi CustomerReturn, thời điểm thực nhập và IsRestocked cho phần tương ứng trong một giao dịch.

#### Scenario: Lô trả có nhiều kết quả
- **WHEN** kho nhận ba đơn vị rồi xác nhận một đơn vị bán lại được, một lỗi và một cần sửa
- **THEN** tồn tăng một, chỉ phần được nhập có movement và đánh dấu nhập lại, hai phần còn lại ở trạng thái Defective hoặc UnderRepair. Gửi lại cùng phiếu kiểm định không tăng tồn.

### Requirement: Thao tác thiết bị cho bảo hành

Inventory SHALL nhận/trả máy sửa và xuất máy thay theo quyết định của luồng bảo hành trong transaction của Q. Vinh. Số tồn vật lý cho hàng serial bằng số IMEI InStock + Reserved sau mỗi lần commit.

#### Scenario: Nhận máy sửa rồi trả khách
- **WHEN** máy Sold được nhận sửa và trả đúng khách
- **THEN** IMEI đi qua UnderRepair rồi về Sold, giữ hạn bảo hành cũ, không tăng tồn hay ghi bán/hoàn.

#### Scenario: Xuất máy thay
- **WHEN** hồ sơ được duyệt đổi và máy thay InStock khớp sản phẩm/biến thể
- **THEN** máy cũ thành Defective, máy thay thành Sold và nhận `warranty_months`, `warranty_start_at`, `warranty_end_at` của máy cũ (hạn bảo hành còn lại, không kéo dài), tồn giảm một, trả mã biến động và giá vốn máy thay cho hồ sơ liên kết hai IMEI với đơn gốc.

#### Scenario: Hai luồng tranh cùng thiết bị
- **WHEN** yêu cầu trả và yêu cầu bảo hành cùng nhận một IMEI, hoặc hai hồ sơ cùng chọn một máy thay
- **THEN** chỉ một giao dịch nhận được thiết bị, giao dịch xung đột không để lại thay đổi kho.

#### Scenario: Quyền nhận bảo hành theo dòng bán
- **WHEN** nhân viên được phép tiếp nhận bảo hành (Admin, Manager hoặc SalesStaff) mở hồ sơ nhận một IMEI trong thời hạn bảo hành
- **THEN** hệ thống kiểm tra khách, dòng đã giao và thiết bị khách còn giữ. Hệ thống từ chối nhận lại thiết bị mà khách đã trả, kể cả khi đó là dòng trên chứng từ hoàn cũ.

#### Scenario: Tra cứu và thử lại quyết định bảo hành
- **WHEN** người dùng gửi lại quyết định đã thực hiện với cùng mã hồ sơ và máy thay
- **THEN** nhận lại hồ sơ đã ghi, gồm hạn bảo hành gốc và mã biến động kho. Một quyết định khác trên cùng hồ sơ đã đóng trả 409.
