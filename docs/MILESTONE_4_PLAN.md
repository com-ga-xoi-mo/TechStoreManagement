# Milestone 4: Hoàn thiện và bàn giao

Bắt đầu sau khi [M3](MILESTONE_3_PLAN.md) được nghiệm thu. Hoàn thành các tính năng dưới đây và bộ nộp theo [REQUIREMENTS.md](REQUIREMENTS.md). M3 vẫn do An làm một mình.

## Phân công

Mỗi người làm trọn phần mình: API, giao diện, dữ liệu demo, test và tài liệu.

| Người | Phần phụ trách |
| --- | --- |
| An | Đổi trả, hoàn tiền và nhập lại hàng trả |
| Q. Vinh | Bảo hành, sửa chữa và xuất máy thay |
| Bình | Báo cáo doanh thu, lợi nhuận và hiệu suất bán hàng |
| Đạt | VietQR, giữ/trừ kho khi thanh toán và đối soát ca |
| Vy | Điểm thưởng, VIP và voucher |
| T. Vinh | Trợ lý Gemini |

An và Đạt tự nối luồng hoàn tiền/thanh toán với service của Vy. Bình chỉ đọc dữ liệu để báo cáo. Mỗi người sửa lỗi phần mình; An ghép file chung, migration và bộ nộp.

## Sprint 1: Chuẩn bị đầu vào, đầu ra và test

Mỗi người xác định API/DTO, dữ liệu cần lưu và test cases cho phần mình. Dùng lại [trạng thái đơn](../backend/Orders/Services/OrderStatusTransitions.cs), [schema](database/schema.dbml) và [mức VIP](../backend/Customers/README.md). Các quy tắc ở sprint 2 là đề xuất triển khai, cần đối chiếu với bản M3 bàn giao.

| Người | Bàn giao để người khác làm độc lập |
| --- | --- |
| Vy | Hàm tính/phân bổ ưu đãi; giữ/xác nhận/hủy lượt voucher; cộng/đảo điểm theo mã đơn/lần hoàn, kèm số dư và lỗi validation |
| Đạt | Quy tắc ghi tiền, khóa ca và mã tham chiếu ngân hàng cho An; làm thử tạo QR và xác nhận tiền |
| An | Dữ liệu hoàn tiền, phân bổ thuế/giảm giá và thời điểm nhập lại hàng cho Bình |
| Q. Vinh | Dữ liệu xuất máy thay và chi phí bảo hành cho Bình |
| Bình | Công thức, dữ liệu tính tay và mẫu kết quả API báo cáo cho T. Vinh; nhận dữ liệu bán từ Đạt |
| T. Vinh | Danh sách tools, quyền truy cập và câu hỏi kiểm thử AI; dùng API sản phẩm/tồn kho của M3 |

Quy tắc làm chung:

- Mỗi người quản lý service, API/DTO và màn hình của mình; dùng dữ liệu giả đúng cấu trúc khi phần khác chưa xong, thay bằng phần thật ở sprint 3.
- Tiền, kho và điểm phải cùng thành công hoặc cùng rollback. Hàm của Vy dùng UnitOfWork bên gọi, không tự mở/commit transaction. Gọi ngân hàng ngoài transaction DB, có chống lặp và đối soát.
- Bổ sung lưu trữ cho giữ hàng/voucher, yêu cầu/đối soát tiền, phân bổ thuế/giảm giá và hồ sơ bảo hành. Thay schema phải có migration, cập nhật `schema.dbml` và test.

Nghiệm thu: mỗi phần có API/DTO, mẫu dữ liệu và test cases; các bên dùng chung biết rõ cách gọi và kết quả trả về.

## Sprint 2: Làm tính năng

### An: Đổi trả và hoàn tiền

