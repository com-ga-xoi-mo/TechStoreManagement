# Dữ liệu demo tháng 10/2026

Chạy seed bằng đường khởi động API của dự án. Trên DB sạch, seed tạo 5 danh mục, 20 sản phẩm, 10 khách, 21 đơn và 4 tài khoản thuộc 4 vai trò. Hai mươi đơn mang mã `DEMO-202610-…` thuộc fixture M3, gồm 6 giao dịch VietQR đã xác nhận. M4 bổ sung đơn khách lẻ `M4-202610-0001` và một biến thể có SKU `M4-CABLE-PACK`.

| Ngày (giờ Việt Nam) | Chứng từ | Số liệu để đối chiếu |
|---|---|---|
| 24/10 | Nhập `PO-M4-202610-0001` | Nhận 4 bộ cáp, giá vốn 600.000đ/bộ, tồn sau nhập 4. |
| 25/10 | Đơn Cash `M4-202610-0001` của `cashier1` | Hai bộ giá 1.000.000đ mỗi bộ, giảm 200.000đ, thuế 180.000đ. Tổng thu 1.980.000đ. Tồn sau bán 2. |
| 26/10 | Phiếu hoàn đã đối soát theo chứng từ demo `DEMO-M4-20261026-01` | Hoàn qua ngân hàng 1.980.000đ cho 2 bộ. Phiếu lưu 1.800.000đ tiền hàng sau giảm và 180.000đ thuế. |
| 27/10 | Kho kiểm định lô trả | 1 bộ đã nhập lại, 1 bộ còn chờ. Movement CustomerReturn +1 liên kết phần đã kiểm, tồn sau nhập lại là 3. Vốn nhập lại ngày 27/10 là 600.000đ. |
| 26–27/10 | Sửa rồi trả khách thiết bị của `DEMO-202610-0001` | Hồ sơ `warranty_cases` trạng thái ReturnedToCustomer liên kết hai thao tác nhận và trả máy. IMEI cuối cùng Sold, giữ hạn bảo hành ban đầu. |

Để xem phiếu trả, đăng nhập bằng tài khoản được phép xem đơn và tra `M4-202610-0001`. Warehouse thấy phần còn chờ trong danh sách kiểm định. Nếu kiểm định tiếp, lần seed sau giữ kết quả của người dùng. Các chứng từ demo được tạo trong một transaction được khóa riêng cho seed. Ca Cash ban đầu của M3 giữ nguyên và ca ngày 25/10 chứa khoản thu 1.980.000đ.

API bảo hành đọc được hồ sơ đã kết thúc cùng dòng bán và IMEI gốc. Dữ liệu hoàn qua ngân hàng là chứng từ đối soát mô phỏng tại máy. Tài khoản khách lẻ không có sổ điểm.
