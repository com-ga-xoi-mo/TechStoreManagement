# Design

## Context

`DemoSeedRepository` đối chiếu theo khóa tự nhiên dưới advisory lock và `DataSeeder` lưu toàn bộ trong một transaction. Manifest hiện có 5 danh mục, 20 sản phẩm có giá vốn, 3 biến thể (`IP15PM-256-NT`, `S24U-256-GR`, `MBP14-M3-512-SG`), 10 khách, 20 đơn (12 Completed, 2 Processing, 3 Pending, 3 Cancelled) và 50 IMEI. Repository tạo bốn tài khoản demo. Các SKU, mã đơn, timestamps và chỉ số vị trí của manifest đang được dùng trong fixture. Thiếu sót dữ liệu đã xác định là chưa seed `ProductImage`. `backend/samples/pixel.png` là mẫu test nhỏ, phù hợp cho test upload nhưng không đủ để minh họa sản phẩm trong demo.

`AssertAnalyticsReferenceAsync` trong bộ kiểm thử seed PostgreSQL hiện gọi repository và tự zero-fill. Sau change Dashboard Manager, endpoint thật đã hỗ trợ `custom`. Nó đọc thời điểm database hiện tại để tính ba kỳ tương đối và `asOf`.

## Goals / Non-Goals

- Cho seed Development tạo bìa trên DB mới và bổ sung bìa thiếu theo khóa SKU trên DB cũ, đồng thời giữ ảnh, giá vốn và dữ liệu người dùng đã có.
- Chạy kiểm chứng cố định qua pipeline HTTP thực với dữ liệu PostgreSQL. Ghi lại lần chạy live khi trình diễn bằng khoảng lịch sử.
- Ngoài phạm vi có migration schema, chuyển ảnh giữa provider của DB có sẵn, thay đổi công thức tiền hoặc Analytics, và đối chiếu đơn POS đóng ca mới.

## Decisions

### D1. Chỉ bổ sung phần thiếu đã thấy trong seed

Dùng nguyên `AnchorUtc=2026-10-18T05:00:00Z` và toàn bộ dữ liệu đã có. Mọi mapping ảnh mới được nối thành dữ liệu mới theo SKU hoặc slug danh mục, giữ nguyên các dòng đã tồn tại, thứ tự và chỉ số trong manifest. Không lên kế hoạch thêm đơn hoặc đổi số tiền. Nếu có thêm dòng seed hợp lệ, cập nhật đồng thời các assert số cố định trong `DemoSeedManifestTests`, `DemoSeedRepositoryTests` và `DataSeederPostgresIntegrationTests`, cùng bảng Expected liên quan. Đếm đúng 50 IMEI trong kiểm thử hồi quy của fixture hiện tại và kiểm tính duy nhất, định dạng, liên kết; tiêu chí M3 không đặt mức tối thiểu IMEI. Trên dữ liệu cũ, kiểm tra giá vốn và metadata đã chỉnh được bảo toàn, kể cả khi chúng khác manifest.

### D2. Ảnh mẫu trong repo, URL do provider cung cấp

Nhóm tự tạo ảnh PNG placeholder bằng hình minh họa chung, mỗi nhóm danh mục có một mẫu và mỗi SKU nhận một ảnh bìa. Ảnh không chứa ảnh chụp sản phẩm thật, logo hay dấu hiệu nhận diện của hãng. Đưa file vào `backend/samples/demo-products/`, đóng gói dưới dạng embedded resources để seed dùng được khi chạy từ repo, chạy test hoặc publish từ thư mục khác. Dùng kiểm tra chữ ký nội dung và giới hạn ảnh hiện có của Catalog. Mỗi sản phẩm nhận object riêng để thao tác xóa một ảnh không ảnh hưởng sản phẩm khác.

