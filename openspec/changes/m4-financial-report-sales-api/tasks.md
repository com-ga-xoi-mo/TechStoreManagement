# Tasks

## 1. Đồng bộ baseline khi bắt đầu apply

- [x] 1.1 Xác nhận nhánh `feat/m4-financial-reports`, chạy `git fetch origin dev` rồi `git merge origin/dev` trước khi sửa implementation. Xác minh bằng `git merge-base --is-ancestor origin/dev HEAD` và ghi SHA của `origin/dev` và HEAD sau merge trong `docs/M4_FINANCIAL_REPORT.md`.

## 2. Kỳ, contract và quyền API

- [x] 2.1 Thêm action và kiểm tra query trong Analytics theo D1, DTO trong `shared/DTOs/FinancialReportDto.cs` theo D5. Thêm phương thức báo cáo vào `IAnalyticsService`/`IAnalyticsRepository` và cập nhật các test double của Bình theo D6. Kiểm tra HTTP với kỳ 1 và 31 ngày, tham số trùng/rỗng/thiếu, preset không hỗ trợ, ngày sai, ngày không thể đổi biên UTC, kỳ đảo ngược, 32 ngày và ngày cuối tương lai. Xác nhận 400 `ValidationProblemDetails` đánh dấu đúng trường và không tổng hợp số liệu.
- [x] 2.2 Thêm route và A/M vào đúng một dòng mới của `docs/API_PERMISSIONS.md` và baseline độc lập của `ApiAuthorizationCoverageTests`. Chạy test coverage và kiểm tra HTTP kết nối PostgreSQL với JWT thật của bốn role. Assert Admin/Manager 200 cùng phạm vi, SalesStaff/WarehouseStaff 403, thiếu hoặc sai JWT 401. Ghi mã ca và Expected/Actual vào file bằng chứng.

## 3. Số liệu bán, tiền thu và hiệu suất

- [x] 3.1 Thêm đọc bán theo `CompletedAt` và tiền thu theo `PaidAt` bằng hai tập riêng trong Analytics, gom theo ngày UTC+07 trong cùng snapshot. Kiểm tra PostgreSQL các mốc trước/đúng biên đầu, ngay trước/đúng biên cuối, ngày hiện tại bị chặn ở `asOf`, đơn thanh toán và giao khác ngày, đơn `Processing`, và cả hai trạng thái hoàn giữ nguyên số bán gốc.
- [x] 3.2 Map tiền hàng, thuế, giá vốn, lãi gộp và bộ đếm lệch phân bổ từ dữ liệu đã lưu. Đối chiếu PostgreSQL với bảng Expected D7 và fixture đúng 0 riêng. Assert tổng ngày bằng tổng kỳ, ngày trống có dòng 0, phân bổ 0 hợp lệ và 0 có chênh lệch, giá vốn thiếu lan `null`, và bảy trường change 2 có mặt với JSON `null`.
- [x] 3.3 Thêm hai bảng top 10 theo `ProductId` và hiệu suất theo `CashierUserId`. Kiểm tra PostgreSQL ca nhiều dòng/biến thể, thu ngân trùng tên, tie-break ID, thứ tự hai bảng, giới hạn và danh sách rỗng. Assert mỗi đơn và tiền thu chỉ tính một lần, tên sản phẩm lấy từ snapshot đã chốt, và giá vốn thiếu chỉ ảnh hưởng các nhóm liên quan.

## 4. Nghiệm thu và bàn giao

- [x] 4.1 Build mã đem nghiệm thu, chạy HTTP + PostgreSQL trên database test riêng và chạy lại test Analytics/Dashboard M3. So sánh output với DTO/JSON mẫu trong `design.md` và bảng Expected tính tay độc lập. Ghi mã ca, dữ liệu/kỳ, expected, actual, Pass/Fail/Skipped, SHA build, trạng thái worktree khi build và giới hạn dữ liệu trong duy nhất `docs/M4_FINANCIAL_REPORT.md`.
- [x] 4.2 Rà `git diff` để xác nhận implementation chỉ chạm Analytics, `shared/DTOs/FinancialReportDto.cs`, test báo cáo và test double Analytics của Bình, `ApiAuthorizationCoverageTests`, dòng API permissions mới và tài liệu bằng chứng. Kiểm tra không có script, log hoặc Markdown tạm. Nếu có commit, kiểm tra message không có trailer `Co-Authored-By` của AI.
