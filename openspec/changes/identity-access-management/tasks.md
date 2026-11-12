# Tasks

## 1. Contracts và schema tài khoản

- [x] 1.1 Thêm shared RefreshTokenRequest và requests/DTO quản lý tài khoản theo design D4; giữ LoginResponseDto cho refresh, UserAccountDto không chứa secrets và role nullable cho legacy role không hợp lệ; kiểm tra build shared và serialization tests không xuất hash/token/version.
- [x] 1.2 Thêm User.TokenVersion cùng EF configuration/check >= 0; migration và snapshot cho cột bigint default 0 và unique indexes lower(btrim(username/email)), giữ refresh indexes sẵn có; kiểm chứng trên database test mới và nâng cấp từ M2, giữ nguyên password hash/user IDs/history.
- [x] 1.3 Bổ sung preflight normalized duplicates và active Admin có thể login; test migration từ chối dữ liệu trùng mà không tự đổi/merge/xóa tài khoản, kiểm tra pg_indexes và đồng bộ `docs/database/schema.dbml` cùng hướng dẫn migrate/rollback trong Identity README.
- [x] 1.4 Mở rộng IUserRepository/IRefreshTokenRepository với query read-only AsNoTracking và methods tracked/locks theo design D3–D4; cập nhật FakeUserRepository/FakeRefreshTokenRepository và kiểm tra test query/lock/normalization trên PostgreSQL, không save hoặc mở transaction trong repository.

## 2. Login, kiểm tra JWT và vòng đời token

- [x] 2.1 Bỏ role fallback trong AuthService, dùng PasswordHasher hiện có, cấp token_version và kiểm tra active/đúng một role khi login dưới user lock; thêm tests cả bốn role, sai password, inactive, zero/multiple roles và password cũ không đổi.
- [x] 2.2 Thêm xác minh identity sau JWT validation qua IAuthService và repository; tách auth registration dùng chung Program/TestServer, trả ProblemDetails an toàn 401/403 và Bearer challenge; HTTP tests token hết hạn/giả mạo/sai issuer-audience, subject thiếu/sai/Empty/unknown/conflicting và version thiếu/cũ đều bị chặn.
- [x] 2.3 Thêm POST /api/auth/refresh rotation dưới transaction user → refresh token, dùng unique token_hash index và thời gian từ TimeProvider; unit tests expiry/revoked/reuse/blank input, replacement chỉ trả sau commit và exception/cancellation rollback cả consumption lẫn token mới.
- [x] 2.4 Đổi logout sang revoke toàn bộ phiên của caller, tăng TokenVersion và revoke refresh tokens cùng transaction; HTTP tests JWT cũ trả 401 ngay ở request xác thực sau commit, optional foreign refreshToken không ảnh hưởng user khác và logout không dùng actor fallback.
- [x] 2.5 Thêm PostgreSQL tests hai request/giao dịch độc lập có barrier cho refresh cùng token và refresh-vs-logout/lock; kiểm chứng chỉ một refresh thành công, replacement không bị replay cũ thu hồi, revocation không để token mới còn dùng được sau commit; ghi test database và lệnh chạy trong Identity README.

## 3. Bảng quyền, fallback và Orders

