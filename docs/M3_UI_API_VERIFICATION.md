# M3 — Kiểm chứng UI/API phần T. Vinh

Ngày chốt: 17/11/2026. Code được kiểm chứng: `7240d3f` trên nhánh `feat/m3-role-ui-catalog-search`; commit báo cáo chỉ đổi tài liệu/tracking. Đây **không phải nghiệm thu toàn M3**.

## Môi trường và phạm vi bằng chứng

- Ubuntu 26.04.1 LTS, Linux 7.0.0-43-generic x86_64, .NET SDK 10.0.101; Skia `net10.0-desktop`, Wayland, `DISPLAY=:0`.
- API Kestrel thật `http://localhost:5199`, PostgreSQL trong `techstore_postgres`, DB riêng `techstore_m3_verify`. Test PostgreSQL tự tạo/xóa DB cô lập qua `TECHSTORE_TEST_CONNECTION`.
- API/SQL, headless dùng class production và log khởi động Skia được phân biệt với quan sát UI. Không có công cụ nhìn/điều khiển Wayland; không cài thêm. Không có máy Windows.
- Log `/tmp/m3-*.log` là bằng chứng cục bộ, không được commit; kết quả, input và lệnh chính được ghi lại dưới đây. Không lưu token/mật khẩu trong báo cáo.

## Các commit triển khai

| Change | Commit |
| --- | --- |
| catalog-cost-price-by-role | `782f521` |
| role-based-shell | `1b5d883` |
| catalog-sales-search | `6bcea2c` |
| session-isolated-ui | `f198a81` |
| personal-settings-split | `7240d3f` |

## Build và test cuối — Pass

```sh
dotnet build TechStore.sln
# Thiết lập TECHSTORE_TEST_CONNECTION theo backend/Identity/README.md trước lệnh backend.
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj
dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj
```

| Kiểm tra | Pass | Fail | Skipped | Bằng chứng |
| --- | ---: | ---: | ---: | --- |
| Backend unit, có PostgreSQL thật | 609 | 0 | 0 | `/tmp/m3-final-backend.log` |
| Frontend unit | 975 | 0 | 0 | `/tmp/m3-final-frontend.log` |

Build thành công, 0 warning/error (`/tmp/m3-final-build.log`). Không lấy các lần suite lỗi hoặc skip trước đó làm kết quả đạt.

## HTTP methods, JSON và mã lỗi

Đã chạy curl với token lấy từ login thật; Admin là `admin`, Sales là `cashier1`. Mỗi dòng là request thật, không suy ra từ attributes hay fake controller. Bằng chứng: `/tmp/m3-final-http.log`.

| Method/path và input | Role | Mong đợi | Thực tế | Content-Type / body | Kết quả |
| --- | --- | --- | --- | --- | --- |
| GET `/api/products` | Admin | 200 | 200 | `application/json; charset=utf-8`, JSON đọc được | Pass |
| POST `/api/categories`, tên/slug riêng `m3-verify-96eaf578ca43` | Admin | 201 | 201 | `application/json; charset=utf-8`, JSON đọc được | Pass |
| POST `/api/categories`, lặp slug vừa tạo | Admin | 409 | 409 | `application/problem+json; charset=utf-8`, ProblemDetails status 409 | Pass |
| POST `/api/categories`, tên rỗng/slug sai | Admin | 400 | 400 | `application/problem+json; charset=utf-8`, lỗi validation JSON | Pass |
| GET `/api/products`, không token | Chưa đăng nhập | 401 | 401 | `application/problem+json`, JSON đọc được | Pass |
| POST `/api/categories` | Sales | 403 | 403 | `application/problem+json`, JSON đọc được | Pass |
| GET `/api/products/00000000-0000-0000-0000-000000000000` | Admin | 404 | 404 | `application/problem+json; charset=utf-8`, ProblemDetails | Pass |
| DELETE `/api/categories/721bc4bf-dbd0-450c-b951-16ecc8d037f7` | Admin | 204 | 204 | Không Content-Type, không body | Pass |
| Lỗi server 500 | — | JSON lỗi an toàn | Chưa chạy | Không tìm thấy test backend middleware tương ứng; không cố gây lỗi trên DB thật, theo design D2 | Chưa kiểm tra |

Category thử đã DELETE 204. Các probe sai trước đó được giữ trong log từng change, không tính là request đạt.

## Bốn role: Shell, route và nút thao tác

`RoleAccessTests.PolicyAndShell_FollowRoleTable` chạy `ShellViewModel` và `InMemorySessionStore` thật với token mô phỏng. Output thực tế trong `/tmp/m3-role-frontend.log`:

