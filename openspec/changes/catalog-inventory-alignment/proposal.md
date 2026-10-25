# Proposal

## Why

Sau khi gộp nhánh Catalog (PR #16) với nhánh Feature 3 của Dev 3 (`feat/dev3-catalog-inventory-management`), bản review kiến trúc ngày 2026-10-25 tìm ra hai nhóm vấn đề:

1. **Lỗi hành vi thật ở Catalog.** Spec `category-catalog` mới cho `GET /api/categories/tree` trả cả danh mục ẩn. Picker danh mục trong màn soạn sản phẩm không lọc, nên người dùng tạo được sản phẩm trong danh mục ẩn (`POST` không kiểm tra `IsActive`). Ngược lại, `PUT` bắt buộc danh mục active, nên một sản phẩm có danh mục vừa bị ẩn sẽ không sửa được gì nữa, kể cả đổi giá.
2. **Phần Inventory/Category lệch kiến trúc nhóm** đã ghi trong `AGENTS.md`:
   - client gọi HTTP thẳng, bỏ qua `IApiClient`;
   - ViewModel trang không dùng `PageViewModel`;
   - View dùng màu hex cứng và ép giao diện sáng;
   - View dùng event handler bị cấm;
   - service backend bắt lỗi EF/Npgsql trực tiếp;
   - entity chứa dữ liệu hiển thị;
   - API lọc theo tên danh mục.

   Ngoài ra, token màu `DangerText` dùng chung bị đổi sang `#EF4444`, không đạt contrast 4.5:1 cho chữ trên nền trắng.

Phải sửa bây giờ vì cả hai nhánh vừa vào `dev`. Nếu để lâu, các module sau sẽ copy theo cách làm lệch này.

## What Changes

**Catalog: danh mục ẩn**
- Quy tắc: ẩn danh mục chỉ ngăn **đưa thêm** sản phẩm vào danh mục đó. Sản phẩm đang nằm trong danh mục vẫn sửa được bình thường.
- `POST /api/products` từ chối `categoryId` của danh mục ẩn (400 cho `categoryId`).
- `PUT /api/products/{id}` vẫn từ chối việc **chuyển sang** một danh mục ẩn, nhưng **cho phép giữ nguyên** danh mục hiện tại kể cả khi nó đã bị ẩn. Trước đây mọi chỉnh sửa đều bị chặn trong trường hợp này.
- Picker danh mục trong màn soạn sản phẩm chỉ hiện danh mục đang active. Khi sửa một sản phẩm đang thuộc danh mục ẩn, danh mục đó vẫn hiện kèm nhãn "(đang ẩn)" và một dòng thông tin cho biết sản phẩm vẫn lưu được.
- Bộ lọc danh mục ở danh sách sản phẩm vẫn hiện mọi danh mục, nhưng danh mục ẩn có nhãn "(đang ẩn)".

**Inventory API (backend)**
- **BREAKING (API nội bộ):** `GET /api/inventory/stocks` bỏ tham số `categoryName`, thay bằng `categoryId`. `InventoryStockDto` và `InventoryStockDetailDto` thêm `categoryId`. Client trong repo được cập nhật cùng lúc.
- Từ khóa `search` (stock và serial) được escape ký tự `%`, `_`, `!` như Catalog và Customers.
- Lỗi vi phạm check constraint (PostgreSQL `23514`) được dịch trong `UnitOfWork` thành `CheckConstraintViolationException`. Service không còn `using Npgsql`/`Microsoft.EntityFrameworkCore`.
- `InventoryStock.RelatedSerialCount` (`[NotMapped]`) bị bỏ. Repository trả số serial qua read model riêng.
- Khóa và xóa serial chuyển từ `IInventoryStockRepository` sang `ISerialImeiRepository`.
- Spec ghi lại các bộ lọc đã có trong code mà spec còn thiếu (`search`, `inStock`, `outOfStock`; ngưỡng sắp hết tính theo số lượng khả dụng).
- Ba trạng thái tồn (hết, sắp hết, còn) không chồng nhau và cùng tính theo số khả dụng so với `MinStockAlert`. `lowStock=true` không còn trả các dòng đã hết hàng, và client thôi ẩn bớt dòng sau khi nhận từ server, nhờ đó tổng số và phân trang đúng.
- **BREAKING (quyền):** Manager không còn được `DELETE /api/inventory/stocks/{id}`, giống như khởi tạo, nhập và điều chỉnh.

**Category API (backend)**
- Cây danh mục đếm sản phẩm/SKU bằng truy vấn projection, không nạp toàn bộ sản phẩm kèm biến thể. `GetActiveCategoriesAsync` đổi tên thành `GetAllAsync` cho đúng nghĩa.
- Bỏ `RestoreCategory` thừa trong `CategoryService.UpdateAsync`.
- Service Catalog và Inventory lấy thời gian từ `TimeProvider` thay vì `DateTime.UtcNow`.

**Client Inventory/Category**
- Bỏ `CategoryApiClient`, `ICategoryApiClient`, `IAccessTokenProvider`, `SessionAccessTokenProvider` và HttpClient riêng. Các thao tác cây/tạo/sửa/xóa danh mục chuyển vào `Repositories/Catalog/CategoryRepository` qua `IApiClient`.
- `InventoryApiClient` thành `Repositories/Inventory/InventoryRepository`.
- `CategoryManagementViewModel` kế thừa `PageViewModel`, bỏ lớp bọc `CategoryRouteViewModel`. ViewModel của tab tồn kho và Serial/IMEI do ViewModel trang sở hữu và bị hủy theo trang.
- View Category/Inventory và các dialog:
  - bỏ `PropertyChanged +=`, `Click +=` và đoạn tự đặt `Visibility`;
  - bỏ màu hex cứng, `new SolidColorBrush`, `ColorHelper.FromArgb` và `RequestedTheme = Light`;
  - dùng `AppColors`, `AppTypography`, `AppControlStyles` để giao diện đổi theo Sáng/Tối;
  - bỏ font Plus Jakarta riêng, dùng font chung Inter.
- Khôi phục `DangerText` (Light) về `#B91C1C`. Thêm test contrast cho các token chữ.
- Chuyển thư mục `Features/Categories`, `Features/Inventory` về cấu trúc nhóm (`Views/`, `ViewModels/`, `Models/`, `Services/`).
- Cập nhật `AGENTS.md`: bảng trạng thái view (`InventoryPage` đã bị xóa) và mô tả module.

## Capabilities

### New Capabilities
- (không có)

### Modified Capabilities
- `product-catalog`: tạo sản phẩm yêu cầu danh mục tồn tại **và đang active**; sửa sản phẩm được giữ nguyên danh mục hiện tại dù đã bị ẩn, nhưng không được chuyển sang danh mục ẩn.
- `frontend-catalog-management`: picker danh mục trong màn soạn ẩn danh mục ẩn, nhưng giữ danh mục hiện tại của sản phẩm kèm nhãn.
- `frontend-catalog-browsing`: bộ lọc danh mục ở danh sách gắn nhãn "(đang ẩn)" cho danh mục ẩn.
- `inventory-management`: truy vấn tồn kho lọc theo `search`, `categoryId` và ba trạng thái tồn không chồng nhau; DTO có `categoryId`; thêm requirement cho API xóa tồn kho (chỉ Admin và WarehouseStaff).
- `inventory-stock-workspace`: bộ lọc danh mục dùng cây danh mục và gửi `categoryId`; trạng thái hiển thị theo `MinStockAlert` của từng dòng.
- `category-workspace`: trang danh mục/kho và mọi dialog của nó đổi theo theme Sáng/Tối của app.

## Impact

- **Backend:**
  - `backend/Catalog/Services/ProductService.cs`, `CategoryService.cs`, `ProductImageService.cs`;
  - `backend/Catalog/Repositories/CategoryRepository.cs`, `ICategoryRepository.cs`;
  - `backend/Inventory/**` (Service, Repository, Entity, Controller);
  - `backend/Common/Data/UnitOfWork.cs` cùng một exception mới;
  - `backend/Program.cs` nếu cần đăng ký `TimeProvider`.
- **Shared:** `InventoryStockFilterRequest`, `InventoryStockDto`, `InventoryStockDetailDto`.
- **Client:**
  - `frontend/TechStore.Client/Features/**` (chuyển sang thư mục mới);
  - `Services/Api/*Category*`, `Services/Api/*Inventory*`, `Services/Authentication/`;
  - `Repositories/Catalog/CategoryRepository.cs`, `Models/Catalog/CategoryOption.cs`;
  - `ViewModels/Catalog/ProductEditorViewModel.cs`, `ProductListViewModel.cs`, `Views/Catalog/ProductEditorPage.cs`;
  - `ServiceCollectionExtensions.cs`, `UiServiceCollectionExtensions.cs`, `Styles/AppColors.cs`.
- **Test:**
  - `tests/Backend.UnitTests` (Fake repository, test service/controller Inventory và Category, test ProductService);
  - `tests/Frontend.UnitTests` (bỏ `CategoryApiClientTests`, đổi tên `InventoryApiClientTests`, test ViewModel Category/Inventory, link trong csproj, test contrast).
- **Không đổi:** schema database (không có migration mới), quyền theo role (trừ việc bỏ quyền xóa tồn kho của Manager), giao diện của các trang ngoài Catalog/Inventory/Category, định nghĩa "sắp hết" của Dashboard.
- **Phối hợp:** báo Dev 3 trước khi làm, vì change sửa code của Feature 3.
