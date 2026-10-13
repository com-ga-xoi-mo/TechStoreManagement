# Kế hoạch chuyển thiết kế UI thành Uno XAML theo MVVM + Repository

Ngày lập: **08/10/2026**. Trạng thái: **kế hoạch, chưa triển khai code**.

Kiến trúc áp dụng: [MVVM_REPOSITORY_BASE_PROJECT.md](MVVM_REPOSITORY_BASE_PROJECT.md), đặc biệt mục 6. Nguồn thiết kế: [UI/](../UI/stitch_techstore_retail_admin_dashboard/), gồm **31 bộ `screen.png` + `code.html`**. Mã ảnh D01–D31 ở cuối tài liệu dùng để truy ngược từng thiết kế.

## 1. Phạm vi và cách chia việc

- Mỗi lượt triển khai chỉ xử lý **một mã công việc** dưới đây. Màn lớn được tách tiếp thành các bước nhỏ, không triển khai toàn bộ ứng dụng trong một lượt.
- Nhiều ảnh mô tả cùng một màn ở các trạng thái khác nhau. Chúng dùng chung View/ViewModel; không tạo 31 Page độc lập.
- Chuyển thiết kế thành **control Uno/WinUI và XAML native**. HTML là tài liệu tham khảo bố cục, nội dung, màu sắc; không nhúng nguyên HTML bằng WebView.
- `UI/` ở gốc repository tiếp tục là thư mục thiết kế. Code giao diện đặt trong `frontend/TechStore.Client/Views/` theo kiến trúc đã thống nhất.
- Những nhãn như “đã xác thực”, số tồn kho, số doanh thu trong ảnh là dữ liệu minh họa, không chứng minh backend đã hỗ trợ nghiệp vụ đó.
- Frontend hiện có `MainPage` khung cơ bản. Việc chuyển sang `Views/Shell/ShellPage` là một công việc tương lai ở UI-01. Tài liệu này không di chuyển hay sửa code hiện tại.

**Cách thực hiện một mã công việc:** dựng View với mock/fake repository qua DI → hoàn thiện Binding/Command và các trạng thái → kiểm tra giao diện, hành vi → bàn giao UI/mock. Tích hợp API thật là change riêng sau khi có hợp đồng backend; không giữ task HTTP/JWT/endpoint trong change dựng màn. Chỉ sau khi kiểm tra xong phạm vi của mã đó mới chuyển sang mã khác.

Phân biệt hai mức bàn giao: **UI hoàn thành với mock/fake repository** và **API thật chưa tích hợp**. Mock là nguồn dữ liệu được chọn rõ ràng qua DI để kiểm tra UI, không phải cơ chế fallback khi HTTP lỗi. Change UI chỉ cần hoàn thành mức UI/mock; API thật được theo dõi như dependency/change riêng, không thành checkbox bắt buộc để archive change UI.

## 2. Quy tắc kiến trúc áp dụng cho mọi màn

```text
Views/<NghiệpVụ>/*.xaml
    ↕ x:Bind / Binding / Command
ViewModels/<NghiệpVụ>/*ViewModel.cs
    ↓ interface được inject qua constructor
Repositories/<NghiệpVụ>/I...Repository.cs + ...Repository.cs
    ↓
Services/Api/IApiClient.cs + ApiClient.cs
    ↓ HTTPS / JWT
ASP.NET Core API
```

`Models/`, `Views/`, `ViewModels/`, `Repositories/`, `Services/` là các thư mục **cùng cấp**. Không thêm lớp `Data/` hoặc thư mục `Contracts/`. Interface nằm cạnh implementation trong từng nhóm repository/service. Những tên lớp chưa tồn tại trong kế hoạch là **đề xuất**, không phải API đang có sẵn.

| Thành phần          | Trách nhiệm khi chuyển ảnh thành code                                                                                                                                                                                                                                        |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| View                  | Bố cục, style, binding, focus và thao tác thuần giao diện; code-behind chỉ nối ViewModel/lifecycle cần thiết, không truy vấn API hay tính nghiệp vụ.                                                                                                               |
| ViewModel             | `[ObservableProperty]`, `[RelayCommand]`, trạng thái loading/error/empty, lựa chọn bộ lọc, điều phối thao tác qua repository. Không tham chiếu Page, ContentDialog hoặc HttpClient.                                                                              |
| Model                 | Dữ liệu có kiểu như ProductSummary, StockSummary, CustomerDetail, OrderDraft; không chứa control XAML. Chỉ tách Model khỏi DTO khi có lợi ích thực tế.                                                                                                             |
| Repository            | Trong change UI, mock implementation cung cấp Model/kết quả giả lập qua cùng interface. HTTP implementation dùng ApiClient và ánh xạ DTO → Model ở change tích hợp riêng khi có contract thật. Không định dạng chuỗi tiền để phục vụ riêng một View. |
| ApiClient và service | HTTP/JSON/lỗi/token; NavigationService, DialogService phụ trách cơ chế UI qua interface. Đăng ký DI trong App/ServiceCollectionExtensions theo tài liệu base.                                                                                                           |
| Backend               | Giá và giảm giá chính thức, thanh toán, phân quyền, uniqueness, tồn kho, Serial/IMEI và transaction. UI chỉ kiểm tra đầu vào và hiển thị kết quả.                                                                                                            |

Tiền giữ bằng `decimal`, ngày giờ theo hợp đồng API khi đã chốt; định dạng tiền/ngày ở lớp trình bày. Các thao tác async truyền `CancellationToken`; hủy truy vấn cũ khi tìm kiếm hoặc rời màn, tránh kết quả cũ ghi đè kết quả mới. Tạo mock repository cho màn đang làm, không viết sẵn toàn bộ repository của ứng dụng. Mock phải giả lập rõ success/loading/empty/error và các lỗi nghiệp vụ cần để thử UI, nhưng không được coi kết quả giả là xác nhận nghiệp vụ server.

## 3. Chọn thiết kế chính và xử lý khác biệt

| Nội dung       | Quyết định để triển khai                                                                                                                                                                                                                                                                                                        |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Theme           | Lấy bảng style D31 làm mốc: primary`#2563EB`, canvas `#F8FAFC`, surface trắng, text `#0F172A`; heading 20, body 13, caption 11–12; badge radius 4, input/button radius 8. Các trị số khác trong `DESIGN.md` chỉ tham khảo, tránh trộn hai bộ token. Kiểm tra chữ tiếng Việt và scale thực tế trên Uno. |
| Shell           | Theo D01: 5 mục chính Tổng quan, Sản phẩm, Danh mục và Kho, Đơn hàng, Khách hàng. POS đi từ “Tạo đơn”; không thêm mục POS riêng trong đợt này. Cài đặt ở footer nhưng chưa có thiết kế nội dung.                                                                                                  |
| Đăng nhập    | Chọn D11 chia hai vùng làm bản chính. D10 là phương án bố cục khác, không phải màn thứ hai. Các nút xem thử trạng thái trong thiết kế chỉ là công cụ demo, không đưa vào ứng dụng.                                                                                                                   |
| Sản phẩm      | D23 là danh sách chính dạng lưới; D27 là dialog chi tiết. D28 chỉ dùng phần form thêm sản phẩm, không lấy nền split-view để thay màn danh sách.                                                                                                                                                                  |
| Sửa sản phẩm | D24 là bố cục chính theo tab biến thể; D25/D26 bổ sung trường và gợi ý SKU. D25 PNG hiển thị dialog không đầy đủ nên đối chiếu HTML; không tái tạo lỗi render đó.                                                                                                                                        |
| Đơn hàng     | D09 là danh sách chính nhóm theo ngày. D21/D22/D29/D30 là các trạng thái bên trong một dialog tạo đơn, triển khai từng bước.                                                                                                                                                                                        |
| Dialog          | D31 gợi ý rộng 480/640/800; clamp theo cửa sổ, có vùng cuộn. Chỉ mở một ContentDialog tại một thời điểm; sửa IMEI/thêm khách nhanh dùng vùng nội dung bên trong dialog đang mở hoặc đóng rồi khôi phục dialog cha.                                                                                     |