| Role | Landing thực tế | Menu thực tế | Kết quả |
| --- | --- | --- | --- |
| Admin | Dashboard | Dashboard, Products, Inventory, Orders, Customers, Settings | Pass headless |
| Manager | Orders | Products, Inventory, Orders, Settings | Pass headless |
| SalesStaff | Orders | Products, Orders, Settings | Pass headless |
| WarehouseStaff | Inventory | Products, Inventory, Settings | Pass headless |
| Role lạ/chưa đăng nhập | Settings | Settings | Pass headless |

Guard/reset/action tests đạt: Sales không mở ProductCreate/Edit, Warehouse không mở Orders; route cũ không được giữ sau Admin → Sales. Factory dùng guard chung trước tạo page; DEBUG DI verification không phụ thuộc role. Các ViewModel cấp quyền danh mục A; kho/serial A/W; tạo/chuyển trạng thái đơn A/S; catalog A/M.

HTTP thật bỏ qua UI vẫn bị chặn: Sales POST categories, stocks/initialize, products đều 403; Warehouse POST categories, products, orders đều 403 (`/tmp/m3-role-api.log`). Không dùng kết quả này để tuyên bố binding Visibility đã được nhìn trên UI.

## Tra cứu bán hàng và giá vốn

- Theo người dùng duyệt trong phiên: tìm exact cả `SerialNumber` **và** `Imei`. Dữ liệu thật có 50 bản ghi `serial_number` null; IMEI nằm ở cột riêng. Proposal/design/spec/tasks đã cập nhật trước áp dụng. Không đổi schema/quyền API.
- `q` AND tối đa 8 từ, giữ dấu tiếng Việt và escape ký tự LIKE; OR với số Serial/IMEI đầy đủ, không phân biệt hoa/thường. Đây không phải PostgreSQL tsvector/ranking hay tìm không dấu. So sánh `lower(...)` không được tuyên bố sử dụng index thường.
- IMEI thật `490000000000049` từ GET `/api/inventory/serials`: GET `/api/products?q=490000000000049` trả 200, đúng 1 sản phẩm `09efa9e9-8f17-e3fc-bbd7-f096ecec2c40` (Logitech MX Keys S); prefix `49000000` trả 200, 0 kết quả. **Pass**.
- `q=USB-C Braided` trả 1 sản phẩm USB-C 100W Braided Cable; thêm `impossibleword` trả 0. **Pass**.
- `q=Dell XPS 15&brand=Dell&minPrice=45000000&maxPrice=45000000&spec=ram_gb:32`: `totalCount=1`, SQL độc lập trả 1. **Pass**. SQL dùng:

```sql
select count(*) from products p
where p.is_active = true
  and p.name ilike '%Dell%' and p.name ilike '%XPS%'
  and p.brand = 'Dell'
  and p.base_price between 45000000 and 45000000
  and p.specs @> '{"ram_gb":32}'::jsonb;
```

- Mười trang `pageSize=2` thu 20 ID, khớp `pageSize=100`, không trùng/thiếu. **Pass**. Các phép thử keyword/SQL/paging này chạy trước bổ sung cột Imei; regression PostgreSQL cuối kiểm lại kết hợp lọc/paging sau bổ sung. Log `/tmp/m3-search-api.log`, `/tmp/m3-search-approved-regression-final.log`; ca regression dùng repository thật và DB cô lập.
- Giá vốn ProductDto: kiểm chứng trước phiên đã ghi ở tasks change 1 — A/M nhận số, S/W nhận null, field khác giữ nguyên, thiếu/sai token 401. Bằng chứng mới trong phiên gồm test HTTP role và mapper/repository trong suite cuối. Không tự nhận vừa chạy lại curl bốn role. **Pass API/test; chưa kiểm tra dòng giá vốn trên UI**.
- Bàn giao Đạt: dùng GET `/api/products?q=...` với các filter hiện có; kết quả là sản phẩm, không chứng nhận Serial/IMEI còn bán được. Trạng thái/khả dụng máy vẫn tra Inventory, không sao chép logic kho vào Catalog.

## Cô lập phiên và lỗi mạng

**Pass với client production và API/DB thật, không fake backend:**

- Runner tạm dùng `InMemorySessionStore`, `BearerTokenHandler`, `ApiClient`: Admin generation 1 gọi inventory; giữ response sau HTTP thật, xóa phiên cục bộ, login `cashier1` thành Sales generation 3, rồi thả response. Caller nhận lỗi đổi phiên, không nhận body Admin; control cùng generation thành công. `/tmp/m3-session-live.log`. Đây là logout cục bộ, không tuyên bố đã gọi server logout/revoke.
- `dotnet run --project /tmp/m3-live-runner -- network` dùng API riêng cổng 5201 và **cùng** `ProductListViewModel`, repository/ApiClient thật: load `Content` → tắt API → `Error`, Retry khả dụng → bật lại API → gọi Retry → `Content`; role Admin, generation 1 không đổi. `/tmp/m3-final-network.log`.
- Unit tests bao phủ response sau logout/đổi tài khoản, dispose response, login/refresh ngoại lệ và silent refresh không đổi generation; route reset được kiểm riêng. Không thêm cache/cơ chế hủy nghiệp vụ đã tới server.

