# Tasks

## 1. Schema và contract chung

- [x] 1.1 Thêm `SupplierDocumentNumber` và `ClientRequestId` vào `PurchaseOrder` và `PurchaseOrderConfiguration` (unique index `idx_purchase_orders_client_request_id`) theo design D2; tạo migration `AddPurchaseReceiptFields` bằng `dotnet ef migrations add` và kiểm tra `dotnet ef database update` chạy được trên database M3 hiện có mà phiếu cũ giữ `NULL`.
- [x] 1.2 Cập nhật `docs/database/schema.dbml` cho hai cột và index mới; kiểm tra tên cột/index trùng với migration (`\d purchase_orders` trong psql).
- [x] 1.3 Thêm requests và DTOs trong `shared/` theo design D6 (nhà cung cấp, phiếu nhập, dòng, máy); kiểm tra `dotnet build TechStore.sln` thành công và request không có trường tổng tiền.

## 2. Nhà cung cấp (backend)

- [x] 2.1 Thêm `ISupplierRepository`/`SupplierRepository` và `SupplierSearchCriteria` (AsNoTracking khi đọc, tìm kiếm literal `%`/`_`, `ReceiptCount`, loại nhà cung cấp hệ thống), chuyển `FindSupplierByNameAsync`/`AddSupplier` khỏi `IPurchaseOrderRepository`, dùng hằng tên hệ thống chung cho `InventoryService`; chuyển hai method tương ứng từ `FakePurchaseOrderRepository` sang `FakeSupplierRepository` mới; kiểm tra các test `InventoryServiceTests` cho số dư đầu kỳ vẫn qua.
- [x] 2.2 Thêm `ISupplierService`/`SupplierService` (validation tên, phone, email, độ dài; tên trùng không phân biệt hoa thường → 409; bảo vệ "Số dư đầu kỳ"; ngừng dùng/mở lại) và `FakeSupplierRepository`; unit tests phủ mọi scenario trong `specs/inventory-suppliers/spec.md`.
- [x] 2.3 Thêm `SuppliersController` (`GET`, `POST`, `PUT /{id}`, `PATCH /{id}/status`) với role theo design D7, đăng ký DI trong `Program.cs`; HTTP tests qua `InventoryApiHost` cho 200/201/400/404/409 và 401/403 (Sales mọi route, Manager khi ghi).

## 3. Phiếu nhập (backend)

- [x] 3.1 Tách helper nội bộ dùng chung cho thứ tự khóa tồn (`ProductId`, rồi `VariantId`) và kiểm tra định dạng/trùng Serial/IMEI; cho `InventoryFulfillmentService` và `SerialImeiService` dùng helper này; kiểm tra `InventoryFulfillmentServiceTests` và `SerialImeiServiceTests` vẫn qua.
- [x] 3.2 Mở rộng `IPurchaseOrderRepository` (`FindIdByClientRequestIdAsync`, `SearchReceiptsAsync`, `GetReceiptDetailAsync`) và `IProductRepository.GetManyWithVariantsAsync`; cập nhật `FakePurchaseOrderRepository` và `FakeProductRepository` (backend) để `dotnet build` test project thành công; test PostgreSQL cho projection danh sách (tên NCC, người nhận, số dòng, tổng số lượng) và cho tìm kiếm literal.
- [x] 3.3 Viết `IPurchaseReceiptService.CreateAsync` theo design D4–D5: kiểm tra gửi lặp theo `clientRequestId` trước mọi kiểm tra dữ liệu khác, validation header/dòng/máy (bắt buộc biến thể đang dùng cho sản phẩm có biến thể, serial ≤ 100 ký tự), kiểm tra NCC và sản phẩm, replay theo `clientRequestId`, khóa tồn theo thứ tự chung, tạo tồn nếu thiếu, movement `PurchaseReceipt` có số phiếu, máy `InStock` có giá vốn và bảo hành, mọi `DuplicateKeyException` đọc lại phiếu theo `clientRequestId` trước rồi mới xử lý theo constraint; unit tests phủ mọi scenario validation, ghi, replay và rollback trong `specs/inventory-purchase-receipts/spec.md`, gồm cả kiểm tra fake ghi đúng thứ tự gọi `GetForUpdateAsync`.
- [x] 3.4 Viết `SearchAsync` và `GetDetailAsync` của service (phân trang như `GetStocksAsync`, mới nhất trước, gồm phiếu `OB-`); unit tests cho lọc, phân trang và 404.
- [x] 3.5 Thêm `PurchaseReceiptsController` (`GET`, `GET /{id}`, `POST` trả 201 hoặc 200 khi replay) với role theo design D7, đăng ký DI; HTTP tests cho mã trạng thái, Location header và ma trận 401/403.
- [x] 3.6 Test PostgreSQL theo design D11: ghi phiếu có và không có serial rồi đối chiếu tồn, movement, máy và giá vốn trung bình (10 × 100.000 + 10 × 120.000 → 110.000 khi bán); lỗi giữa giao dịch không để lại dòng nào; cùng `clientRequestId` tuần tự và song song chỉ ghi một lần, kể cả khi sản phẩm chưa có dòng tồn và dòng có IMEI (một 201, một 200, không có 409); hai phiếu song song cùng sản phẩm cộng đủ; hai phiếu song song cùng IMEI thì một 201, một 409. Ghi lệnh chạy trong `backend/Inventory/README.md`.

