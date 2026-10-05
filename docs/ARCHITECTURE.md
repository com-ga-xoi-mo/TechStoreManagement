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
│   ├── Configurations/                # Fluent API Configurations (IEntityTypeConfiguration<T>)
│   ├── Services/                      # ICatalogService, CatalogService
│   └── ProductsController.cs          # API CRUD, tìm kiếm, lọc theo thông số (JSONB)
│
├── Inventory/                         # Quản lý Kho, Serial/IMEI, Nhập kho & Khóa Concurrency
│   ├── Entities/                      # InventoryStock, InventoryMovement, Supplier, PurchaseOrder, PurchaseOrderItem, SerialImei
│   ├── Configurations/                # Fluent API Configurations (Composite FKs, Check Constraints)
│   ├── Services/                      # IInventoryService, InventoryService (Transaction FOR UPDATE)
│   └── InventoryController.cs         # API Tồn kho, nhập kho NCC, quản lý danh sách Serial/IMEI
│
├── Sales/                             # Nghiệp vụ Bán hàng tại quầy (POS), Ca làm việc & VietQR
│   ├── Entities/                      # PosSession, VietQrTransaction
│   ├── Configurations/                # Fluent API Configurations
│   ├── Services/                      # IPosService, VietQrService
│   └── SalesController.cs             # API Lập đơn tại quầy, tính tiền, ca thu ngân, sinh mã VietQR
│
├── Orders/                            # Vòng đời Đơn hàng, Chi tiết món, Đổi trả & Hoàn tiền
│   ├── Entities/                      # Order, OrderItem, OrderReturn
│   ├── Configurations/                # Fluent API Configurations
│   ├── Services/                      # IOrderService, ReturnRefundService
│   └── OrdersController.cs            # API Danh sách đơn, đổi trạng thái, hủy/hoàn tiền
│
├── Customers/                         # Quản lý Khách hàng, Tích điểm & Khuyến mãi Voucher
│   ├── Entities/                      # Customer, CustomerLoyaltyPoint, Voucher
│   ├── Configurations/                # Fluent API Configurations (Partial Index Voucher)
│   ├── Services/                      # ICustomerService, AnalyticsService
│   ├── CustomersController.cs         # API Khách hàng, tích điểm thành viên VIP
│   └── AnalyticsController.cs         # API Dashboard báo cáo doanh thu & lợi nhuận
│
├── Identity/                          # Bảo mật, Phân quyền RBAC & Trợ lý AI
│   ├── Entities/                      # User, Role, UserRole, RefreshToken
│   ├── Configurations/                # Fluent API Configurations (Partial Index Token)
│   ├── Services/                      # IAuthService, JwtService, GeminiAiAgentService
│   ├── AuthController.cs              # API Đăng nhập, đổi mật khẩu, phân quyền RBAC
│   └── AiController.cs                # API Trợ lý AI (Function Calling tra cứu kho)
│
├── appsettings.json
├── appsettings.Development.json
└── Program.cs                         # Cấu hình DI, DbContext, JWT, Swagger

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

---

## 3. Kiến trúc Cơ sở Dữ liệu Chuẩn hóa (PostgreSQL 16+ & Schema Specification)

