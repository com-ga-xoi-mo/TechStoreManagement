# M3 — Đối soát Dashboard sau giao dịch POS

## Build và phạm vi

Lượt kiểm chứng ngày 18/11/2026 dùng .NET SDK `10.0.404`. SHA nền `e587ee787a42a719dc72ff4d4efdf5f99b086036` đã chứa hai commit Dashboard Manager và demo data, cùng `dev` mới nhất tại thời điểm fetch (`6c5d8c567bc69deebeb41e9c99bbce9814cec9bc`). Lớp test mới của Bình được build cục bộ trên SHA này. SHA-256 bản Debug tham gia chạy HTTP:

| Artifact | SHA-256 |
| --- | --- |
| `TechStore.Api.dll` | `828DC01839695C137A2920E0702381A47F534A458446D6459C1E23E9E81AAC2B` |
| `TechStore.UnitTests.dll` | `37052B1A80B46BBDDE9530EC76D3B0E40DC01D76130B6B693884B92DADFE5B00` |

Endpoint đã chạy gồm login, tìm sản phẩm qua `GET /api/products`, dò IMEI qua `GET /api/inventory/serials`, tìm khách qua `GET /api/customers` bằng Sales, mở và đóng ca qua `/api/pos/shifts`, checkout qua `/api/pos/checkout`, đọc lại đơn qua `GET /api/orders/{id}` và Dashboard qua `GET /api/analytics/dashboard` bằng Manager. Phân quyền Dashboard có bằng chứng trong [bàn giao Dashboard Manager](M3_DASHBOARD_DATA_TEST_HANDOFF.md#dashboard-manager).

## Kiểm chứng HTTP trên PostgreSQL

`IdentityPostgresApi` tạo database riêng cho từng ca, áp migrations và seed qua API host. Login dùng JWT thật. Test chỉ cố định đồng hồ ca, checkout và thời điểm snapshot Analytics. Lượt này dùng thiết bị `InStock` từ fixture. DB được đọc lại bằng context độc lập để kiểm tra dữ liệu đã lưu.

Đặt `TECHSTORE_TEST_CONNECTION` thành chuỗi kết nối PostgreSQL test có quyền tạo database riêng. Từ gốc repo, có thể ghi lại log kết quả bằng PowerShell:

```powershell
New-Item -ItemType Directory -Path backend/obj/m3-live-flow -Force | Out-Null
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter FullyQualifiedName~DashboardLiveFlowPostgresHttpTests --logger 'console;verbosity=detailed' *> backend/obj/m3-live-flow/api-postgres.log
```

Lượt ghi nhận đạt **2 Pass, 0 Fail, 0 Skipped**. Log cục bộ tại `backend/obj/m3-live-flow/api-postgres.log` ghi HTTP, dữ liệu read-back và snapshot. Các trích đoạn bên dưới giữ ID thử nghiệm và không in JWT, mật khẩu hay chuỗi kết nối.

### Cash thành công, ghi doanh thu vào ngày Việt Nam

Đầu vào gồm một iPad `DEMO-TAB-IPAD-AIR`, IMEI fixture `490000000000004`, khách `Demo Customer 01` tra bằng `cashier1`, tiền đầu ca 2.000.000 ₫ và tiền nhận 20.000.000 ₫. Đơn `ORD-202610-0001` có ID `be903d21-5824-45a2-a92d-aa946d96cfdd`; ca `836ed425-a916-4823-8401-99166abe9f61`. Kỳ tùy chọn là 20–21/10/2026 theo UTC+07.

| Hạng mục | Expected | Actual |
| --- | --- | --- |
| Mở ca, checkout, đọc đơn và đóng ca | Ca Open, checkout 201, đơn lưu Completed/Cash, ca Closed | Đạt. Đơn liên kết đúng Sales, khách, ca và IMEI. Đóng ca trả 200. |
| Thời điểm | PaidAt và CompletedAt bằng nhau. UTC và UTC+07 khác ngày | Cả hai cùng `2026-10-20T17:01:00Z`, tức 00:01 ngày 21/10 giờ Việt Nam. |
| Giá, giảm và VAT | Giá 16.990.000 ₫, giảm dòng 90.000 ₫, giảm đơn 0 ₫, subtotal 16.900.000 ₫, VAT 10% là 1.690.000 ₫ | Các trường đã lưu khớp. Tổng **18.590.000 ₫**, tiền thừa **1.410.000 ₫**. |
| Tồn và serial | Tồn 3 → 2, giữ chỗ 1 → 1, IMEI Sold | Khớp. Bảo hành từ lúc hoàn tất đến `2027-10-20T17:01:00Z`. Snapshot giá vốn 14.500.000 ₫. |
| Lịch sử kho | Một dòng Sale, lượng −1, tồn sau 2 và đúng actor/khóa nguồn | Khớp. Movement ID `8ff4539a-6eb7-47e0-a7c1-2f9efd8a5bfb`. |
| Tiền ca | Cash tăng bằng tổng lưu. Tiền dự kiến và thực đếm 20.590.000 ₫, chênh lệch 0 | Khớp. Ca đóng lúc `2026-10-20T17:02:00Z`. |
| Dashboard | Thêm 1 đơn và 18.590.000 ₫. Đơn mới nằm trong 5 đơn gần nhất | 0 → 1 đơn, 0 → 18.590.000 ₫. Danh sách có đúng một dòng cho mã đơn mới. Sản phẩm hoạt động 20, tồn thấp 7. |
| Doanh thu ngày | 20/10 giữ 0, 21/10 tăng 18.590.000 ₫. Tổng các ngày bằng summary | Khớp ở cả hai snapshot. `asOf` trước `2026-10-20T17:00:30Z`, sau `2026-10-20T17:03:00Z`. |

### IMEI đã Sold, khóa checkout mới

Ca độc lập tạo một đơn thành công để bán đúng IMEI fixture trên. Sau đó giữ ca `4858f89c-2e60-4cc9-9495-645bbbfb34d2` ở trạng thái Open và thử lại với khóa mới, cùng khách và cùng số tiền đủ. Đơn gốc ID `4dd08d53-e14c-42cf-b528-632c84b26ead`.

| Expected | Actual |
| --- | --- |
| HTTP 409 vì thiết bị Sold | 409. Log: `Line 1 (DEMO-TAB-IPAD-AIR): Device is Sold; expected InStock.` |
| Khóa lỗi không lưu, đơn/dòng/movement giữ nguyên | Không tìm thấy khóa lỗi. 21 order, 21 line và cùng các ID movement trước/sau. |
| Stock, bảo hành và ca giữ nguyên | Tồn 2, giữ chỗ 1, Sold, bảo hành giữ nguyên, ca Open và Cash 18.590.000 ₫. |
| Summary, daily, recent và low stock giữ nguyên | 1 đơn, 18.590.000 ₫, daily 0 / 18.590.000 ₫, cùng đơn và các dòng tồn thấp. `asOf` tiến từ 17:02 đến 17:03 UTC. |

Đây là ca xung đột tuần tự.

## Kiểm chứng desktop với Kestrel

Lượt ngày 18/11/2026 dùng Windows Uno Skia (`net10.0-desktop`), preview tắt, Kestrel `127.0.0.1:5298` và một PostgreSQL demo mới do API migrate/seed. Probe tạm được đưa vào bản build desktop qua MSBuild target trong thư mục build. Probe dùng màn hình, binding và command hiện có, và tệp phiên riêng. SHA-256 của `TechStore.Client.dll` bản probe là `E91621F9077A0F5B0E5B595091D9CF31C4E1E673C4ED97F4788CD1DFC71E0B86`. API dùng artifact ở bảng build phía trên.

Đầu vào gồm IMEI fixture `490000000000004`, khách lẻ, tiền đầu ca 2.000.000 ₫ và tiền nhận 20.000.000 ₫. Phiên desktop không nhập giảm dòng. Với giá 16.990.000 ₫ và VAT 10%, tổng mong đợi là 18.689.000 ₫. Đơn `ORD-202611-0001` có ID `5d5414da-d168-48c1-ab3d-45f29f25a3c7`, ca `064aaba5-0173-43d5-80e9-8ccf1d989e49`.

| Bước | Expected | Actual | Trạng thái |
| --- | --- | --- | --- |
| Manager mở kỳ 18/11 trên Dashboard | Nguồn Live, 0 đơn, 0 ₫ | Khớp tại `asOf=2026-11-18T07:44:43.5929460Z`. | Pass |
| Sales mở ca, quét IMEI, thanh toán Cash | Ca Open, giỏ có một dòng đúng IMEI, có biên nhận | Khớp. Completed lúc `2026-11-18T07:44:46.479016Z`, VAT 1.699.000 ₫, tổng 18.689.000 ₫ và tiền thừa 1.311.000 ₫. | Pass |
| Đóng ca với tiền thực đếm | Tiền dự kiến và đếm 20.689.000 ₫, chênh lệch 0 | Khớp. UI trở về trạng thái chưa mở ca. HTTP read-back riêng xác nhận Closed và số tiền. | Pass |
| Manager đọc lại cùng kỳ | Nguồn Live, thêm 1 đơn và 18.689.000 ₫ ngày 18/11, có mã đơn vừa bán | Khớp, đã xác nhận doanh thu và nhãn kỳ của UI tại `asOf=2026-11-18T07:45:17.0802100Z`. | Pass |
| UI receiving | Nhận IMEI trước bán | Build chưa có receiving. Lượt này dùng IMEI đã có từ seeder trên DB demo. | Chưa kiểm tra, Q. Vinh |
| Chọn khách trên POS desktop | Chọn khách đã tìm và lưu liên kết đơn | Build chưa nối bộ chọn vào POS. Lượt này bán khách lẻ. | Chưa kiểm tra, Đạt nối bộ chọn của Vy |

Log công cụ desktop nằm tại `backend/obj/m3-live-flow/desktop-evidence.log`. HTTP read-back của ca, đơn và Dashboard nằm riêng ở `backend/obj/m3-live-flow/desktop-read-back.json`. Probe kết thúc mã 0 sau khi ghi Pass. Chưa kiểm tra Windows native hoặc desktop nền tảng khác.

## Phụ thuộc bàn giao

| Mục | Trạng thái | Owner và điều kiện chạy lại |
| --- | --- | --- |
| Nhà cung cấp → phiếu nhận IMEI → POS → Dashboard | Chưa kiểm tra đầu-cuối. `dev` đã fetch chưa có controller receiving. | Q. Vinh merge receiving và migration vào `dev`. Bình chạy cùng build, bán đúng IMEI từ phiếu nhận qua API và lặp ca 409 trên SHA đó. |
| Đối chiếu tiền và ca 409 trên build có receiving (task 2.2) | Đã quan sát VAT 10% và tổng lưu 18.590.000 ₫ trong ca HTTP dùng IMEI fixture. Chưa chạy lại ca 409 và ca giảm cấp đơn khác 0 trên build có receiving. | Chờ Q. Vinh merge receiving rồi chạy lại ca 409 trên build mới. Ca giảm cấp đơn còn chờ Đạt nối POS với contract của Vy. Khi đủ phụ thuộc, đối chiếu tổng lưu, tiền ca và delta Dashboard theo cấu hình bàn giao. |
| Khách hàng qua Sales HTTP | Pass | API của Vy đã được gọi bằng `cashier1`. |

Tổng hợp baseline gồm **2/2 ca HTTP/PostgreSQL** và **1/1 lượt desktop** đạt trong phạm vi đã chạy. Không có ca fail hoặc skipped trong lượt HTTP có lọc. Checklist 2.1 và 2.2 đã chốt để archive với trạng thái **Chưa kiểm tra** cho các lượt cần build có receiving. Chạy bổ sung theo mục Risks / Trade-offs của design sau khi owner bàn giao.