## 4. Giá vốn serial, báo lỗi máy và điều chỉnh tồn

- [x] 4.1 Truyền `includeCost` từ `InventoryController.GetSerials` (allowlist Admin, Manager, WarehouseStaff) vào `SerialImeiService.GetSerialsAsync` và đặt `UnitCost = null` khi không được xem; unit test service và HTTP test Sales nhận `null`, Warehouse/Manager nhận giá thật.
- [x] 4.2 Trong `AdjustStockAsync`, từ chối mọi `QuantityChange` cho sản phẩm có `IsSerialTracked` bằng Conflict tiếng Việt theo design D8; vì `Product.IsSerialTracked` mặc định là `true`, đổi fixture của các test điều chỉnh hiện có trong `InventoryServiceTests` (Increase, DecreaseBelowReserved, CheckConstraintViolation) sang `IsSerialTracked = false`; unit test tăng và giảm đều bị 409 mà tồn và movement không đổi, hàng không serial vẫn điều chỉnh như cũ; HTTP test trả 409 ProblemDetails.
- [x] 4.3 Trong `InitializeStockAsync`, trả 400 dưới `productId` khi sản phẩm có `IsSerialTracked` theo design D13; đổi fixture các test khởi tạo hiện có đang dùng `new Product` mặc định (Succeed, Conflict khi tồn đã có, Quantity ≤ 0, Variant sai sản phẩm) sang `IsSerialTracked = false`, chỉ giữ hàng serial ở test bị từ chối; sửa test "serial-tracked needs no cost" trong `InventoryServiceTests` thành test bị từ chối và không ghi gì, thêm HTTP test 400; kiểm tra `OrdersApiIntegrationTests` chỉ khởi tạo tồn cho phụ kiện không serial.
- [x] 4.4 Thêm `ReportDefectiveAsync` và `RestoreAsync` vào `ISerialImeiService` theo design D12 (khóa máy rồi tồn, kiểm tra trạng thái sau khi khóa, tồn ±1, movement `Defective` −1 / `Adjustment` +1 có IMEI và lý do, giữ giá vốn và liên kết phiếu); thêm requests vào `shared/Requests`; thêm hai method vào `FakeSerialImeiService` trong `InventoryControllerTests` để project test biên dịch; unit tests phủ mọi scenario "Report a device defective" và "Return a repaired device to stock", gồm cả thứ tự khóa và trường hợp `quantity - 1 < reserved_quantity` trả 409.
- [x] 4.5 Thêm route `POST serials/{id}/defective` và `POST serials/{id}/restore` vào `InventoryController` (Admin, WarehouseStaff); HTTP tests cho 200/400/404/409 và 401/403 với Manager và Sales, dùng `FakeSerialImeiService` đã mở rộng ở 4.4.
- [x] 4.6 Test PostgreSQL (mẫu chạy đồng thời: `IdentityConcurrencyPostgresTests`): báo lỗi máy song song với thanh toán đơn giữ chỗ cùng máy (hai transaction độc lập có điểm đồng bộ) thì đúng một bên thành công; một máy báo lỗi rồi đưa về kho thì `quantity` luôn bằng số máy `InStock` + `Reserved`; máy `Defective` không giữ chỗ được khi thanh toán đơn.
- [x] 4.7 Viết truy vấn đối soát tồn hàng serial (D13) và helper `AssertSerialStockReconciledAsync` trong project test PostgreSQL; gọi helper sau phần Assert của mọi test PostgreSQL kho (phiếu nhập 3.6, báo lỗi 4.6, các test `InventoryPostgresTests` hiện có) và thêm một test cố ý làm lệch bằng SQL để helper báo đúng dòng lệch.
- [x] 4.8 Cập nhật `docs/API_PERMISSIONS.md` (route nhà cung cấp, phiếu nhập, báo lỗi/đưa về kho, chính sách giá vốn serial) và `backend/Inventory/README.md` (luồng phiếu nhập, vòng đời máy lỗi, thứ tự khóa dùng chung cho POS, ngữ nghĩa `clientRequestId`, giới hạn phiếu không sửa được, máy mất dùng `Defective`, lỗi seed dự kiến sau khi thao tác trên sản phẩm demo, bảng các đường ghi giữ bất biến, câu SQL đối soát và cách chạy bằng `psql`); kiểm tra mọi route mới đều có trong bảng quyền và câu SQL chạy được trên database demo, không trả dòng nào.

