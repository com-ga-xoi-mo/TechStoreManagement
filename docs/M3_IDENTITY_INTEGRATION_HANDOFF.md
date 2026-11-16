# M3 — Bàn giao kiểm chứng Identity và hiện trạng tích hợp

Phụ trách: An. Baseline `dev`/HEAD `4548b45` sau PR #21, ngày 16/11/2026;
phần bổ sung nằm trên working tree nhánh `feat/m3-security-verification-handoff`.
Change: [proposal](../openspec/changes/archive/2026-11-16-m3-security-verification-handoff/proposal.md),
[tasks](../openspec/changes/archive/2026-11-16-m3-security-verification-handoff/tasks.md).

Đây là bản bàn giao phần An làm độc lập trên code hiện có, dùng làm đầu vào cho
nghiệm thu bản tích hợp M3. Hoàn tất phần này không chứng nhận M3 toàn luồng hoặc
nghiệp vụ của các thành viên khác. Tình trạng ghi dưới đây chỉ áp dụng baseline
được kiểm, không khẳng định tiến độ ở nhánh riêng của chủ module.

## Kết quả theo build

Môi trường: macOS arm64, SDK 10.0.401/runtime .NET 10.0.12, Uno 6.7, PostgreSQL 16 Docker local. Các fixture
PostgreSQL tạo/migrate/seed database tên riêng, dispose host/connections rồi drop;
không reset database ứng dụng. Log và TRX local ở `/tmp/techstore-m3-*`, không
chứa JWT, refresh token, signing key hoặc connection string. Bảng này là tóm tắt
được track; không phụ thuộc script local hoặc thư mục `docs/verification` bị ignore.

| Kiểm tra | Pass | Fail | Skipped | Phạm vi và bằng chứng |
| --- | --- | --- | --- | --- |
| Suite bổ sung Identity/authorization | 7 | 0 | 0 | `IdentityPostgresHttpTests` và `ApiAuthorizationCoverageTests`, TestServer; log `techstore-m3-security-focused.log` |
| Backend regression + PostgreSQL opt-in | 600 | 0 | 0 | `techstore-m3-backend-regression.log`, TRX `techstore-m3-results/m3-backend.trx`; gồm 7 test mới và test seed giữ credentials |
| Frontend headless regression | 936 | 0 | 0 | Chạy sau backend; `techstore-m3-frontend-regression.log`, TRX `techstore-m3-results/m3-frontend.trx` |
| Fresh backend Kestrel + database tạm | Đạt | 0 | 0 | Migrate/seed thành công; health/store 200, anonymous me 401, 4 login + 4 profile đúng role qua TCP; `techstore-m3-kestrel-result.json` |

Build được test là `4548b45` cộng working tree của change, chưa phải commit phát
hành. SHA-256 bộ ba source test mới (sắp xếp theo path, nối UTF-8 path + NUL + bytes
file + NUL): `6f48c3a7526bf99e10dcaa4ad195e680c2ce5a50600e09090be9e911a22b80cb`.
Tên ba file xem liên kết test ở dưới và [fixture](../tests/Backend.UnitTests/IdentityPostgresApi.cs),
[endpoint guard](../tests/Backend.UnitTests/ApiAuthorizationCoverageTests.cs).
Kết quả suite bổ sung là tập con của backend 600 tests, không cộng thêm để tính tổng.

Fresh Kestrel dùng `--launch-profile http --no-build` trên binary đã build trong
backend regression; xác minh cổng 5176 đúng README. Chỉ database mới, không phải
clean checkout/restore hoặc kiểm UI. Process, database và storage tạm đã được dọn.

Suite bổ sung kiểm tra:

- Inventory **51 method/route**, gồm **4 public** và **47 protected**; policy
  hiệu lực theo cả bốn role, so sánh hai chiều với baseline độc lập.
- **47** request thiếu token trả đúng **401**, **90** request role bị cấm trả
  đúng **403**; body ProblemDetails an toàn và Bearer challenge. Có positive
  reads/DTO qua bốn role, không coi 400/404/415/500 là quyền đã đạt.
- Login/me bốn role; JWT thiếu, hết hạn, giả mạo, subject/version sai; refresh
  rotation/reuse, logout nhiều phiên và không thu hồi phiên người khác.
- Khóa/mở khóa, đổi role, reset password, no-op giữ phiên, Admin cuối cùng và
  metadata tài khoản không lộ secrets. Request sau commit kiểm qua PostgreSQL.
- Hai Sales dùng dữ liệu riêng: list/count/detail đúng ownership, đơn người
  khác trả 404 khi đọc/chuyển trạng thái; cashier và actor audit persist từ JWT,
  kể cả client cố gửi thêm `cashierUserId` khác.
- Cố ý ném lỗi trong vòng đời fixture vẫn drop database tự sinh. Thử tạm cấp
  SalesStaff quyền upload ảnh làm guard fail đúng method/route/role; đã khôi phục
  production file. Đây là kiểm tra khả năng phát hiện regression, không phải
  lỗi production còn tồn tại.

