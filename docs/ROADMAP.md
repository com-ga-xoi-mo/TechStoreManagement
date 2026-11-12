# Roadmap TechStore

Yêu cầu gốc: [REQUIREMENTS.md](REQUIREMENTS.md).

M1 và M2 đã hoàn thành. M3 do An làm một mình. M4 chia cho sáu thành viên. Thời gian từng sprint sẽ được chốt theo hạn nộp.

## Các milestone

| Milestone | Nội dung | Phụ trách |
| --- | --- | --- |
| [M1: Nền tảng](MILESTONE_1_PLAN.md) | Solution, API, client, database, migrations và dữ liệu mẫu | Cả nhóm, đã hoàn thành |
| [M2: Quản trị Admin](MILESTONE_2_PLAN.md) | Sản phẩm, danh mục, kho/IMEI, khách hàng, đơn hàng và thống kê cơ bản | Cả nhóm, đã hoàn thành |
| [M3: Vận hành đa vai trò](MILESTONE_3_PLAN.md) | Phân quyền, tài khoản, nhập hàng, POS tiền mặt, ca thu ngân và dashboard Manager | An |
| [M4: Hoàn thiện và bàn giao](MILESTONE_4_PLAN.md) | VietQR, đổi trả, bảo hành, điểm thưởng, voucher, báo cáo nâng cao, AI và nghiệm thu | Sáu thành viên |

## Milestone 3

1. Phân quyền toàn bộ API; quản lý tài khoản và phiên đăng nhập.
2. Điều chỉnh màn hình theo role; hoàn thiện tìm kiếm sản phẩm và khách hàng cho Sales.
3. Quản lý nhà cung cấp, phiếu nhập và nhận hàng theo lô.
4. Hoàn thiện POS tiền mặt, thuế, giảm giá và ca thu ngân.
5. Kiểm thử, đối chiếu số liệu và bàn giao cho nhóm làm M4.

Dùng lại phần đã có ở M2: khóa dòng chống bán vượt tồn, lịch sử biến động kho, giá vốn tại thời điểm bán và kích hoạt ngày bảo hành.

Ưu tiên sửa API đơn hàng chưa yêu cầu đăng nhập và còn dùng ID Admin mặc định. Thuế và giảm giá cấp đơn hiện bằng 0 cũng cần hoàn thiện trong M3.

## Milestone 4

| Thành viên | Công việc chính |
| --- | --- |
| An (Dev 1) | Đổi trả và hoàn tiền, gồm cả hàng nhận lại và nhập lại kho |
| Q. Vinh (Dev 2) | Bảo hành và sửa chữa, gồm cả trạng thái kho và xuất máy thay |
| Bình (Dev 3) | Báo cáo doanh thu, hoàn tiền, lợi nhuận và hiệu suất |
| Đạt (Dev 4) | Thanh toán VietQR, giữ hàng, hoàn tất bán và đối soát ca |
| Vy (Dev 5) | Khách hàng thân thiết: điểm thưởng, VIP và voucher |
| T. Vinh (Dev 6) | Trợ lý Gemini và các tools đọc dữ liệu theo quyền |

M4 có ba sprint: xây dựng tính năng, tích hợp, nghiệm thu và nộp bài. API/DTO và test được làm ngay trong sprint xây dựng, không tách sprint chuẩn bị. Mỗi người làm trọn API, giao diện, dữ liệu demo, test và đoạn video của phần mình. An ghép tài liệu và bộ nộp ở sprint cuối.

Chỉ giữ các điểm nối cần thiết: Đạt và An gọi service điểm/voucher của Vy; Bình đọc dữ liệu giao dịch để báo cáo; T. Vinh gọi API báo cáo của Bình và API M3. Đầu vào/đầu ra được ghi rõ trong kế hoạch M4 để các phần có thể làm độc lập.

## Đối chiếu yêu cầu

| Yêu cầu trong đề | Nơi thực hiện |
| --- | --- |
| Mục 1, 2, 5: nghiệp vụ cửa hàng, CRUD, tìm kiếm, đơn hàng, thống kê và thiết kế | Kế thừa M2; bổ sung trong M3; kiểm thử lại ở M4 |
| Mục 3, 4: client chỉ qua API, nghiệp vụ và dữ liệu xử lý tại server | Giữ kiến trúc hiện có trong cả M3 và M4 |
| Mục 3, 9: đăng nhập, đăng xuất, token và phân quyền tại server | M3 sprint 1, 2; kiểm thử lại toàn bộ ở M4 |
| Mục 9: tính tiền, thuế, giảm giá, trừ kho, transaction và xử lý đồng thời | M3 sprint 3, 4, 5; mở rộng cho thanh toán và hoàn tiền ở M4 |
| Mục 3, 10: UI hoàn chỉnh, validation, loading, empty và error | Mỗi sprint có giao diện; nghiệm thu tổng thể ở M4 |
| Mục 6: dữ liệu demo đầy đủ và tài khoản đại diện các role | Chuẩn bị ở M3; chốt dữ liệu cuối kỳ ở M4 |
| Mục 7: Git private, lịch sử đóng góp, bảo mật và README | Thực hiện xuyên suốt; kiểm tra cuối ở M4 |
| Mục 8: AI dùng dữ liệu thật và tuân thủ RBAC | M4, T. Vinh phụ trách |
| Mục 9: test chức năng, biên, API, RBAC, hồi quy và báo cáo chất lượng | Test trong từng sprint; tổng hợp ở M4 |
| Mục 11: video, năm file nộp bài, ZIP và Moodle | M4 sprint 3 |

POS, ca thu ngân, VietQR, nhập hàng, full-text, đổi trả/bảo hành, VIP/voucher và lợi nhuận là phạm vi TechStore đã chọn. AI là phần khuyến khích trong đề và vẫn được giữ trong M4. Các ví dụ AI khác trong đề không bắt buộc triển khai tất cả.

## Cách nghiệm thu

- Chạy tính năng qua giao diện, API và database thật; ghi kết quả kiểm thử theo bản build.
- Kiểm tra lại các luồng M2 sau khi thay đổi. Không tính test bị bỏ qua hoặc chưa chạy là đạt.
- Giữ kiến trúc client qua API và cập nhật tài liệu, test, dữ liệu mẫu khi đổi nghiệp vụ.
- Chỉ đóng milestone khi không còn lỗi chặn luồng chính, sai tiền, sai tồn kho hoặc vượt quyền.

Tài liệu cuối kỳ cần mô tả đúng việc nhóm đã dùng API-first từ đầu. Điểm AI ở mục 8 và mục 10 của đề chưa thống nhất; An xác minh với giảng viên trước khi tổng hợp phần đề xuất điểm.
