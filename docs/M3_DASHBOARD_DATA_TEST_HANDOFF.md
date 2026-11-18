# M3 — Bàn giao Dashboard, dữ liệu demo và kiểm thử

## Dashboard Manager

Kiểm chứng ngày 18/11/2026 trên Windows `10.0.26200.0` với .NET SDK `10.0.404`. Source là commit nền `4a1f095a4b8f7143e4868bb4d3d84998da0e6fb6` cộng các thay đổi cục bộ của `m3-manager-dashboard-access`. Kestrel dùng `http://127.0.0.1:5297` và database `techstore_m3_dashboard_live_test` đã migrate/seed trong container PostgreSQL 16 cô lập. Các test repository và seed dùng database test riêng.

Giao diện được kiểm trên cửa sổ Uno Skia `net10.0-desktop` với bộ điều khiển kiểm thử tạm dùng cùng login, Shell, Dashboard và API của bản build. Probe dùng tệp phiên theo process để giữ dữ liệu phiên đang có trên máy. SHA-256 bản probe là `776864c0de73e28e2e93140e8cd83b6592fb33ed32275ba3faf897b239361591`. Mã probe đã được gỡ khỏi source bàn giao.

- SHA-256 `TechStore.Api.dll` dùng với Kestrel test — `4deb2a47a7e0ca83f246174dcb5ac9726453baa88888fc7cc0ec3496e305954a`.
- SHA-256 `TechStore.Client.dll` từ build desktop sau khi gỡ probe — `54493960ad655a5c35a56c0bd18a67d2090d7e2aeeb483bd476330d6c3a38d45`. Build 0 warning, 0 error.

| Ca | Mong đợi | Kết quả thực tế | Trạng thái |
| --- | --- | --- | --- |
| JWT từ đăng nhập thật, gọi `last7Days` và `custom` | Admin/Manager 200, SalesStaff/WarehouseStaff 403 | Cả hai kỳ trả 200/200/403/403. Body của 403 không có dữ liệu Dashboard | Pass |
| Thiếu JWT và JWT sai, gọi `custom` | HTTP 401 | Cả hai trả 401 | Pass |
| Manager gửi kỳ sai, khoảng trên 31 ngày hoặc khoảng trong tương lai | HTTP 400 và chỉ rõ trường lỗi | Cả ba trả 400 với trường validation | Pass |
| Manager gửi `custom` từ `2026-10-12` đến `2026-10-18` | Bảy điểm theo UTC+07, doanh thu dương, có đơn demo thật | `asOf=2026-11-18T03:35:57.775208Z`, 7 ngày, 20 đơn, doanh thu quan sát được `136080000.00`, có `DEMO-202610-0011` | Pass |
| Swagger của endpoint Dashboard | Hiện `period`, `from`, `to` và giới hạn 31 ngày | Đã kiểm tra JSON tại `/swagger/v1/swagger.json` | Pass |
| Backend Analytics và kiểm kê phân quyền (chạy có lọc) | Các test đã chọn chạy đạt | 35 pass, 0 fail, 0 skipped, gồm ca quyền dùng PostgreSQL | Pass |
| Toàn bộ suite `Backend.UnitTests` | Chạy lại sau khi sửa `IdentityHttpTests` theo quyền Dashboard mới | 649 pass, 0 fail, 39 skipped | Pass |
| PostgreSQL Analytics và seed regression | Biên UTC+07, ngày trống, số đơn/doanh thu theo đúng trường, seed tiếp tục chạy ổn định | 1 ca Analytics và 3 ca seed pass. Repository giữ snapshot tồn kho/sản phẩm tại thời điểm đọc | Pass |
| Đăng nhập `manager1` trong app, chọn 12–18/10/2026 | Vào Tổng quan, có dữ liệu live, nhãn kỳ/as-of và đơn seed | Tổng quan được mở, menu không có Khách hàng, `asOf=2026-11-18T03:45:28.4485200Z`, quan sát 20 đơn và doanh thu 136.080.000 đ | Pass |
| Khoảng ngày trên desktop | Ô ngày và Áp dụng hiển thị, Tab đi từ Từ ngày đến Đến ngày, lỗi ngày giữ báo cáo cũ | Gửi Tab vào chính cửa sổ probe và xác nhận focus. `31/02/2026` hiện lỗi đúng ô, snapshot giữ nguyên | Pass |
| Làm mới sau khi sửa nháp | Giữ khoảng 12–18/10 và đọc snapshot live mới | Sửa nháp thành 13/10, bấm Làm mới vẫn nhận ngày đầu 12/10 và snapshot mới | Pass |
| Đổi Sáng/Tối, resize và kỳ qua tháng | Màu form cập nhật khi trang đang mở, form vừa cửa sổ, bảng ngày đầy đủ và phạm vi rõ | Shell viewport 994×698 và 610×474 DIP ở raster scale 1,25. Đổi theme giữ snapshot. Kỳ 28/09–18/10 có 21 ngày, chart hiện nhãn `18/10`, tiêu đề đơn/tồn nêu đúng phạm vi | Pass |
| Preview với `TECHSTORE_DASHBOARD_DEMO=1` | Dùng fixture và chỉ hiện ba kỳ sẵn có | Snapshot mang `SourceKind=Preview`, nút Khoảng ngày ẩn và VM có đúng ba kỳ | Pass |
| Frontend headless | Test điều hướng, repository, preview, VM và các test khác | 1.042 pass khi lọc riêng ca DI bên dưới | Pass |
| Frontend DI `AddFrontendFoundation_ResolvesSharedSessionAndTransientTypedClient` | Test hoàn thành với tệp phiên test | Lần chạy đầy đủ có 1 fail do sandbox từ chối xóa `AppData/Local/TechStore/session.json`. Tệp phiên người dùng được giữ nguyên | Fail môi trường |

