# Design

## Context

Xem [proposal](proposal.md) về mục tiêu. Baseline khảo sát: `origin/dev` tại `4548b45`, ngày 16/11/2026; cây mã giống HEAD archive `31d4378` của PR #21.

### Bằng chứng đã đọc

| Nguồn | Quan sát và hệ quả cho phạm vi |
| --- | --- |
| `docs/MILESTONE_3_PLAN.md` | Chín task Sprint 1 của An đã đánh dấu xong; còn mục tích hợp quyền/actor/phiên, HTTP regression, README/bàn giao và điều kiện nghiệm thu toàn M3. Các mục sau có phần phụ thuộc kết quả của chủ module. |
| `docs/IDENTITY_ACCESS_MANAGEMENT_ACCEPTANCE.md` | Đã có bằng chứng Sprint 1, gồm 69 HTTP checks lịch sử và kết quả backend/frontend. Không coi test chưa được đưa vào repo là chức năng chưa làm hoặc nghiệm thu cũ thất bại. |
| `tests/Backend.UnitTests/IdentityHttpTests.cs`, `TestJwtAuthentication.cs` | HTTP test dùng JWT production nhưng thay user/refresh/UoW và một số repository bằng fake. Đã kiểm tra claim sai, hết hạn, giả mạo, quyền, revoke, last Admin và Orders ownership; cần bổ sung đường HTTP + persistence thật, không viết lại toàn bộ unit tests. |
| `IdentityMigrationPostgresTests.cs`, `IdentitySeedPostgresTests.cs`, `IdentityConcurrencyPostgresTests.cs`, `PostgresDatabase.cs` | Có fixture database sinh tên riêng, migrate/drop và kiểm tra seed, locking, refresh race. Tái sử dụng; không thêm framework hoặc race suite trùng lặp. |
| `tests/Frontend.IntegrationTests/` | Có live Catalog/Orders tests và `OptInFact` dựa vào `TECHSTORE_API_BASE_URL`; chưa có suite Identity lifecycle được track trong thư mục này. Các suite hiện có dùng để smoke test nếu chạy trên backend tạm, không thay bằng chứng riêng cho Identity. |
| Các production `*Controller.cs`, `IdentityServiceCollectionExtensions.cs`, main specs và `docs/API_PERMISSIONS.md` | Chính sách hiện hành là Sprint 1; Customers và Analytics còn Admin-only đúng contract. Fallback đã có, Orders đã xác thực và dùng actor từ token. Không lập task sửa lại các phần này. |
| `backend/Inventory/Entities/`, `Repositories/`, `backend/Sales/Entities/`, `backend/Sales/README.md` | Có entity/configuration cho supplier, purchase order, ca và VietQR; có purchase-order repository. Chưa thấy controller/service vận hành nhập hàng/ca trong cây khảo sát. Đây là mức sẵn sàng tích hợp tại baseline, không phải kết luận chủ module chưa làm ở nhánh khác hoặc lỗi cần An sửa. |
| `OrderStatusTransitions.cs`, `OrderPricingHelper.cs`, `OrderService.cs`, `InventoryFulfillmentService.cs` | Có contract trạng thái/actor/kho hiện tại để dẫn nguồn; thuế và giảm cấp đơn vẫn bằng 0. Phần thay công thức do Vy, nối checkout do Đạt. Chỉ bàn giao hiện trạng. |
| `backend/Properties/launchSettings.json`, `ApiClientOptions.cs`, README | API HTTP/client mặc định là cổng 5176; README vẫn chỉ 5000. Có thể sửa hướng dẫn chạy độc lập với các module mới. |

### Ánh xạ phần An còn lại