Trong khi dựng fixture, hai request test ban đầu không đúng contract đã được
sửa: upload ảnh phải gửi multipart; product mới tạo ở trạng thái chưa bán phải
được activate. Không sửa production code hoặc nới expected status để làm test xanh.

TestServer dùng HTTP pipeline và PostgreSQL thật, không kiểm socket/Kestrel hay
desktop UI. Windows native, HighContrast và luồng tích hợp nhập → ca → Cash →
dashboard chưa được nghiệm thu trong change này. Không tính phần chưa chạy là pass.
Bằng chứng Sprint 1 lịch sử, gồm UI mật khẩu do người dùng xác nhận, được giữ riêng
trong [báo cáo đã chốt](IDENTITY_ACCESS_MANAGEMENT_ACCEPTANCE.md); không ghi thành
lần chạy lại mới.

## Chạy lại

Từ repository root, dùng PostgreSQL local có quyền `CREATE DATABASE`:

```sh
docker compose up -d
export TECHSTORE_TEST_CONNECTION='<local PostgreSQL connection with CREATE DATABASE rights>'
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj \
  --filter 'FullyQualifiedName~IdentityPostgresHttpTests|FullyQualifiedName~ApiAuthorizationCoverageTests'
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj
dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj
```

Không chạy hai lệnh build/test này song song. Nếu thiếu biến PostgreSQL, test
database báo skipped và không hoàn tất nghiệm thu. Test policy inventory không
cần kết nối database. Cấu hình/signing key của fixture chỉ dành cho host test;
không tắt JWT lifetime validation, không dùng fake user/token repository.

