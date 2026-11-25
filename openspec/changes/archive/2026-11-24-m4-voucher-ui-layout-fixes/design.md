# Design

## D1. Cột cố định, chỉ cột Mã voucher co giãn

Thử ba cách trên app Skia thật (ảnh chụp ở mỗi bước):

1. Cột sao có `MinWidth` và cuộn ngang khi hẹp (cách trang Khách hàng đang dùng). Ở 1024 đủ chỗ nên thẳng hàng, nhưng ở 800, nơi tổng độ rộng tối thiểu bằng đúng chiều rộng bảng, Uno chia các cột sao theo nội dung dài nhất của từng hàng, nên các hàng lệch khỏi tiêu đề. Thêm khoảng dư 24px không sửa được.
2. Bảng khởi tạo ở độ rộng hữu hạn. Không đủ một mình.
3. **Chọn**: sáu cột có độ rộng pixel cố định, cột Mã voucher là cột sao duy nhất (`MinWidth` bằng độ rộng của nó). Pixel không phụ thuộc nội dung nên mọi hàng thẳng với tiêu đề ở mọi kích thước.

Độ rộng (DIP): mã 150, mức giảm 150, đơn tối thiểu 108, lượt dùng 94, hết hạn 94, trạng thái 142, thao tác 70, khe 8, đệm 16 mỗi bên. Tổng tối thiểu 868. Ở 1024 và 1440 bảng vừa khít; ở 800 cuộn ngang khoảng 140px, giống trang Khách hàng.

Tiêu đề và hàng nằm trong một `Grid` bọc bởi một `ScrollViewer` ngang; các hàng giữ thanh cuộn dọc riêng. Chiều rộng bảng được đặt bằng `max(viewport, tối thiểu)` qua `SizeChanged`/`Loaded`, vì `ScrollViewer` đo nội dung với chiều rộng vô hạn (cùng cách `CustomerListPage`). `SizeChanged` cho bố cục đáp ứng là ngoại lệ đã được cho phép trong `AGENTS.md`.

Mức giảm và đơn tối thiểu cho xuống dòng (`TextWrapping.Wrap`) phòng khi chữ dài hơn cột. Nhãn không đổi (`VoucherPresentation` đã có test).

## D2. Thẻ ưu đãi là một dải, không phải một thẻ

Hộp thoại hồ sơ khách là một `Popup` có `MaxHeight = max(500, cao cửa sổ − 64)`; ở 800×600 vùng cuộn chỉ cao khoảng 290px. Ô số liệu 160px cộng thẻ cũ 132px đã lấp đầy vùng đó, nên tab nằm ngay dưới mép. Dải một dòng chiếm khoảng 50px nên tab hiện ra ngay dưới ô số liệu ở cả ba kích thước (đo bằng vị trí phần tử và ảnh chụp).

Nội dung giữ nguyên: tên "Ưu đãi theo hạng", mức giảm của hạng, nhãn hạng kế tiếp, thanh tiến độ có `AutomationProperties.Name` "Tiến độ lên hạng kế tiếp".

## Không làm

- Không đổi các bảng khác. Các cột sao của danh sách khách hàng, kho, nhập hàng có thể cắt chữ ở 800 như cũ; đó là phần của người sở hữu từng màn.