- [x] 3.1 Viết `docs/API_PERMISSIONS.md` từ matrix spec, ghi endpoint, method, quyền đọc/tạo/sửa/xóa, phạm vi dữ liệu, public exceptions và quyền để Sprint 2; kiểm tra từng controller/action hiện hữu đều có dòng tương ứng.
- [x] 3.2 Cấu hình fallback yêu cầu authenticated user; áp role theo matrix cho toàn bộ business actions và AllowAnonymous đúng login/refresh/health/GET store; HTTP tests endpoint không policy vẫn 401, public exceptions truy cập được, Warehouse không sửa giá/tạo tài khoản và Sales không xem analytics.
- [x] 3.3 Bỏ DefaultActingUserId ở OrdersController, nhận actor/role đã validate cho IOrderService và cập nhật mọi caller; HTTP tests mọi Orders method thiếu token 401, Warehouse 403 và audit cashier/movement đúng actor thật, không lưu dữ liệu khi bị từ chối.
- [x] 3.4 Thêm mandatory Sales ownership scope vào OrderSearchCriteria/repository trước Count và paging, detail mapping và status transition trên order đã khóa; cập nhật FakeOrderRepository và service tests: Sales chỉ thấy own items/totals, foreign detail/status 404 không mutation, Manager read được nhưng write 403, Admin giữ luồng hiện có.
- [x] 3.5 Cập nhật TestJwtAuthentication, CategoryApiHost, InventoryApiHost và các Product/Images/Customer/Analytics hosts/token builders dùng auth production và fixtures user/version hợp lệ; chạy backend HTTP regression, giữ tests role Admin-only đúng giai đoạn thay vì disable validation.
- [x] 3.6 Cập nhật TechStore.Api.http, README/API docs và live Catalog/Orders integration fixtures để login thật trước protected reads/writes; kiểm chứng mẫu không dùng token giả hoặc ID Admin fallback, các assertions Catalog anonymous cũ đổi sang 401.

## 4. API quản lý tài khoản và Admin cuối cùng

- [x] 4.1 Thêm UserSearchCriteria, service/controller/DI và GET users/detail; tests filters kết hợp, role không hợp lệ, paging ổn định, empty/404, metadata không secrets và HTTP 401/403 cho mọi role không phải Admin.
- [x] 4.2 Thêm create/profile update với field validation theo spec, username bất biến, một role và PasswordHasher; tests bounds/invalid input, missing required fields, normalized duplicates 409 và transaction failure không để user/role dở dang; ghi contracts và ví dụ trong Identity README/Api.http.
- [x] 4.3 Thêm lock/unlock, set role và reset password, tăng version/revoke tokens khi effective change; tests HTTP old JWT/refresh bị chặn sau mỗi action, unlock không hồi token, password cũ không dùng được, no-op status/role không revoke và 404/400 đúng contract.
- [x] 4.4 Bảo vệ last active sole-role Admin bằng lock Admin role row → actor/target GUID order → refresh rows; revalidate actor sau khi chờ; service tests self-lock/self-demote 409, allowed khi còn Admin khác, rollback không đổi role/status/tokens; ghi thứ tự khóa trong Identity README.
- [x] 4.5 Thêm PostgreSQL concurrent tests bằng hai connection/DbContext và barrier cho last-two-Admins demote/lock và duplicate account creation; khẳng định còn ít nhất một Admin có thể login, chỉ một normalized identity được tạo và account write không dùng quyền actor đã bị thu hồi trong lúc chờ.

## 5. Desktop session và refresh

- [x] 5.1 Thêm expiry/RememberMe và compare-and-replace theo Generation cho session store/persistence; tests replace/expire/clear nguyên tử, remembered restart, non-remembered xóa disk cũ, legacy persisted record bootstrap và late save không ghi đè tài khoản mới.
- [x] 5.2 Thêm refresh coordinator dùng same-origin client không redirect/không recursion, chia sẻ một operation mỗi generation khi token <= 60 giây; fake transport tests nhiều callers chỉ một refresh, cancellation một waiter không hủy callers khác và không gửi token ngoài origin.
- [x] 5.3 Tích hợp preflight vào BearerTokenHandler, refresh pair chỉ commit nếu session gốc còn current, business request gửi tối đa một lần; tests POST/PATCH/DELETE/multipart không replay, 401 không reactive refresh, 403 giữ phiên, refresh reject/mất response không gửi write hay retry token cũ.
- [x] 5.4 Cập nhật AuthRepository/LoginViewModel/App bootstrap/DI cho RememberMe, refresh/me trước khi mở restored session, generation guard cho login đến muộn và logout clear local trong finally; tests role/profile mới, offline logout không báo server revoke thành công và late auth results không hồi phiên đã logout.
- [x] 5.5 Cập nhật headless linked sources và các session/composition/auth fakes bị đổi contract; chạy frontend unit suite và ghi hành vi refresh/logout/RememberMe cùng giới hạn file storage vào client/Identity docs.