| Mục trong kế hoạch M3 | Làm độc lập trong change | Điều kiện giữ lại cho bản tích hợp |
| --- | --- | --- |
| Tích hợp: ghép quyền, actor, phiên; kiểm tra API nghiệp vụ | Kiểm tra tất cả endpoint đang đăng ký theo contract hiện hành; HTTP + PostgreSQL cho Identity và ownership/actor Orders hiện có | Endpoint/contract mới của từng module sau khi merge phải được chủ module bàn giao để kiểm lại |
| Sprint 5: login/logout, JWT hết hạn/giả mạo/thu hồi, khóa/đổi quyền qua HTTP | Suite HTTP production pipeline + database thật, có lệnh chạy và kết quả theo SHA | Chạy lại trên bản tích hợp, phân biệt TestServer với backend Kestrel chạy riêng; không tick toàn mục chỉ vì suite độc lập pass |
| Sprint 5: README, tài khoản, API/quyền/trạng thái đơn-ca/công thức/khóa/migrate-seed | Sửa hướng dẫn hiện tại, tổng hợp contract có nguồn và ghi rõ owner/mức sẵn sàng của phần chưa tích hợp | Chủ module xác nhận contract cuối của ca/POS, tiền, nhập hàng; Bình bàn giao dữ liệu và tổng kết test |
| Điều kiện: bốn role đúng quyền, có dữ liệu/test/docs tiếp tục M4 | Báo cáo kiểm chứng phạm vi hiện có và danh sách điều kiện còn chờ | Luồng đầy đủ M3, dữ liệu demo tổng thể, UI hai nền tảng và kết quả nghiệp vụ của các owner |

## Goals / Non-Goals

**Goals:** An có thể hoàn tất mọi task của change trên baseline hiện tại, không cần endpoint mới của người khác. Mỗi kết quả chỉ khẳng định đúng tầng đã kiểm; các thay đổi tương lai có điểm kiểm soát để cập nhật quyền/actor/phiên.

**Non-Goals:** Không đổi permission matrix Sprint 1, contract phiên, formula, transaction hoặc UI. Không tự triển khai endpoint còn chờ, tạo fake luồng POS để gọi là nghiệm thu, hoặc biến checklist unchecked thành bằng chứng code thiếu. Không tạo permission engine, endpoint-discovery tool production hay pipeline CI mới.

## Decisions

### D1. Kiểm thử HTTP + PostgreSQL trong project backend test hiện có

Thêm `IdentityPostgresHttpTests.cs` và fixture tối thiểu cạnh các test Identity trong `tests/Backend.UnitTests/`. Dùng `PostgresDatabase.CreateAsync(migrate: true)` và `WebApplicationFactory<Program>`: giữ production JWT middleware, services, repositories và `UnitOfWork`; chỉ cấu hình database, signing settings test, môi trường `Testing` và storage local riêng nếu cần. Seed database tạm bằng `IDataSeeder` hiện có, không sửa demo manifest để phục vụ test. Dispose host/connections trước khi drop database.

Không dùng `AddTestIdentities` cho suite này. Assert host đang dùng provider PostgreSQL và dependencies thật để tránh vô tình chạy lại đường fake của `IdentityApiFactory`. Mỗi test/fixture dùng database tự sinh; tài khoản phụ tạo qua API Admin. Không trỏ fixture vào database ứng dụng hoặc xóa schema của database người dùng.

Các nhóm ca cần được track:

- Login bốn role và `/auth/me`; sai mật khẩu, tài khoản khóa và request thiếu token.
- JWT giả mạo, hết hạn, subject/version sai: dùng signing settings chỉ của host test; token hết hạn ký đúng khóa và đúng claim khác để kiểm tra riêng expiry, không tắt lifetime validation hoặc sleep chờ token hết hạn.
- Refresh single-use; token đã dùng nhận 401, replacement vẫn dùng được. Logout nhiều phiên cùng user làm JWT/refresh cũ mất hiệu lực.
- Khóa/mở khóa, đổi role, reset password: request token cũ sau commit trả 401; login mới phản ánh trạng thái/role mới; last active Admin guard vẫn 409. Kiểm tra no-op status/role theo contract không thu hồi phiên.
- Safe account metadata và ProblemDetails; response không lộ secret, challenge giữ Bearer. Không ghi password, JWT/refresh, signing key hoặc connection string vào log.

Các test migration/seed/concurrency hiện có tiếp tục chạy nguyên trạng; không thêm bản sao của mọi ca vào HTTP suite. TestServer gửi HTTP qua pipeline thật và PostgreSQL thật nhưng không phải chứng cứ transport qua socket/Kestrel hay UI; báo cáo phải ghi rõ.

**Cân nhắc:** Script HTTP local dễ chạy nhưng không đảm bảo người khác lấy từ repo chạy lại được; project integration mới hoặc package mới không cần thiết vì fixture/MVC testing đã có sẵn.

### D2. Baseline quyền độc lập với metadata đang kiểm

Thêm `ApiAuthorizationCoverageTests.cs` với danh sách method/route, public/roles mong đợi được viết rõ từ `docs/API_PERMISSIONS.md` và main spec `api-role-authorization`. So sánh với production controller endpoints được host đăng ký; không tính Swagger/static files là business controllers. Test-only probe nếu xuất hiện phải được loại trừ bằng tên controller cụ thể, không bỏ qua nguyên nhóm `/api` hoặc route mới.