- [ ] Làm UI/API yêu cầu trả, hoàn từng phần/toàn bộ và lịch sử. Sales gửi yêu cầu; Admin/Manager hoàn tiền. Kiểm tra đơn, IMEI, lý do, số lượng và tiền còn được hoàn.
- [ ] Đơn `Completed`/`PartiallyRefunded` được trả phần còn lại. Đơn `Processing` chưa giao chỉ hoàn toàn bộ và giải phóng hàng giữ, không cộng tồn hay đảo điểm chưa được cộng.
- [ ] Tính hoàn theo giá bán gốc: phân bổ giảm giá cấp đơn theo tiền dòng sau giảm giá dòng, cộng thuế đã lưu. Làm tròn 2 chữ số; lần hoàn cuối nhận phần dư. Giữ nguyên dòng bán, `PaidAt`, `CompletedAt`; cập nhật `PartiallyRefunded`/`Refunded`.
- [ ] Hoàn Cash dùng ca đang mở của người hoàn. Tiền két = đầu ca + bán Cash - hoàn Cash; QR không tính vào két. Hoàn ngân hàng phải có mã tham chiếu, chống chi lặp và đối soát khi ngân hàng thành công nhưng DB lỗi. Lưu yêu cầu chờ riêng; chỉ ghi `OrderReturn` khi đã hoàn tiền.
- [ ] Nhận hàng vào `Returned`; Warehouse kiểm tra rồi chuyển `InStock` và cộng tồn, ghi `CustomerReturn`/`IsRestocked` cùng transaction. Hàng lỗi/sửa vào `Defective`/`UnderRepair`, chưa cộng tồn; xử lý riêng từng phần của lô trả.
- [ ] Gọi hàm đảo điểm của Vy. Đổi hàng thương mại = trả/hoàn + đơn bán mới có liên kết, không sửa đơn cũ.

Nghiệm thu: hoàn một phần rồi toàn bộ khớp tiền/thuế/kho/điểm; chặn hoàn vượt giới hạn, sai IMEI, gửi lặp và hai yêu cầu đồng thời; lỗi phải rollback.

### Q. Vinh: Bảo hành và sửa chữa

- [ ] Làm UI/API tra cứu, tiếp nhận, kiểm định, sửa/đổi và lịch sử. Kiểm tra đúng khách/đơn/IMEI, `WarrantyStartAt <= lúc nhận < WarrantyEndAt`; khách lẻ dùng mã đơn + IMEI. Dòng đã trả/hoàn hết không còn bảo hành theo lần bán đó.
- [ ] Nhận sửa: `Sold → UnderRepair → Sold` khi trả khách; giữ hạn bảo hành, không tăng tồn hoặc tạo giao dịch bán/hoàn.
- [ ] Admin/Manager duyệt đổi máy. Máy thay cùng sản phẩm/biến thể, đang `InStock`; xuất đúng một lần, liên kết IMEI cũ/mới với đơn gốc và giữ thời hạn bảo hành còn lại. Máy cũ thành `Defective`.
- [ ] Lưu giá vốn máy thay, biến động kho và hồ sơ bảo hành; không tạo doanh thu/điểm mới. Tồn serial vẫn bằng số máy `InStock + Reserved`, không tính máy khách gửi sửa.

Nghiệm thu: chặn thiết bị sai/hết hạn, xử lý trùng một máy và xuất máy thay lặp; lịch sử, tồn, chi phí khớp và rollback khi lỗi. Máy sửa/lỗi không được bán.

### Bình: Báo cáo quản trị

- [ ] Làm API/UI doanh thu ngày/kỳ, hoàn tiền, doanh thu thuần, giá vốn, lợi nhuận gộp, sản phẩm bán chạy và hiệu suất người bán. Chỉ Admin/Manager xem báo cáo tài chính.
- [ ] Tính ngày theo giờ Việt Nam: bán theo `CompletedAt`, hoàn theo `RefundedAt`. Giữ doanh thu gốc của đơn đã chuyển `PartiallyRefunded`/`Refunded`; thuế và dòng tiền hiển thị riêng.
- [ ] Doanh thu thuần chưa thuế = tiền hàng sau giảm giá đã giao - tiền hàng hoàn trong kỳ. Giá vốn lấy từ `OrderItem.UnitCost`; chỉ giảm khi hàng trả thực sự nhập lại, theo ngày nhập lại và giá vốn gốc.
- [ ] Lợi nhuận gộp = doanh thu thuần chưa thuế - giá vốn thuần. Hiển thị chi phí bảo hành riêng; giá vốn thiếu phải báo chưa tính được, không thay bằng 0.

Nghiệm thu: khớp bộ dữ liệu tính tay, tổng ngày bằng tổng kỳ, tiền ca khớp thu/hoàn; đúng cả hàng lỗi chưa nhập lại và nhập lại khác ngày hoàn. Bàn giao API thật cho T. Vinh.

### Đạt: VietQR và ca thu ngân

