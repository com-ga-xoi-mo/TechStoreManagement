# UI-02 — biên bản kiểm tra đang mở

**Ngày cập nhật:** 10/10/2026  
**Trạng thái:** Chưa nghiệm thu UI/preview. Auth API thật và JWT/refresh token nằm ngoài UI-02.

## Kết quả manual test của chủ dự án

Ngày 10/10/2026, chủ dự án báo: không nhập được mật khẩu, bấm Đăng nhập không thấy phản hồi và không hiện lỗi. Đây là kết quả **không đạt** của bản trước. Chưa có xác nhận manual test mới cho bản sửa; không được ghi là owner đã duyệt hoặc UI/preview hoàn thành.

## Bản sửa hiện tại

- `LoginPage.xaml` dùng style đúng loại `PasswordBox`; `PasswordChanged` chuyển giá trị vào ViewModel và đồng bộ khi mật khẩu bị xóa. Lỗi bắt buộc của tên đăng nhập/mật khẩu được hiển thị cạnh trường nhập và trường đầu tiên bị lỗi nhận focus.
- So với D11, giao diện giữ bố cục hai vùng và dùng Uno XAML native cùng token UI-00; không sao chép HTML, tài khoản mẫu hay bộ nút chuyển trạng thái vào trang đăng nhập.
- Mặc định ứng dụng ở chế độ `Real` chưa có Auth API: màn đăng nhập hiển thị thông báo chưa khả dụng, không đăng nhập bằng fake repository. Chế độ thử giao diện được bật riêng bằng `TECHSTORE_AUTH_MODE=preview` trong bản Debug; app mở form trống, đăng nhập mock mới chuyển sang Shell với nhãn Preview.
- Lệnh đăng xuất đặt lại lịch sử điều hướng trước khi về form đăng nhập; việc chặn Back vẫn cần kiểm tra trên cửa sổ của chủ dự án.
- Các thay đổi chính nằm tại `Views/Identity/LoginPage.xaml(.cs)`, `ViewModels/Identity/LoginViewModel.cs`, `Styles/TechStoreTheme.xaml`, `App.xaml.cs`, `Services/Session/SessionConfig.cs`, `Services/Navigation/`, `Repositories/Identity/UnavailableAuthRepository.cs` và `tests/Frontend.UnitTests/`.
- Không có HTTP AuthApiClient, JWT, refresh/revoke hay backend login endpoint trong UI-02.

## Bằng chứng đã có

- `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore -v minimal`: đạt, 0 warnings, 0 errors sau khi sửa form và điều hướng.
- `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj --logger 'console;verbosity=normal'`: 10/10 test đạt. Test kiểm tra validation bắt buộc, trạng thái mock, đăng nhập bị từ chối, preview thành công, chế độ chưa có API và chặn gửi trùng khi đang tải. Test này không thay thế tương tác thật với Uno `PasswordBox`.
- Chạy preview bằng `TECHSTORE_AUTH_MODE=preview timeout 10s dotnet run --no-build --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --launch-profile 'TechStore.Client (Desktop)'`: tiến trình hoạt động đến khi timeout chủ động; còn log Uno RemoteControl/HotDesign/X11. Lượt chạy này **không xác nhận** đã nhập liệu/click/kiểm tra giao diện bằng tay.
- Chạy chế độ mặc định bằng `timeout 8s dotnet run --no-build --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --launch-profile 'TechStore.Client (Desktop)'`: tiến trình cũng hoạt động đến timeout chủ động; chưa xác nhận trực quan thông báo chưa có Auth API trên cửa sổ.
- `dotnet test TechStore.sln --no-restore`: 10 frontend test đạt; toàn solution thất bại ở backend `DataSeederTests` vì PostgreSQL báo thiếu bảng `roles` (`42P01`). Đây là lỗi môi trường/schema backend cần xử lý riêng, không phải bằng chứng UI-02 thất bại hay đạt.
- `openspec validate ui-02-login --strict`: đạt. Điều này chỉ kiểm tra artifact, không thay thế test runtime.
- Windows native build/runtime: chưa kiểm tra vì máy hiện tại là Linux.
- Các hàm `Verify()` cũ trong source vẫn chưa được gọi bởi test runner; chỉ 10 xUnit test trong `tests/Frontend.UnitTests/` được xác nhận đã chạy.

## Cách chạy và nghiệm thu còn thiếu

Từ thư mục gốc repository, chạy:

```bash
TECHSTORE_AUTH_MODE=preview dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --launch-profile 'TechStore.Client (Desktop)'
```

Trong preview, tài khoản mock để thử luồng là `admin` / `Admin@123`; đây không phải tài khoản backend và không được điền sẵn trong form. Chủ dự án cần thử lại nhập mật khẩu, nút Đăng nhập, lỗi khi bỏ trống trường, mật khẩu sai, nút hiện/ẩn mật khẩu, Tab/Enter/Escape, cửa sổ thu hẹp, Shell Preview, đăng xuất và Back. Ghi ngày xác nhận mới cùng mục chưa đạt vào đây trước khi đánh dấu task nghiệm thu hoặc bàn giao.