Kiểm tra hai chiều: production endpoint không có trong baseline, hoặc baseline endpoint đã mất đều làm test fail và liệt kê method/route. Dùng authorization policy hiệu lực sau khi kết hợp controller, action, default/fallback và `AllowAnonymous`; không chỉ kiểm tra có attribute hay không. Expected roles không được suy ra từ chính attribute đang test. Đánh giá cả bốn role để phát hiện role vô tình được cấp hoặc mất quyền.

HTTP assertions: mỗi business method/route thiếu token trả 401; mỗi role bị cấm trong bảng trả 403. Dùng GUID đúng định dạng, content type/payload tối thiểu đúng route để không nhầm 404 routing, 400 model binding hoặc 500 với kết quả quyền. Không dùng điều kiện `status != 401/403` để coi một request hợp lệ là thành công.

Đường được phép dùng các ca đọc thực với fixture và HTTP 200 + DTO phù hợp cho bốn role; các ghi Identity/Orders ở D1/D3 và feature tests hiện có cung cấp positive checks. Độ phủ policy không được báo thành mọi nghiệp vụ đều hoạt động. Không gửi mutation hàng loạt qua các API của người khác chỉ để kiểm quyền.

Endpoint tương lai chưa có không được thêm vào baseline đang chạy, gọi thử rồi kết luận lỗi. Khi một owner merge endpoint/thay contract, test nhắc cập nhật bảng quyền, main spec/delta của owner và expected test cases trong cùng thay đổi; không auto-approve bằng cách tái sinh expected từ runtime.

**Cân nhắc:** Hard-code vài route như hiện nay có thể bỏ sót endpoint mới; quét source/parse Markdown bằng một framework riêng phức tạp hơn việc giữ một bảng test rõ ràng và so sánh route runtime.

### D3. Actor và ownership chỉ theo contract Orders hiện có

Trong suite PostgreSQL HTTP, tạo hai Sales riêng, sản phẩm/tồn test tối thiểu bằng fixture/API hiện hành; tạo đơn qua HTTP, không dựa dữ liệu demo của Bình. Kiểm tra actor/cashier từ JWT, Sales A không thấy đơn Sales B trong item/count/detail và không chuyển trạng thái đơn B (404, dữ liệu không đổi); Admin/Manager đọc đúng phạm vi hiện hành. Dùng luồng hiện có và kiểm tra actor ghi audit khi hoàn tất nếu fixture cần; không thêm ca/POS, công thức thuế, chống thanh toán lặp hoặc test race của Đạt.

Nếu phát hiện lỗi, lưu request/expected/actual và module sở hữu. Lỗi Identity/hạ tầng chung cần sửa production phải được lập phạm vi sửa riêng trước khi đổi hành vi; lỗi Orders/kho/tiền chuyển đúng owner, không viết service thay thế để làm test xanh. Không archive change với test thất bại bị đổi thành skip/pass.

**Cân nhắc:** Unit HTTP fake hiện đã chứng minh intent; PostgreSQL HTTP bổ sung bằng chứng persistence/actor xuyên lớp, không thay việc owner nghiệm thu checkout mới.

### D4. Bàn giao có nguồn, trạng thái và phạm vi rõ

Tạo `docs/M3_IDENTITY_INTEGRATION_HANDOFF.md` được track, không đặt bản tóm tắt duy nhất trong `docs/verification` đang bị ignore. Nội dung gồm:

1. SHA/build, ngày, môi trường, lệnh chạy, điều kiện PostgreSQL, totals Pass/Fail/Skipped/Chưa kiểm tra và phạm vi bằng chứng. Raw log có thể local; báo cáo tracked phải đủ thông tin chạy lại và không chứa secrets.
2. Hướng dẫn fresh setup/upgrade: cổng theo launch profile/client, migrate rồi seed trong Development, preflight trùng tài khoản, login lại sau upgrade, seed giữ credentials. Liên kết Identity README thay vì sao chép lock order nhiều nơi.
3. Bảng contract hiện trạng: API/roles và public exceptions; actor/session; trạng thái đơn và `PaidAt`/`CompletedAt`; tiền và giá vốn; thứ tự khóa/service/UoW; migration/seed. Mỗi dòng có đường dẫn code/spec, owner, mức sẵn sàng và việc cần bàn giao tiếp.
4. Ca/POS/nhập hàng có entity/config hoặc repository chưa đủ để chứng nhận API nghiệp vụ. Không suy ra API/service hoàn tất chỉ từ schema, hoặc khẳng định chưa triển khai ở mọi nhánh khi mới đọc baseline.
5. Điều kiện tích hợp còn chờ: Q. Vinh nhập/giá vốn/kho; Bình Manager dashboard, dữ liệu và đối chiếu; Đạt POS/ca/trạng thái; Vy Sales Customers/công thức; T. Vinh shell/giá vốn/UI. Ghi các hợp đồng chưa bàn giao là chờ owner, không tự phát minh method/path hay công thức.

