# UI-07 — Sửa sản phẩm và biến thể: bàn giao

Ngày: **2026-10-10**  
Phạm vi: **UI/mock preview**. Không có HTTP, API, JWT, backend, migration hoặc thay đổi tồn kho trong change này.

## Hành vi được triển khai

- Nút **Sửa sản phẩm** chỉ xuất hiện khi phiên preview có capability `catalog:product-edit`; command kiểm tra capability lần nữa trước khi mở dialog.
- Dialog dùng dữ liệu từ `IProductRepository`, giữ bản nháp riêng; từng biến thể giữ input khi đổi tab. Thêm/xóa biến thể chỉ thay đổi bản nháp, luôn giữ ít nhất một biến thể. Hủy không cập nhật catalog.
- Lưu preview cập nhật chung fake repository, giữ nguyên tồn khả dụng và chế độ Serial/IMEI. Conflict/lỗi giữ dữ liệu nhập và báo rõ kết quả mẫu; uniqueness thật, giao dịch của biến thể và quyền thật thuộc server.
- Trường giá vốn chỉ được tải/hiển thị khi capability preview `catalog:cost-write` được bật. Đây là cờ kiểm tra giao diện, không đại diện RBAC.
- SKU gợi ý là thao tác chủ động, có thể sửa tay và không được tự sinh lại khi sửa trường khác. Không cam kết SKU duy nhất.
- Không có xóa sản phẩm, thay đổi tồn, sửa danh sách Serial/IMEI, upload ảnh hoặc lưu nháp server. URL ảnh chỉ dùng để preview; khi tải ảnh lỗi, dialog hiển thị ảnh thay thế và thông báo.

## Chạy và thử các trạng thái

Chạy desktop UI với capability sửa và giá vốn:

```bash
TECHSTORE_AUTH_MODE=preview \
TECHSTORE_PRODUCT_EDIT_PREVIEW_ACCESS=authorized \
TECHSTORE_COST_WRITE_PREVIEW_ACCESS=authorized \
dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
```

Đăng nhập preview bằng `admin` / `Admin@123`, mở **Sản phẩm**, chọn **Sửa sản phẩm** trên một card. Bỏ `TECHSTORE_COST_WRITE_PREVIEW_ACCESS` để kiểm tra ẩn giá vốn; bỏ `TECHSTORE_PRODUCT_EDIT_PREVIEW_ACCESS` để kiểm tra ẩn/chặn thao tác sửa. Nếu chạy lại app từ terminal hiện có, hãy đóng process cũ trước khi đổi biến môi trường.

Các kịch bản repository giả lập được chọn lúc khởi động bằng `TECHSTORE_PRODUCT_EDIT_PREVIEW`:

| Giá trị | Kết quả |
| --- | --- |
| `Success` | Tải/lưu thành công |
| `DelayedLoad`, `DelayedSave` | Trạng thái đang tải/đang lưu |
| `NotFound`, `LoadError` | Không tìm thấy / lỗi tải |
| `SaveError` | Lỗi lưu, giữ bản nháp |
| `DuplicateSku`, `DuplicateBarcode` | Conflict định danh, giữ bản nháp |
| `VariantHasTransactions` | Sửa định danh hoặc xóa biến thể bị từ chối trong preview |
| `ConcurrentUpdate` | Conflict cập nhật đồng thời giả lập |

Ví dụ: `TECHSTORE_PRODUCT_EDIT_PREVIEW=VariantHasTransactions` trước lệnh `dotnet run`.

## API, quyền và dữ liệu còn phụ thuộc

- Tích hợp thật cần contract tải/sửa sản phẩm và biến thể, luật cập nhật category/brand, version/concurrency, payload lỗi validation/conflict, uniqueness SKU/barcode, quyền sửa giá vốn và quy tắc bảo vệ biến thể có giao dịch.
- UI-05/UI-06 đã có code preview; UI-07 dùng repository và category repository hiện hữu. Luồng nhập kho, chỉnh Serial/IMEI và UI-16 xóa sản phẩm vẫn tách biệt.
- Fake repository không kiểm tra trùng toàn hệ thống và không xác định transaction history thật. Backend phải xác thực mọi quyền và invariant.

## File đã thay đổi

- App/DI: `App.xaml.cs`.
- Model/repository: `Models/Catalog/ProductEditModels.cs`; `Repositories/Catalog/IProductRepository.cs`, `FakeProductRepository.cs`, `UnavailableProductRepository.cs`, `ProductEditPreviewScenario.cs`, `ProductEditPreviewSettings.cs`.
- Session/dialog: `Services/Session/ICurrentSession.cs`, `ProductEditCapabilityPreviewSettings.cs`; `Services/Dialog/IProductEditDialogService.cs`, `ProductEditDialogService.cs`.
- ViewModel: `ViewModels/Catalog/ProductEditViewModel.cs`, `ProductListViewModel.cs`, `ProductCardViewModel.cs`.
- View: `Views/Catalog/ProductEditDialog.xaml`, `ProductEditDialog.xaml.cs`, `ProductListPage.xaml`, `ProductListPage.xaml.cs`.
- Tests: `tests/Frontend.UnitTests/ProductEditRepositoryTests.cs`, `ProductEditViewModelTests.cs`, `ProductListViewModelTests.cs`, and `Frontend.UnitTests.csproj` plus existing repository test doubles.

## Ảnh sản phẩm — vấn đề còn mở

Chủ dự án đã thử trong UI-07 và xác nhận ngày **2026-10-10** rằng URL ảnh HTTP vẫn không hiển thị. Các luồng sửa sản phẩm khác được nghiệm thu; riêng preview ảnh bằng URL chưa đạt. Để dành điều tra và xử lý sau khi có cơ sở dữ liệu cùng quyết định về lưu trữ/media endpoint. Change này không thêm upload, CDN, API ảnh hay persistence; ảnh lỗi vẫn phải dùng placeholder/fallback và không làm mất nội dung form.

## Agent verification

- `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj --no-restore`: **Passed, 139/139**.
- `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore`: **Succeeded, 0 warnings, 0 errors**.
- The image-failure/fallback state is covered by a headless test. Owner manual test confirms that an HTTP image URL still does not display; follow-up is deferred until database/media storage is available.
- Desktop process launched with the three preview environment variables above. The environment logged the existing Uno RemoteControl/dev-server and X11 `_NET_WM_STATE` warnings. Owner manually tested the edit UI and confirmed it is OK except for the HTTP image URL preview issue.
- Windows target was not built in this Linux environment.

## Owner acceptance

- Status: **Confirmed with one deferred issue — 2026-10-10**. Owner reports the tested flows are OK, except an HTTP image URL does not display.
- Manual acceptance covered the current and narrower usable window and the edit interactions. No screenshot or exact scale was required.
- Follow-up: after a database exists and image storage/API behavior is defined, investigate HTTP image loading and decide whether URL validation, hosting, or media storage integration is needed. Until then, this remains an acknowledged limitation, not a completed image-preview behavior.
