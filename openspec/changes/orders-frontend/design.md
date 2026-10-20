## Context

Backend Orders API đã có và đã kiểm chứng trên PostgreSQL thật (spec `order-management`, `inventory-fulfillment`). Phía desktop mới có khung: `PageViewModel` (trạng thái tải, hủy theo vòng đời trang), `IApiClient` (GET/POST/PUT), mẫu repository của Catalog (`ProductRepository` kiểm tra hình dạng JSON rồi ánh xạ sang model client), điều hướng theo `AppRoute`, `IDialogService` (hộp thoại xác nhận chỉ có chữ), và nền C# Markup (`AppColors`/`AppTypography`/`AppControlStyles`, `StatusBadge`, `SearchBoxControl`, `EmptyStateControl`).

Hiện trạng phía Orders trên `dev` (7116a71):
- `OrderListViewModel` chỉ có 5 dòng, `OrderListPage.xaml` là placeholder; đã đăng ký DI và nằm trong `ShellRoutePageFactory`.
- Orders API trả `status`, `paymentMethod` là chuỗi tên enum; tiền là `decimal`; lỗi validate trả ProblemDetails có `errors` theo khóa như `items[0].serialImeiId`; lỗi nghiệp vụ trả 409 kèm `detail`.
- `IApiClient` không có PATCH; endpoint đổi trạng thái là `PATCH /api/orders/{id}/status`.
- Chưa có API/UI Khách hàng (Dev 5) và chưa có repository client cho Inventory (Dev 3).
- Test headless của client (`tests/Frontend.UnitTests`) tự liên kết mã nguồn bằng glob: `Models/**`, `Repositories/**`, `ViewModels/**`, `Services/Api|Dialog|Navigation/**`, `Controls/Presentation/**`. Mã nằm ngoài các glob này (đặc biệt `Views/**`) không test được.

Ràng buộc từ AGENTS.md: ViewModel không tham chiếu control WinUI; View C# Markup chỉ khai báo cây và binding; màu chủ đề đặt trực tiếp lên phần tử bằng `AppColors.Brushes.*`; không dùng `StitchTokens`/`Color.FromArgb` cho giao diện có chủ đề; không `+=` sự kiện để đồng bộ dữ liệu.

## Goals / Non-Goals

