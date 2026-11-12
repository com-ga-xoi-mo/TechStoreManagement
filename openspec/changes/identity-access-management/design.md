# Design

## Context

Xem `proposal.md` phần Why và `docs/MILESTONE_3_PLAN.md` Sprint 1. Những điểm đã kiểm tra trong source:

- `Program.cs` kiểm tra JWT signature/issuer/audience/lifetime nhưng chỉ gọi `AddAuthorization()`, chưa có fallback hay kiểm tra tài khoản/token version sau khi JWT hợp lệ.
- `OrdersController` chưa có Authorize, `GetUserId()` còn fallback Admin; `OrderService.SearchAsync/GetByIdAsync` và `OrderSearchCriteria` chưa nhận phạm vi người gọi.
- `AuthService` đang dùng `PasswordHasher<User>` của ASP.NET Core Identity, không phải BCrypt như spec cũ. Login lấy role đầu tiên hoặc mặc định SalesStaff. Chưa có refresh endpoint. Logout chỉ thu hồi refresh token, không thu hồi JWT.
- `users` đã có IsActive; `user_roles` là bảng liên kết. `refresh_tokens` đã có unique index token_hash, index user_id và partial index (user_id, token_hash) WHERE is_revoked=false. Không cần thêm bảng phiên hay index refresh mới.
- Desktop có `ISessionStore`, Generation và TryExpire; `FileSessionStorage` đã phục hồi token qua restart dù spec cũ nói memory-only. `AuthRepository` chưa truyền RememberMe cho store. Handler hiện chỉ expire khi nhận 401.
- Settings là C# Markup nhưng còn static StitchTokens. `ShellRoutePageFactory` tạo route bằng DI scope/activation lease; có sáu destination và chưa có quản lý tài khoản.
- Seeder thực tế ở `Common/Data/DemoSeedRepository`, được `DataSeeder` điều phối. Nó có admin/cashier1/warehouse1, chưa có Manager, luôn thêm lại seed role và hiện từ chối seed account bị khóa/đổi email. Cần sửa các hành vi này để quản lý tài khoản không bị startup seed đảo ngược.
- Có sẵn TestServer/Mvc.Testing trong Backend.UnitTests và PostgreSQL tests trong Backend.IntegrationTests. `TestJwtAuthentication` sao chép cấu hình Program; các host/token builders tạo subject ngẫu nhiên chưa có identity state. Đây là các callers phải cập nhật, không chỉ tests Auth.

## Goals / Non-Goals

**Goals:** dùng hạ tầng hiện có, thu hồi quyền ở request được xác thực sau commit, không lộ toàn bộ doanh số qua Orders của Sales, có invariant Admin cuối cùng được bảo vệ ở database transaction, và luồng Admin quản lý tài khoản hoàn chỉnh qua API/UI.

**Non-Goals:** không đổi nền tảng auth, không thêm ASP.NET Identity schema/OAuth, không tạo permission editor hoặc role CRUD, không đổi trạng thái/tính tiền/khóa kho của Orders. Không hủy ngược request nghiệp vụ đã vượt qua xác thực trước khi revocation commit. Không triển khai menu/landing page toàn bộ theo role, tách Settings cá nhân, che giá vốn, mở Customers cho Sales hay dashboard cho Manager (Sprint 2).

## Decisions

### D1. Một bảng quyền Sprint 1 và fallback bắt đăng nhập

Bảng đầy đủ nằm trong `specs/api-role-authorization/spec.md`; khi apply xuất cùng nội dung sang `docs/API_PERMISSIONS.md`, ghi riêng các quyền dự kiến Sprint 2 để không mô tả chúng như đã có. Dùng policy tên theo hành động (hoặc role constants chung nếu gọn hơn), bốn role enum cố định, không tạo engine permission động. Mọi endpoint nghiệp vụ có kiểm tra một trong bốn role phù hợp; fallback chỉ chịu trách nhiệm yêu cầu authenticated user. Cả default policy và fallback dùng Bearer scheme cùng kiểm tra tài khoản.

AllowAnonymous chỉ trên login, refresh, health và GET store settings hiện hữu. Giữ static product images là tài nguyên public và Swagger chỉ Development. Public store DTO hiện hữu chỉ chứa thông tin hiển thị cửa hàng, không mở thêm dữ liệu user/banking/secrets. Soát tất cả controllers trong bảng và endpoint metadata để tránh quên action mới.

