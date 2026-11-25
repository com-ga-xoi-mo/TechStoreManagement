# Spec Delta

## MODIFIED Requirements

### Requirement: Six coherent admin destinations
Shell SHALL have exactly eight entry points: Overview, Products, Categories and Inventory, Orders, Return inspections, Warranty, Customers, and Settings. They SHALL be structured with C# Markup Fluent API (`Uno.Extensions.Markup.WinUI` on `NavigationView`). Selected menu, title, and content SHALL reflect the successfully navigated route. Shell Pane Footer SHALL show the active account (FullName, Role) and Logout action through declarative C# Markup bindings. Products SHALL open live Catalog data and provide productId detail navigation; Overview SHALL show live Analytics by default or labeled preview only when enabled; unimplemented business pages SHALL clearly remain placeholders without fabricated data. At expanded desktop widths, the navigation pane SHALL visually follow the left-sidebar hierarchy in `docs/sample_dashboard.png`: brand and subtitle, menu section label, highlighted current destination, compact icon/label rows, Settings separated near the bottom, and account footer. Visual changes MUST preserve existing route, selection, session and logout behavior.

#### Scenario: Visit every destination
- **WHEN** lần lượt chọn tám điểm vào bằng chuột hoặc keyboard
- **THEN** nội dung tương ứng hiển thị, Sản phẩm tải Catalog thật, Bảo hành tải hồ sơ thật, selection/title đúng, Cài đặt chỉ xuất hiện một lần ở footer và không có mục POS/Identity bổ sung

#### Scenario: Same destination selected again
- **WHEN** chọn lại route hiện hành với cùng tham số
- **THEN** nội dung/state giữ nguyên, không tạo trang hoặc load lần nữa

#### Scenario: Unknown or unavailable destination
- **WHEN** yêu cầu route không hỗ trợ, productId rỗng hoặc không tạo được trang đích
- **THEN** giữ trang/title/selection trước đó và báo lỗi an toàn, không để Shell trống hoặc selection sai

#### Scenario: Read-only detail navigation
- **WHEN** mở card A rồi chuyển tới detail B trong nhánh Sản phẩm
- **THEN** truyền đúng id từng sản phẩm, tải chi tiết tương ứng, hủy activation trước và giữ Sản phẩm selected; detail không tạo mục menu thứ chín

#### Scenario: Preview stays within dashboard
- **WHEN** mở Dashboard demo rồi chọn Sản phẩm hoặc mục khác
- **THEN** vẫn có đúng tám mục, Catalog tiếp tục dùng API thật và các mục chưa triển khai giữ trạng thái khung

#### Scenario: Shell reflects active user session
- **WHEN** Quản trị viên đăng nhập thành công với tài khoản "admin"
- **THEN** Pane footer của Shell cập nhật tên người dùng và vai trò "Quản trị viên", nhãn trạng thái chuyển thành "Đã đăng nhập"

#### Scenario: Expanded sidebar matches the reference hierarchy
- **WHEN** Shell is shown with its navigation pane expanded at a wide desktop size
- **THEN** brand, menu caption, selected Overview row, other destinations, separated Settings entry and account footer follow the reference's visual hierarchy without adding/removing destinations or changing navigation behavior

#### Scenario: Warranty and return inspections use distinct icons
- **WHEN** Admin opens the Shell with all destinations available
- **THEN** Warranty and Return inspections show distinct icons

### Requirement: Voucher route under Customers
Shell SHALL host a voucher list route under the Customers destination without adding a menu entry. The route SHALL be openable only by Admin and Manager and SHALL be refused for other roles before the page loads. Leaving it SHALL return to the Customers list.

#### Scenario: Route keeps the menu
- **WHEN** a Manager navigates to the voucher route
- **THEN** the menu still shows the same destinations and Customers is selected

#### Scenario: Warehouse direct route
- **WHEN** a WarehouseStaff session navigates to the voucher route
- **THEN** navigation is refused and the previous page stays

#### Scenario: Back to customers
- **WHEN** the user presses "← Khách hàng" on the voucher page
- **THEN** the Customers list opens

### Requirement: Role based menu and landing page

Shell SHALL show only the destinations the signed-in role may use, following the client role table that mirrors `docs/API_PERMISSIONS.md`. Each new session SHALL open the role's landing page, the first allowed destination in the order Overview, Orders, Categories and Inventory, Products. Warranty SHALL be shown for every role but SHALL NOT be a landing page. Settings SHALL be shown for every role. The previous session's route SHALL NOT be reused.

#### Scenario: Warehouse login
- **WHEN** `warehouse1` signs in
- **THEN** Shell opens Categories and Inventory and the menu shows Products, Categories and Inventory, Return inspections, Warranty and Settings only

#### Scenario: Sales login after Admin
- **WHEN** Admin was on Customers, logs out, then `cashier1` signs in
- **THEN** Shell opens Orders, not Customers; the menu shows Customers (search and create only), Warranty and no Overview item while the server keeps the dashboard closed to Sales

#### Scenario: Admin login
- **WHEN** `admin` signs in
- **THEN** Shell opens Overview and shows all eight destinations

#### Scenario: Manager login

- **WHEN** `manager1` signs in and preview is disabled
- **THEN** Shell opens Overview and shows Overview, Products, Categories and Inventory, Orders, Warranty, Customers, and Settings, without Return inspections. Overview uses the live Dashboard source

### Requirement: Action buttons follow the role
Write actions SHALL be visible only to roles the server allows: category add/edit/delete for Admin; stock import/adjust/delete and serial import for Admin and WarehouseStaff; order create and status change for Admin and SalesStaff; product create/edit/status/delete for Admin and Manager; customer create for Admin, Manager and SalesStaff; customer edit, deactivate/reactivate and history for Admin and Manager; warranty intake and return to customer for Admin, Manager and SalesStaff; warranty inspection and repair completion for Admin and WarehouseStaff; warranty replacement approval and rejection for Admin and Manager.

#### Scenario: Manager views inventory
- **WHEN** a Manager opens Categories and Inventory
- **THEN** stock and category data are shown without add, import, adjust or delete buttons

#### Scenario: Sales views customers
- **WHEN** a SalesStaff opens Customers
- **THEN** the add button is shown and edit, activity and history actions are not

#### Scenario: Warehouse views warranty
- **WHEN** a WarehouseStaff opens Warranty
- **THEN** claims are listed without the intake button, and a `Received` claim shows the inspection actions
