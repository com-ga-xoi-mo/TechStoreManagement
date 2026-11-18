# Design

## Context

Dashboard Manager và khoảng ngày `custom` đã được kiểm chứng trong `m3-manager-dashboard-access`. `m3-demo-data-reconciliation` đã đối soát KPI bằng fixture cố định. POS Cash và ca đã có trong `backend/Sales/PosController.cs`. `PosCheckoutPostgresHttpTests` đã chạy các bước mở ca, bán Cash, đóng ca và kiểm chứng kho qua HTTP thật trên PostgreSQL. Fixture này dùng `IdentityPostgresApi` để tạo database test riêng và đăng nhập thật.

Baseline để apply là nhánh của Bình sau khi đã commit hai change `m3-manager-dashboard-access`, `m3-demo-data-reconciliation` và merge `origin/dev` mới nhất. Bảng dưới mô tả trạng thái của `origin/dev` lúc khảo sát. Design không ghi SHA cố định vì các nhánh còn thay đổi; khi apply, ghi SHA của build thực chạy và kiểm tra lại từng phụ thuộc trên build đó.

| Bước | Trạng thái trên `origin/dev` lúc khảo sát | Bàn giao / xử lý |
| --- | --- | --- |
| Manager đọc Dashboard | Có sau khi nhánh của Bình (hai change trước) được merge cùng `dev` | Bình giữ contract `admin-analytics-api` và ghép cùng build với POS. Dùng lại bằng chứng quyền ở change trước. |
| Nhà cung cấp, phiếu nhập, nhận IMEI | Đang chặn luồng đầu-cuối. `dev` chưa có controller nhập. Nhánh owner `origin/feat/inventory-purchase-receipts` có `SuppliersController` và `PurchaseReceiptsController`; `POST /api/inventory/purchase-receipts` tạo phiếu `Received` và ghi nhập kho trong cùng giao dịch. | Q. Vinh merge receiving vào `dev` kèm migration và contract nhận hàng, gồm chứng từ, IMEI, giá vốn và nguồn của dòng lịch sử. Bình chỉ dùng contract trên build đã merge, không lấy code từ nhánh chưa merge. |
| Tra SKU/IMEI, mở ca, checkout, đóng ca | Có API tìm sản phẩm, tra serial và các route POS. Checkout nhận `customerId` tùy chọn. | T. Vinh phụ trách tra cứu sản phẩm/UI tìm hàng. Đạt phụ trách POS/ca, nối kết quả tra cứu, khách và ghi đơn. Q. Vinh phụ trách khả dụng, tồn, serial, lịch sử và bảo hành. |
| Tìm/chọn khách qua Sales | Có GET Customers cho Sales từ Vy. POS desktop chưa có bước chọn khách. | Dùng API của Vy để Sales tra và chọn khách khi chạy HTTP. Đạt bàn giao tích hợp bộ chọn của Vy vào POS để nghiệm thu thao tác chọn khách trên desktop. |
| Thuế và giảm giá | Có công thức của Vy trong `OrderService`. `PosCheckoutRequest` và bước nối của POS chưa nhận/truyền giảm giá cấp đơn. | Kiểm tra VAT theo cấu hình và tổng lưu trên build đã merge. Ca giảm cấp đơn khác 0 trên POS chờ Đạt nối checkout với contract Vy. Vy xác nhận công thức và số mong đợi theo cấu hình test. |

## Goals / Non-Goals

**Goals**

- Kiểm chứng bằng HTTP thật toàn bộ thay đổi từ trước giao dịch đến sau commit hoặc rollback, với số đối chiếu độc lập lấy từ dữ liệu đã lưu.
- Có thể chạy ca POS và Dashboard từ fixture trên baseline. Tiêu chí hoàn tất luồng đầu-cuối vẫn yêu cầu chứng từ nhập thật và bán đúng IMEI vừa nhận trên cùng build.

**Non-Goals**

- Thay API, phân quyền, tính tiền, nghiệp vụ receiving, POS, ca, kho hoặc tính năng UI của owner. Không lặp ma trận bốn role, kiểm thử race hay đối soát toàn bộ fixture cố định.
- Sửa file của owner khác, kể cả file test và helper test. Khi test lộ lỗi ở phần của owner, ghi lỗi và chuyển owner theo D4; không sửa code hoặc nới assert để test pass.

## Decisions

### D1. Một database riêng cho mỗi lượt kiểm chứng

Dùng PostgreSQL test được migrate trên cùng build đang nghiệm thu. Tạo lớp test riêng `DashboardLiveFlowPostgresHttpTests` trong `tests/Backend.UnitTests/`, không sửa `PosCheckoutPostgresHttpTests` của Đạt. Gọi lại `IdentityPostgresApi`, cấu hình opt-in `TECHSTORE_TEST_CONNECTION` và database sinh riêng của test mà không sửa các helper đó. Phần còn thiếu, như thay clock của ca/checkout hay bọc thời điểm snapshot Analytics trong DI test, đặt trong lớp test mới hoặc helper mới thuộc Bình. Kiểm tra HTTP thật qua API host/DI production. Dùng Kestrel và database demo riêng cho lượt desktop. Chỉ đọc DB test với context mới cho các khẳng định về dữ liệu đã commit, rollback và trường không có trên DTO. Xử lý nghiệp vụ qua endpoint công khai.

