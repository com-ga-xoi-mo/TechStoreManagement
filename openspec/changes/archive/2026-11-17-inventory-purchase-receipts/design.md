# Design

## Context

Xem `proposal.md` phần Why. Những điểm đã kiểm tra trong source:

- Bảng `suppliers`, `purchase_orders`, `purchase_order_items`, `serial_imeis.purchase_order_id` và `inventory_movements.purchase_order_item_id` đã có, kèm check constraint, unique `(purchase_order_id, product_id, variant_id)` NULLS NOT DISTINCT và unique `po_number`. Enum `InventoryMovementType.PurchaseReceipt` đã có nhưng chỉ seed dùng.
- `InventoryService.StageOpeningReceiptAsync` đã tạo phiếu `Received` (ordered = received) cho nhà cung cấp ẩn "Số dư đầu kỳ", tìm theo tên. `IPurchaseOrderRepository` chỉ có `FindSupplierByNameAsync`, `AddSupplier`, `Add`.
- `SerialImeiService.BatchImportAsync` có sẵn kiểm tra IMEI 15 số, trùng trong payload và `FindExistingIdentifiersAsync` cho trùng trong DB.
- `InventoryFulfillmentService` khóa máy theo id tăng dần, rồi khóa tồn theo `OrderBy(ProductId).ThenBy(VariantId)` qua `GetForUpdateAsync`. Giá vốn hàng không serial lúc bán là `GetAverageReceivedUnitCostAsync`, trung bình có trọng số trên mọi `purchase_order_items` đã nhận.
- `AdjustStockAsync` không kiểm tra `IsSerialTracked`. `SerialImeiDto.UnitCost` trả cho mọi role. Client không hiển thị giá vốn serial.
- Client: màn Danh mục và Kho có `CategoryWorkspaceTab { Categories, Inventory, SerialImei }`. "Nhập kho mới" mở `ImportStockDialog`, dialog này gọi `InitializeStockAsync` hoặc `BatchImportSerialsAsync` và chặn khi tồn đã có. `InventoryStockDto` không có cờ serial.
- Actor lấy từ claim `NameIdentifier`/`sub` như `InventoryController.GetUserId`. Bảng quyền ở `docs/API_PERMISSIONS.md` đến từ change `identity-access-management`.

## Goals / Non-Goals

**Goals:** một luồng nhập hàng dùng nhiều lần, ghi đúng giá vốn và lịch sử kho, nguyên tử, an toàn khi gửi lại, cùng thứ tự khóa với luồng bán; tái dùng bảng và quy tắc đang có, chỉ thêm hai cột.

**Non-Goals:** không thêm bảng mới, không thêm trạng thái máy hay loại movement mới, không làm trạng thái `Draft`/`Ordered`/`PartiallyReceived`, không sửa/xóa/hủy phiếu đã nhận, không đổi `IInventoryFulfillmentService`, không đổi cách tính giá vốn trung bình, không làm menu/nút theo role ở client, không che `costPrice` sản phẩm. Không gộp `stocks/initialize` và `serials/batch-import` vào luồng mới. `serials/batch-import` giữ nguyên; `stocks/initialize` chỉ thêm một luật từ chối hàng có serial (D13), phần còn lại giữ nguyên.

## Decisions

### D1. Phiếu nhập là một `purchase_orders` ở trạng thái `Received`

Mỗi phiếu là một dòng `purchase_orders` (`Status = Received`, `OrderedAt = ReceivedAt = now`, `CreatedByUserId = ReceivedByUserId = actor`) với các `purchase_order_items` có `OrderedQuantity = ReceivedQuantity = quantity`. Đây đúng là hình dạng của phiếu "Số dư đầu kỳ", nên `GetAverageReceivedUnitCostAsync` và seed tiếp tục đọc được mà không sửa.

Phương án khác: bảng `purchase_receipts` riêng nối với phiếu đặt hàng. Bị loại vì chỉ cần khi có đặt hàng và nhận nhiều đợt (ngoài phạm vi). Nếu sau này làm, phiếu `Received` hiện tại vẫn là điểm cuối hợp lệ của vòng đời đó.

API dùng tên "purchase-receipts" (`/api/inventory/purchase-receipts`) vì người dùng chỉ thấy phiếu nhập, còn tên bảng giữ nguyên. Danh sách trả mọi `purchase_orders` có `Status = Received`, gồm phiếu `OB-` và phiếu seed.

### D2. Hai cột mới trên `purchase_orders`