Đã sửa [`IdentityHttpTests`](../tests/Backend.UnitTests/IdentityHttpTests.cs) để giữ kiểm tra Dashboard trả `403` cho SalesStaff và WarehouseStaff. `AnalyticsControllerTests` kiểm tra Manager nhận `200`. Lần chạy toàn bộ `Backend.UnitTests` chưa bật `TECHSTORE_TEST_CONNECTION`, nên 39 ca PostgreSQL opt-in được ghi là skipped.

Trích bằng chứng HTTP đã lọc, không chứa JWT hoặc thông tin kết nối:

```text
Admin           last7Days=200 custom=200
Manager         last7Days=200 custom=200
SalesStaff      last7Days=403 custom=403
WarehouseStaff  last7Days=403 custom=403
Anonymous=401; invalid JWT=401
Manager invalid period=400, oversized range=400, future range=400
```

Trích log cửa sổ Skia từ phiên live:

```text
[UI PASS] Tab moves from start date to end date
[UI PASS] Invalid date shows a bound field error
[UI PASS] Live custom range reached database
[UI PASS] Rendered report interval and as-of info
[UI PASS] Refresh uses applied interval after draft edit
[UI PASS] Dark theme updates form surface while Dashboard remains open
[UI PASS] Narrow Dashboard keeps date fields and Apply visible
[UI PASS] Cross-month chart and complete daily-date values
[UI PASS] Preview fixture and exactly three periods (lần chạy preview riêng)
```

Kiểm tra UI lần này chạy trên Skia/Win32. Windows native và các nền tảng Skia khác chưa kiểm tra. Các số của luồng live là output quan sát khi nghiệm thu truy cập. Bằng chứng đối soát riêng của `m3-demo-data-reconciliation` nằm trong phần Dữ liệu demo và đối soát bên dưới.

## Dữ liệu demo và đối soát

Xem [bảng Expected/Actual, bằng chứng kiểm thử và hướng dẫn chạy lại demo Local/R2](M3_DEMO_DATA_RECONCILIATION.md). Bốn kỳ API ở mốc seed đều khớp bảng tính tay. Luồng live ghi `asOf` riêng theo ngày demo.

## Đối soát giao dịch POS live

Xem [bằng chứng API/PostgreSQL và desktop sau checkout](M3_DASHBOARD_LIVE_FLOW.md). Trên build chứa POS/ca và API tìm khách cho Sales, hai ca HTTP kiểm chứng doanh thu ngày Việt Nam sau commit và dữ liệu giữ nguyên sau 409. Lượt desktop trên Kestrel kiểm chứng bán khách lẻ, đóng ca và Manager thấy đúng đơn mới. Luồng từ phiếu nhập đến Dashboard còn chờ receiving của Q. Vinh vào `dev`.

## Kiểm thử Sprint 5

[Báo cáo kiểm thử M3](M3_TEST_SUMMARY.md) ghi kết quả regression Admin, read-back sau 201/409/400, các lượt runner theo build và các ca còn chờ bàn giao cho An.