Đây là các giả định thiết kế của kế hoạch để có một hướng triển khai nhất quán; có thể đổi từng lựa chọn trước khi thực hiện màn liên quan.

## 4. Thứ tự triển khai

| Đợt                  | Mã công việc, làm lần lượt                                             | Kết quả                                                               |
| ---------------------- | ----------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| A — Nền tảng        | UI-00 → UI-01 → UI-02                                                       | Theme tối thiểu, Shell, đăng nhập.                                 |
| B — Màn đầu tiên  | UI-03                                                                         | Dashboard hoàn chỉnh độc lập; mốc bàn giao đầu phù hợp Dev6. |
| C — Sản phẩm        | UI-04 → UI-05 → UI-06 → UI-07                                              | Danh sách → chi tiết → thêm → sửa/biến thể.                    |
| D — Danh mục và Kho | UI-08 → UI-09 → UI-10 → UI-11 → UI-12 → UI-13 → UI-14 → UI-15 → UI-16 | Mỗi tab/dialog được làm riêng.                                    |
| E — Khách hàng      | UI-17 → UI-18 → UI-19 → UI-20                                              | Danh sách → thêm/sửa → chi tiết → xử lý xóa.                  |
| F — Đơn hàng/POS   | UI-21 → UI-22 → UI-23 → UI-24 → UI-25 → UI-26                            | Danh sách → giỏ hàng → khách → tiền mặt → thẻ → VietQR.     |

Đây là thứ tự kỹ thuật, **không phải cam kết hoàn tất cả sáu đợt trong Sprint 1**. Nếu chỉ bắt đầu một phần, bắt đầu UI-00 rồi UI-01; dừng ở mốc A/B trước khi mở rộng nghiệp vụ. Các phụ thuộc ở từng task giúp đổi thứ tự mà không làm dở nhiều màn cùng lúc.

## 5. Công việc chi tiết theo màn

Quy ước đường dẫn: tên View dưới đây nằm trong `Views/`; ViewModel cùng tên bỏ hậu tố `Page`/`Dialog`, thêm `ViewModel`, đặt trong thư mục nghiệp vụ tương ứng tại `ViewModels/`. Ví dụ `Views/Catalog/ProductListPage.xaml` đi với `ViewModels/Catalog/ProductListViewModel.cs`. Task mở rộng dùng lại View/ViewModel đã có, không tạo bản sao. Các task đều áp dụng tiêu chí chung ở mục 6. Trong các dòng “Hoàn thành khi” bên dưới, yêu cầu về dữ liệu hoặc phản hồi từ API/backend là **kịch bản mock cần kiểm tra trên UI** cho change dựng màn; chúng không chứng minh server đã xử lý hay trả kết quả thật. Chỉ change tích hợp riêng mới đánh dấu đạt API/backend.

### Cách dùng prompt cho từng công việc

Mỗi công việc bên dưới có một khối prompt độc lập và tên change riêng. Trong Codex, copy nguyên khối có `$openspec-propose`; nếu dùng giao diện hỗ trợ slash command, thay dòng đầu bằng `/opsx:propose`. Đây là prompt gửi cho agent, không phải lệnh chạy trong terminal. Không cần copy thêm phần hướng dẫn chung vì từng prompt đã yêu cầu đọc đúng tài liệu và thiết kế.

Propose tạo bộ tài liệu change để chuẩn bị triển khai. Sau khi xem các artifact và xử lý dependency/blocker, gửi một yêu cầu riêng để code:

```text
$openspec-apply-change
Áp dụng change <tên-change-trong-prompt>, chỉ triển khai phạm vi change này.
```

Ví dụ task đầu tiên dùng tên `ui-00-theme-foundation`. Làm lần lượt theo phụ thuộc; không gửi cả 27 prompt cùng lúc. Nếu tên change đã tồn tại, dùng workflow cập nhật change hoặc chọn tên mới, không ghi đè change đang làm.

### UI-00 — Theme và thành phần dùng cho màn đầu tiên

- **Thiết kế:** D31, D01. **Phụ thuộc:** không.
- Tạo resource dictionary trong `Styles/` cho màu, chữ, spacing; style button/input/card/badge, trạng thái focus/disabled/error. Font Inter đóng gói nếu có asset và giấy phép phù hợp; fallback font hệ thống hỗ trợ tiếng Việt.
- Chuẩn bị `CurrencyConverter` và trạng thái loading/empty/error dùng chung tối thiểu; chỉ tách `Controls/` khi màn thực tế cần. Chốt cách dùng icon native/SVG, ảnh sản phẩm có fallback.
- **Hoàn thành khi:** chủ dự án xác nhận đã tự kiểm tra token/control trong vùng preview trên cửa sổ đang dùng và khi thu hẹp còn dùng được; không cần canh scale chính xác hoặc dựng trước mọi control của ứng dụng.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-00-theme-foundation` cho UI-00 — Theme và thành phần dùng cho màn đầu tiên trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-00, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ xây nền theme/control tối thiểu theo D31, có preview kiểm tra; không dựng các màn nghiệp vụ hoặc một thư viện control đầy đủ.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-00; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-01 — Khung điều hướng Shell

- **Thiết kế:** D01 và sidebar D23/D20. **Phụ thuộc:** UI-00.
- **View:** `Shell/ShellPage.xaml`; **VM:** `ShellViewModel`; service `INavigationService`, `IDialogService` và session hiện tại.
- Sidebar, logo, mục đang chọn, vùng nội dung, user footer, thu gọn khi cửa sổ hẹp. Thiết lập DI/ViewModel factory để điều hướng tương thích Frame và constructor injection.
- **Hoàn thành khi:** đổi route không mất Shell; menu active đúng; route chưa làm hiển thị placeholder rõ ràng. Cài đặt chưa có thiết kế nên tạm vô hiệu hóa hoặc ghi rõ chưa triển khai. Kiểm tra back/navigation và quyền hiển thị menu theo capability do server cung cấp/contract quy định.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-01-navigation-shell` cho UI-01 — Khung điều hướng Shell trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-01, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm Shell với 5 mục chính, footer, DI/navigation/dialog service tối thiểu và placeholder cho route chưa làm; không triển khai nội dung các màn đích.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-01; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-02 — Đăng nhập