| Cột | Kiểu | Ràng buộc | Dùng cho |
| --- | --- | --- | --- |
| `supplier_document_number` | `text NULL` | service kiểm tra ≤ 100 ký tự | số hóa đơn/phiếu giao của nhà cung cấp |
| `client_request_id` | `uuid NULL` | unique index `idx_purchase_orders_client_request_id` | chống ghi nhận lặp (D5) |

Index unique mặc định NULLS DISTINCT, nên các phiếu cũ để `NULL` không xung đột. Một migration `AddPurchaseReceiptFields`; cập nhật `PurchaseOrderConfiguration`, entity và `docs/database/schema.dbml`. Không cần backfill.

### D3. Phân lớp backend

```
SuppliersController        --> ISupplierService        --> ISupplierRepository
PurchaseReceiptsController --> IPurchaseReceiptService --> IPurchaseOrderRepository (mở rộng)
                                                         IInventoryStockRepository (GetForUpdateAsync, Add, AddMovement)
                                                         ISerialImeiRepository (FindExistingIdentifiersAsync, AddRange)
                                                         IProductRepository (+ đọc nhiều sản phẩm kèm biến thể)
                                                         ISupplierRepository, IUnitOfWork, TimeProvider
```

- `ISupplierRepository` mới: `SearchAsync(SupplierSearchCriteria)`, `GetByIdAsync` (AsNoTracking), `GetForUpdateAsync`/tracked `FindAsync`, `NameExistsAsync(normalized, excludeId)`, `FindByNameAsync`, `Add`. Chuyển `FindSupplierByNameAsync`/`AddSupplier` từ `IPurchaseOrderRepository` sang đây và sửa `InventoryService` theo. Tên hệ thống đặt ở một hằng nội bộ dùng chung (ví dụ `SystemSuppliers.OpeningBalanceName`) thay cho hằng riêng trong `InventoryService`.
- `IPurchaseOrderRepository` thêm `FindIdByClientRequestIdAsync`, `SearchReceiptsAsync(PurchaseReceiptSearchCriteria)` trả projection (kèm tên NCC, tên người nhận, số dòng, tổng số lượng) và `GetReceiptDetailAsync(id)` (Include dòng, sản phẩm, biến thể, máy theo `purchase_order_id`).
- `IProductRepository` thêm `GetManyWithVariantsAsync(ids)` (AsNoTracking) để kiểm tra mọi dòng trong một truy vấn.
- Controller không gọi repository. Service trả result record theo mẫu `InitializeStockResult` (`Success`/`Replayed`/`ValidationFailed`/`NotFound`/`Conflict`), controller chuyển sang 201/200/400/404/409.

### D4. Luồng ghi phiếu và thứ tự khóa

```
1. clientRequestId rong --> 400; neu da co phieu voi clientRequestId nay --> tra 200 phieu do (truoc moi kiem tra du lieu khac)
2. Kiem tra request (khong DB): header, dong, trung san pham, so may = so luong, IMEI, serial <= 100, trung ma trong phieu
3. Doc NCC + san pham (AsNoTracking): NCC ton tai, dang dung, khong phai "So du dau ky"; san pham ton tai;
   san pham co bien the thi bat buoc bien the dang dung cua no, khong co bien the thi khong gui bien the;
   serial-tracked thi phai co may, khong serial thi khong co may
4. BeginTransaction
   4a. FindExistingIdentifiers (serial/IMEI da co trong DB) --> 409
   4b. Voi tung dong theo (ProductId, VariantId) tang dan: GetForUpdateAsync; chua co thi Add InventoryStock(0, 0, 5)
       stock.Quantity += qty; AddMovement(PurchaseReceipt, quantity_after, PurchaseOrderItem = dong, Reason = "Phiếu nhập PN-...")
   4c. Stage PurchaseOrder + Items + SerialImei(InStock, UnitCost = gia dong, WarrantyMonths, PurchaseOrder = phieu)
   4d. SaveChanges --> Commit --> 201
5. Loi: Rollback; DuplicateKeyException: kiem tra lai clientRequestId truoc, roi moi xu ly theo ConstraintName (D5)
```