## Cài đặt cá nhân/cửa hàng

- SettingsViewModel tests 10 Pass; Sales/Warehouse/Unknown trực tiếp gọi lệnh lưu vẫn không gửi repository request; A/M được phép, Manager gửi đúng request. `/tmp/m3-settings-tests.log`.
- API thật: Sales `cashier1` và Warehouse `warehouse1` PUT store 403, GET không đổi; Manager `manager1` PUT 200 và GET thấy thay đổi. PUT khôi phục 200; GET cuối xác nhận `restored=true`. `/tmp/m3-settings-api.log`.
- Sai sót phép thử: worker dùng nhầm username `sales`/`sales1`, tạo user thử `sales1` ID `926972e2-db35-4a85-a117-4a287d9b215b`. Đã xóa đúng user/user_roles này trong transaction; query sau dọn có 0 users/user_roles/refresh_tokens theo ID. Không thay tài khoản có sẵn. Lần kiểm chứng Sales thành công dùng `cashier1`, không dùng user thử. Không tính các probe lỗi là đạt.

## UI: giới hạn còn mở

Đã build và chạy app Skia thật trỏ API sau các change; log `/tmp/m3-skia.log`, `/tmp/m3-role-skia.log`, `/tmp/m3-search-skia.log`, `/tmp/m3-session-skia.log`, `/tmp/m3-settings-skia.log`. Các lần chạy có cảnh báo Uno dev-server endpoint và X11 `_NET_WM_STATE`; phần lớn dừng bởi timeout 15 giây. **Startup không chứng nhận trang/binding hoạt động.**

| Hạng mục | Trạng thái | Giới hạn |
| --- | --- | --- |
| Click đăng nhập bốn role, landing/menu/nút, mở route bị cấm trên Skia | Chưa kiểm tra | Chỉ có headless guard/menu và HTTP thật |
| Theme Light/Dark/System, tương phản, readonly form và ẩn giá vốn/nút | Chưa kiểm tra | Không nhìn/điều khiển được UI Wayland |
| Resize, bàn phím/focus, tiếng Việt và binding, loading/empty/error/retry trực quan | Chưa kiểm tra | Network retry chỉ được chứng minh headless |
| Windows native và HighContrast | Chưa kiểm tra | Không có máy Windows |

## Tick kế hoạch và phần chờ tích hợp

Chỉ tick các mục đã có bằng chứng đầy đủ trong `MILESTONE_3_PLAN.md`: cô lập dữ liệu phiên (120), tra cứu/lọc/phân trang (121), DTO giá vốn sản phẩm (122). Giữ mở:

- 118–119: code/headless/API đã đạt nhưng chưa quan sát menu/nút và nhóm cài đặt trên UI thật.
- 123, 136 (nghiệm chứng UI trong bảng phân công), 153: chưa có kiểm tra UI/binding/theme/focus/resize hai nền tảng.
- 152: HTTP 8 mã và phục hồi mạng đạt; 500 và phục hồi giao diện trực quan còn chưa kiểm tra.
- 182: chưa chạy đầy đủ luồng M2/M3 tích hợp; không suy từ unit tests thành “không còn lỗi sai tiền/sai tồn/vượt quyền”.

Các phần ngoài phạm vi, **không sửa thay owner**:

| Owner | Phần cần tích hợp |
| --- | --- |
| Q. Vinh | `SerialImeiDto.UnitCost` vẫn có thể trả cho mọi role; cần áp dụng cùng bảng `FieldAccess` bảo vệ giá vốn Inventory |
| Bình | Analytics hiện chỉ Admin; Manager dashboard chờ owner mở API và đối chiếu; client giữ chặn theo server |
| Vy | Customers hiện chỉ Admin; Sales Customers chờ owner; client không tự mở quyền |
| Đạt và các owner nghiệp vụ | Nối POS/khách/kho/ca và nghiệm chứng toàn luồng; Catalog không thay thế validation/transaction bán hàng |

## Dọn môi trường

Đã khôi phục store settings, DELETE category thử 204, dọn user thử nêu trên. Runner tạm `/tmp/m3-live-runner` đã xóa sau khi chạy; API riêng 5201 không còn listener. API 5199 đã tắt, curl `/api/health` trả mã transport `000` sau dừng. Giữ DB test có sẵn, không xóa dữ liệu người dùng. Không push, archive hoặc mở PR.