- **Thiết kế:** D11 chính, D10 tham khảo. **Phụ thuộc:** UI-00, UI-01.
- **View:** `Identity/LoginPage.xaml`; **VM:** `LoginViewModel`; `Repositories/Identity/IAuthRepository`, dịch vụ lưu token/session.
- Form tài khoản/mật khẩu, hiện/ẩn mật khẩu, ghi nhớ phiên. Trạng thái thiếu dữ liệu, đang gửi, sai tài khoản, khóa tài khoản, lỗi mạng phải đến từ validation/kết quả repository.
- **Hoàn thành khi:** đăng nhập preview qua mock repository vào Shell có nhãn demo; gửi thất bại giữ input phù hợp và thông báo; chặn gửi lặp; đăng xuất xóa session mock. Không lưu mật khẩu để thực hiện “ghi nhớ”; 401/403 được mô phỏng để kiểm tra điều phối UI. JWT, refresh token, lưu phiên bảo mật và API thật thuộc change tích hợp riêng.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-02-login` cho UI-02 — Đăng nhập trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-02, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Dùng D11 làm layout chính; bao phủ validation, loading, sai thông tin, khóa tài khoản, lỗi mạng, session/token và đăng xuất. Không đưa bộ nút demo trạng thái vào app.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-02; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-03 — Tổng quan kinh doanh

- **Thiết kế:** D01. **Phụ thuộc:** UI-01; trạng thái preview của UI-02 đủ để dựng màn bằng mock, API thật cần change tích hợp riêng.
- **View:** `Dashboard/DashboardPage.xaml`; **VM:** `DashboardViewModel`; `IAnalyticsRepository`, Model `DashboardSummary`, điểm doanh thu và đơn gần đây.
- Bốn KPI, bộ lọc thời gian, biểu đồ doanh thu, tồn kho thấp, đơn gần đây. Chọn control/thư viện chart tương thích cả hai target khi làm task này; dữ liệu chart nhận từ VM.
- **Hoàn thành khi:** đổi khoảng ngày cập nhật đồng bộ; loading/empty/error/retry rõ ràng; số liệu đối chiếu được với repository. Chốt định nghĩa doanh thu, kỳ so sánh và múi giờ với API, không lấy tổng các đơn của một trang để giả làm KPI toàn hệ thống. Link sang màn chưa làm chỉ kích hoạt sau task tương ứng.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-03-dashboard` cho UI-03 — Tổng quan kinh doanh trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-03, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm KPI, bộ lọc ngày, biểu đồ, tồn thấp và đơn gần đây. Chốt định nghĩa số liệu và khả năng chart trên Uno; không triển khai các màn được liên kết từ Dashboard.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-03; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-04 — Danh sách sản phẩm dạng lưới

- **Thiết kế:** D23. **Phụ thuộc:** UI-01.
- **View:** `Catalog/ProductListPage.xaml`; **VM:** `ProductListViewModel`; `IProductRepository`, `ProductSummary`, bộ lọc và kết quả phân trang.
- Tìm tên/SKU, lọc danh mục/thương hiệu/giá/tồn kho, sắp xếp, card ảnh/giá/biến thể/tồn và phân trang. Chế độ bảng chưa có thiết kế độc lập nên để backlog, không dựng thêm trong task này.
- **Hoàn thành khi:** tìm kiếm hủy request cũ; đổi filter về trang đầu; rỗng khác với lỗi tải; ảnh hỏng có fallback; số lượng tổng do API cung cấp. Các nút thêm/chi tiết nối ở UI-05/06.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-04-product-list` cho UI-04 — Danh sách sản phẩm dạng lưới trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-04, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm lưới sản phẩm, tìm kiếm/filter/sort/paging và trạng thái tải. Xử lý request cũ; không làm chế độ bảng hoặc form thêm/sửa/chi tiết trong change này.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-04; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-05 — Chi tiết sản phẩm (showroom)

- **Thiết kế:** D27. **Phụ thuộc:** UI-04.
- **View:** `Catalog/ProductDetailDialog.xaml`; **VM:** `ProductDetailViewModel`; `IProductRepository`, Model `ProductDetail`/`ProductVariant`.
- Ảnh, specs, chọn dung lượng/màu, giá và số lượng đúng biến thể. Giá vốn/lãi chỉ hiển thị khi người dùng được phép và API trả dữ liệu.
- **Hoàn thành khi:** chọn biến thể cập nhật toàn bộ phần liên quan, không hiển thị tổ hợp không tồn tại. Liên kết xem Serial/IMEI và sửa nối sau UI-12/UI-07. Ngừng kinh doanh cần API hỗ trợ và phản hồi thành công trước khi đổi trạng thái.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-05-product-detail` cho UI-05 — Chi tiết sản phẩm (showroom) trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-05, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm dialog showroom, lựa chọn biến thể, specs, giá/tồn và quyền xem giá vốn. Không dựng form sửa hay tab Serial/IMEI; chỉ nối đến thành phần đã có.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-05; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-06 — Thêm sản phẩm

- **Thiết kế:** phần dialog D28. **Phụ thuộc:** UI-04, dữ liệu lựa chọn danh mục.
- **View:** `Catalog/ProductCreateDialog.xaml`; **VM:** `ProductCreateViewModel`; `IProductRepository`, `ICategoryRepository`, Model form tạo sản phẩm.
- Thông tin cơ bản, image URL/preview, giá, specs động, biến thể. Báo lỗi theo trường; uniqueness SKU/barcode kiểm tra cuối ở server.
- **Hoàn thành khi:** tạo thành công refresh danh sách; lỗi không mất dữ liệu nhập; Hủy không ghi dữ liệu. “Lưu nháp” chỉ bật khi có contract lưu nháp. Phần tồn ban đầu trong ảnh phải đi qua nghiệp vụ nhập kho được backend hỗ trợ; sản phẩm quản lý serial không được cộng tồn bằng số lượng đơn thuần. Nếu chưa có API kết hợp, tạo sản phẩm trước rồi dẫn tới nhập kho.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-06-product-create` cho UI-06 — Thêm sản phẩm trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-06, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm form thêm sản phẩm, specs/biến thể và validation. Tồn ban đầu phải theo contract nhập kho, hàng serial cần danh sách máy; không tự cộng tồn hoặc mặc định có API lưu nháp.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-06; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-07 — Sửa sản phẩm và biến thể

- **Thiết kế:** D24 chính, D25/D26 bổ sung. **Phụ thuộc:** UI-05, UI-06.
- **View:** `Catalog/ProductEditDialog.xaml`; **VM:** `ProductEditViewModel`; dùng lại editor cho specs/biến thể khi đã thấy phần trùng với form tạo; `IProductRepository`.
- Nạp bản đang có, chuyển tab biến thể, sửa SKU/barcode/giá/specs, thêm/xóa biến thể theo quyền. Gợi ý SKU chỉ là đề xuất, server quyết định hợp lệ/duy nhất.
- **Hoàn thành khi:** chuyển tab không mất input; Hủy không làm đổi Model của danh sách; lỗi conflict/biến thể đã phát sinh giao dịch hiển thị rõ. Không tự đổi kiểu quản lý serial khi server không cho phép; hành động xóa theo UI-16.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-07-product-edit` cho UI-07 — Sửa sản phẩm và biến thể trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-07, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm dialog sửa theo tab biến thể D24, dùng D25/D26 bổ sung. Giữ input khi đổi tab, Hủy không sửa dữ liệu gốc; không triển khai thao tác xóa thuộc UI-16.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-07; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-08 — Danh mục và Kho: tab cây danh mục

