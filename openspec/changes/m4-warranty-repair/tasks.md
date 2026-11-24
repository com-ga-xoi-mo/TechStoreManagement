# Tasks

## 1. Chuẩn bị

- [x] 1.1 Sao lưu DB local bằng `pg_dump`, rồi chạy `dotnet ef database update` để áp 5 migration của An. Kiểm tra `__EFMigrationsHistory` có `RecordBankRefundApproval`, và câu SQL đối soát tồn trả 0 dòng.
- [x] 1.2 Tạo nhánh `feat/m4-warranty-repair` từ `dev`. Chạy toàn bộ test backend và frontend để lấy mốc trước khi sửa. Ghi số test đạt vào mô tả PR.
- [x] 1.3 Sửa lỗi lệch thời gian trong `WarrantyCaseService` sẵn có: làm tròn `OpenedAt`/`ClosedAt` xuống micro giây (design D10). Trước đó 2 test bảo hành của An đỏ trên Linux vì lần trả đầu giữ 100 ns, còn lần gửi lại đọc từ PostgreSQL. Kiểm tra: 4/4 test `PosCheckoutPostgresHttpTests.WarrantyCase*` qua, toàn bộ backend 926/926.

## 2. Chuyển module và schema

- [x] 2.1 Chuyển `WarrantyCase`, `WarrantyCaseConfiguration`, `WarrantyCaseRepository`, `WarrantyCaseService` và `WarrantyCasesController` sang `backend/Warranty/` (namespace `TechStore.Api.Warranty.*`). Sửa `using` trong `Program.cs`, `DemoSeedRepository.AfterSale.cs` và test. Kiểm tra: build thành công; `dotnet ef migrations add Probe` sinh hàm `Up`/`Down` rỗng (snapshot chỉ đổi tên kiểu CLR), sau đó xóa probe bằng `dotnet ef migrations remove`; toàn bộ test như mốc 1.2.
- [x] 2.2 Mở rộng entity và configuration theo D3:
  - 7 trạng thái;
  - các cột `case_code`, `issue_description`, `contact_name`, `contact_phone`, `inspection_note`, `rejection_reason`;
  - viết lại `ck_warranty_cases_outcome`;
  - `idx_warranty_cases_code`, `uq_warranty_cases_open_device`;
  - entity và configuration `WarrantyCaseEvent` (`warranty_case_events`).

  Kiểm tra bằng test model (tên bảng, cột, index, check) theo mẫu `InventoryEntityTests`.
- [x] 2.3 Tạo migration `ExtendWarrantyCases`, có phần SQL tự viết theo Migration Plan: đổi `UnderRepair` thành `Received`, sinh mã, sinh sự kiện, rồi mới tạo constraint và index. Trong cùng task, sửa `EnsureDemoWarrantyCaseAsync` để hồ sơ demo có `case_code`, thông tin liên hệ và hai sự kiện (tiếp nhận, trả máy), vì trên DB trống seed chạy sau migration. Kiểm tra:
  - trên DB đã có dữ liệu của An: hồ sơ demo có mã `BH-202610-0001` và 2 sự kiện;
  - trên DB trống: seed chạy được và hồ sơ demo giống hệt;
  - `\d warranty_cases` và `\d warranty_case_events` đúng;
  - down migration rồi up lại chạy được;
  - mọi test xanh, **trừ 4 test trong `PosCheckoutPostgresHttpTests.Warranty.cs`**. Bốn test này được phép đỏ tới task 5.2, vì `ReceiveAsync` cũ chưa sinh mã hồ sơ (task 4.3 viết lại).
- [x] 2.4 Cập nhật `docs/database/schema.dbml`: `warranty_cases` mở rộng, `warranty_case_events`, giá trị `WarrantyReplacement` trong enum `inventory_movement_type`, ghi chú check và index. Kiểm tra tên trùng với migration, và file import được trên dbdiagram.io.
- [x] 2.5 Đổi `ReceiveWarrantyRequest` sang `code`, `orderCode`, `customerId`, `issueDescription`, `contactName`, `contactPhone`. Thêm request inspect, repaired, reject-replacement. Đổi `WarrantyCaseDto` sang record có tên trường, thêm trường mới và `events`. Thêm `WarrantyLookupDto`, `WarrantyCaseSummaryDto`, `WarrantyCaseEventDto` và hằng mã lý do. Kiểm tra build.

## 3. Repository

- [x] 3.1 Mở rộng `IWarrantyCaseRepository`:
  - `FindDeviceByCodeAsync`;
  - `HasOpenReturnRequestAsync(orderItemId)` (`return_cases` ở `Pending`/`AwaitingBank`);
  - `GetLastCaseCodeAsync`, `AddEvent`;
  - `SearchAsync` (lọc trạng thái, `q` tìm theo chữ không coi `%`/`_` là ký tự đại diện, phân trang, mới nhất trước);
  - `GetDetailAsync` kèm sự kiện;
  - `CasesForDeviceAsync` cho lịch sử của máy.

  Mọi hàm đọc dùng `AsNoTracking`. Lần bán dùng `SaleOrderItemId` và `IInventoryAfterSaleRepository.FindLegacySaleAsync` theo D4, không viết cách tìm thứ hai.

  Test PostgreSQL:
  - máy bán lại cho khách Y;
  - máy thay từng bán cho khách Z (phải ra dòng gốc);
  - máy M3 có `sale_order_item_id` trống;
  - tìm theo `%`/`_`;
  - `uq_warranty_cases_open_device` ném `DuplicateKeyException` đúng tên.