## 5. Client: nhà cung cấp

- [x] 5.1 Thêm model `SupplierItem` và `ISupplierRepository`/`SupplierRepository` (map DTO sang model, failure kind kèm lỗi theo field) theo design D10; headless tests cho query string, mapping và 400/401/403/404/409/network.
- [x] 5.2 Thêm `SupplierListViewModel` và `SupplierEditorDialogViewModel` (tìm kiếm, lọc trạng thái, phân trang, tạo/sửa, ngừng dùng/mở lại, sự kiện theo design D10); headless tests cho trạng thái loading/empty/error, validation, "Tên nhà cung cấp đã tồn tại." khi 409 và làm mới một lần sau khi lưu.
- [x] 5.3 Thêm `SupplierListView` và `SupplierEditorDialog` bằng C# Markup với theme accessors, row "•••" `RowActionsButton`; thêm tab "Nhà cung cấp" vào `CategoryWorkspaceTab`/`CategoryManagementPage`, đăng ký DI; chạy app (`net10.0-desktop`), kiểm tra binding, bàn phím và đổi Sáng/Tối khi tab và dialog đang mở.

## 6. Client: phiếu nhập

- [x] 6.1 Thêm models phiếu nhập và `IPurchaseReceiptRepository`/`PurchaseReceiptRepository` (danh sách, chi tiết, tạo; phân biệt 201 và 200 replay; 409 kèm mã trùng); headless tests cho mapping và lỗi.
- [x] 6.2 Thêm `PurchaseReceiptListViewModel` và `PurchaseReceiptDetailDialogViewModel` (tìm kiếm, lọc NCC, phân trang, thông báo 403 "Bạn không có quyền xem phiếu nhập.", nhãn trạng thái máy tiếng Việt); headless tests cho các trạng thái và chuyển trang.
- [x] 6.3 Thêm `CreatePurchaseReceiptDialogViewModel` và `ReceiptLineDraft` (bộ chọn sản phẩm/biến thể, thêm/xóa dòng, parse tiền `vi-VN`, tách Serial/IMEI, xem trước tổng tiền, validation theo spec, một `ClientRequestId` cho mọi lần gửi, giữ dữ liệu khi lỗi); headless tests cho từng thông báo validation, cùng `ClientRequestId` sau lỗi mạng, hiện mã trùng khi 409 và đóng form kèm số phiếu khi thành công.
- [x] 6.4 Thêm `PurchaseReceiptListView`, `PurchaseReceiptDetailDialog` và `CreatePurchaseReceiptDialog` bằng C# Markup (không đặt picker trong item template; lệnh xóa dòng nhận `object?`); thêm tab "Nhập hàng", đăng ký DI; chạy app và kiểm tra tạo phiếu có/không serial, binding, bàn phím, resize và đổi Sáng/Tối khi form đang mở.
- [x] 6.5 Giữ `ImportStockCommand`/`ActionRequested(InventoryStockAction.Import)` và đổi `CategoryManagementPage.OnInventoryActionRequested` sang mở `CreatePurchaseReceiptDialog` cho cả hai nút "Nhập kho mới" (tab Tồn kho và Serial/IMEI); khi `ReceiptCreated` thì làm mới tab Tồn kho, Serial/IMEI và Nhập hàng mỗi tab một lần; bỏ `ImportStockDialog`, `ImportStockDialogViewModel` và test của chúng, bỏ `InitializeStockAsync`/`BatchImportSerialsAsync` khỏi client `IInventoryRepository` và xóa stub của chúng trong 5 test double còn lại; kiểm tra test `ImportStockCommand` phát `Import` hiện có vẫn qua, thêm headless test làm mới đúng một lần và đóng form không gửi request; chạy `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`.
- [x] 6.6 Thêm client repository riêng `ISerialDeviceRepository`/`SerialDeviceRepository` (`ReportDefectiveAsync`/`RestoreAsync`, không đụng `IInventoryRepository`) và đăng ký DI, `DeviceStatusDialogViewModel` (lý do gợi ý "Hư hỏng"/"Mất" hoặc tự gõ, ghi chú, giữ dữ liệu khi lỗi) và hai lệnh nhận `object?` trong `SerialImeiViewModel`; headless tests cho mục menu bật/tắt theo trạng thái, nút xác nhận tắt khi lý do trống, tải lại đúng một lần khi thành công và map lỗi 400/403/404/409/network.
- [x] 6.7 Thêm "•••" `RowActionsButton` với "Báo lỗi máy" và "Sửa xong, đưa về kho" cho mỗi dòng `SerialImeiPage`, cùng `DeviceStatusDialog` bằng C# Markup; thêm nhãn `Defective` → "Báo lỗi máy" và `OpeningBalance` → "Tồn đầu kỳ" vào bộ chuyển nhãn của `StockDetailHistoryDialog`; chạy app, báo lỗi một máy rồi đưa về kho, kiểm tra trạng thái dòng, tồn ở tab Tồn kho, lịch sử tồn, bàn phím và đổi Sáng/Tối khi dialog đang mở.
- [x] 6.8 Ghi các sự kiện ViewModel → Page mới vào danh sách ngoại lệ trong `AGENTS.md` và các view đã chuyển sang C# Markup; kiểm tra `git grep` không có `Click +=`, `TextChanged +=` hay `Color.FromArgb` trong các file view mới.