- [ ] Làm UI/API QR đúng ngân hàng, số tiền, mã đơn và trạng thái chờ/thành công/thất bại/hết hạn. Giữ `Pending` = chưa trả, `Processing` = đã trả chưa giao, `Completed` = đã giao.
- [ ] Đơn nháp không giữ hàng. Tạo QR phải kiểm tra lại tổng tiền, ca mở, hàng và ưu đãi; giữ hàng/voucher 15 phút. Mỗi đơn một QR chờ, khóa sửa giỏ/tổng/phương thức; hết hạn hoặc hủy thì trả hàng/lượt voucher đúng một lần.
- [ ] Sales tạo QR trong ca mình. Admin/Manager xác nhận thủ công dựa trên giao dịch ngân hàng, lưu người xác nhận, thời điểm và mã tham chiếu; không dùng ảnh khách gửi làm bằng chứng.
- [ ] Làm webhook kiểm tra chữ ký/nguồn, số tiền, mã đơn và mã giao dịch; dùng chung nghiệp vụ xác nhận thủ công. Ghi rõ dùng thật hay sandbox, không coi thao tác thủ công là webhook.
- [ ] Xác nhận tiền dùng hàng/voucher đã giữ, ghi tiền ca, trừ kho, giá vốn và kích hoạt bảo hành một lần. Gọi hàm cộng điểm của Vy khi hoàn tất bán, cả Cash và QR.
- [ ] Chặn đóng ca còn QR chờ. Tiền đến sau hết hạn/hủy/đóng ca phải lưu để đối soát/hoàn, không tự hoàn tất đơn hoặc sửa ca cũ. Xác nhận tranh với hết hạn/hủy chỉ được xử lý một nhánh.

Nghiệm thu: kiểm tra sai tiền/nguồn/mã, callback lặp, mất response, tiền đến muộn và xác nhận đồng thời với hết hạn/đóng ca; không thu/trừ kho/cộng điểm hai lần, rollback khi lỗi.

### Vy: Điểm thưởng, VIP và voucher

- [ ] Làm UI/API quản lý voucher, điểm, hạng và lịch sử khách hàng; cung cấp service cho An/Đạt, không sửa luồng checkout/hoàn tiền.
- [ ] Cộng 1 điểm/100.000 đồng tiền hàng sau giảm giá, chưa thuế, làm tròn xuống; chỉ cộng khi hoàn tất đơn có khách hàng. Chống cộng lặp theo mã đơn.
- [ ] VIP: Standard dưới 500 điểm; Silver từ 500 giảm 2%; Gold từ 1.500 giảm 5%; Platinum từ 5.000 giảm 10%. Tính ưu đãi bằng hạng trước thanh toán, cập nhật hạng sau thay đổi điểm.
- [ ] Mỗi đơn chọn một ưu đãi cấp đơn: VIP, voucher hoặc giảm thủ công theo quyền M3. Giữ giảm giá dòng; tính giảm cấp đơn rồi mới tính thuế, lưu phân bổ để hoàn tiền.
- [ ] Voucher phần trăm/số tiền phải còn hoạt động, chưa hết hạn, đủ đơn tối thiểu và còn lượt; giới hạn mức giảm, không vượt tiền hàng. Giữ lượt khi bắt đầu trả tiền, tiêu khi nhận tiền, trả lượt nếu hủy/hết hạn trước trả; hoàn đơn đã trả không phục hồi voucher.
- [ ] Hoàn tiền thì tính lại điểm trên hàng còn giữ, trừ chênh lệch và ghi lịch sử theo mã lần hoàn. Bảo hành không đảo điểm; chưa làm đổi điểm lấy tiền. Giữ số dư ban đầu và lịch sử cũ, không xóa để khớp test.

Nghiệm thu: đúng công thức, mốc VIP, số dư không âm, voucher hết hạn/hết lượt; hai người dùng lượt cuối chỉ một người thành công; gọi lặp và rollback không làm sai điểm/lượt.

### T. Vinh: Trợ lý Gemini

- [ ] Làm UI chat và tools chỉ đọc: tìm sản phẩm theo ngân sách/specs, tồn khả dụng và báo cáo qua service/API hiện có. Không cho model chạy SQL hoặc tự ghi dữ liệu.
- [ ] Server kiểm tra quyền ở từng tool, loại trường cấm trước khi gửi model. Sales tư vấn từ tồn thật, không xem giá vốn/báo cáo tổng; Warehouse tra tồn; Admin/Manager hỏi báo cáo.
- [ ] Trả lời bằng tên/mã sản phẩm thật, báo cáo có kỳ và thời điểm dữ liệu. Không có dữ liệu/tool lỗi thì báo rõ; có timeout, hủy và giới hạn lượt gọi tool.

