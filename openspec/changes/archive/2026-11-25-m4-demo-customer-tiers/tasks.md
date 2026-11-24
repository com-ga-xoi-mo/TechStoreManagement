# Tasks

## 1. Test trước (TDD)

- [x] 1.1 `DemoSeedCustomerTierTests`: hạng của mọi khách seed bằng `LoyaltyRules.TierFor(điểm)`; số dư ban đầu của mười khách giữ nguyên. Chạy trước khi sửa: bài hạng đỏ vì sáu khách lệch, bài số dư đạt.

## 2. Cài đặt

- [x] 2.1 `DemoSeedManifest.BuildCustomers`: hạng theo điểm (Tiêu chuẩn dưới 500, Bạc từ 500).

## 3. Kiểm tra

- [x] 3.1 Backend unit `DemoSeed*` 17/17 sau khi sửa.
- [x] 3.2 `dotnet build TechStore.sln` 0 lỗi 0 cảnh báo; backend unit 975 đạt, 80 bỏ qua (cần PostgreSQL), 0 lỗi; frontend unit 1312/1312.
- [ ] 3.3 Chưa chạy lại: test seed trên PostgreSQL thật (Docker tắt lúc sửa). Thay đổi chỉ đổi cột hạng, không có ràng buộc DB liên quan.

## 4. Quyết định

- [x] 4.1 Sửa hạng (hướng 2); không báo lead thay cho việc sửa, không thêm khách Vàng/Bạch kim.
