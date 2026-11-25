# Design

## Context

Lý do làm và phạm vi: xem `proposal.md`. Yêu cầu chi tiết: xem `specs/warranty-cases/spec.md` và `specs/frontend-warranty-workspace/spec.md`.

Hiện trạng sau PR #31 của An (`dev` tại `f5756b0`):

- **Thao tác kho, do An sở hữu:** `IInventoryAfterSaleService.ApplyWarrantyAsync(operationId, caseId, orderId, orderItemId, deviceId, action, replacementId, actorId)`, với `action` là `ReceiveForRepair`, `ReturnToCustomer` hoặc `Replace`. Hàm làm các việc sau:
  - khóa đơn, khóa mã thao tác (`inventory_operations`, gửi lại cùng mã thì trả kết quả cũ), khóa máy theo id tăng dần, rồi khóa tồn;
  - kiểm tra máy thuộc dòng bán (`OwnsSaleAsync`), và dòng không có khoản hoàn ngân hàng đang chờ;
  - ghi `serial_imeis.warranty_case_id` và `after_sale_quarantine`;
  - khi đổi máy: ghi movement `WarrantyReplacement`, đặt `sale_order_item_id` của máy thay bằng dòng gốc, trả `MovementId` và `UnitCost`. Máy thay hiện chỉ chép `warranty_start_at`/`warranty_end_at`, chưa chép `warranty_months`.
- **Hồ sơ tối giản, An làm thay phần của Q. Vinh:**
  - entity `WarrantyCase` và bảng `warranty_cases` với 3 trạng thái `UnderRepair`, `ReturnedToCustomer`, `Replaced`;
  - `WarrantyCaseService`: hàm nhận máy theo `orderId`/`orderItemId`/`deviceId`/`customerId`, và hàm chốt trả hoặc đổi;
  - `WarrantyCasesController` `/api/warranties`, chỉ cho A M;
  - 4 test PostgreSQL trong `PosCheckoutPostgresHttpTests.Warranty.cs` và một hồ sơ demo đã đóng trong `DemoSeedRepository.AfterSale.cs`.
- `serial_imeis.sale_order_item_id` chỉ có giá trị từ M4 trở đi. Máy bán từ M3 thì dùng `IInventoryAfterSaleRepository.FindLegacySaleAsync` (dòng có `completed_at` mới nhất).
- **Trả hàng của An:** yêu cầu trả (`return_cases`) có trạng thái `Pending`, `AwaitingBank`, `Refunded`, `Failed`.
  - Lúc **tạo yêu cầu**, hệ thống chưa kiểm tra trạng thái máy.
  - Lúc **chi tiền**, `ReceiveReturnAsync` bắt máy phải `Sold` và đúng dòng bán. Vì vậy máy đang bảo hành hoặc máy cũ đã đổi sẽ bị chặn ở bước chi.
- Client chưa có trang bảo hành. Shell đã có thêm mục "Kiểm định hàng trả" của An.

## Goals / Non-Goals

**Goals:**
- Một bảng, một API bảo hành. Mở rộng những gì An đã làm, không làm song song.
- Mỗi luồng bảo hành có đúng một nơi mở và commit transaction (`WarrantyCaseService`). Mọi thay đổi máy và tồn đi qua `ApplyWarrantyAsync`.
- Hồ sơ cũ và test cũ vẫn chạy sau migration, sau khi sửa theo API mới.

**Non-Goals:**
- Sửa luồng trả/hoàn của An.
- Kéo dài hạn bảo hành khi đổi máy.
- Thông báo cho khách, tính chi phí sửa chữa.

## Decisions

### D1. Tiếp quản và chuyển sang `backend/Warranty/`

