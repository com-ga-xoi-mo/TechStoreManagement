# M3 — Đối soát dữ liệu demo Dashboard

Mốc cố định là `2026-10-18T05:00:00Z`, tức 12:00 ngày 18/10/2026 (UTC+07). Các bảng Expected áp dụng cho PostgreSQL mới, chỉ có dữ liệu seed. Dashboard đếm đơn theo `CreatedAt` ở mọi trạng thái và cộng `TotalAmount` của đơn Completed theo ngày `CompletedAt`. Hai biên dùng ngày Việt Nam, với thời điểm cuối kỳ giới hạn theo `asOf` nếu kỳ còn đang diễn ra.

## Expected tính từ manifest

Mỗi dòng dưới đây là một đơn, một mặt hàng, số lượng 1. Tiền đơn bằng đơn giá trong fixture, thuế và giảm giá đều bằng 0. Seeder gán trực tiếp các số này. Kiểm chứng công thức của Vy trên đơn POS thật thuộc `m3-dashboard-live-flow`. Cột mã lấy tiền tố chung `DEMO-202610-`. Giờ và ngày đều là giờ Việt Nam tháng 10/2026.

| Mã | SKU | Trạng thái | Tạo lúc | Hoàn tất lúc | Tiền đơn (₫) |
| --- | --- | --- | --- | --- | ---: |
| 0001 | DEMO-TAB-IPAD-AIR | Completed | 17/10 11:39 | 17/10 11:59 | 16.990.000 |
| 0002 | DEMO-TAB-GALAXY-S9 | Completed | 16/10 11:39 | 16/10 11:59 | 19.990.000 |
| 0003 | DEMO-TAB-LENOVO-P12 | Completed | 15/10 11:39 | 15/10 11:59 | 8.990.000 |
| 0004 | DEMO-MON-DELL-U2723 | Completed | 13/10 11:39 | 13/10 11:59 | 15.990.000 |
| 0005 | DEMO-MON-LG-27GP | Completed | 12/10 11:39 | 12/10 11:59 | 11.990.000 |
| 0006 | DEMO-MON-ASUS-PROART | Completed | 18/10 11:39 | 18/10 11:59 | 10.990.000 |
| 0007 | DEMO-SSD-SAMSUNG-T7 | Completed | 17/10 11:39 | 17/10 11:59 | 2.690.000 |
| 0008 | DEMO-SSD-SANDISK-EXT | Completed | 16/10 11:39 | 16/10 11:59 | 2.990.000 |
| 0009 | DEMO-DOCK-ANKER-777 | Completed | 15/10 11:39 | 15/10 11:59 | 5.990.000 |
| 0010 | DEMO-KEY-LOGI-MX | Completed | 12/10 11:39 | 12/10 11:59 | 2.490.000 |
| 0011 | DEMO-TAB-IPAD-AIR | Completed | 18/10 11:39 | 18/10 11:59 | 16.990.000 |
| 0012 | DEMO-TAB-GALAXY-S9 | Completed | 13/10 11:39 | 13/10 11:59 | 19.990.000 |
| 0013 | DEMO-TAB-IPAD-AIR | Processing | 16/10 23:00 | — | 16.990.000 |
| 0014 | DEMO-TAB-GALAXY-S9 | Processing | 16/10 22:00 | — | 19.990.000 |
| 0015 | DEMO-MOUSE-LOGI-MX3 | Pending | 16/10 21:00 | — | 1.690.000 |
| 0016 | DEMO-WEBCAM-LOGI-BRIO | Pending | 16/10 20:00 | — | 4.290.000 |
| 0017 | DEMO-HEADSET-JABRA-EVOLVE | Pending | 16/10 19:00 | — | 6.290.000 |
| 0018 | DEMO-HEADSET-JABRA-EVOLVE | Cancelled | 15/10 18:00 | — | 6.290.000 |
| 0019 | DEMO-CHARGER-ANKER-735 | Cancelled | 15/10 17:00 | — | 1.190.000 |
| 0020 | DEMO-CABLE-USB-C | Cancelled | 15/10 16:00 | — | 290.000 |

