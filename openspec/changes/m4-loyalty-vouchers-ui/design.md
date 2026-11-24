# Design

## D1. Voucher là route con của Khách hàng, không phải mục menu thứ bảy

Spec shell quy định đúng sáu mục và nhiều test đếm sáu. Voucher thuộc nhóm khách hàng/ưu đãi, nên thêm `AppRouteKind.VoucherList` với `ShellDestination.Customers` (cùng cách `UserManagement` nằm dưới `Settings`). Menu giữ Khách hàng được chọn; trang có nút "← Khách hàng" quay lại. Khác với Khách hàng, chỉ Admin/Manager mở được (SalesStaff không có quyền ở API), nên `RoleAccess.CanOpen` chặn trước khi tải.

## D2. Repository ném `ApiException`, ViewModel dịch lỗi

Theo `CustomerRepository` (M3): repository kiểm tra và map JSON, lỗi HTTP ném `ApiException`; ViewModel đổi 401/403/404/409/400 thành thông báo tiếng Việt và đưa lỗi theo trường (`FieldErrors`) vào ô tương ứng (`code`, `discountValue`, `minOrderAmount`, `maxDiscountAmount`, `usageLimit`, `expiresAt`). Không dùng bộ result record của nhà cung cấp để khỏi có hai kiểu trong cùng module.

## D3. Mô hình hiển thị

`VoucherItem` giữ số liệu thô (loại, giá trị, đơn tối thiểu, trần giảm, lượt, hạn, bật/tắt). Chuỗi hiển thị (ví dụ "10% · tối đa 150.000 ₫", "3 đã dùng · 1 đang giữ / 50", "Hết hạn", "Hết lượt") nằm ở lớp trình bày thuần `VoucherPresentation` để test không cần UI. Trạng thái hiển thị suy ra từ dữ liệu: Đã tắt, Hết hạn, Hết lượt (`used + reserved >= limit`), Đang hoạt động; hết hạn tính theo `TimeProvider` được truyền vào, không dùng đồng hồ tĩnh.

## D4. Hộp thoại tạo/sửa theo mẫu nhà cung cấp

ViewModel phát sự kiện `EditorRequested(VoucherItem?)` và `StatusRequested(VoucherItem)`; Page mở `ContentDialog` (như `SupplierEditorDialog`). Hộp thoại sửa khóa mã và loại giảm (server không cho đổi). Đây là ngoại lệ "C# event yêu cầu mở dialog" đã được AGENTS.md cho phép; thêm hai tên sự kiện `VoucherEditorRequested`, `VoucherStatusRequested` vào danh sách ngoại lệ ở tài liệu này (không thêm ngoại lệ mới về cơ chế).

## D5. Thẻ tóm tắt điểm

Tóm tắt điểm có repository riêng `ILoyaltyRepository` (không thêm hàm vào `ICustomerRepository`, vì nhiều test của người khác có fake implement interface đó). `CustomerDetailViewModel` nhận `ILoyaltyRepository` là tham số tùy chọn cuối của constructor (các test hiện có không đổi) và gọi `GetSummaryAsync` song song với hồ sơ. Lỗi của riêng thẻ này không làm hỏng hồ sơ: thẻ ẩn và hồ sơ vẫn hiện. Tỉ lệ thanh tiến độ tính từ mốc hạng hiện tại tới hạng kế tiếp (500/1.500/5.000), ở hạng Platinum hiện "Đã đạt hạng cao nhất". Các mốc (0/500/1.500/5.000, cùng số với `LoyaltyRules` ở server) nằm trong `LoyaltyTierThresholds` phía client chỉ để vẽ thanh tiến độ và có test ghim số; hạng kế tiếp lạ thì dùng số điểm còn thiếu do server trả. Hạng và điểm vẫn lấy từ hồ sơ; summary chỉ bổ sung mức giảm, hạng kế tiếp và điểm thiếu.

## D6. Màu và kiểu chữ

Mọi màu đặt trực tiếp trên phần tử bằng `AppColors.Brushes.*`; style Markup chỉ chứa thuộc tính không màu; không thêm style vào `AddThemedStyles`. Bảng dài: mỗi dòng dùng một nút "•••" với `MenuFlyout` (`RowActionsButton`), trạng thái là chữ thường, không thêm hộp icon trang trí (xem AGENTS.md, bảng dài trên Skia).

## D7. Kiểm thử

Test headless cho repository (đường dẫn, query, map, lỗi), ViewModel (tải, lọc, trang, tạo/sửa/bật tắt, lỗi theo trường, quyền), `VoucherPresentation`, mốc hạng, điều hướng và `RoleAccess`. Giao diện được build cho `net10.0-desktop` và chạy thử bằng tay (ghi kết quả vào tasks); binding sai đường dẫn vẫn build được nên bắt buộc chạy trang.
