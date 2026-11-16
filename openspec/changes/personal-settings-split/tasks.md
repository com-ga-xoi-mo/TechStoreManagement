# Tasks

## 1. Client

- [x] 1.1 `SettingsViewModel.CanEditStore` từ `RoleAccess`; `SaveStoreSettingsAsync` không gửi request khi không được phép; tests bốn role.
- [x] 1.2 `SettingsPage`: nhóm Cá nhân trước, nhóm Cửa hàng chỉ đọc + ẩn nút lưu khi `!CanEditStore`.

## 2. Kiểm thử thủ công (AI)

- [x] 2.1 `PUT /api/settings/store` bằng token Sales/Warehouse → 403, Manager → 200 (khôi phục giá trị cũ sau khi thử).
- [x] 2.2 Headless: `SettingsViewModel` với session Sales gọi lệnh lưu → repository fake không nhận request; Manager → nhận đúng một request.
- [ ] 2.3 Chạy app Skia, đăng nhập Sales mở Cài đặt, đổi Sáng/Tối; ghi kết quả/ảnh nếu môi trường cho phép.

## Kết quả kiểm thử (AI, 2026-11-17)

- 2.2 `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj --filter FullyQualifiedName~SettingsViewModelTests --logger "console;verbosity=detailed"`: **10 Pass / 0 Fail**. `SaveStoreSettings_RoleControlsRepositoryCall` bao phủ Admin/Manager/Sales/Warehouse/Unknown; role bị cấm không gọi repository, Manager gửi đúng request. **Pass headless**, `/tmp/m3-settings-tests.log`.
- Desktop build **Pass**, 0 warning/error (`/tmp/m3-settings-build.log`). Chạy Skia trỏ API thật timeout 15 giây (124), có cảnh báo Uno dev-server/X11 (`/tmp/m3-settings-skia.log`).
- 2.3 **Chưa kiểm tra** đăng nhập Sales và đổi theme Sáng/Tối trực quan trên UI Wayland; giữ checkbox mở. Không coi headless hoặc startup log là đạt binding/theme.
- 2.1 API thật: login `cashier1`/`warehouse1`/`manager1` đều 200; Sales/Warehouse PUT 403 và GET xác nhận không đổi; Manager PUT 200, GET thấy thay đổi; PUT khôi phục 200, GET cuối `restored=true`. **Pass**, `/tmp/m3-settings-api.log`.
- Sai sót phép thử: probe ban đầu dùng nhầm `sales`/`sales1` thay vì `cashier1`, đã tạo user thử `sales1` (ID `926972e2-db35-4a85-a117-4a287d9b215b`) ngoài dự kiến. Đã xóa đúng user này và user_roles của nó trong transaction ở DB test; query sau dọn: users/user_roles/refresh_tokens theo ID đều 0. Không đổi tài khoản có sẵn; phép thử Sales thành công dùng `cashier1`, không dùng user thử. Không tính các probe sai là đạt.
