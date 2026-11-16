# Tasks

## 1. Client

- [ ] 1.1 `SettingsViewModel.CanEditStore` từ `RoleAccess`; `SaveStoreSettingsAsync` không gửi request khi không được phép; tests bốn role.
- [ ] 1.2 `SettingsPage`: nhóm Cá nhân trước, nhóm Cửa hàng chỉ đọc + ẩn nút lưu khi `!CanEditStore`.

## 2. Kiểm thử thủ công (AI)

- [ ] 2.1 `PUT /api/settings/store` bằng token Sales/Warehouse → 403, Manager → 200 (khôi phục giá trị cũ sau khi thử).
- [ ] 2.2 Headless: `SettingsViewModel` với session Sales gọi lệnh lưu → repository fake không nhận request; Manager → nhận đúng một request.
- [ ] 2.3 Chạy app Skia, đăng nhập Sales mở Cài đặt, đổi Sáng/Tối; ghi kết quả/ảnh nếu môi trường cho phép.