| Ngày | Mã đơn cộng vào doanh thu | Cộng tiền (₫) | Đơn tạo trong ngày |
| --- | --- | ---: | ---: |
| 01–11/10, mỗi ngày | — | 0 | 0 |
| 12/10 | 0005 + 0010 | 14.480.000 | 2 |
| 13/10 | 0004 + 0012 | 35.980.000 | 2 |
| 14/10 | — | 0 | 0 |
| 15/10 | 0003 + 0009 | 14.980.000 | 5 |
| 16/10 | 0002 + 0008 | 22.980.000 | 7 |
| 17/10 | 0001 + 0007 | 19.680.000 | 2 |
| 18/10 đến 12:00 | 0006 + 0011 | 27.980.000 | 2 |
| **Cộng 01–18/10** | **12 đơn Completed** | **136.080.000** | **20** |

| Kỳ | Ngày UTC+07 | Điểm theo ngày | Doanh thu (₫) | Số đơn | Đơn mới nhất theo thứ tự |
| --- | --- | ---: | ---: | ---: | --- |
| `today` | 18/10 | 1 | 27.980.000 | 2 | 0011, 0006 |
| `last7Days` | 12–18/10 | 7 | 136.080.000 | 20 | 0011, 0006, 0001, 0007, 0013 |
| `thisMonth` | 01–18/10 | 18 | 136.080.000 | 20 | 0011, 0006, 0001, 0007, 0013 |
| `custom&from=2026-10-12&to=2026-10-18` | 12–18/10 | 7 | 136.080.000 | 20 | 0011, 0006, 0001, 0007, 0013 |

Tổng ngày của từng kỳ bằng doanh thu kỳ. Ngày 14/10 được trả về với giá trị 0 trong cả ba kỳ chứa ngày đó. `thisMonth` còn có 11 điểm 0 đầu tháng. Đơn mới nhất sắp theo `CreatedAt DESC, id DESC`, dùng ID fixture để phân thứ tự khi giờ tạo trùng.

**Snapshot tồn kho.** Cả 20 sản phẩm fixture đều hoạt động. Mười SKU có IMEI nhập 5 chiếc mỗi SKU. iPad Air và Galaxy Tab S9 bán 2 chiếc, giữ 1 chiếc mỗi SKU nên tồn 3, khả dụng 2. Tám SKU theo dõi IMEI còn lại bán 1 chiếc mỗi SKU nên tồn 4. Năm SKU không theo dõi IMEI có dòng tồn bằng 0. Ngưỡng là 3 và điều kiện Dashboard là tồn vật lý ≤ 3, nên có **7** dòng tồn thấp.

| Thứ tự | Sản phẩm trong nhóm tồn thấp | Tồn | Đang giữ | Khả dụng | Ngưỡng | API trả trong danh sách |
| ---: | --- | ---: | ---: | ---: | ---: | --- |
| 1 | Anker 735 Charger 65W | 0 | 0 | 0 | 3 | Có |
| 2 | Jabra Evolve2 65 Flex | 0 | 0 | 0 | 3 | Có |
| 3 | Logitech Brio 4K Webcam | 0 | 0 | 0 | 3 | Có |
| 4 | Logitech MX Master 3S | 0 | 0 | 0 | 3 | Có |
| 5 | USB-C 100W Braided Cable | 0 | 0 | 0 | 3 | Có |
| 6 | Galaxy Tab S9 256GB | 3 | 1 | 2 | 3 | — |
| 7 | iPad Air 11 M2 128GB | 3 | 1 | 2 | 3 | — |

Fixture hiện có 5 danh mục, 20 sản phẩm có giá vốn, 3 biến thể, 10 khách, 20 đơn (12 Completed, 2 Processing, 3 Pending, 3 Cancelled), 4 tài khoản và 50 IMEI. Các mục tối thiểu của M3 gồm 5 danh mục, 20 sản phẩm có giá vốn và ảnh trên DB mới, 10 khách, 20 đơn nhiều trạng thái và 4 tài khoản. 50 IMEI là số hồi quy của fixture hiện tại.

