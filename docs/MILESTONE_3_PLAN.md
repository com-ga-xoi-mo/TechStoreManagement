# Milestone 3: Vận hành đa vai trò

Phụ trách: An thực hiện toàn bộ năm sprint, gồm backend, giao diện, test và tài liệu.

Mục tiêu: Admin quản lý tài khoản, WarehouseStaff nhập hàng, SalesStaff bán tiền mặt và Manager xem doanh thu. Bốn role phải được kiểm tra quyền tại server.

Thực hiện lần lượt các sprint dưới đây. [Roadmap](ROADMAP.md) ghi phạm vi chung; [M4](MILESTONE_4_PLAN.md) tiếp tục VietQR, đổi trả, bảo hành, điểm thưởng, voucher, báo cáo nâng cao và AI.

## Sprint 1: Phân quyền và tài khoản

- [ ] Lập bảng quyền cho bốn role: `Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`. Ghi rõ quyền xem, tạo, sửa, xóa và dữ liệu được đọc ở từng API.
- [ ] Thêm yêu cầu xác thực và quyền theo hành động trong `OrdersController.cs`. Bỏ `DefaultActingUserId`; lấy người thao tác từ token hợp lệ, từ chối claim người dùng thiếu hoặc sai.
- [ ] Cấu hình fallback policy bắt đăng nhập trong `Program.cs`. Chỉ cho phép truy cập không token ở endpoint public đã xác định, như login; kiểm tra riêng quyền role của các API nghiệp vụ.
- [ ] Thêm HTTP tests: Orders thiếu token trả 401, role không được phép trả 403, claim người dùng thiếu/sai bị từ chối; endpoint không khai báo policy riêng vẫn yêu cầu đăng nhập, login vẫn truy cập được.
- [ ] Thống nhất lỗi 401 khi chưa xác thực và 403 khi thiếu quyền. Không trả stack trace hoặc thông tin bí mật.
- [ ] Làm màn hình và API danh sách, tìm kiếm, tạo, sửa, khóa/mở khóa, gán role và đặt lại mật khẩu tài khoản. Chặn khóa hoặc hạ quyền Admin cuối cùng.
- [ ] Hoàn thiện login, logout và refresh token. Từ chối token hết hạn, giả mạo hoặc bị thu hồi; thay refresh token sau mỗi lần dùng. Khóa tài khoản hoặc đổi role phải làm mất quyền cũ.
- [ ] Bổ sung tài khoản Manager vào dữ liệu mẫu. Giữ tài khoản và mật khẩu người dùng đang có.
- [ ] Cập nhật spec và test cũ đang cho phép truy cập không token hoặc chỉ cho Admin.

Nghiệm thu: quản lý tài khoản được qua UI; gọi API trực tiếp vẫn bị chặn nếu thiếu quyền. Sales không xem tổng doanh thu; Warehouse không sửa giá hoặc tạo tài khoản.

## Sprint 2: Giao diện theo role và tra cứu bán hàng

- [ ] Chọn trang mở đầu, menu và nút thao tác theo role. Kiểm tra cả trường hợp mở thẳng một trang bị hạn chế.
- [ ] Tách cài đặt cá nhân như theme khỏi quyền sửa cấu hình cửa hàng.
- [ ] Xóa dữ liệu và hủy request cũ khi đăng xuất hoặc đổi tài khoản; không hiển thị kết quả trả về muộn của phiên trước.
- [ ] Hoàn thiện full-text và lọc kết hợp theo giá, thương hiệu, CPU, RAM và specs; giữ phân trang ổn định và tìm chính xác SKU/barcode/IMEI.
- [ ] Kiểm tra quyền trả về giá vốn trong DTO sản phẩm và serial. Không chỉ ẩn giá trên giao diện.
- [ ] Cho Sales tìm, chọn và tạo khách hàng phục vụ bán hàng. Quyền sửa, vô hiệu hóa và xem lịch sử phải theo bảng quyền đã chốt.
- [ ] Cho Manager xem dashboard thật; giữ chặn Sales và Warehouse. Đối chiếu doanh thu theo ngày Việt Nam, kể cả ngày không có đơn.
- [ ] Kiểm tra loading, dữ liệu rỗng, lỗi, retry, focus bàn phím, tiếng Việt, resize và đổi theme trên các trang đã sửa.