## 7. Kiểm tra tích hợp

- [x] 7.1 Chạy `dotnet build TechStore.sln` và `dotnet test TechStore.sln` với `TECHSTORE_TEST_CONNECTION` trỏ tới một database PostgreSQL test riêng (thiếu biến này thì mọi `[PostgresFact]` bị Skip, không được tính là đạt); ghi số Pass/Fail/Skipped và xác nhận không có test bị bỏ qua ngoài các ca chủ đích.
- [x] 7.2 Chạy backend và client thật bằng tài khoản warehouse demo: tạo nhà cung cấp, nhập một phiếu phụ kiện và một phiếu IMEI, kiểm tra tab Tồn kho, lịch sử tồn, Serial/IMEI và chi tiết phiếu khớp nhau; báo lỗi một máy, kiểm tra POS không bán được máy đó, rồi đưa về kho và kiểm tra lại; cuối buổi chạy truy vấn đối soát và xác nhận không có dòng lệch; thử bấm xác nhận hai lần khi backend chậm hoặc tắt rồi mở lại, và đăng nhập Sales để kiểm tra bị chặn; khởi động lại backend sau khi đã nhập hàng hoặc báo lỗi máy của sản phẩm `DEMO-` và xác nhận log chỉ có lỗi "Demo seed conflict" đã ghi trong rủi ro, app vẫn chạy và dữ liệu không đổi; ghi kết quả và ảnh hoặc log vào báo cáo kiểm chứng của change.

## 8. Sửa sau rà soát triển khai