`DataSeeder` điều phối trong transaction seeding hiện có. Sau khi `DemoSeedRepository.ReconcileAsync` đối chiếu thành công, `DataSeeder` gọi repository để tìm SKU demo cần ảnh. Repository xét cả sản phẩm vừa stage, lấy khóa product theo quy ước Catalog cho sản phẩm đã lưu và bỏ qua sản phẩm đã có gallery hoặc cover. Với mỗi SKU còn thiếu ảnh, `DataSeeder` gọi trực tiếp `IObjectStorage.PutAsync`, rồi chuyển URL trả về cho repository để stage `ProductImage` và cover. Repository dùng variant rỗng, sort order 0, timestamp theo mốc seed và quy tắc `ProductCoverImage` để chọn cover. Khi đã stage xong mọi ảnh và cover, `DataSeeder` gọi `IUnitOfWork.SaveChangesAsync` rồi `CommitAsync`.

ID ảnh seed ổn định theo SKU, object key mới theo dạng hiện có `products/{productId:N}/{guid:N}.png`. Local dùng `CreateNew`, nên khả năng chạy lại dựa trên tham chiếu DB và advisory lock, tránh dựa vào ghi đè key.

Trước commit, khi gặp xung đột, lỗi upload hoặc lỗi lưu, `DataSeeder` rollback và dọn best effort các key mới thuộc lần chạy đó qua `IObjectStorage`. Dọn với token độc lập khi yêu cầu bị hủy. Khi kết quả commit mơ hồ, bảo toàn object cho đến khi kiểm tra được nó không có tham chiếu DB, để tránh làm hỏng ảnh đã commit. Hướng dẫn khắc phục khi DB còn URL nhưng file bị xoá ngoài ứng dụng thay vì tự thay gallery đang tồn tại. Dùng cùng provider và storage qua các lần rerun.

### D3. Mở rộng fixture PostgreSQL hiện có

Giữ các test case fresh/rerun, Sprint 1 baseline, partial/conflict của `DataSeederPostgresIntegrationTests` và fixture schema cô lập hiện có. Bổ sung dữ liệu sentinel nhỏ để đo trước/sau khi chạy lại: ID, timestamp, credential hash, các trường người dùng đã chỉnh và gallery/cover cũ. Khi kiểm tra mật khẩu/role của bốn tài khoản seed, phân biệt fresh seed với DB đã chỉnh. Giữ quy tắc ID cố định của admin và khóa SKU/slug tự nhiên cho baseline cũ.

Đối chiếu đơn, item, ca và actor, các tổng tiền ca theo khoản đã ghi nhận, chứng từ VietQR, trạng thái serial và bảo hành, số lượng tồn/giữ và các dòng nhận hàng/xuất bán theo khóa nguồn. Mở rộng ca conflict hiện có để kiểm tra rollback bằng context mới và so sánh snapshots trước/sau. Thêm ca hỏng storage hoặc lỗi lưu sau upload để chứng minh không có tham chiếu dở dang và chỉ dọn object do lần chạy tạo ra. Tái dùng `DemoSeed*Tests` và `DataSeederTests` cho logic chạy lại, khóa và thứ tự save/rollback.

### D4. Đối soát HTTP ở hai thời điểm rõ ràng

Lập bảng tay từ các dòng đơn và item trong fixture với mã đơn, timestamp UTC, ngày Việt Nam, trạng thái và tiền. Tính độc lập trước khi đọc API hoặc số assert hiện có. Tính doanh thu theo `CompletedAt` của đơn Completed và đếm đơn theo `CreatedAt` ở mọi trạng thái. Liệt kê các ngày zero và tối đa 5 mã đơn theo `CreatedAt DESC, id DESC`, kèm các dòng tồn thấp theo thứ tự contract. Sản phẩm hoạt động và tồn thấp được tính từ trạng thái DB seed lúc đọc. Mốc `2026-10-18T05:00:00Z` tương ứng 12:00 ngày 18/10 UTC+07.

| Request tại mốc cố định | Ngày Việt Nam | Doanh thu tham chiếu hiện tại | Số đơn tham chiếu hiện tại |
| --- | --- | ---: | ---: |
| `today` | 18/10/2026 | 27.980.000 ₫ | 2 |
| `last7Days` | 12–18/10/2026 | 136.080.000 ₫ | 20 |
| `thisMonth` | 01–18/10/2026 | 136.080.000 ₫ | 20 |
| `custom&from=2026-10-12&to=2026-10-18` | 12–18/10/2026 | 136.080.000 ₫ | 20 |

