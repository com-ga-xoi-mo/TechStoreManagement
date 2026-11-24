# Tasks

## 1. Điều hướng và quyền (TDD: test trước)

- [x] 1.1 `AppRouteKind.VoucherList`, `AppRoute.Vouchers()` dưới `ShellDestination.Customers`; test tạo route, so sánh, chặn sai destination.
- [x] 1.2 `RoleAccess.CanManageVouchers`, `CanOpen(VoucherList)` chỉ Admin/Manager; test bốn vai trò; menu vẫn sáu mục.
- [x] 1.3 `ShellRoutePageFactory` và `VerifyAllRoutesResolve` biết route mới.

## 2. Dữ liệu client

- [x] 2.1 Models: `VoucherItem`, `VoucherPage`, `VoucherQuery`, `VoucherPresentation` (trạng thái, chuỗi giảm, lượt), `LoyaltySummary`, `LoyaltyTierThresholds`; test trạng thái Inactive/Expired/Exhausted/Active, chuỗi hiển thị và mốc hạng/tiến độ.
- [x] 2.2 `IVoucherRepository`/`VoucherRepository`: list, create, update, activate/deactivate; test đường dẫn, query, map, kiểm tra phản hồi sai, lỗi HTTP.
- [x] 2.3 `ILoyaltyRepository`/`LoyaltyRepository.GetSummaryAsync` (tách khỏi `ICustomerRepository` để không phá các fake trong test của người khác); test đường dẫn, map, kiểm tra phản hồi, 404.

## 3. ViewModel

- [x] 3.1 `VoucherListViewModel`: tải, tìm có chờ gõ, lọc trạng thái, phân trang, hủy khi rời trang, lỗi an toàn; test.
- [x] 3.2 `VoucherEditorDialogViewModel`: tạo/sửa, kiểm tra nhập, lỗi theo trường, 409, khóa mã và loại khi sửa; test.
- [x] 3.3 Bật/tắt voucher trong danh sách, cập nhật dòng, lỗi 403; test.
- [x] 3.4 `CustomerDetailViewModel`: tải tóm tắt điểm song song, lỗi thẻ không làm hỏng hồ sơ, tiến độ hạng; test.

## 4. View (C# Markup, token giao diện)

- [x] 4.1 `VoucherListPage` + bảng dòng (menu "•••"), tìm/lọc, phân trang, trạng thái tải/rỗng/lỗi.
- [x] 4.2 `VoucherEditorDialog` theo mẫu `SupplierEditorDialog`.
- [x] 4.3 Nút "Voucher" ở `CustomerListPage` (Admin/Manager) và thẻ tóm tắt điểm ở `CustomerDetailDialogView`.
- [x] 4.4 DI trong `UiServiceCollectionExtensions`.

## 5. Kiểm thử (AI tự chạy, ghi lệnh và kết quả thật)

- [x] 5.1 `dotnet build TechStore.sln`: 0 lỗi, 0 cảnh báo. `dotnet build ... -f net10.0-desktop` và `-f net10.0-windows10.0.26100`: 0 lỗi, 0 cảnh báo. `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`: 1312/1312 (trước change là 1141). Backend không đổi. Thử đột biến: đổi giới hạn 100% của phần trăm thành 101% làm test `Save_BadDiscountValue_...(100,01)` đỏ; đã khôi phục.
- [x] 5.2 Chạy trang thật trong ứng dụng Skia (probe tạm không commit, dựng `VoucherListPage`, `VoucherEditorDialog`, `CustomerDetailDialogView`, `CustomerListPage` với repository giả): 37/37 kiểm tra đạt, gồm chữ từng cột và dòng, ô tìm/lọc nối hai chiều vào ViewModel, hộp thoại sửa (mã chỉ đọc, giá trị điền sẵn, lỗi hiện cạnh ô, nút Hủy đóng hộp thoại), thẻ tóm tắt điểm (Giảm 2%, còn 800 điểm, thanh tiến độ 0,2), nút Voucher hiện cho Admin và ẩn cho Sales, giao diện Tối. Lần chạy đầu bắt được một lỗi thật: nhãn lỗi trong hộp thoại binding bằng `Func` nên đường dẫn sai và không hiện lỗi; đã sửa bằng binding theo tên thuộc tính. Chưa chạy bằng tay: đổi Sáng/Tối khi trang đang mở bằng mắt thường và `ProgressBar` ở Windows native (chỉ build).
- [x] 5.3 Chạy thật với PostgreSQL 16 (Docker) và backend `dotnet run --launch-profile http`: `TECHSTORE_API_BASE_URL=http://localhost:5176/ dotnet test tests/Frontend.IntegrationTests --filter VoucherLoyalty` được 3/3: tạo voucher qua hộp thoại (mã thường thành chữ hoa), tạo trùng bị 409 và hộp thoại giữ mở, tìm theo mã, sửa (mã giữ nguyên, 12,5%, 10 lượt), tắt rồi bật lại, lọc "Đã tắt" ra rỗng; `cashier1` bị 403 và trang báo không có quyền; thẻ điểm hiện cho `admin` và `cashier1` (Sales không tải lịch sử), khách lạ trả 404 thành "không có thẻ".