- Thứ tự khóa tồn dùng đúng sắp xếp của `InventoryFulfillmentService.LockStocksAsync` (`OrderBy(ProductId).ThenBy(VariantId)` trên Guid của .NET). Tách sắp xếp này thành một helper nội bộ dùng chung để hai luồng không lệch nhau. Luồng bán khóa máy rồi tồn; phiếu nhập không khóa máy có sẵn (chỉ thêm máy mới), nên không tạo vòng chờ. Ghi thứ tự này vào `backend/Inventory/README.md` cho luồng POS.
- Kiểm tra trùng serial/IMEI ở bước 4a chỉ để báo lỗi rõ. Chặn thật sự là unique index đang có trên `serial_imeis`; hai phiếu đồng thời cùng IMEI thì phiếu commit sau nhận `DuplicateKeyException` → 409.
- Số phiếu `PN-{yyyyMMdd}-{8 ký tự hex in hoa}` theo giờ UTC, như mẫu `OB-`. Trùng `po_number` (gần như không xảy ra) → 409 "thử lại".
- `TotalCost` = Σ `quantity × unitCost`, tính bằng `decimal` ở server. Kiểm tra tổng ≤ `UnitCostRules.MaxLineTotal`.
- Gom kiểm tra định dạng Serial/IMEI dùng chung với `SerialImeiService` (regex IMEI, trim, so trùng không phân biệt hoa thường) vào một helper nội bộ thay vì chép lại.

### D5. Chống gửi lặp bằng `clientRequestId`

Client sinh một GUID khi mở form và gửi lại đúng GUID đó mỗi lần bấm xác nhận. Server:

- Đầu request, trước mọi kiểm tra dữ liệu khác: nếu đã có phiếu với GUID này thì trả **200** với chi tiết phiếu đó. Nhờ vậy gửi lại sau khi nhà cung cấp bị ngừng dùng vẫn nhận phiếu cũ, không ra 400.
- Mọi `DuplicateKeyException` lúc SaveChanges đều đi qua cùng một nhánh: rollback, rồi **đọc lại phiếu theo GUID trước**. Nếu có thì trả 200. Lý do: khi hai request cùng GUID chạy song song cho sản phẩm chưa có dòng tồn, cả hai đều không thấy dòng để khóa, nên request sau có thể vấp unique của dòng tồn hoặc của IMEI trước khi tới unique của `client_request_id`. Thứ tự EF chèn các bảng không được đảm bảo theo ý mình (`inventory_stocks` không phụ thuộc phiếu nên có thể được chèn trước). Request sau nằm chờ trên index entry của giao dịch đầu, nên khi nó nhận lỗi trùng thì giao dịch đầu đã commit xong.
- Nếu không có phiếu với GUID đó thì mới xử lý theo `ConstraintName`: `idx_inventory_stocks_product_variant` → 409 "Tồn kho vừa được tạo bởi thao tác khác, vui lòng thử lại" (gửi lại cùng GUID sẽ thành công vì giao dịch trước đã rollback); `idx_serial_imeis_imei` / `idx_serial_imeis_serial` → 409 kèm mã trùng; `idx_purchase_orders_po_number` → 409 "thử lại".

Không so nội dung request với phiếu đã lưu. Cùng GUID luôn trả phiếu cũ, kể cả khi payload khác. Phương án lưu hash payload và trả 422 khi lệch bị loại vì form chỉ sinh GUID một lần, nên không có trường hợp hợp lệ nào gửi cùng GUID với nội dung khác.

### D6. Contract chia sẻ (`shared/`)

- Requests: `SupplierFilterRequest` (search, isActive, page, pageSize), `SaveSupplierRequest` (name, contactName, phone, email, address), `SetSupplierStatusRequest` (`bool? IsActive`), `PurchaseReceiptFilterRequest` (search, supplierId, page, pageSize), `CreatePurchaseReceiptRequest` (`ClientRequestId`, `SupplierId`, `SupplierDocumentNumber`, `Note`, `Lines`), `PurchaseReceiptLineRequest` (`ProductId`, `VariantId`, `Quantity`, `UnitCost`, `WarrantyMonths`, `Devices`), `PurchaseReceiptDeviceRequest` (`SerialNumber`, `Imei`). Trường số để nullable như request hiện có, nhờ vậy thiếu giá trị thành lỗi 400 thay vì 0.
- DTOs: `SupplierDto` (kèm `ReceiptCount`), `PurchaseReceiptSummaryDto`, `PurchaseReceiptDto` (header + `Note` + `Lines`), `PurchaseReceiptLineDto` (product/variant/SKU, quantity, unit cost, line total, `IsSerialTracked`, `Devices`), `PurchaseReceiptDeviceDto` (serial, IMEI, status).
- Request không có trường tổng tiền. Nếu client gửi thêm trường lạ, JSON binder bỏ qua.
- `PurchaseReceiptSummaryDto.ReceivedByName` là nullable, vì `received_by_user_id` của phiếu cũ hoặc phiếu seed có thể null; client hiển thị "—".
- Serial number tối đa 100 ký tự sau khi trim (cột `text`, giới hạn chỉ ở service, để chặn chuỗi rác). Sản phẩm có biến thể bắt buộc chọn một biến thể đang dùng, khớp quy tắc tạo đơn ở `OrderService` (sản phẩm có biến thể chỉ bán theo biến thể).

