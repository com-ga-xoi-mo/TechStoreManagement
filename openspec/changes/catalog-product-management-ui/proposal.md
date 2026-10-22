# Proposal

## Why

Sprint 2 giao cho Dev 2 lát cắt full-stack Quản lý Sản phẩm (MILESTONE_2_PLAN, mục Dev 2): Admin phải thêm, sửa, ngừng bán và xóa sản phẩm ngay trên desktop, và hai trang Catalog XAML cũ phải chuyển sang C# Markup. Hiện tại luồng Catalog trên client chỉ đọc (spec `frontend-catalog-browsing` còn cấm gửi mutation), `ProductListPage`/`ProductDetailPage` vẫn là XAML có code-behind dùng các pattern AGENTS.md cấm, và `IApiClient` chưa có `DELETE`. Change backend `catalog-product-management-api` đã hoàn tất trên nhánh `feat/catalog-product-management-api` với hợp đồng `PUT /api/products/{id}`, `PATCH /api/products/{id}/status`, `DELETE /api/products/{id}` và quyền ghi Admin/Manager, nên phía client làm ngay trên cùng nhánh đó và chạy thử được với API thật. Không có phần này thì bước 4 của kịch bản E2E Sprint 3 ("tạo sản phẩm Chuột Logitech G Pro X kèm specs JSONB") không làm được trên giao diện.

## What Changes

- **Chuyển giao diện sang C# Markup**: thay `ProductListPage.xaml(.cs)` và `ProductDetailPage.xaml(.cs)` bằng `ProductListPage.cs` và `ProductDetailPage.cs`, giữ nguyên hành vi đọc hiện có (lọc, sắp xếp, phân trang, trạng thái tải/lỗi/rỗng, quay lại có ngữ cảnh truy vấn và trả focus về thẻ sản phẩm). Các converter `Converters/ProductImageSourceConverter.cs` được thay bằng helper thuần `Controls/Presentation/CatalogPresentation.cs` có test.
- **Màn soạn sản phẩm dạng trang** `ProductEditorPage.cs` (route mới `ProductCreate` và `ProductEdit` dưới mục Sản phẩm), bố cục lấy từ các màn Stitch "Thêm sản phẩm mới" và "Chỉnh sửa Master-Detail":
  - thông tin cơ bản, danh mục (từ cây danh mục API), giá bán, giá vốn dự kiến kèm nhãn lãi gộp dự kiến (design D18), URL ảnh có xem trước;
  - trình soạn thông số động: mỗi dòng gồm key snake_case, kiểu (Chuỗi/Số/Đúng-Sai), giá trị;
  - bảng biến thể: biến thể đã lưu có SKU chỉ đọc và công tắc Đang bán, dòng mới chưa lưu có thể bỏ; không xóa biến thể đã lưu;
  - khóa và xóa trắng ô barcode sản phẩm khi có biến thể; công tắc theo dõi IMEI;
  - lỗi 400 hiện tại đúng ô (kể cả `variants[i].sku`, `specs`), lỗi 409/401/403 hiện ở banner; chặn bấm Lưu hai lần, không tự gửi lại.
- **Hành động trên trang chi tiết**: Sửa, Ngừng kinh doanh / Mở bán (`PATCH .../status`; sản phẩm và biến thể mới bắt đầu ở trạng thái chưa bán, design D19), Xóa (`DELETE`; khi server trả 409 thì hiện lời khuyên ngừng kinh doanh). Mỗi hành động có hộp xác nhận chữ qua `IDialogService`.
- **Nút "Thêm sản phẩm"** trên danh sách. Mọi nút ghi chỉ hiện với phiên có vai trò `Admin` hoặc `Manager`; server vẫn là nơi quyết định quyền.
- **Tầng dữ liệu ghi**: interface mới `IProductCommandRepository` (Create/Update/SetStatus/Delete) do `ProductRepository` hiện có cài đặt, dùng lại phần kiểm tra JSON chi tiết sản phẩm. `IProductRepository` (mà màn lập đơn của Dev 4 đang dùng) không đổi.
- **Transport**: thêm `DeleteAsync(relativePath)` (không cần body, chấp nhận 204) vào `IApiClient`/`ApiClient` và 4 stub test cài đặt `IApiClient`.
- **Dọn style theo AGENTS.md quy tắc 4**: sau khi hai trang Catalog thôi dùng XAML, `TechStoreMetricTextStyle` không còn trang XAML nào dùng; chuyển nó ra khỏi `AddThemedStyles` thành Markup style không chứa màu, và đặt màu chữ trực tiếp ở hai nơi đang dùng (`StatCard.cs`, `OrderCreatePage.cs`).
- **Tài liệu**: AGENTS.md (bảng trạng thái chuyển đổi, mô tả thư mục `Views/`, ngoại lệ trả focus mới, `DeleteAsync`), và ngoại lệ D2 mới ghi trong `design.md` của change này.
- **Nhánh**: làm thẳng trên `feat/catalog-product-management-api` (BE đã xong, chưa push). Khi xong, đổi tên nhánh thành `feat/catalog-product-management`, push và mở một PR duy nhất (BE + FE) vào `dev`.