Ví dụ kỳ 12–18/10 cộng theo từng ngày được 14.480.000, 35.980.000, 0, 14.980.000, 22.980.000, 19.680.000, 27.980.000 ₫. Các số trong thiết kế là mốc tham chiếu. Actual chỉ ghi sau khi chạy và xử lý chênh lệch trên DB cô lập.

Thêm `Microsoft.AspNetCore.Mvc.Testing` phiên bản `10.0.12` vào `tests/Backend.IntegrationTests/TechStore.Backend.IntegrationTests.csproj`, đồng bộ với Backend.UnitTests. Mở rộng ca reconciliation hiện có để dùng `WebApplicationFactory<Program>` trên schema và thư mục Local riêng của fixture. Dùng login thật và PostgreSQL thật. DI của test host bọc repository Analytics để chỉ cố định kết quả `GetSnapshotTimeUtcAsync` theo `AnchorUtc`. Tất cả truy vấn, controller, mapping, xác thực và serializer dùng đường production. Không thêm tham số API để đổi clock. Kiểm tra `AdminDashboardDto` trên HTTP, cả bốn kỳ, mỗi ngày zero, tổng tiền và top orders. Cách này dùng contract bảo vệ KPI thay vì lặp lại code của repository trong Expected. Đảm bảo `asOf` của chế độ cố định là mốc test, không mô tả là snapshot lịch sử của một DB đã bị sửa.

Khi trình diễn, chạy API chuẩn với clock database, tắt Dashboard preview, đăng nhập `manager1` và chọn 12–18/10/2026. Custom có thể đọc hết ngày 18/10 nếu ngày demo muộn hơn, trong khi test cố định cắt lúc 12:00 hôm đó. Tính Expected theo đúng biên của từng lần chạy và ghi riêng kết quả. Ba kỳ tương đối luôn đi theo thời điểm demo, có thể rỗng ngoài tháng seed.

### D5. Phân ranh công thức tiền

`EnsureOrdersAsync` lưu `TotalAmount` từ giá sản phẩm/variant và gán thuế, giảm giá bằng 0, không gọi công thức checkout. Đối chiếu fixture kiểm số tiền đã lưu, các quan hệ thanh toán và phép tổng hợp. Công thức chính thức và phân bổ thuộc Vy. Cách tính thuế/giảm giá trên checkout và đơn POS thật được kiểm ở `m3-dashboard-live-flow`.

## Risks / Trade-offs

- [Object storage ngoài giao dịch DB] → Chỉ dọn key mới khi rollback đã xác nhận, bảo toàn tham chiếu và ghi trạng thái lỗi dọn để xử lý trong storage demo.
- [Sai kỳ nếu dùng clock máy hoặc ngày chạy] → Giữ anchor riêng cho host test, chụp `asOf` thật cho lần demo live, ghi biên UTC+07 trong báo cáo.
- [Đổi provider làm URL cũ không mở được] → Mỗi lần trình diễn dùng DB và storage demo phù hợp nhau. Với R2 dùng bucket demo và public base URL riêng, không đưa secrets vào log hoặc hướng dẫn có giá trị thật.

## Migration Plan

Dùng các migration hiện có. Bàn giao hướng dẫn Local bằng `ObjectStorage__Provider=Local`, `ObjectStorage__Local__RootPath` trỏ tới thư mục demo có quyền ghi, `ObjectStorage__Local__RequestPath=/uploads`, và `dotnet run --project backend/TechStore.Api.csproj --launch-profile http` với connection string của DB demo. Xác nhận log seed, đọc cover từ API rồi mở URL hoặc mở Catalog trong client.

Để chạy R2, dùng DB/bucket demo mới và cấu hình `ObjectStorage__Provider=R2`, `ObjectStorage__R2__AccountId`, `ObjectStorage__R2__AccessKeyId`, `ObjectStorage__R2__SecretAccessKey`, `ObjectStorage__R2__BucketName`, `ObjectStorage__R2__PublicBaseUrl` bằng biến môi trường, user-secrets hoặc `backend/.env` nằm ngoài git. Theo dõi log seed, lấy URL tuyệt đối từ product detail và kiểm tra file public. Việc mở thử R2 được ghi là chưa kiểm nếu thiếu storage test.