## 6. Giao diện quản lý tài khoản

- [x] 6.1 Thêm client account models và IUserManagementRepository gọi các endpoint mới, map DTO sang models; repository tests URI/filter/paging/enum, safe metadata, field errors/409 và reset-password 204 không deserialize JSON.
- [x] 6.2 Thêm PageViewModel danh sách/search/filter/paging cùng commands create/edit/lock/unlock/role/reset và confirmations qua dialog contracts hiện có; headless tests loading/empty/error/retry, chống submit lặp, invalid role state, preserve non-secret inputs khi lỗi, clear password sau success/close.
- [x] 6.3 Thêm route con Settings, entry chỉ cho Admin, route guard và DI/factory/VerifyAllRoutesResolve; navigation tests vẫn sáu destinations, Settings selected, non-Admin direct route không load dữ liệu, logout/role loss hủy activation và late account responses không commit.
- [ ] 6.4 Làm trang/form/dialog bằng C# Markup với tokens, typed bindings, PasswordBox exception được giới hạn tại design D8, commands trong template đúng DataContext; build net10.0-desktop và chạy thật kiểm tra binding, masked password, focus/keyboard, resize và toàn bộ thao tác quản lý tài khoản qua API.
- [x] 6.5 Chuyển màu static trong SettingsPage sang theme accessors khi thêm account entry, giữ thay đổi ngoài phạm vi account ở mức cần thiết; runtime switch Sáng/Tối/System khi account screen/dialog đang mở, xác nhận text/panel/button readable và ghi ảnh/log build-specific, không coi headless/build là nghiệm thu theme.

## 7. Manager demo và seed không đảo quyền

- [x] 7.1 Bổ sung manager1 deterministic ID, Manager role và demo credentials theo design D9, cập nhật manifest/tuple/callers chỉ khi cần; tests database mới/legacy/partial tạo đúng một Manager và vẫn đăng nhập được ba account cũ với password cũ.
- [x] 7.2 Sửa reconciliation identity theo stable ID/natural key, chỉ gán default role cho user mới, giữ user cũ đã đổi profile/email/password/role/status/version; tests seed hai lần sau account changes không unlock, không re-add role, không reset hash/version và không thay business timestamps/số liệu demo.
- [x] 7.3 Cập nhật README tài khoản demo, seed ownership/idempotency và giới hạn quyền từng role trong Sprint 1; chạy seed PostgreSQL regression với DB test riêng, kiểm tra expected analytics fixture không đổi vì thêm Manager.

## 8. Kiểm chứng tích hợp Sprint 1

- [x] 8.1 Chạy backend/frontend unit suites, PostgreSQL identity/concurrency và live API suites với auth thật trên database test riêng; ghi Pass/Fail/Skipped/Chưa kiểm tra và lệnh/env cần dùng, không tính skipped là đạt.
- [ ] 8.2 Nghiệm thu qua UI và HTTP: Admin CRUD-access account, last Admin blocked, Sales only-own Orders/analytics 403, Warehouse price/users 403, lock/role/reset/logout làm token cũ 401 và refresh không replay; ghi expected/actual cho từng acceptance trong M3 Sprint 1.
- [x] 8.3 Build frontend desktop và backend; kiểm tra runtime Skia và Windows native nếu có môi trường, ghi OS/target/build/ảnh-log; phần chưa chạy ghi rõ, kiểm tra hồi quy M2 và không tick các hạng mục Sprint 2–5.
- [x] 8.4 Đối chiếu artifacts/specs/tài liệu/bảng quyền với behavior đã nghiệm thu, cập nhật checklist Sprint 1 trong docs/MILESTONE_3_PLAN.md chỉ theo bằng chứng và chạy `openspec validate identity-access-management --strict` trước bàn giao.