- [x] 3.2 Viết `FakeWarrantyCaseRepository` và mở rộng `FakeInventoryAfterSaleRepository`. Thêm bản giả `IInventoryAfterSaleService` cho unit test service: ghi lại lời gọi, cho phép giả lập conflict. Kiểm tra project test biên dịch.

## 4. Service

- [x] 4.1 Thêm `WarrantyCaseTransitions` và unit test cho mọi cặp trạng thái hợp lệ/không hợp lệ.
- [x] 4.2 Viết `LookupAsync` theo D4: đủ mã lý do, và không lộ thông tin khi chưa chứng minh lần bán. Unit test phủ các scenario của "Tra cứu", "Xác định lần bán", "Điều kiện" và "Chứng minh lần bán", gồm biên `now == warranty_end_at` và `now == warranty_start_at`.
- [x] 4.3 Viết lại `ReceiveAsync` theo D5–D6:
  - validation;
  - tìm máy theo `code`, khóa đơn, rồi khóa hồ sơ;
  - gửi lại cùng `caseId`;
  - gọi `ApplyWarrantyAsync(ReceiveForRepair)`;
  - kiểm tra lại điều kiện;
  - sinh mã, thử lại 5 lần;
  - hồ sơ `Received`, sự kiện đầu;
  - thời gian làm tròn micro giây theo D10, có unit test với đồng hồ giả lệch dưới micro giây.

  Lỗi trả mã lý do. Unit test cho 201, 200 khi gửi lại, 400, 409 kèm `reasons`, cùng `caseId` khác máy, rollback khi kho trả conflict, và thứ tự khóa.
- [x] 4.4 Viết `InspectAsync`, `MarkRepairedAsync` và `RejectReplacementAsync` (chỉ khóa hồ sơ, không gọi kho). Viết lại `ResolveAsync` thành `ReplaceAsync` (chỉ từ `PendingReplacement`, lưu giá vốn có thể null) và `ReturnAsync` (chỉ từ `Repaired`/`Rejected`). Mỗi bước ghi sự kiện. Unit test phủ các scenario còn lại trong `specs/warranty-cases/spec.md`: 404, 409 kèm `currentStatus`, gửi lại khi đã đóng cùng quyết định, rollback.
- [x] 4.5 `GetAsync`/`SearchAsync` nhận `includeCost`, che `ReplacementUnitCost` khi `false`. Unit test cho cả hai giá trị.
- [x] 4.6 Thêm `replacement.WarrantyMonths = device.WarrantyMonths` vào nhánh `Replace` của `InventoryAfterSaleService` theo D7, cùng test trong `InventoryAfterSaleServiceTests`. Kiểm tra toàn bộ test Inventory vẫn qua.
- [x] 4.7 Viết `backend/Warranty/README.md`: vòng đời, bảng gọi kho, thứ tự khóa D5, giới hạn, câu SQL chi phí bảo hành cho Bình (`replacement_unit_cost` của hồ sơ `Replaced` theo ngày `closed_at` giờ Việt Nam). Kiểm tra câu SQL chạy được trên DB local.

## 5. API, quyền và tài liệu

- [x] 5.1 Viết lại `WarrantyCasesController`: bảy route POST/GET theo D8, `[Authorize(Roles)]` từng action, `includeCost` theo A M W, 201 kèm `Location` hoặc 200, 409 kèm `reasons`/`currentStatus`. Cập nhật `ApiAuthorizationCoverageTests` và `docs/API_PERMISSIONS.md`. Kiểm tra test coverage quyền qua.
- [x] 5.2 Sửa 4 test trong `PosCheckoutPostgresHttpTests.Warranty.cs` theo API mới. Giữ đúng mục đích:
  - khách thành viên;
  - khách lẻ đổi máy và gửi lại;
  - đơn M3 hoàn một phần;
  - hoàn tiền tranh nhau.

  Sửa thêm chỗ `PosCheckoutPostgresHttpTests.Returns.cs` có dùng bảo hành. Kiểm tra cả bộ PostgreSQL qua.

  Khi `WarrantyCaseDto` có danh sách `events`, so sánh hai DTO theo từng trường thay vì `Assert.Equal` cả record (record so danh sách theo tham chiếu). Test `WarrantyCase_ConcurrentRefundOfSameSoldImei...` của An được giữ làm ca "chi tiền trả hàng tranh với tiếp nhận cùng IMEI".
