# Chốt nghiệm thu identity-access-management — 16/11/2026

Change hoàn tất **37/37 task**. Phần triển khai đã merge vào `dev` qua [PR #20](https://github.com/com-ga-xoi-mo/TechStoreManagement/pull/20). Lần chốt này cập nhật kết quả nghiệm thu và checklist, không thay đổi logic ứng dụng.

## Hai task cuối

| Task | Bằng chứng và kết quả |
| --- | --- |
| 6.4 — Giao diện tài khoản | Agent đã chạy trang thật, kiểm tra bindings, chọn dòng, sửa hồ sơ, role, khóa/mở khóa, confirmation, chặn Admin cuối cùng, bàn phím/focus, resize và theme. Các ảnh/log nằm trong báo cáo local ngày 12/11 (`docs/verification/identity-access-management-2026-11-12/`). Sau khi được bàn giao form `ui_acceptance12`, người dùng phản hồi **“Đã thực hiện cả hai bước”** cho tạo tài khoản và đặt lại mật khẩu qua UI. Hoàn tất theo nghiệm thu kết hợp của agent và người dùng. |
| 8.2 — Nghiệm thu UI và HTTP | UI như trên; 69 kiểm tra HTTP trên PostgreSQL đã đạt: safe metadata, role scope, Admin cuối cùng, ownership Orders, thu hồi JWT/refresh, reset/logout và refresh rotation. Headless tests kiểm tra clear password sau success/close và không replay business write. Không còn acceptance Sprint 1 bị hoãn. |

Phân biệt nguồn bằng chứng: hai thao tác mật khẩu trên UI do **người dùng xác nhận**, không phải agent tự nhập hoặc trực tiếp quan sát kết quả. Khi đối chiếu ngày 16/11, app nghiệm thu đã đóng và database tạm không có bản ghi `ui_acceptance12`; không dùng database đó để khẳng định hai thao tác đã được quan sát độc lập. Bằng chứng HTTP/headless được ghi riêng, không thay thế mô tả nguồn nghiệm thu UI.

## Kiểm tra lại trên mã đã merge

- Frontend headless: **936 passed / 0 failed / 0 skipped**. Log local: `docs/verification/identity-access-management-2026-11-16/frontend-tests.log`.
- Backend, gồm PostgreSQL migration/seed/concurrency: **593 passed / 0 failed / 0 skipped**. Log local: `docs/verification/identity-access-management-2026-11-16/backend-tests.log`.
- Desktop `net10.0-desktop`: build thành công, **0 warnings / 0 errors**. Log local: `docs/verification/identity-access-management-2026-11-16/desktop-build.log`.
- `openspec validate --specs --strict` và `git diff --check`: đạt.
- Live API 3 test, 69 HTTP checks và 14 runtime assertions dùng bằng chứng ngày 12/11 trên cùng phần triển khai; không ghi thành các lần chạy lại ngày 16/11.

Lần khởi chạy backend tests đầu tiên gặp khóa `TechStore.Shared.deps.json` do chạy build song song với frontend. Đã chạy lại tuần tự và đạt; không sửa source để xử lý lỗi điều phối build này.

Môi trường: macOS arm64, .NET 10, Uno 6.7.30, PostgreSQL 16. Windows native và HighContrast vẫn **chưa kiểm tra** vì không có môi trường Windows; task 8.3 đã ghi giới hạn này. Không đánh dấu các hạng mục Sprint 2–5. JSON session storage và quyền đọc cost fields giữ các giới hạn đã công bố trong Identity README/bảng quyền.

## Chạy lại kiểm tra

```sh
TECHSTORE_TEST_CONNECTION='<PostgreSQL connection có quyền CREATE DATABASE>' \
  dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --no-restore
dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj --no-restore
dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore
openspec validate --specs --strict
git diff --check
```

Chạy các lệnh build/test tuần tự vì cùng dùng output của `shared/`. `docs/verification/` được repository chủ động git-ignore; log/ảnh là artifacts local, không phải file đã commit. Báo cáo này lưu kết quả và nguồn bằng chứng để reviewer đọc được trong PR.

PostgreSQL unit fixtures tự tạo/xóa database riêng; lần chốt không sửa database phát triển và không xóa database nghiệm thu cũ. Đã đồng bộ cả 7 delta specs vào main specs và [archive change ngày 16/11/2026](../openspec/changes/archive/2026-11-16-identity-access-management/tasks.md). Kiểm tra strict toàn bộ main specs đạt 32/32; giữ nguyên requirement/scenario ngoài phạm vi change.

Các kiểm tra liên quan trong source: [Identity HTTP](../tests/Backend.UnitTests/IdentityHttpTests.cs), [concurrency PostgreSQL](../tests/Backend.UnitTests/IdentityConcurrencyPostgresTests.cs), [frontend account](../tests/Frontend.UnitTests/Identity/UserManagementTests.cs), [runtime theme/focus](../frontend/TechStore.Client/Testing/IdentityThemeProbe.cs). Bảng quyền: [API_PERMISSIONS.md](API_PERMISSIONS.md).
