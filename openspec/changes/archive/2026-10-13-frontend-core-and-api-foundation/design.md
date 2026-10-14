# Design

## Context

Xem `proposal.md` phần Why. Client hiện có `App`, `MainPage` và desktop bootstrap; chưa có API/session services hoặc DI composition root. `App.InitializeLogging()` đang được desktop entry point gọi trước Uno host; phải giữ logging startup hoạt động khi hợp nhất với DI.

Theo quyết định của chủ dự án, `docs/MVVM_REPOSITORY_BASE_PROJECT.md` có ưu tiên cao hơn các hướng dẫn frontend Feature-First cũ. Giữ .NET 10, Uno SDK 6.7.30, Windows/desktop targets và CPM. Backend/shared chỉ là nguồn contract đọc tham khảo: `GET /api/products` trả `PagedResultDto<ProductSummaryDto>`, không phải mảng; endpoint auth/refresh chưa có implementation ở nhánh này. Không suy diễn snippets minh họa thành contract thật.

## Goals / Non-Goals

**Goals:** Foundation thuần C# có thể dùng từ các repository về sau, kiểm thử bằng HTTP handler giả; một nơi sở hữu cấu hình, dependencies và phiên; giữ startup hiện có chạy offline. Specs `frontend-runtime` và `frontend-api-access` là acceptance contract.

**Non-Goals:** Không tạo ViewModel/repository rỗng để lấp cấu trúc; không sửa backend/shared, triển khai auth thật, refresh protocol, secure persistence, Catalog hoặc Shell. Không đổi XAML/design; Windows Community Toolkit controls sẽ được chọn ở Change 2.

## Decisions

### 1. Ranh giới MVVM + Repository theo tài liệu đã chốt

Quy ước root `Views/`, `ViewModels/`, `Models/`, `Repositories/`, `Services/`; có thể dùng `.gitkeep` cho thư mục chưa có production component, không tạo class placeholder. Di chuyển `MainPage.xaml/.xaml.cs` hiện có vào `Views/`, cập nhật namespace/navigation reference, giữ layout và hành vi. Root vẫn chứa `App` và `ServiceCollectionExtensions`.

```text
View -> ViewModel -> IRepository -> Repository -> IApiClient -> REST API
                       Model        shared DTO

Services/Api/
  IApiClient.cs, ApiClient.cs, ApiClientOptions.cs
  ApiException.cs, ApiProblemDetails.cs, BearerTokenHandler.cs
Services/Session/
  ISessionStore.cs, InMemorySessionStore.cs, SessionSnapshot.cs
```

Interface nằm cạnh implementation. ViewModel chỉ nhận repository/interface nền cần thiết qua constructor, không có UI control, HttpClient hoặc service locator. Code-behind được phép wiring/lifecycle bắt buộc của framework, không có nghiệp vụ. Chưa tạo repository trong change này; Catalog sẽ tạo interface/implementation cụ thể ở Change 4.

**Lý do/alternative:** Feature-First trái quyết định của chủ dự án. Generic CRUD/UoW hoặc thêm API client theo domain chỉ chuyển tiếp sẽ làm mờ ranh giới DTO→Model mà không giải quyết nhu cầu hiện tại.

### 2. Composition root trực tiếp, lifetime minh bạch

`ServiceCollectionExtensions.AddFrontendFoundation(...)` đăng ký services thuần C#. `App` gọi một lần, tạo một provider với `ValidateOnBuild`/`ValidateScopes` trong development, sở hữu/dispose provider khi cửa sổ đóng. Không dùng `Ioc.Default`, static service provider hoặc resolve services trong ViewModel. Wiring view hiện tại ở startup; chưa cần VM cho màn hình template.

| Dependency | Lifetime / ownership |
| --- | --- |
| Options đã validate, session store | Singleton trong một lần chạy |
| `IApiClient` typed client | Transient từ `AddHttpClient` |
| `BearerTokenHandler` | Transient; chỉ nhận singleton session, không capture request scope |
| Repository, feature ViewModel về sau | Transient; không giữ typed client trong singleton Shell |
| Logging factory/providers | Một owner, cleanup idempotent khi shutdown |

Giữ entry point desktop gọi logging trước Uno host. Hợp nhất bootstrap factory và factory dùng qua `ILogger<T>`/Uno ambient logger, không tạo hai pipeline khác nhau. Nếu factory tạo ngoài provider, App phải dispose nó rõ ràng; tránh double dispose. Log chỉ operation/method, loại lỗi, status, traceId; không URL query, headers hoặc body. Tắt/filter logging HTTP mặc định có thể in URI/header nhạy cảm; test capture log bằng sentinel secrets.

**Lý do/alternative:** `ServiceCollection` đủ cho desktop hiện không có hosted services; Generic Host thêm lifecycle không cần thiết. Singleton typed client có thể giữ handler/connection lâu và gây captive dependency; không chọn.

### 3. API options non-secret và kiểm tra sớm

