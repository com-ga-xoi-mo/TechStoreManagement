# Design

## Context

Xem [proposal.md](proposal.md). Hiện `AnalyticsService` mở transaction Repeatable Read, đọc timestamp database và truyền `AsOfUtc` vào repository làm biên cuối cho các truy vấn theo kỳ. `AdminDashboardDto` đã có `Period`, hai ngày báo cáo và `AsOf`. Client đã có đường `DashboardPage → DashboardViewModel → AnalyticsApiRepository → IApiClient` và cơ chế hủy request theo activation và phiên.

## Goals / Non-Goals

**Goals**

- Dùng một policy truy cập toàn cửa hàng và một contract Analytics cho Admin và Manager.
- Hỗ trợ khoảng ngày lịch sử trên database thật qua cùng pipeline và giữ được hành vi của ba kỳ tương đối.

**Non-Goals**

- Công thức KPI mới, ảnh chụp tồn kho lịch sử, dữ liệu seed mới và quyền Customers.
- Refactor màu biểu đồ cũ. `StitchTokens.Primary` hiện là fallback trong `DashboardPage.CreateRevenueBarBrush` và nằm ngoài phạm vi lần này. Các control ngày mới dùng token `AppColors`. Đối soát bộ số liệu tính tay thuộc `m3-demo-data-reconciliation`.

## Decisions

### D1. Quyền server và điều hướng

Cấp cả hai role tại `AnalyticsController` và giữ kiểm tra JWT của pipeline hiện tại. Khi cập nhật `docs/API_PERMISSIONS.md`, sửa cả dòng Analytics và các đoạn còn mô tả Manager nhận 403. `ApiAuthorizationCoverageTests` tiếp tục giữ expected role độc lập với metadata controller. Sửa `AnalyticsControllerTests` để xác nhận đủ bốn role, thêm SalesStaff/WarehouseStaff vào test auth handler hiện chỉ dựng được Admin và Manager.

Tách hai nhánh Dashboard và Customers trong `RoleAccess.CanOpen`. `Destinations`, guard và `Landing` tiếp tục dùng cùng bảng. Thứ tự Tổng quan → Đơn hàng → Danh mục và Kho → Sản phẩm với Cài đặt làm fallback khiến Manager tự vào Tổng quan mà không cần điều hướng đặc biệt. Quyền Customers vẫn chỉ Admin. Sửa copy hướng dẫn đăng nhập trong Dashboard để phù hợp với cả Admin và Manager. Một endpoint chung giữ được cùng snapshot và cùng bộ kiểm thử cho hai role. Endpoint Manager riêng sẽ nhân đôi contract đọc đang giống nhau.

### D2. Kỳ tùy chọn có giới hạn trên endpoint hiện có

Request ví dụ: `GET /api/analytics/dashboard?period=custom&from=2026-10-12&to=2026-10-18`. Hai đầu tính theo UTC+07 và đều được tính vào kỳ. Giới hạn 31 ngày tương đương độ dài tối đa của kỳ tháng hiện có, phù hợp với số điểm biểu đồ hiện tại.

Controller kiểm tra tổ hợp tham số, phân biệt thiếu và rỗng hoặc lặp, parse chính xác `yyyy-MM-dd` bằng `DateOnly` và invariant culture, rồi truyền tiêu chí typed vào service. Controller trả `ValidationProblemDetails` cho input sai. Service kiểm tra ngày cuối so với ngày Việt Nam tại timestamp của snapshot, kiểm tra các biên có thể biểu diễn an toàn và trả kết quả validation có thể map về HTTP 400, trước khi chạy aggregate. Giữ rollback khi đường đọc hoặc validation trong transaction kết thúc bất thường. Ví dụ, kỳ 32 ngày hoặc ngày cuối ở tương lai phải bị từ chối. Với `today`, `last7Days`, `thisMonth` và mặc định, request hợp lệ chỉ truyền kỳ như cũ.

Đã cân nhắc preset dành riêng cho dữ liệu demo và seed dịch ngày theo ngày chạy. Khoảng ngày dùng được với giao dịch thật bất kỳ và giữ tính tái lập của mốc seed. `from`/`to` được gắn rõ với `period=custom` để kỳ mặc định và kỳ tùy chọn có thể được nhận diện trong JSON, trên UI và trong bằng chứng HTTP.

### D3. Tách biên lọc khỏi thời điểm snapshot

