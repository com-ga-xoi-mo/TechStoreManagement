# Tasks

Tất cả task dưới đây thuộc An và chạy được trên baseline `dev` hiện có. Đối chiếu [design](design.md), đặc biệt bảng ánh xạ task M3 và D1–D5. Không có task triển khai thay chủ module hoặc chờ endpoint tương lai. Specs được bỏ qua có chủ đích bằng `skip_specs: true` vì hành vi nghiệp vụ không đổi.

## 1. HTTP Identity với persistence PostgreSQL thật

- [x] 1.1 Thêm fixture tối thiểu cho `IdentityPostgresHttpTests` trong `tests/Backend.UnitTests/`, tái sử dụng `PostgresDatabase` migrate/seed trên database sinh tên riêng và `WebApplicationFactory<Program>`; xác minh host dùng PostgreSQL, production JWT/services/repositories/UoW, test smoke login/me đạt và database được cleanup cả khi test lỗi, không gọi `AddTestIdentities`.
- [x] 1.2 Bổ sung login/me cho bốn role, sai mật khẩu/tài khoản khóa, JWT thiếu/hết hạn/giả mạo và subject/version sai; xác minh HTTP status, safe ProblemDetails và Bearer challenge đúng contract, token hết hạn ký đúng khóa test, không sleep hoặc tắt lifetime validation.
- [x] 1.3 Bổ sung refresh rotation/reuse và logout account-wide qua nhiều phiên; xác minh token cũ trả 401 sau commit, replacement của lần refresh thắng vẫn dùng được, logout không làm mất phiên tài khoản khác và assertion đi qua PostgreSQL thật.
- [x] 1.4 Bổ sung khóa/mở khóa, đổi role, reset password, status/role no-op và last Admin guard qua HTTP; xác minh JWT/refresh cũ bị thu hồi đúng lúc, login mới mang quyền mới, no-op giữ phiên và last Admin trả 409. Kiểm tra account DTO không chứa password/hash/token/version; tất cả ca chạy đạt trên database tạm.
- [x] 1.5 Bổ sung hướng dẫn suite mới trong `backend/Identity/README.md`: biến `TECHSTORE_TEST_CONNECTION`, filter/lệnh chạy, database tự tạo/xóa, host TestServer + PostgreSQL và giới hạn Kestrel/UI; chạy lệnh được ghi để xác minh có test mới thực thi, không tính skipped là pass và log không chứa secrets.

## 2. Độ phủ quyền endpoint và contract actor hiện có

- [x] 2.1 Thêm baseline method/route/public/roles mong đợi trong `ApiAuthorizationCoverageTests.cs` từ bảng quyền Sprint 1; so sánh hai chiều với production controller endpoints và policy hiệu lực (controller/action/default/fallback/AllowAnonymous), xác minh đủ bốn role. Thử tạm thêm endpoint hoặc đổi role để xác nhận guard fail rõ method/route, rồi hoàn nguyên thay đổi thử; không sinh expected từ metadata đang kiểm.
- [x] 2.2 Thêm HTTP matrix trên host PostgreSQL cho business endpoints thiếu token và các role bị cấm; xác minh đúng 401/403, public allowlist được xử lý theo input riêng và các ca đọc được phép trả 200 + DTO phù hợp. Không chấp nhận 400/404/500 làm bằng chứng quyền đã đúng hoặc gọi mutation trái phạm vi chỉ để lấy success.
- [x] 2.3 Thêm ca actor/ownership Orders bằng hai Sales và dữ liệu test riêng qua API hiện hành: cashier/actor lấy từ JWT, list/count/detail đúng scope, chặn chuyển trạng thái đơn người khác với 404 và dữ liệu không đổi, Admin/Manager đọc đúng quyền; hoàn tất một đơn test để xác minh actor audit đã persist. Chạy các ca này đạt, không thêm POS/ca/công thức/race mới của Đạt hoặc Q. Vinh.
- [x] 2.4 Cập nhật `docs/API_PERMISSIONS.md` phần kiểm chứng và cách chủ module cập nhật baseline khi merge endpoint/đổi quyền; đối chiếu mỗi method/route test với controller và main spec, giữ nguyên quyền Customers/Analytics và cost-field policy Sprint 1. Xác minh không đưa endpoint tương lai vào baseline hoặc viết lại requirement đã archive.

## 3. Tài liệu chạy và bàn giao phần độc lập của An

- [x] 3.1 Sửa README cách chạy API bằng launch profile/cổng 5176 và cấu hình client tương ứng, tài khoản demo, migrate/seed và liên kết hướng dẫn Identity; xác minh lệnh fresh setup/khởi động/health/login bằng backend/database tạm trên môi trường hiện có, seed rerun không đổi credentials theo test PostgreSQL đã có. Ghi rõ lệnh/nền tảng chưa chạy, không chứng nhận Windows từ build macOS.
- [x] 3.2 Tạo `docs/M3_IDENTITY_INTEGRATION_HANDOFF.md` với bảng hiện trạng API/quyền, actor/phiên, trạng thái đơn/ca, công thức tiền, khóa/UoW và migrate/seed; mỗi dòng có nguồn code/spec, owner và mức sẵn sàng. Xác minh đường dẫn tồn tại và mô tả khớp baseline; phần ca/POS/nhập hàng chưa tích hợp ghi đúng mức entity/repository/contract, không khẳng định tình trạng nhánh riêng của owner.
- [x] 3.3 Cập nhật `docs/ROADMAP.md` và `docs/MILESTONE_3_PLAN.md`: sửa mô tả Orders chưa auth/Admin fallback và change Sprint 1 còn tiếp tục; bổ sung các bước con của An gắn bằng chứng cho scope độc lập, danh sách phần chờ Q. Vinh/Bình/Đạt/Vy/T. Vinh. Kiểm diff để giữ nguyên task người khác và checkbox nghiệm thu M3 toàn phần chưa đủ bằng chứng; không sửa lịch sử archive.
- [x] 3.4 Điền kết quả mới vào báo cáo bàn giao: SHA/build, ngày, môi trường, lệnh, Pass/Fail/Skipped/Chưa kiểm tra, tầng TestServer/PostgreSQL và điểm chờ tích hợp; xác minh từng số liệu truy về kết quả lần chạy tương ứng, tách bằng chứng lịch sử Sprint 1 và không để raw logs/secret thành deliverable duy nhất trong thư mục ignored.

## 4. Kiểm tra tổng hợp phạm vi change

- [x] 4.1 Chạy backend regression với PostgreSQL opt-in và frontend headless tuần tự để tránh tranh file shared build; xác minh test mới thực chạy không skip và các regression liên quan đạt. Đối chiếu report đã ghi với kết quả tổng hợp; lỗi thực tế phải có expected/actual/owner, không đổi expectation hoặc skip để che lỗi.
- [x] 4.2 Chạy `openspec validate m3-security-verification-handoff --strict`, `git diff --check` và rà toàn diff; xác minh không đổi production API/UI/schema/seed hoặc làm task của chủ module khác, mọi task change có bằng chứng và các điều kiện nghiệm thu bản tích hợp M3 còn chờ được nêu rõ. Chỉ kết luận hoàn tất phần độc lập của An, không kết luận hoàn tất toàn M3.