- **Thiết kế:** D16. **Phụ thuộc:** UI-01.
- **View:** `Inventory/InventoryPage.xaml` chứa tab; nội dung `Catalog/CategoryListView.xaml`; **VM:** `InventoryViewModel`, `CategoryListViewModel`; `ICategoryRepository`, Model `CategoryNode`.
- Cây cha/con, mở/thu, mô tả, số sản phẩm, trạng thái hoạt động. VM tab con tải khi được chọn; không dồn toàn bộ nghiệp vụ kho vào một VM.
- **Hoàn thành khi:** chọn tab/nhánh đúng, số lượng không đếm từ dữ liệu phân trang thiếu. Form thêm/sửa danh mục chưa có ảnh: tách task bổ sung sau, không tự coi đã hoàn thành CRUD.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-08-category-tree` cho UI-08 — Danh mục và Kho: tab cây danh mục trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-08, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm khung tab Danh mục và Kho cùng cây danh mục tải độc lập. Không làm nội dung tab tồn/serial hoặc form thêm/sửa danh mục còn thiếu thiết kế.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-08; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-09 — Tab tồn kho sản phẩm

- **Thiết kế:** D08. **Phụ thuộc:** UI-08.
- **View:** `Inventory/StockListView.xaml`; **VM:** `StockListViewModel`; `IInventoryRepository`, Model `StockSummary`.
- Bảng tồn, reserved/available, lọc/tìm kiếm/phân trang; actions mở chi tiết/điều chỉnh. Bộ lọc nhiều kho chỉ bật nếu API và mô hình dữ liệu hỗ trợ.
- **Hoàn thành khi:** số tồn vật lý, giữ chỗ, khả dụng không bị đánh đồng; lọc kết hợp đúng. Nhập kho/xuất file là action có task/contract riêng, không giả lập tải thành công.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-09-stock-list` cho UI-09 — Tab tồn kho sản phẩm trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-09, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm bảng tồn, phân biệt physical/reserved/available và filter/paging. Không làm các dialog tiếp theo; xuất file, nhiều kho và nhập kho cần contract riêng.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-09; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-10 — Chi tiết tồn kho

- **Thiết kế:** D03. **Phụ thuộc:** UI-09.
- **View:** `Inventory/StockDetailDialog.xaml`; **VM:** `StockDetailViewModel`; `IInventoryRepository`, Model chi tiết số dư và movement gần đây.
- Tóm tắt sản phẩm/biến thể, ba số lượng, lịch sử biến động, link sang Serial/IMEI có filter.
- **Hoàn thành khi:** số dư khớp cùng đối tượng trong danh sách; lịch sử do API cung cấp, không suy diễn từ tồn hiện tại. Khi điều chỉnh xong tải lại dữ liệu của dialog và list.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-10-stock-detail` cho UI-10 — Chi tiết tồn kho trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-10, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm dialog số dư và movement gần đây, đường dẫn tới màn đã có, refresh sau thay đổi. Không suy diễn lịch sử từ số tồn hiện tại và không triển khai form điều chỉnh.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-10; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-11 — Điều chỉnh tồn kho

- **Thiết kế:** D02 và trạng thái chặn D05. **Phụ thuộc:** UI-10.
- **View:** `Inventory/StockAdjustmentDialog.xaml`; **VM:** `StockAdjustmentViewModel`; `IInventoryRepository`.
- Hàng thường: chọn tăng/giảm, lượng, lý do, xem trước số dư. Hàng serial: trạng thái hướng dẫn sang quy trình serial, không nhập số lượng trực tiếp.
- **Hoàn thành khi:** số lượng dương, lý do hợp lệ; lỗi 409/không đủ tồn giữ form và cho tải lại. Client không tự sửa ledger hoặc xác nhận số dư trước phản hồi server; server ghi movement trong transaction.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-11-stock-adjustment` cho UI-11 — Điều chỉnh tồn kho trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-11, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm tăng/giảm tồn cho hàng thường và trạng thái chặn hàng serial theo D02/D05. Xử lý lỗi 409, giữ form; backend chịu trách nhiệm transaction/ledger.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-11; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-12 — Tab Serial/IMEI

- **Thiết kế:** D07. **Phụ thuộc:** UI-08; liên kết với UI-09/UI-10.
- **View:** `Inventory/SerialListView.xaml`; **VM:** `SerialListViewModel`; `IInventoryRepository`, Model `SerialSummary`.
- Tìm mã, lọc sản phẩm/trạng thái, bảng Serial/IMEI, trạng thái máy và ngày bảo hành nếu API cung cấp. Nhận filter khi mở từ sản phẩm/tồn kho.
- **Hoàn thành khi:** hỗ trợ serial chữ/số lẫn IMEI; map sáu trạng thái theo enum thật. Hạn bảo hành lấy từ ngày bảo hành, không tạo thêm trạng thái tồn kho “còn bảo hành”.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-12-serial-list` cho UI-12 — Tab Serial/IMEI trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-12, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm tab Serial/IMEI, filter và sáu trạng thái theo enum thật. Hỗ trợ mã serial chữ/số; bảo hành dựa trên ngày. Không làm dialog nhập hoặc timeline.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-12; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-13 — Nhập Serial/IMEI: quét và dán hàng loạt

- **Thiết kế:** D13 (quét/nhập), D12 (dán). **Phụ thuộc:** UI-12, contract nhập kho.
- **View:** `Inventory/SerialImportDialog.xaml`; **VM:** `SerialImportViewModel`; `IInventoryRepository`, Model các dòng nhập và lỗi từng dòng.
- Làm chế độ nhập/quét bàn phím trước; sau đó thêm tab dán nhiều dòng trong cùng dialog. Parse, chuẩn hóa, phát hiện trùng trong danh sách, hiển thị valid/invalid/duplicate, loại bỏ dòng.
- **Hoàn thành khi:** chuyển tab giữ danh sách; submit tránh gửi lặp; server kiểm tra trùng và cập nhật tồn qua transaction. Chốt với API nhập toàn bộ hoặc trả kết quả từng dòng để UI không báo nhập thành công cho dòng lỗi. Không mặc định đã tích hợp camera hoặc máy quét SDK.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-13-serial-import` cho UI-13 — Nhập Serial/IMEI: quét và dán hàng loạt trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-13, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm một dialog nhập/quét bàn phím rồi mở rộng dán hàng loạt, kiểm tra từng dòng và submit. Chốt nhập toàn bộ hay từng phần; không mặc định tích hợp camera/SDK hoặc làm editor UI-14.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-13; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-14 — Sửa nhanh một mã nhập lỗi