### D7. Quyền và giá vốn

| Route | Roles |
| --- | --- |
| GET suppliers, GET purchase-receipts, GET purchase-receipts/{id} | A M W |
| POST/PUT/PATCH suppliers, POST purchase-receipts | A W |

Dùng `[Authorize(Roles = ...)]` như `InventoryController`. Với `GET /api/inventory/serials`, controller tính `includeCost = User.IsInRole(Admin|Manager|WarehouseStaff)` và truyền vào `SerialImeiService.GetSerialsAsync`. Service đặt `UnitCost = null` khi không được xem, để service không phụ thuộc HTTP. Dùng allowlist chứ không loại riêng SalesStaff, để role mới sau này mặc định không thấy giá vốn. Cập nhật `docs/API_PERMISSIONS.md`.

### D8. Chặn điều chỉnh tồn cho hàng có serial

`AdjustStockAsync`, sau khi khóa dòng tồn, đọc `IsSerialTracked` của sản phẩm (qua `IProductRepository` hoặc Include trong `GetByIdForUpdateAsync`). Nếu là hàng có serial thì rollback và trả Conflict: "Hàng có Serial/IMEI: nhập máy qua phiếu nhập, bớt máy bằng Báo lỗi máy theo từng IMEI.", bất kể tăng hay giảm. Dialog điều chỉnh hiện có đã giữ form và hiện lỗi 409 (requirement "Detail and adjustment error recovery"), nên client không cần sửa. `InventoryStockDto` chưa có cờ serial nên không ẩn nút trước được; điều này chấp nhận được vì server luôn chặn.

### D9. Nhà cung cấp

- Chuẩn hóa tên: trim, so sánh không phân biệt hoa thường (`lower(name)` ở repository). Trùng tên kiểm tra trong service và trả 409. Không thêm unique index, vì thêm một unique index biểu thức trong EF cần SQL thô, còn khả năng hai người tạo cùng tên một lúc là rất thấp.
- Nhận diện nhà cung cấp hệ thống bằng tên hằng. Danh sách luôn loại tên này, update/status trả 409, create/rename trùng tên này trả 409.
- Phone theo regex `^[0-9 +().-]{1,30}$`. Email kiểm tra bằng `MailAddress.TryCreate`.
- `ReceiptCount` là số `purchase_orders` của nhà cung cấp, đếm bằng subquery trong projection.

### D10. Client

