# Proposal

## Why

Sprint 2 yêu cầu C# Markup nhưng shared resources, bốn controls và gallery vẫn phụ thuộc XAML; các trang C# mới lại dùng bộ StitchTokens riêng. Dev 6 cần một nền tảng dùng chung có resource/binding contracts ổn định để các dev chuyển màn hình từng phần mà không làm mất live theme hoặc phá consumer hiện có.

## What Changes

- Chuyển năm dictionaries Metrics, Colors, Typography, ControlStyles, Icons sang C# resources; thống nhất tokens theo design-system hiện hành và giữ tên/key/type tài nguyên cho consumer cũ.
- Nạp resources trước khi tạo UI, giữ WinUI resources và app/session/navigation bootstrap hiện có.
- Chuyển StatCard, SearchBoxControl, EmptyStateControl, StatusBadge thành C# Markup UserControls, giữ namespace, constructors, DependencyProperties và command semantics.
- Chuyển ComponentGalleryPage và MainPage sang C#; gallery tiếp tục offline, MainPage vẫn là trang khung không được đăng ký làm Shell hay route mới.
- Bổ sung contracts kiểm chứng tương thích giữa consumer XAML và C#, specimens, evidence và handoff; xóa đúng các tệp XAML đã được thay thế.
- Apply toàn bộ phạm vi change liên tục; hoàn tất resources, controls, gallery/MainPage và integration trước khi chạy build/tests/runtime verification cuối cùng và bàn giao. Tasks theo dõi deliverables nhưng không tạo checkpoint dừng giữa implementation.

## Capabilities

### New Capabilities

Không có capability mới.

### Modified Capabilities

- `frontend-design-system`: thêm contract tương thích resource keys/types giữa XAML/C# và bootstrap resources thống nhất trước khi dựng UI.
- `frontend-shared-controls`: thêm contract tương thích public API, binding và command cho cả hai loại consumer trong migration.
- `frontend-component-gallery`: thêm specimens để kiểm chứng contracts tương thích và vòng đời mount/unmount của gallery C#.

Các yêu cầu C# Markup và live theme đã có vẫn được giữ; delta tập trung vào các bảo đảm tương thích được yêu cầu trong change này.

## Impact

- Client: `Styles/`, bốn controls trong `Controls/`, `Views/Development/ComponentGalleryPage`, `Views/MainPage`, phần resource bootstrap của `App.xaml`/`App.xaml.cs`.
- Kiểm tra: `tests/Frontend.UnitTests/` cho logic hiện có, gallery runtime cho UI thực, handoff/evidence theo build và target. Không coi headless tests là kiểm chứng WinUI binding.
- Tái sử dụng UnoFeatures CSharpMarkup và CPM hiện có; không dự kiến thêm package hoặc nâng Uno/.NET.
- Integration với Dev 1 giới hạn bootstrap/resources và compatibility facade StitchTokens. Không viết lại Login/Shell/Settings, không thay auth/navigation, không chuyển Dashboard/Catalog/Inventory/Orders/Customers, không làm Analytics hoặc Seeder.
- `App.xaml` application host có thể còn; phạm vi xóa là năm dictionaries và sáu cặp View/Control XAML tương ứng. Không yêu cầu toàn repo hết XAML trong change này.
- Không thay backend/shared DTO/API/database. Chỉ tạo planning artifacts ở lượt propose; chưa implementation, commit hoặc push.
