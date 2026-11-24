# Proposal

## Why

Milestone 4 giao cho Q. Vinh phần bảo hành và sửa chữa (`docs/MILESTONE_4_PLAN.md`, mục "Q. Vinh: Bảo hành và sửa chữa"). PR #31 của An đã có sẵn hai thứ:
- thao tác kho cho bảo hành: `IInventoryAfterSaleService.ApplyWarrantyAsync`;
- một bản hồ sơ tối giản: bảng `warranty_cases` và API `/api/warranties` cho Admin/Manager, gồm nhận máy, trả máy và đổi máy.

Bản này chưa đáp ứng kế hoạch M4 ở các điểm sau:
- chưa tra cứu được theo IMEI hay theo mã đơn của khách lẻ;
- chưa có bước kiểm định, chưa từ chối được lỗi do khách, chưa tách "đề nghị đổi" khỏi "duyệt đổi";
- chưa có lịch sử, danh sách hồ sơ hay thông tin liên hệ;
- Sales chưa tiếp nhận được;
- chưa trả giá vốn máy thay cho báo cáo;
- chưa có giao diện.

## What Changes

- **Q. Vinh tiếp quản hồ sơ bảo hành của An.** Chuyển `WarrantyCase`, configuration, repository, service và controller từ `backend/Inventory/` sang module mới `backend/Warranty/`. Bảng `warranty_cases` và API gốc `/api/warranties` giữ nguyên tên. Thao tác kho `ApplyWarrantyAsync` vẫn là của An, được gọi như hiện tại.
- **Vòng đời đủ bước**, đúng kế hoạch:
  - `Received → Repairing | PendingReplacement | Rejected`;
  - `Repairing → Repaired`;
  - `PendingReplacement → Replaced` (duyệt) hoặc `→ Repairing` (từ chối đổi);
  - `Repaired | Rejected → ReturnedToCustomer`.

  Hai trạng thái đóng `ReturnedToCustomer` và `Replaced` giữ tên cũ. Hồ sơ cũ đang `UnderRepair` được chuyển sang `Received`.
- **Tra cứu theo IMEI/Serial**:
  - lần bán đang bảo hành lấy theo `serial_imeis.sale_order_item_id`, và dùng cách tìm cho dữ liệu M3 của An khi cột này trống;
  - khách lẻ phải có mã đơn;
  - chưa chứng minh được lần bán thì không lộ mã đơn hay tên khách;
  - các lý do không đủ điều kiện có mã riêng.