- Chuyển `Entities/WarrantyCase.cs`, `Configurations/WarrantyCaseConfiguration.cs`, `Repositories/WarrantyCaseRepository.cs`, `Services/WarrantyCaseService.cs` và `WarrantyCasesController.cs` sang `backend/Warranty/`, namespace `TechStore.Api.Warranty.*`.
- Cập nhật `using` trong `Program.cs`, `DemoSeedRepository.AfterSale.cs` và test. Tên bảng, tên index và route giữ nguyên.
- `IInventoryAfterSaleService`, `IInventoryAfterSaleRepository`, `InventoryOperationLog` và `WarrantyAction` vẫn ở `backend/Inventory/` và vẫn của An.
- Lý do: kế hoạch M4 giao hồ sơ bảo hành cho Q. Vinh. Để trong `Inventory/` thì người sửa không rõ là ai.
- Phương án không chọn:
  - giữ nguyên vị trí: dễ đụng code An đang bảo trì;
  - tạo bảng mới song song: hai bảng, hai API, và `serial_imeis.warranty_case_id` sẽ trỏ lẫn lộn.

### D2. Vòng đời hồ sơ

```
Received --inspect Repair--> Repairing --repaired--> Repaired --return--> ReturnedToCustomer
   |                            ^
   |--inspect Replace--> PendingReplacement --replace--> Replaced
   |                        |
   |                        +--reject-replacement--> Repairing
   +--inspect Reject--> Rejected --return--> ReturnedToCustomer
```

| Bước | Gọi kho của An | Máy |
|---|---|---|
| receive | `ReceiveForRepair` | `Sold → UnderRepair` |
| inspect, repaired, reject-replacement | không gọi | giữ `UnderRepair` |
| replace | `Replace` | máy cũ `Defective`, máy thay `Sold` |
| return | `ReturnToCustomer` | `UnderRepair → Sold` |

- Hồ sơ **mở** là mọi trạng thái trừ `ReturnedToCustomer` và `Replaced`.
- Hai trạng thái đóng giữ tên của An, để không phải đổi dữ liệu cũ.
- Bảng chuyển trạng thái đặt trong lớp tĩnh `WarrantyCaseTransitions`, theo mẫu `OrderStatusTransitions`.
- `ApplyWarrantyAsync` cho phép `Replace` khi máy `inThisRepair`, nên đổi từ `PendingReplacement` không cần sửa thao tác kho.

### D3. Lưu trữ

- `warranty_cases` thêm các cột:
  - `case_code` (unique `idx_warranty_cases_code`);
  - `issue_description`, `contact_name`, `contact_phone`, `inspection_note`, `rejection_reason`.

  Các cột nội dung để nullable ở DB vì hồ sơ cũ không có dữ liệu này. API bắt buộc với hồ sơ mới.
- Dùng lại cột sẵn có, không thêm cột mới cho cùng ý nghĩa:
  - `closed_by_id` là người trả máy, hoặc người duyệt đổi;
  - `closed_at` là thời điểm đổi máy, Bình dùng làm ngày chi phí;
  - `movement_id` là liên kết tới biến động kho.
- Viết lại `ck_warranty_cases_outcome` cho 7 trạng thái:
  - các trạng thái mở không có trường đóng, máy thay, movement hay giá vốn;
  - `Rejected` bắt buộc có `rejection_reason`;
  - hai trạng thái đóng giữ quy tắc cũ.
- `ck_warranty_cases_dates` giữ nguyên.
- Thêm partial unique `uq_warranty_cases_open_device` trên `device_id` WHERE `status` thuộc 5 trạng thái mở. Đây là lớp chặn ở DB, ngoài `serial_imeis.warranty_case_id` của An.
- Bảng mới `warranty_case_events(id, case_id, from_status, to_status, actor_id, note, created_at)`, chỉ thêm, không sửa. Index `(case_id, created_at)` và `actor_id`.

### D4. Tìm lần bán và không lộ bằng chứng mua hàng

1. Tìm máy theo IMEI (đúng 15 chữ số) hoặc Serial, dùng unique index có sẵn.
2. Dòng bán: lấy `device.SaleOrderItemId`, nếu trống thì gọi `FindLegacySaleAsync(device.Id)`. Sau đó nạp đơn và kiểm tra `status IN (Completed, PartiallyRefunded)`.
   - Đây là cách An dùng trong `OwnsSaleAsync`. Không tự viết cách tìm thứ hai, nên tra cứu và thao tác kho luôn ra cùng một dòng.
   - Ca máy thay từng bán cho khách khác tự đúng, vì `Replace` gán `sale_order_item_id` vào dòng gốc.
