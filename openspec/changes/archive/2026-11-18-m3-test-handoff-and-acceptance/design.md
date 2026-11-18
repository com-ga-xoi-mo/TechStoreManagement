# Design

## Context

`feat/m3-manager-dashboard-access` đã ghép `origin/dev` và chứa ba change đã archive: `m3-manager-dashboard-access`, `m3-demo-data-reconciliation`, `m3-dashboard-live-flow`. Ref `origin/dev` hiện có trong workspace chưa có API receiving của Q. Vinh. Kiểm tra lại sau fetch trong lượt apply và ghi build thực chạy trong báo cáo.

[Kế hoạch M3](../../../../docs/MILESTONE_3_PLAN.md) hiện ghi Dashboard Manager ở dòng 84 trong mục Bình, regression Admin và commit/rollback ở 154–155, dữ liệu demo ở 165–166 và tổng hợp ở 171. Dùng đề mục, tên Bình và nội dung checkbox để nhận diện nếu số dòng thay đổi sau merge.

## Goals / Non-Goals

**Goals**

- Tách kết quả Bình chạy ở Sprint 5 khỏi kết quả trích từ các build trước. Mỗi kết luận có bằng chứng và phạm vi rõ để An tiếp tục bàn giao.
- Chạy độc lập phần regression và xác minh hậu giao dịch trong khi các owner đang hoàn thiện receiving hoặc tích hợp desktop.

**Non-Goals**

- Thay đổi hành vi ứng dụng, spec, cấu hình production hoặc test/helper của owner khác. Giữ nguyên câu chữ và checkbox của các owner khác trong plan, bao gồm điều kiện hoàn thành M3.
- Sửa nghiệp vụ hoặc hạ kỳ vọng của test khi phát hiện lỗi. Bình ghi ca tái hiện, kết quả và owner chịu trách nhiệm.

## Decisions

### D1. Baseline và build

Lúc apply, kiểm tra nhánh và các thay đổi cục bộ, fetch và merge `origin/dev` mới nhất rồi build lại. Xác minh ba archive còn trên nhánh và kiểm tra receiving đã vào build hay chưa. Với các lượt Bình chạy, ghi ngày, SHA nguồn đã merge, .NET SDK, OS, cấu hình/target build, lệnh, đường dẫn output và SHA-256 artifact chạy nếu có test thêm chưa commit. Kết quả của owner giữ nguyên SHA và môi trường ở nguồn. Nếu nguồn thiếu SHA hoặc log gốc, ghi rõ trường còn thiếu và chỉ dùng bằng chứng trong phạm vi xác nhận được.

Bình chỉ push lên nhánh của mình; việc merge vào `dev` do người phụ trách thực hiện qua PR. Vì vậy build bàn giao là SHA của `feat/m3-manager-dashboard-access` **đã push lên `origin`**, không dùng SHA chỉ có ở local. Báo cáo ghi rõ SHA này đã chứa `origin/dev` tại SHA nào và chưa được merge vào `dev`. Mỗi lần commit thêm (kể cả sửa báo cáo hoặc test), push lại và cập nhật SHA trong báo cáo trước khi bàn giao. Sau khi PR được merge, nếu `dev` có thêm thay đổi so với SHA đã ghi, chạy lại các suite chính trên `dev` và ghi thành lượt riêng, không ghi đè kết quả cũ.

Chạy các project backend unit, backend integration và frontend headless hiện có để lấy kết quả trên build bàn giao. Bật PostgreSQL opt-in cho các ca dự định chạy qua `TECHSTORE_TEST_CONNECTION`, `TECHSTORE_SEED_TEST_POSTGRES` và `TECHSTORE_ANALYTICS_TEST_POSTGRES` cùng các cờ fixture yêu cầu. Trỏ `TECHSTORE_API_BASE_URL` của `Frontend.IntegrationTests` về Kestrel dùng DB test riêng khi chạy Catalog live. Ghi từng lần chạy riêng nếu phải dùng filter hoặc môi trường khác. UI native là mục bàn giao của T. Vinh, build thành công cũng chưa xác nhận thao tác UI. Lưu log/TRX đã lọc thông tin xác thực vào vùng build/test, và trích kết quả cần bàn giao trong tài liệu của Bình. Vùng này bị git ignore nên log chỉ nằm trên máy chạy; yêu cầu tự chứa của báo cáo ghi ở D5.