- **Thiết kế:** D14 lỗi, D15 hợp lệ. **Phụ thuộc:** UI-13.
- **View:** `Inventory/SerialEntryEditorView.xaml` bên trong dialog nhập; **VM:** `SerialEntryEditorViewModel`.
- Chỉnh một dòng chưa submit, đếm ký tự, báo lỗi, cập nhật lại danh sách và số dòng hợp lệ.
- **Hoàn thành khi:** quy tắc IMEI không áp cho serial laptop. Chỉ dùng độ dài/Luhn nếu hợp đồng validation yêu cầu; không hiển thị “GSMA đã xác thực” khi chưa có dịch vụ xác minh. Hai ảnh là hai trạng thái của cùng editor.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-14-serial-entry-editor` cho UI-14 — Sửa nhanh một mã nhập lỗi trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-14, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ bổ sung editor một dòng lỗi vào dialog UI-13. D14/D15 là hai trạng thái cùng component; không mở ContentDialog lồng, không áp luật IMEI lên serial và không giả xác minh GSMA.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-14; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-15 — Lịch sử một Serial/IMEI

- **Thiết kế:** D04. **Phụ thuộc:** UI-12.
- **View:** `Inventory/SerialHistoryDialog.xaml`; **VM:** `SerialHistoryViewModel`; `IInventoryRepository`, Model sự kiện lịch sử.
- Timeline nhập/bán/trả/sửa chữa với mốc thời gian, tham chiếu đơn và người thực hiện khi được phép.
- **Hoàn thành khi:** thứ tự thời gian đúng, empty/error rõ ràng; các trường chi nhánh/kệ/giá vốn chỉ hiện khi có dữ liệu và quyền. In/xuất lịch sử là bước bổ sung khi có yêu cầu định dạng, không nằm trong phần dựng timeline.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-15-serial-history` cho UI-15 — Lịch sử một Serial/IMEI trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-15, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm timeline từ dữ liệu lịch sử server, quyền xem chi tiết và các trạng thái tải. Không triển khai in/xuất file hoặc tự tạo dữ liệu kệ/chi nhánh chưa có API.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-15; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-16 — Xác nhận xóa/ngừng kinh doanh sản phẩm

- **Thiết kế:** D06. **Phụ thuộc:** UI-07, UI-09.
- **View:** `Catalog/ProductDeleteDialog.xaml`; **VM:** `ProductDeleteViewModel`; `IProductRepository`.
- Xác nhận đối tượng, đang xử lý, thành công và trường hợp server không cho xóa. Dùng action ngừng kinh doanh nếu nghiệp vụ/API quy định thay cho xóa.
- **Hoàn thành khi:** không sao chép lời hứa “xóa vĩnh viễn toàn bộ IMEI” trong ảnh khi trái với lịch sử giao dịch/ledger. Chỉ đóng và refresh sau thành công; bị chặn thì hiển thị lý do cụ thể.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-16-product-delete` cho UI-16 — Xác nhận xóa/ngừng kinh doanh sản phẩm trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-16, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm xác nhận xóa/ngừng kinh doanh và trạng thái bị backend chặn. Không sao chép lời hứa xóa toàn bộ IMEI trong ảnh; bảo toàn ledger và lịch sử giao dịch theo contract.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-16; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-17 — Danh sách khách hàng

- **Thiết kế:** D20. **Phụ thuộc:** UI-01.
- **View:** `Customers/CustomerListPage.xaml`; **VM:** `CustomerListViewModel`; `ICustomerRepository`, `CustomerSummary`.
- Search tên/điện thoại, lọc hạng, sort, phân trang; các cột điểm, số đơn, chi tiêu, mua gần nhất.
- **Hoàn thành khi:** format nhất quán; tổng số theo từng hạng và trang do API trả; khách chưa có giao dịch hiển thị phù hợp. Không tính tổng chi tiêu từ một trang lịch sử đơn.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-17-customer-list` cho UI-17 — Danh sách khách hàng trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-17, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm bảng khách, tìm kiếm/filter hạng/sort/paging. Điểm và tổng chi tiêu do server trả; không làm các form/dialog khách tiếp theo.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-17; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-18 — Thêm/sửa khách hàng

- **Thiết kế:** D19. **Phụ thuộc:** UI-17.
- **View:** `Customers/CustomerEditDialog.xaml`; **VM:** `CustomerEditViewModel`; `ICustomerRepository`.
- Dùng hai mode tạo/sửa, tên/điện thoại/ngày sinh; validation cục bộ và lỗi trùng điện thoại từ server; link “Xem khách này” khi server cho phép trả định danh khách trùng.
- **Hoàn thành khi:** sửa giữ ID, tạo không lấy nhầm ID; retry không mất form; điểm/hạng do server quyết định. Form khách có thể tái sử dụng trong tạo đơn sau này nhưng chưa tích hợp ngay ở task này.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-18-customer-editor` cho UI-18 — Thêm/sửa khách hàng trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-18, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm form dùng chung hai mode tạo/sửa, validation và lỗi trùng điện thoại. Điểm/hạng do server quyết định; chưa nhúng vào tạo đơn hoặc xây dialog chi tiết khách.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-18; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-19 — Chi tiết khách hàng

- **Thiết kế:** D17. **Phụ thuộc:** UI-17/UI-18.
- **View:** `Customers/CustomerDetailDialog.xaml`; **VM:** `CustomerDetailViewModel`; `ICustomerRepository` và repository nghiệp vụ liên quan nếu contract tách riêng lịch sử đơn/thiết bị.
- Profile, KPI, tab lịch sử mua/thiết bị/ghi chú; lazy load từng tab, không gọi chéo ViewModel của màn khác.
- **Hoàn thành khi:** đổi tab không lẫn dữ liệu khách trước; chỉ hiện ghi chú/số liệu có API hỗ trợ; sửa xong refresh profile. Link mở đơn nối khi có màn chi tiết đơn ở backlog.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-19-customer-detail` cho UI-19 — Chi tiết khách hàng trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-19, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm dialog profile/KPI và lazy load các tab lịch sử, thiết bị, ghi chú có contract hỗ trợ. Không dựng màn chi tiết đơn hoặc hành động xóa thuộc UI-20.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-19; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-20 — Xóa khách hàng và trường hợp bị chặn

- **Thiết kế:** D18. **Phụ thuộc:** UI-19.
- **View:** `Customers/CustomerDeleteDialog.xaml`; **VM:** `CustomerDeleteViewModel`; `ICustomerRepository`.
- Form xác nhận và trạng thái không thể xóa vì có giao dịch, kèm đường dẫn xem lịch sử.
- **Hoàn thành khi:** backend quyết định được xóa hay không kể cả khi dữ liệu vừa thay đổi; không chỉ dựa vào số đơn đang có trên UI. Không mở chồng hai ContentDialog.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-20-customer-delete` cho UI-20 — Xóa khách hàng và trường hợp bị chặn trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-20, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm xác nhận xóa và trạng thái bị chặn do giao dịch. Backend quyết định quyền xóa với dữ liệu hiện thời; không mở chồng ContentDialog hoặc mở rộng thành quản lý đơn.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-20; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-21 — Danh sách đơn hàng nhóm theo ngày

- **Thiết kế:** D09. **Phụ thuộc:** UI-01; liên kết khách dùng UI-17.
- **View:** `Orders/OrderListPage.xaml`; **VM:** `OrderListViewModel`; `IOrderRepository`, Model nhóm ngày và `OrderSummary`.
- Tìm theo mã/khách/IMEI khi API hỗ trợ, filter ngày/trạng thái/phương thức, nhóm ngày, phân trang, mở/thu nhóm.
- **Hoàn thành khi:** nhóm theo múi giờ cửa hàng đã thống nhất; tổng theo ngày do server cung cấp, không cộng trang hiện tại. Chế độ bảng và chi tiết đơn riêng chưa đủ thiết kế, để backlog. “Tạo đơn” nối sau UI-22.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-21-order-list` cho UI-21 — Danh sách đơn hàng nhóm theo ngày trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-21, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm danh sách đơn nhóm theo ngày, tìm/filter/paging. Tổng ngày do server trả theo múi giờ thống nhất; không dựng tạo đơn, chi tiết đơn hoặc chế độ bảng chưa có thiết kế.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-21; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-22 — Tạo đơn: khung dialog và giỏ hàng

