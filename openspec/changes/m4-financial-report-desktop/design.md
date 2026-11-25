# Design

## Context

- `DashboardPage` đã dùng C# Markup và là `IRoutePage`. Nó bind `DashboardViewModel`, có vùng cuộn riêng, ba preset và ô ngày tùy chọn ở live. `DashboardViewModel` giữ `_appliedQuery`, dùng `PageViewModel.RunLatestRequestAsync` cho Dashboard, và có luồng preview qua `IAnalyticsRepository`.
- `ShellViewModel` lấy menu và landing từ `RoleAccess`. `RoleAccess.CanOpen(AppRoute.Root(ShellDestination.Dashboard), role)` hiện chỉ mở Tổng quan cho Admin/Manager. `ShellRoutePageFactory` kiểm quyền khi tạo scope và lease. `ShellNavigationAdapter` thay và dispose trang. Luồng cửa sổ đổi Shell/Login khi generation phiên thay đổi.
- `AnalyticsApiRepository` đã dùng `IApiClient`, query invariant và map DTO sang model. [Change phần bán, mục D5](../archive/2026-11-25-m4-financial-report-sales-api/design.md#d5-dto-và-json-cho-t-vinh) cung cấp `FinancialReportDto` và mẫu JSON. [Change 2, mục D5/D6](../archive/2026-11-25-m4-financial-report-refunds-restocks-warranty/design.md#d5-tương-thích-json-tổng-hợp-và-giá-trị-chưa-biết) điền trường hậu mãi và thêm breakdown thuế hoàn, Cash/QR và ca. Mẫu phần bán giữ nguyên làm fixture tương thích.

## Goals / Non-Goals

**Goals** — Đọc số báo cáo đúng kỳ, đúng phiên và đúng nghĩa từng trường. Có luồng UI chạy được trên hai target desktop và có thể kiểm thử repository/ViewModel không cần UI thread.

**Non-Goals** — Thay menu, route, landing hoặc disposal của Shell. Sửa KPI, chart, truy vấn, preset hay preview của Dashboard M3. Tính nghiệp vụ tài chính, xuất file, drilldown giao dịch hoặc sửa dây chuyền API/DTO dùng chung.

## Decisions

### D1. Hai chế độ trong một DashboardPage

- Thêm bộ chọn nội bộ **Tổng quan** và **Báo cáo tài chính** phía trên content đang có. Tổng quan là mặc định ở mỗi activation. Giữ cây UI Tổng quan hiện có trong scope của trang khi đổi chế độ, để kỳ, trạng thái, preview, chart và request riêng tiếp tục theo hành vi M3. Chỉ mở và tải report ở thao tác chọn báo cáo lần đầu hoặc khi mở lại.
- Kiểm quyền cả visibility lẫn trước mở/load. Dùng `ISessionStore.Current` với trạng thái Active, generation thuộc activation, live mode và rule Dashboard sẵn có ở `RoleAccess`. Server vẫn quyết định quyền qua JWT. Preview, anonymous, expired, SalesStaff, WarehouseStaff hoặc role lạ không mở báo cáo và không gửi HTTP.
- Shell giữ route, nav item, tiêu đề, menu và landing hiện hành. Nếu sau này cần menu mới, cập nhật design trước apply với thay đổi tối thiểu cho destination/route, ánh xạ page và guard, kèm xác nhận của T. Vinh. Phương án hiện tại dùng selector nội bộ và nằm trong phạm vi Bình.

### D2. Repository và hợp đồng đọc

- Tạo cổng hẹp `IFinancialReportRepository` trong Analytics client. `AnalyticsApiRepository` mở rộng phần đọc báo cáo qua `IApiClient`; đăng ký cổng live trong `DashboardAnalyticsServiceCollectionExtensions` hiện có. Giữ wiring `IAnalyticsRepository` và fixture M3. Dashboard VM nhận dependency chuyên biệt trong scope hiện tại, và chỉ gọi khi qua guard ở D1.
- Gửi `GET api/analytics/financial-report?period=custom&from=yyyy-MM-dd&to=yyyy-MM-dd`, truyền cancellation tới transport. Dùng `FinancialReportDto` và JSON D5 phần bán làm chuẩn phần trường gốc, tái sử dụng các DTO dòng sản phẩm/thu ngân. Vì DTO shared gốc không có trường mới, dùng projection đọc có kiểu ở client Analytics tương thích layout cũ, bổ sung các trường nullable `refundTaxAmount`, `cashReceivedAmount`, `cashRefundPaidAmount`, `vietQrReceivedAmount`, `vietQrRefundPaidAmount` và `cashByShift` nếu có. Map sang model client. Trường mới bị thiếu hoặc `null` vẫn là chưa khả dụng, mảng ca thiếu khác mảng ca rỗng; không sửa shared DTO/backend và không deserialize trực tiếp kiểu response của backend.
- Kiểm tra hình dạng và metadata, gồm period chính xác, range trùng query, `asOf` UTC hợp lệ, `timeZone=UTC+07:00`, summary có đủ trường bắt buộc, daily đủ đúng ngày tuần tự, các mảng gốc hợp lệ. Trường core bắt buộc bị thiếu là lỗi response, không nhận giá trị mặc định của serializer làm số 0. Giữ `decimal`, nullable, số âm, thứ tự bảng top và hiệu suất. Không từ chối số thuần âm hoặc tính lại tổng để thay số server.

### D3. Kỳ, trạng thái và vòng đời request

- ViewModel báo cáo giữ riêng ngày nháp dạng `dd/MM/yyyy`, kỳ đã áp dụng bất biến và snapshot kèm ngày trả về. Mặc định lấy 7 ngày tới hôm nay theo UTC+07, dùng clock có thể cố định khi test. Validate ngày Việt Nam và giới hạn chung `AnalyticsLimits.MaxCustomRangeDays`. Áp dụng hợp lệ mới tạo request. Lỗi nháp gắn trường, giữ trạng thái/kỳ đang có. Làm mới và Thử lại đọc kỳ đã áp dụng, kể cả nháp đã bị sửa.
- Dùng state báo cáo riêng trong một ViewModel con headless thuộc Dashboard VM. Công việc async của báo cáo được Dashboard VM đăng ký qua `RunTrackedRequestAsync` để lease hiện tại chờ cả request bỏ qua cancellation trước khi dispose scope. Giữ CTS và số thứ tự riêng của báo cáo, không dùng slot latest của Dashboard cho cả hai. Nối token activation/window, cho lệnh Áp dụng hợp lệ chạy thay thế khi đang loading, hủy CTS cũ và giữ CTS đến khi request thực sự thoát. Chọn lại Tổng quan hủy work report và xóa snapshot, giữ kỳ đã áp dụng trong cùng activation/phiên; mở lại dùng kỳ đó và lấy snapshot mới.
- Khi bắt đầu request, chụp sequence, kỳ bất biến và generation. Trước commit success, lỗi hoặc finalization loading, kiểm tra đều còn khớp, mode còn mở và vẫn đủ quyền. Sự kiện phiên xóa report state, nháp, snapshot, lỗi và hủy report; trang cũ không tái gửi dưới phiên mới. Hủy/clear scope, unsubscribe lúc deactivation. Chuyển cập nhật state từ sự kiện background về context UI của activation theo cơ chế hiện có, giữ logic có thể test không có WinUI. Đọc generation hiện tại trước publish để từ chối phản hồi cũ ngay cả khi callback UI đang đợi.
- Loading mới không đặt label mới lên snapshot cũ. Chỉ commit snapshot và label cùng nhau khi kết quả đúng yêu cầu. Hủy dự kiến im lặng. Lỗi 400, timeout/network, 5xx và payload lỗi có thông báo tiếng Việt và retry khi còn quyền. 403 giữ phiên và báo không có quyền; 401 do transport quản lý cho đúng generation qua `BearerTokenHandler`, không thêm cơ chế refresh hoặc tự replay trong report.

### D4. Bố cục và ánh xạ số

Khu vực đầu gồm bộ chọn chế độ, ngày Từ/Đến, Áp dụng, Làm mới và dòng “Kỳ … · Cập nhật … (UTC+07)”. Giờ hiển thị đổi từ `asOf` UTC, không lấy đồng hồ máy thay giờ snapshot. Lỗi và retry thuộc panel report. Bố cục tổng kỳ có cụm kết quả bán, dòng tiền, giá vốn và hậu mãi. Bảng ngày hiển thị các chỉ số chính và có chi tiết theo ngày dùng được bằng bàn phím, để tránh kéo một bảng 20 cột trên cửa sổ hẹp.

| Khu vực | Nguồn hiển thị |
| --- | --- |
| Bán/hoàn và thuế | `salesExcludingTax`, `refundedSalesExcludingTax`, `netSalesExcludingTax`, `salesTax`, `refundTaxAmount`, `completedOrderCount`, `quantity`. Có nhãn cảnh báo phân bổ cũ nếu `allocationMismatchOrderCount` khác 0, không sửa số. |
| Dòng tiền | `paymentReceivedAmount`, `refundPaidAmount`, thu/chi Cash và VietQR khi có. `cashByShift` hiển thị mã ca, thu, chi và `cashNetMovement` của kỳ từ API, không gắn tên giả hay suy ra số dư két. |
| Giá vốn, lãi và hậu mãi | `costOfGoodsSold`, `restockedCost`, `netCostOfGoodsSold`, `salesGrossProfit`, `netGrossProfit`; hiển thị `warrantyCost` là chi phí máy thay ở mục riêng. |
| Theo ngày | Từng `daily[].date` và các trường cùng tên từ `daily[].totals`, gồm ngày 0 và giá trị chưa tính được. |
| Sản phẩm và hiệu suất | `topProductsByQuantity`, `topProductsByRevenue` và `cashierPerformance` theo thứ tự API. Hiển thị tên, số lượng/doanh thu và chỉ số sẵn có của người bán. Giữ nghĩa bán gốc; các bảng không trở thành giao dịch thu/chi hoặc xếp hạng ròng. |

- C# Markup declarative bindings và commands, `AppColors.Brushes` trực tiếp trên phần tử để đổi theme khi đang mở, `AppTypography/AppMetrics/AppControlStyles` cho hình thức chung. Dùng màu như tín hiệu bổ sung cho nhãn, số và focus. ViewModel không tham chiếu UI controls. Không thêm event đồng bộ dữ liệu của View; nếu cần gọi focus imperative cho chuyển panel, giới hạn ở focus/lifecycle cục bộ không gán dữ liệu, kiểm thử cả hai target.
- Tiền theo `vi-VN` kèm `₫`, giữ dấu âm và phần lẻ có nghĩa theo precision VND trong hợp đồng. `null` hiện “Chưa tính được”; 0 hiện “0 ₫”. Mảng rỗng có thông báo riêng. Vì API luôn có một dòng/ngày và không có cờ “không giao dịch”, chỉ khi toàn bộ chỉ số báo cáo cần xét đều biết và bằng 0, các danh sách đều có mặt và rỗng, dùng thông báo “Không có số liệu phát sinh khác 0 trong kỳ”. Giữ `asOf` và bảng ngày 0. Có trường thiếu thì vẫn hiển thị phần đã biết, không suy ra kỳ rỗng từ số 0 hoặc chưa khả dụng.
- Tab order theo bộ chọn, Từ, Đến, Áp dụng, Làm mới/Thử lại và nội dung. Có automation name, focus nhìn rõ, lỗi inline đọc được và focus thuận khi chuyển panel hoặc retry. Nội dung ẩn không ăn phím Tab. Ở chiều rộng hẹp, picker và nhóm tổng xếp dọc, chi tiết ngày reflow và bảng rộng có cuộn ngang cục bộ để mọi số còn truy cập được. Đổi theme/size không gửi request và không reset focus hay kỳ.

### D5. Bằng chứng tách theo lớp và nền tảng

- Headless dùng JSON D5 phần bán làm test data cố định. Đối chiếu kỳ 12–14/10/2024, `asOf=2024-10-15T02:00:00Z` hiển thị 09:00 UTC+07, tiền hàng 480, thuế 36, tiền thu 370, ngày 13 có số 0, vốn/lãi thiếu và thứ tự top đúng. Fixture mở rộng từ D6 change 2 kiểm số âm, thuế hoàn, chi phí bảo hành, Cash/QR, ca và khả năng đọc JSON cũ. Fixture chỉ ở test, không dùng cho runtime fallback.
- Nghiệm thu live trên build API tích hợp change 2 và PostgreSQL. Đọc cùng kỳ trực tiếp từ API và desktop, đối chiếu dữ liệu theo ngày/tổng và bảo toàn quyền. Ghi giới hạn luồng nguồn thực tế, gồm thu QR đầu cuối nếu vẫn chưa sẵn sàng. Phân biệt fixture đã xác nhận với kiểm chứng thu QR thật.
- Với từng target, ghi OS, target framework, SHA client/API, cách chạy, role, kỳ và kết quả cho tiếng Việt/VND, bàn phím/focus, resize 800×600/1024×768/1440×1000 DIP và đổi Sáng/Tối ngay khi report vẫn mở. Dùng cơ chế theme hiện có, hoặc đổi theme OS ở chế độ System/công cụ phát triển để đổi ngay trên cùng instance. Chuyển sang Settings rồi mở lại báo cáo không đủ bằng chứng. Lỗi native build ghi “Chưa kiểm tra” cùng lệnh, thông báo lỗi và điều kiện chạy lại. Tất cả ghi tiếp vào duy nhất `docs/M4_FINANCIAL_REPORT.md`.

## Risks / Trade-offs

- Report có nhiều chỉ số → tách cụm và chi tiết từng ngày để giữ khả năng đọc ở cửa sổ hẹp.
- Giao diện cùng trang có hai tải độc lập → mỗi chế độ có state request riêng, lease chung chờ cleanup và kiểm bằng fake bỏ qua cancellation.
- Client thấy API cũ hoặc nguồn thiếu → hợp đồng cộng thêm và nhãn chưa tính được, bằng chứng mô tả chính xác build đã kiểm.

## Migration Plan

Tích hợp client sau khi có contract phần bán và kiểm chứng live sau change 2. Không có migration dữ liệu. Có thể hoàn nguyên selector, panel và đăng ký report để quay về Tổng quan M3 khi cần.
