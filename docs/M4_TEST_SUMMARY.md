# Nghiệm thu phần trả hàng và hoàn tiền M4

Ngày 24/11/2026. Kiểm tra trên nhánh `feature/m4-returns-refunds-and-inventory`, nền M3 `6c31870`, macOS 26.2 arm64, .NET 10.0.401 và PostgreSQL 16.15. Test HTTP dùng tài khoản theo role, API thật và database riêng. Probe Skia gắn các trang trả hàng và kiểm định vào cửa sổ Uno thật và dùng HTTP fixture để lặp lại các trạng thái giao diện.

## Kết quả trên bản Release

| Bộ test | Tổng | Đạt | Lỗi | Bỏ qua |
| --- | ---: | ---: | ---: | ---: |
| Backend unit, HTTP và PostgreSQL | 926 | 926 | 0 | 0 |
| Frontend unit | 1157 | 1157 | 0 | 0 |
| Backend integration | 4 | 4 | 0 | 0 |
| Frontend integration qua Kestrel | 4 | 4 | 0 | 0 |
| **Tổng** | 2091 | 2091 | 0 | 0 |

Tỷ lệ đạt là 2091/2091 (100%). Ngoài bốn bộ test này, giao diện Skia Release đạt 20/20 phép kiểm tra với bốn role. Build Skia Release có 0 warning và 0 error. EF Core xác nhận model khớp migration mới nhất. Theo phạm vi đã chốt, phần giao diện của An được nghiệm thu trên Skia/macOS.

Bằng chứng trên máy chạy nằm trong `.local/m4-returns/test-results/` (bốn tệp TRX, log từng suite và `reconciliation.txt`). Kết quả giao diện nằm trong `.local/m4-returns/skia-release-probe.log`.

## Ca kiểm thử nghiệp vụ