- **Thiết kế:** phần giỏ D21/D29/D30. **Phụ thuộc:** UI-21, UI-04, UI-12.
- **View:** `Orders/OrderCreateDialog.xaml`; **VM:** `OrderCreateViewModel`; `IOrderRepository`, `IProductRepository`, `IInventoryRepository`; Model `OrderDraft` và dòng hàng.
- Tìm/quét sản phẩm, chọn biến thể/Serial/IMEI, tăng giảm hàng thường, bỏ dòng, tổng tạm tính. Task này chỉ hoàn thiện giỏ, chưa bật nút thanh toán thật.
- **Hoàn thành khi:** không chọn một IMEI cho hai dòng; quantity hàng serial khớp số máy chọn; không tự giữ chỗ ở server chỉ vì chọn trên UI. Việc lưu nháp/giữ chỗ phải theo contract, xử lý hết tồn/giá thay đổi từ server và giữ giỏ khi lỗi.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-22-order-cart` cho UI-22 — Tạo đơn: khung dialog và giỏ hàng trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-22, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ làm khung tạo đơn và giỏ: tìm/quét hàng, chọn biến thể/IMEI, quantity, xóa dòng, tổng tạm. Chưa bật thanh toán; không tự giữ tồn, không dựng thêm khách hoặc các phương thức trả tiền.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-22; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-23 — Tạo đơn: chọn khách và thêm khách nhanh

- **Thiết kế:** D21/D22. **Phụ thuộc:** UI-22, UI-18.
- Mở rộng cùng `OrderCreateDialog`/`OrderCreateViewModel`; tái dùng editor khách trong vùng phải; `ICustomerRepository`.
- Tìm/chọn/bỏ khách, khách lẻ, thêm nhanh, lỗi điện thoại trùng và chọn khách đã tồn tại.
- **Hoàn thành khi:** không mở modal lồng; thêm khách thất bại không mất giỏ; tạo thành công chọn đúng khách. Hạng/chiết khấu hiển thị từ kết quả backend, không hard-code “Vàng = 5%” từ ảnh.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-23-order-customer` cho UI-23 — Tạo đơn: chọn khách và thêm khách nhanh trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-23, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ mở rộng dialog tạo đơn đã có để tìm/chọn khách, khách lẻ và thêm khách nhanh bằng editor tái dùng. Giữ giỏ khi lỗi/trùng điện thoại; không mở modal lồng, không hard-code giảm giá theo ảnh.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-23; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-24 — Tạo đơn: thanh toán tiền mặt

- **Thiết kế:** D29. **Phụ thuộc:** UI-23, contract báo giá và chốt đơn.
- Mở rộng phần thanh toán của dialog hiện có; `IOrderRepository` (hoặc repository Sales nếu hợp đồng thực tế tách riêng, tránh hai lớp cùng bọc một thao tác).
- Tiền khách đưa, gợi ý mệnh giá, tiền trả lại, tổng chính thức, xác nhận thanh toán và trạng thái kết quả.
- **Hoàn thành khi:** tiền đưa không nhỏ hơn phải trả; backend chốt giá, tồn và điểm trong quy trình nhất quán. Chặn click lặp; timeout khi submit phải tra trạng thái giao dịch bằng định danh/idempotency đã thống nhất trước khi thử lại, tránh tạo hai đơn. Thành công mới xóa giỏ và refresh danh sách.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-24-cash-payment` cho UI-24 — Tạo đơn: thanh toán tiền mặt trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-24, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ thêm thanh toán tiền mặt vào dialog hiện có, tiền nhận/trả lại và kết quả chốt đơn. Chốt contract giá chính thức, idempotency/tra trạng thái sau timeout; không triển khai thẻ hoặc VietQR.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-24; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-25 — Tạo đơn: thanh toán thẻ POS

- **Thiết kế:** D30. **Phụ thuộc:** UI-24 và contract phương thức thẻ.
- Bổ sung trạng thái chọn thẻ, mã giao dịch tham chiếu, kết quả ghi nhận; tái dùng pipeline chốt đơn.
- **Hoàn thành khi:** server nhận phương thức/mã đúng hợp đồng; không hiển thị “đã kết nối Pax A920/COM3” hoặc “đã xác thực” chỉ vì nhập mã. Tích hợp thiết bị thanh toán thật là phạm vi riêng nếu được yêu cầu; ghi nhận giao dịch thủ công phải được nghiệp vụ cho phép và mô tả đúng trên UI.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-25-card-payment` cho UI-25 — Tạo đơn: thanh toán thẻ POS trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-25, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ thêm trạng thái thẻ và mã giao dịch vào pipeline chốt đơn hiện có. Không mặc định kết nối thiết bị POS/COM3 hoặc xác thực giao dịch; xác định rõ contract cho ghi nhận thủ công.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-25; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

### UI-26 — Tạo đơn: VietQR

- **Thiết kế:** tab VietQR trong D21/D22; **thiếu ảnh trạng thái QR đầy đủ**. **Phụ thuộc:** UI-24, bổ sung thiết kế QR/chờ/thành công/hết hạn và contract thanh toán.
- Dự kiến phần QR trong cùng dialog, dữ liệu nhận từ repository; hiển thị số tiền/nội dung chuyển khoản, refresh trạng thái và hủy chờ.
- **Hoàn thành khi:** trạng thái xác nhận đến từ cơ chế backend đã thống nhất, không tự coi hiện QR là đã thanh toán. Hủy polling khi đóng; xử lý hết hạn/lỗi mạng/đã trả tiền nhưng response timeout. Chưa triển khai phần thiếu hình/API bằng cách tự đoán nghiệp vụ.

**Prompt OpenSpec propose — copy toàn bộ khối:**

```text
$openspec-propose

Tạo change `ui-26-vietqr-payment` cho UI-26 — Tạo đơn: VietQR trong repository TechStoreManagement.
Đọc docs/UI_IMPLEMENTATION_PLAN.md: toàn bộ mục UI-26, quy tắc/tiêu chí chung mục 1–6 và các giới hạn mục 7; mở screen.png cùng code.html tương ứng qua bảng ảnh mục 8. Đọc docs/MVVM_REPOSITORY_BASE_PROJECT.md, đặc biệt mục 6, và kiểm tra code/spec/change hiện có trước khi đề xuất.
Phạm vi: Chỉ thêm phần VietQR vào dialog hiện có. Kiểm tra trước phần thiết kế QR/chờ/thành công/hết hạn và contract xác nhận còn thiếu; ghi rõ blocker/đề xuất bổ sung, không bịa API hoặc coi hiện QR là đã trả tiền.
Áp dụng Uno XAML native + CommunityToolkit.Mvvm + DI; Models/Views/ViewModels/Repositories/Services cùng cấp, interface cạnh implementation, không thêm Data/Contracts hoặc đổi sang Features. ViewModel gọi repository interface; với màn cần API, dùng mock repository qua DI để kiểm tra UI. HTTP repository/ApiClient chỉ thuộc change tích hợp riêng khi có contract thật; backend giữ nghiệp vụ và phân quyền.
Kiểm tra các task phụ thuộc đã có; tái sử dụng thành phần hiện hữu, ghi rõ dependency còn thiếu, không gộp cả màn phụ thuộc vào change này. Nếu màn cần API, change này chỉ tạo mock repository qua DI cùng các trạng thái cần kiểm tra; không thêm task gọi API thật, HTTP client, JWT hay sửa backend. Ghi hợp đồng/endpoint còn thiếu thành dependency cho change tích hợp riêng; mock không được trình bày là dữ liệu hoặc quyền thật.
Với mọi phần giao diện/tương tác, thêm task nghiệm thu riêng: sau khi agent tự build/test và chạy ứng dụng, hỏi chủ dự án manual test trên cửa sổ đang dùng và khi thu hẹp còn thao tác được; ghi lại xác nhận có ngày cùng phần chưa đạt. Không yêu cầu canh đúng kích thước/scale hoặc ảnh chụp, và không đánh dấu task nghiệm thu trực quan hoàn thành trước khi chủ dự án xác nhận.
Tạo đủ proposal, delta specs, design và tasks theo schema của dự án, giới hạn đúng UI-26; đưa tiêu chí hoàn thành của task và kiểm tra phù hợp ở mục 6 vào scenarios/tasks. Chia tasks đủ nhỏ để apply tuần tự, nêu rõ phần chưa sẵn sàng. Lượt propose này chỉ tạo artifact, chưa sửa code ứng dụng hoặc chạy apply.
```

