# Milestone 3: Vận hành đa vai trò

Phụ trách: An, Q. Vinh, Bình, Đạt, Vy và T. Vinh. Mỗi người làm API, giao diện, test, dữ liệu mẫu và tài liệu của phần mình; An tổng hợp bản bàn giao.

Mục tiêu: Admin quản lý tài khoản, WarehouseStaff nhập hàng, SalesStaff bán tiền mặt và Manager xem doanh thu. Bốn role phải được kiểm tra quyền tại server.

Giữ năm sprint làm các mốc nghiệm thu. Các phần độc lập ở sprint 2–4 có thể làm song song ngay khi chốt quyền và contract cần dùng ở sprint 1; sprint 5 kiểm tra trên bản tích hợp thật. [Roadmap](ROADMAP.md) ghi phạm vi chung; [M4](MILESTONE_4_PLAN.md) tiếp tục VietQR, đổi trả, bảo hành, điểm thưởng, voucher, báo cáo nâng cao và AI.

## Phân công

Mỗi logic có một người viết và sửa. Người phụ trách làm API, UI, test, dữ liệu demo và tài liệu; bên gọi dùng service chung, không viết lại logic của người khác.

| Người | Logic sở hữu | Phạm vi code và bàn giao |
| --- | --- | --- |
| An | Phân quyền, tài khoản và phiên đăng nhập | Identity, JWT/refresh/logout, bảng quyền, UI quản lý tài khoản; ghép cấu hình chung, migration và tài liệu |
| Q. Vinh | Nhà cung cấp, phiếu nhập và nhận hàng | Inventory, UI nhập hàng, Serial/IMEI, giá vốn, lịch sử kho và thao tác kho dùng chung cho POS |
| Bình | Dashboard Manager, dữ liệu demo và đối chiếu | Analytics, UI dashboard, báo cáo theo ngày Việt Nam, bộ dữ liệu tính tay; tổng hợp kết quả kiểm thử |
| Đạt | POS tiền mặt, vòng đời đơn và ca thu ngân | Orders/Sales, UI quét và giỏ hàng, thu tiền/tiền thừa, mở/đóng ca; điều phối transaction bán hàng |
| Vy | Khách hàng cho Sales, thuế và giảm giá | Customers, UI chọn/tạo khách; công thức tiền duy nhất tại server, validation và phân bổ tiền từng dòng |
| T. Vinh | Giao diện theo role và tra cứu bán hàng | Shell, điều hướng, cài đặt cá nhân, hủy dữ liệu UI phiên cũ; Catalog search, SKU/barcode/IMEI và bảo vệ giá vốn sản phẩm |

Đạt điều phối transaction bán hàng; Q. Vinh phụ trách thao tác kho; Vy phụ trách công thức tiền. Service được gọi tham gia cùng UnitOfWork, không mở transaction lồng nhau. UI hiển thị kết quả server.

Mỗi người tự test và sửa lỗi phần mình. Bình tổng hợp kết quả kiểm thử, T. Vinh tổng hợp kiểm tra UI hai nền tảng; An ghép cấu hình chung, migration và bản bàn giao theo thứ tự phụ thuộc để tránh xung đột snapshot.

## Làm tính năng (Sprint 1–4)

Chốt quyền và contract cần dùng ở Sprint 1; các phần độc lập của Sprint 2–4 có thể làm song song. Làm sớm API/service người khác cần gọi, kèm ví dụ input/output và lỗi trả về. Khi chưa xong có thể dùng fake đúng contract; nghiệm thu phải dùng API và database thật. Chưa chốt ngày từng sprint vì chưa có hạn nộp.

### An: Phân quyền, tài khoản và phiên đăng nhập

Thực hiện ở Sprint 1. Bàn giao bảng quyền, actor từ token, lỗi 401/403 và contract phiên cho cả nhóm trước khi ghép API. Tiếp tục change `identity-access-management` đã có; không làm lại hoặc đánh dấu hoàn thành khi chưa có bằng chứng.