`ApiClientOptions` cung cấp base URL mặc định development `http://localhost:5176/`, timeout mặc định 30 giây. Cho override qua `TECHSTORE_API_BASE_URL`, `TECHSTORE_API_TIMEOUT_SECONDS`; bind bằng configuration/options của Microsoft.Extensions, validate trước khi tạo window. Document HTTPS local `https://localhost:7207/` và yêu cầu trusted development certificate. Không hardcode token hoặc tắt certificate validation. Đây là cấu hình development; cấu hình môi trường phân phối phải được cung cấp rõ ràng, không tự đổi sang HTTP khi HTTPS lỗi.

Base URL phải là URI tuyệt đối HTTP/HTTPS, không user-info/query/fragment; normalize slash cuối. Timeout phải dương và nằm trong giới hạn `HttpClient.Timeout`. Không log nguyên giá trị cấu hình lỗi vì có thể chứa credentials. Diagnostic nêu tên option sai và nguyên nhân an toàn. Desktop bootstrap/startup phải báo lỗi cấu hình rõ, không mở window nửa khởi tạo.

**Lý do/alternative:** Defaults + environment overrides đáp ứng local development và headless tests mà không thêm loader asset cấu hình riêng cho từng platform. Secret/token nằm ở session abstraction, không ở options/config.

### 4. HTTP transport nhỏ, không mang nghiệp vụ

Public contract tối thiểu: `GetAsync<TResponse>(relativePath, cancellationToken)`, `PostAsync<TRequest,TResponse>(...)`, `PostAsync<TRequest>(...)` không cần response body. Chưa thêm PUT/DELETE/generic CRUD đến khi feature có nhu cầu. Overload không body trả `Task`; typed overload bắt buộc body khác null kể cả status 204. Cả 200/201 và các status 2xx được xử lý theo expectation của overload.

Typed `ApiClient` dùng `IHttpClientFactory` và handler pipeline. Dùng request riêng, serialize JSON web defaults tương thích ASP.NET; field thêm được bỏ qua, `JsonElement` giữ specs động. Query thuộc caller/repository, transport không tự sửa tên field/backend contract. Reject absolute/network-path URI và mọi resolved URI khác configured origin. Disable automatic redirects/cookies trên primary handler để bearer không bị gửi ngoài ranh giới và session không phụ thuộc cookie. Không thêm retry, nhất là POST.

Dispose request/response/content ở cả success/error paths. CancellationToken đi qua SendAsync, đọc/deserialize body; cancellation do caller giữ `OperationCanceledException`, timeout riêng và network riêng. Dùng timeout cho toàn operation gồm đọc body, không chỉ chờ headers; lựa chọn completion mode/cancellation scope phải được test bằng body bị chặn. Không bắt tất cả exceptions rồi đổi thành dữ liệu rỗng.

**Lý do/alternative:** Repository cụ thể sẽ ánh xạ DTO→Model. Transport không thể phát hiện mọi field domain bị thiếu vì DTO hiện có default values; chỉ đảm bảo cú pháp, null/body và kiểu JSON. Schema/semantic validation bắt buộc của từng DTO thuộc repository về sau, không hứa validate toàn bộ schema trong generic transport.

### 5. Một hợp đồng lỗi cho repository/ViewModel

`ApiException` chứa error kind (`Http`, `Network`, `Timeout`, `InvalidResponse`), nullable HTTP status, title/detail/code/traceId nếu nhận diện, và field errors immutable. 401/403 là Http errors phân biệt bằng status; không cần exception class cho từng mã. `ApiProblemDetails` đọc shape RFC 7807 cùng `errors` validation và extension code/traceId khi hợp lệ. HTTP status thực tế luôn có ưu tiên hơn `status` trong payload.

Non-success có JSON nhận diện thì giữ thông tin cấu trúc cho consumer; malformed/HTML/empty/unrecognized JSON dùng thông báo fallback an toàn theo status, không expose body thô trong exception message. Exception message/log không chứa raw body, headers hoặc stacktrace server; detail/validation dùng cho UI sau này cũng không được tự ghi log. Không coi 404 là null hay 409 là retry; repository quyết định nghiệp vụ về sau. Lỗi decode success khác lỗi HTTP decode error: body lỗi không hợp lệ không được che mất HTTP status.

**Lý do/alternative:** Trả default DTO hoặc danh sách rỗng sẽ khiến UI hiển thị thành công giả. Giữ HttpRequestException thuần không đủ field validation/ProblemDetails cho ViewModel.

### 6. Phiên trong bộ nhớ, chống 401 của phiên cũ

`ISessionStore` cung cấp snapshot immutable gồm access token, generation và trạng thái `Anonymous`/`Active`/`Expired`; các thao tác replace/clear tăng generation và thread-safe. Event `SessionChanged` chỉ đưa trạng thái/generation, không đưa token; consumer không giữ subscription sau dispose. Không thêm refresh token, role giả hoặc login endpoint. Restart bắt đầu Anonymous.