Lượt baseline dùng thiết bị fixture `InStock` với đúng product/variant và stock khả dụng. Khách hàng được tra qua GET Customers bằng tài khoản Sales trên build đã merge `dev`. Lượt đầu-cuối bắt buộc tra/tạo nhà cung cấp theo contract bàn giao, ghi nhận phiếu, đối chiếu receipt/IMEI và tra khách qua Sales trên cùng build. Các dòng dùng fixture và nhận qua API được ghi riêng trong bàn giao để biết đã kiểm chứng đầu vào nào.

### D2. Ca thành công và các lần đọc

1. Manager đăng nhập `manager1` và gọi `GET /api/analytics/dashboard` trước khi nhận hàng. Ghi `period`, biên ngày, `timeZone`, `asOf`, summary, daily, recent orders và low stock. Dùng cùng kỳ `custom` với ngày Việt Nam của giao dịch và ngày liền trước. Chạy tuần tự trên database riêng để mỗi delta có thể gắn với một giao dịch.
2. Khi receiving có trong build, dùng tài khoản kho nhận IMEI test. Kiểm tra mã phiếu, người nhận, liên kết IMEI với nguồn nhập, giá vốn, kho tăng và dòng `PurchaseReceipt`. Lưu số tồn và giữ chỗ sau nhận và trước checkout, vì nhận hàng có thể làm thay đổi điểm xuất phát của tồn thấp.
3. `cashier1` mở ca qua `POST /api/pos/shifts`. Dùng `GET /api/products?q=...` và `GET /api/inventory/serials?search=...` để nhận diện đúng SKU/product/variant–IMEI. Tìm khách test và lấy `customerId` qua GET Customers trên build có quyền tìm khách. Gửi `POST /api/pos/checkout` với `posSessionId`, `checkoutKey` mới, `customerId`, `cashTendered` đủ theo cấu hình và một dòng số lượng 1 có `serialImeiId` đúng. Lưu HTTP 201, ID, mã đơn, dòng, và đọc lại đơn qua `GET /api/orders/{id}`. Lấy `TotalAmount` và timestamps đã lưu bằng context mới để kiểm chứng response.
4. Kiểm tra đơn `Completed`, `Cash`, `PaidAt = CompletedAt` có giá trị, đúng khách, người bán, ca và IMEI. Với stock đã chụp ngay trước bán, sau commit `Quantity` giảm 1, `ReservedQuantity` trở về mức ban đầu. Đối chiếu IMEI `Sold`, kỳ bảo hành theo `WarrantyMonths`, giá vốn snapshot và đúng một dòng `Sale` có lượng −1, số tồn sau, actor và khóa nguồn order item. Giá vốn và lịch sử đọc bổ sung từ DB test nếu DTO thiếu trường.
5. Đóng ca qua `POST /api/pos/shifts/{id}/close` với tiền đếm theo ca chỉ có một giao dịch Cash. Kiểm tra `CashSalesTotal` tăng bằng tổng đơn, tiền thừa bằng tiền nhận trừ tổng đơn, tiền dự kiến bằng tiền đầu ca cộng tổng Cash đã ghi, tiền đếm khớp và chênh lệch 0. Kiểm tra ca `Closed`, actor và thời điểm đóng.
6. Manager đọc lại cùng kỳ sau khi giao dịch đã commit. `asOf` phải sau `CreatedAt` và `CompletedAt`. So với trước giao dịch, `OrderCount` tăng đúng 1 theo ngày UTC+07 của `CreatedAt`, doanh thu tăng đúng `TotalAmount` theo ngày của `CompletedAt`, chỉ bucket ngày đó tăng và tổng daily bằng summary. Đơn mới xuất hiện đúng một lần trong tối đa 5 dòng mới nhất, đối chiếu ID, mã, trạng thái và tổng tiền. Nếu tồn thấp thay đổi do nhập/bán, so theo từng stock balance thực tế, ngưỡng dựa trên `Quantity`.

Ở regression test, chọn thời điểm checkout sau 17:00 UTC để ngày UTC và UTC+07 khác nhau. Dùng DI test-only cho clock của ca/checkout và thời điểm đọc snapshot Analytics, giữ auth/JWT thật và giữ toàn bộ logic truy vấn và ghi của production. Có hai mốc snapshot nằm trước giao dịch và sau commit để tránh rơi đúng biên loại trừ `asOf`. Lượt Kestrel/desktop dùng giờ thật. Nếu vượt biên kỳ trong lượt live, tạo lại DB demo và chạy từ baseline với kỳ bao đúng ngày Việt Nam cần đối soát.