- **Models** (`Models/Inventory/`): `SupplierItem`, `PurchaseReceiptSummary`, `PurchaseReceiptDetail`, `PurchaseReceiptLine`, `PurchaseReceiptDevice`. **Repositories** (`Repositories/Inventory/`): `ISupplierRepository`/`SupplierRepository`, `IPurchaseReceiptRepository`/`PurchaseReceiptRepository`. Chúng map DTO sang model và trả result với failure kind (BadRequest kèm lỗi theo field, Unauthorized, Forbidden, NotFound, Conflict kèm detail, Network) theo mẫu `InventoryStockDetailFailure`. Repository mới không trả DTO.
- **Tabs**: thêm `PurchaseReceipts`, `Suppliers` vào `CategoryWorkspaceTab`. `CategoryManagementPage` host hai view mới `PurchaseReceiptListView`, `SupplierListView` (C# Markup, theme accessors, row "•••" bằng `RowActionsButton` + `MenuFlyout` của `WorkspaceParts.cs`, ít `Border` sơn màu trên mỗi dòng theo quy tắc Skia).
- **Form tạo phiếu** (`CreatePurchaseReceiptDialog` + `CreatePurchaseReceiptDialogViewModel`): phía trên là bộ chọn sản phẩm (tải sản phẩm đang bán như `ImportStockDialog`: PageSize 100, biến thể tải khi chọn) cùng nút "Thêm dòng". Bên dưới là danh sách `ReceiptLineDraft` (ObservableObject) chỉ hiện sản phẩm đã chọn và cho sửa số lượng, giá nhập, bảo hành, ô Serial/IMEI. Không đặt picker trong item template, để tránh các bẫy binding của item template trong `AGENTS.md`. Lệnh xóa dòng gán trực tiếp và nhận `object?`. Parse tiền theo `vi-VN` như `ImportStockDialogViewModel.TryReadUnitCost`. Tách mã Serial/IMEI theo `\r \n , ; \t`; chuỗi 15 chữ số là IMEI, còn lại là serial (giữ quy tắc của dialog cũ). `ClientRequestId` sinh trong constructor của ViewModel.
- **Dialog**: `PurchaseReceiptDetailDialog`, `SupplierEditorDialog` (tạo/sửa), xác nhận "Ngừng sử dụng"/"Sử dụng lại" qua `IDialogService` có sẵn.
- **"Nhập kho mới" dùng lại hook có sẵn**: hai nút "Nhập kho mới" ở tab Tồn kho và tab Serial/IMEI (`CategoryManagementPage`) đã cùng gọi `InventoryStockViewModel.ImportStockCommand`, lệnh này phát `ActionRequested(InventoryStockAction.Import)` (đã có test trong `InventoryStockViewModelTests`). Giữ nguyên lệnh và sự kiện; chỉ đổi `OnInventoryActionRequested` của page từ mở `ImportStockDialog` sang mở `CreatePurchaseReceiptDialog` qua `_dialogPresenter` như các dialog hiện có. Nút "Tạo phiếu nhập" của tab Nhập hàng mở cùng form. Sau khi phiếu được ghi, page làm mới tab Tồn kho, Serial/IMEI (phiếu có thể thêm máy) và Nhập hàng, mỗi tab đúng một lần.
- **Logic nằm trong ViewModel, page chỉ mở dialog**: `CategoryManagementViewModel` sở hữu `Receipts` và `Suppliers`, tự tải trang đầu khi tab được chọn lần đầu, và có `OnReceiptCreatedAsync`/`OnDeviceStatusChangedAsync` để làm mới các danh sách mỗi cái một lần (page không nghe `PropertyChanged`). Đổi trạng thái nhà cung cấp gọi `SupplierListViewModel.ChangeStatusAsync` sau khi page hỏi xác nhận.
- **Ô nhập trong form và dialog** dùng `UpdateSourceTrigger.PropertyChanged` để số mã, thành tiền và nút xác nhận cập nhật ngay khi gõ. Số lượng và bảo hành là chuỗi (`QuantityText`, `WarrantyMonthsText`) được kiểm tra trong ViewModel. Lệnh xóa dòng gán trực tiếp `Command = viewModel.RemoveLineCommand`, vì trong dòng `DataContext` là `ReceiptLineDraft`. Lỗi 400 của server được hiển thị theo từng dòng ("Dòng 1 (iPhone 16): ..."). Bộ chọn sản phẩm và nhà cung cấp tải hết mọi trang. "Thêm dòng" bị tắt khi biến thể đang tải, và kết quả tải biến thể cũ bị bỏ qua.
- **Quyền ở client**: `InventoryPermissions` (cùng bảng quyền D7) cho biết ai được ghi (Admin, WarehouseStaff) và ai được xem phiếu nhập/nhà cung cấp (thêm Manager). `CategoryManagementViewModel.ApplyPermissions` (chạy khi trang được mở) đặt `CanWrite` cho các ViewModel con; nút "Nhập kho mới", "Tạo phiếu nhập", "Thêm nhà cung cấp" và menu dòng của nhà cung cấp/Serial bị ẩn khi không có quyền, và lệnh tương ứng cũng từ chối. Danh sách phiếu nhập và nhà cung cấp chỉ được tải trước cho vai trò được xem; SalesStaff mở tab thì thấy thông báo không có quyền.
- **Lỗi hiển thị**: client repository chỉ chuyển `detail` của server (có thể null), không chuyển câu kỹ thuật của transport; ViewModel chọn câu tiếng Việt theo loại lỗi. Khi server trả phiếu đã ghi (200 replay), thông báo nói rõ phiếu đã được ghi ở lần gửi trước.
- **Sự kiện ViewModel → Page mới** (ngoại lệ được phép theo `AGENTS.md`, ghi lại tại đây): `ReceiptCreated` (của form tạo phiếu), `ReceiptDetailRequested`, `CreateReceiptRequested` (nút "Tạo phiếu nhập" của tab Nhập hàng), `SupplierEditorRequested`, `SupplierSaved`, `SupplierStatusRequested`, `DeviceStatusRequested`, `DeviceStatusChanged` (D12), cùng `RequestClose` → `Hide()` của các dialog mới. `ActionRequested` đã nằm trong danh sách cho phép.
- **Bỏ** `ImportStockDialog`, `ImportStockDialogViewModel` và test của chúng. Bỏ `InitializeStockAsync`/`BatchImportSerialsAsync` khỏi client `IInventoryRepository` (không còn nơi gọi sau khi bỏ dialog), và xóa các stub tương ứng trong 5 test double còn lại của interface này (`SerialImeiViewModelTests`, `StockAdjustmentDialogViewModelTests`, `CategoryWorkspaceTests`, `InventoryStockViewModelTests`, `DeleteStockItemDialogViewModelTests`). Hai API server vẫn còn (`initialize` đổi theo D13).
- Đăng ký DI (Transient) trong `UiServiceCollectionExtensions`.

### D11. Test

- **Backend unit** (fake repositories): validation từng trường (gồm bắt buộc biến thể); đổi fixture test cũ đang dựa vào `Product.IsSerialTracked` mặc định `true`; serial/non-serial; trùng trong phiếu và trong DB; tổng server tính; replay trả 200; rollback khi lỗi; thứ tự khóa (fake ghi lại thứ tự gọi `GetForUpdateAsync`); nhà cung cấp hệ thống; tên trùng; chặn điều chỉnh tồn hàng serial; ẩn giá vốn; báo lỗi/đưa về kho theo từng trạng thái, lý do, thứ tự khóa máy → tồn và movement ghi ra. Mở rộng `FakePurchaseOrderRepository`, thêm `FakeSupplierRepository`.
- **HTTP** (`InventoryApiHost`): ma trận quyền 401/403 cho mọi route mới, Sales nhận `unitCost = null`.
- **PostgreSQL** (`[PostgresFact]`, chạy khi có `TECHSTORE_TEST_CONNECTION`; mẫu `InventoryPostgresTests`, còn mẫu chạy đồng thời có điểm đồng bộ là `IdentityConcurrencyPostgresTests`): ghi phiếu thật và kiểm tra tồn, movement, máy, giá vốn trung bình; lỗi giữa chừng không để lại gì; cùng `clientRequestId` tuần tự và song song (gồm sản phẩm chưa có dòng tồn); hai phiếu song song cùng sản phẩm (tổng đúng); hai phiếu song song cùng IMEI (một 201, một 409); báo lỗi máy song song với thanh toán đơn giữ chỗ cùng máy (đúng một bên thành công); báo lỗi rồi đưa về kho thì tồn và số máy luôn khớp. Song song bằng hai `DbContext`/transaction độc lập có điểm đồng bộ, không phải hai request chạy lần lượt.
- **Frontend headless**: ViewModel và repository mới, sinh và giữ `clientRequestId`, validation, map lỗi.

### D12. Báo lỗi máy và đưa máy về kho

```
             POST serials/{id}/defective (reason, note)
 [InStock] ----------------------------------------------> [Defective]
     ^          quantity -1, movement Defective -1              |
     |                                                          |
     +----------------------------------------------------------+
             POST serials/{id}/restore (note)
             quantity +1, movement Adjustment +1
```

- Đặt trong `ISerialImeiService` (`ReportDefectiveAsync`, `RestoreAsync`), route trên `InventoryController` cạnh `GET serials`, role Admin/WarehouseStaff. Request mới `ReportDefectiveDeviceRequest` (`Reason`, `Note`) và `RestoreDeviceRequest` (`Note`); trả `SerialImeiDto` (giá vốn theo D7).
- Transaction: khóa máy bằng `ISerialImeiRepository.GetForUpdateAsync([id])` trước, rồi khóa tồn bằng `GetForUpdateAsync(productId, variantId)`, đúng thứ tự máy → tồn của `InventoryFulfillmentService`. Kiểm tra trạng thái sau khi khóa: báo lỗi cần `InStock`, đưa về cần `Defective`, sai thì trả 409. Báo lỗi kiểm tra thêm `quantity - 1 >= reserved_quantity`; nếu dữ liệu cũ đã lệch thì 409 thay vì để check constraint ném lỗi.
- Lịch sử kho: báo lỗi ghi `InventoryMovementType.Defective` với −1; đưa về ghi `Adjustment` với +1 vì enum không có loại "sửa xong", và không thêm loại mới để `schema.dbml` và báo cáo cũ khỏi phải đổi. `Reason` có dạng "Báo lỗi máy IMEI 3567…: Hư màn hình — <note>" và "Sửa xong máy IMEI 3567…: <note>" (dùng serial khi không có IMEI). Cột `reason` là `text` không giới hạn, nên lý do ≤ 200 và ghi chú ≤ 1000 ký tự luôn lưu đủ.
- Máy mất dùng chung `Defective` với lý do "Mất", không thêm trạng thái mới (người dùng đã chọn). Sau này nếu cần tách thì thêm `Lost` vào enum và chặn restore cho trạng thái đó.
- Máy không bao giờ bị xóa. `DELETE stocks/{id}` đã từ chối khi có máy khác `InStock` hoặc có lịch sử, nên máy `Defective` giữ nguyên lịch sử.
- Client: `SerialImeiViewModel` thêm hai lệnh nhận `object?` (kiểm tra kiểu dòng) và phát sự kiện `DeviceStatusRequested`. Page mở `DeviceStatusDialog` (một dialog cho cả hai thao tác; chế độ báo lỗi có hai nút gợi ý "Hư hỏng" và "Mất" điền vào ô lý do, và ô lý do cho phép tự gõ). Khi `DeviceStatusChanged` thì tải lại trang hiện tại một lần. Menu dòng dùng `RowActionsButton` + `MenuFlyout` như tab Tồn kho; mục không áp dụng được thì disable theo trạng thái dòng. Hai thao tác này nằm trong client repository nhỏ riêng `ISerialDeviceRepository`/`SerialDeviceRepository` (`ReportDefectiveAsync`, `RestoreAsync`, failure kind như D10), không thêm vào `IInventoryRepository`, để không phải sửa 6 test double của interface đó chỉ vì thêm method. Nhãn loại lịch sử trong `StockDetailHistoryDialog` thêm `Defective` → "Báo lỗi máy" và `OpeningBalance` → "Tồn đầu kỳ" (hiện chưa có, nên dialog đang hiện chữ tiếng Anh).

### D13. Giữ tồn hàng serial khớp số máy ở production

Bất biến `quantity = COUNT(InStock, Reserved)` và `reserved_quantity = COUNT(Reserved)` do service giữ, database không kiểm tra. Sau change này, các đường ghi được rà như sau:

| Đường ghi | Cách giữ bất biến |
| --- | --- |
| Phiếu nhập, `serials/batch-import` | +N tồn cùng N máy `InStock` trong một transaction |
| Reserve / ConfirmSale | Đổi trạng thái máy và tồn cùng lúc (đã có) |
| Báo lỗi / đưa về kho | ±1 tồn cùng đổi trạng thái đúng máy (D12) |
| `stocks/{id}/adjust` | Chặn với hàng serial (D8) |
| `stocks/initialize` | **Sửa trong change này**: trả 400 khi `IsSerialTracked`, vì trước đây tạo được tồn không có máy |
| `DELETE stocks/{id}` | Xóa tồn cùng các máy `InStock`, từ chối khi có máy khác trạng thái (đã có) |
| Hủy đơn | Chỉ hủy đơn `Pending`, không giữ chỗ (đã có) |
| Đổi `IsSerialTracked` của sản phẩm | Bị chặn khi sản phẩm có tồn, phiếu nhập hoặc đơn (`ProductService`, đã có) |

Phương án trigger hoặc constraint trigger trong PostgreSQL để database tự chặn bị loại: phải đếm máy trên mỗi lần ghi `serial_imeis` và `inventory_stocks`, khó viết đúng với các bước trung gian trong cùng transaction, và nằm ngoài EF migrations quen thuộc của nhóm.

Thay vào đó là phát hiện sớm:

- **Truy vấn đối soát** ghi trong `backend/Inventory/README.md`: với mỗi `inventory_stocks` của sản phẩm `is_serial_tracked`, so `quantity`/`reserved_quantity` với số máy đếm theo `(product_id, variant_id)` (dùng `IS NOT DISTINCT FROM` cho `variant_id` NULL), trả các dòng lệch. Chạy được bằng `psql` trước khi demo hoặc định kỳ ở production.
- **Helper test** `AssertSerialStockReconciledAsync` trong project test PostgreSQL chạy đúng câu SQL đó. Mọi test PostgreSQL của kho (nhập, bán, báo lỗi, đồng thời) gọi helper sau phần Assert, và có một test cố ý làm lệch bằng SQL để chứng minh helper bắt được.
- Tính năng sau này (đổi trả M4, sửa phiếu) phải liệt kê mình vào bảng trên và dùng helper này. Điều này được ghi trong README.

## Risks / Trade-offs

- [Phiếu đã nhận không sửa được; nhập sai giá hoặc IMEI thì không có cách sửa trong app] → Hàng không serial vẫn sửa được số lượng bằng điều chỉnh tồn (giá vốn thì không). Máy nhập thừa hoặc nhập nhầm có thể tạm báo lỗi để rời khỏi tồn bán được. Ghi giới hạn này vào tài liệu và để việc sửa/hủy phiếu cho lần sau.
- [Dữ liệu cũ đã lệch (tồn khác số máy) không còn sửa được bằng điều chỉnh tồn] → Báo lỗi máy kiểm tra `quantity - 1 >= reserved_quantity` và trả 409 thay vì làm hỏng dữ liệu. Dữ liệu demo được seed khớp sẵn. Trước khi triển khai, chạy truy vấn đối soát (D13) trên database thật; nếu có dòng lệch thì sửa bằng script riêng, không mở lại điều chỉnh tồn.
- [Database không tự chặn lệch tồn hàng serial; sửa SQL tay hoặc tính năng mới quên quy tắc vẫn có thể làm lệch] → Truy vấn đối soát và helper test bắt buộc trong mọi test PostgreSQL của kho (D13).
- [Seed khi khởi động ở Development báo "Demo seed conflict" sau khi nhập thêm, bán hoặc báo lỗi máy của sản phẩm demo `DEMO-` có serial, vì `DemoSeedRepository` đối chiếu tồn và trạng thái IMEI với dữ liệu mẫu cố định rồi rollback khi lệch (đúng spec `data-seeding`: không ghi đè dữ liệu người dùng đã sửa)] → App vẫn chạy, dữ liệu không hỏng, chỉ phần seed của lần đó bị bỏ qua. Ghi vào README module là lỗi dự kiến, kiểm tra trong task 7.2, và báo người phụ trách dữ liệu demo. Muốn demo lại từ đầu thì dùng database mới.
- [Máy mất và máy hư cùng trạng thái `Defective`; người dùng có thể bấm "Sửa xong" cho máy mất] → Lý do "Mất" hiện trong lịch sử. Tách trạng thái `Lost` khi cần (D12).
- [Cùng `clientRequestId` với payload khác vẫn trả phiếu cũ] → Form sinh GUID mới mỗi lần mở; API docs ghi rõ ngữ nghĩa này.
- [Hai phiếu cùng tạo dòng tồn đầu tiên: một bên nhận 409] → Thông báo "thử lại"; gửi lại cùng GUID thành công.
- [Giá vốn trung bình tính trên mọi lô đã nhận, kể cả hàng đã bán] → Giữ cách tính hiện có, ghi rõ để báo cáo lợi nhuận đối chiếu đúng.
- [Trùng tên nhà cung cấp chỉ kiểm tra ở service] → Chấp nhận khả năng rất thấp hai người tạo cùng tên một lúc.
- [Bộ chọn sản phẩm chỉ tải 100 sản phẩm đầu] → Giới hạn giống dialog cũ và đủ cho dữ liệu demo. Đổi sang tìm theo từ khóa nếu catalog lớn hơn.
- [Client chưa ẩn nút theo role] → Server luôn chặn; UI hiện thông báo 403 rõ ràng. Ẩn nút theo role thuộc phần giao diện theo role.

## Migration Plan

1. Thêm migration `AddPurchaseReceiptFields` (hai cột nullable + unique index) và cập nhật `schema.dbml`. Startup auto-migrate áp dụng như các migration trước.
2. Triển khai backend trước client: API cũ không đổi contract, trừ ba thay đổi BREAKING (khởi tạo và điều chỉnh tồn hàng serial, `unitCost` của Sales). Client mới không còn gọi `stocks/initialize` cho hàng serial; client cũ chỉ nhận thông báo lỗi. Chạy truy vấn đối soát trước và sau khi triển khai.
3. Rollback: gỡ migration. Hai cột nullable chỉ chứa dữ liệu của phiếu mới; các phiếu mới vẫn đọc được như `purchase_orders` bình thường.
