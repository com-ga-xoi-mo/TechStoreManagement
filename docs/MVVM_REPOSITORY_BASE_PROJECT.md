# Base project TechStore: MVVM kết hợp Repository

Ngày nghiên cứu: **08/10/2026**. Phạm vi: nền tảng frontend cho Dev6, Sprint 1; mở rộng được sang các màn hình nghiệp vụ ở Sprint 2.

Đây là **đề xuất kiến trúc bổ sung theo yêu cầu của thành viên Dev6**, không phải bản chép lại phân công của leader. Tài liệu trình bày cấu trúc base project và mẫu triển khai; chưa triển khai hoặc thay đổi runtime của ứng dụng.

Kế hoạch áp dụng kiến trúc này để chuyển thiết kế trong `UI/` thành code, chia nhỏ theo từng màn hình: [UI_IMPLEMENTATION_PLAN.md](UI_IMPLEMENTATION_PLAN.md).

## 1. Kết luận kiến trúc

Đề xuất sử dụng **MVVM với Views/ViewModels riêng + Repository theo nghiệp vụ + Dependency Injection**, với Repository phía client lấy dữ liệu qua HTTP API.

```text
View (XAML)
  ↕ Binding / Command
ViewModel (trạng thái màn hình)
  ↓ constructor injection
IProductRepository (hợp đồng lấy Model)
  ↑ được hiện thực bởi
ProductRepository (truy vấn, chuyển DTO thành Model)
  ↓
ApiClient (HTTP, JSON, chuẩn hóa lỗi)
  ↓ Bearer token qua handler
ASP.NET Core API → Domain Service → EF Core → PostgreSQL
```

Repository bổ sung một ranh giới giữa ViewModel và nguồn dữ liệu. ViewModel yêu cầu sản phẩm hoặc thống kê bằng ngôn ngữ nghiệp vụ, không cần biết URL, JSON, mã HTTP hoặc cách lưu cache. Phần backend tiếp tục chịu trách nhiệm xác thực, tính tiền và tính nhất quán dữ liệu.

Đây là lựa chọn phù hợp với TechStore, **không phải một “chuẩn duy nhất” bắt buộc cho mọi ứng dụng MVVM**. Các repo được kiểm chứng bên dưới sử dụng những biến thể khác nhau.

## 2. Hiểu đúng Model và Repository trong MVVM