| Mã | Đầu vào và hành vi cần kiểm tra | Kết quả quan sát |
| --- | --- | --- |
| RT-01 | Đăng nhập Sales, xem đơn mình bán và đơn của Sales khác, gửi sai số lượng/IMEI, gọi duyệt chi | Phạm vi đơn trả 404 khi truy cập chéo, sai input trả lỗi theo dòng, duyệt chi trả 403. Gửi lặp yêu cầu hợp lệ giữ cùng mã phiếu. |
| RT-02 | Bán hai món giá 1.000.000đ, giảm 200.000đ và VAT 10%, trả từng món, gửi lại lệnh Cash | Thu 1.980.000đ và cộng 18 điểm. Mỗi phiếu hoàn 990.000đ, đảo 9 điểm đúng một lần. Số dư trước bán được bảo toàn. |
| RT-03 | Bán qua ca Sales. Manager mở ca chi 2.000.000đ, hoàn hai lần rồi đóng ca | Ca Manager còn 20.000đ, chênh lệch bằng 0. Ca Sales vẫn ghi doanh số 1.980.000đ. |
| RT-04 | Hai lệnh chi cùng tranh phần còn lại. Kiểm tra mất phản hồi và thử lại chứng từ ngân hàng | Chỉ một lượt giữ ngân hàng hợp lệ. Lỗi DB sau khi xác minh giữ phiếu ở AwaitingBank, chưa ghi OrderReturn. Đối soát lại cùng mã chi chỉ ghi một kết quả. |
| RT-05 | Hoàn đơn Processing chưa giao và đồng thời yêu cầu giao hàng | Chỉ hoàn toàn bộ. Ca xử lý giải phóng giữ chỗ giữ nguyên tồn vật lý, điểm và CompletedAt. Đường giao hàng kiểm tra lượt hoàn dưới khóa. |
| RT-06 | Trả khiếu nại và nhận bảo hành tranh cùng IMEI đã bán | Hai kết nối PostgreSQL tranh cùng hàng. Chỉ một thao tác nhận máy thành công. Khi đổi máy đúng hồ sơ, tồn giảm một, movement liên kết giá vốn máy thay và giữ hạn bảo hành. |
| RT-07 | Trả lô ba đơn vị, chia bán lại được/lỗi/sửa. Gửi lại lần kiểm định | Một movement CustomerReturn +1, hai phần còn lại chưa tăng tồn. Tổng chi, thuế và thời điểm chi giữ nguyên sau tách dòng. |
| RT-08 | Đối chiếu giá vốn 600.000đ/món sau lần hoàn thứ nhất trước và sau nhập lại | Lãi gộp lũy kế lần lượt −300.000đ và 300.000đ. Ngày thực nhập có thể khác ngày hoàn. Trường hợp thiếu giá vốn phân biệt được với giá vốn 0. |
| RT-09 | Checkout đổi hàng lỗi, gửi lại bằng mã phiếu đã hoàn | Giao dịch bán mới thành công một lần, history dẫn tới đúng đơn mới, không chi tiền lần nữa. |
| RT-10 | Nâng DB M3 có khoản hoàn cũ, phân bổ thiếu hoặc mặc định và đơn Processing | Chứng từ cũ còn đọc được. Đơn thiếu số liệu hiển thị lý do cần kiểm tra, đơn đang giữ có bản ghi theo dòng gốc. |
| RT-11 | Tạo dữ liệu từ DB sạch, seed lại, đối chiếu bốn role và kỳ dashboard M3 | 20 sản phẩm, 5 danh mục, 10 khách, 21 đơn. Fixture M3 và dữ liệu phát sinh sau seed giữ được. |
| RT-12 | Sales hủy hoặc cho hết hạn một giao dịch VietQR còn giữ tồn, thử lại và gọi từ tài khoản khác | Server kiểm tra chủ giao dịch, giải phóng giữ chỗ đúng một lần. Giao dịch có voucher hoặc bằng chứng thanh toán chưa rõ trả 409 và giữ chỗ để đối soát. |
| RT-13 | Nhận bảo hành, trả máy cũ hoặc xuất máy thay, gửi lại cùng yêu cầu | Hồ sơ giữ khách, dòng bán và hạn gốc. Xuất máy thay ghi một movement cùng giá vốn. Dòng đã hoàn của đơn cũ bị chặn, dòng khách còn giữ vẫn được nhận. |
| RT-14 | Xem dashboard và lịch sử mua của khách sau hoàn từng phần, hoàn hết và hoàn trước giao | Đơn từng giao vẫn giữ doanh số bán gốc, ngày mua và dòng đã mua. Truy vấn đối chiếu khấu trừ khoản hoàn theo ngày chi. Hoàn trước giao không tạo doanh số bán. |
| RT-15 | Lập phiếu trước ngày duyệt hai ngày, gửi chứng từ trước mốc duyệt một ngày hoặc một microgiây, sau đó gửi chứng từ đúng mốc | Các lần gửi sớm trả 400 và chưa ghi khoản hoàn. Chứng từ đúng mốc được chấp nhận. Duyệt lặp giữ nguyên thời điểm duyệt. |
| RT-16 | Nâng cấp PostgreSQL khi còn phiếu chờ ngân hàng của phiên bản cũ | Phiếu vẫn chờ. Khi xác nhận đã chi, quản lý phải nhập mốc duyệt đã xác minh từ chứng từ gốc. Gửi lại kết quả giữ nguyên một khoản hoàn. |
| UI-01 | Mở trang theo bốn role ở ba cỡ 800×600, 1024×768 và 1440×1000 DIP | Probe Skia kiểm tra bố cục, nút theo quyền, focus, Light/Dark, nhập tiền/giờ và mở lại lô đang kiểm định. Log và clip nằm trong thư mục .local/m4-returns. |
| UI-02 | Chuyển từ lô kiểm định theo số lượng sang máy có IMEI rồi chọn lại lô | Giao diện hiện SKU và IMEI của máy, khóa số lượng ở 1. Khi chọn lại lô, ô số lượng cho nhập tiếp. |
| UI-03 | Chọn giữa hai biến thể cùng tên và giá rồi xem lại phiếu | SKU và tên biến thể phân biệt được hai dòng. Lịch sử phiếu mới và phiếu cũ đều dùng thông tin dòng bán gốc. |
| UI-04 | Tải danh sách kho khi API lỗi, thử lại với danh sách trống rồi tải hàng đang chờ | Giao diện báo lỗi ở lần đầu. Thông báo trống chỉ xuất hiện sau lần tải thành công với 0 dòng. Lần tiếp theo hiển thị lô có thể kiểm định. |

Mã test và fixture nằm trong [nhóm HTTP hoàn tiền](../tests/Backend.UnitTests/PosCheckoutPostgresHttpTests.Returns.cs), [ghi chi và đảo điểm](../tests/Backend.UnitTests/PosCheckoutPostgresHttpTests.RefundLoyalty.cs), [race với đóng ca](../tests/Backend.UnitTests/PosCheckoutPostgresHttpTests.CashRefundRace.cs), [Inventory](../tests/Backend.UnitTests/Inventory/AfterSalePostgresTests.cs) và [migration](../tests/Backend.UnitTests/RefundMigrationPostgresTests.cs). Số liệu theo ngày có thể kiểm lại bằng [truy vấn đối chiếu](database/reconcile-return.sql).

## Các lỗi phát hiện khi ghép module

Trong lượt chạy song song, test thu hồi quyền gặp timeout lúc xóa database test và test tìm kiếm khách hàng vượt ngưỡng thời gian debounce. Chạy lại lần lượt toàn bộ frontend unit và backend unit trên cùng bản Release đều đạt. Log lượt trước nằm trong các tệp `*-first.log` cùng thư mục kết quả.

- Đơn đã giao biến mất khỏi doanh số gốc và lịch sử mua khi chuyển sang trạng thái hoàn. Ca RT-14 kiểm tra lại ngày giao, dòng đã mua và tổng tiền qua cả hai lần hoàn.
- Tra hồ sơ bảo hành chưa tồn tại từng trả HTTP 500 do khởi tạo kết quả rỗng sai. Ca HTTP hiện yêu cầu đúng 404 rồi tiếp tục nhận và trả máy trong thời hạn bảo hành.
- Dự án frontend integration thiếu file nguồn của repository POS sau khi thêm luồng đổi hàng. Đã bổ sung liên kết và chạy lại qua Kestrel. Ca `LiveReturns_OrderHistoryMatchesWarehouseQueueAfterPartialRestock` đối chiếu cùng mã phiếu giữa lịch sử đơn và hàng chờ kiểm định.
- Ca RT-05 kiểm tra lại phiếu được lập trước khi đơn đổi trạng thái giao hàng. Hệ thống từ chối dùng báo giá cho hàng chưa giao sau khi giao hàng thành công.