### D3. Ca lỗi Sold có thể chạy lại

Dùng một lượt độc lập có ca Cash còn mở. Bán một thiết bị qua checkout thành công để tạo trạng thái `Sold` xác định. Sau đó chụp ca, kho, serial/bảo hành, ledger, các đơn liên quan và Dashboard. Gửi checkout cùng IMEI với `checkoutKey` mới, tiền đủ, và cùng ca mở. Với precondition hợp lệ, mong đợi HTTP 409 vì IMEI đã bán. Dùng key mới để đi vào kiểm tra khả dụng theo contract checkout.

Sau 409, đọc DB bằng context mới và chụp lại Dashboard cùng kỳ. Khẳng định key lỗi không có order/line, không có thêm movement, các balance và trạng thái/giá trị bảo hành giữ nguyên, tiền và trạng thái ca giữ nguyên. KPI, daily, recent orders và low-stock giữ nguyên khi so các trường dữ liệu. `asOf` là metadata của lần đọc mới. Ghi rõ đây là xung đột tuần tự.

### D4. Bằng chứng và chuyển lỗi

Tạo [`docs/M3_TEST_SUMMARY.md`](../../../../docs/M3_TEST_SUMMARY.md#đối-soát-giao-dịch-pos-live) và liên kết từ handoff M3. Mỗi ca có build SHA API/client (hoặc SHA nền kèm diff/hash artifact nếu build có sửa đổi cục bộ), mốc UTC/UTC+07, mã fixture hoặc số chứng từ, SKU/IMEI test, ID đơn/ca, endpoint, đầu vào, Expected/Actual, status và đường dẫn log đã lọc thông tin xác thực. Ghi Pass/Fail/Skipped/Chưa kiểm tra theo từng mục. Mục đang chờ bàn giao ghi bước cụ thể, owner, phụ thuộc và điều kiện chạy lại.

API lưu mã HTTP và dữ liệu đối chiếu read-back. Desktop chạy với preview tắt trên build ghép, ghi nền tảng/target, thao tác nhận hàng nếu UI đã bàn giao, mở ca, quét máy, chọn khách, thu tiền, đóng ca và chuyển sang Manager mở lại đúng kỳ. Ghi ảnh hoặc log thao tác và số liệu hiển thị, gắn mã đơn để đối chiếu với API/DB. Các bước UI chưa có ghi riêng theo owner. Bằng chứng API vẫn được lưu cho bước đó. Ca 409 xác định được chạy qua HTTP, vì UI có thể chặn IMEI Sold ở bước scan.

Chuyển sai lệch lifecycle đơn/ca và checkout cho Đạt, nhận hàng/IMEI/tồn/ledger/bảo hành/giá vốn cho Q. Vinh, khách hàng/công thức cho Vy, tra cứu Catalog/UI tìm hàng cho T. Vinh. Bình ghi bằng chứng Delta Dashboard, tổng hợp lỗi và chạy lại trên SHA đã sửa. Chỉ đối chiếu công thức/giảm cấp đơn theo contract đang có trên build. Phần chưa kết nối ghi đúng phạm vi chờ và người chịu trách nhiệm.

## Risks / Trade-offs

- [Nhận hàng đã có code ở nhánh riêng] → Chỉ đóng mục nhận hàng khi cùng build thực hiện POST và liên kết phiếu nhập tới IMEI đã bán. Fixture giúp tiến hành phần POS/Dashboard trong thời gian chờ.
- [Receiving chưa merge trước hạn Sprint 5] → Không giữ change mở vô thời hạn vì `m3-test-handoff-and-acceptance` cần kết quả của change này. Đến hạn mà `dev` vẫn chưa có receiving thì ghi các mục nhận hàng (task 2.1 và bước nhận hàng ở 3.1) là "Chưa kiểm tra – chờ Q. Vinh", kèm điều kiện chạy lại, rồi archive. Chạy bổ sung sau khi receiving merge và cập nhật bằng chứng trong cùng tài liệu.
- [Lần đọc cắt qua biên giờ] → So cùng biên kỳ, ghi `asOf`, kiểm tra mốc nằm trong kỳ và chỉ một checkout đã commit trong lượt. Dùng test neo giờ cho regression UTC+07.
- [Build API và desktop khác nhau] → Ghi SHA cho từng loại bằng chứng. Thiếu UI chọn khách, UI nhận hàng hoặc tích hợp giảm cấp đơn thì ghi rõ từng mục còn chờ duyệt, dù API checkout cơ bản đã pass.

## Migration Plan

Dùng migrations của build owner đã ghép khi tạo DB test/demo. Dùng fixture dọn database riêng do lượt kiểm chứng tạo. Change này chỉ cần chạy lại test và cập nhật hồ sơ bàn giao sau khi tích hợp.