## 6. Tiêu chí kiểm tra và bàn giao từng task

1. **Giao diện:** agent đối chiếu ảnh nguồn và tự kiểm tra những gì quan sát được; sau khi chạy được ứng dụng, nhờ chủ dự án **manual test** trên cửa sổ desktop đang dùng và một cửa sổ thu hẹp còn thao tác được. Chủ dự án kiểm tra bố cục, chữ tiếng Việt, spacing, cuộn, bảng/modal và nội dung không bị cắt; ghi nhận kết quả có ngày. Không bắt canh đúng kích thước cửa sổ, tỷ lệ scale 100%/125% hay chụp ảnh. Không dùng ảnh nguyên giao diện để giả làm control.
2. **Trạng thái:** màn lấy dữ liệu có loading, có dữ liệu, empty, error/retry; form có validation, submitting, success, failure. Không cần thêm trạng thái vô nghĩa cho màn không có thao tác đó.
3. **MVVM:** View không gọi API; VM không biết HTTP/JSON/control; repository interface và mock implementation đặt cạnh nhau; chọn nguồn qua DI. HTTP implementation chỉ thêm trong change tích hợp riêng khi có hợp đồng API. Không tạo service/repository trùng chức năng chỉ để đủ cây thư mục.
4. **Hành vi:** keyboard Tab/Enter/Escape, focus sau lỗi, command disabled đúng lúc; mock trả tình huống 401/403/409 cần thiết để kiểm tra phản hồi UI. Đây không phải kiểm chứng backend; phân quyền client chỉ phục vụ trải nghiệm, API thật vẫn bắt buộc kiểm quyền khi được tích hợp.
5. **Kiểm thử phù hợp:** VM tests cho hành vi có rủi ro như kết quả search cũ, validation, cancel, double-submit, bảo toàn giỏ; repository tests khi có mapping/error phức tạp. Không tạo unit test chỉ để kiểm màu hay lặp lại getter/setter.
6. **Build khi có code:** build target `net10.0-desktop`; kiểm tra target Windows trên môi trường Windows khả dụng. Màn cần hành vi native/dialog phải smoke test runtime, build thành công chưa đủ chứng minh giao diện đúng.
7. **Bàn giao:** ghi mã task, đối chiếu thiết kế, file thay đổi, trạng thái “UI/mock hoàn thành” và “API thật thuộc change sau”, kết quả build/test/runtime, xác nhận manual test có ngày của chủ dự án cho phần giao diện/tương tác và phần còn thiếu. Ảnh chụp chỉ là bằng chứng tùy chọn. Không tự đánh dấu đạt kiểm tra trực quan nếu chủ dự án chưa xác nhận; hoàn thành task đang chọn trước khi bắt đầu task tiếp theo.

**Riêng lần lập kế hoạch này:** chỉ kiểm tra tài liệu, liên kết và độ bao phủ 31 bộ thiết kế; không cần build/test ứng dụng vì không thay đổi code.

## 7. Các khoảng trống cần tách khỏi phạm vi hiện có

- Chưa có thiết kế riêng cho nội dung Cài đặt, chi tiết đơn, form thêm/sửa danh mục, danh sách sản phẩm dạng bảng và QR đầy đủ. Đưa vào backlog thiết kế, không tự thêm nhiều màn trong task hiện tại.
- Xuất Excel/in tem/in lịch sử, multi-warehouse/kệ, upload ảnh, tích hợp thiết bị POS và xác thực GSMA cần contract/dịch vụ tương ứng. Ảnh có nút/nhãn không đồng nghĩa tính năng đã được triển khai.
- Chốt trước mỗi task tích hợp: request/response, paging/filter, enum, lỗi validation/conflict, quyền, cancellation và retry. Backend hiện có khung entity/configuration; không coi URL và DTO minh họa trong tài liệu base là endpoint thật.
- Dữ liệu tồn/giá/khách/đơn trên các ảnh có thể không nhất quán. Fake data phục vụ kiểm tra phải dùng cùng tập định danh và quan hệ, không sao chép rời rạc số liệu từng ảnh.
- Mọi trạng thái “xóa hàng kèm IMEI”, “tồn ban đầu”, “thẻ đã xác thực” phải đối chiếu nghiệp vụ server trước khi thực hiện. Không đánh đổi ledger, lịch sử giao dịch hay tính đúng thanh toán để khớp lời mô tả trong ảnh.

## 8. Danh mục ảnh và đối chiếu task

Các link bên dưới trỏ đến bộ thiết kế gốc, gồm cả ảnh và HTML. Mã D chỉ dùng trong tài liệu kế hoạch, không đổi tên thư mục nguồn.

