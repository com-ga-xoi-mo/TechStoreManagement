# Proposal

## Why

Identity Sprint 1 đã hoàn tất và archive; phần An còn lại trong M3 là kiểm chứng quyền/actor/phiên và ghép tài liệu bàn giao. Có thể chuẩn bị kiểm thử chạy lại được và bàn giao hiện trạng ngay trên `dev`, trong khi nghiệm thu M3 toàn luồng vẫn cần kết quả tích hợp của từng chủ module.

## What Changes

- Bổ sung kiểm thử HTTP đi qua JWT, services, repositories và PostgreSQL thật cho lifecycle tài khoản/phiên; tái sử dụng fixture và test có sẵn, thay việc phụ thuộc vào script nghiệm thu local không được track.
- Thêm kiểm tra độ phủ quyền theo method/route của endpoint đang được đăng ký, đối chiếu bảng quyền Sprint 1 hiện hành; kiểm tra policy hiệu lực và HTTP 401/403. Endpoint mới hoặc thay quyền cần được chủ module cập nhật contract và test tương ứng.
- Bổ sung bằng chứng ownership Orders/actor từ token trên dữ liệu test tự tạo, chỉ kiểm tra contract hiện có; không mở rộng luồng bán hàng.
- Chuẩn hóa README cách chạy, tài khoản demo, migrate/seed và hướng dẫn kiểm thử; sửa mô tả Orders chưa xác thực trong roadmap và lời nhắc tiếp tục change đã archive.
- Lập bản bàn giao hiện trạng M3 cho M4, có nguồn code/contract, người sở hữu, bằng chứng và phần chờ tích hợp. Ghi rõ trạng thái đơn, công thức tiền, thứ tự khóa hiện tại, không tự chốt contract tương lai thay chủ module.
- Tách kết quả kiểm chứng độc lập của An khỏi các điều kiện hoàn tất M3; chỉ đánh dấu các bước có bằng chứng đúng phạm vi.

## Capabilities

### New Capabilities

Không có. Đây là change kiểm thử và tài liệu, đặt `skip_specs: true`; không tạo requirement nghiệp vụ mới để hợp thức hóa kiểm thử.

### Modified Capabilities

Không có. Giữ nguyên các requirement đã sync của `admin-authentication`, `api-role-authorization`, `account-management`, `order-management` và `data-seeding`.

## Impact

- Nhánh `feat/m3-security-verification-handoff` từ `origin/dev` tại `4548b45`, sau PR #21 đã merge. Chủ phạm vi: An.
- Dự kiến sửa/thêm test trong `tests/Backend.UnitTests/`, tái sử dụng `PostgresDatabase`, `WebApplicationFactory<Program>` và cấu hình JWT production. Không thay auth bằng test scheme hoặc dùng fake repository cho bằng chứng PostgreSQL.
- Tài liệu: `README.md`, `backend/Identity/README.md`, `docs/API_PERMISSIONS.md`, `docs/MILESTONE_3_PLAN.md`, `docs/ROADMAP.md` và bản bàn giao mới `docs/M3_IDENTITY_INTEGRATION_HANDOFF.md`. Báo cáo Sprint 1 đã archive giữ nguyên nguồn bằng chứng lịch sử.
- Không đổi API/DTO, schema, migration, seed nghiệp vụ, package hoặc UI. Nếu phát hiện lỗi production, ghi rõ bằng chứng và chủ sở hữu; sửa lỗi nghiệp vụ ngoài phạm vi bằng change riêng.
- Loại trừ công việc Q. Vinh, Bình, Đạt, Vy, T. Vinh: nhập hàng, POS/ca, thuế/giảm giá, mở quyền dashboard/Customers, ẩn giá vốn, shell/UI và dữ liệu demo tổng thể. Không bao gồm nghiệp vụ M4.

Hoàn tất change này chỉ có nghĩa bộ kiểm chứng và tài liệu phần độc lập của An đã sẵn sàng; không đồng nghĩa An đã nghiệm thu toàn bộ M3.