Handler đọc snapshot ngay trước SendAsync và gắn header trên request đó; không dùng `DefaultRequestHeaders`, không cache token trong handler dù handler được factory reuse. Không cho caller ghi đè Authorization bằng token khác. Khi response 401 của request có token, gọi `TryExpire(expectedGeneration)` atomically: chỉ nếu còn đúng phiên thì clear token, chuyển Expired, phát một notification. 401 cũ vẫn là lỗi cho caller nhưng không thay đổi phiên mới; anonymous 401 không expire một phiên vừa tạo. 403 không mutate session. Không có automatic login/refresh/retry.

Clear chỉ bảo đảm request gửi sau clear không dùng token cũ; request đã gửi không thể thu hồi. Không cache dữ liệu hoặc tự cancel mọi feature operation trong change này. Các consumer tương lai phải quản lý kết quả in-flight khi chuyển phiên để tránh hiển thị dữ liệu phiên trước.

**Lý do/alternative:** Persist token plaintext không phù hợp; secure storage đa platform cần change có auth thật. Clear vô điều kiện trên mọi 401 có thể đăng xuất token B bởi response muộn của token A. Generation giải quyết cả khi token string được dùng lại.

### 7. Headless tests dùng cùng production source

Thêm `tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` target `net10.0`, project reference shared; link các source thuần C# API/session/options/registration production, không copy và không tham chiếu Uno multi-target executable. Dùng xUnit và tooling theo conventions hiện có. Test project ngoài frontend giữ explicit package references; frontend package versions thêm ở `frontend/Directory.Packages.props`, không duplicate package đã implicit qua UnoFeatures Mvvm.

Fake `HttpMessageHandler` kiểm tra request/headers/body, trả payload, chặn response/body bằng async primitives, capture cancellation/disposal. Compose DI bằng production registration và fake primary handler; test isolation, service resolution/lifetimes và validate invalid options. Timeout tests phải bounded, không sleep dài; stale401 dùng controlled completion để có thứ tự xác định. Không thêm class production chỉ để test MVVM Toolkit; chỉ dùng `ObservableObject`/generators khi thực tế có bindable state/commands.

**Lý do/alternative:** Tham chiếu executable Uno làm test phụ thuộc platform/UI. Tách project core mới là lựa chọn về sau khi code tăng; source linking đủ cho foundation nhỏ, giữ một bản source với nullable/constants/dependency versions đồng nhất.

### 8. Skills và bằng chứng nghiệm thu

- `uno-platform`: giữ targets/SDK/CPM, startup và runtime hai nền.
- `mvvm-toolkit-di`: constructor injection, đăng ký/lifetimes, test seams; không áp dụng Generic Host hoặc service locator một cách máy móc.
- `mvvm-toolkit`: chỉ dùng khi có observable state thực tế, không sinh VM rỗng.
- `openspec-apply-change`: chỉ ở lượt implementation riêng; `winui-design` và Community Toolkit controls ở Change 2 khi thay XAML.

Ghi command, exit/result, environment và observation vào `handoff.md` khi apply. Build không thay thế mở app. Desktop startup cần quan sát trực tiếp cửa sổ hiện có, responsive và không có startup error khi backend dừng. Windows build/startup kiểm tra trên Windows; nếu không có môi trường thì ghi unverified, để task tương ứng unchecked. Owner acceptance riêng phải có xác nhận explicit, ngày và tested build.

Nguồn kỹ thuật: [Microsoft — IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory), [Microsoft — Dependency injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection), [Uno — Using Uno SDK](https://platform.uno/docs/articles/features/using-the-uno-sdk.html). Phiên bản package được chọn tương thích .NET 10/Uno hiện có, không tự nâng SDK theo tài liệu mới.

## Risks / Trade-offs

- [Source linking có thể lệch compile settings/dependencies] → Dùng source production, match nullable/constants/versions; build app và tests đều bắt buộc. Không claim test headless thay UI acceptance.
- [Native Windows và Skia khác lifecycle] → Kiểm tra startup/shutdown trên từng target, giữ task unverified nếu thiếu runtime.
- [In-memory token mất sau restart] → Hành vi deliberate cho foundation; persistence/auth contract nằm ngoài scope.
- [Default HTTP chỉ phù hợp local development] → Document environment overrides/HTTPS, không fallback khi HTTPS fail, không dùng token thật trong logs/tests.
- [Generic DTO decode không phát hiện domain fields omitted] → Repository validate schema/semantics ở feature change; tests ở đây không hứa ngoài khả năng generic transport.
- [Request cũ còn hoàn tất sau clear] → Generation guard cho expire; feature/session UI tương lai phải bỏ kết quả cũ, không invent global caching trong foundation.

## Migration Plan

Apply lần lượt nền thư mục/DI/options, transport, session handler và test project; giữ màn hình startup hiện có. Không có database/backend migration. Chạy checks trong `tasks.md`, record bằng chứng và review dependency ownership trước handoff. Rollback bằng revert các file implementation và solution/package registrations của change này; không đổi shared contracts hoặc dữ liệu.
