# Proposal

## Why

Client hiện chỉ có khung `MainPage`, chưa có composition root, transport HTTP hoặc session abstraction để các màn hình dùng nhất quán. Dev 6 cần hoàn thành phần nền này trong Sprint 1 trước khi dựng design system, Shell và feature Catalog mẫu, để các thành viên phát triển Sprint 2 trên cùng kiến trúc MVVM + Repository.

## What Changes

- Thiết lập ranh giới `Views/`, `ViewModels/`, `Models/`, `Repositories/`, `Services/` theo `docs/MVVM_REPOSITORY_BASE_PROJECT.md`; quyết định của chủ dự án ưu tiên tài liệu này khi khác hướng dẫn frontend cũ. Chỉ thêm thành phần cần cho change, không tạo hàng loạt lớp rỗng.
- Thêm composition root duy nhất tại `App.xaml.cs`/`ServiceCollectionExtensions.cs`, constructor injection, cấu hình API và logging qua DI; giữ cửa sổ khởi động hiện có để kiểm chứng wiring.
- Thêm `IApiClient`/`ApiClient` dùng `IHttpClientFactory`, JSON và lỗi phản hồi thống nhất; phân biệt response có body, thao tác không có body, cancellation và timeout.
- Thêm token/session store trong bộ nhớ và `BearerTokenHandler` lấy token hiện hành theo từng request; cung cấp tín hiệu phiên hết hạn mà không tự đăng nhập/refresh hoặc đăng xuất khi 403.
- Thêm project `Frontend.UnitTests` .NET 10, kiểm thử transport/session/DI bằng fake handlers, không cần Uno UI thread hoặc backend chạy.
- Ghi bằng chứng build, tests và mở app trong handoff khi apply; checks chưa thực hiện giữ task unchecked.

Không bao gồm backend auth/refresh, secure persistence đa nền tảng, generic CRUD repository, client Unit of Work, Catalog feature, Shell/navigation/dialog mới, UI tokens hoặc packages controls Windows Community Toolkit. Design system và ưu tiên thư viện giao diện tiếp tục ở Change 2 của kế hoạch đã thống nhất.

## Capabilities

### New Capabilities

- `frontend-runtime`: Khởi tạo client bằng composition root, cấu hình hợp lệ, dependency lifetimes và logging, giữ các thành phần thuần C# kiểm thử được ngoài UI.
- `frontend-api-access`: Giao tiếp JSON qua API đã cấu hình, chuẩn hóa kết quả/lỗi và gắn token hiện hành với session boundary rõ ràng.

### Modified Capabilities

Không có. Các specs hiện tại mô tả backend Catalog, Inventory, schema/migrations và seeding; change này không đổi hành vi API phía server.

## Impact

- Client: `App.xaml.cs`, composition root mới, `Services/Api/`, `Services/Session/`, cấu hình non-secret và wiring startup của `MainPage` nếu cần.
- Dependencies: Microsoft.Extensions HTTP/DI/configuration/options/logging qua CPM frontend, giữ Uno SDK 6.7.30, `Mvvm` và hai target .NET 10 hiện có; không thêm Generic Host khi không cần hosted services.
- Tests/solution: `tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` và đăng ký vào `TechStore.sln`; link source thuần C# thay vì tham chiếu Uno executable đa target.
- Backend/shared contracts: chỉ đọc để đối chiếu, không sửa. DTO phân trang thật của Catalog được giữ nguyên; snippets mảng trong tài liệu kiến trúc là minh họa.
- Kết quả lần propose này chỉ là planning artifacts; hành vi nêu trên cần được implement và kiểm chứng ở lượt apply riêng.
