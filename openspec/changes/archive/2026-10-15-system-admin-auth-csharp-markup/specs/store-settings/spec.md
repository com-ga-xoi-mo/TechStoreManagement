# Spec Delta

## Purpose

Quản lý thông tin cấu hình cửa hàng TechStore (tên cửa hàng, địa chỉ, số hotline, mã số thuế, giờ làm việc) phục vụ in ấn hóa đơn và quản trị hệ thống.

## ADDED Requirements

### Requirement: Get Store Profile Settings
Hệ thống SHALL trả về thông tin cấu hình hiện tại của cửa hàng thông qua API dành cho quản trị viên.

#### Scenario: Truy vấn cấu hình cửa hàng thành công
- **WHEN** Quản trị viên gửi request `GET /api/settings/store`
- **THEN** Hệ thống trả về HTTP 200 OK kèm thông tin tên cửa hàng, địa chỉ, số điện thoại hotline, email và mã số thuế

### Requirement: Update Store Profile Settings
Hệ thống SHALL kiểm tra tính hợp lệ và cho phép Quản trị viên cập nhật thông tin cấu hình cửa hàng.

#### Scenario: Cập nhật cấu hình cửa hàng hợp lệ
- **WHEN** Quản trị viên gửi request `PUT /api/settings/store` với thông tin tên, địa chỉ, hotline hợp lệ
- **THEN** Hệ thống lưu trữ thay đổi và trả về HTTP 200 OK với thông tin đã cập nhật

#### Scenario: Cập nhật thất bại do thiếu thông tin bắt buộc
- **WHEN** Request cập nhật có tên cửa hàng rỗng hoặc số hotline không hợp lệ
- **THEN** Hệ thống trả về HTTP 400 Bad Request kèm chi tiết lỗi xác thực