Hệ thống cơ sở dữ liệu được đặc tả hoàn chỉnh và trực quan hóa qua file chuẩn **`docs/database/schema.dbml`** (tương thích trực tiếp với [dbdiagram.io](https://dbdiagram.io) và [dbdocs.io](https://dbdocs.io)). Toàn bộ schema tuân thủ nghiêm ngặt **Supabase Postgres Best Practices**.

### 3.1. Phân rã 6 Phân hệ Bảng (TableGroups) - 21 Bảng Dữ liệu

| Phân hệ (TableGroup) | Bảng dữ liệu | Số bảng | Mô tả nghiệp vụ cốt lõi |
| :--- | :--- | :---: | :--- |
| **Catalog** | `categories`, `products`, `product_variants` | 3 | Cây danh mục cha-con (`parent_id`), sản phẩm gốc và các biến thể phần cứng (màu sắc, dung lượng). Thông số kỹ thuật lưu qua cột `specs JSONB`. |
| **Inventory** | `inventory_stocks`, `inventory_movements`, `suppliers`, `purchase_orders`, `purchase_order_items`, `serial_imeis` | 6 | Quản lý tồn kho vật lý, số lượng đặt trước (`reserved_quantity`), sổ cái biến động kho bất biến (`inventory_movements`), nhà cung cấp, đơn mua hàng (hỗ trợ nhập từng phần `PartiallyReceived`), và theo dõi định danh Serial/IMEI từng máy. |
| **Orders** | `orders`, `order_items`, `order_returns` | 3 | Đơn hàng bán lẻ POS, từng dòng sản phẩm liên kết Serial/IMEI đã xuất bán, và đơn hoàn trả/bảo hành kèm cờ nhập lại kho (`is_restocked`). |
| **Customers** | `customers`, `customer_loyalty_points`, `vouchers` | 3 | Hồ sơ khách hàng, phân hạng VIP (Standard, Silver, Gold, Platinum), sổ cái tích/tiêu điểm thưởng (`customer_loyalty_points`), và mã giảm giá voucher (`vouchers`). |
| **Sales** | `pos_sessions`, `vietqr_transactions` | 2 | Quản lý ca làm việc của thu ngân (đối soát tiền mặt đầu/cuối ca, tổng doanh thu thẻ/QR) và các giao dịch chuyển khoản ngân hàng động VietQR. |
| **Identity** | `users`, `roles`, `user_roles`, `refresh_tokens` | 4 | Tài khoản nhân viên, 4 vai trò RBAC, bảng liên kết nhiều-nhiều `user_roles`, và chuỗi refresh token được mã hóa SHA-256 (`token_hash`) hỗ trợ thu hồi. |

---

### 3.2. Chuẩn hóa 10 Domain Enums

Hệ thống chuẩn hóa 10 kiểu liệt kê (Enum) định nghĩa toàn bộ trạng thái vòng đời trong hệ thống:

1. **`order_status`**: `Pending` (Chờ xử lý/thanh toán) $\rightarrow$ `Processing` (Đang chuẩn bị/lấy hàng) $\rightarrow$ `Completed` (Hoàn tất) $\mid$ `Cancelled` (Đã hủy) $\mid$ `Refunded` (Đã hoàn tiền).
2. **`payment_method`**: `Cash` (Tiền mặt tại quầy), `VietQr` (Chuyển khoản VietQR động), `Card` (Thẻ ngân hàng POS).
3. **`serial_imei_status`**: `InStock` (Trong kho) $\rightarrow$ `Reserved` (Khóa giữ chỗ cho đơn đang thanh toán) $\rightarrow$ `Sold` (Đã bán) $\rightarrow$ `Returned` (Khách trả lại, chờ kiểm định) $\rightarrow$ `UnderRepair` (Đang sửa chữa bảo hành) $\rightarrow$ `Defective` (Hỏng/lỗi chờ trả NCC). *(Thời hạn bảo hành tính tự động theo `warranty_start_at` và `warranty_end_at`).*
4. **`inventory_movement_type`**: `OpeningBalance` (Tồn đầu kỳ), `PurchaseReceipt` (Nhập kho NCC +), `Sale` (Xuất bán -), `CustomerReturn` (Khách trả hàng restock +), `Adjustment` (Điều chỉnh kiểm kê +/-), `Defective` (Xuất hủy hàng lỗi -).
5. **`customer_tier`**: `Standard` (0 - 499 điểm), `Silver` (500 - 1.499 điểm, giảm 2%), `Gold` (1.500 - 4.999 điểm, giảm 5%), `Platinum` (5.000+ điểm, giảm 10%).
6. **`voucher_type`**: `Percentage` (Chiết khấu phần trăm), `FixedAmount` (Giảm số tiền cố định VND).
7. **`purchase_order_status`**: `Draft` (Bản nháp) $\rightarrow$ `Ordered` (Đã đặt hàng NCC) $\rightarrow$ `PartiallyReceived` (Nhận hàng một phần) $\rightarrow$ `Received` (Đã nhận đủ) $\mid$ `Cancelled` (Đã hủy).
8. **`pos_session_status`**: `Open` (Ca đang mở), `Closed` (Ca đã đóng & đối soát két).
9. **`vietqr_status`**: `Pending` (Chờ quét mã), `Confirmed` (Xác nhận thành công qua webhook/bank), `Failed` (Thất bại), `Expired` (Hết hạn giao dịch).
10. **`role_type`**: `Admin` (Quản trị tối cao), `Manager` (Cửa hàng trưởng), `SalesStaff` (Thu ngân / Bán hàng), `WarehouseStaff` (Thủ kho).

---

### 3.3. Các Nguyên tắc Thiết kế & Supabase Postgres Best Practices

1. **100% Foreign Key Indexing (B-Tree):**
   Mọi cột khóa ngoại (FK) trên cả 21 bảng đều được tạo chỉ mục B-tree rõ ràng (chuẩn đặt tên: `idx_<table>_<col>`). Điều này ngăn chặn việc PostgreSQL quét toàn bảng (Sequential Scan) khi JOIN và triệt tiêu nguy cơ khóa bảng khi kiểm tra ràng buộc tầng (Cascade validation).

2. **Khóa Ngoại Tổng Hợp (Composite Foreign Keys):**
   Ràng buộc khóa ngoại kép `(variant_id, product_id)` tham chiếu tới `product_variants.(id, product_id)` được áp dụng trên các bảng `inventory_stocks`, `purchase_order_items`, `serial_imeis`, và `order_items`. Điều này đảm bảo tuyệt đối ở tầng CSDL rằng biến thể được chọn phải thuộc về đúng sản phẩm cha đó, tránh lỗi dữ liệu cắm nhầm variant sang sản phẩm khác. Bảng `product_variants` có index duy nhất `uq_product_variants_id_product` trên `(id, product_id)`.

3. **PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT`:**
   Với các sản phẩm không có biến thể (`variant_id = NULL`), PostgreSQL thông thường coi các giá trị NULL là khác nhau nên có thể cho phép chèn nhiều dòng trùng lặp. Tính năng PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT (product_id, variant_id)` (trong EF Core: `.AreNullsDistinct(false)`) bảo đảm:
   - Trên `inventory_stocks`: Chỉ tồn tại duy nhất 1 dòng tồn kho cho mỗi cặp `(product_id, variant_id)`, kể cả khi `variant_id` là NULL.
   - Trên `purchase_order_items`: Chỉ tồn tại duy nhất 1 dòng chi tiết cho mỗi sản phẩm/biến thể trên cùng 1 đơn nhập hàng `(purchase_order_id, product_id, variant_id)`.

4. **Tối ưu Cột Thông số Kỹ thuật Động (`specs JSONB`):**
   - Cột `specs` trên bảng `products` và `product_variants` được đánh chỉ mục **GIN với operator class `jsonb_path_ops`** (`idx_products_specs_gin`, `idx_product_variants_specs_gin`), tối ưu hóa tốc độ truy vấn chứa (`specs @> '{"ram_gb": 16}'`).
   - Quy ước cấu trúc JSON phẳng với khóa dạng lowercase snake_case (`cpu`, `ram_gb`, `storage_gb`, `battery_mah`, `color`).
   - Kế thừa động: Thông số hiệu lực của biến thể được tính bằng `product.specs || variant.specs` (biến thể ghi đè khóa cấp 1 tương ứng của sản phẩm cha).

5. **Chỉ Mục Một Phần (Partial / Filtered Indexes):**
   Tối ưu hóa các truy vấn tần suất cực cao tại quầy và bảo mật:
   - `serial_imeis`: Partial index `(product_id, status)` với điều kiện `WHERE status = 'InStock'` giúp thu ngân quét mã vạch/IMEI tức thì.
   - `vouchers`: Partial index `(is_active, expires_at)` với điều kiện `WHERE is_active = true` (điều kiện so sánh động `expires_at > now()` được lọc ở tầng query vì PostgreSQL cấm hàm không immutable trong index predicate).
   - `refresh_tokens`: Partial index `(user_id, token_hash)` với điều kiện `WHERE is_revoked = false` (`token_hash` lưu hash SHA-256 an toàn).

6. **Bảo toàn Số dư Tồn kho & Chống Bán Âm (Anti-Overselling Concurrency):**
   - Ràng buộc kiểm tra (Check Constraints): `quantity >= 0`, `reserved_quantity >= 0`, `reserved_quantity <= quantity`.
   - Số lượng thực tế có thể bán: `available = quantity - reserved_quantity`.
   - Khi giữ chỗ cho đơn đang thanh toán: Tăng `reserved_quantity` và đổi trạng thái IMEI sang `Reserved` trong transaction sử dụng khóa dòng `SELECT ... FOR UPDATE`.
   - Bất biến với máy quản lý IMEI (`is_serial_tracked = true`): `inventory_stocks.quantity` phải luôn bằng `COUNT(serial_imeis WHERE status IN ('InStock', 'Reserved'))`.

7. **Sổ Cái Biến Động Kho Bất Biến (Append-Only Audit Ledger `inventory_movements`):**
   Mọi thay đổi số lượng tồn kho vật lý (`quantity_change <> 0`) bắt buộc phải ghi 1 bản ghi vào `inventory_movements` trong cùng transaction, lưu vết số dư ngay sau giao dịch (`quantity_after >= 0`), người thực hiện (`performed_by_user_id`), và chứng từ gốc liên kết (`purchase_order_item_id`, `order_item_id`, hoặc `order_return_id`). Bảng chỉ hỗ trợ INSERT (Append-Only), nghiêm cấm UPDATE/DELETE lịch sử.

8. **Chuẩn hóa Kiểu Dữ liệu & Quy ước Định danh:**
   - Mọi mốc thời gian dùng kiểu `timestamptz` chuẩn múi giờ UTC.
   - Mọi số tiền, đơn giá, chiết khấu dùng kiểu `numeric(18, 2)` tránh sai số làm tròn số thực.
   - Tên bảng, cột, khóa, index và enum tuân thủ thống nhất chữ thường nối gạch dưới (`snake_case`).