Model là dữ liệu và hành vi nghiệp vụ không phụ thuộc giao diện. Repository là thành phần cung cấp/lưu Model và che giấu nguồn dữ liệu. Không cần viết một class `Model` chứa Repository rồi cho ViewModel gọi xuyên qua class đó. Microsoft mô tả Model có thể là DTO/POCO và thường được dùng cùng service hoặc repository để đóng gói truy cập dữ liệu/cache. [Nguồn: Microsoft — MVVM, mục Model](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm#model).

Trong đề xuất này:

| Thành phần              | Ví dụ                                         | Trách nhiệm                                                                |
| ------------------------- | ----------------------------------------------- | ---------------------------------------------------------------------------- |
| Entity backend            | `Product` trong `backend/Catalog/Entities/` | Persistence và nghiệp vụ phía server; không đưa EF entity vào client |
| DTO                       | `ProductSummaryDto` trong `shared/DTOs/`    | Hình dạng dữ liệu trao đổi qua API                                     |
| Model client              | `ProductSummary`                              | Dữ liệu độc lập giao diện mà Repository trả cho ViewModel            |
| Repository contract       | `IProductRepository`                          | Mô tả dữ liệu cần lấy và tham số truy vấn                           |
| Repository implementation | `ProductRepository`                           | Chọn nguồn HTTP, xây query, ánh xạ DTO → Model                         |
| ViewModel                 | `ProductListViewModel`                        | Danh sách hiển thị, tìm kiếm, loading/error/empty, command              |
| View                      | `ProductListPage.xaml`                        | Layout, binding và tương tác người dùng                               |

**Quyết định cho base project:** dùng Model client riêng ở các ranh giới cần ổn định trước thay đổi API. Với DTO đơn giản đã phù hợp hoàn toàn, có thể dùng trực tiếp làm Model MVVM; không bắt buộc tạo thêm lớp chỉ để đổi tên. Trường đang chỉnh sửa, lựa chọn dòng và lỗi validation thuộc ViewModel, không đưa vào DTO dùng chung.

### Code mẫu cho từng thành phần

Cùng lấy ví dụ “màn hình xem chi tiết một sản phẩm”. Các đoạn dưới cho thấy mỗi thành phần giữ trách nhiệm nào. `TechStore.Api` là namespace backend minh họa; các đoạn là ví dụ kiến trúc, chưa được thêm vào source code ứng dụng.

**1. Entity backend — dữ liệu lưu ở server.** Entity dùng bởi EF Core; client không nhận trực tiếp object này qua mạng.

```csharp
// backend/Catalog/Entities/Product.cs
namespace TechStore.Api.Catalog.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
}
```

**2. DTO — hình dạng dữ liệu API gửi cho client.** API map entity thành response DTO; JSON response chính là dữ liệu được serialize từ DTO này.

```csharp
// shared/DTOs/ProductDto.cs
namespace TechStore.Shared.DTOs;

public sealed record ProductDto(Guid Id, string Name, decimal SellingPrice);
```

Ví dụ JSON backend trả về:

```json
{
  "id": "d2719e34-8c5c-4a91-9fcb-e844747dbb31",
  "name": "Laptop TechStore X",
  "sellingPrice": 25000000
}
```

**3. Model client — dữ liệu ứng dụng muốn sử dụng.** Model không gọi API, không phụ thuộc XAML. Trong ví dụ này tên Model khác DTO để chỉ rõ bước ánh xạ; nếu dự án không cần ranh giới riêng thì có thể dùng DTO làm Model.

```csharp
// frontend/TechStore.Client/Models/Catalog/ProductSummary.cs
namespace TechStore.Client.Models.Catalog;

public sealed record ProductSummary(Guid Id, string Name, decimal Price);
```

**4. API client — gửi HTTP và nhận DTO.** Nó biết route và kiểu JSON từ API.

```csharp
// frontend/TechStore.Client/Services/Api/IProductApiClient.cs
using TechStore.Shared.DTOs;

namespace TechStore.Client.Services.Api;

public interface IProductApiClient
{
    Task<ProductDto?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default);
}
```

```csharp
// Phần chính của ProductApiClient
using System.Net;
using System.Net.Http.Json;
using TechStore.Shared.DTOs;

public async Task<ProductDto?> GetByIdAsync(
    Guid id, CancellationToken cancellationToken = default)
{
    using var response = await _httpClient.GetAsync(
        $"api/products/{id}", cancellationToken);
    if (response.StatusCode == HttpStatusCode.NotFound)
        return null;
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<ProductDto>(
        cancellationToken: cancellationToken);
}
```

`_httpClient` được truyền vào `ProductApiClient` qua constructor bởi DI; không tạo `new HttpClient()` cho từng request.

**5. Repository contract — nói dữ liệu gì ViewModel cần.** Contract không nói giao diện cần gọi URL nào.

```csharp
// frontend/TechStore.Client/Repositories/Catalog/IProductRepository.cs
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public interface IProductRepository
{
    Task<ProductSummary?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default);
}
```

**6. Repository implementation — gọi API client và trả Model.** Đây là nơi nối API client với Model ứng dụng.

```csharp
using TechStore.Client.Services.Api;
// frontend/TechStore.Client/Repositories/Catalog/ProductRepository.cs
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public sealed class ProductRepository(IProductApiClient apiClient)
    : IProductRepository
{
    public async Task<ProductSummary?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await apiClient.GetByIdAsync(id, cancellationToken);
        return dto is null
            ? null
            : new ProductSummary(dto.Id, dto.Name, dto.SellingPrice);
    }
}
```

**7. ViewModel — yêu cầu Model và chuẩn bị state cho màn hình.** Nó phụ thuộc vào interface `IProductRepository`, không biết `HttpClient` hay URL API.

```csharp
// frontend/TechStore.Client/ViewModels/Catalog/ProductDetailViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Repositories.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

public partial class ProductDetailViewModel(IProductRepository products)
    : ObservableObject
{
    [ObservableProperty]
    private string _name = "Đang chờ tải";

    [ObservableProperty]
    private string _price = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var product = await products.GetByIdAsync(Guid.Parse(
            "d2719e34-8c5c-4a91-9fcb-e844747dbb31"));
        if (product is null)
        {
            Name = "Không tìm thấy sản phẩm";
            Price = string.Empty;
            return;
        }

        Name = product.Name;
        Price = $"{product.Price:N0} ₫";
    }
}
```

ID cố định chỉ để ví dụ ngắn gọn; màn hình thật nhận ID được chọn qua navigation. Phần xử lý loading/lỗi đầy đủ được minh họa ở các mục code xuyên suốt bên dưới.

**8. View — binding state và command từ ViewModel.** View không tự gọi backend.

```xml
<!-- ProductDetailPage.xaml: fragment minh họa -->
<StackPanel Spacing="8">
    <TextBlock Text="{x:Bind ViewModel.Name, Mode=OneWay}" />
    <TextBlock Text="{x:Bind ViewModel.Price, Mode=OneWay}" />
    <Button Content="Tải sản phẩm"
            Command="{x:Bind ViewModel.LoadCommand}" />
</StackPanel>
```

**9. DI — ứng dụng ghép các phần với nhau.** Đăng ký implementation để khi ViewModel yêu cầu interface, DI biết cần đưa object nào vào.

```csharp
services.AddHttpClient<IProductApiClient, ProductApiClient>(client =>
{
    client.BaseAddress = apiBaseUri;
});
services.AddTransient<IProductRepository, ProductRepository>();
services.AddTransient<ProductDetailViewModel>();
```

Đọc luồng từ đầu đến cuối:

```text
Backend Entity → API response/DTO → ProductApiClient
    → ProductRepository ánh xạ thành ProductSummary Model
    → ProductDetailViewModel cập nhật Name/Price
    → Binding hiển thị lên View
```

Trong sơ đồ này **Model là dữ liệu mà ứng dụng sử dụng**. Repository là thành phần lấy Model; API client là thành phần giao tiếp HTTP. Khi ViewModel cần sản phẩm, nó gọi Repository, Repository gọi API client, và dữ liệu trở về theo chiều ngược lại.

## 3. Ví dụ chương trình MVVM cơ bản (chưa dùng Repository)

Ví dụ nhỏ: trang hiển thị một sản phẩm và số lượng; người dùng bấm nút để tăng số lượng. Nó minh họa ba phần MVVM mà chưa cần backend, API hay Repository.

```text
Models/Demo/Product.cs              # Model: dữ liệu sản phẩm
ViewModels/Demo/ProductDemoViewModel.cs  # ViewModel: state và command
MainPage.xaml                   # View: giao diện và binding
MainPage.xaml.cs                # Nối View với ViewModel trong ví dụ tối giản
```

### Model — dữ liệu nghiệp vụ

```csharp
// Models/Demo/Product.cs
namespace TechStore.Client.Models.Demo;

public sealed record Product(string Name, decimal Price);
```

`Product` mô tả tên và giá sản phẩm. Nó không biết XAML và không tự kết nối backend.

### ViewModel — state và hành động của màn hình

```csharp
using TechStore.Client.Models.Demo;
// ViewModels/Demo/ProductDemoViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TechStore.Client.ViewModels.Demo;

public partial class ProductDemoViewModel : ObservableObject
{
    public Product Product { get; } = new("Bàn phím cơ", 1_500_000m);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPrice))]
    private int _quantity = 1;

    public decimal TotalPrice => Product.Price * Quantity;

    [RelayCommand]
    private void IncreaseQuantity() => Quantity++;
}
```

`Product` là Model được ViewModel sử dụng. `Quantity` là state màn hình; command thay đổi số lượng, còn `TotalPrice` được tính từ giá trong Model và số lượng. Toolkit tự sinh property thông báo thay đổi cùng `IncreaseQuantityCommand`.

### View — hiển thị và binding

```xml
<!-- MainPage.xaml: chỉ là ví dụ tối giản -->
<Page x:Class="TechStore.Client.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel Padding="24" Spacing="12">
        <TextBlock Text="{x:Bind ViewModel.Product.Name, Mode=OneWay}"
                   FontSize="24" />
        <TextBlock Text="{x:Bind ViewModel.Quantity, Mode=OneWay}" />
        <TextBlock Text="{x:Bind ViewModel.TotalPrice, Mode=OneWay}" />
        <Button Content="Tăng số lượng"
                Command="{x:Bind ViewModel.IncreaseQuantityCommand}" />
    </StackPanel>
</Page>
```

```csharp
// MainPage.xaml.cs: cách nối trực tiếp đơn giản để minh họa
using TechStore.Client.ViewModels.Demo;

namespace TechStore.Client;

public sealed partial class MainPage : Page
{
    public ProductDemoViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
    }
}
```

Khi bấm nút: `Button → IncreaseQuantityCommand → Quantity thay đổi → thông báo PropertyChanged → binding cập nhật số lượng và tổng giá`. XAML không tự gọi Model hay backend; nó chỉ hiển thị state và phát command tới ViewModel.

Code-behind ở ví dụ này chỉ tạo ViewModel để minh họa việc gắn View với ViewModel. Khi ViewModel cần dependency (ví dụ Repository), ứng dụng nên dùng DI/page factory để tạo và gắn nó; không `new` repository trong ViewModel. Đây là ví dụ giải thích, không phải thay đổi code chạy của ứng dụng.

## 4. Ví dụ cơ bản: MVVM có Repository lấy dữ liệu từ API

Đây là phiên bản mở rộng của ví dụ ở mục 3: trang hiển thị tên sản phẩm và giá do backend trả về. Để làm rõ từng vai trò, ví dụ dùng `ProductApiClient` gọi HTTP, `ProductRepository` chuyển DTO thành Model, rồi ViewModel đưa Model ra binding.

```text
Models/Demo/Product.cs
ViewModels/Demo/ProductDemoViewModel.cs
MainPage.xaml(.cs)
Services/Api/IProductApiClient.cs
Repositories/Catalog/IProductRepository.cs
Repositories/Catalog/ProductRepository.cs
Services/Api/ProductApiClient.cs
shared/DTOs/ProductDto.cs
App.xaml.cs                         # Đăng ký và ghép các dependency
```

URL `api/products/demo` và các trường JSON bên dưới là contract **minh họa**. Khi áp dụng vào TechStore, phải dùng route/DTO backend đã thống nhất.

### API response và client

Backend trả JSON, ví dụ:

```json
{
  "id": "d2719e34-8c5c-4a91-9fcb-e844747dbb31",
  "name": "Bàn phím cơ",
  "price": 1500000
}
```

```csharp
// shared/DTOs/ProductDto.cs — hình dạng response HTTP
namespace TechStore.Shared.DTOs;

public sealed record ProductDto(Guid Id, string Name, decimal Price);
```

```csharp
// Services/Api/IProductApiClient.cs — giao tiếp HTTP có thể thay thế khi test
using TechStore.Shared.DTOs;

namespace TechStore.Client.Services.Api;

public interface IProductApiClient
{
    Task<ProductDto> GetDemoProductAsync(
        CancellationToken cancellationToken = default);
}
```

```csharp
// Services/Api/ProductApiClient.cs — gửi request và đọc JSON thành DTO
using System.Net.Http.Json;

using TechStore.Shared.DTOs;

namespace TechStore.Client.Services.Api;

public sealed class ProductApiClient(HttpClient httpClient) : IProductApiClient
{
    public async Task<ProductDto> GetDemoProductAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/products/demo", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProductDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "Backend trả về response sản phẩm rỗng.");
    }
}
```

API client chỉ xử lý HTTP và deserialize response. `EnsureSuccessStatusCode` giúp ví dụ cơ bản không coi response lỗi như dữ liệu thành công; ứng dụng thật nên chuyển status/ProblemDetails thành lỗi thân thiện như mục 8.

### Repository và Model

```csharp
// Models/Demo/Product.cs — Model ứng dụng dùng
namespace TechStore.Client.Models.Demo;

public sealed record Product(Guid Id, string Name, decimal Price);
```

```csharp
// Repositories/Catalog/IProductRepository.cs — dữ liệu sản phẩm mà ViewModel yêu cầu
using TechStore.Client.Models.Demo;

namespace TechStore.Client.Repositories.Catalog;

public interface IProductRepository
{
    Task<Product> GetDemoProductAsync(
        CancellationToken cancellationToken = default);
}
```

```csharp
using TechStore.Client.Services.Api;
// Repositories/Catalog/ProductRepository.cs — dùng API client, đổi DTO thành Model
using TechStore.Client.Models.Demo;


namespace TechStore.Client.Repositories.Catalog;

public sealed class ProductRepository(IProductApiClient apiClient)
    : IProductRepository
{
    public async Task<Product> GetDemoProductAsync(
        CancellationToken cancellationToken = default)
    {
        var dto = await apiClient.GetDemoProductAsync(cancellationToken);
        return new Product(dto.Id, dto.Name, dto.Price);
    }
}
```

Repository gọi API client để lấy DTO rồi ánh xạ thành Model. Việc tách hai kiểu ở đây nhằm làm rõ luồng; trong ứng dụng thực tế, có thể cho Repository trả DTO trực tiếp nếu Model riêng không đem lại lợi ích.

### ViewModel và View

```csharp
using TechStore.Client.Models.Demo;
// ViewModels/Demo/ProductDemoViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Repositories.Catalog;

namespace TechStore.Client.ViewModels.Demo;

public partial class ProductDemoViewModel(IProductRepository repository)
    : ObservableObject
{
    [ObservableProperty]
    private string _productName = "Chưa tải sản phẩm";

    [ObservableProperty]
    private string _priceText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    private async Task LoadProductAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var product = await repository.GetDemoProductAsync(cancellationToken);
            ProductName = product.Name;
            PriceText = $"{product.Price:N0} ₫";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Người dùng đã hủy thao tác.
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Không kết nối được API. Hãy thử tải lại.";
        }
        catch (Exception)
        {
            ErrorMessage = "Không tải được sản phẩm.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

```xml
<!-- MainPage.xaml: View chỉ binding dữ liệu và command -->
<Page x:Class="TechStore.Client.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel Padding="24" Spacing="12">
        <TextBlock Text="{x:Bind ViewModel.ProductName, Mode=OneWay}"
                   FontSize="24" />
        <TextBlock Text="{x:Bind ViewModel.PriceText, Mode=OneWay}" />
        <ProgressRing IsActive="{x:Bind ViewModel.IsLoading, Mode=OneWay}" />
        <TextBlock Text="{x:Bind ViewModel.ErrorMessage, Mode=OneWay}" />
        <Button Content="Tải sản phẩm"
                Command="{x:Bind ViewModel.LoadProductCommand}" />
    </StackPanel>
</Page>
```

```csharp
// MainPage.xaml.cs: Page factory/DI truyền ViewModel vào constructor
using TechStore.Client.ViewModels.Demo;

namespace TechStore.Client;

public sealed partial class MainPage : Page
{
    public ProductDemoViewModel ViewModel { get; }

    public MainPage(ProductDemoViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
```

Constructor trên minh họa dependency cần có; để `Frame` của Uno tạo Page theo constructor này, composition root phải đăng ký page factory/navigator để resolve Page từ DI. Không thể mặc định rằng `Frame.Navigate(typeof(MainPage))` tự truyền tham số.

Khi người dùng bấm nút: `View → command của ViewModel → IProductRepository → ProductRepository → IProductApiClient → HTTP GET backend`. Response quay về theo chiều ngược lại: `JSON → ProductDto → Product Model → ViewModel properties → binding cập nhật View`.

Ứng dụng phải ghép các interface với implementation một lần ở composition root qua DI. Ví dụ đăng ký phần data:

```csharp
services.AddHttpClient<IProductApiClient, ProductApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5176/"); // Chỉ để chạy local
});
services.AddTransient<IProductRepository, ProductRepository>();
services.AddTransient<ProductDemoViewModel>();
```

`MainPage` cần nhận `ProductDemoViewModel` được tạo từ DI/page factory của ứng dụng; không tự gọi API và không tự tạo Repository. Đoạn đăng ký `HttpClient` chỉ minh họa môi trường local; cấu hình thật lấy từ settings theo môi trường và gắn handler JWT. Cơ chế page factory của Uno phụ thuộc cách Shell khởi tạo Page, vì vậy mẫu này tập trung vào luồng kiến trúc, không thay thế cấu hình khởi động hoàn chỉnh.

**So với ví dụ mục 3:** trước đó, ViewModel dùng Model khởi tạo sẵn. Ở đây Model được tạo từ response backend; API client lo HTTP, Repository cung cấp Model, ViewModel điều khiển trạng thái hiển thị. Model vẫn là dữ liệu, không phải thành phần tự kết nối backend.

## 5. Nghiên cứu cách đặt tên trong các ứng dụng lớn

### 5.1. Phạm vi và cách kiểm chứng

Cập nhật nghiên cứu ngày **08/10/2026** theo yêu cầu chỉ dựa vào dự án lớn. Chọn ứng dụng đã phát hành cho người dùng, có lịch sử phát triển cộng đồng hoặc doanh nghiệp, và mã nguồn đủ để đối chiếu View, ViewModel cùng tầng dữ liệu. Số GitHub stars chỉ là dấu hiệu mức độ quan tâm, **không phải số người dùng hoặc thước đo chất lượng kiến trúc**.

| Ứng dụng                                                   | Quy mô tham khảo khi tra cứu                                                        | Phạm vi mã nguồn được đọc                         | Commit cố định                            |
| ------------------------------------------------------------ | -------------------------------------------------------------------------------------- | --------------------------------------------------------- | -------------------------------------------- |
| [Microsoft PowerToys](https://github.com/microsoft/PowerToys) | Khoảng 139 nghìn stars; ứng dụng tiện ích Windows của Microsoft                 | Settings UI, C#/XAML; ViewModel và Repository cấu hình | `31030bef356a5438696ae1ef7dda123ee7cfa340` |
| [Files](https://github.com/files-community/Files)             | Khoảng 46 nghìn stars; ứng dụng quản lý file Windows                             | C#/WinUI; View, ViewModel, Contracts, Models và Services | `951a2ca8cabe9e5feb7f52b7158e0e8ab35e9634` |
| [Bitwarden Android](https://github.com/bitwarden/android)     | Khoảng 9,5 nghìn stars; ứng dụng quản lý mật khẩu/Authenticator của Bitwarden | Kotlin/Compose; MVVM, Repository và network services     | `1d0f544a7a5008599f8e9d994c2dfa9a9da53812` |

Đã đọc cây thư mục và các file nguồn liên kết bên dưới tại các commit này; không suy đoán từ tên repo. Cây minh họa là **trích một phần đường dẫn có thật**, không phải toàn bộ kiến trúc của mỗi ứng dụng. Phần nghiên cứu này thay cho các mẫu Contoso/MAUI dùng làm căn cứ trước đó; mẫu template không được tính là bằng chứng về ứng dụng lớn trong lần đối chiếu này.

### 5.2. PowerToys — C#/XAML, có Repository thật

Đường dẫn thực tế trong `src/settings-ui/`:

```text
Settings.UI/
├── ViewModels/
│   └── GeneralViewModel.cs
└── SettingsXAML/Views/
    └── GeneralPage.xaml
Settings.UI.Library/
├── Interfaces/
│   └── ISettingsRepository`1.cs
├── SettingsRepository`1.cs
└── GeneralSettings.cs
```

Dấu backtick và số `1` là một phần tên file trong repo. Tên kiểu C# bên trong là `ISettingsRepository<T>` và `SettingsRepository<T>`.

| Bằng chứng trực tiếp                                                                                                                                                                    | Quan sát                                                                                                          |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| [ISettingsRepository<T></t>](https://github.com/microsoft/PowerToys/blob/31030bef356a5438696ae1ef7dda123ee7cfa340/src/settings-ui/Settings.UI.Library/Interfaces/ISettingsRepository%601.cs) | Interface có tiền tố`I`, đặt trong `Interfaces/`                                                          |
| [SettingsRepository<T></t>](https://github.com/microsoft/PowerToys/blob/31030bef356a5438696ae1ef7dda123ee7cfa340/src/settings-ui/Settings.UI.Library/SettingsRepository%601.cs)              | Implementation bỏ tiền tố`I`; file nằm ở gốc project Library, không có thư mục Repositories bắt buộc |
| [GeneralViewModel](https://github.com/microsoft/PowerToys/blob/31030bef356a5438696ae1ef7dda123ee7cfa340/src/settings-ui/Settings.UI/ViewModels/GeneralViewModel.cs)                          | Constructor nhận`ISettingsRepository<GeneralSettings>`                                                          |
| [GeneralPage.xaml](https://github.com/microsoft/PowerToys/blob/31030bef356a5438696ae1ef7dda123ee7cfa340/src/settings-ui/Settings.UI/SettingsXAML/Views/GeneralPage.xaml)                     | Page binding với ViewModel; View và ViewModel ở hai nhánh khác nhau                                           |
| [GeneralSettings](https://github.com/microsoft/PowerToys/blob/31030bef356a5438696ae1ef7dda123ee7cfa340/src/settings-ui/Settings.UI.Library/GeneralSettings.cs)                               | Dữ liệu cấu hình mang tên nghiệp vụ; không bắt buộc hậu tố`Model`                                    |

Luồng đã kiểm chứng: `GeneralPage → GeneralViewModel → ISettingsRepository<GeneralSettings> → SettingsRepository<GeneralSettings> → SettingsUtils/file cấu hình`.

**Có thể học cách đặt tên:** `I<Tên nghiệp vụ>Repository`, `<Tên nghiệp vụ>Repository`, `<Tên màn hình>ViewModel`, `<Tên màn hình>Page.xaml`. Đây là Repository cấu hình/file, không phải ví dụ Repository gọi REST. PowerToys dùng singleton riêng cho Repository này; không suy ra mọi repository của TechStore cũng phải singleton. [Tài liệu chính thức giải thích SettingsRepository](https://microsoft.github.io/PowerToys/core/settings/viewmodels/).

### 5.3. Files — C#/WinUI, đối chiếu cách đặt tên Data/Contracts

Đường dẫn thực tế trong `src/Files.App/`:

```text
Data/
├── Contracts/
│   └── IGeneralSettingsService.cs
└── Models/
    └── TaggedItemModel.cs
Services/
└── Settings/
    └── GeneralSettingsService.cs
ViewModels/
└── Settings/
    └── GeneralViewModel.cs
Views/
└── Settings/
    └── GeneralPage.xaml
```

| Bằng chứng trực tiếp                                                                                                                                                  | Quan sát                                                                                          |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| [IGeneralSettingsService](https://github.com/files-community/Files/blob/951a2ca8cabe9e5feb7f52b7158e0e8ab35e9634/src/Files.App/Data/Contracts/IGeneralSettingsService.cs)  | `Data/Contracts` chứa interface; tên này có tiền lệ thật trong ứng dụng lớn            |
| [GeneralSettingsService](https://github.com/files-community/Files/blob/951a2ca8cabe9e5feb7f52b7158e0e8ab35e9634/src/Files.App/Services/Settings/GeneralSettingsService.cs) | Implementation nằm trong`Services/Settings`, có hậu tố `Service`                           |
| [GeneralViewModel](https://github.com/files-community/Files/blob/951a2ca8cabe9e5feb7f52b7158e0e8ab35e9634/src/Files.App/ViewModels/Settings/GeneralViewModel.cs)           | Kế thừa`ObservableObject`; dùng `IUserSettingsService` và truy cập GeneralSettingsService |
| [GeneralPage.xaml](https://github.com/files-community/Files/blob/951a2ca8cabe9e5feb7f52b7158e0e8ab35e9634/src/Files.App/Views/Settings/GeneralPage.xaml)                   | View nằm trong`Views/Settings`, binding với GeneralViewModel                                   |
| [TaggedItemModel](https://github.com/files-community/Files/blob/951a2ca8cabe9e5feb7f52b7158e0e8ab35e9634/src/Files.App/Data/Models/TaggedItemModel.cs)                     | Có model ở`Data/Models`, thay vì bắt buộc `Models` tại gốc                              |

**Giới hạn bằng chứng:** Files chứng minh cách đặt tên thư mục MVVM/Contracts/Services. Luồng settings được kiểm tra dùng **Service**, không dùng lớp mang tên Repository. Không gọi Files là bằng chứng cho một `Data/Repositories` mà mã nguồn được đọc không có. ViewModel này resolve service bằng `Ioc.Default`; lựa chọn constructor injection của TechStore là điều chỉnh riêng.

### 5.4. Bitwarden Android — MVVM + Repository + network trong sản phẩm lớn

Đường dẫn thực tế; tiền tố package Kotlin được ghi ở đầu mỗi cây:

```text
app/src/main/kotlin/com/x8bit/bitwarden/
├── data/auth/repository/
│   ├── AuthRepository.kt
│   ├── AuthRepositoryImpl.kt
│   └── model/...
└── ui/auth/feature/login/
    ├── LoginScreen.kt
    └── LoginViewModel.kt

network/src/main/kotlin/com/bitwarden/network/
├── api/
│   └── AuthenticatedAccountsApi.kt
└── service/
    ├── AccountsService.kt
    └── AccountsServiceImpl.kt
```

| Bằng chứng trực tiếp                                                                                                                                                                                                                                                                                                                                                          | Quan sát                                                                                                       |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| [AuthRepository](https://github.com/bitwarden/android/blob/1d0f544a7a5008599f8e9d994c2dfa9a9da53812/app/src/main/kotlin/com/x8bit/bitwarden/data/auth/repository/AuthRepository.kt) và [AuthRepositoryImpl](https://github.com/bitwarden/android/blob/1d0f544a7a5008599f8e9d994c2dfa9a9da53812/app/src/main/kotlin/com/x8bit/bitwarden/data/auth/repository/AuthRepositoryImpl.kt) | Interface không có tiền tố`I`; implementation có hậu tố `Impl`; hai file ở cùng package repository |
| [LoginViewModel](https://github.com/bitwarden/android/blob/1d0f544a7a5008599f8e9d994c2dfa9a9da53812/app/src/main/kotlin/com/x8bit/bitwarden/ui/auth/feature/login/LoginViewModel.kt) và [LoginScreen](https://github.com/bitwarden/android/blob/1d0f544a7a5008599f8e9d994c2dfa9a9da53812/app/src/main/kotlin/com/x8bit/bitwarden/ui/auth/feature/login/LoginScreen.kt)             | Screen và ViewModel cùng thư mục tính năng`login`; ViewModel nhận AuthRepository qua constructor       |
| [AccountsServiceImpl](https://github.com/bitwarden/android/blob/1d0f544a7a5008599f8e9d994c2dfa9a9da53812/network/src/main/kotlin/com/bitwarden/network/service/AccountsServiceImpl.kt)                                                                                                                                                                                             | Network service phụ thuộc`AuthenticatedAccountsApi` và `UnauthenticatedAccountsApi`                      |
| [AuthenticatedAccountsApi](https://github.com/bitwarden/android/blob/1d0f544a7a5008599f8e9d994c2dfa9a9da53812/network/src/main/kotlin/com/bitwarden/network/api/AuthenticatedAccountsApi.kt)                                                                                                                                                                                       | Interface mô tả endpoint HTTP bằng Retrofit                                                                  |

Luồng đã kiểm chứng: `LoginViewModel → AuthRepository → AuthRepositoryImpl → AccountsService → AccountsServiceImpl → AccountsApi/Retrofit` (trong đó hai API được đặt tên phân biệt authenticated/unauthenticated).

Bitwarden [xác nhận dùng MVVM và mô tả quy tắc Repository/Impl](https://contributing.bitwarden.com/architecture/mobile-clients/android/). Đây là bằng chứng cho cách gom theo feature và tách network khỏi Repository. **`Impl` là cách đặt tên của repo Kotlin này**, không phải tên bắt buộc để đưa sang C#. Repo `bitwarden/mobile` MAUI/Xamarin cũ đã retired; nghiên cứu hiện tại dùng `bitwarden/android`, không gắn nhãn C# cho ứng dụng Kotlin.

### 5.5. Những gì rút ra được về tên gọi

| Câu hỏi                                                                             | Kết quả từ ba ứng dụng                                                                                       |
| ------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| Có bắt buộc thư mục`Models`, `Views`, `ViewModels` nằm cạnh nhau không? | Không. PowerToys chia qua project; Files có`Data/Models`; Bitwarden chia `data` và `ui` rồi gom feature |
| `Contracts` có phải tên tự nghĩ ra không?                                     | Không. Files có đúng`Data/Contracts`; PowerToys dùng `Interfaces`                                        |
| Interface Repository đặt tên thế nào?                                            | PowerToys:`ISettingsRepository<T>`; Bitwarden: `AuthRepository`                                               |
| Implementation đặt tên thế nào?                                                  | PowerToys:`SettingsRepository<T>`; Bitwarden: `AuthRepositoryImpl`                                            |
| Có bằng chứng buộc tên phải có`Http` không?                                 | Không trong các lớp được kiểm tra. Các implementation được đặt theo nghiệp vụ                      |
| Lớp mạng có bắt buộc tên`ApiClient` không?                                   | Không. Bitwarden dùng cả`...Api` và `...Service`; vai trò phải được đọc từ mã nguồn             |
| File giao diện có bắt buộc hậu tố`View` không?                               | Không. Hai ứng dụng C#/XAML dùng `...Page.xaml`; Bitwarden dùng `...Screen.kt`                           |

**Đề xuất suy ra cho C# TechStore:** dùng `IProductRepository` / `ProductRepository` khi chỉ có một implementation chính. Chỉ thêm `Http`, `Cached` hoặc `InMemory` nếu cần phân biệt nhiều implementation có trách nhiệm khác nhau. Giữ `ProductListViewModel` và `ProductListPage.xaml`. TechStore chọn đặt interface cạnh implementation trong từng thư mục nghiệp vụ; các repo tham khảo không bắt buộc dự án phải có thư mục `Contracts` hoặc `Interfaces` riêng.

## 6. Cấu trúc base project đề xuất sau khi đối chiếu dự án lớn

### 6.1. Cách tổ chức được chọn

Theo phương án đã thống nhất, **`Models/`, `Views/`, `ViewModels/`, `Repositories/` và `Services/` nằm cùng cấp** trong client. Bỏ thư mục trung gian `Data/` và thư mục gom interface `Contracts/`. Bên trong mỗi nhóm tiếp tục chia theo nghiệp vụ để dễ tìm file.

Interface đặt cạnh implementation tương ứng: `IProductRepository` cạnh `ProductRepository` trong `Repositories/Catalog`; `IApiClient` cạnh `ApiClient` trong `Services/Api`; `INavigationService` cạnh `NavigationService` trong `Services/Navigation`.

Cách tách Views/ViewModels tham khảo Files và PowerToys; đặt interface và implementation gần nhau có tiền lệ ở Bitwarden. Việc chọn tên và cây cụ thể dưới đây là phương án của TechStore. Phần nghiên cứu ở mục 5 vẫn mô tả đúng đường dẫn gốc của các dự án tham khảo.

| Thành phần | Đường dẫn được chọn |
| --- | --- |
| Model client | `Models/Catalog/ProductSummary.cs` |
| View | `Views/Catalog/ProductListPage.xaml` |
| ViewModel | `ViewModels/Catalog/ProductListViewModel.cs` |
| Repository interface | `Repositories/Catalog/IProductRepository.cs` |
| Repository implementation | `Repositories/Catalog/ProductRepository.cs` |
| API client interface và implementation | `Services/Api/IApiClient.cs`, `Services/Api/ApiClient.cs` |
| Đăng ký DI | `App.xaml.cs` và `ServiceCollectionExtensions.cs` tại gốc client |

### 6.2. Cây đề xuất cho TechStore

```text
frontend/TechStore.Client/
├── App.xaml
├── App.xaml.cs                          # Khởi tạo ứng dụng, gọi đăng ký DI
├── ServiceCollectionExtensions.cs       # Đăng ký Repository, Service, ViewModel
│
├── Models/                              # Dữ liệu client, không phụ thuộc UI
│   ├── Catalog/
│   │   └── ProductSummary.cs
│   ├── Customers/
│   │   └── CustomerSummary.cs
│   └── Analytics/
│       └── DashboardSummary.cs
│
├── Repositories/                        # Interface đặt cạnh implementation
│   ├── Catalog/
│   │   ├── IProductRepository.cs
│   │   └── ProductRepository.cs
│   ├── Customers/
│   │   ├── ICustomerRepository.cs
│   │   └── CustomerRepository.cs
│   ├── Orders/
│   │   ├── IOrderRepository.cs
│   │   └── OrderRepository.cs
│   ├── Inventory/
│   │   ├── IInventoryRepository.cs
│   │   └── InventoryRepository.cs
│   └── Analytics/
│       ├── IAnalyticsRepository.cs
│       └── AnalyticsRepository.cs
│
├── Services/
│   ├── Api/                             # HTTP, JSON, token và lỗi phản hồi
│   │   ├── IApiClient.cs
│   │   ├── ApiClient.cs
│   │   ├── ApiException.cs
│   │   ├── ApiProblemDetails.cs
│   │   └── BearerTokenHandler.cs
│   ├── Navigation/
│   │   ├── INavigationService.cs
│   │   └── NavigationService.cs
│   ├── Dialog/
│   │   ├── IDialogService.cs
│   │   └── DialogService.cs
│   └── Session/
│       ├── ITokenStorageService.cs
│       └── TokenStorageService.cs
│
├── Views/                               # XAML và code-behind của Page
│   ├── Shell/
│   │   ├── ShellPage.xaml
│   │   └── ShellPage.xaml.cs
│   ├── Catalog/
│   │   ├── ProductListPage.xaml
│   │   ├── ProductListPage.xaml.cs
│   │   ├── ProductDetailPage.xaml
│   │   └── ProductDetailPage.xaml.cs
│   ├── Inventory/                       # InventoryPage.xaml + .xaml.cs
│   ├── Orders/                          # OrderListPage.xaml + .xaml.cs
│   ├── Customers/                       # CustomerListPage.xaml + .xaml.cs
│   ├── Dashboard/                       # DashboardPage.xaml + .xaml.cs
│   └── Settings/                        # SettingsPage.xaml + .xaml.cs
│
├── ViewModels/                          # State, command; không giữ UI control
│   ├── Shell/
│   │   └── ShellViewModel.cs
│   ├── Catalog/
│   │   ├── ProductListViewModel.cs
│   │   └── ProductDetailViewModel.cs
│   ├── Inventory/
│   │   └── InventoryViewModel.cs
│   ├── Orders/
│   │   └── OrderListViewModel.cs
│   ├── Customers/
│   │   └── CustomerListViewModel.cs
│   ├── Dashboard/
│   │   └── DashboardViewModel.cs
│   └── Settings/
│       └── SettingsViewModel.cs
│
├── Controls/                            # StatCard, SearchBox, EmptyState
├── Converters/
└── Styles/

shared/
├── DTOs/                                # Hợp đồng API giữa client và backend
├── Requests/
└── Enums/

tests/Frontend.UnitTests/
├── ViewModels/                          # Test state và command
├── Repositories/                        # Test mapping DTO → Model
├── Services/Api/                        # Test xử lý HTTP
└── Fakes/
```

Mỗi View có ViewModel tương ứng: `Views/Catalog/ProductListPage.xaml` binding với `ViewModels/Catalog/ProductListViewModel.cs`. Ví dụ nhập môn ở mục 3–4 giữ `MainPage` ở gốc để khớp trang khởi động của template; Model minh họa nằm trong `Models/Demo`.

### 6.3. Namespace và chiều phụ thuộc

| File | Namespace |
| --- | --- |
| `Models/Catalog/ProductSummary.cs` | `TechStore.Client.Models.Catalog` |
| `Repositories/Catalog/IProductRepository.cs` | `TechStore.Client.Repositories.Catalog` |
| `Repositories/Catalog/ProductRepository.cs` | `TechStore.Client.Repositories.Catalog` |
| `Services/Api/IApiClient.cs` và `ApiClient.cs` | `TechStore.Client.Services.Api` |
| `Views/Catalog/ProductListPage.xaml.cs` | `TechStore.Client.Views.Catalog` |
| `ViewModels/Catalog/ProductListViewModel.cs` | `TechStore.Client.ViewModels.Catalog` |

- View → ViewModel → repository interface + Model.
- Repository implementation → API client interface + DTO + Model.
- API client → HttpClient + token storage; không tham chiếu View/ViewModel.
- `App.xaml.cs` và `ServiceCollectionExtensions.cs` ghép implementation với interface.
- Model và repository interface không phụ thuộc WinUI, DbContext hay implementation HTTP.

Interface và implementation ở cùng thư mục/namespace vẫn là hai kiểu riêng biệt. ViewModel tiếp tục nhận `IProductRepository` qua constructor; DI cung cấp `ProductRepository` tương ứng.

Để test headless trong giai đoạn một client project, project test `net10.0` có thể link các file ViewModel thuần C#, Model và interface cần thiết. Khi cần tách assembly, chuyển các thành phần độc lập UI sang class library dùng chung; không sao chép các lớp thành hai bản.

## 7. Quy định ranh giới và cách mở rộng

### 7.1. Repository client khác Repository backend

| Repository client — thuộc đề xuất này                         | Repository backend — tùy nhu cầu module                                   |
| ------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| Lấy Model từ REST, ánh xạ DTO, có thể quản lý cache đọc   | Truy vấn và persistence bằng EF Core                                      |
| Không có DbContext/SQL/connection string PostgreSQL               | Có thể sử dụng DbContext trong scope request                             |
| Không cam kết transaction giữa nhiều HTTP request               | Domain service sở hữu transaction cho nhiều thay đổi                    |
| Không tự quyết định giảm kho, giá cuối hay quyền truy cập | Server xác thực, kiểm tra quyền, tính tiền, khóa dòng và ghi ledger |

Không cần thêm `IRepository<T>` backend chỉ để frontend được gọi là “MVVM + Repository”. Tầng service backend hiện tại có thể tiếp tục dùng DbContext. Nếu bổ sung backend repository, việc commit transaction phải do service điều phối; không tự `SaveChanges` rời rạc ở từng repository trong một nghiệp vụ bán hàng.

### 7.2. Repository và ApiClient có nhiệm vụ khác nhau

- `IProductRepository.SearchAsync(...)`: dữ liệu sản phẩm mà màn hình cần.
- `ApiClient.GetAsync<T>(relativeUri, token)`: thao tác HTTP/JSON dùng chung.
- `BearerTokenHandler`: gắn access token hiện hành cho mỗi request tới API đã cấu hình.
- `AuthService`: đăng nhập, đăng xuất và vòng đời session; không ép mọi service thành Repository.

Trong base này, **ProductRepository chính là lớp truy cập API đặc thù sản phẩm**. Không thêm một `ProductApiClient` chỉ chuyển tiếp y hệt từng phương thức. Nếu sau này dùng client được generate từ OpenAPI, Repository có thể bọc generated client để ánh xạ transport contract sang Model.

Không tạo generic CRUD repository cho tất cả: báo cáo, chuyển trạng thái đơn và nạp IMEI có ngữ nghĩa khác nhau. Chỉ trích xuất cơ chế lặp lại như gửi HTTP và giải mã lỗi.

### 7.3. Khi cần use case

Màn hình danh sách: `ViewModel → Repository` là đủ. Thêm `LoadCheckoutContextUseCase` khi có điều phối nhiều nguồn, được dùng lại và đáng test riêng. Không thêm một service ở giữa nếu nó chỉ chuyển tiếp lời gọi.

Tạo đơn phải gửi một request nghiệp vụ tới server. Không viết client lần lượt gọi “tạo đơn”, “trừ kho”, “đánh dấu IMEI Sold” rồi xem ba lời gọi HTTP đó như một transaction.

## 8. Mẫu xuyên suốt: tải danh sách sản phẩm

Các đoạn dưới là **code mẫu tự viết cho kiến trúc đề xuất**, không sao chép từ repo tham khảo. Tên DTO, route `api/products` và dạng response mảng là contract minh họa, cần đối chiếu API của Dev2 trước khi dùng. Nếu API trả phân trang, thay bằng `PagedResultDto<T>` và trả Model page chứa items/total/page; không tải toàn bộ kho rồi phân trang trên client.

Mỗi đoạn ghi rõ file tương ứng. Chưa đưa các snippet này vào project hoặc kiểm chứng bằng build. `IApiClient` bên dưới là điểm mở rộng có contract rõ; phần HTTP đầy đủ cần triển khai theo mục 9.

### 8.1. DTO và Model

```csharp
// shared/DTOs/ProductSummaryDto.cs — DTO minh họa
namespace TechStore.Shared.DTOs;

public sealed record ProductSummaryDto(
    Guid Id, string Name, string Sku, decimal SellingPrice);
```

```csharp
// Models/Catalog/ProductSummary.cs
namespace TechStore.Client.Models.Catalog;

public sealed record ProductSummary(
    Guid Id, string Name, string Sku, decimal SellingPrice);
```

Ở ví dụ nhỏ hai kiểu có cùng trường để làm rõ ranh giới. Khi triển khai thật, có thể gộp nếu việc ánh xạ không mang lại lợi ích; không thêm property UI vào DTO để tiết kiệm một lớp.

### 8.2. Repository contract và transport contract

```csharp
// Repositories/Catalog/IProductRepository.cs
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public interface IProductRepository
{
    Task<IReadOnlyList<ProductSummary>> SearchAsync(
        string? search, CancellationToken cancellationToken = default);
}
```

```csharp
// Services/Api/IApiClient.cs
namespace TechStore.Client.Services.Api;

public interface IApiClient
{
    // Trả payload hợp lệ; lỗi HTTP/network/JSON được chuẩn hóa thành ApiException.
    // Hủy do caller phải giữ nguyên OperationCanceledException.
    Task<T> GetAsync<T>(
        string relativeUri, CancellationToken cancellationToken = default);
}
```

```csharp
// Services/Api/ApiException.cs
namespace TechStore.Client.Services.Api;

public sealed class ApiException(
    string message, int? statusCode = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;
}
```

### 8.3. Repository implementation

```csharp
// Repositories/Catalog/ProductRepository.cs
using TechStore.Client.Services.Api;
using TechStore.Client.Models.Catalog;
using TechStore.Shared.DTOs;

namespace TechStore.Client.Repositories.Catalog;

public sealed class ProductRepository(IApiClient apiClient)
    : IProductRepository
{
    public async Task<IReadOnlyList<ProductSummary>> SearchAsync(
        string? search, CancellationToken cancellationToken = default)
    {
        var term = Uri.EscapeDataString(search?.Trim() ?? string.Empty);
        var items = await apiClient.GetAsync<ProductSummaryDto[]>(
            $"api/products?search={term}", cancellationToken);

        return items.Select(item => new ProductSummary(
            item.Id, item.Name, item.Sku, item.SellingPrice)).ToArray();
    }
}
```

Repository không trả `ObservableCollection`, không mở dialog, không giữ trạng thái chọn dòng. Lỗi API không bị biến thành danh sách rỗng: “không có sản phẩm” và “không kết nối được server” là hai trạng thái khác nhau.

### 8.4. ViewModel

```csharp
// ViewModels/Catalog/ProductListViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

public partial class ProductListViewModel(
    IProductRepository repository,
    ILogger<ProductListViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private IReadOnlyList<ProductSummary> _products = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;
    public bool IsEmpty => HasLoaded && !IsBusy && !HasError && Products.Count == 0;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var products = await repository.SearchAsync(SearchText, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Products = products;
            HasLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Giữ snapshot trước đó khi người dùng hủy/rời trang.
        }
        catch (ApiException exception)
        {
            logger.LogWarning(exception, "Could not load product list");
            ErrorMessage = exception.StatusCode switch
            {
                401 => "Phiên đăng nhập không còn hợp lệ.",
                403 => "Bạn không có quyền xem danh sách sản phẩm.",
                _ => "Không tải được sản phẩm. Vui lòng thử lại."
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected product loading failure");
            ErrorMessage = "Đã xảy ra lỗi khi tải sản phẩm.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

Mẫu thay toàn bộ snapshot danh sách nên `IReadOnlyList` cùng property notification là đủ. Nếu cần thêm/xóa item ngay tại chỗ, dùng `ObservableCollection` ở ViewModel. Command mặc định không cho thực thi đồng thời; generator hỗ trợ truyền cancellation và sinh cancel command. [Tài liệu RelayCommand](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand).

Command cập nhật UI cần được khởi chạy trên UI thread. Không bọc I/O trong `Task.Run`; nếu có cập nhật từ background subscription, chuyển về UI qua dispatcher abstraction. Timeout của HTTP phải trở thành lỗi có thể thông báo, không bị nhầm với hành động hủy của người dùng.

### 8.5. View và khởi tạo bằng DI

Các binding chính trong `ProductListPage.xaml`, sau khi navigation/composition đã cung cấp property `ViewModel` có kiểu `ProductListViewModel`:

```xml
<TextBox Text="{x:Bind ViewModel.SearchText, Mode=TwoWay}"
         PlaceholderText="Tên hoặc SKU" />
<Button Content="Tìm kiếm" Command="{x:Bind ViewModel.LoadCommand}" />
<Button Content="Hủy tải" Command="{x:Bind ViewModel.LoadCancelCommand}" />
<ProgressRing IsActive="{x:Bind ViewModel.IsBusy, Mode=OneWay}" />
<InfoBar IsOpen="{x:Bind ViewModel.HasError, Mode=OneWay}"
         IsClosable="False"
         Severity="Error"
         Message="{x:Bind ViewModel.ErrorMessage, Mode=OneWay}" />
<ListView ItemsSource="{x:Bind ViewModel.Products, Mode=OneWay}" />
```

Đây là fragment binding, chưa phải Page hoàn chỉnh: cần layout, item template và binding `IsEmpty` sang EmptyStateControl. `x:Bind` mặc định OneTime nên dữ liệu thay đổi cần `Mode=OneWay` hoặc `TwoWay` rõ ràng.

Composition root đăng ký:

```csharp
// Bên trong ServiceCollectionExtensions, sau khi đã có ApiClient và handler.
services.AddLogging();
services.AddTransient<BearerTokenHandler>();
services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.BaseAddress = apiBaseUri; // Đọc từ cấu hình môi trường, kết thúc bằng '/'.
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<BearerTokenHandler>();
services.AddTransient<IProductRepository, ProductRepository>();
services.AddTransient<ProductListViewModel>();
```

`apiBaseUri`, `ApiClient`, `BearerTokenHandler` và token store là các thành phần cần hiện thực/kết nối; đoạn đăng ký không tự tạo ra chúng. `AddHttpClient` cần package `Microsoft.Extensions.Http` được khai báo qua CPM của frontend. Không chọn lại phiên bản Uno hay toolkit chỉ vì repo mẫu dùng phiên bản khác. Factory hỗ trợ cấu hình tập trung và quản lý vòng đời handler. [Microsoft — IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory).

Page/navigation adapter chịu trách nhiệm ghép View với ViewModel và kích hoạt `LoadCommand` khi vào trang; hủy command khi rời trang. Giữ constructor ViewModel không làm I/O. Với `Frame.Navigate`, cần page factory/adapter tương thích cơ chế tạo Page; không mặc định cho rằng Frame tự inject constructor như DI. Adapter cấp `ViewModel` trước binding hoặc cập nhật binding sau khi cấp; code-behind không xử lý tải dữ liệu/nghiệp vụ.

## 9. Các phần base phải hoàn thiện trước khi dùng thật

| Thành phần         | Hành vi cần có                                                                                                                                                              |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ApiClient`        | Nhận HttpClient qua DI; kiểm tra HTTP status trước deserialize; dispose response; truyền CancellationToken; phân biệt payload lỗi với payload thành công            |
| Chuẩn hóa lỗi     | Đọc ProblemDetails khi có; giữ status/error code/trace ID cần thiết; chuẩn hóa lỗi mạng, timeout và JSON sai; không hiển thị nguyên stack trace/body từ server |
| Kết quả rỗng      | `200 []` là danh sách rỗng hợp lệ; JSON null hoặc response sai contract là lỗi; `204` chỉ hợp lệ với thao tác đã quy định không có body                 |
| Token/session        | Gắn token hiện hành; 401 thông báo session hết hạn; 403 không đăng xuất người dùng; logout xóa dữ liệu riêng của session                                    |
| Refresh token        | Chỉ bật khi API có contract; gom các request refresh đồng thời; giới hạn một lần thử lại; không retry ghi dữ liệu tùy tiện                                   |
| Navigation lifecycle | Vào trang tải dữ liệu; rời trang hủy; giữ state chỉ khi có chủ đích; không đăng ký event/subscription bị rò rỉ                                              |
| Search               | Mẫu trên dùng nút tìm kiếm; nếu thêm tìm tức thời, debounce và bỏ kết quả request cũ; không chỉ bật command chạy đồng thời                              |
| Cache                | Sprint 1 mặc định đọc trực tiếp API; nếu bổ sung, cache phải phân biệt user/role/filter, có hạn dùng và invalidation sau mutation                              |
| Stock/checkout       | Số tồn trên UI có thể cũ; server luôn kiểm tra lại tồn và IMEI trong transaction khi xác nhận đơn                                                               |

Typed HTTP client và repository nên có lifetime transient trong base này. Token/session store có thể singleton nếu quản lý trạng thái an toàn. Desktop không tự có request scope như ASP.NET; chỉ dùng scoped service khi navigation tạo và dispose scope rõ ràng. Nếu Shell sống cả phiên, không giữ typed HTTP client transient trong singleton đó; Shell điều hướng, còn feature tạo dependency theo vòng đời của nó.

## 10. Áp dụng cho Dev6 trong Sprint 1

Thứ tự bàn giao đề xuất:

1. Thống nhất namespace, hợp đồng repository, Model và quy ước lỗi; dùng một feature đọc dữ liệu nhỏ làm ví dụ xuyên suốt.
2. Dựng composition root, HTTP transport, token/session abstraction và kiểm thử bằng HTTP handler giả.
3. Dựng Shell cùng navigation/dialog adapter, lifecycle tải/hủy và 6 điểm vào màn hình.
4. Dựng theme theo Design Tokens, StatCard, SearchBox và EmptyState; kết nối các trạng thái UI từ ViewModel.
5. Bàn giao ví dụ Repository → Model → ViewModel → View cho Dev2–Dev5 áp dụng.
6. Chuẩn bị `IAnalyticsRepository` theo nhu cầu Dashboard: summary, hoạt động gần nhất, tồn kho thấp, doanh thu theo ngày; chốt DTO với backend trước Sprint 2.

Không cần xây dựng cache offline, generic Unit of Work client hoặc toàn bộ use case layer trong Sprint 1. Khi API chưa sẵn sàng, fake repository chỉ nằm ở test hoặc chế độ demo phát triển được bật rõ ràng; không tự fallback sang dữ liệu giả khi API thật bị lỗi.

Với Dashboard: thống kê là query nghiệp vụ phía server; Repository lấy kết quả tổng hợp. Không tải toàn bộ đơn hàng xuống desktop để tự tính KPI hoặc dùng dữ liệu cache làm nguồn quyết định doanh thu.

## 11. Kiểm chứng kiến trúc khi triển khai

Các ca test cần chứng minh hành vi, không chỉ kiểm tra “đã gọi đúng tên method”:

| Ca kiểm chứng                             | Kết quả cần đạt                                                                 |
| ------------------------------------------- | ------------------------------------------------------------------------------------ |
| ViewModel + repository giả trả dữ liệu  | Items đúng, hết loading, không báo lỗi                                         |
| Repository trả danh sách rỗng            | Chỉ hiện empty sau lần tải thành công                                          |
| Lỗi mạng/401/403                          | Có thông báo phù hợp; không hiện empty giả và không làm mất snapshot cũ |
| Hủy request/rời trang                     | Không hiển thị lỗi hủy, không gán kết quả đến muộn                       |
| Tải thất bại rồi thử lại              | Command dùng tiếp được, state lỗi được xóa khi lần sau thành công       |
| Query có dấu, khoảng trắng,`&`        | HTTP query được encode đúng và server nhận đúng từ khóa                   |
| DTO/JSON không đúng contract             | Báo lỗi contract; không âm thầm coi là không có sản phẩm                   |
| Đăng xuất/đăng nhập tài khoản khác | Không lộ state/cache của tài khoản trước                                      |

Repository test dùng `IApiClient` giả để kiểm tra mapping; transport test dùng `HttpMessageHandler` giả để kiểm tra request/status/body. Integration test riêng mới gọi API thật. ViewModel test không cần Uno UI thread nếu thao tác thuần state và không reference control.

**Điều kiện bàn giao base:** build target desktop thành công, mở và điều hướng được Shell, có ít nhất một luồng lấy dữ liệu qua Repository, hiển thị đủ loading/error/empty/content và kiểm thử headless cho luồng đó. Hiện tài liệu này là thiết kế có dẫn chứng; chưa tuyên bố các điều kiện triển khai đã đạt.

## 12. Nguồn bổ sung và giới hạn nghiên cứu

- [Microsoft — MVVM](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm): trách nhiệm Model, View, ViewModel và khả năng kiểm thử.
- [Microsoft — RelayCommand generator](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand): async command, concurrency và cancellation.
- [Microsoft — IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory): HTTP pipeline và lifetime.
- [Uno — x:Bind](https://platform.uno/docs/articles/uno-development/Uno-UI-xBind-architecture.html): tài liệu đối chiếu binding khi triển khai View.

PowerToys và Bitwarden là bằng chứng trực tiếp về ViewModel kết hợp Repository; Files cung cấp đối chiếu tên thư mục và lớp Service. Các dẫn chứng không chứng minh mọi chi tiết trong đề xuất là bắt buộc hoặc có thể chuyển nguyên trạng sang .NET 10/Uno 6.7. Cấu trúc thư mục, Model client riêng, quy tắc lỗi và kế hoạch Sprint 1 là quyết định thiết kế được đề xuất cho TechStore.
