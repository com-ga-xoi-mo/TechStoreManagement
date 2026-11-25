# M4 · Hợp đồng thao tác kho

Đạt, An và Q. Vinh dùng `IInventoryAfterSaleService` trong cùng scope DI với `IUnitOfWork`. Bên gọi mở transaction, gọi kho, lưu thay đổi và commit. Khi nhận conflict hoặc exception, bên gọi rollback toàn bộ. Dịch vụ kho chỉ khóa, kiểm tra và stage. Một thao tác dùng một UUID nghiệp vụ lưu cùng hồ sơ để gửi lại khi mất phản hồi. Replay cùng UUID và payload trả kết quả đã ghi, kể cả khi IMEI đã chuyển sang trạng thái kế tiếp. Cùng UUID khác payload trả conflict.

## Đạt · hủy QR và An · hoàn đơn chưa giao

`ReleaseAsync(operationId, orderId, lines, ct)` nhận toàn bộ lượt giữ của đơn. Mỗi `FulfillmentLine` gồm mã dòng đơn, sản phẩm, biến thể nullable, mã thiết bị nullable và lượng. Với máy serial, lượng bằng 1. `inventory_reservations` gắn lượt giữ với dòng đơn thực. Đơn Processing của M3 dùng các dòng bán cố định được ghi cùng giao dịch giữ chỗ. QR Pending chỉ được release khi đã có bản ghi giữ cho từng dòng. `CheckReleaseAsync(orderId, lines, ct)` chỉ kiểm tra và khóa để An ghi ý định chi ngân hàng.

| Lời gọi | Kết quả trong giao dịch |
|---|---|
| Đơn A giữ 2, B giữ 3 cùng SKU. Release A, mã R | Thành công, reserved 5 → 3, lượng vật lý và ledger giữ nguyên. Máy Reserved của A về InStock. |
| Release A bằng mã dòng của B, hoặc release A đã tiêu thụ bằng mã mới | Conflict kèm LineIndex và Message. |
| Gửi lại R, cùng A và cùng dòng | Thành công như lần trước, reserved vẫn là 3. |

## An và kho · nhận hàng đã giao và kiểm định

`CheckReturnAsync(orderId, lines, ct)` dùng lúc giữ quyền xử lý khoản ngân hàng. `ReceiveReturnAsync(refundId, orderId, lines, ct)` nhận hàng khi chốt khoản chi. Dòng IMEI phải thuộc lần bán hiện tại và máy phải là Sold. Nhận hàng chuyển máy về Returned. Hàng theo lượng chờ trong các OrderReturn đã ghi cùng transaction chốt. Tồn khả dụng bắt đầu tăng ở bước kiểm định.

Bên gọi khóa phiếu, tính lượng còn chờ và tách riêng phần OrderReturn có cùng kết quả. `InspectAsync(inspectionId, orderReturnId, line, disposition, actorId, ct)` ghi CustomerReturn và mã movement cho Restock. Defective và UnderRepair giữ nguyên lượng tồn. Cùng transaction, bên gọi ghi IsRestocked và thời điểm kiểm định trên phần tương ứng. Mỗi OrderReturn chỉ được kiểm định một lần. Ba phần lượng 1 xử lý theo ba kết quả sẽ có đúng một movement +1. Gửi lại mã kiểm định trả mã movement cũ, thay lượng với cùng mã trả conflict.

## Q. Vinh · nhận sửa, trả khách, đổi máy

`ApplyWarrantyAsync(operationId, caseId, orderId, orderItemId, deviceId, action, replacementId, actorId, ct)`. Hồ sơ bảo hành xác nhận điều kiện/đồng ý của khách trước khi gọi kho. Mỗi quyết định có một operationId riêng. Kết quả có IsSuccess, Conflict, MovementId và UnitCost. Giá vốn có thể null và chỉ dùng nội bộ.

| Quyết định | Thành công | Conflict | Replay cùng mã |
|---|---|---|---|
| ReceiveForRepair, máy A, phiếu C | Sold → UnderRepair, lưu phiếu C | Máy thuộc lần bán khác, đang xử lý hoàn ngân hàng hoặc đang ở kho | Trả thành công, giữ nguyên trạng thái hiện tại |
| ReturnToCustomer, máy A, phiếu C | UnderRepair → Sold, giữ nguyên ngày bảo hành, không có movement | A đang sửa theo phiếu khác hoặc là hàng thương mại chờ sửa | Trả thành công, không tăng tồn |
| Replace, máy A, máy B | A → Defective, B → Sold, B kế thừa hạn bảo hành của A. Xuất một đơn vị với movement WarrantyReplacement, trả MovementId và UnitCost của B | B sai biến thể, trùng A, không InStock, hoặc phần tồn khả dụng bằng 0 | Trả đúng MovementId và UnitCost ban đầu, tồn không giảm thêm |

