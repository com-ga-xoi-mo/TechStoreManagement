# Proposal

## Why

Hiện app chỉ đưa hàng vào kho được một lần cho mỗi sản phẩm/biến thể: `stocks/initialize` cho hàng không serial và `serials/batch-import` qua dialog "Nhập kho mới", vốn chặn khi tồn đã tồn tại. Sau đó cách duy nhất để thêm hàng là "Điều chỉnh tồn +N", không có giá nhập, không có nhà cung cấp. Với hàng có Serial/IMEI, điều chỉnh tăng còn làm `quantity` lệch với số máy thật. Giá vốn lúc bán (`GetAverageReceivedUnitCostAsync`) chỉ đọc `purchase_order_items`, nên hàng thêm bằng điều chỉnh không có giá vốn, và lô mới giá khác không được tính.

## What Changes

- Thêm **phiếu nhập một bước**: mỗi lần hàng về, người dùng lập một phiếu (nhà cung cấp, số chứng từ, ghi chú, nhiều dòng sản phẩm/biến thể với số lượng, giá nhập, số tháng bảo hành và Serial/IMEI cho hàng có serial) và xác nhận. Server kiểm tra toàn bộ rồi trong một transaction: tạo `purchase_orders` ở trạng thái `Received`, các dòng `purchase_order_items` đã nhận đủ, cộng tồn (tạo dòng tồn nếu chưa có), ghi movement `PurchaseReceipt` cho từng dòng và tạo các máy `InStock` có giá vốn riêng. Lỗi ở bất kỳ dòng nào thì không ghi gì.
- Chống ghi nhận lặp: client gửi `clientRequestId` cho mỗi lần lập phiếu; gửi lại cùng mã trả về phiếu đã có, không cộng kho lần hai.
- Thêm API xem danh sách và chi tiết phiếu nhập (gồm cả phiếu "Số dư đầu kỳ" và phiếu seed đã có).
- Thêm quản lý nhà cung cấp: danh sách/tìm kiếm, tạo, sửa, ngừng dùng/mở lại. Nhà cung cấp hệ thống "Số dư đầu kỳ" không hiện trong danh sách chọn và không sửa được.
- Client: thêm hai tab "Nhập hàng" và "Nhà cung cấp" trong màn Danh mục và Kho. Nút "Nhập kho mới" mở form tạo phiếu nhập; dialog nhập tồn ban đầu cũ (`ImportStockDialog`) bị bỏ khỏi UI. API `serials/batch-import` giữ nguyên; `stocks/initialize` chỉ còn nhận hàng không có Serial/IMEI (xem mục BREAKING bên dưới).
- Thêm **báo lỗi máy theo IMEI**: chọn một máy đang `InStock`, ghi lý do (hư hỏng, mất, ...) và chuyển máy sang `Defective`, tồn giảm 1. Khi sửa xong thì **đưa máy về kho**: `Defective` → `InStock`, tồn tăng 1. Máy không bao giờ bị xóa; mỗi lần đổi đều ghi lịch sử kho. Máy mất cũng dùng trạng thái `Defective`, có lý do "Mất".
- **BREAKING**: `POST /api/inventory/stocks/{stockId}/adjust` từ chối mọi điều chỉnh (tăng và giảm) cho hàng có Serial/IMEI (409). Máy mới vào qua phiếu nhập, máy bớt đi qua báo lỗi theo IMEI, nên số tồn luôn bằng số máy thật.
- **BREAKING**: `POST /api/inventory/stocks/initialize` từ chối hàng có Serial/IMEI (400). Trước đây API này tạo được tồn mà không có máy nào; nay hàng có serial chỉ vào kho kèm máy qua phiếu nhập hoặc `serials/batch-import`.
- Thêm câu SQL đối soát (tồn khác số máy `InStock` + `Reserved`) vào tài liệu module, và chạy nó trong mọi test PostgreSQL của kho để phát hiện lệch dữ liệu trước khi lên production.
- **BREAKING**: `GET /api/inventory/serials` trả `unitCost = null` cho SalesStaff; Admin, Manager, WarehouseStaff vẫn thấy giá vốn từng máy.
- Ngoài phạm vi: đặt hàng trước rồi nhận nhiều đợt (`Draft`/`Ordered`/`PartiallyReceived`), hủy phần chưa về, trả hàng nhà cung cấp, sửa/xóa phiếu đã nhận, che `costPrice` trong DTO sản phẩm, trạng thái riêng cho máy mất, bảo hành máy của khách (`UnderRepair`, M4).

## Capabilities

### New Capabilities

- `inventory-suppliers`: API quản lý nhà cung cấp, validation, ngừng dùng và bảo vệ nhà cung cấp hệ thống "Số dư đầu kỳ".
- `inventory-purchase-receipts`: API lập phiếu nhập một bước trong một transaction, chống gửi lặp, ghi giá vốn và lịch sử kho, xem danh sách/chi tiết phiếu, quyền theo role.
- `inventory-receipt-workspace`: tab "Nhập hàng" (danh sách, chi tiết, form tạo phiếu nhiều dòng) và tab "Nhà cung cấp" trong màn Danh mục và Kho của client.

### Modified Capabilities

- `inventory-management`: khởi tạo tồn và điều chỉnh tồn từ chối hàng có Serial/IMEI; yêu cầu tồn hàng serial luôn khớp số máy, kèm truy vấn đối soát; thêm API báo lỗi máy và đưa máy về kho; danh sách Serial/IMEI ẩn `unitCost` với SalesStaff.
- `inventory-stock-workspace`: "Nhập kho mới" không còn là hook chờ, mà mở form tạo phiếu nhập; tab Tồn kho làm mới sau khi phiếu được ghi; tab Serial/IMEI có menu "•••" với "Báo lỗi máy" và "Sửa xong, đưa về kho".

## Impact

- **Backend** `backend/Inventory/`: thêm `SuppliersController`, `PurchaseReceiptsController`, `ISupplierService`, `IPurchaseReceiptService`, mở rộng `IPurchaseOrderRepository` (hoặc thêm `ISupplierRepository`); sửa `InventoryService.AdjustStockAsync`, `SerialImeiService`/`InventoryController` cho giá vốn theo role và hai route báo lỗi/đưa về kho; đăng ký DI trong `Program.cs`.
- **Schema**: `purchase_orders` thêm `supplier_document_number` (nullable) và `client_request_id` (nullable, unique). Một migration EF Core mới, cập nhật `docs/database/schema.dbml`.
- **Shared**: DTO/Request mới cho nhà cung cấp và phiếu nhập.
- **Client** `frontend/TechStore.Client/`: tab, page, dialog, ViewModel và client repository mới cho phiếu nhập và nhà cung cấp; bỏ `ImportStockDialog` và ViewModel của nó; sửa `CategoryWorkspaceTab` và `CategoryManagementPage` (hook "Nhập kho mới" có sẵn mở form phiếu nhập); thêm client repository `ISerialDeviceRepository`; thêm menu dòng và dialog đổi trạng thái máy cho `SerialImeiPage`/`SerialImeiViewModel`.
- **Tests**: unit test service (fake repository), HTTP/PostgreSQL test cho transaction, gửi lặp, IMEI trùng và quyền; test headless cho ViewModel và repository client.
- **Docs**: `docs/API_PERMISSIONS.md`, `backend/Inventory/README.md`.
- **Phụ thuộc**: dùng bảng quyền và actor từ token của change `identity-access-management` (đã merge vào `dev`). Không đổi `IInventoryFulfillmentService`; thứ tự khóa của phiếu nhập được ghi trong design để luồng bán (POS) dùng chung.