## Actual qua HTTP ở mốc cố định

Chạy ngày 18/11/2026 với .NET SDK `10.0.404`, Debug, commit nền `4a1f095` và thay đổi cục bộ của hai change Dashboard Manager và dữ liệu demo. `WebApplicationFactory<Program>` dùng PostgreSQL schema riêng và storage Local. Manager lấy JWT qua login thật. Host test cố định riêng thời điểm snapshot mà Analytics đọc, để controller, truy vấn, phân quyền và JSON chạy theo đường API thực tế.

| Kỳ | Biên ngày Actual | Expected doanh thu / đơn | Actual doanh thu / đơn | Sản phẩm / tồn thấp Actual | Điểm ngày Actual | HTTP |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| `today` | 18/10 | 27.980.000 / 2 | 27.980.000 / 2 | 20 / 7 | 1 | 200 · Pass |
| `last7Days` | 12–18/10 | 136.080.000 / 20 | 136.080.000 / 20 | 20 / 7 | 7 | 200 · Pass |
| `thisMonth` | 01–18/10 | 136.080.000 / 20 | 136.080.000 / 20 | 20 / 7 | 18 | 200 · Pass |
| `custom` | 12–18/10 | 136.080.000 / 20 | 136.080.000 / 20 | 20 / 7 | 7 | 200 · Pass |

Cả bốn response có `asOf=2026-10-18T05:00:00Z` và `timeZone=UTC+07:00`. Actual khớp từng dòng Expected về ngày, doanh thu, tổng kỳ, danh sách tồn thấp và thứ tự đơn mới. Hai chỉ số phần trăm thay đổi đều trả null. Các ngày zero gồm 14/10 cho ba kỳ chứa ngày đó và thêm 01–11/10 cho `thisMonth`.

### Chạy lại kiểm thử

Đặt biến `TECHSTORE_SEED_TEST_POSTGRES` thành chuỗi kết nối PostgreSQL test có tên database chứa `test`. Fixture tạo và xóa schema riêng, dùng storage tạm trong mỗi ca. Lệnh PowerShell dưới đây in Actual của từng kỳ khi chạy với logger chi tiết.

```powershell
$env:TECHSTORE_SEED_TEST_ALLOW_SCHEMA = 'YES'
dotnet test tests/Backend.IntegrationTests/TechStore.Backend.IntegrationTests.csproj --filter 'FullyQualifiedName~DataSeederPostgresIntegrationTests' --logger 'console;verbosity=detailed'
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter 'FullyQualifiedName~DemoSeed|FullyQualifiedName~DataSeederTests'
```

| Bộ kiểm thử | Pass | Fail | Skipped | Bằng chứng |
| --- | ---: | ---: | ---: | --- |
| Seed PostgreSQL opt-in | 3 | 0 | 0 | Fresh, rerun, DB Sprint 1, thiếu một phần và conflict. Kiểm chứng 20 URL ảnh qua HTTP, tài khoản theo role, liên kết đơn/ca/VietQR/IMEI/kho và bốn kỳ Dashboard. |
| Seed unit tests có lọc | 15 | 0 | 0 | Manifest, ảnh nhúng, idempotency và lỗi asset, storage, save, phản hồi commit. |
| Toàn bộ Backend.UnitTests | 654 | 0 | 39 | 39 ca PostgreSQL opt-in của suite này được skip. Trong sandbox Windows, chạy lại với `Logging__EventLog__LogLevel__Default=None` và Local storage trong workspace để tránh lỗi quyền Windows Event Log. |
| Toàn bộ Backend.UnitTests bật PostgreSQL | 694 | 0 | 0 | Lần cuối bật `TECHSTORE_TEST_CONNECTION` trỏ PostgreSQL test, gồm ca giữ credential của tài khoản demo qua rerun. |