Chọn giữ Customer/Analytics Admin-only trong Sprint 1 vì Sprint 2 đã phân công mở quyền cụ thể. Catalog write giữ Admin/Manager; Inventory write giữ Admin/Warehouse. Quyền đọc Catalog/Inventory của bốn role hiện vẫn trả DTO đang có, có thể bao gồm giá vốn; đây là giới hạn được ghi rõ cho Sprint 2, không tuyên bố đã bảo vệ giá vốn.

### D2. JWT token_version và kiểm tra identity qua service

Thêm `User.TokenVersion` kiểu long, cột `users.token_version bigint NOT NULL DEFAULT 0`, check >= 0. JWT thêm claim `token_version`; token thiếu/sai version bị từ chối, kể cả token cũ trước nâng cấp. Sau cryptographic validation, OnTokenValidated gọi một method xác minh trong IAuthService; service đọc trạng thái qua IUserRepository với AsNoTracking. Kiểm tra subject/name identifier là cùng GUID khác Empty nếu có nhiều dạng claim; user tồn tại, active, có đúng một role hợp lệ, role JWT trùng và version bằng DB. Không inject repository/DbContext vào controller; không cache identity state trong sprint này để tránh cửa sổ giữ quyền cũ.

Lock/unlock thực sự đổi trạng thái, đổi role và reset password tăng version rồi revoke tất cả refresh tokens cùng transaction. Không tăng version khi request status/role là no-op. Logout cũng tăng version và revoke toàn bộ refresh tokens của chính caller; optional refreshToken không thay đổi phạm vi user. Chọn account-wide logout để tránh thêm bảng session/JTI blacklist; đây là thay đổi tương thích có chủ đích. Các request đã được xác thực trước commit có thể hoàn tất; tất cả request xác thực sau commit phải bị chặn.

Dùng lại PasswordHasher<User> hiện có để giữ password hashes. Mô hình quản trị một role/tài khoản phù hợp UserProfileDto.Role và session/UI hiện hữu; API thay role bằng một giá trị, không thêm role thứ hai. Legacy zero/multiple roles không được tự suy diễn hoặc sửa: từ chối login, Admin có thể gán lại một role. Migration không xóa role associations. Trước rollout kiểm tra vẫn có ít nhất một active Admin có thể đăng nhập.

### D3. Refresh rotation dùng transaction và khóa user trước token

`POST api/auth/refresh` nhận RefreshTokenRequest, trả LoginResponseDto. Hash raw token rồi lookup read-only bằng unique token_hash index đã có để xác định user ID; không log raw token. Bắt đầu transaction, khóa user bằng FOR UPDATE, sau đó khóa token theo ID/hash và kiểm tra lại active/expiry/revoked/role. Đánh dấu consumed, thêm token mới, cấp JWT theo version hiện tại, SaveChanges và Commit trước khi trả response. Mọi lỗi rollback bằng CancellationToken.None để dọn ChangeTracker ngay cả khi caller hủy.

Login cũng khóa user và đọc lại role/active trước khi cấp token để không phát phiên từ snapshot trước khóa/đổi role. Logout và account access changes dùng cùng thứ tự user → refresh tokens (ID tăng dần khi khóa nhiều token). Không có nhánh token → user. Hai refresh đồng thời chỉ một token thắng; replay token đã consumed nhận 401, không thu hồi replacement của request thắng. Không cần family tree/replay detector ở sprint này.

Auth errors dùng ProblemDetails chung; empty refresh là 400, token không dùng được là 401, thiếu quyền action là 403. Tách phần đăng ký JWT/auth policy/error events vào extension backend dùng được cả Program và TestServer để tests không chạy cấu hình yếu hơn production. Chỉ đưa thông báo an toàn ra response, giữ chi tiết lỗi nội bộ trong server logs không chứa credentials.

### D4. Quản lý tài khoản và bảo vệ Admin cuối cùng

Thêm UsersController → IUserManagementService/UserManagementService → mở rộng IUserRepository; module-internal UserSearchCriteria chứa filter đã parse. Read methods AsNoTracking, write/lock methods tracked; mọi Save/Begin/Commit/Rollback qua IUnitOfWork.