| Mã | Thiết kế                                     | Nguồn                                                                                                                                                                                                                                                                                      | Công việc              |
| --- | ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------ |
| D01 | Tổng quan kinh doanh                          | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/admin_dashboard_techstore_management_system/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/admin_dashboard_techstore_management_system/code.html)                                                                   | UI-01, UI-03             |
| D02 | Điều chỉnh tồn kho hàng thường          | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_modal_ch_nh_s_a_t_n_kho_nh_m_c/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_modal_ch_nh_s_a_t_n_kho_nh_m_c/code.html)                                                                   | UI-11                    |
| D03 | Chi tiết tồn kho                             | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_modal_chi_ti_t_t_n_kho_imei/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_modal_chi_ti_t_t_n_kho_imei/code.html)                                                                         | UI-10                    |
| D04 | Timeline Serial/IMEI                           | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_modal_l_ch_s_m_y_serial_imei_timeline/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_modal_l_ch_s_m_y_serial_imei_timeline/code.html)                                                     | UI-15                    |
| D05 | Điều chỉnh hàng serial: hướng dẫn/chặn | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_popup_i_u_ch_nh_t_n_kho_bi_n_th_serial_imei/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_popup_i_u_ch_nh_t_n_kho_bi_n_th_serial_imei/code.html)                                         | UI-11                    |
| D06 | Xác nhận xóa sản phẩm                     | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_popup_x_c_nh_n_x_a_s_n_ph_m/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_popup_x_c_nh_n_x_a_s_n_ph_m/code.html)                                                                         | UI-16                    |
| D07 | Tab Serial/IMEI                                | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_tab_serial_imei/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_tab_serial_imei/code.html)                                                                                                 | UI-12                    |
| D08 | Tab tồn kho sản phẩm                        | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_tab_t_n_kho_s_n_ph_m/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/danh_m_c_kho_tab_t_n_kho_s_n_ph_m/code.html)                                                                                       | UI-09                    |
| D09 | Đơn hàng nhóm theo ngày                   | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/n_h_ng_danh_s_ch_nh_m_theo_ng_y_pos_b_n_l_t_i_qu_y/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/n_h_ng_danh_s_ch_nh_m_theo_ng_y_pos_b_n_l_t_i_qu_y/code.html)                                                     | UI-21                    |
| D10 | Đăng nhập: phương án ngang               | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/ng_nh_p_bi_n_th_3_ph_n_t_ng_ngang_khung_ng_nh_p_t_p_trung_horizontal_stack/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/ng_nh_p_bi_n_th_3_ph_n_t_ng_ngang_khung_ng_nh_p_t_p_trung_horizontal_stack/code.html)     | UI-02 (tham khảo)       |
| D11 | Đăng nhập: phương án chính              | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/ng_nh_p_techstore_management_system/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/ng_nh_p_techstore_management_system/code.html)                                                                                   | UI-02                    |
| D12 | Dán Serial/IMEI hàng loạt                   | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_danh_m_c_kho_modal_d_n_danh_s_ch_imei_h_ng_lo_t/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_danh_m_c_kho_modal_d_n_danh_s_ch_imei_h_ng_lo_t/code.html)                                             | UI-13                    |
| D13 | Quét/nhập Serial/IMEI                        | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_danh_m_c_kho_modal_qu_t_nh_p_imei/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_danh_m_c_kho_modal_qu_t_nh_p_imei/code.html)                                                                         | UI-13                    |
| D14 | Sửa mã nhập: lỗi                           | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_danh_m_c_kho_popup_ch_nh_s_a_nhanh_m_imei_l_i/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_danh_m_c_kho_popup_ch_nh_s_a_nhanh_m_imei_l_i/code.html)                                                 | UI-14                    |
| D15 | Sửa mã nhập: hợp lệ                       | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_danh_m_c_kho_popup_s_a_imei_th_nh_c_ng_15_s_h_p_l/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_danh_m_c_kho_popup_s_a_imei_th_nh_c_ng_15_s_h_p_l/code.html)                                         | UI-14                    |
| D16 | Cây danh mục                                 | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_danh_m_c_t_n_kho_techstore_admin_s_a_l_i_icon/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_danh_m_c_t_n_kho_techstore_admin_s_a_l_i_icon/code.html)                                                 | UI-08                    |
| D17 | Chi tiết khách hàng                         | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_kh_ch_h_ng_bi_n_th_b_c_c_2_h_p_tho_i_trung_t_m_ph_n_t_ng_modal_dialog/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_kh_ch_h_ng_bi_n_th_b_c_c_2_h_p_tho_i_trung_t_m_ph_n_t_ng_modal_dialog/code.html) | UI-19                    |
| D18 | Xóa khách bị chặn                          | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_kh_ch_h_ng_h_p_tho_i_x_c_nh_n_x_a/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_kh_ch_h_ng_h_p_tho_i_x_c_nh_n_x_a/code.html)                                                                         | UI-20                    |
| D19 | Thêm/sửa khách: trùng điện thoại        | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_kh_ch_h_ng_modal_th_m_s_a_b_o_l_i_s_t/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_kh_ch_h_ng_modal_th_m_s_a_b_o_l_i_s_t/code.html)                                                                 | UI-18                    |
| D20 | Danh sách khách hàng                        | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_kh_ch_h_ng_techstore/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_kh_ch_h_ng_techstore/code.html)                                                                                                   | UI-17                    |
| D21 | Tạo đơn: tìm khách, tab VietQR            | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_n_h_ng_modal_t_o_n_m_i_tr_ng_th_i_1_t_m_kh_ch_ch_a_c/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_n_h_ng_modal_t_o_n_m_i_tr_ng_th_i_1_t_m_kh_ch_ch_a_c/code.html)                                   | UI-22, UI-23, UI-26      |
| D22 | Tạo đơn: thêm khách nhanh lỗi            | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_n_h_ng_modal_t_o_n_m_i_tr_ng_th_i_2_form_th_m_nhanh_kh_ch_b_o_l_i_s_t/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_n_h_ng_modal_t_o_n_m_i_tr_ng_th_i_2_form_th_m_nhanh_kh_ch_b_o_l_i_s_t/code.html) | UI-23, UI-26             |
| D23 | Danh sách sản phẩm lưới                   | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_l_i_th_tinh_g_n_techstore_grid_view/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_l_i_th_tinh_g_n_techstore_grid_view/code.html)                                                   | UI-04                    |
| D24 | Sửa sản phẩm: tab biến thể                | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_modal_ch_nh_s_a_master_detail_tabs_phi_n_b_n_nh_gi/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_modal_ch_nh_s_a_master_detail_tabs_phi_n_b_n_nh_gi/code.html)                     | UI-07 (chính)           |
| D25 | Sửa sản phẩm: form thông tin               | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_modal_ch_nh_s_a_s_n_ph_m_producteditdialog/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_modal_ch_nh_s_a_s_n_ph_m_producteditdialog/code.html)                                     | UI-07 (tham khảo HTML)  |
| D26 | Sửa sản phẩm: gợi ý SKU/biến thể        | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_modal_ch_nh_s_a_t_ng_sinh_bi_n_th_sku/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_modal_ch_nh_s_a_t_ng_sinh_bi_n_th_sku/code.html)                                               | UI-07                    |
| D27 | Showroom chi tiết sản phẩm                  | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_modal_showroom_bi_n_th_1_chia_c_t_song_song_split_panel/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_modal_showroom_bi_n_th_1_chia_c_t_song_song_split_panel/code.html)           | UI-05                    |
| D28 | Thêm sản phẩm                               | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_modal_th_m_s_n_ph_m_m_i_uc_prod_03/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_modal_th_m_s_n_ph_m_m_i_uc_prod_03/code.html)                                                     | UI-06                    |
| D29 | Tạo đơn: tiền mặt                         | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/t_o_n_h_ng_tr_ng_th_i_1_thanh_to_n_ti_n_m_t/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/t_o_n_h_ng_tr_ng_th_i_1_thanh_to_n_ti_n_m_t/code.html)                                                                   | UI-22, UI-24             |
| D30 | Tạo đơn: thẻ POS                           | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/t_o_n_h_ng_tr_ng_th_i_2_thanh_to_n_th_pos/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/t_o_n_h_ng_tr_ng_th_i_2_thanh_to_n_th_pos/code.html)                                                                       | UI-22, UI-25             |
| D31 | Style guide                                    | [Ảnh](../UI/stitch_techstore_retail_admin_dashboard/techstore_style_guide_system_specification_board/screen.png) · [HTML](../UI/stitch_techstore_retail_admin_dashboard/techstore_style_guide_system_specification_board/code.html)                                                         | UI-00 và toàn bộ màn |