Chạy ứng dụng theo [README](../README.md#2-hướng-dẫn-cài-đặt-và-chạy-dự-án-how-to-run):

```sh
dotnet run --project backend/TechStore.Api.csproj --launch-profile http
dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
```

API/client mặc định `http://localhost:5176/`, Swagger chỉ Development. Client dùng
`TECHSTORE_API_BASE_URL` nếu đổi địa chỉ. Muốn xác minh fresh setup riêng, tạo
database tạm và đặt `ConnectionStrings__DefaultConnection` cho process backend;
không reset schema database đang dùng. Development tự migrate rồi seed; phải
thấy log cả hai thành công, vì startup có thể tiếp tục sau khi báo lỗi migrate.

Fresh database có `admin`, `manager1`, `cashier1`, `warehouse1`; mật khẩu demo và
quyền xem [Identity README](../backend/Identity/README.md#demo-identities). Seed
rerun giữ credentials/profiles/roles/activity đã chỉnh. Upgrade database có dữ
liệu cần backup và xử lý preflight trùng username/email theo
[hướng dẫn migration/rollback](../backend/Identity/README.md#upgrade-and-rollback),
không tự gộp/xóa tài khoản. JWT cũ trước migration Identity cần login lại.

## Contract hiện trạng và người sở hữu

| Hạng mục | Contract đã có trên baseline | Nguồn | Owner và điểm nối tiếp |
| --- | --- | --- | --- |
| Xác thực/quyền API | JWT active user, sole role, token version; 401 identity sai, 403 thiếu quyền; fallback yêu cầu login; bốn public endpoints | [Bảng quyền](API_PERMISSIONS.md), [auth registration](../backend/Identity/IdentityServiceCollectionExtensions.cs), [main spec](../openspec/specs/api-role-authorization/spec.md) | An: cung cấp contract chung; chủ module áp dụng quyền từng endpoint |
| Actor Orders | Lấy ID/role từ token; Sales chỉ đơn của mình; actor lưu vào stock audit khi hoàn tất | [OrdersController](../backend/Orders/OrdersController.cs), [OrderService](../backend/Orders/Services/OrderService.cs), [HTTP/PostgreSQL test](../tests/Backend.UnitTests/IdentityPostgresHttpTests.cs) | An kiểm chứng actor/quyền; Đạt sở hữu lifecycle/checkout |
| Phiên và thu hồi | Refresh single-use; logout toàn tài khoản; lock/unlock, đổi role, reset password thu hồi token cũ; no-op giữ phiên | [AuthService](../backend/Identity/Services/AuthService.cs), [Identity README](../backend/Identity/README.md#token-lifecycle-and-account-api) | An; dùng contract này khi tích hợp module |
| Client transport/phiên | Refresh trước request, không replay business write; generation đổi khi thay account/logout; lưu RememberMe; offline logout không chứng nhận server revoke | [AuthRepository](../frontend/TechStore.Client/Repositories/Identity/AuthRepository.cs), [refresh coordinator](../frontend/TechStore.Client/Services/Session/SessionRefreshCoordinator.cs), [Bearer handler](../frontend/TechStore.Client/Services/Api/BearerTokenHandler.cs) | An phần session; T. Vinh nối shell/menu, cancellation và clear dữ liệu từng trang |
| Trạng thái đơn | `Pending` không giữ kho; sang `Processing` ghi PaidAt và giữ hàng; `Completed` ghi CompletedAt, xuất kho/giá vốn/bảo hành; chỉ Pending được Cancelled | [Transitions](../backend/Orders/Services/OrderStatusTransitions.cs), [OrderService](../backend/Orders/Services/OrderService.cs) | Đạt chốt lifecycle cuối M3; không tự suy ra các thao tác ca |
| Công thức tiền hiện tại | Giá variant hoặc base price; dòng = đơn giá × số lượng − giảm dòng; subtotal là tổng dòng đã giảm; giảm cấp đơn và thuế đang 0 | [OrderPricingHelper](../backend/Orders/Services/OrderPricingHelper.cs) | Vy sở hữu công thức; Đạt lưu/nối checkout, Bình đối chiếu; chưa chứng nhận thuế/giảm cấp đơn M3 |
| Giá vốn và trường dữ liệu | OrderItem.UnitCost lưu lúc sale; không xuất cost snapshot trong order DTO; Catalog/Inventory vẫn theo policy Sprint 1, chưa chứng nhận redaction Sprint 2 | [Fulfillment](../backend/Inventory/Services/InventoryFulfillmentService.cs), [OrderDto](../shared/DTOs/OrderDto.cs), [bảng quyền](API_PERMISSIONS.md) | Q. Vinh serial/kho, T. Vinh Catalog; không đổi permission để che thiếu redaction |
| Khóa và transaction | Orders khóa order trước transition; fulfillment khóa serial theo ID rồi stock theo product/variant; service chung dùng cùng UoW, không mở transaction lồng | [OrderService](../backend/Orders/Services/OrderService.cs), [fulfillment](../backend/Inventory/Services/InventoryFulfillmentService.cs), [UoW](../backend/Common/Data/UnitOfWork.cs) | Đạt điều phối checkout, Q. Vinh kho; owner xác nhận thứ tự cuối khi nối receiving/POS |
| Khóa Identity | Role Admin → actor/target users tăng dần ID → refresh rows; login/refresh/logout user trước token; rollback clear tracker | [Identity README](../backend/Identity/README.md#token-lifecycle-and-account-api), [PostgreSQL concurrency tests](../tests/Backend.UnitTests/IdentityConcurrencyPostgresTests.cs) | An; giữ tách biệt với thứ tự khóa nghiệp vụ kho/đơn |
| Nhập hàng | Có supplier/purchase-order entities/config và purchase-order repository; chưa thấy API/service vận hành receiving trong cây baseline khảo sát | [Inventory entities](../backend/Inventory/Entities), [repository](../backend/Inventory/Repositories/PurchaseOrderRepository.cs) | Q. Vinh bàn giao API/DTO, trạng thái nhận/hủy, chống lặp và khóa; chưa có hợp đồng API đủ để An nghiệm thu |
| Ca/POS | Có PosSession/VietQr entities/config; Sales README còn mô tả thành phần dự kiến, chưa thấy controller/service vận hành ca/POS trên baseline | [Sales](../backend/Sales/README.md), [entities](../backend/Sales/Entities) | Đạt bàn giao mở/đóng ca, checkout, actor và cash data; không tự đặt API/path |
| Migrate/seed | EF migrations + seeder Development; seed giữ dữ liệu người dùng; PostgreSQL fixture cô lập và preflight upgrade đã có test | [Program](../backend/Program.cs), [Identity upgrade](../backend/Identity/README.md#upgrade-and-rollback), [seed test](../tests/Backend.UnitTests/IdentitySeedPostgresTests.cs) | An ghép cấu hình/migration; Bình sở hữu demo dataset và đối chiếu, từng owner bàn giao migration module |

## Điều kiện còn chờ bản tích hợp

| Owner | Bàn giao cần có trước nghiệm thu toàn M3 |
| --- | --- |
| Q. Vinh | Supplier/receiving, IMEI/giá vốn, kho dùng chung, transaction và kiểm tra concurrency theo task của mình |
| Bình | Manager dashboard thật, dữ liệu demo và số liệu tính tay, tổng hợp Pass/Fail/Skipped/Chưa kiểm tra |
| Đạt | POS Cash, ca, lifecycle cuối, transaction checkout/tiền/kho và chống ghi nhận lặp |
| Vy | Customers cho Sales, quyền giảm giá, công thức thuế/giảm cấp đơn và phân bổ làm tròn |
| T. Vinh | Role shell/route/menu, cài đặt cá nhân, tìm kiếm, giá vốn Catalog, không lẫn phiên và UI Windows/Skia |
| An | Chạy lại quyền/actor/phiên trên các contract mới đã merge; ghép bản bàn giao cuối theo dữ liệu/test/docs của các owner |

Khi merge endpoint/thay quyền, chủ module cập nhật bảng quyền/spec/test/caller và
baseline method/route cùng lúc. Guard không tự chấp nhận metadata mới; endpoint
chưa có trên baseline không bị gọi rồi đánh dấu lỗi. Phát hiện sai contract phải
ghi request/expected/actual/SHA và owner; không viết service thay chủ module.

Các checkbox nghiệm thu toàn phần của An trong [kế hoạch M3](MILESTONE_3_PLAN.md)
giữ unchecked đến khi đủ bằng chứng tích hợp. Nghiệp vụ return/refund và các thao
tác kho mới của An ở [M4](MILESTONE_4_PLAN.md) không thuộc change này.