- [x] Lập bảng quyền cho bốn role: `Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`. Ghi rõ quyền xem, tạo, sửa, xóa và dữ liệu được đọc ở từng API.
- [x] Thêm yêu cầu xác thực và quyền theo hành động trong `OrdersController.cs`. Bỏ `DefaultActingUserId`; lấy người thao tác từ token hợp lệ, từ chối claim người dùng thiếu hoặc sai.
- [x] Cấu hình fallback policy bắt đăng nhập trong `Program.cs`. Chỉ cho phép truy cập không token ở endpoint public đã xác định, như login; kiểm tra riêng quyền role của các API nghiệp vụ.
- [x] Thêm HTTP tests: Orders thiếu token trả 401, role không được phép trả 403, claim người dùng thiếu/sai bị từ chối; endpoint không khai báo policy riêng vẫn yêu cầu đăng nhập, login vẫn truy cập được.
- [x] Thống nhất lỗi 401 khi chưa xác thực và 403 khi thiếu quyền. Không trả stack trace hoặc thông tin bí mật.
- [ ] Làm màn hình và API danh sách, tìm kiếm, tạo, sửa, khóa/mở khóa, gán role và đặt lại mật khẩu tài khoản. Chặn khóa hoặc hạ quyền Admin cuối cùng.
- [x] Hoàn thiện login, logout và refresh token. Từ chối token hết hạn, giả mạo hoặc bị thu hồi; thay refresh token sau mỗi lần dùng. Khóa tài khoản hoặc đổi role phải làm mất quyền cũ.
- [x] Bổ sung tài khoản Manager vào dữ liệu mẫu. Giữ tài khoản và mật khẩu người dùng đang có.
- [x] Cập nhật spec và test cũ đang cho phép truy cập không token hoặc chỉ cho Admin; người sở hữu module cập nhật caller và test phần mình theo bảng quyền.

Nghiệm thu: Quản lý tài khoản được qua UI; API trực tiếp vẫn chặn thiếu token hoặc sai quyền. Khóa tài khoản/đổi role làm mất quyền cũ; không khóa hoặc hạ quyền Admin cuối cùng.

Bằng chứng ngày 12/11/2026: [báo cáo kiểm chứng Sprint 1](verification/identity-access-management-2026-11-12/README.md). API, headless và runtime theme đã đạt; role/focus và chặn Admin cuối cùng đã được nghiệm thu thêm trên UI; còn bước nhập/gửi mật khẩu mới đang bàn giao người dùng. Windows native chưa chạy trên máy macOS. Chưa đánh dấu hoàn tất Sprint 1.

### Q. Vinh: Nhà cung cấp, phiếu nhập và nhận hàng

Bảo vệ giá vốn serial ở Sprint 2, triển khai nhập hàng ở Sprint 3. Chốt với Đạt thứ tự khóa chung và contract kiểm tra/trừ kho, giá vốn, lịch sử và bảo hành. Dùng lại `IInventoryFulfillmentService`; Q. Vinh sửa logic kho, Đạt gọi từ luồng bán.

- [ ] Áp dụng bảng quyền đã chốt cho giá vốn trong DTO serial. Không chỉ ẩn giá trên giao diện.
- [ ] Làm màn hình/API tìm, tạo, sửa và ngừng sử dụng nhà cung cấp; giữ lịch sử phiếu nhập.
- [ ] Làm danh sách, chi tiết và tạo phiếu nhập theo sản phẩm/biến thể. Server tính tổng từ số lượng và đơn giá nhập.
- [ ] Chốt trạng thái phiếu và quyền sửa/hủy. Hỗ trợ nhận một phần, nhận đủ và hủy phần chưa nhận.
- [ ] Khi nhận hàng, chặn số lượng vượt phần còn lại và chặn ghi nhận lại cùng lần nhận. Lưu người nhận, thời điểm và chứng từ.
- [ ] Nhập Serial/IMEI đúng sản phẩm/biến thể; kiểm tra định dạng và trùng trong file gửi lên hoặc database. Ghi giá vốn từng máy và số tháng bảo hành.
- [ ] Cập nhật phiếu, tồn kho, serial và lịch sử kho trong cùng transaction; giữ thứ tự khóa dòng thống nhất với luồng bán.
- [ ] Lưu giá vốn nhập của hàng không serial để dùng lúc bán. Giữ nguyên số dư đầu kỳ và lịch sử cũ.
- [ ] Test nhập thiếu/thừa, IMEI trùng, giá hoặc số lượng sai, gửi lại request và lỗi giữa giao dịch.

Nghiệm thu: Nhận được hàng có và không có serial; phiếu, giá vốn, tồn và lịch sử khớp. Lỗi một dòng rollback toàn bộ lần nhận; gửi lại request không cộng kho lần hai.

### Bình: Dashboard Manager và đối chiếu số liệu