Ngoài phạm vi: upload ảnh, nhập tồn kho đầu kỳ khi tạo sản phẩm (UC-INV-02 của Dev 3), hiển thị tồn kho/màu biến thể/số lượng theo danh mục (API Catalog không có dữ liệu này), chặn rời trang khi đang sửa dở bằng sidebar Shell, gợi ý tự sinh SKU biến thể, chuyển các trang XAML khác, sửa backend ngoài điều kiện tìm theo hãng.

## Capabilities

### New Capabilities
- `frontend-catalog-management`: người dùng có vai trò Admin/Manager tạo, sửa (kể cả thông số động và biến thể), ngừng/mở bán và xóa sản phẩm trên desktop qua Catalog API thật, với kiểm tra đầu vào tại client, lỗi server hiển thị đúng chỗ và không gửi lại yêu cầu ghi.

### Modified Capabilities
- `frontend-catalog-browsing`: trang danh sách chỉ còn ô từ khóa, danh mục, trạng thái và ô thông số thu gọn (design D15); bỏ ràng buộc "luồng Catalog MUST NOT gửi mutation"; phần duyệt vẫn chỉ đọc, còn thao tác ghi thuộc `frontend-catalog-management`.
- `frontend-api-access`: thêm yêu cầu gửi `DELETE` không cần body và chấp nhận 204.
- `frontend-shell-navigation`: mục Sản phẩm chứa thêm route tạo và sửa sản phẩm, quay lại giữ ngữ cảnh truy vấn.
- `product-catalog`: từ khóa `q` khớp thêm hãng sản phẩm, để ô tìm kiếm thay được ô thương hiệu riêng (design D15).

## Impact

- **Client (`frontend/TechStore.Client/`)**:
  - Mới: `Views/Catalog/ProductListPage.cs`, `ProductDetailPage.cs`, `ProductEditorPage.cs`, `CatalogViewParts.cs`; `ViewModels/Catalog/ProductEditorViewModel.cs`, `ProductEditorItems.cs`, `ProductInputParser.cs`, `ProductDraftValidator.cs`, `CatalogPermissions.cs`, `CatalogApiErrors.cs`; `Models/Catalog/ProductDraft.cs`, `SpecValueKind.cs`; `Repositories/Catalog/IProductCommandRepository.cs`; `Controls/Presentation/CatalogPresentation.cs` (chỉ quy tắc hiển thị).
  - Sửa: `Services/Api/IApiClient.cs`, `ApiClient.cs`; `Repositories/Catalog/ProductRepository.cs`; `ViewModels/Catalog/ProductListViewModel.cs`, `ProductDetailViewModel.cs`; `Services/Navigation/AppRoute.cs`; `Views/Shell/ShellRoutePageFactory.cs`; `UiServiceCollectionExtensions.cs`; `Styles/AppResources.cs`; `Controls/StatCard.cs`; `Views/Orders/OrderCreatePage.cs` (một dòng màu chữ).
  - Xóa: `Views/Catalog/ProductListPage.xaml(.cs)`, `ProductDetailPage.xaml(.cs)`, `Converters/ProductImageSourceConverter.cs`.
- **Test (`tests/Frontend.UnitTests/`)**: test mới cho `DeleteAsync`, repository ghi, parser thông số, presentation, quyền, `ProductEditorViewModel`, hành động ở danh sách/chi tiết, route mới; sửa helper khởi tạo ViewModel trong test Catalog và 4 stub `IApiClient`.
- **Backend**: một điều kiện `ILike(p.Brand, …)` trong `backend/Catalog/Repositories/ProductRepository.cs` (D15).
- **Không đổi**: `shared/` (dùng ba request `UpdateProduct*` do change BE tạo), `IProductRepository`, trang Orders ngoài một dòng màu chữ.
- **Phụ thuộc**: hợp đồng API trong `openspec/changes/catalog-product-management-api/specs/product-catalog/spec.md`, đã có code BE trên cùng nhánh nên kiểm thử thật chạy được ngay.