| Method/route | Contract |
| --- | --- |
| GET api/users | q, role, isActive, page/pageSize; PagedResultDto<UserAccountDto> |
| GET api/users/{id} | UserAccountDto hoặc 404 |
| POST api/users | username, fullName, email, password, role; 201 + Location + DTO |
| PUT api/users/{id} | fullName, email; 200 DTO; username bất biến |
| PATCH api/users/{id}/status | required bool isActive; 200 DTO |
| PUT api/users/{id}/role | named role; 200 DTO |
| POST api/users/{id}/reset-password | password; 204 |

Account DTO dùng nullable role cho legacy zero/multiple roles, hiển thị chưa gán/không hợp lệ để Admin chọn lại; active Admin trong guard chỉ tính tài khoản có duy nhất role Admin. DTO chỉ metadata trong spec; không tái dùng entity hoặc trả password/token version. Validators trả tất cả field errors camelCase, unknown/numeric enum từ chối 400; duplicate conflict 409. Chọn quy tắc mật khẩu mới 8..128 ký tự không trim, không áp lại để vô hiệu hóa mật khẩu cũ.

Dùng row của role Admin làm khóa chung bằng repository FOR UPDATE trong mọi account-admin mutation; tiếp đó khóa actor/target user theo GUID tăng dần, rồi refresh rows. Đọc lại active Admin của actor sau khi chờ khóa, tính số active Admin hiện hành và từ chối 409 nếu target change làm về 0. Tất cả promote/demote/lock/unlock qua service này, nên hai thao tác trên hai Admin khác nhau vẫn được tuần tự hóa. Chọn row lock sẵn có thay vì count ngoài transaction hoặc khóa riêng từng target, vốn không bảo vệ trường hợp cuối hai Admin cùng bị demote. Không tạo nested transaction. Login/refresh/logout không khóa role row và không đổi role/status nên không đảo thứ tự khóa.

Quyền actor có thể đã bị request khác thu hồi trong lúc chờ: revalidate sau khóa và trả 401/403 phù hợp; không dùng quyền cũ để thay đổi tài khoản. Self-demote/lock chỉ được phép khi vẫn còn active Admin khác và phiên tự bị xóa ngay sau thành công. Không có hard delete hoặc workflow gửi email/reset link.

### D5. Uniqueness tại database và nâng cấp bảo toàn dữ liệu

Giữ username/email dạng text hiện có. Chuẩn hóa input bằng trim, username về lowercase; email so trùng không phân biệt hoa thường. Thêm unique expression B-tree indexes trên `lower(btrim(username))` và `lower(btrim(email))` với tên constraint/index rõ ràng để DuplicateKeyException chuyển thành 409. Tái dùng raw-column indexes hiện hữu cho tới khi có lý do xóa; không tự mở rộng schema thành bộ normalized columns. Repository login/lookup dùng cùng phép chuẩn hóa. Với expression indexes cần SQL migration rõ ràng và kiểm thử pg_indexes/model migration diff, không giả rằng EF tự suy ra expression từ LINQ.

Preflight dữ liệu để phát hiện normalized duplicates trước khi tạo index, báo IDs cần xử lý và dừng migration nếu xung đột; tuyệt đối không merge/xóa/đổi username/email/password tự động. EF config cho token_version, migration, snapshot và schema.dbml phải đồng bộ. Không thêm FK mới; giữ các FK indexes và timestamps UTC đã có. Migration không đổi ID, cashier history, user_roles hay password_hash.

### D6. Phạm vi Orders qua service và query, không lọc sau phân trang

Controller nhận identity hợp lệ và role được server xác minh; bỏ GetUserId fallback. Truyền actor/role vào các service read/transition có liên quan, tạo criteria có CashierUserId cho Sales từ chính actor; không thêm client query cho phép override nó. Repository áp cùng predicate trước Count và Skip/Take. Detail kiểm tra ownership trước mapping; transition kiểm tra ownership trên order đã khóa, trước validation/state/stock changes. Foreign order của Sales trả 404, Manager write bị policy chặn 403. Cập nhật IOrderService, OrderSearchCriteria/repository, FakeOrderRepository và mọi caller/test, giữ nguyên audit actor và inventory lock order hiện có.

### D7. Desktop refresh trước request và thay phiên nguyên tử