- **Hồ sơ lưu thêm**: mã hồ sơ `BH-yyyyMM-NNNN`, lỗi khách báo, tên và số điện thoại liên hệ, kết luận kiểm định, lý do từ chối. Có thêm bảng sự kiện `warranty_case_events` (chỉ thêm, không sửa) và unique index "một máy chỉ có một hồ sơ mở".
- **BREAKING (API `/api/warranties` của PR #31):**
  - `POST /{caseId}/receive` nhận IMEI/Serial, mã đơn hoặc khách và thông tin liên hệ, thay cho `orderId`/`orderItemId`/`deviceId`. Trả 201 khi tạo mới, 200 khi gửi lại cùng mã hồ sơ.
  - `POST /{caseId}/return` chỉ hợp lệ ở `Repaired` hoặc `Rejected`.
  - `POST /{caseId}/replace` chỉ hợp lệ ở `PendingReplacement`.
  - DTO hồ sơ có thêm các trường mới.
  - Lỗi điều kiện trả 409 kèm `reasons`, không còn 404.

  Hiện chưa có client nào gọi các API này. Bốn test bảo hành và dữ liệu demo của An được cập nhật theo.
- **API mới:**
  - `GET /api/warranties/lookup`;
  - `GET /api/warranties` (danh sách);
  - `POST /{caseId}/inspect`;
  - `POST /{caseId}/repaired`;
  - `POST /{caseId}/reject-replacement`.
- **Quyền:**
  - A M S W: tra cứu, danh sách, chi tiết;
  - A M S: tiếp nhận, trả máy;
  - A W: kiểm định, báo sửa xong;
  - A M: duyệt hoặc từ chối đổi máy;
  - giá vốn máy thay trả cho A M W, Sales nhận `null`.
- **Thao tác kho:** máy thay chép thêm `warranty_months` của máy cũ, không chỉ hai ngày bảo hành. Đây là một dòng trong code của An, ghi rõ trong mô tả PR.
- **Client:** mục menu **"Bảo hành"** cho cả bốn role, gồm trang danh sách, hộp thoại tiếp nhận và hộp thoại chi tiết (lịch sử, nút theo trạng thái và role, chọn máy thay).
- Ngoài phạm vi:
  - hoàn tiền khi không sửa được và hết hàng để đổi;
  - chi phí sửa chữa;
  - bảo hành của hãng;
  - in phiếu;
  - bắt buộc nhập khách khi bán.

## Capabilities

### New Capabilities

- `warranty-cases`: tra cứu điều kiện, tiếp nhận, kiểm định, sửa, đổi máy, trả máy, lịch sử, danh sách và chi phí máy thay qua `/api/warranties`.
- `frontend-warranty-workspace`: mục "Bảo hành" trên client, gồm danh sách, hộp thoại tiếp nhận và hộp thoại chi tiết hồ sơ.

### Modified Capabilities

- `api-role-authorization`: thay requirement "Warranty case authorization" (hiện chỉ A/M) bằng bảng quyền mới, và thêm quy tắc xem giá vốn máy thay.
- `inventory-fulfillment`: requirement "Thao tác thiết bị cho bảo hành" nêu rõ máy thay nhận cả `warranty_months`, và người tiếp nhận theo bảng quyền mới.
- `database-schema`: `inventory_movement_type` có `WarrantyReplacement` (đã có trong code). Thêm requirement lưu hồ sơ bảo hành mở rộng và bảng sự kiện.
- `frontend-shell-navigation`: Shell có tám điểm vào, gồm "Kiểm định hàng trả" của An và "Bảo hành"; menu và nút theo role.

## Impact

- **Backend:**
  - module mới `backend/Warranty/`, nhận các file `WarrantyCase*` chuyển từ `backend/Inventory/`;
  - sửa `Program.cs` và các `using` trong `DemoSeedRepository.AfterSale.cs` cùng test;
  - đọc `IInventoryAfterSaleRepository` (lần bán M3, hoàn ngân hàng đang chờ) và gọi `ApplyWarrantyAsync`, không cập nhật kho trực tiếp;
  - sửa một dòng trong `InventoryAfterSaleService` để chép `warranty_months`.
- **Shared:** `ReceiveWarrantyRequest` đổi trường; thêm request/DTO tra cứu, kiểm định, danh sách, sự kiện; `WarrantyCaseDto` có thêm trường.
- **Schema:** một migration mở rộng `warranty_cases`, gồm:
  - cột mới;
  - đổi `UnderRepair` thành `Received`;
  - sinh mã cho hồ sơ cũ;
  - viết lại check constraint;
  - thêm partial unique index;
  - tạo bảng `warranty_case_events` và sinh sự kiện cho hồ sơ cũ.

  Cập nhật `docs/database/schema.dbml` theo.
- **Client:** `ShellDestination.Warranty`, `RoleAccess`, page, dialog, ViewModel và client repository bảo hành.
- **Tests:**
  - unit test service;
  - HTTP + PostgreSQL thật cho từng luồng, quyền và giao dịch đồng thời;
  - sửa 4 test bảo hành sẵn có trong `PosCheckoutPostgresHttpTests.Warranty.cs` theo API mới;
  - test headless cho client.
- **Docs:** `docs/API_PERMISSIONS.md`, `docs/M4_INVENTORY_HANDOFF.md` (mục API bảo hành), `backend/Warranty/README.md`.
- **Phối hợp:**
  - gửi Bình cách đọc chi phí bảo hành (`replacement_unit_cost` của hồ sơ `Replaced`, theo `closed_at`).
