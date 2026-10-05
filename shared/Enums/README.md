# Shared Domain Enums

Thư mục chứa 10 kiểu liệt kê (Enum) dùng chung xuyên suốt giữa Backend API, Frontend Client và Test Suites:

1. **`OrderStatus.cs`**:
   - `Pending`: Đơn hàng mới tạo, đang chờ thu ngân xử lý hoặc chờ thanh toán.
   - `Processing`: Đã thanh toán, nhân viên đang lấy hàng/đóng gói.
   - `Completed`: Đơn hàng hoàn tất, đã giao hàng và kích hoạt bảo hành.
   - `Cancelled`: Đơn hàng bị hủy trước khi thanh toán hoàn tất.
   - `Refunded`: Đơn hàng được trả lại và hoàn tiền cho khách.

2. **`PaymentMethod.cs`**:
   - `Cash`: Thanh toán bằng tiền mặt tại quầy thu ngân POS.
   - `VietQr`: Thanh toán chuyển khoản ngân hàng qua mã VietQR động.
   - `Card`: Thanh toán qua máy quẹt thẻ ngân hàng (POS Card Terminal).

3. **`SerialImeiStatus.cs`**:
   - `InStock`: Thiết bị đang có mặt thực tế trong kho hoặc tại quầy trưng bày.
   - `Reserved`: Thiết bị bị khóa giữ chỗ cho đơn hàng đang thanh toán (chống bán trùng).
   - `Sold`: Thiết bị đã bán và liên kết với dòng đơn hàng `order_items`.
   - `Returned`: Khách trả lại, đang chờ kỹ thuật kiểm định trước khi restock hoặc hủy lỗi.
   - `UnderRepair`: Máy đã bán gửi lại sửa chữa/bảo hành; chuyển lại `Sold` khi trả khách.
   - `Defective`: Thiết bị lỗi phần cứng/hỏng, chờ trả nhà cung cấp (RMA).
   *(Lưu ý: Thời hạn bảo hành được tính động dựa trên `warranty_start_at` và `warranty_end_at`).*

4. **`InventoryMovementType.cs`**:
   - `OpeningBalance`: Số dư tồn kho ban đầu khi hệ thống bắt đầu theo dõi mặt hàng.
   - `PurchaseReceipt`: Nhập kho từ đơn đặt hàng nhà cung cấp (+).
   - `Sale`: Xuất kho khi đơn hàng hoàn tất (-).
   - `CustomerReturn`: Khách trả hàng sau khi kiểm định đạt chuẩn nhập lại kho bán (+).
   - `Adjustment`: Điều chỉnh số lượng sau kiểm kê hoặc mất mát (+/-).
   - `Defective`: Xuất hủy hàng hỏng/lỗi (-).

5. **`CustomerTier.cs`**:
   - `Standard`: Hạng tiêu chuẩn (0 - 499 điểm).
   - `Silver`: Hạng Bạc (500 - 1.499 điểm, chiết khấu 2%).
   - `Gold`: Hạng Vàng (1.500 - 4.999 điểm, chiết khấu 5%).
   - `Platinum`: Hạng Bạch kim VIP (5.000+ điểm, chiết khấu 10%).

6. **`VoucherType.cs`**:
   - `Percentage`: Chiết khấu theo tỷ lệ phần trăm (ví dụ: 10% giá trị đơn hàng).
   - `FixedAmount`: Giảm trừ trực tiếp số tiền cố định (ví dụ: 200.000 VND).

7. **`PurchaseOrderStatus.cs`**:
   - `Draft`: Đơn mua hàng đang chuẩn bị/lập dự thảo.
   - `Ordered`: Đã gửi đơn đặt hàng tới nhà cung cấp.
   - `PartiallyReceived`: Nhà cung cấp giao hàng từng phần, một số dòng đã nhận đủ, dòng khác còn thiếu.
   - `Received`: Toàn bộ hàng hóa đã nhận đủ vào kho và tăng số dư tồn kho.
   - `Cancelled`: Đơn mua hàng bị hủy.

8. **`PosSessionStatus.cs`**:
   - `Open`: Ca làm việc của thu ngân đang mở.
   - `Closed`: Ca làm việc đã kết thúc, đã đối soát tiền két.

9. **`VietQrStatus.cs`**:
   - `Pending`: Đã sinh chuỗi mã VietQR động, đang chờ khách quét và webhook ngân hàng.
   - `Confirmed`: Tiền đã về tài khoản, giao dịch hoàn tất thành công.
   - `Failed`: Giao dịch bị lỗi hoặc ngân hàng từ chối.
   - `Expired`: Mã VietQR hết hạn thanh toán (vượt quá thời gian chờ).

10. **`RoleType.cs`**:
    - `Admin`: Toàn quyền quản trị hệ thống, nhân sự, cấu hình và danh mục.
    - `Manager`: Cửa hàng trưởng, xem báo cáo doanh thu, lợi nhuận, thống kê.
    - `SalesStaff`: Nhân viên thu ngân / bán hàng tại quầy POS.
    - `WarehouseStaff`: Nhân viên thủ kho, nhập hàng NCC và quản lý Serial/IMEI.