- [x] 8.1 Gán trực tiếp lệnh "Xóa dòng" trong `CreatePurchaseReceiptDialog`; thêm test `RemoveLineCommand`.
- [x] 8.2 Cho các ô nhập của form phiếu nhập, dialog báo lỗi máy và dialog nhà cung cấp cập nhật ngay khi gõ (`UpdateSourceTrigger.PropertyChanged`); đổi số lượng và bảo hành sang chuỗi được kiểm tra (1–100.000; 0–120); thay `ComboBox` lý do bằng nút gợi ý cộng ô nhập; thêm test.
- [x] 8.3 Hiển thị lỗi 400 theo dòng bằng tiếng Việt, cùng các thông báo 401/403/404/mạng và danh sách mã trùng khi 409; thêm test.
- [x] 8.4 Tắt "Thêm dòng" khi biến thể đang tải, bỏ qua kết quả tải biến thể cũ; tải đủ mọi trang sản phẩm và nhà cung cấp (form và bộ lọc); thêm test.
- [x] 8.5 Bỏ route phụ `/api/suppliers` và `/api/purchase-receipts`; client, test và tài liệu dùng `/api/inventory/...`.
- [x] 8.6 Chuyển việc tải tab lần đầu và làm mới sau khi ghi phiếu hoặc đổi trạng thái máy vào `CategoryManagementViewModel`, việc đổi trạng thái nhà cung cấp vào `SupplierListViewModel`; bỏ `PropertyChanged +=` khỏi page; thay test tự gọi callback bằng test gọi ViewModel thật.
- [x] 8.7 Từ chối phiếu có tổng tiền vượt `numeric(18,2)` bằng 400; thêm test.
- [x] 8.8 Trả tên sản phẩm và biến thể trong phản hồi báo lỗi/đưa về kho, dùng nhãn trạng thái tiếng Việt trong thông báo 409; thêm test.
- [x] 8.9 Dùng `LockBarrier` trong hai test PostgreSQL chạy song song (cùng `clientRequestId`, cùng IMEI) để thật sự đi qua nhánh trùng khóa.
- [x] 8.10 Bỏ cài đặt mặc định ném lỗi của `IApiClient.PostWithStatusAsync` và cài nó trong mọi fake; thêm dự phòng mở dialog trực tiếp cho ba dialog mới; cập nhật bảng view trong `AGENTS.md` và design D10/D12.
- [x] 8.11 Chạy app thật và kiểm tra: xóa dòng, số mã cập nhật khi dán IMEI, nút xác nhận bật khi gõ lý do, thông báo lỗi 400 theo dòng, đổi Sáng/Tối khi form đang mở. Đã chạy backend và client trên Xvfb với tài khoản `warehouse1` (2026-11-17): cả năm mục đạt, không ghi dữ liệu. Dialog là modal nên không đổi theme được khi form đang mở; đã đổi Sáng/Tối trong Cài đặt rồi mở lại form, và trang đã dựng sẵn cũng tự đổi màu.
- [x] 8.12 Sửa các lỗi tìm thấy khi chạy app: huy hiệu và tiêu đề của tab "Nhập hàng"/"Nhà cung cấp" và nút "Tạo phiếu nhập" trên đầu trang bind qua field riêng nên trỏ nhầm vào ViewModel danh mục (đổi sang `ViewModel.Receipts`/`ViewModel.Suppliers`); bộ lọc nhà cung cấp trống khi mở tab (thêm placeholder "Nhà cung cấp: Tất cả"); thêm placeholder cho hai ComboBox của form; câu lỗi giá nhập của `UnitCostRules` chuyển sang tiếng Việt; form chặn giá nhập trên 1.000.000.000.000 ₫ trước khi gửi.

## 9. Sửa sau rà soát lần 3

- [x] 9.1 Client repository chỉ trả `detail` của server; các ViewModel phiếu nhập, nhà cung cấp và báo lỗi máy chọn câu tiếng Việt theo loại lỗi (401, 403, 404, 400, mạng); thêm test.
- [x] 9.2 Thêm `InventoryPermissions`; ẩn và chặn các thao tác ghi với vai trò không được phép (Manager chỉ xem, SalesStaff không xem phiếu nhập/nhà cung cấp); chỉ tải trước hai tab này cho vai trò được xem; thêm test theo từng vai trò.
- [x] 9.3 Lỗi 409 khi lưu nhà cung cấp hiện đúng lý do của server (trùng tên hoặc tên hệ thống); nhãn trạng thái đổi thành "Đang sử dụng"/"Ngừng sử dụng" theo spec.
- [x] 9.4 Bỏ việc tự tải ở `Loaded` của `PurchaseReceiptListView`/`SupplierListView` (ViewModel quyết định); bỏ code thừa trong `PurchaseReceiptDetailDialog`.
- [x] 9.5 Backend trả 400 cho phần tử `null` trong `lines`/`devices`; danh sách phiếu sắp thêm theo `Id` để phân trang ổn định; thêm test.
- [x] 9.6 Client báo rõ khi server trả phiếu đã ghi ở lần gửi trước (replay); thêm test.
- [x] 9.8 Tab "Nhà cung cấp" ẩn nút "Thử lại" khi bị từ chối quyền (403), giống tab "Nhập hàng"; thêm kiểm tra trong test.
- [x] 9.7 Cập nhật `backend/Inventory/README.md` (phiếu nhập một bước, controller, service) và bước nhập hàng trong `docs/DEMO_SCRIPT_MIDTERM.md`.

## Workflow follow-up

- Báo nhóm (người tổng hợp bàn giao và người làm POS) về thứ tự khóa dùng chung và việc cố ý không làm nhận một phần.
- Archive change bằng `/opsx:archive` sau khi review xong.
