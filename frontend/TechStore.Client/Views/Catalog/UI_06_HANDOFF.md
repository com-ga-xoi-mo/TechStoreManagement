# UI-06 — Thêm sản phẩm: bàn giao UI preview

Ngày ghi nhận: **2026-10-10**  
Phạm vi: **UI/mock hoàn thành phía agent; nghiệm thu thủ công đã nhận ngày 2026-10-10 với một ngoại lệ: thêm/sử dụng link ảnh chưa hoạt động.**

## Đối chiếu thiết kế và hành vi

Dialog native Uno dùng D28 làm nguồn bố cục và D31 làm nền style/control. Form gồm thông tin sản phẩm, danh mục preview, SKU/barcode, giá, URL ảnh xem trước, specs động và các dòng biến thể. Có loading/error danh mục, validation theo trường, trạng thái đang gửi, lỗi giữ dữ liệu, đóng khi preview tạo thành công, và thông báo rằng sản phẩm mới chưa có tồn kho. URL ảnh sai giao thức/định dạng được báo lỗi; không có trường nhập số tồn hoặc nút lưu nháp. Không thể xóa biến thể cuối cùng.

Tạo thành công chỉ thêm một bản ghi vào `FakeProductRepository` đang được chia sẻ với danh sách. Sản phẩm preview mới có số lượng khả dụng bằng 0. Sản phẩm theo dõi Serial/IMEI cần được nhận hàng cùng danh sách mã máy qua luồng nhập kho riêng; UI-09/UI-13 và contract nhập kho chưa sẵn sàng. Không có HTTP/ApiClient/JWT/backend integration trong change này.

## File thuộc UI-06

- `frontend/TechStore.Client/App.xaml.cs`
- `frontend/TechStore.Client/Models/Catalog/CatalogModels.cs`
- `frontend/TechStore.Client/Repositories/Catalog/README.md`
- `frontend/TechStore.Client/Repositories/Catalog/IProductRepository.cs`
- `frontend/TechStore.Client/Repositories/Catalog/ICategoryRepository.cs`
- `frontend/TechStore.Client/Repositories/Catalog/FakeCategoryRepository.cs`
- `frontend/TechStore.Client/Repositories/Catalog/UnavailableCategoryRepository.cs`
- `frontend/TechStore.Client/Repositories/Catalog/FakeProductRepository.cs`
- `frontend/TechStore.Client/Repositories/Catalog/UnavailableProductRepository.cs`
- `frontend/TechStore.Client/Repositories/Catalog/ProductCreatePreviewScenario.cs`
- `frontend/TechStore.Client/Repositories/Catalog/ProductCreatePreviewSettings.cs`
- `frontend/TechStore.Client/Services/Dialog/IProductCreateDialogService.cs`
- `frontend/TechStore.Client/Services/Dialog/ProductCreateDialogService.cs`
- `frontend/TechStore.Client/ViewModels/Catalog/ProductCreateViewModel.cs`
- `frontend/TechStore.Client/ViewModels/Catalog/ProductListViewModel.cs`
- `frontend/TechStore.Client/Views/Catalog/ProductCreateDialog.xaml`
- `frontend/TechStore.Client/Views/Catalog/ProductCreateDialog.xaml.cs`
- `frontend/TechStore.Client/Views/Catalog/ProductListPage.xaml`
- `tests/Frontend.UnitTests/Frontend.UnitTests.csproj`
- `tests/Frontend.UnitTests/FakeProductRepositoryTests.cs` (existing test stubs updated for the repository contract)
- `tests/Frontend.UnitTests/ProductCreateRepositoryTests.cs`
- `tests/Frontend.UnitTests/ProductCreateViewModelTests.cs`
- `tests/Frontend.UnitTests/ProductDetailViewModelTests.cs` (existing test stubs updated for the repository contract)
- `tests/Frontend.UnitTests/ProductListViewModelTests.cs`

OpenSpec artifacts are in `openspec/changes/ui-06-product-create/`.

## Agent verification

- `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj --no-restore`: **Passed, 103/103**.
- `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore`: **Succeeded, 0 warnings and 0 errors**.
- Launched with `TECHSTORE_AUTH_MODE=preview dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-build`: **desktop process remained running**. The environment emitted Uno RemoteControl/dev-server and X11 `_NET_WM_STATE` warnings also seen in earlier runs; no application startup exception appeared. This is a launch smoke check, not owner visual acceptance.
- Preview error scenarios are selected at process start with `TECHSTORE_PRODUCT_CREATE_PREVIEW=Error`, `DuplicateSku`, or `DuplicateBarcode`; default is `Success`.

## Owner manual acceptance

- Status: **Accepted with one known issue; task 3.3 completed on 2026-10-10 per owner confirmation.**
- Confirmation date: **2026-10-10.**
- Accepted behavior: owner reports the rest of the UI-06 manual test is okay.
- Unmet behavior / follow-up: **Adding/using an image link does not work as expected.** Investigate URL preview/loading and error feedback in a follow-up task/change. Current scope only previews an HTTP/HTTPS URL; it does not upload files or provide CDN storage. Do not present image handling as complete until the preview behavior is fixed and verified.
- Test in the open preview app: sign in with `admin` / `Admin@123`, open **Sản phẩm → Thêm sản phẩm**, submit an invalid form and check the first invalid field gets focus, add/remove specs and variants, then create a valid preview product and confirm it appears in the list without stock. Cancel another form and confirm it does not add anything. Repeat at the current window width and at a narrower width where the app remains usable; try Tab/Enter/Escape and vertical scrolling.
- To verify failure retention, close the preview app and relaunch with `TECHSTORE_AUTH_MODE=preview TECHSTORE_PRODUCT_CREATE_PREVIEW=Error dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`; enter form data and submit, then confirm the dialog remains open and all values remain. Restart with the default scenario to test success.