SHA-256 bản chạy HTTP cố định cuối là `839ED352E99706B9036EBCE36D596B4E2EBAC5830A8D0CE2A43B85DCF1BBF10E` cho `TechStore.Api.dll` và `5B3E29B1A39487866092A2A192BA4DA990541DB4F56985AA94C284E24282BDB9` cho `TechStore.Backend.IntegrationTests.dll`.

## Chạy demo Local

1. Khởi động PostgreSQL bằng `docker compose up -d`. Tạo một database demo trống và dùng chuỗi kết nối của database đó cho `ConnectionStrings__DefaultConnection`. API tự chạy migrations và seed ở môi trường Development.
2. Mở PowerShell ở gốc repo và chạy API. `ObjectStorage__Local__RootPath` phải là thư mục demo có quyền ghi. Giữ cùng database và thư mục qua các lần seed lại.

   ```powershell
   $env:ObjectStorage__Provider = 'Local'
   $env:ObjectStorage__Local__RootPath = Join-Path (Get-Location) 'backend/uploads/m3-demo'
   $env:ObjectStorage__Local__RequestPath = '/uploads'
   dotnet run --project backend/TechStore.Api.csproj --launch-profile http
   ```

3. Ở cửa sổ PowerShell khác, chạy client và đăng nhập `manager1` với mật khẩu demo `Manager@123`. Mở **Tổng quan → Khoảng ngày**, nhập **12/10/2026** đến **18/10/2026** rồi bấm **Áp dụng**.

   ```powershell
   $env:TECHSTORE_API_BASE_URL = 'http://127.0.0.1:5176/'
   $env:TECHSTORE_DASHBOARD_DEMO = '0'
   dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
   ```

4. Mở chi tiết sản phẩm trong Catalog. Có thể xác nhận trực tiếp bằng HTTP với đoạn dưới đây. Dùng cùng địa chỉ gốc nếu API chạy trên cổng khác. Kiểm tra `imageUrl` trùng URL ảnh bìa trong gallery và URL trả HTTP 200, `image/png`.

   ```powershell
   $baseUri = [uri]'http://127.0.0.1:5176/'
   $auth = Invoke-RestMethod ([uri]::new($baseUri, 'api/auth/login')) -Method Post -ContentType 'application/json' -Body '{"username":"manager1","password":"Manager@123"}'
   $headers = @{ Authorization = "Bearer $($auth.accessToken)" }
   $catalog = Invoke-RestMethod ([uri]::new($baseUri, 'api/products?page=1&pageSize=20')) -Headers $headers
   $product = Invoke-RestMethod ([uri]::new($baseUri, "api/products/$($catalog.items[0].id)")) -Headers $headers
   $image = Invoke-WebRequest ([uri]::new($baseUri, $product.imageUrl)) -UseBasicParsing
   [pscustomobject]@{
       CoverMatches = $product.imageUrl -eq $product.images[0].url
       Status = $image.StatusCode
       ContentType = $image.Headers['Content-Type']
   }
   Invoke-RestMethod ([uri]::new($baseUri, 'api/analytics/dashboard?period=custom&from=2026-10-12&to=2026-10-18')) -Headers $headers
   ```

## Chạy demo R2

Dùng một database demo trống khác, bucket R2 test có quyền ghi/xóa và public base URL đã bật cho bucket đó. Đặt cấu hình qua biến môi trường, user-secrets nếu đã cấu hình, hoặc `backend/.env` đã được git ignore trong Development.

| Tên biến | Giá trị |
| --- | --- |
| `ObjectStorage__Provider` | `R2` |
| `ObjectStorage__R2__AccountId` | Account ID Cloudflare của môi trường test |
| `ObjectStorage__R2__AccessKeyId` | Access key của bucket test |
| `ObjectStorage__R2__SecretAccessKey` | Secret key của bucket test |
| `ObjectStorage__R2__BucketName` | Tên bucket test |
| `ObjectStorage__R2__PublicBaseUrl` | URL HTTPS public của bucket test hoặc domain public đã nối tới bucket |