Máy khách đang sửa và máy cũ đã đổi bảo hành không thể nhập lại qua endpoint khôi phục máy báo lỗi của kho. Với IMEI bán lại, Inventory đối chiếu dòng bán hiện tại dưới khóa. Với IMEI M3, dịch vụ tìm dòng giao gần nhất theo CompletedAt.

## Thứ tự khóa

1. Bên gọi khóa ca chi khi cần, rồi khóa đơn. Inventory khóa cùng đơn và các dòng đơn theo UUID tăng dần. Với kiểm định, bên gọi khóa dòng phiếu để xác nhận lượng còn lại.
2. Inventory tuần tự hóa mã thao tác bằng khóa transaction trong PostgreSQL. IMEI cũ và mới được khóa trong một truy vấn theo UUID tăng dần.
3. Inventory khóa tồn theo product_id rồi variant_id. Giải phóng giữ chỗ thay reserved_quantity. Chỉ nhập lại hàng bán được và xuất máy thay mới thay quantity.
4. Bên gọi hoàn tất sổ chi, điểm hoặc hồ sơ, rồi lưu và commit. Tất cả khóa được giải phóng khi transaction kết thúc.

## Đạt · gọi qua HTTP để đóng lượt giữ VietQR

Với mã giao dịch `qrId` mà checkout đã lưu, thu ngân bán đơn hoặc Admin gửi POST không có body. Trước hạn gọi `/api/pos/vietqr/{qrId}/cancel`; từ đúng thời điểm hết hạn gọi `/api/pos/vietqr/{qrId}/expire`. Hai đường đều dùng UUID của giao dịch làm mã release. Hủy chủ động trả QR `Failed` theo bộ trạng thái hiện có. Xử lý quá hạn trả `Expired`. Đơn chuyển `Cancelled` và không ghi thu hoặc chi. HTTP 200 trả `id`, `orderId`, `status`, `orderStatus`. Sau mất phản hồi, gửi lại cùng URL để lấy kết quả đã lưu.

Service khóa ca, đơn, các giao dịch QR của đơn, rồi gọi Inventory. Các lần xác nhận thanh toán mới cần tuân theo cùng thứ tự khóa và kiểm tra lại trạng thái sau khóa. Service trả 409 khi đơn đã trả tiền, số tiền hoặc ca không khớp, có giao dịch khác chờ/đã xác nhận, có mã tham chiếu thanh toán cần đối soát hoặc còn voucher phải giải phóng. Trong các trường hợp này, giữ chỗ và lượt voucher đều giữ nguyên. Tiền đến muộn phải đối soát theo mã giao dịch. Các API ở đây chỉ đóng lượt giữ đã tồn tại; checkout của Đạt phụ trách tạo mã QR và tiếp nhận kết quả ngân hàng.

`PosCheckoutPostgresHttpTests.VietQrHolds.cs` dựng giao dịch Pending trong DB thử và gọi ReserveAsync thật. Bài kiểm tra gửi HTTP qua JWT với ca và hai thu ngân riêng, kiểm tra cấm PATCH bỏ qua bước giải phóng, gửi lại lệnh cũ, hai yêu cầu hết hạn tranh với hủy và bảo toàn chứng từ khi gặp xung đột.

## Q. Vinh · API tiếp nhận và chốt phiếu bảo hành

Admin và Manager tạo một UUID cho phiếu và giữ mã đó khi gửi lại. `POST /api/warranties/{caseId}/receive` nhận `orderId`, `orderItemId`, `deviceId`, `customerId`. Với khách lẻ dùng `customerId: null`. Dòng bán phải đã giao, còn quyền bảo hành, thuộc khách của đơn; IMEI phải đúng lần bán hoặc là máy thay đã liên kết với chính dòng bán. Thời điểm tiếp nhận theo đồng hồ server thỏa `WarrantyStartAt <= OpenedAt < WarrantyEndAt`. Cả hồ sơ và thao tác Sold → UnderRepair được ghi trong một transaction.

`POST /api/warranties/{caseId}/return` trả máy đã sửa cho khách. `POST /api/warranties/{caseId}/replace` nhận `replacementDeviceId` để duyệt xuất máy thay. Cùng quyết định và cùng thiết bị trả lại kết quả đã ghi. Quyết định khác trên phiếu đã đóng trả 409. Nếu quá hạn trong lúc sửa, hạn bảo hành trên phiếu vẫn là hạn gốc đã xác nhận khi nhận. `GET /api/warranties/{caseId}` tra kết quả, IMEI cũ/mới, ngày nhận/trả, mã movement. Bảng `warranty_cases` giữ thêm người thao tác, hai ID nghiệp vụ kho, và giá vốn thay thế nullable để đối chiếu nội bộ.