Nghiệm thu: bốn role vào đúng trang và dùng đúng chức năng; dữ liệu giữa các phiên không bị lẫn; tìm kiếm và chọn khách chạy qua API; Manager xem được số liệu thật.

## Sprint 3: Nhà cung cấp và nhập hàng

- [ ] Làm màn hình/API tìm, tạo, sửa và ngừng sử dụng nhà cung cấp; giữ lịch sử phiếu nhập.
- [ ] Làm danh sách, chi tiết và tạo phiếu nhập theo sản phẩm/biến thể. Server tính tổng từ số lượng và đơn giá nhập.
- [ ] Chốt trạng thái phiếu và quyền sửa/hủy. Hỗ trợ nhận một phần, nhận đủ và hủy phần chưa nhận.
- [ ] Khi nhận hàng, chặn số lượng vượt phần còn lại và chặn ghi nhận lại cùng lần nhận. Lưu người nhận, thời điểm và chứng từ.
- [ ] Nhập Serial/IMEI đúng sản phẩm/biến thể; kiểm tra định dạng và trùng trong file gửi lên hoặc database. Ghi giá vốn từng máy và số tháng bảo hành.
- [ ] Cập nhật phiếu, tồn kho, serial và lịch sử kho trong cùng transaction; giữ thứ tự khóa dòng thống nhất với luồng bán.
- [ ] Lưu giá vốn nhập của hàng không serial để dùng lúc bán. Giữ nguyên số dư đầu kỳ và lịch sử cũ.
- [ ] Test nhập thiếu/thừa, IMEI trùng, giá hoặc số lượng sai, gửi lại request và lỗi giữa giao dịch.

Nghiệm thu: nhận được hàng có và không có serial; phiếu, giá vốn và tồn kho khớp. Lỗi một dòng phải rollback toàn bộ lần nhận; gửi lại request không cộng kho lần hai.

## Sprint 4: POS tiền mặt và ca thu ngân

- [ ] Chốt ý nghĩa trạng thái đơn và thời điểm ghi nhận tiền. Hiện `Pending` không giữ hàng, `Processing` đã ghi `PaidAt`; cập nhật cả code, spec và test nếu thay đổi.
- [ ] Làm màn bán hàng: quét barcode/Serial/IMEI, thêm/xóa dòng, đổi số lượng, chọn khách hoặc khách lẻ. Chặn IMEI trùng trong giỏ và máy đã bán.
- [ ] Mở ca với tiền đầu ca; gắn đơn với người bán và ca. Chặn bán vào ca đã đóng hoặc ca của người khác nếu không được phép.
- [ ] Chốt và triển khai thứ tự giảm giá dòng, giảm giá đơn, thuế và làm tròn. Kiểm tra quyền giảm giá; server tính tổng chính thức và lưu giá tại thời điểm bán.
- [ ] Sửa `OrderPricingHelper.Calculate` để thuế và giảm giá cấp đơn không còn cố định bằng 0; dùng lại giảm giá dòng đã có. Cập nhật request, service, UI và test liên quan; có ca thuế/giảm giá cấp đơn khác 0 và kiểm tra không trừ giảm giá dòng hai lần.
- [ ] Thu tiền mặt và tính tiền thừa. Hoàn tất đơn, ghi tiền ca, trừ kho, ghi lịch sử, lưu giá vốn và kích hoạt bảo hành trong cùng giao dịch.
- [ ] Chặn ghi nhận thanh toán lặp khi nhấn hai lần hoặc thử lại sau lỗi mạng.
- [ ] Đóng ca với tiền dự kiến, tiền thực đếm và lý do chênh lệch.
- [ ] Giữ hủy đơn nháp và các bước chuyển trạng thái hợp lệ. Đơn đã thanh toán phải theo quy trình hoàn tiền M4; không cho đánh dấu QR đã trả tiền khi chưa có xác nhận.
- [ ] Hiển thị đủ chi tiết đơn: mã, ngày, khách, mặt hàng, số lượng, đơn giá, giảm giá, thuế, tổng tiền, phương thức và trạng thái.
- [ ] Test đơn rỗng, số lượng 0/âm/quá tồn, giả giá từ client, giảm giá vượt giá trị, thuế sai, làm tròn, tiền nhận thiếu và ca đã đóng.