Triển khai dashboard ở Sprint 2. Dựng bộ dữ liệu tính tay trước, sau đó thay bằng giao dịch thật. Chốt với Đạt trạng thái đơn, thời điểm ghi nhận doanh thu, dữ liệu đơn/ca và quy tắc ngày Việt Nam; đọc số tiền đã lưu do Vy tính để đối chiếu.

- [ ] Cho Manager xem dashboard thật; giữ chặn Sales và Warehouse. Đối chiếu doanh thu theo ngày Việt Nam, kể cả ngày không có đơn.

Nghiệm thu: Manager xem được số liệu thật, Sales/Warehouse bị chặn; doanh thu khớp bộ dữ liệu tính tay, đúng ngày Việt Nam và cả ngày không có đơn.

### Đạt: POS tiền mặt, đơn hàng và ca thu ngân

Triển khai ở Sprint 4. Nhận tìm sản phẩm/IMEI từ T. Vinh, chọn/tạo khách và công thức tiền từ Vy, thao tác kho từ Q. Vinh. Lưu giá và phân bổ tiền tại thời điểm bán, trả DTO cho UI và dữ liệu đơn/ca cho Bình; không viết lại công thức hoặc logic kho.

- [ ] Chốt ý nghĩa trạng thái đơn và thời điểm ghi nhận tiền. Hiện `Pending` không giữ hàng, `Processing` đã ghi `PaidAt`; cập nhật cả code, spec và test nếu thay đổi.
- [ ] Làm màn bán hàng: quét barcode/Serial/IMEI, thêm/xóa dòng, đổi số lượng, chọn khách hoặc khách lẻ. Chặn IMEI trùng trong giỏ và máy đã bán.
- [ ] Mở ca với tiền đầu ca; gắn đơn với người bán và ca. Chặn bán vào ca đã đóng hoặc ca của người khác nếu không được phép.
- [ ] Thu tiền mặt và tính tiền thừa. Gọi công thức của Vy và service kho do Q. Vinh phụ trách; hoàn tất đơn, ghi tiền ca, trừ kho, ghi lịch sử, lưu giá vốn và kích hoạt bảo hành trong cùng giao dịch.
- [ ] Chặn ghi nhận thanh toán lặp khi nhấn hai lần hoặc thử lại sau lỗi mạng.
- [ ] Đóng ca với tiền dự kiến, tiền thực đếm và lý do chênh lệch.
- [ ] Giữ hủy đơn nháp và các bước chuyển trạng thái hợp lệ. Đơn đã thanh toán phải theo quy trình hoàn tiền M4; không cho đánh dấu QR đã trả tiền khi chưa có xác nhận.
- [ ] Hiển thị đủ chi tiết đơn: mã, ngày, khách, mặt hàng, số lượng, đơn giá, giảm giá, thuế, tổng tiền, phương thức và trạng thái.
- [ ] Test đơn rỗng, số lượng 0/âm/quá tồn, giả giá từ client, giảm giá vượt giá trị, thuế sai, làm tròn, tiền nhận thiếu và ca đã đóng.

Nghiệm thu: Mở ca → bán tiền mặt → trừ kho/kích hoạt bảo hành → xem doanh thu chạy đúng; tiền đơn và tiền ca khớp. Lỗi phải rollback, gửi lặp không ghi tiền hoặc trừ kho hai lần.

### Vy: Khách hàng, thuế và giảm giá

Làm khách hàng ở Sprint 2, công thức tiền ở Sprint 4. Bàn giao chọn/tạo khách cho Đạt; chốt thứ tự giảm dòng → giảm đơn → thuế, làm tròn và phân bổ từng dòng trong `OrderPricingHelper` cho Đạt và Bình. Đạt lưu kết quả, Bình đọc để đối chiếu.

- [ ] Cho Sales tìm, chọn và tạo khách hàng phục vụ bán hàng. Quyền sửa, vô hiệu hóa và xem lịch sử phải theo bảng quyền đã chốt.
- [ ] Chốt và triển khai thứ tự giảm giá dòng, giảm giá đơn, thuế và làm tròn. Kiểm tra quyền giảm giá; server tính tổng chính thức và phân bổ từng dòng. Đạt lưu giá và kết quả tính tại thời điểm bán trong luồng checkout.
- [ ] Sửa `OrderPricingHelper.Calculate` để thuế và giảm giá cấp đơn không còn cố định bằng 0; dùng lại giảm giá dòng đã có. Cập nhật contract và test công thức; Đạt nối request, service và UI POS theo contract này; có ca thuế/giảm giá cấp đơn khác 0 và kiểm tra không trừ giảm giá dòng hai lần.