README liên kết báo cáo mới và sửa cổng 5000 thành launch profile được kiểm tra. `docs/ROADMAP.md` bỏ mô tả Orders chưa auth/Admin fallback đã sửa, giữ công việc thuế của Vy. Kế hoạch M3 ghi rõ Sprint 1 đã archive và có change cho phần độc lập; thêm các bước con có thể đánh dấu riêng sau khi có bằng chứng, giữ nguyên checkbox nghiệm thu toàn phần ở bảng ánh xạ khi chưa đủ điều kiện. Không sửa tasks archive thành chưa hoàn tất.

**Cân nhắc:** Tick hết task An sẽ làm sai trạng thái M3; một checklist chi tiết nhưng không ghi mức bằng chứng cũng có nguy cơ coi test thiếu/chưa chạy là pass. Dùng một báo cáo hiện trạng có nguồn và điểm chờ để tránh cả hai.

### D5. Không có delta spec hay migration

`skip_specs: true` vì đây là regression tests và tài liệu cho hành vi đã quy định. Không tạo capability “test/handoff” mới, không sửa main specs. Phát hiện chênh contract là thông tin cần chuyển owner/chốt phạm vi, không tự đổi expectation hoặc quyền trong change này.

## Risks / Trade-offs

- [Quyền Sprint 1 thay đổi khi chủ module tích hợp] → Gắn SHA baseline, fail rõ endpoint/policy mới và yêu cầu owner cập nhật contract; không kết luận lỗi chỉ vì kế hoạch Sprint 2 khác hiện trạng.
- [TestServer bị hiểu là nghiệm thu backend ngoài process/UI] → Ghi tầng thực thi cho từng nhóm test; nghiệm thu Kestrel/UI bản M3 cuối vẫn là việc tích hợp sau.
- [Bằng chứng lịch sử bị trình bày như mới chạy] → Giữ nguyên báo cáo Sprint 1, tách ngày/SHA/test totals mới; skip/chưa chạy không tính pass.
- [Tái sử dụng seed bị hiểu là An nhận sở hữu dữ liệu demo] → Chỉ dùng seed hiện có trên database tạm; Bình vẫn sở hữu dataset và đối chiếu toàn M3.
- [Fixture làm thay đổi database thật hoặc log lộ token] → Chỉ dùng database tên tự sinh của `PostgresDatabase`, cấu hình test riêng, cleanup kể cả lỗi và output đã bỏ secrets.
- [HTTP matrix sinh nhiều host/database chậm] → Dùng fixture/class collection nhỏ và tái sử dụng host cho các ca chỉ đọc/quyền; ca thay đổi trạng thái cô lập user hoặc database, không thêm tầng abstraction chung.

## Migration Plan

Không có deploy/schema migration. Triển khai test trước, chạy các suite cần thiết tuần tự để tránh tranh file build trong shared, rồi cập nhật báo cáo/README và trạng thái các bước con theo kết quả. Chạy PostgreSQL opt-in với `TECHSTORE_TEST_CONNECTION`; môi trường thiếu biến phải báo skipped, lần nghiệm thu change yêu cầu thực chạy các test mới. Nếu rollback, revert các test/docs của change; database ứng dụng và credential hiện hữu không đổi.

Điều kiện đóng change: các test mới chạy đạt trên PostgreSQL, regression liên quan đạt, báo cáo và hướng dẫn chạy đã đối chiếu, phần chờ owner được nêu rõ và không có checkbox toàn M3 bị tick thiếu bằng chứng. Không cần chờ owner hoàn tất tính năng tương lai để đóng phần chuẩn bị độc lập này; kết quả kiểm tra hiện có thất bại thực sự phải được xử lý hoặc ghi blocking issue, không coi là phần tương lai để bỏ qua.