Nghiệm thu: chạy được nhập hàng, bán tiền mặt, cập nhật kho/bảo hành và xem doanh thu. Tiền ca và đơn khớp; giao dịch lỗi không để lại tiền hoặc tồn kho cập nhật dở dang.

## Sprint 5: Kiểm thử và bàn giao

- [ ] Chạy lại CRUD, tìm kiếm, lọc, phân trang và các luồng Admin của M2; kiểm tra tên rỗng/quá dài, giá 0/âm và dữ liệu không hợp lệ.
- [ ] Test login/logout, token hết hạn/giả mạo/thu hồi, khóa tài khoản và đổi quyền qua HTTP thật.
- [ ] Đổi mô tả ca “race” hiện tại trong `OrdersApiIntegrationTests.cs` thành kiểm tra xung đột tuần tự. Thêm ca hai đơn tranh cùng IMEI bằng hai request/giao dịch độc lập có điểm đồng bộ để thực sự chạy đồng thời trên PostgreSQL; đúng một đơn hoàn tất, đơn còn lại nhận 409, tồn và lịch sử bán chỉ thay đổi một lần.
- [ ] Dùng hai giao dịch PostgreSQL chạy đồng thời để thử tranh IMEI cuối, phụ kiện cuối, chuyển trạng thái cùng đơn, nhận cùng phiếu và bán trong lúc đóng ca.
- [ ] Kiểm tra tồn, hàng giữ chỗ, serial, lịch sử kho và tiền sau commit/rollback. Không tính hai request chạy tuần tự là kiểm thử đồng thời.
- [ ] Kiểm tra HTTP methods, JSON và mã lỗi 200/201/204/400/401/403/404/409/500 phù hợp; thử lỗi mạng và phục hồi giao diện.
- [ ] Chạy UI trên Windows native và Skia. Kiểm tra Light/Dark/System, resize, bàn phím và binding; ghi build, hệ điều hành, ảnh hoặc log kết quả.
- [ ] Chuẩn bị ít nhất 20 sản phẩm, 5 danh mục, 10 khách, 20 đơn nhiều trạng thái và bốn tài khoản demo. Sản phẩm có ảnh và giá vốn; đơn có đủ chi tiết và dữ liệu để vẽ biểu đồ.
- [ ] Kiểm tra seed chạy lại không đổi mật khẩu hoặc phá dữ liệu. Chọn kỳ báo cáo có dữ liệu, giữ các ngày cố định mà test hiện tại đang dùng.
- [ ] Cập nhật README cách chạy và tài khoản demo. Bàn giao API, bảng quyền, trạng thái đơn/ca, công thức tiền, thứ tự khóa và cách migrate/seed cho nhóm M4.
- [ ] Ghi tổng test Pass/Fail/Skipped/Chưa kiểm tra, lỗi đã sửa và kết quả kiểm tra lại theo bản build bàn giao.

Dùng database test riêng. Mỗi test case cần có đầu vào, kết quả mong đợi, kết quả thực tế, trạng thái và ảnh/log nếu cần. Test bị bỏ qua hoặc chưa chạy không được tính là đạt.

## Điều kiện hoàn thành M3

- [ ] Bốn role dùng được hệ thống đúng quyền; mọi API nghiệp vụ kiểm tra quyền ở server.
- [ ] Luồng nhập hàng, mở ca, bán tiền mặt, trừ kho, bảo hành và doanh thu chạy đúng.
- [ ] Test đồng thời đạt, không bán trùng IMEI, âm kho hoặc ghi nhận tiền/nhập hàng hai lần.
- [ ] Các luồng M2 vẫn hoạt động; không còn lỗi chặn bán hàng, sai tiền, sai tồn hoặc vượt quyền.
- [ ] Có kết quả kiểm thử, dữ liệu demo và tài liệu để nhóm tiếp tục M4.

Giữ kiến trúc và quy ước hiện có. Khi thay API hoặc schema, cập nhật các nơi gọi, spec, test, dữ liệu mẫu, migration và schema.dbml liên quan; secrets chỉ đặt trong cấu hình local hoặc biến môi trường.