3. Điều kiện:
   - đã trả hết: tổng `order_returns.quantity` của dòng lớn hơn hoặc bằng `quantity` (giống `ReturnedQuantityAsync` của An);
   - đang trả: có `return_cases` ở `Pending` hoặc `AwaitingBank` chứa dòng này. Nếu thiết bị tra cứu là máy thay được ghi trong hồ sơ `Replaced` của dòng bán, yêu cầu `Pending` cho máy cũ không cản bảo hành máy thay.
   - hạn bảo hành: so với `TimeProvider`.
4. Chưa chứng minh được lần bán thì DTO tra cứu để trống mã đơn, ngày mua, tên khách và lịch sử. Lý do: với khách lẻ, mã đơn chính là bằng chứng mua hàng.

### D5. Thứ tự khóa và transaction

| Thao tác | Thứ tự |
|---|---|
| receive | đọc máy và dòng bán (không khóa) → **khóa đơn** → **khóa hồ sơ** (`LockAsync` của An: advisory lock theo `caseId` rồi `FOR UPDATE`) → kiểm tra gửi lại → `ApplyWarrantyAsync(ReceiveForRepair)` (khóa máy, tồn) → kiểm tra lại điều kiện → thêm hồ sơ, sự kiện → save → commit |
| inspect, repaired, reject-replacement | **khóa hồ sơ** → kiểm tra trạng thái → cập nhật, thêm sự kiện |
| replace, return | **khóa đơn** → **khóa hồ sơ** → kiểm tra trạng thái và dòng bán → `ApplyWarrantyAsync` → cập nhật, thêm sự kiện |

- Khi cần cả đơn lẫn hồ sơ thì luôn khóa đơn trước, giống An. Thao tác chỉ khóa hồ sơ không bao giờ khóa đơn sau đó, nên không có vòng chờ.
- Khi chi tiền trả hàng, An cũng khóa đơn trước, nên chi tiền và tiếp nhận cùng một dòng sẽ xếp hàng ở dòng `orders`. Bên vào sau thấy máy không còn `Sold` hoặc dòng đã trả, và nhận 409.

### D6. Chống lặp và mã hồ sơ

- Mã chống gửi lặp lúc tiếp nhận là `caseId` do client tạo, như An đã làm. Không thêm `client_request_id`.
  - Gửi lại cùng `caseId` và cùng máy thì trả hồ sơ cũ với 200.
  - Cùng `caseId` mà khác máy thì trả 409.
- Mỗi lần gọi kho có một `operationId` mới.
  - Lần gửi lại của `replace`/`return` khi hồ sơ đã đóng cùng quyết định thì trả hồ sơ cũ, như `ResolveAsync` hiện nay.
- Mã hồ sơ `BH-yyyyMM-NNNN` lấy tháng theo UTC, sinh như `OrderCodeGenerator`.
  - Gặp `DuplicateKeyException` trên `idx_warranty_cases_code` thì rollback và thử lại, tối đa 5 lần, như checkout.
  - `DuplicateKeyException` trên `uq_warranty_cases_open_device` thì trả 409 `OpenCase`.

### D7. Ranh giới với code của An

- **Chỉ một sửa đổi trong code của An:** thêm `replacement.WarrantyMonths = device.WarrantyMonths` trong nhánh `Replace` của `InventoryAfterSaleService`, cùng một test trong `InventoryAfterSaleServiceTests`. Ghi rõ trong mô tả PR.
- **Không sửa** luồng trả hàng.
  - Việc tạo yêu cầu trả cho máy đang bảo hành vẫn được phép, nhưng sẽ bị chặn ở bước chi tiền. Phía bảo hành tự chặn chiều ngược lại (coi yêu cầu `Pending`/`AwaitingBank` là `PendingReturn`), nên không cần sửa `ReturnService`.