Nghiệm thu: test dữ liệu rỗng, tool lỗi, giả role, sửa tham số và chỉ dẫn độc hại trong prompt/dữ liệu sản phẩm; không bịa số liệu hoặc lộ dữ liệu trái quyền.

Nghiệm thu sprint 2: mỗi phần có UI/API/database thật và test. Chỉ điểm nối chưa xong được dùng dữ liệu giả đã ghi rõ; sprint 3 phải thay hết bằng triển khai thật.

## Sprint 3: Tích hợp và kiểm tra toàn luồng

| Người | Việc cần hoàn thành |
| --- | --- |
| An | Nối đảo điểm thật; chạy trả/hoàn/nhập lại, đối chiếu tiền và kho |
| Q. Vinh | Chạy nhận sửa, trả khách, đổi máy; đối chiếu IMEI, tồn và chi phí |
| Bình | Chạy báo cáo trên giao dịch thật; đối chiếu tổng ngày/kỳ, quyền và tiền ca |
| Đạt | Nối ưu đãi/điểm thật; chạy Cash/QR, hết hạn, tiền tới muộn và đóng ca |
| Vy | Kiểm tra service trong transaction thật của thanh toán/hoàn tiền |
| T. Vinh | Dùng API báo cáo thật; thử tư vấn và báo cáo bằng bốn role |

Ví dụ đối chiếu: bán 2 sản phẩm giá 1.000.000đ, vốn 600.000đ/món; giảm đơn 200.000đ, thuế demo 10%. Thu 1.980.000đ, cộng 18 điểm. Trả 1 món hoàn 990.000đ (900.000 + 90.000 thuế), đảo 9 điểm. Nếu nhập lại: vốn thuần 600.000đ, lãi gộp 300.000đ; chưa nhập lại: vốn 1.200.000đ, lãi -300.000đ. Trả nốt hoàn 990.000đ/đảo 9 điểm; gửi lặp không thay số liệu.

Nghiệm thu:

- [ ] Nhập hàng → bán Cash/QR → hoàn một phần/toàn bộ → báo cáo: tiền, thuế, giảm giá, tồn, điểm và voucher khớp.
- [ ] Hàng trả lỗi, nhận sửa và đổi máy có đúng IMEI/lịch sử; máy lỗi không bán được.
- [ ] Hai người tranh hàng/lượt voucher cuối không vượt tồn/hạn mức; chạy giao dịch thực sự đồng thời trên PostgreSQL test riêng.
- [ ] Gọi API trực tiếp hoặc qua AI đều bị chặn nếu trái quyền. Đăng xuất khi đang tải, đổi tài khoản, mất mạng/thử lại không lộ phiên cũ hoặc thanh toán trùng.

Mỗi người ghi kết quả và sửa lỗi phần mình. Chỉ qua sprint 4 khi không còn lỗi sai tiền, sai kho hoặc vượt quyền.

## Sprint 4: Nghiệm thu và nộp bài

Mỗi người bàn giao test, dữ liệu demo, hướng dẫn, giới hạn và đoạn video phần mình. An ghép README/video/báo cáo test, thử cài đặt sạch và chuẩn bị bộ nộp.

### Kiểm thử và giao diện