**Goals:**
- Danh sách đơn thật, có lọc/tìm/sắp xếp/phân trang, đổi trạng thái đúng luật server.
- Lập đơn tại quầy từ giao diện, với máy có IMEI phải chọn đúng máy `InStock`, tổng tiền ước tính theo thời gian thực.
- Xem chi tiết đơn kèm IMEI/serial từng dòng.
- Toàn bộ logic nằm trong repository, ViewModel và helper thuần để test headless; View mỏng.
- Xóa `OrderListPage.xaml` và `.xaml.cs` (yêu cầu chuyển C# Markup của Sprint 2).

**Non-Goals:**
- Chọn khách hàng (chờ API/UI Dev 5); đơn lập ở đây là khách lẻ. Request vẫn có `customerId` nên thêm bộ chọn sau chỉ là thêm.
- Lọc theo khoảng ngày (`from`/`to`), hoàn trả/hoàn tiền, in hóa đơn, voucher, thuế, xác minh thanh toán VietQR/thẻ.
- Đổi backend, `shared/`, hay xây repository Inventory đầy đủ (Dev 3 làm riêng).
- Đăng nhập bắt buộc cho Orders (API hiện chưa bắt token).

## Decisions

### D1. Repository theo mẫu Catalog, model client tách khỏi DTO
`IOrderRepository` (`SearchAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateStatusAsync`) nhận `JsonElement`, kiểm tra hình dạng và giá trị (id khác rỗng, tiền không âm, `status`/`paymentMethod` thuộc enum đã biết, `totalPages` khớp `totalCount`/`pageSize`) rồi ánh xạ sang record `OrderSummary`, `OrderDetail`, `OrderLine`, `OrderPage`. Trạng thái và phương thức thanh toán chuyển sang enum dùng chung `OrderStatus`/`PaymentMethod` của `shared/Enums` (client đã tham chiếu `shared/`; đủ 6 trạng thái của server, kể cả `PartiallyRefunded`, `Refunded`); giá trị lạ là `ApiErrorKind.InvalidResponse`, không đoán.
- `GetByIdAsync` trả `null` khi 404 (như `ProductRepository`).
- `CreateAsync` và `UpdateStatusAsync` để `ApiException` (400/409) đi lên ViewModel, vì ViewModel cần `FieldErrors` và `Detail`.
- Alternative: dùng thẳng DTO trong ViewModel. Bỏ vì làm ViewModel phụ thuộc hình dạng truyền tải và mất chỗ kiểm tra dữ liệu; mẫu Catalog đã chọn cách tách.

### D2. Máy `InStock` lấy qua repository riêng, hẹp
`IOrderDeviceRepository.GetInStockAsync(productId, variantId?)` gọi `GET /api/inventory/serials?status=InStock&productId=..&variantId=..&pageSize=100` và chỉ giữ `AvailableDevice(Id, Imei, SerialNumber)`. Giá vốn (`unitCost`) có trong `SerialImeiDto` nhưng bị bỏ ngay ở lớp ánh xạ, vì README Orders ghi rõ giá vốn không lộ ra màn hình bán.
- Giới hạn 100 máy mỗi lần; nếu `totalCount` lớn hơn thì hiển thị "chỉ hiện 100 máy đầu" thay vì giả vờ đủ.
- Alternative: dùng chung repository Inventory của Dev 3. Bỏ vì chưa có và sẽ buộc hai người chờ nhau; interface hẹp này thay được bằng bản của Dev 3 sau, ViewModel không đổi.

### D3. Danh sách: tiêu chí gửi theo nút, giống Catalog
`OrderListViewModel` giữ nháp (`SearchText`, `StatusFilter`, `SelectedSort`, `SelectedPageSize`) tách khỏi tiêu chí đã áp dụng (`OrderQuery`); chỉ nút Tìm hoặc Enter mới áp dụng và đưa về trang 1. Dùng `RunLatestRequestAsync` của `PageViewModel` để request cũ bị hủy khi có request mới. Bộ lọc trạng thái là chọn một ("Tất cả" + 6 trạng thái), sort chỉ `createdAt` và `totalAmount` (asc/desc) đúng bộ server cho phép.
- Từ khóa chỉ áp dụng khi gửi; trạng thái, sắp xếp, kích thước trang áp dụng ngay (giống Catalog) và giữ từ khóa đã áp dụng.
- ViewModel dùng `ManagesLoadState => true` như Catalog: hành vi mặc định của `PageViewModel` ghi đè trạng thái Lỗi bằng Rỗng khi lần tải đầu thất bại, nên ViewModel tự đặt Đang tải/Nội dung/Rỗng/Lỗi (lỗi qua `OnLatestRequestFailed`). Hệ quả tốt: hàng cũ vẫn còn trong lúc tải lại.
- Trang vượt biên (xóa đơn khiến `page > totalPages`): quay về trang 1 một lần, không lặp.

### D4. Nút chuyển trạng thái nằm ở hàng danh sách; chi tiết là hộp thoại chỉ đọc
Hành động hợp lệ suy ra từ trạng thái bằng helper thuần `OrderStatusActions.For(status)`:

| Trạng thái | Hành động hiển thị |
|---|---|
| Pending | Xác nhận thanh toán (→ Processing), Hoàn tất (→ Completed), Hủy đơn (→ Cancelled) |
| Processing | Hoàn tất (→ Completed) |
| Còn lại | không có |

Mỗi hành động qua `IDialogService.ConfirmAsync` trước khi gọi `PATCH`. Sau khi thành công tải lại trang hiện tại; khi 409 hiện `detail` của server trong banner rồi cũng tải lại (trạng thái đã cũ). Server vẫn là nơi quyết định tính hợp lệ; helper chỉ để ẩn nút thừa.
- `OrderDetailDialog` chỉ đọc, không chứa nút đổi trạng thái. Lý do: `ContentDialog` của WinUI chỉ mở được một cái tại một thời điểm, nếu hành động nằm trong dialog thì hộp xác nhận sẽ là dialog thứ hai.
- Alternative: nút hành động trong dialog chi tiết. Bỏ vì xung đột modal như trên.

### D5. Hộp thoại chi tiết qua presenter riêng, không sửa `IDialogService`
`IDialogService`/`DialogRequest` chỉ có tiêu đề và chữ. Thêm `IOrderDetailPresenter.ShowAsync(OrderDetail, CancellationToken)` trong `Services/Dialog/` (độc lập UI, nằm trong glob test); bản cài đặt `OrderDetailDialogPresenter` ở `Views/Orders/` dựng `OrderDetailDialog : ContentDialog` bằng C# Markup và mở trên XAML root của Shell. Root lấy từ `ContentDialogPresenter` qua một thuộc tính chỉ đọc mới (thay đổi duy nhất ở tệp dùng chung này). Presenter bỏ qua yêu cầu nếu đang có dialog khác mở và trả `Dismissed`.
- Alternative: mở rộng `DialogRequest` có nội dung tùy ý. Bỏ vì phải sửa hợp đồng và mọi bản giả `IDialogService` trong test của người khác.

### D6. Lập đơn: một ViewModel, dòng nháp là record bất biến
`OrderCreateViewModel` giữ `Lines` (danh sách `OrderDraftLine`) và thuộc tính tính toán `EstimatedTotal`. Quy tắc phản chiếu server để báo lỗi sớm, nhưng server vẫn quyết định:
- Sản phẩm có biến thể thì bắt buộc chọn biến thể đang hoạt động; đơn giá = giá biến thể, nếu không có biến thể thì `basePrice`.
- Sản phẩm theo dõi IMEI (`isSerialTracked`): mỗi dòng đúng một máy, số lượng cố định 1, mỗi máy chỉ xuất hiện một lần trong đơn; muốn bán 3 máy thì thêm 3 dòng.
- Sản phẩm không theo dõi IMEI: số lượng nguyên dương, tối đa 10.000; giảm giá dòng từ 0 đến thành tiền dòng. Thêm lại cùng sản phẩm/biến thể thì gộp vào dòng cũ (server từ chối hai dòng trùng).
- Dòng đã thêm không sửa tại chỗ: muốn đổi thì xóa và thêm lại. Nhờ vậy giá trị sai bị chặn ngay ở khu vực thêm dòng, còn lỗi từ server mới gắn vào từng dòng.
- Sau khi đơn đã được tạo (kể cả khi bước hoàn tất lỗi), nháp bị khóa: không thêm, xóa hay gửi lại.
- Tổng tạm tính = Σ(đơn giá × số lượng − giảm giá) bằng `decimal`, hiển thị nhãn "Tạm tính"; sau khi tạo, hiển thị tổng server trả về. Thuế và giảm giá cấp đơn bằng 0 như server ở Milestone 2.
- Tìm sản phẩm dùng `IProductRepository.SearchAsync` (đọc chéo module, hợp lệ theo AGENTS) với `isActive=true`, lấy chi tiết bằng `GetByIdAsync` để có biến thể và `isSerialTracked`. Tồn kho sản phẩm không theo dõi IMEI không tra trước; thiếu hàng sẽ do server trả 400 dưới `items[i].quantity` và hiện đúng dòng đó.

### D7. Hai hành động gửi đơn, không tự gửi lại
- "Lưu đơn nháp": `POST /api/orders`, kết quả `Pending`, quay về danh sách.
- "Thanh toán & hoàn tất": `POST` rồi `PATCH {targetStatus: Completed}` (server cho phép `Pending → Completed` trong một transaction).
Hai lời gọi là hai bước riêng nên có trường hợp một nửa thành công: nếu `POST` xong mà `PATCH` lỗi (ví dụ máy vừa bị đơn khác chốt, 409), đơn vẫn tồn tại ở `Pending`. ViewModel khi đó khóa nút gửi (chống tạo trùng), hiển thị "Đơn {mã} đã tạo nhưng chưa hoàn tất: {lý do}" và nút "Về danh sách đơn". Không tự retry bất kỳ ghi nào (đúng spec `frontend-api-access`).
- Lỗi 400 của `POST`: khóa `items[n].<field>` gắn vào dòng n, khóa khác vào banner chung; chỉ chữ số trong dấu ngoặc được dùng làm chỉ số, nếu không parse được thì vào banner chung.
- Phương thức thanh toán gửi dưới dạng tên enum (`Cash`, `VietQr`, `Card`). VietQR/thẻ chỉ ghi nhận lựa chọn, chưa xác minh giao dịch.

### D8. Điều hướng: thêm một loại route, giữ mục Đơn hàng được chọn
Thêm `AppRouteKind.OrderCreate` thuộc `ShellDestination.Orders`. Hàm tạo `AppRoute` hiện chỉ cho phép loại khác `Root` nằm dưới `Products`; đổi thành bảng ghép loại ↔ destination. `ShellRoutePageFactory.ResolvePage` thêm một nhánh. Quay lại danh sách dùng `AppRoute.Root(Orders)`; vì cùng destination nên thanh điều hướng không đổi mục chọn.
- Alternative: lập đơn trong dialog. Bỏ vì form nhiều dòng, tìm sản phẩm, chọn IMEI không vừa dialog.

### D9. `PatchAsync` mở rộng `IApiClient`
Thêm `PatchAsync<TRequest, TResponse>` cùng đường đi với `PutAsync` (cùng kiểm tra origin, timeout, chuẩn hóa lỗi, không retry). Việc thêm thành viên vào interface làm 3 stub test riêng lẻ (`StubApiClient`, `JsonApiClient`, `ThrowingApiClient`) không biên dịch; sửa trong cùng change.

### D10. Tiền và trạng thái hiển thị bằng helper thuần
`OrderPresentation` (trong `Controls/Presentation/`, đã được test liên kết) cung cấp: nhãn tiếng Việt và `StatusTone` của trạng thái, nhãn phương thức thanh toán, định dạng tiền `vi-VN` dạng `15.500.000 ₫` bằng `decimal` (không chuyển qua `double`). Tone: Pending = Warning, Processing = Info, Completed = Success, Cancelled = Neutral, PartiallyRefunded = Accent, Refunded = Danger.

### D11. View C# Markup: chỉ khai báo, kiểm chứng khi chạy
`OrderListPage`, `OrderCreatePage`, `OrderDetailDialog` theo mẫu `MarkupReferencePage`: `ViewModel` public, `DataContext = viewModel`, màu đặt trực tiếp bằng `AppColors.Brushes.*`, style từ `AppControlStyles`/`AppTypography`, binding bằng lambda. Đường dẫn binding không được trình biên dịch kiểm tra, nên mỗi trang phải được chạy thật và thử đổi chủ đề Sáng/Tối. Việc đầu tiên khi làm giao diện là dựng một danh sách tối thiểu để xác nhận cách khai báo mẫu danh sách (item template) trong Uno C# Markup chạy được, rồi mới dựng phần còn lại.

## Risks / Trade-offs

- [Tệp dùng chung sửa đồng thời bởi nhiều người: `IApiClient`, `AppRoute`, `ShellRoutePageFactory`, `UiServiceCollectionExtensions`] → chỉ thêm dòng/nhánh, không đổi chữ ký đang có; commit riêng cho từng tệp để dễ giải quyết xung đột; rebase trên `dev` ngay trước khi mở PR (PR #12 của Dev 6 đang mở).
- [Cách khai báo danh sách trong C# Markup chưa có mẫu trong repo] → tác vụ spike đầu tiên cho phần View; nếu `ItemTemplate` lỗi thì dùng `ListView` với template do code dựng, vẫn không viết logic trong View.
- [Hai bước "Thanh toán & hoàn tất" không nguyên tử] → D7: khóa gửi lại, thông báo rõ mã đơn đã tạo, đơn `Pending` hủy được từ danh sách.
- [Danh sách máy `InStock` cắt ở 100] → hiển thị rõ khi bị cắt; sản phẩm thật chỉ có vài chục máy trong dữ liệu seed.
- [Dữ liệu seed thiếu serial cho 3 sản phẩm (DELL-XPS15-9530, MBP14-M3-512, S24U-256) đã ghi ở change `orders-api`] → chọn các sản phẩm này sẽ thấy "không còn máy InStock"; đây là trạng thái đúng, không phải lỗi giao diện. Dev 6 đang mở rộng seeder trong PR #12.
- [Orders API chưa yêu cầu đăng nhập, `OrdersController` còn mã admin cố định] → ngoài phạm vi; khi nhóm bật `[Authorize]`, `BearerTokenHandler` sẽ tự gắn token, không cần đổi mã Orders phía client.
- [Giá ước tính có thể lệch giá server nếu giá đổi giữa lúc chọn và lúc gửi] → hiển thị là "Tạm tính", sau khi tạo luôn dùng số server trả về.

## Open Questions

- Nhóm có muốn thêm bộ chọn khách hàng ngay khi Dev 5 xong API, hay giữ đơn khách lẻ cho hết Sprint 2? (Không chặn change này.)
- Có cần lọc theo khoảng ngày ở danh sách trước Sprint 3 không? Hiện để ngoài phạm vi.