Dùng lại Generation trong ISessionStore. Thêm thao tác compare-and-replace phiên/token với expected generation và token pair gốc; cập nhật bộ nhớ, RememberMe và persistence trong cùng vùng đồng bộ để một response refresh hoặc login cũ không ghi đè phiên mới. Không dùng SetSession vô điều kiện cho refresh. Refresh giữ Generation của tài khoản và không phát SessionChanged để không dựng lại Shell; 401 so thêm access token đã gửi để không expire pair mới. Coordinator chia sẻ operation theo Generation cùng refresh token gốc; mỗi pair chỉ refresh một lần. Lưu expiresAt từ ExpiresIn và RememberMe cùng persisted record; record cũ thiếu metadata được kiểm tra/refresh một lần qua bootstrap trước khi mở protected data. Login operation cũng có generation guard để kết quả đến muộn không thay tài khoản mới.

Một coordinator nhỏ cho refresh trước gửi request khi token còn <= 60 giây hoặc đã hết hạn; một operation chung cho mỗi generation, các callers chờ không được tự hủy shared work khi chỉ một caller cancel. Endpoint auth login/refresh bỏ qua bearer preflight; refresh dùng HttpClient cùng base origin với redirects tắt và không đi vòng lại handler. Sau refresh, handler lấy snapshot đã kiểm chứng lại và gửi request business đúng một lần. 401 business request vẫn expire matching generation, không reactive refresh hoặc replay. 403 giữ session.

Refresh token có thể đã consumed dù response mất mạng: không tự retry refresh cũ; expire matching session và yêu cầu login với thông báo recovery an toàn. Không gửi business write trước khi refresh chắc chắn thành công. RememberMe=false xóa disk session cũ; refresh giữ lựa chọn này. Startup remembered session gọi me/refresh trước khi nhận session là usable. Logout luôn clear local state trong finally; trường hợp offline chỉ xác nhận local logout.

### D8. UI tài khoản là route con của Settings

Thêm AppRouteKind cho quản lý tài khoản dưới Settings; giữ sáu Shell destinations và Settings selected. Cập nhật route validation/factory/VerifyAllRoutesResolve, DI và headless linked sources. Chặn route này ở navigation/preparer và ViewModel khi role khác Admin; không áp dụng lại toàn bộ shell role filtering trong sprint này. Test route resolution dùng active Admin fixture thay vì bypass production guard.

Thêm Identity/UserAccount models, IUserManagementRepository, PageViewModel danh sách và các form/dialog nhỏ dùng pattern dialog hiện có. Tái sử dụng SearchBoxControl, RowActionsButton nếu phù hợp, PagedResult, token styles và field-error presentation. View không HTTP; ViewModel không WinUI controls. C# Markup bindings theo MarkupReferencePage, commands trong template gán trực tiếp ViewModel command và parameter là object? kiểm tra type.

D8 ghi nhận mở rộng ngoại lệ PasswordBox của LoginPage sang form tạo/reset mật khẩu tài khoản: PasswordChanged chỉ đồng bộ input không phải DependencyProperty; clear sau success/close. Nếu cần dùng lại PropertyChanged để hiện/ẩn password, giới hạn đúng ngoại lệ đó và unsubscribe khi Unloaded. Ngoại lệ này chỉ áp dụng PasswordBox; các field và display state khác vẫn dùng bindings/commands. Dialog RequestClose/Hide và lifecycle theo pattern hiện hữu. Confirmation cho khóa, đổi role và reset password; không log/persist giá trị password. Account route bị deactivate/clear nếu session không còn Admin, loại bỏ late responses bằng activation lease và generation guard.

Account page/dialogs dùng AppColors trực tiếp trên element và styles chỉ non-color. Khi thêm entry Settings, chuyển các brush màu static của SettingsPage sang theme accessors để kiểm tra end-to-end Light/Dark đọc được; không refactor Shell/Login hoặc xóa StitchTokens toàn repo trong sprint này. UI bằng tiếng Việt, loading/empty/error/retry, keyboard/focus/resize từ 800×600.

Ba commands khóa/role/reset giữ nút focusable trong thời gian xác nhận (AllowConcurrentExecutions trên RelayCommand, không disable nút bằng IsBusy); IsBusy trong MutateAsync vẫn tuần tự hóa toàn bộ mutation và chặn mọi lần nhấn lặp. Nhờ đó presenter trả focus về đúng nút sau khi hủy dialog thay vì mất focus về Shell. Kiểm chứng bằng headless pending-confirmation test và runtime focus probe.

### D9. Seed bổ sung và kiểm thử đúng hệ thống

