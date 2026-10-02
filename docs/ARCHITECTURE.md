# KIẾN TRÚC HỆ THỐNG VÀ CẤU TRÚC THƯ MỤC (ARCHITECTURE SPECIFICATION)
## Dự án: TechStore Management System
**Nền tảng:** .NET 10 | **Kiến trúc:** 3-Tier API-First | **Thiết kế:** Backend Modular & Frontend WinUI 3 Standard

---

## 1. Mô hình Kiến trúc Tổng quan (3-Tier API-First)

```text
┌────────────────────────────────────────────────────────────────────────┐
│                   FRONTEND: UNO PLATFORM (WINUI 3)                     │
│   • Windows 11 Native (WinUI 3) & macOS Desktop (Skia Engine)          │
│   • Chuẩn kiến trúc: Feature-First MVVM (CommunityToolkit.Mvvm)        │
│   • Dịch vụ giao tiếp: Refit / HttpClient + Token Authorization        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    │ HTTPS (JSON / RESTful API + JWT Bearer)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                   BACKEND: ASP.NET CORE 10 (MODULAR)                   │
│   • Phân rã phẳng thành các Modules độc lập (Catalog, Orders, Sales...)│
│   • Bảo mật tập trung: Middleware JWT & RBAC 4 Roles                   │
│   • Logic nghiệp vụ, Transaction & Khóa chống bán âm kho (FOR UPDATE)  │
│   • Trợ lý AI: Google Gemini API Function Calling                      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    │ EF Core 10 (Npgsql)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        DATABASE: POSTGRESQL 16+                        │
│   • PostgreSQL (Cột JSONB thông số linh hoạt, ACID Transactions)       │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Cấu trúc Thư mục Toàn hệ thống

### 2.1. Cây Thư mục Tổng quan
```text
SalesManagement/
├── TechStore.sln                      # Solution tổng (.NET 10)
├── docker-compose.yml                 # Khởi chạy PostgreSQL 16
├── README.md
├── docs/                              # Tài liệu kỹ thuật dự án
│   ├── REQUIREMENTS.md
│   ├── TECH_REPORT.md
│   ├── ROADMAP.md
│   └── ARCHITECTURE.md
│
├── backend/                           # API Server (ASP.NET Core 10)
├── frontend/                          # Client Desktop App (Uno Platform WinUI 3)
├── shared/                            # DTOs và Contracts dùng chung
└── tests/                             # Dự án kiểm thử tự động
```

---

### 2.2. Chi tiết Cấu trúc `backend/`
Các module nghiệp vụ được tổ chức theo từng thư mục tính năng độc lập, nằm trực tiếp trong `backend/` ngang hàng với `Common/`:

```text
backend/
├── Common/                            # Thành phần dùng chung nội bộ Server
│   ├── Data/                          # AppDbContext (EF Core 10), Migrations, DataSeeder
│   ├── Entities/                      # BaseEntity (Id, CreatedAt, UpdatedAt)
│   └── Middlewares/                   # ExceptionHandlerMiddleware, JwtMiddleware
│
├── Catalog/                           # Quản lý Sản phẩm, Danh mục & Thông số kỹ thuật
│   ├── Entities/                      # Product, Category, ProductVariant
│   ├── Services/                      # ICatalogService, CatalogService
│   └── ProductsController.cs          # API CRUD, tìm kiếm, lọc theo thông số (JSONB)
│
├── Inventory/                         # Quản lý Kho, Serial/IMEI & Khóa Concurrency
│   ├── Entities/                      # InventoryStock, SerialImei, PurchaseOrder
│   ├── Services/                      # IInventoryService (Transaction FOR UPDATE)
│   └── InventoryController.cs         # API Nhập kho NCC, quản lý danh sách Serial/IMEI
│
├── Sales/                             # Nghiệp vụ Bán hàng tại quầy (POS) & Thanh toán
│   ├── Services/                      # IPosService, VietQrService
│   └── SalesController.cs             # API Lập đơn tại quầy, tính tiền, sinh mã VietQR
│
├── Orders/                            # Vòng đời Đơn hàng, Đổi trả & Hoàn tiền
│   ├── Entities/                      # Order, OrderItem, OrderReturn
│   ├── Services/                      # IOrderService, ReturnRefundService
│   └── OrdersController.cs            # API Danh sách đơn, đổi trạng thái, hủy/hoàn tiền
│
├── Customers/                         # Quản lý Khách hàng, Tích điểm & Thống kê
│   ├── Entities/                      # Customer, LoyaltyPoint, Voucher
│   ├── Services/                      # ICustomerService, AnalyticsService
│   ├── CustomersController.cs     # API Khách hàng, tích điểm thành viên VIP
│   └── AnalyticsController.cs         # API Dashboard báo cáo doanh thu & lợi nhuận
│
├── Identity/                          # Bảo mật, Phân quyền & Trợ lý AI
│   ├── Entities/                      # User, Role, RefreshToken
│   ├── Services/                      # IAuthService, JwtService, GeminiAiAgentService
│   ├── AuthController.cs              # API Đăng nhập, đổi mật khẩu, phân quyền RBAC
│   └── AiController.cs                # API Trợ lý AI (Function Calling tra cứu kho)
│
├── appsettings.json
├── appsettings.Development.json
└── Program.cs                         # Cấu hình DI, DbContext, JWT, Swagger
```

---

### 2.3. Chi tiết Cấu trúc `frontend/`
Thiết kế theo chuẩn Microsoft Template Studio cho WinUI 3 kết hợp Feature-First MVVM:

```text
frontend/
├── Controls/                          # Custom UI Controls tái sử dụng
│   ├── StatCard.xaml                  # Thẻ thống kê số liệu Dashboard
│   ├── ProductCard.xaml               # Card hiển thị sản phẩm (ảnh, giá, thông số)
│   └── SearchBoxControl.xaml          # Ô tìm kiếm autocomplete
│
├── Converters/                        # Value Converters cho XAML Data Binding
│   ├── CurrencyConverter.cs           # Định dạng tiền tệ VND
│   ├── StatusToColorConverter.cs      # Đổi màu theo trạng thái đơn hàng
│   └── NullToVisibilityConverter.cs   # Ẩn/Hiện control dựa trên giá trị null
│
├── Features/                          # Giao diện & Logic phân theo tính năng (View + ViewModel)
│   │
│   ├── Shell/                         # Khung ứng dụng & Navigation Menu chính
│   │   ├── ShellPage.xaml             # NavigationView chuẩn Fluent Design Windows 11
│   │   ├── ShellPage.xaml.cs
│   │   └── ShellViewModel.cs          # Quản lý điều hướng và menu theo quyền
│   │
│   ├── Catalog/                       # Giao diện Sản phẩm & Danh mục
│   │   ├── ProductListPage.xaml       # Danh sách sản phẩm, bộ lọc thông số kỹ thuật
│   │   ├── ProductListViewModel.cs
│   │   ├── ProductDetailDialog.xaml   # Dialog chi tiết thông số & danh sách IMEI
│   │   └── ProductDetailViewModel.cs
│   │
│   ├── Inventory/                     # Giao diện Quản lý Kho & Nhập hàng
│   │   ├── InventoryPage.xaml         # Bảng tồn kho, cảnh báo tồn tối thiểu
│   │   ├── InventoryViewModel.cs
│   │   └── StockInDialog.xaml         # Dialog lập phiếu nhập kho NCC
│   │
│   ├── Sales/                         # Giao diện Bán hàng tại quầy (POS)
│   │   ├── PosCheckoutPage.xaml       # Màn hình POS bán hàng, giỏ hàng, quét mã
│   │   ├── PosCheckoutViewModel.cs    # Tính tổng tiền, thuế, áp voucher
│   │   └── VietQrDialog.xaml          # Dialog hiển thị mã VietQR chuyển khoản
│   │
│   ├── Orders/                        # Giao diện Đơn hàng & Đổi trả
│   │   ├── OrderListPage.xaml         # Danh sách đơn hàng lọc theo trạng thái
│   │   ├── OrderListViewModel.cs
│   │   └── OrderDetailDialog.xaml     # Chi tiết đơn hàng, nút hủy/hoàn tiền
│   │
│   ├── Customers/                     # Giao diện Khách hàng & Dashboard Doanh thu
│   │   ├── CustomerListPage.xaml      # Quản lý khách hàng, lịch sử mua sắm
│   │   ├── CustomerListViewModel.cs
│   │   ├── DashboardPage.xaml         # Biểu đồ doanh thu, top sản phẩm bán chạy
│   │   └── DashboardViewModel.cs
│   │
│   ├── Identity/                      # Giao diện Đăng nhập & Quản lý Tài khoản
│   │   ├── LoginPage.xaml             # Form đăng nhập Fluent Design
│   │   └── LoginViewModel.cs          # Xử lý đăng nhập, lưu trữ token
│   │
│   └── AiAssistant/                   # Widget Trợ lý AI Thông minh
│       ├── AiChatWidget.xaml          # Cửa sổ chat AI nổi góc giao diện
│       └── AiChatViewModel.cs         # Gửi câu hỏi, hiển thị thẻ sản phẩm gợi ý
│
├── Services/                          # Tầng Dịch vụ Client (Dependency Injection)
│   ├── Api/                           # HttpClients gọi API Server
│   │   ├── ProductApiClient.cs        # Gọi API sản phẩm
│   │   ├── OrderApiClient.cs          # Gọi API đơn hàng
│   │   ├── InventoryApiClient.cs      # Gọi API tồn kho
│   │   └── AuthApiClient.cs           # Gọi API xác thực
│   ├── Navigation/                    # INavigationService (Điều hướng trang)
│   ├── Dialog/                        # IDialogService (Hộp thoại xác nhận, thông báo lỗi)
│   └── LocalStorage/                  # ITokenStorageService (Lưu trữ JWT an toàn)
│
├── Styles/                            # XAML Resource Dictionaries (Fluent Theme)
│   ├── Colors.xaml                    # Bảng màu chủ đạo hệ thống
│   ├── Styles.xaml                    # Kiểu dáng các control bo góc
│   └── TextBlockStyles.xaml           # Font chữ và kiểu hiển thị chữ
│
├── App.xaml                           # Nạp Resource Dictionaries
├── App.xaml.cs                        # Cấu hình DI Container (IServiceCollection)
└── Program.cs                         # Bootstrapper Uno Platform
```

---

### 2.4. Chi tiết Cấu trúc `shared/` và `tests/`

```text
shared/                                # DTOs VÀ CONTRACTS DÙNG CHUNG
├── DTOs/                              # ProductDto, OrderDto, UserDto, CustomerDto...
├── Requests/                          # CreateProductRequest, CreateOrderRequest, LoginRequest...
└── Enums/                             # RoleType, OrderStatus, ProductType, PaymentMethod...