- [x] 5.3 Thêm test HTTP + PostgreSQL:
  - ma trận 401/403 cho bốn role trên mọi route;
  - ba vòng đủ: sửa → trả; đề nghị đổi → từ chối → sửa → trả; đổi máy;
  - tra cứu không lộ mã đơn;
  - Sales nhận `replacementUnitCost = null`, Warehouse thấy giá.

  Sau mỗi vòng, câu SQL đối soát tồn trả 0 dòng, và không có đơn, hoàn hay điểm mới.
- [x] 5.4 Test giao dịch thực sự đồng thời:
  - hai tiếp nhận cùng máy với hai `caseId`: một 201, một 409;
  - hai lần duyệt đổi cùng hồ sơ: một 200, một 409, tồn chỉ giảm 1;
  - chi tiền trả hàng cho dòng đã `Replaced`: 409.
- [x] 5.5 Viết lại mục "Q. Vinh · API tiếp nhận và chốt phiếu bảo hành" trong `docs/M4_INVENTORY_HANDOFF.md` theo API mới. Kiểm tra ví dụ request/response khớp với test 5.3.

## 6. Client

- [x] 6.1 Thêm `ShellDestination.Warranty` sau "Kiểm định hàng trả", `RoleAccess.CanOpen` cho bốn role, và `CanReceiveWarranty`, `CanInspectWarranty`, `CanDecideWarrantyReplacement`. Cập nhật test headless menu/landing của bốn role theo delta `frontend-shell-navigation`:
  - Admin 8 mục;
  - Manager không có "Kiểm định hàng trả";
  - Warehouse có cả hai mục;
  - landing page không đổi.
- [x] 6.2 Thêm model, `IWarrantyRepository`/`WarrantyRepository` (lookup, receive, list, detail, inspect, repaired, replace, reject-replacement, return) và nhãn tiếng Việt cho trạng thái và mã lý do. Ánh xạ 400/401/403/404/409 (đọc `reasons`/`currentStatus`) và lỗi mạng. Test headless repository với transport giả.
- [x] 6.3 Thêm `WarrantyCaseActions.For(status, role)` và test cho mọi tổ hợp trạng thái × role.
- [x] 6.4 Viết `WarrantyCaseListViewModel` và `WarrantyCaseListPage`: tìm kiếm, lọc, phân trang, trạng thái trang, nút "Tiếp nhận" theo role. Test headless cho load, lọc, lỗi, thử lại, hủy khi rời trang.
- [x] 6.5 Viết `WarrantyIntakeDialogViewModel` và dialog: tạo `caseId` khi mở, bước tra cứu, rồi bước thông tin, điền sẵn liên hệ từ khách, giữ `caseId` qua lỗi mạng. Test headless cho máy không đủ điều kiện, gửi lại sau lỗi mạng (cùng `caseId`) và tiếp nhận xong.
- [x] 6.6 Viết `WarrantyCaseDetailDialogViewModel` và dialog: thông tin, lịch sử, nút theo `WarrantyCaseActions`, chọn máy thay `InStock` cùng sản phẩm/biến thể, xử lý 409 bằng cách tải lại. Test headless cho từng thao tác, hết máy cùng loại, Sales không thấy giá vốn.
- [x] 6.7 Đăng ký DI client và route trong `ShellRoutePageFactory`. Ghi sự kiện mới vào danh sách ngoại lệ và các view mới vào bảng trạng thái chuyển đổi trong `AGENTS.md`. Chạy app Skia với backend thật bằng bốn tài khoản demo, đổi Sáng/Tối khi đang mở trang, kiểm tra binding. Ghi kết quả (build, hệ điều hành, role, thao tác đã thử) vào `docs/M4_TEST_SUMMARY.md`, mục Bảo hành.

## 7. Dữ liệu demo và bàn giao

- [x] 7.1 Thêm hồ sơ demo `Received`, `Repairing`, `PendingReplacement` và `Replaced`, đi qua `ApplyWarrantyAsync` thật, trong kỳ demo. Kiểm tra chạy seed hai lần không tạo trùng, đối soát tồn bằng 0 dòng, và các test seed hiện có vẫn qua.
- [x] 7.2 Gửi Bình câu SQL chi phí bảo hành và JSON mẫu của một hồ sơ `Replaced` (có ca giá vốn null). Ghi ngày gửi vào README module.
- [ ] 7.3 Rebase lên `dev` mới nhất, chạy lại toàn bộ test, mở PR vào `dev`. Mô tả PR liệt kê các file của An bị chạm (chuyển `WarrantyCase*`, dòng `warranty_months`, 4 test và seed bảo hành). Kiểm tra PR không có conflict và CI xanh.
- [ ] 7.4 Sprint 3: chạy giao diện bảo hành trên Windows native và Skia với bốn role, quay đoạn video phần bảo hành, viết mục bảo hành trong README (chức năng, giới hạn, tài khoản demo). Xong khi có bằng chứng chạy trên cả hai nền tảng.

## Workflow follow-up

- Sau khi nghiệm thu, archive change bằng `/opsx:archive` và sync các delta spec.