Chạy API/client như với Local sau khi chọn provider R2. Lấy `imageUrl` từ product detail và mở nguyên URL HTTPS bằng trình duyệt hoặc `Invoke-WebRequest`. Kiểm tra MIME, hình hiển thị và số object sau khi seed lại. Credential chỉ nằm trong cấu hình cục bộ. Nếu file biến mất nhưng DB còn tham chiếu, khôi phục object theo key cũ từ bản sao, hoặc thay ảnh qua Catalog đối với gallery hiện hữu. Nếu commit báo kết quả chưa xác định, kiểm tra tham chiếu `product_images.object_key` với database tương ứng trước khi dọn object của lần chạy.

**Trạng thái R2:** chưa chạy với bucket thật do thiếu môi trường R2 test. Unit test đã xác nhận URL HTTPS do provider trả về được giữ nguyên trong gallery và cover.

## Actual live trên database demo mới

Chạy ngày **18/11/2026** bằng Kestrel trên cổng 5297, với PostgreSQL demo mới đã migrate và seed, Local storage và JWT của `manager1`. SHA-256 của API dùng cho phiên live là `C8D1E6F87B3DA2D3A2771BF1C291CEC7A7B21A1E6FC46FCE25D85BEF545C8899`. Kỳ custom đọc từ `2026-10-11T17:00:00Z` đến trước `2026-10-18T17:00:00Z` theo UTC+07, tức hết ngày 18/10. Lần cố định kết thúc ở 12:00 ngày 18/10 theo giờ Việt Nam. Fixture không có đơn sau 12:00 hôm đó nên hai lần đối soát cho cùng doanh thu và số đơn kỳ custom.

| Kỳ | Biên ngày UTC+07 | `asOf` API (UTC) | Expected doanh thu / đơn | Actual doanh thu / đơn | Điểm ngày |
| --- | --- | --- | ---: | ---: | ---: |
| `today` | 18/11/2026 | `2026-11-18T05:55:47.920706Z` | 0 / 0 | 0 / 0 | 1 |
| `last7Days` | 12–18/11/2026 | `2026-11-18T05:55:48.119261Z` | 0 / 0 | 0 / 0 | 7 |
| `thisMonth` | 01–18/11/2026 | `2026-11-18T05:55:48.210231Z` | 0 / 0 | 0 / 0 | 18 |
| `custom` | 12–18/10/2026 | `2026-11-18T05:55:48.246998Z` | 136.080.000 / 20 | 136.080.000 / 20 | 7 |

Cả bốn response HTTP thành công và có snapshot gồm 20 sản phẩm hoạt động, 7 dòng tồn thấp. Ba kỳ tháng 11 có doanh thu từng ngày bằng 0 và danh sách đơn rỗng. Kỳ custom có đủ bảy điểm theo bảng tay, 14/10 bằng 0, đơn đầu tiên `DEMO-202610-0011`. Catalog có 20 file Local, ảnh mở thử trả **HTTP 200, image/png, 4.543 byte** và URL trùng ảnh bìa trong gallery.

**Dashboard desktop.** Cửa sổ Uno Skia/Win32 chạy với preview tắt. Bộ kiểm tra tạm đăng nhập `manager1`, mở Dashboard qua Shell, điền hai ô ngày và dùng lệnh gắn trên nút **Áp dụng**. Kết quả có `source=Live`, `asOf=2026-11-18T06:00:48.9676630+00:00`, doanh thu 136.080.000 ₫, 20 đơn, 20 sản phẩm hoạt động, 7 tồn thấp và 7 điểm ngày. Đã kiểm nội dung doanh thu và nhãn kỳ hiển thị trong cây giao diện. Sau khi ghi kết quả Pass, probe gặp exception ở luồng render OpenGL khi gọi thoát. SHA-256 bản probe là `1373524CB277ED8617A1C8FD4A31B5A53C0E0655A434E80D6648CBB6E1DF36C9`. Sau khi dỡ probe, build desktop có 0 warning, 0 error và SHA-256 `54493960AD655A5C35A56C0BD18A67D2090D7E2AEEB483BD476330D6C3A38D45`.