Nghiệm thu: Sales tìm/chọn/tạo khách đúng quyền. Server tính đúng thuế và giảm giá khác 0, phân bổ/làm tròn nhất quán; không trừ giảm giá dòng hai lần.

### T. Vinh: Giao diện theo role và tra cứu bán hàng

Triển khai ở Sprint 2. Bàn giao tìm sản phẩm/IMEI cho Đạt. Dùng session generation/cancellation của An để bảo vệ route, hủy request và chặn dữ liệu trả muộn của phiên trước; phối hợp Q. Vinh áp dụng cùng bảng quyền cho giá vốn.

- [ ] Chọn trang mở đầu, menu và nút thao tác theo role. Kiểm tra cả trường hợp mở thẳng một trang bị hạn chế.
- [ ] Tách cài đặt cá nhân như theme khỏi quyền sửa cấu hình cửa hàng.
- [ ] Xóa dữ liệu và hủy request cũ khi đăng xuất hoặc đổi tài khoản; không hiển thị kết quả trả về muộn của phiên trước.
- [ ] Hoàn thiện full-text và lọc kết hợp theo giá, thương hiệu, CPU, RAM và specs; giữ phân trang ổn định và tìm chính xác SKU/barcode/IMEI.
- [ ] Kiểm tra quyền trả về giá vốn trong DTO sản phẩm. Không chỉ ẩn giá trên giao diện.
- [ ] Kiểm tra loading, dữ liệu rỗng, lỗi, retry, focus bàn phím, tiếng Việt, resize và đổi theme trên các trang đã sửa.

Nghiệm thu: Bốn role vào đúng trang, dùng đúng chức năng; mở thẳng route trái quyền bị chặn. Tìm/lọc/phân trang qua API thật, không lẫn dữ liệu giữa các phiên; UI đọc được ở các theme và kích thước đã kiểm tra.

## Tích hợp và kiểm tra toàn luồng

| Người | Việc cần hoàn thành |
| --- | --- |
| An | Ghép quyền, actor và phiên đăng nhập; kiểm tra API nghiệp vụ theo bảng quyền |
| Q. Vinh | Nối kho với POS; đối chiếu tồn, IMEI, giá vốn và lịch sử nhập/bán |
| Bình | Chạy dashboard bằng giao dịch thật; đối chiếu doanh thu theo ngày Việt Nam |
| Đạt | Nối tìm hàng, khách, công thức tiền và kho thật; chạy mở ca, bán Cash, đóng ca |
| Vy | Kiểm tra chọn/tạo khách và công thức tiền trong transaction checkout thật |
| T. Vinh | Chạy bốn role, tìm kiếm và chuyển phiên trên UI; kiểm tra binding và theme |

Chạy luồng nhập hàng → mở ca → tìm hàng/chọn khách → bán tiền mặt → kiểm tra tồn/IMEI/bảo hành → đối chiếu ca và dashboard. Mỗi người sửa lỗi logic mình sở hữu; bên gọi không viết bản thay thế. Sprint 5 nghiệm thu trên bản tích hợp thật.

## Sprint 5: Kiểm thử và bàn giao

Mỗi người bàn giao kết quả test, dữ liệu demo và tài liệu phần mình. Bình tổng hợp kết quả, T. Vinh tổng hợp kiểm tra UI; An ghép README và bản bàn giao M4.

### Kiểm thử nghiệp vụ, quyền và giao diện

- [ ] **An:** Test login/logout, token hết hạn/giả mạo/thu hồi, khóa tài khoản và đổi quyền qua HTTP thật.
- [ ] **Q. Vinh:** Dùng hai giao dịch PostgreSQL chạy đồng thời để thử tranh IMEI cuối, phụ kiện cuối và nhận cùng phiếu. Bình tổng hợp bằng chứng kết quả từng ca.
- [ ] **Bình:** Chạy lại CRUD, tìm kiếm, lọc, phân trang và các luồng Admin của M2; kiểm tra tên rỗng/quá dài, giá 0/âm và dữ liệu không hợp lệ.
- [ ] **Bình:** Kiểm tra tồn, hàng giữ chỗ, serial, lịch sử kho và tiền sau commit/rollback. Không tính hai request chạy tuần tự là kiểm thử đồng thời.
- [ ] **Đạt:** Đổi mô tả ca “race” hiện tại trong `OrdersApiIntegrationTests.cs` thành kiểm tra xung đột tuần tự. Thêm ca hai đơn tranh cùng IMEI bằng hai request/giao dịch độc lập có điểm đồng bộ để thực sự chạy đồng thời trên PostgreSQL; đúng một đơn hoàn tất, đơn còn lại nhận 409, tồn và lịch sử bán chỉ thay đổi một lần.
- [ ] **Đạt:** Dùng hai giao dịch PostgreSQL chạy đồng thời để thử chuyển trạng thái cùng đơn và bán trong lúc đóng ca. Bình tổng hợp bằng chứng kết quả từng ca.
- [ ] **T. Vinh:** Kiểm tra HTTP methods, JSON và mã lỗi 200/201/204/400/401/403/404/409/500 phù hợp; thử lỗi mạng và phục hồi giao diện.
- [ ] **T. Vinh:** Chạy UI trên Windows native và Skia. Kiểm tra Light/Dark/System, resize, bàn phím và binding; ghi build, hệ điều hành, ảnh hoặc log kết quả.