Bổ sung manager1 với deterministic `DemoSeedManifest.Id("user:manager1")`, email manager1@techstore.vn và password demo `Manager@123` chỉ khi identity mới. Giữ tuple/callers hiện có cho data nghiệp vụ hoặc mở rộng đúng các nơi dùng; không đổi cashier/warehouse ownership hay anchor ngày demo. Role mặc định chỉ gán cho identity mới; user cũ giữ profile, email, mật khẩu, role, IsActive và TokenVersion. Matching ưu tiên deterministic ID rồi natural username; phát hiện bản ghi trùng/ambiguous thay vì overwrite. Mở rộng seed tests chứng minh rerun không re-add role, không unlock và không reset password.

HTTP tests chạy auth registration chung với production và identity fixtures có user/role/token_version thật. Không tắt validation trong TestJwtAuthentication để cứu tests cũ. Backend.IntegrationTests dùng database test riêng; nếu host ở project này cần TestHost thì tái sử dụng project Backend.UnitTests đã có phụ thuộc hoặc thêm dependency test-only đúng nhu cầu, không thêm runtime library. Hai-request tests có điểm đồng bộ và hai connection/DbContext riêng cho refresh rotation, last-Admin và refresh-vs-lock; assertions kiểm tra database sau commit/rollback. Không tính tests chạy tuần tự là race.

Cập nhật Catalog/Inventory/Customers/ProductImages/Analytics authorization hosts và live API fixtures đang tự tạo JWT hoặc đọc Catalog không token. `OrdersApiIntegrationTests` phải login thật trước đọc Catalog/Orders và không mặc định ID Admin. Sprint này không sửa semantics VietQR hay thay concurrency suite kho của Sprint 5.

## Risks / Trade-offs

- [Logout đăng xuất mọi thiết bị, token cũ thiếu version bị vô hiệu] → ghi rõ release/README/UI, yêu cầu login lại sau rollout; không cần schema session mới.
- [Mỗi protected request thêm một identity lookup] → query theo PK, AsNoTracking, không cache quyền cũ; chỉ tối ưu khi đo được vấn đề.
- [Role Admin row tuần tự hóa account writes] → lưu lượng quản trị thấp; giữ transaction ngắn và lock order chung, kiểm thử đồng thời thật.
- [Schema cũ cho phép nhiều role, normalized duplicates] → không sửa dữ liệu âm thầm; preflight và báo tài khoản cần Admin xử lý, fail closed với role không rõ.
- [FileSessionStorage có rủi ro credentials tại disk như hiện tại] → Respect RememberMe, clear đúng phiên, không thêm log/token dump; chưa thay sang OS credential vault trong sprint này.
- [Sales vẫn nhận trường giá vốn của Catalog/Inventory] → ghi cụ thể trong bảng quyền Sprint 1 và bàn giao Sprint 2; chưa nghiệm thu an toàn field-level đa vai trò.
- [Login vẫn mở Dashboard cho các role chưa được quyền] → API trả 403 an toàn; landing/menu theo role thuộc Sprint 2, không coi M3 hoàn tất sau Sprint 1.
- [Auth revocation không rollback request đã được xác thực trước commit] → acceptance chỉ bảo đảm request xác thực sau commit, kiểm thử với điểm đồng bộ rõ.

## Migration Plan

1. Trên database test riêng, kiểm tra normalized username/email duplicates, zero/multiple roles, ít nhất một active Admin, và password/profile snapshots. Không tự sửa tài khoản không hợp lệ.
2. Sinh migration cho token_version và expression unique indexes, cập nhật model snapshot/config/schema.dbml. Kiểm tra apply trên database mới và M2 hiện hữu; failure không để index/cột dở dang.
3. Triển khai backend/client cùng đợt: token thiếu version được yêu cầu login lại; không chấp nhận mixed backend cũ còn bỏ qua revocation. Seed thêm Manager, giữ user edits và business anchor.
4. Chạy HTTP regression, PostgreSQL concurrency, frontend headless và UI runtime. Ghi build/OS/target, expected/actual, ảnh/log; Windows native chưa chạy phải ghi chưa kiểm tra, không tính đạt.
5. Rollback ứng dụng auth về bản cũ có thể khôi phục quyền JWT đã bị thu hồi: trước rollback chặn traffic, đổi signing key qua secret/config để vô hiệu tất cả JWT, revoke refresh tokens và xóa session client. Ưu tiên giữ cột/index additive; chỉ down migration sau kiểm tra compatibility và backup. Không rollback bằng xóa database hoặc reset passwords.