- 4 test bảo hành của An được sửa theo API mới: nhận máy bằng IMEI, đi qua kiểm định trước khi trả hoặc đổi. Mục đích của từng test giữ nguyên.
- `docs/M4_INVENTORY_HANDOFF.md`, mục "Q. Vinh · API tiếp nhận và chốt phiếu bảo hành", được viết lại theo API mới.

### D8. API, quyền và lỗi

- Route gốc `/api/warranties`:

  | Route | Quyền |
  |---|---|
  | `GET lookup`, `GET` (danh sách), `GET {caseId}` | A M S W |
  | `POST {caseId}/receive`, `POST {caseId}/return` | A M S |
  | `POST {caseId}/inspect`, `POST {caseId}/repaired` | A W |
  | `POST {caseId}/replace`, `POST {caseId}/reject-replacement` | A M |

- Đặt `[Authorize(Roles)]` trên từng action. Cập nhật baseline `ApiAuthorizationCoverageTests` (4 dòng cũ bỏ hoặc đổi, thêm dòng mới) và `docs/API_PERMISSIONS.md`.
- **Giá vốn máy thay** trả cho A M W, Sales nhận `null`.
  - Cùng danh sách role với `unitCost` của `GET /api/inventory/serials`.
  - Không dùng `FieldAccess.CanViewProductCost` (chỉ A/M), vì quy tắc đó dành cho `costPrice` của sản phẩm.
  - Controller tính `includeCost` rồi truyền vào service.
- **Lỗi điều kiện** trả 409 `ProblemDetails` có thêm trường `reasons: string[]`.
  - Lỗi trạng thái trả 409 có `currentStatus`.
  - Sai mã đơn hay sai khách không còn trả 404 như bản cũ, vì 404 khiến người dùng tưởng không có máy.
- **Tiếp nhận** trả 201 kèm `Location` khi tạo mới, và 200 khi gửi lại.
- **DTO:** `WarrantyCaseDto` chuyển sang record có tên trường, có thêm `caseCode`, các trường nội dung, `replacementUnitCost` và `events`. `WarrantyLookupDto` là DTO riêng cho tra cứu.
- **Số điện thoại trong tra cứu:** `customerPhone` dùng để điền sẵn liên hệ lúc tiếp nhận, nên chỉ trả cho A M S (các role tiếp nhận). WarehouseStaff nhận `null`, vì Thủ kho không đọc được API khách hàng (task 4.9).
- **Lỗi từ thao tác kho:** service không trả nguyên `Conflict.Message` của `ApplyWarrantyAsync` (câu tiếng Anh) ra API, mà đổi sang câu tiếng Việt theo từng bước (tiếp nhận, đổi máy, trả máy) (task 4.8).
- **Mã không tồn tại:** tiếp nhận với IMEI/Serial không có trong hệ thống trả 404, giống tra cứu (task 4.9).

### D9. Client

