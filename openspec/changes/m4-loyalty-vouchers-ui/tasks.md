# Tasks

## 1. Điều hướng và quyền (TDD: test trước)

- [ ] 1.1 `AppRouteKind.VoucherList`, `AppRoute.Vouchers()` dưới `ShellDestination.Customers`; test tạo route, so sánh, chặn sai destination.
- [ ] 1.2 `RoleAccess.CanManageVouchers`, `CanOpen(VoucherList)` chỉ Admin/Manager; test bốn vai trò; menu vẫn sáu mục.
- [ ] 1.3 `ShellRoutePageFactory` và `VerifyAllRoutesResolve` biết route mới.

## 2. Dữ liệu client

- [ ] 2.1 Models: `VoucherItem`, `VoucherPage`, `VoucherQuery`, `VoucherPresentation` (trạng thái, chuỗi giảm, lượt), `LoyaltySummary`, `LoyaltyTierThresholds`; test trạng thái Inactive/Expired/Exhausted/Active và mốc hạng khớp server.
- [ ] 2.2 `IVoucherRepository`/`VoucherRepository`: list, create, update, activate/deactivate; test đường dẫn, query, map, kiểm tra phản hồi sai, lỗi HTTP.
- [ ] 2.3 `ICustomerRepository.GetLoyaltySummaryAsync`; test đường dẫn, map, 404.

## 3. ViewModel

- [ ] 3.1 `VoucherListViewModel`: tải, tìm có chờ gõ, lọc trạng thái, phân trang, hủy khi rời trang, lỗi an toàn; test.
- [ ] 3.2 `VoucherEditorDialogViewModel`: tạo/sửa, kiểm tra nhập, lỗi theo trường, 409, khóa mã và loại khi sửa; test.
- [ ] 3.3 Bật/tắt voucher trong danh sách, cập nhật dòng, lỗi 403; test.
- [ ] 3.4 `CustomerDetailViewModel`: tải tóm tắt điểm song song, lỗi thẻ không làm hỏng hồ sơ, tiến độ hạng; test.

## 4. View (C# Markup, token giao diện)

- [ ] 4.1 `VoucherListPage` + bảng dòng (menu "•••"), tìm/lọc, phân trang, trạng thái tải/rỗng/lỗi.
- [ ] 4.2 `VoucherEditorDialog` theo mẫu `SupplierEditorDialog`.
- [ ] 4.3 Nút "Voucher" ở `CustomerListPage` (Admin/Manager) và thẻ tóm tắt điểm ở `CustomerDetailDialogView`.
- [ ] 4.4 DI trong `UiServiceCollectionExtensions`.

## 5. Kiểm thử (AI tự chạy, ghi lệnh và kết quả thật)

- [ ] 5.1 Build solution 0 warning; frontend unit tests; backend không đổi nhưng chạy lại.
- [ ] 5.2 Build `net10.0-desktop`; chạy trang thật với API và PostgreSQL, kiểm binding từng ô, đổi Sáng/Tối khi trang đang mở.
- [ ] 5.3 Kiểm tay: Admin, Manager, SalesStaff (không thấy nút, route bị chặn).
