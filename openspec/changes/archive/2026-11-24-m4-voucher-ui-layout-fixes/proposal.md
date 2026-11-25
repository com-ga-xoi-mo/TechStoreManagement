# Proposal

## Why

M4 Sprint 3: "Chạy từng trang Windows native và Skia … Kiểm tra Light/Dark/System, 800×600 / 1024×768 / 1440×1000 DIP". Chạy màn Voucher và hồ sơ khách trong app Skia thật ở ba kích thước và hai theme, kèm ảnh chụp, cho thấy hai lỗi bố cục:

1. **Hồ sơ khách (Admin/Manager)**: thẻ "Ưu đãi theo hạng" cao khoảng 130px đẩy hàng tab "Lịch sử mua hàng / Thiết bị / Lịch sử điểm" xuống dưới mép vùng cuộn, phải cuộn mới thấy tab.
2. **Danh sách voucher**: ở 800×600 nhãn trạng thái bị cắt ("Đang ho…"), tiêu đề "THAO…", mức giảm "10% · tối đa 2…", "Không yêu…", ngày "03/12/20…". Ở 1024×768 mức giảm vẫn bị cắt ("tối đa 200.00…").

## What Changes

- Thẻ ưu đãi thành một dải một dòng (khoảng 50px): "Ưu đãi theo hạng" + mức giảm bên trái, hạng kế tiếp bên phải, thanh tiến độ mảnh bên dưới.
- Bảng voucher: các cột có độ rộng cố định đủ cho tiêu đề và giá trị dài nhất (chỉ cột Mã voucher co giãn); mức giảm và đơn tối thiểu được xuống dòng; tiêu đề và các hàng nằm trong một bảng nên một thanh cuộn ngang di chuyển cả hai; dưới độ rộng tối thiểu bảng cuộn ngang thay vì cắt chữ.
- **Không làm**: đổi nội dung nhãn, API hay ViewModel.

## Capabilities

### Modified Capabilities

- `frontend-voucher-management`: bố cục bảng ở cửa sổ hẹp.
- `frontend-customer-management`: thẻ ưu đãi gọn, không đẩy tab lịch sử ra khỏi tầm nhìn.

## Impact

- `frontend/TechStore.Client/Views/Vouchers/VoucherListPage.cs`, `Views/Customers/CustomerDetailDialogView.cs`.
- Không đổi test headless (view không được link vào test); kiểm chứng bằng chạy app Skia thật, kết quả ghi ở `tasks.md` mục 3 và 4.
- Ảnh hưởng người khác: `CustomerDetailDialogView` là file chung của màn Khách hàng; chỉ đổi hàm `BuildLoyaltySummary` do M4 thêm.
