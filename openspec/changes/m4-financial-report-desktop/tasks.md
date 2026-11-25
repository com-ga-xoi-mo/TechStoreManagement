# Tasks

## 1. Hợp đồng đọc Analytics

- [ ] 1.1 Thêm `IFinancialReportRepository`, tích hợp `AnalyticsApiRepository` với `IApiClient` và đăng ký DI trong Dashboard/Analytics. Kiểm thử method, route, query `period=custom` với ngày ISO, truyền cancellation và giữ nguyên truy vấn Dashboard M3.
- [ ] 1.2 Map DTO và JSON D5 của change phần bán sang model client. Bổ sung trường optional theo D5/D6 của change 2 trong lớp đọc client. Dùng fixture JSON cố định trong test headless để đối chiếu kỳ, `asOf`, số dương/âm/0, ngày 0, thứ tự top và người bán, trạng thái null/thiếu, thuế hoàn, Cash/QR, ca và bảo hành. Kiểm payload lệch kỳ, thiếu ngày hoặc thiếu trường bắt buộc được báo lỗi.

## 2. Kỳ, trạng thái và phiên

- [ ] 2.1 Gắn ViewModel báo cáo vào Dashboard scope và kiểm Active generation, live mode, Admin/Manager trước khi mở và gửi request. Kiểm headless với bốn role, preview và phiên không hoạt động. Xác nhận Dashboard M3 vẫn tải theo luồng live/preview và chế độ mặc định hiện có.
- [ ] 2.2 Tách ngày nháp, kỳ đã áp dụng và snapshot. Kiểm headless mặc định bảy ngày qua biên UTC+07, khoảng 1–31 ngày, định dạng sai, khoảng ngược và ngày tương lai. Kiểm Làm mới/Thử lại dùng kỳ đã áp dụng sau khi người dùng sửa nháp, và snapshot luôn đi cùng metadata của nó.
- [ ] 2.3 Gắn request báo cáo vào tracking của activation với CTS và sequence riêng. Dùng fake bất chấp cancellation để kiểm khi đổi A sang B, cả phản hồi thành công, lỗi và kết thúc của A đều không ghi đè B. Kiểm rời report/trang, đóng app, logout và đổi Admin sang Manager hoặc role bị chặn khi đang tải. Kiểm scope đợi request thoát trước khi dispose và request Dashboard chạy độc lập.
- [ ] 2.4 Map loading, nội dung, dữ liệu rỗng, lỗi và hủy dự kiến. Kiểm headless nhánh 400, 403 giữ phiên, 401 theo transport hiện có, timeout, 5xx và payload sai. Kiểm khả năng retry đúng kỳ khi còn quyền, ngày 0 vẫn xuất hiện, và dữ liệu thiếu hoặc lỗi không trở thành kỳ rỗng.

## 3. C# Markup trong Tổng quan

- [ ] 3.1 Thêm bộ chọn chế độ nội bộ, picker, trạng thái và dòng kỳ/`asOf` trong `DashboardPage`. Dùng bindings, commands và `AppColors.Brushes` trực tiếp trên phần tử. Kiểm thao tác mở report, quay về Tổng quan với lựa chọn kỳ M3 còn nguyên và mở lại report theo kỳ đã áp dụng của activation.
- [ ] 3.2 Hiển thị tổng kỳ, chi tiết từng ngày, bán/hoàn, thuế, dòng tiền, giá vốn, lãi gộp, bảo hành, hai bảng sản phẩm, hiệu suất người bán và Cash theo ca. Dùng giá trị và thứ tự từ API. Kiểm headless định dạng tiếng Việt/VND, `asOf` lúc 09:00 UTC+07 của JSON mẫu, số 0, dấu âm, phần lẻ và nhãn “Chưa tính được”.
- [ ] 3.3 Thêm automation name, thứ tự Tab, focus khi đổi chế độ hoặc retry và bố cục phù hợp cửa sổ hẹp. Kiểm bằng bàn phím đến bộ lọc, retry và chi tiết ngày. Xác nhận panel ẩn không nhận focus, các bảng còn đọc được sau resize và thao tác giao diện không tạo request ngoài dự kiến.

## 4. Nghiệm thu live và bằng chứng

- [ ] 4.1 Chạy headless suite của frontend và nghiệm thu desktop với API đã tích hợp change 2 trên PostgreSQL. Đối chiếu kỳ, `asOf`, từng ngày, tổng kỳ và các bảng với response live. Kiểm Admin/Manager, role bị chặn, đổi kỳ nhanh và chuyển phiên trong lúc tải. Ghi SHA client/API, dữ liệu mẫu, kết quả và điều kiện chạy lại cho nguồn chưa kiểm được vào `docs/M4_FINANCIAL_REPORT.md`.
- [ ] 4.2 Build và chạy Skia desktop (`dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`). Ghi mục Skia trong `docs/M4_FINANCIAL_REPORT.md` với OS, target, SHA, cách chạy, role, kỳ và kết quả tiếng Việt/VND, bàn phím/focus, 800×600/1024×768/1440×1000 DIP và đổi Sáng/Tối ngay trên report đang mở.
- [ ] 4.3 Build và chạy Windows native (`dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-windows10.0.26100`). Ghi mục Windows native riêng với cùng các ca và thông tin của target này trong `docs/M4_FINANCIAL_REPORT.md`. Nếu build lỗi, ghi “Chưa kiểm tra” kèm lệnh, lỗi thực tế và điều kiện chạy lại.
- [ ] 4.4 Kiểm diff của phần apply nằm trong Dashboard/Analytics client, test của Bình và file bằng chứng đã chỉ định. Xác nhận không có file tạm và nếu tạo commit thì message không có dòng `Co-Authored-By` của AI.