Đối chiếu kết quả tài chính với [số liệu dự kiến theo ngày và theo dòng](M4_REFUND_HANDOFF.md). Test phân quyền lấy danh sách endpoint độc lập với controller trong [ma trận hồi quy](../tests/Backend.UnitTests/ApiAuthorizationCoverageTests.cs). Các ca HTTP bảo hành và QR nằm trong [bộ test bảo hành](../tests/Backend.UnitTests/PosCheckoutPostgresHttpTests.Warranty.cs) và [bộ test hủy giữ chỗ](../tests/Backend.UnitTests/PosCheckoutPostgresHttpTests.VietQrHolds.cs).

## Chạy lại

Dùng `docker compose up -d` và chỉ định PostgreSQL test cho `TECHSTORE_TEST_CONNECTION`. Suite backend unit tự tạo/xóa database riêng cho từng ca. Seed integration cần `TECHSTORE_SEED_TEST_POSTGRES` và `TECHSTORE_SEED_TEST_ALLOW_SCHEMA=YES`. Analytics integration cần **một database test riêng** cho `TECHSTORE_ANALYTICS_TEST_POSTGRES` và `TECHSTORE_ANALYTICS_ALLOW_DATABASE_RESET=YES`. Frontend integration cần API Kestrel đã seed, đăng nhập được, tại `TECHSTORE_API_BASE_URL`. Chạy `dotnet test <project> -c Release` với từng csproj dưới tests. Xem baseline và input M2/M3 trong [báo cáo hồi quy M3](M3_TEST_SUMMARY.md).

Chạy probe UI bằng `dotnet run -c Release --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -- --probe-returns`. Clip riêng của An là bản ghi giao diện Skia dùng fixture. Hoàn chuyển khoản trong sản phẩm dùng đối soát thủ công. Khi gặp kết quả chưa rõ, giữ yêu cầu chờ để tra mã giao dịch.

## Bảo hành

Ngày 25/11/2026, bản Uno Skia Release `net10.0-desktop` chạy trên Ubuntu 24.04 x64 với API ASP.NET Core và PostgreSQL 16 thật trong DB thử riêng. Probe mở danh sách và chi tiết bằng bốn tài khoản demo, xác nhận dữ liệu, binding và màu chữ/nền khi đổi Sáng/Tối ngay trên trang.

| Tài khoản | Thao tác đã kiểm tra |
|---|---|
| `admin` | Mở chi tiết, đổi kết luận kiểm định qua bộ chọn và chuyển từ Chờ kiểm định sang Đang sửa. |
| `warehouse1` | Mở chi tiết, báo sửa xong và xác nhận nút Tiếp nhận ẩn. |
| `cashier1` | Trả máy đã sửa. Mở tiếp nhận, tra theo IMEI và mã đơn, nhận sẵn liên hệ, nhập lỗi và ghi hồ sơ mới. |
| `manager1` | Tải các máy thay còn kho, từ chối đề nghị đổi và chuyển hồ sơ về Đang sửa. |

Chạy seed hai lần cho kết quả 5 hồ sơ demo với đúng số sự kiện. Truy vấn đối soát tồn trả 0 dòng. Kiểm thử local đạt 965 backend unit và HTTP/PostgreSQL, 4 backend integration, 1.174 frontend unit và 4 frontend integration với API thật. Clip Linux và log từng vai trò ở `.local/m4-warranty-repair/runtime/warranty-skia.mp4` và `.local/m4-warranty-repair/runtime/skia-recorded.log`. Bản Windows native còn chờ nghiệm thu trên máy Windows.

Lượt Skia sau rebase xác nhận lớp phủ kín cửa sổ và hộp thoại chi tiết giữ ở giữa tại 820×520 và 1150×700. Bốn tài khoản demo đã chạy lại với API thật. Manager chọn máy thay từ kho, duyệt sang `Replaced` và xác nhận tồn kho của biến thể giảm từ 4 xuống 3. Clip và log nằm tại `.local/m4-warranty-repair/runtime/warranty-skia-rebased.mp4` và `.local/m4-warranty-repair/runtime/skia-rebased.log`.

Sau khi rebase lên `dev` tại `6b58ccd`, hồi quy đạt **2.519/2.519** test, gồm 1.163 backend unit và HTTP/PostgreSQL, 4 backend integration, 1.345 frontend unit và 7 frontend integration qua Kestrel. API chạy migration và seed thành công trên PostgreSQL sạch. EF Core xác nhận model khớp snapshot và bản Skia Release build với 0 warning, 0 error. Tệp TRX và log của lượt này có tiền tố `follow-up-` trong `.local/m4-warranty-repair/`.