Từ timestamp database hiện tại, service tính ngày Việt Nam, ngày đầu/cuối báo cáo, `FromUtc` và `ToExclusiveUtc`. `ToExclusiveUtc` là min giữa nửa đêm Việt Nam sau ngày cuối và `asOf`. Repository nhận khoảng UTC nửa mở và dùng cùng predicate cho tổng doanh thu, trend, số đơn và 5 đơn gần nhất theo trường thời gian của từng KPI. Giữ truy vấn có tham số và phép nhóm theo ngày UTC+07. Sản phẩm hoạt động và tồn thấp đọc tại snapshot trong cùng transaction, độc lập với biên kỳ.

Giữ JSON shape và tên `AdminDashboardDto`. Mở rộng tài liệu trường `Period` để nhận `custom` và mô tả riêng `StartDate`/`EndDate` và `AsOf`. Ngày không phát sinh vẫn zero-fill đủ khoảng, danh sách và tiêu chí KPI giữ contract hiện có. Không thêm trường mốc thời gian, vì hai ngày và `AsOf` đủ biểu diễn hành vi. Client kiểm tra hai ngày custom trả về đúng query trước khi map snapshot.

### D4. Nhập ngày tách biệt với request đang áp dụng

Giữ ba nút chọn kỳ. Trong live mode thêm nút Khoảng ngày để mở cụm nhập Từ ngày và Đến ngày, định dạng hiển thị `dd/MM/yyyy`, và lệnh Áp dụng. Khi lần đầu mở cụm nhập, có thể điền từ ngày của snapshot đang hiển thị. Bố trí dưới các nút kỳ ở chiều rộng hẹp. Dùng label, thông báo lỗi theo field, binding và command, và giữ tab order rõ ràng.

VM giữ riêng ngày đang nhập và `DashboardQuery` bất biến đã gửi, với `DashboardPeriod.Custom` và hai `DateOnly` ở client. Chỉ Áp dụng khoảng hợp lệ mới kích hoạt request. Refresh và Retry đọc query đã gửi. Preset tạo query chỉ có period. Mỗi lần kích hoạt trang mới mặc định 7 ngày qua. Tiếp tục dùng cơ chế hủy request và kiểm tra session generation hiện có. Client kiểm tra ngày hiện tại theo UTC+07 để phản hồi sớm. Service xác thực theo timestamp database, nên HTTP 400 vẫn được hiển thị an toàn nếu đồng hồ lệch.

Client repository format ngày bằng invariant culture và dùng cùng API/DTO. Trong preview, ẩn cụm Khoảng ngày và giữ fixture, banner và ba kỳ cũ. Hiển thị khoảng ngày live đã áp dụng và giờ cập nhật. Với khoảng qua tháng, dùng nhãn ngày có tháng và giảm mật độ nhãn trục khi cần, cùng bảng giá trị ngày đầy đủ như hiện tại. Cho thấy rõ các ô sản phẩm/tồn thấp là số tại thời điểm đọc. Danh sách đơn là 5 đơn tạo cuối cùng trong kỳ (D3), không phải đơn gần nhất tính tới hôm nay, nên tiêu đề danh sách nêu rõ phạm vi kỳ, ví dụ "Đơn mới nhất trong kỳ", cho cả preset và khoảng tùy chọn. Form áp dụng thủ công giúp người dùng hoàn chỉnh cả hai mốc trước khi gửi request, đồng thời tránh tải lại khi đang gõ.

## Risks / Trade-offs

- [Lệch ngày ở biên UTC hoặc nửa đêm Việt Nam] → Test hai biên, ngày kết thúc là hôm nay, ngày lịch sử và tháng chuyển tiếp với timestamp cố định. Dùng cùng một timestamp database cho giới hạn và metadata.
- [Người dùng hiểu tồn thấp là số liệu lịch sử] → Ghi chú snapshot hiện tại cạnh các chỉ số này ngay khi xem khoảng ngày.
- [Request từ account cũ hoặc kỳ cũ về muộn] → Giữ request/generation guard và test custom refresh, retry, logout và đổi phiên.
- [API và client khác phiên bản] → Phát hành API trước desktop. Desktop hiện tại vẫn dùng được ba preset khi backend mới đã triển khai. Nghiệm thu Manager sau khi cả hai phía cùng cập nhật.

## Migration Plan

Không cần EF migration hay sửa seed. Khi apply, chạy backend và desktop cùng bản tích hợp trên PostgreSQL test riêng đã seed. Dùng `manager1` chọn 12–18/10/2026 và quan sát dữ liệu thật. Ghi `asOf` thực tế cùng điều kiện môi trường. Nếu cần rollback release, rollback desktop trước API để client ngừng gửi `period=custom` và menu không còn mở Dashboard cho Manager khi server đã rút quyền.