- Thêm `ShellDestination.Warranty` ("Bảo hành"), đặt sau "Kiểm định hàng trả" và mở cho cả bốn role. Không phải landing page.
- `RoleAccess` thêm `CanReceiveWarranty` (A M S), `CanInspectWarranty` (A W) và `CanDecideWarrantyReplacement` (A M).
- `WarrantyCaseListPage`, `WarrantyIntakeDialog` và `WarrantyCaseDetailDialog` viết bằng C# Markup theo mẫu của An (`OrderReturnsPage`) và mẫu phiếu nhập. ViewModel kế thừa `PageViewModel`. Client repository `IWarrantyRepository` ánh xạ DTO sang model.
- Hộp thoại tiếp nhận tạo `caseId` khi mở và giữ nó qua lỗi mạng.
- Hàm thuần `WarrantyCaseActions.For(status, role)` quyết định nút nào hiện. Có test headless.
- Chọn máy thay bằng `GET /api/inventory/serials?productId=&variantId=&status=InStock`.
- Dialog Skia dùng Popup và tạo nhóm thao tác khi trạng thái cần tới. Cách này tránh Uno 6.7 duyệt cây theme của tất cả form ẩn lúc mở chi tiết. Các bước tiếp nhận cũng tạo view khi chuyển bước.
- Các sự kiện View↔ViewModel mới (`IntakeRequested`, `CaseDetailRequested`, `CaseChanged`) được ghi vào danh sách ngoại lệ trong AGENTS.md.
- Hai hộp thoại bảo hành dùng chung lớp `WarrantyDialog`, tự dựng trên `Popup` có lớp phủ thay vì `ContentDialog`. Lý do: nội dung dài (lịch sử, chọn máy thay, chọn khách) cần cuộn trong khung rộng tới 640 px, và hộp thoại tiếp nhận nhúng `CustomerPickerView`.
- Ngoại lệ mới, đã ghi vào AGENTS.md: `Popup.Opened` gọi `ActivateAsync` của ViewModel; `Popup.Closed` gọi `Deactivate` và kết thúc `ShowAsync`. Hai handler này chỉ quản lý vòng đời, không đồng bộ dữ liệu với ViewModel.
- Lớp phủ co giãn theo `XamlRoot.Changed` (task 6.8).

### D10. Thời gian

- So hạn bảo hành bằng UTC theo `TimeProvider`.
- Mọi thời gian service tự ghi (`opened_at`, `closed_at`, thời gian sự kiện) SHALL được làm tròn xuống micro giây trước khi lưu và trả về (`utc.AddTicks(-(utc.Ticks % 10))`, cùng cách `ReturnService.Payout` dùng). PostgreSQL chỉ giữ micro giây, nên nếu không làm tròn, lần trả đầu (giữ 100 ns) sẽ khác lần gửi lại đọc từ DB. Lỗi này đã làm đỏ 2 test của An trên Linux và đã sửa ở bước 1.3.
- Client hiển thị theo giờ Việt Nam.

## Risks / Trade-offs

- [Đổi API của An (BREAKING)] → Chưa có client nào dùng, chỉ có 4 test và seed. Sửa chúng trong cùng PR, và mô tả PR liệt kê mọi file của An bị chạm.
- [Đụng code An đang bảo trì] → Chỉ sửa một dòng `warranty_months`, chuyển file, và sửa `using`. Rebase theo `dev` trước khi mở PR.
- [Hồ sơ cũ thiếu liên hệ, lỗi] → Cột nội dung để nullable. Client hiện "—". Migration sinh mã và sự kiện cho hồ sơ cũ.
- [Tạo yêu cầu trả cho máy đang bảo hành vẫn được] → Bước chi tiền chặn (409), nên không sai tiền hay kho. Bảo hành coi yêu cầu `Pending`/`AwaitingBank` là `PendingReturn`.
- [Hết máy cùng loại để đổi] → Hồ sơ chờ ở `PendingReplacement` hoặc chuyển sang sửa. Hoàn tiền nằm ngoài phạm vi.
- [Giá vốn máy thay null] → Lưu null, không thay bằng 0. Báo cáo hiện "chưa tính được".

## Migration Plan

1. Một migration `ExtendWarrantyCases`:
   - thêm cột và bảng sự kiện;
   - chuyển `UPDATE ... SET status = 'Received' WHERE status = 'UnderRepair'`;
   - sinh `case_code` cho hồ sơ cũ bằng `row_number()` theo tháng của `opened_at`;
   - sinh sự kiện tiếp nhận (`opened_at`, `opened_by_id`) và sự kiện đóng (`closed_at`, `closed_by_id`);
   - viết lại check constraint, rồi tạo unique index;
   - down migration đảo lại các bước trên.
2. Sao lưu DB local (`pg_dump`) trước khi chạy. DB local còn phải chạy 5 migration của An trước.
3. Seed demo của An được đặt thêm mã và liên hệ cho hồ sơ demo. Thêm hồ sơ ở các trạng thái khác, đi qua thao tác kho thật. Chạy lại seed không tạo trùng, và đối soát tồn bằng 0 dòng.
