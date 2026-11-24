# Tasks

## 1. Chạy thật trước khi sửa

- [x] 1.1 Probe tạm (không commit) chạy app Skia thật với dữ liệu thật từ API: danh sách voucher, hộp thoại tạo voucher và hồ sơ khách, ở 800×600, 1024×768, 1440×1000, Light và Dark; đo vị trí phần tử, độ tương phản và chụp ảnh.
- [x] 1.2 Ghi lại lỗi: tab lịch sử ở ngoài tầm nhìn trong hồ sơ khách; chữ bị cắt và hàng lệch cột trong bảng voucher.

## 2. Sửa

- [x] 2.1 `BuildLoyaltySummary` thành một dải một dòng.
- [x] 2.2 Bảng voucher: cột cố định, bọc cuộn ngang, đặt chiều rộng bảng theo viewport, mức giảm và đơn tối thiểu xuống dòng. Hai lần thử đầu (cột sao có `MinWidth`; thêm khoảng dư) bị bỏ vì hàng lệch cột ở 800; xem design D1.

## 3. Kiểm tra

- [x] 3.1 Chạy lại probe sau sửa: 18 kịch bản (3 kích thước × 2 theme × 3 màn) không lỗi; đủ 6 voucher; không cuộn ngang ở 1024 và 1440, cuộn 138px ở 800; hộp thoại nằm trong cửa sổ, nút Lưu nhìn thấy; tab "Lịch sử mua hàng" nhìn thấy trong vùng cuộn ở cả ba kích thước; 0 chỗ dưới ngưỡng tương phản.
- [x] 3.2 Xem ảnh chụp 800×600 và 1440×1000, Light và Dark: các hàng thẳng cột, không cắt chữ.
- [x] 3.3 `dotnet build TechStore.sln` và bản Windows native 0 lỗi 0 cảnh báo; frontend 1312/1312; backend 973 đạt, 80 bỏ qua (cần Postgres).
- [x] 3.4 Probe đã xóa, `Program.cs` đã khôi phục.

## 4. Chưa kiểm được (ghi trong tài liệu kết quả)

- [ ] 4.1 Chưa kiểm được, không tính là đạt: đổi Sáng/Tối khi trang đang mở, chạy Windows native (chỉ build), System theme, text scale, HighContrast, bàn phím/focus, bố cục trong shell thật (có thanh điều hướng).

## 5. Tài liệu

- [x] 5.1 Báo cáo kết quả chạy giữ cục bộ, không đưa vào repo.
- [x] 5.2 Sync spec và archive change.