Dùng database test riêng. Mỗi test case cần có đầu vào, kết quả mong đợi, kết quả thực tế, trạng thái và ảnh/log nếu cần. Test bị bỏ qua hoặc chưa chạy không được tính là đạt.

### Dữ liệu demo

- [ ] **Bình:** Chuẩn bị ít nhất 20 sản phẩm, 5 danh mục, 10 khách, 20 đơn nhiều trạng thái và bốn tài khoản demo. Sản phẩm có ảnh và giá vốn; đơn có đủ chi tiết và dữ liệu để vẽ biểu đồ.
- [ ] **Bình:** Kiểm tra seed chạy lại không đổi mật khẩu hoặc phá dữ liệu. Chọn kỳ báo cáo có dữ liệu, giữ các ngày cố định mà test hiện tại đang dùng.

### Tài liệu và kết quả kiểm thử

- [ ] **An:** Cập nhật README cách chạy và tài khoản demo. Bàn giao API, bảng quyền, trạng thái đơn/ca, công thức tiền, thứ tự khóa và cách migrate/seed cho nhóm M4.
- [ ] **Bình:** Ghi tổng test Pass/Fail/Skipped/Chưa kiểm tra, lỗi đã sửa và kết quả kiểm tra lại theo bản build bàn giao.

## Mốc nghiệm thu

| Sprint | Trọng tâm | Điều kiện nghiệm thu |
| --- | --- | --- |
| 1 | Phân quyền và tài khoản | Quản lý tài khoản qua UI; API trực tiếp chặn thiếu/sai quyền; Sales không xem tổng doanh thu, Warehouse không sửa giá/tạo tài khoản |
| 2 | Giao diện theo role và tra cứu | Bốn role dùng đúng chức năng; không lẫn phiên; tìm kiếm/chọn khách qua API, Manager xem dashboard thật |
| 3 | Nhà cung cấp và nhập hàng | Nhận hàng có/không serial; phiếu, giá vốn và tồn khớp; lỗi rollback, gửi lặp không cộng kho hai lần |
| 4 | POS tiền mặt và ca | Nhập hàng, mở ca, bán Cash, kho/bảo hành và doanh thu khớp; lỗi không để tiền hoặc tồn cập nhật dở dang |
| 5 | Kiểm thử và bàn giao | Có test trên bản tích hợp thật, kiểm tra đồng thời, UI hai nền tảng, dữ liệu demo và tài liệu tiếp tục M4 |

## Điều kiện hoàn thành M3

- [ ] **An:** Bốn role dùng được hệ thống đúng quyền; mọi API nghiệp vụ kiểm tra quyền ở server.
- [ ] **Đạt:** Luồng nhập hàng, mở ca, bán tiền mặt, trừ kho, bảo hành và doanh thu chạy đúng.
- [ ] **Q. Vinh:** Test đồng thời đạt, không bán trùng IMEI, âm kho hoặc ghi nhận tiền/nhập hàng hai lần.
- [ ] **T. Vinh:** Các luồng M2 vẫn hoạt động; không còn lỗi chặn bán hàng, sai tiền, sai tồn hoặc vượt quyền.
- [ ] **An:** Có kết quả kiểm thử, dữ liệu demo và tài liệu để nhóm tiếp tục M4.

Giữ kiến trúc và quy ước hiện có. Khi thay API hoặc schema, cập nhật các nơi gọi, spec, test, dữ liệu mẫu, migration và schema.dbml liên quan; secrets chỉ đặt trong cấu hình local hoặc biến môi trường.