- [ ] Chạy lại M2/M3: CRUD sản phẩm/danh mục/khách, tìm/lọc/phân trang, tạo/hủy/chuyển đơn, thuế/giảm giá, trừ kho, đăng nhập/đăng xuất/token. Tự động hóa luồng đăng nhập → tạo đơn → kiểm tra/trừ kho → đối chiếu doanh thu; chạy lại test bị ảnh hưởng sau sửa lỗi.
- [ ] Test biên: số lượng 0/âm/quá tồn, giá 0/âm, tên rỗng/quá dài, đơn rỗng, token hết hạn/giả, voucher hết hạn, hoàn quá mức và sai IMEI.
- [ ] Kiểm tra JSON, GET/POST/PUT/PATCH/DELETE và mã 200/201/204/400/401/403/404/409/500; lỗi không lộ thông tin. Test quyền cho phép/từ chối ở server, gồm Sales xem báo cáo và Warehouse sửa giá/tạo tài khoản.
- [ ] Mỗi test có mã, chức năng, input, expected, actual, trạng thái và build/OS; lưu ảnh/log cần thiết. Báo cáo Pass/Fail/Skipped/Chưa kiểm tra, tỷ lệ với mẫu số rõ, lỗi/cách sửa/kết quả chạy lại; chưa chạy không tính Pass.
- [ ] Review convention, phân lớp, validation, async/cancellation, xử lý lỗi và secrets. Client chỉ gọi API; cập nhật spec, caller, test, dữ liệu mẫu và migration/schema khi đổi.
- [ ] Chạy từng trang trên Windows native và Skia: binding, tiếng Việt/VND, bàn phím/focus, loading/empty/error/retry và validation theo trường.
- [ ] Kiểm tra Light/Dark/System, text scale, 800×600/1024×768/1440×1000 DIP; HighContrast trên Windows hỗ trợ. Ghi kết quả theo bản release.

### Dữ liệu demo

- [ ] Tối thiểu 20 sản phẩm, 5 danh mục, 10 khách, 20 đơn nhiều trạng thái; đủ tìm/lọc/phân trang/biểu đồ.
- [ ] Sản phẩm đủ tên/mã, giá bán/nhập, ảnh mở được, tồn, danh mục, trạng thái, specs. Đơn đủ mã/ngày/khách, dòng hàng/số lượng/đơn giá, giảm giá/thuế/tổng, phương thức/trạng thái; có thuế và giảm giá khác 0.
- [ ] Có nhập hàng, ca, Cash/QR, voucher/điểm, hoàn tiền/bảo hành và tài khoản bốn role khớp README. Dữ liệu báo cáo nằm trong kỳ demo; giữ fixture tháng 10/2026.
- [ ] Seed từ môi trường sạch và chạy lại không phá dữ liệu/mật khẩu; dùng dữ liệu/tài khoản demo riêng. Mỗi người bổ sung phần mình, An ghép kiểm tra.

### README và Git

- [ ] README đủ: tên/domain, tổng quan, chức năng/giới hạn, sơ đồ giữa kỳ/cuối kỳ (ghi đúng API-first từ đầu), công nghệ, hướng dẫn cài/build/chạy Windows/Skia/test, cấu hình DB/API/JWT/ảnh/VietQR/Gemini và tài khoản/quyền demo.
- [ ] Ghi họ tên, MSSV, vai trò, tỷ lệ đóng góp cả sáu người; tổng 100%, phản ánh M3 do An làm một mình.
- [ ] Repo private; commit/push thường xuyên, message rõ, lịch sử đúng đóng góp. Secrets dùng biến môi trường/config local có gitignore; kiểm tra file/lịch sử/ZIP/video, thu hồi secrets đã lộ. PAT chỉ nằm trong bộ nộp riêng.
- [ ] Ghi rõ thanh toán thật/sandbox/thủ công. AI là phần khuyến khích nhóm đã chọn; An xác minh chênh lệch điểm AI giữa mục 8 và 10 của đề.

### Bộ nộp do An tổng hợp

| File | Nội dung |
| --- | --- |
| `repo.txt` | Chỉ link repo private, người chấm truy cập được |
| `pat.txt` | PAT quyền đọc, còn hạn ít nhất 1 tháng; không commit |
| `youtube.txt` | Video Unlisted, mở được, demo toàn bộ tính năng và bốn role |
| `instructions.txt` | Cách build file thực thi, cấu hình DB/API/biến môi trường và chạy |
| `info.txt` | Phân công sáu người, % hoàn thành, chức năng xong/chưa xong, kỹ thuật đề xuất cộng điểm có bằng chứng |

- [ ] Nén đúng năm file thành `NhomXX_<MSSV_NhomTruong>.zip` hoặc tên giảng viên yêu cầu; không nộp trực tiếp source.
- [ ] Kiểm tra hướng dẫn/hạn Moodle, nộp trước hạn và xác nhận đúng file đã nhận.

Nghiệm thu M4: tính năng M2/M3 và sáu phần M4 hoạt động; tiền/kho/điểm/báo cáo khớp; các kiểm tra bắt buộc đạt trên bản release; đủ dữ liệu, tài khoản, tài liệu, video và bộ nộp. Ghi rõ mọi giới hạn còn lại.