tests/                                 # KIỂM THỬ TỰ ĐỘNG
├── Backend.UnitTests/                 # Unit Tests cho logic tính tiền, trừ kho (xUnit)
└── Backend.IntegrationTests/          # Integration Tests cho API & Phân quyền (WebApplicationFactory)
```

---

### 2.5. Giải thích Kiến trúc MVVM và Feature-First MVVM

#### A. Mô hình MVVM (Model - View - ViewModel) là gì?
**MVVM** là mẫu kiến trúc phần mềm tiêu chuẩn cho các ứng dụng giao diện XAML (.NET MAUI, WPF, WinUI 3, Uno Platform), chia ứng dụng thành 3 thành phần tách biệt:

```text
┌─────────────────────────┐        Data Binding / Commands       ┌─────────────────────────┐            Calls / DTOs            ┌─────────────────────────┐
│       VIEW (XAML)       │ ◄──────────────────────────────────► │     VIEWMODEL (C#)      │ ─────────────────────────────────► │      MODEL (C# DTO)     │
│                         │                                      │                         │                                    │                         │
│ • Giao diện người dùng  │                                      │ • Trạng thái hiển thị   │                                    │ • Dữ liệu nghiệp vụ     │
│ • XAML layout & styling │                                      │ • Xử lý lệnh bấm nút    │                                    │ • Các class từ shared/  │
│ • Không chứa code logic │                                      │ • Gọi API qua Services  │                                    │ • Không phụ thuộc ai    │
└─────────────────────────┘                                      └─────────────────────────┘                                    └─────────────────────────┘
```

1. **Model:** Đại diện cho dữ liệu nghiệp vụ (các DTOs như `ProductDto`, `OrderDto` từ `shared/`). Không chứa logic giao diện.
2. **View (XAML):** Chịu trách nhiệm hiển thị giao diện người dùng nhìn thấy (`Page.xaml`, `Dialog.xaml`). View kết nối với ViewModel hoàn toàn thông qua cơ chế **Data Binding** (`{x:Bind ...}`), tuyệt đối không viết code logic vào file code-behind (`.xaml.cs`).
3. **ViewModel (C#):** Là "bộ não" điều khiển View. ViewModel chứa dữ liệu đang hiển thị (Properties), lệnh khi người dùng bấm nút (Commands), và gọi tầng Services để tải/lưu dữ liệu từ API. ViewModel hoàn toàn không phụ thuộc vào control UI cụ thể nào, giúp dễ dàng viết **Unit Test độc lập**.
4. **Công cụ hỗ trợ:** Dự án sử dụng thư viện chính chủ **`CommunityToolkit.Mvvm`** với tính năng Source Generator:
   * Thêm `[ObservableProperty]` lên biến private $\rightarrow$ Tự động sinh property có thông báo thay đổi `INotifyPropertyChanged`.
   * Thêm `[RelayCommand]` lên hàm async $\rightarrow$ Tự động sinh `ICommand` để gắn vào nút bấm trên XAML.

---

#### B. Feature-First MVVM là gì?
**Feature-First MVVM** là phương pháp tổ chức mã nguồn bằng cách **nhóm View và ViewModel thuộc cùng một tính năng nghiệp vụ vào chung một thư mục**, thay vì phân chia theo tầng kỹ thuật truyền thống (Layer-First).

| Tiêu chí | Layer-First (Truyền thống) | Feature-First (Hiện đại) |
| :--- | :--- | :--- |
| **Cách tổ chức** | • Gom tất cả Views vào thư mục `Views/`<br>• Gom tất cả ViewModels vào `ViewModels/`<br>• Gom tất cả Dialogs vào `Dialogs/` | Gom `ProductListPage.xaml`, `ProductListViewModel.cs`, `ProductDetailDialog.xaml` vào chung một thư mục `Features/Catalog/` |
| **Thao tác khi code** | Khi cần sửa một màn hình, lập trình viên phải nhảy qua lại giữa 3 thư mục cách xa nhau trên cây project (**Navigation Tax**). | Mở đúng 1 thư mục là có đầy đủ giao diện (View) và logic điều khiển (ViewModel) của tính năng đó. |
| **Độ kết dính (Cohesion)**| Rời rạc, khó theo dõi toàn bộ ngữ cảnh của một màn hình. | Rất cao (**High Cohesion**), toàn bộ ngữ cảnh tính năng được đóng gói trọn vẹn. |
| **Khả năng mở rộng** | Khi dự án lớn lên, thư mục `Views/` và `ViewModels/` chứa hàng chục file lẫn lộn, rất dễ nhầm lẫn. | Thêm tính năng mới chỉ cần tạo thêm 1 thư mục con trong `Features/`, không làm xáo trộn các thư mục hiện có. |
