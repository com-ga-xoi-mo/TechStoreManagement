# Spec Delta

## ADDED Requirements

### Requirement: Compatible shared resource lookup
Shared resources SHALL giữ các public keys và kiểu giá trị mà consumer hiện tại sử dụng, cho phép consumer XAML và C# cùng đọc một bộ semantic resources. Thay đổi hình thức khai báo MUST NOT buộc các màn hình khác đổi resource references hoặc mất hỗ trợ theme.

#### Scenario: Legacy resource consumers continue working
- **WHEN** trang XAML hiện có hoặc trang C# lookup font, metric, brush, style hoặc icon bằng public key hiện hành
- **THEN** lookup trả đúng kiểu tài nguyên, style dependency được resolve, trang hiển thị mà không có missing-resource hoặc invalid-cast error

#### Scenario: Two consumers observe the same semantic theme
- **WHEN** một consumer XAML và một consumer C# dùng liên kết semantic theme resources tương đương, rồi người dùng đổi Light/Dark/System
- **THEN** cả hai cập nhật theo theme tại chỗ, giữ input/state và không cần dựng lại trang

#### Scenario: Canonical primary colors
- **WHEN** consumer dùng các semantic primary resources trong Light theme
- **THEN** màu thường, hover và pressed lần lượt là #0066FF, #0052CC và #0040A8 theo baseline hiện hành, bất kể consumer khai báo bằng XAML hay C#

### Requirement: Resources available before first presentation
App SHALL cung cấp shared resources cùng tài nguyên WinUI trước khi tạo UI đầu tiên. Việc mở lại trang hoặc đăng ký resource lặp MUST NOT sinh dictionary trùng, thay thứ tự ưu tiên hoặc làm mất default control styling.

#### Scenario: Cold start for every existing entry path
- **WHEN** khởi động mới vào gallery, Login hoặc Shell theo cấu hình và session hiện có
- **THEN** UI đầu tiên resolve được toàn bộ shared resources và default control styles mà không cần mở một trang khác trước

#### Scenario: Repeated initialization or navigation
- **WHEN** resource registration được gọi lại hoặc người dùng điều hướng nhiều lần
- **THEN** resource collection không tích lũy bản sao và cùng key tiếp tục có kiểu, giá trị theo theme và độ ưu tiên ổn định