### D2. Dùng lại bằng chứng và chọn đúng contract cho regression

Gắn trực tiếp nguồn vào các checkbox đã có bằng chứng. Dashboard Manager dùng [bàn giao Dashboard Manager](../../../../docs/M3_TEST_SUMMARY.md#dashboard-manager) và [đối soát bốn kỳ API](../../../../docs/M3_TEST_SUMMARY.md#actual-qua-http-ở-mốc-cố-định). Dữ liệu demo và chạy lại seed dùng [bảng fixture và kiểm thử PostgreSQL](../../../../docs/M3_TEST_SUMMARY.md#dữ-liệu-demo-và-đối-soát-kpi), gồm ảnh Local qua HTTP, số lượng, giá vốn, bốn tài khoản, bảo toàn mật khẩu/dữ liệu và ngày fixture. Ghi SHA và giới hạn theo từng nguồn.

Với dòng regression Admin, lập bảng đối chiếu từng ca bắt buộc với test và assertion đã có, sau đó chạy lại test phù hợp trên build bàn giao. Các nguồn sau xác định phần có thể dùng lại và phần cần xác nhận thêm:

| Phạm vi | Test, contract và phần cần kiểm chứng |
| --- | --- |
| Sản phẩm | `CatalogManagementApiIntegrationTests` phủ vòng đời tạo, cập nhật, đổi trạng thái và xóa; `CatalogApiIntegrationTests` và `ProductSearchPostgresTests` phủ đọc, tìm, lọc và paging. Dùng hợp đồng `product-catalog` để đối chiếu tên rỗng/quá 200, sort/spec sai, giá âm 400, giá 0 hợp lệ với payload Catalog đủ trường và đọc lại đúng 0. Kiểm tra lỗi ghi không tạo hoặc sửa entity. |
| Danh mục | Dùng `CategoryPostgresTests` cho cập nhật, xóa và bảo toàn cấu trúc. Kiểm chứng create, đọc cây, cập nhật và xóa danh mục rỗng trên dữ liệu riêng nếu bằng chứng hiện có chưa đủ. Kiểm tra tên trống/quá 200 và danh mục giữ nguyên sau request lỗi. API `GET /api/categories/tree` trả cây đầy đủ. Dùng `CategoryWorkspaceTests.Search_DeepMatch_OpensAncestorsAndRestoresExpansion` cho tìm trong cây phía client và ghi đó là bằng chứng headless. |
| Khách hàng | Dùng `CustomerServiceTests`, `CustomerRepositoryPostgresTests`, `CustomerHistoryPostgresHttpTests` và bằng chứng của Vy làm điểm xuất phát. Đối chiếu tạo, đọc, cập nhật, deactivate/activate và tìm theo tên/điện thoại, lọc tier/trạng thái, paging bằng Admin. Dùng deactivate là thao tác bỏ sử dụng theo contract. Kiểm tra tên trống/quá giới hạn của request và phone hoặc tier không hợp lệ. Ưu tiên bù các ca quản lý/validation chưa có chứng cứ HTTP qua PostgreSQL với admin thật. |

Bổ sung một lớp test riêng của Bình, dự kiến `M3AcceptancePostgresHttpTests.cs` trong `tests/Backend.UnitTests/`, khi thiếu bằng chứng cho ca bắt buộc. Gọi lại `PostgresFact` và `IdentityPostgresApi`. Ghi qua API thật, dùng dữ liệu nhận diện riêng và DB fixture có thể dọn sau lượt chạy. Xác nhận read-back bằng GET và dùng context mới nếu cần kiểm tra trạng thái không có trên DTO. Ghi rõ từng ca là test tự động hay thao tác tay, gồm TestServer/PostgreSQL, HTTP tới Kestrel hoặc UI. Tick dòng regression khi các ca bắt buộc có bằng chứng phù hợp.

### D3. Hậu giao dịch thành công, 409 và 400

Xuất phát từ [bằng chứng m3-dashboard-live-flow](../../../../docs/M3_TEST_SUMMARY.md#kiểm-chứng-http-trên-postgresql) và chạy lại `DashboardLiveFlowPostgresHttpTests` trên build bàn giao. Dùng ca 201 để kiểm tồn, lượng giữ, serial Sold, dòng Sale, bảo hành, tiền ca và Dashboard. Dùng ca IMEI Sold với khóa mới, ca Open và tiền đủ để kiểm 409 và snapshot không đổi.

Ca tiền nhận thiếu hiện có trong `PosCheckoutServiceTests` dùng fake. Bù chứng cứ HTTP/PostgreSQL trong lớp riêng của Bình ở D2 nếu build chưa có ca 400 tương đương. Với một IMEI InStock, ca Open và payload hợp lệ ở các trường còn lại, gửi tiền nhận thấp hơn tổng, kiểm tra 400 theo trường `cashTendered`, khóa checkout chưa lưu và tồn/giữ chỗ, serial/bảo hành, ledger, đơn/dòng và tiền/trạng thái ca không đổi. Đọc sau request bằng context mới. Ghi 400 là từ chối đầu vào. Ghi 409 Sold là ca tuần tự. Tick dòng commit/rollback khi có bằng chứng cho cả thành công và lỗi theo phạm vi này.

### D4. Nguồn bàn giao theo nhóm

| Nhóm | Owner / nguồn |
| --- | --- |
| Backend unit, PostgreSQL/HTTP, frontend headless | Bình ghi lượt chạy mới theo project/lệnh và môi trường. Trích kết quả lịch sử của từng owner theo build ghi trong tài liệu. |
| RBAC và Identity | An. [Identity acceptance](../../../../docs/IDENTITY_ACCESS_MANAGEMENT_ACCEPTANCE.md) và [Identity integration handoff](../../../../docs/M3_IDENTITY_INTEGRATION_HANDOFF.md). Ghi rõ bằng chứng token, role, actor và ma trận quyền đến từ nguồn nào. |
| POS Cash, ca, công thức và khách | Đạt/Vy. Các archive [m3-pos-cash-shift](../2026-11-17-m3-pos-cash-shift/tasks.md), [m3-order-pricing](../2026-11-17-m3-order-pricing/tasks.md), [m3-sales-customers](../2026-11-18-m3-sales-customers/tasks.md). Phân biệt API khách/đơn nháp với chọn khách trên POS desktop, và pricing có VAT/giảm dòng với giảm cấp đơn qua POS. |
| UI, HTTP methods và khôi phục | T. Vinh. [M3 UI/API verification](../../../../docs/M3_UI_API_VERIFICATION.md). Giữ nền tảng, phạm vi, cách xác nhận UI và mã HTTP đã quan sát. Windows native chờ lượt nghiệm thu của T. Vinh. |
| Dashboard, seed, live flow | Bình. Ba archive trên baseline và các bằng chứng D2–D3, gồm UTC+07, dữ liệu demo, ca HTTP và desktop Skia. |
| Concurrency và receiving | Q. Vinh, Đạt. Đối chiếu bài kiểm thử tranh IMEI/phụ kiện, nhận trùng, chuyển trạng thái cùng đơn và bán trong lúc đóng ca theo phân công plan. Test POS hiện có dùng gate phát hai HTTP request. Chỉ ghi Pass concurrency khi chính bằng chứng của lượt chạy xác nhận hai giao dịch PostgreSQL độc lập thực sự chồng thời gian, ví dụ đồng bộ tại bước giao dịch/khóa hoặc log lock wait. Một ca 409 tuần tự hoặc gate phía client đứng riêng chỉ ghi đúng hành vi đã chứng minh. |

### D5. Mẫu báo cáo và cách đếm

`docs/M3_TEST_SUMMARY.md` có danh mục ca với mã ổn định, nhóm, owner, tự động/thao tác tay, đầu vào, Expected, Actual, SHA, lệnh hoặc liên kết bằng chứng, trạng thái và mục lỗi/chạy lại. Actual của ca chưa chạy ghi bước còn thiếu, owner và điều kiện chạy lại. Bảng lỗi giữ mã ca, lỗi phát hiện, owner, bằng chứng hoặc SHA sửa nếu có và kết quả rerun hoặc trạng thái đang chờ. Không gộp một lỗi cũ đã sửa và lượt rerun thành hai ca độc lập trong tổng cuối.

Báo cáo phải tự chứa: người đọc không có máy của Bình vẫn kiểm được kết quả. Với mỗi lượt chạy, ghi trực tiếp trong `docs/M3_TEST_SUMMARY.md` lệnh đầy đủ, dòng tổng của runner (Passed/Failed/Skipped/Total), tên các ca fail hoặc skip, và trích đoạn log hoặc read-back đã lọc JWT, mật khẩu, chuỗi kết nối cho mỗi ca nghiệm thu. Đường dẫn log local chỉ là tham chiếu phụ, không được là bằng chứng duy nhất. Bằng chứng của owner khác dẫn bằng link tới tài liệu hoặc archive trong repo.

Ghi hai loại mẫu số. Cho từng lần chạy suite, tổng là số case runner báo trong lần đó, gồm Pass, Fail, Skipped. Cho ma trận nghiệm thu, tổng là số mã ca duy nhất thuộc phạm vi ma trận, gồm đúng bốn trạng thái Pass/Fail/Skipped/Chưa kiểm tra. Mỗi mã có một kết quả dùng cho bàn giao. Lịch sử và các lượt lọc chồng nhau ghi riêng, kể cả bài PostgreSQL/HTTP nằm trong project backend unit. Không cộng số suite với số ca ma trận, không cộng suite chạy trên các SHA khác nhau. Ca được runner báo bỏ qua là Skipped. Mục thiếu build, môi trường, xác nhận đồng thời hoặc bằng chứng thực chạy ghi Chưa kiểm tra.

Danh sách cho An nêu các mục chờ và bước tiếp theo. Q. Vinh bàn giao receiving đã merge cùng migration để chạy luồng nhập → bán đúng IMEI vừa nhận → Dashboard, rồi chạy lại ca 409 trên build đó. Đạt nối giảm cấp đơn theo công thức Vy và bộ chọn khách vào POS desktop để đối chiếu tiền, ca, Dashboard và liên kết khách. T. Vinh chạy UI trên Windows native với build, hệ điều hành, thao tác và ảnh/log. Nếu đến hạn nộp vẫn thiếu bằng chứng owner, giữ các dòng đó ở **Chưa kiểm tra**, ghi owner và tiêu chí chạy lại, chốt báo cáo, tick checkbox tổng hợp của Bình và archive theo tiền lệ live flow. Các checkbox nghiệm thu của owner và điều kiện hoàn thành chung vẫn do owner xác nhận.

## Risks / Trade-offs

- [Các build bàn giao khác nhau] → Giữ SHA nguồn cho từng kết quả, ghi riêng lần chạy lại trên bản đã ghép và kiểm tra bài cũ có thực sự phủ ca cần tổng hợp.
- [Dữ liệu test hoặc môi trường làm nhiễu] → Tạo DB/schema test riêng, dùng fixture opt-in và ghi chính xác những ca bị skip hoặc thiếu điều kiện chạy.
- [Bàn giao tới hạn trước một phần tích hợp] → Dùng mốc chốt ở D5, giữ đường dẫn bằng chứng và điều kiện chạy bổ sung trong cùng báo cáo.

## Migration Plan

Áp migration của build đã ghép khi khởi tạo database test riêng. Không thêm migration. Dọn tài nguyên tạm của lượt test theo fixture hiện có.
