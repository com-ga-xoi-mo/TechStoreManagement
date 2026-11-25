# Design

## Context

- Baseline đối chiếu là `origin/dev` ở SHA `f5756b07afa366bab6690287fdc8fd28d9078aa4` (PR #31). SHA trên GitHub đã khớp ref local ngày 25/11/2026. Các nguồn nghiệp vụ được kiểm tra bằng `git show origin/dev:<path>`. API phần bán được đối chiếu trên nhánh hiện tại và change [m4-financial-report-sales-api](../2026-11-25-m4-financial-report-sales-api/design.md), vì change đó nằm ở nhánh báo cáo.
- Mục An, Q. Vinh, Bình, Đạt và Vy trong [kế hoạch M4](../../../../docs/MILESTONE_4_PLAN.md) phân công nguồn ghi. `OrderReturn.cs`, `InventoryMovement.cs`, `docs/database/schema.dbml` và [handoff hoàn tiền](../../../../docs/M4_REFUND_HANDOFF.md) mô tả dữ liệu đã chốt. Kiểm tra writer để phân biệt cột đã có với luồng ghi đã chạy được.

## Goals / Non-Goals

**Goals** — Bổ sung số hậu mãi có thể đối chiếu theo nguồn, ngày và kỳ. Giữ tương thích JSON cả khi chưa đủ dữ liệu cho một chỉ số.

**Non-Goals** — Logic hoàn tiền, chuyển tiền, giữ/nhập/xuất kho, ghi ca, định giá, điểm/voucher, migration/backfill, UI và Gemini. Bảo hành chỉ báo phần giá vốn máy thay có nguồn trên baseline. Chi phí sửa chữa khác cần nguồn riêng của Q. Vinh nếu sau này mở rộng.

## Decisions

### D1. Nguồn dữ liệu và bàn giao theo owner

| Owner / luồng | Đã có trên baseline và dùng được | Còn chờ hoặc giới hạn | Ai bàn giao / điều kiện chạy lại |
| --- | --- | --- | --- |
| An — Khoản hoàn | `OrderReturn`: `Id`, `OrderId`, `OrderItemId`, `CaseLineId`, `NetAmount`, `TaxAmount`, `DiscountAmount`, `RefundAmount`, `RefundMethod`, `RefundedAt`, `RefundPosSessionId`. `ReturnCaseLine.CaseId` nối mã lần hoàn. `ReturnCase.WasUndelivered` và `Order.CompletedAt` phân biệt trước/sau giao. Cash chốt đồng giao dịch. QR hoàn có đối soát thủ công, mã `BankReference` và thời điểm chi đã xác minh. | Dòng M3 có thể thiếu phân bổ hoặc mã lần hoàn. Dữ liệu chờ/thất bại có ở phiếu, chưa có dòng đã chi. Không có chứng từ thì giữ thiếu dữ liệu. | An xác minh chứng từ/phân bổ lịch sử khi có nguồn. Bình chạy lại ca legacy khi có dữ liệu đã xác minh, và ca QR hoàn thủ công khi có phiếu đã đối soát. |
| An — Kiểm định và nhập lại | `inventory_movements` loại `CustomerReturn`, `Id`, `OrderReturnId`, `QuantityChange`, `CreatedAt`. `IsRestocked`, `Disposition`, `InspectionId`, `InspectedAt` phản ánh trạng thái kiểm định. `OrderItem.UnitCost` là giá vốn gốc đã lưu. | Cờ trạng thái cũ thiếu movement không đủ bằng chứng nhập lại. Giá vốn gốc nullable. | An bàn giao movement, liên kết dòng trả và snapshot vốn hợp lệ. Bình kiểm tra lại dữ liệu thiếu khi có liên kết/chứng từ gốc. |
| Đạt — Tiền két và ca | Cash POS ghi `Order.PaidAt`, `PaymentMethod`, `PosSessionId`, `TotalAmount`, `CashTendered`, `ChangeGiven`. Dịch vụ ghi chi có `CashRefundPayout.RefundId` duy nhất theo `ReturnCase.Id`, `ShiftId`, `Amount`. `ReturnCase.RefundedAt` làm mốc thực chi. `PosSession` có tiền đầu ca, tổng bán Cash, tổng hoàn Cash, số dự kiến và số đếm lúc đóng. | Payout cũ có thể thiếu chứng từ ca hoặc liên kết. Tổng ca là số đối chiếu toàn ca, không phân rã theo kỳ báo cáo bằng số tổng tích lũy. | Đạt xác nhận dòng thu/chi ghi đúng một lần và ca bán/chi. Bình kiểm qua API ca, giao dịch thật và báo cáo trong fixture riêng. |
| Đạt — Thu QR | `VietQrTransaction` có `Status`, `Amount`, `ConfirmedAt`, `ConfirmedByUserId` hoặc cặp `Provider/ProviderTransactionId`, liên kết đơn/ca và ràng buộc một xác nhận mỗi đơn. Baseline có seed `Confirmed`, đọc xác nhận đã lưu và luồng hủy/hết hạn. | Chưa thấy luồng production tạo xác nhận thu, ghi doanh số QR vào ca hoặc xử lý callback/tiền tới muộn trên SHA đã kiểm. Seed và xác nhận hoàn ngân hàng thủ công không chứng minh luồng thu QR đầu cuối. | Đạt bàn giao luồng xác nhận thu và chứng từ callback/đối soát, ghi rõ thật hay sandbox. Bình chạy lại ca thu QR thật và thử lại callback khi luồng đó có trên build tích hợp. |
| Q. Vinh — Bảo hành | `WarrantyCase`: `Id`, `Status`, `OrderItemId`, `ReplacementDeviceId`, `ResolutionOperationId`, `MovementId`, `ReplacementUnitCost`, `ClosedAt`. API baseline ghi hồ sơ `Replaced`, movement `WarrantyReplacement` và vốn máy thay cùng transaction. | `ReplacementUnitCost` có thể thiếu. Chưa thấy nguồn riêng cho công sửa/phụ tùng. | Q. Vinh bàn giao fixture qua API xuất máy thay cùng movement và chi phí gốc. Xác minh lại giá vốn thiếu khi có chứng từ cung cấp, và ghi rõ chi phí sửa ngoài phạm vi hiện tại. |
| Vy — Số chụp bán | Giá, giảm giá và thuế đã lưu trên `OrderItem`; An ghi số tiền hoàn sau khi dùng phân bổ nguồn. | Không suy ra phân bổ lịch sử từ cấu hình, giá hiện hành, điểm/voucher hoặc công thức tính lại. | Vy giải thích dữ liệu nguồn nếu cần đối chiếu. Bình chỉ tổng hợp dữ liệu đã chốt. |

### D2. Hoàn theo thời điểm thực chi

Gom `order_returns` theo `RefundedAt`, mỗi ID dòng đã chi một lần. Nếu join phiếu mới để đọc `WasUndelivered` hoặc đối soát kênh, nối qua `CaseLineId → ReturnCaseLine.CaseId` với quan hệ nhiều dòng về một phiếu. Phiếu `Pending`, `AwaitingBank` hoặc `Failed` chưa đóng góp. Dòng legacy không có phiếu vẫn dùng `RefundAmount` và `RefundedAt` đã lưu. Bỏ các phiếu báo giá và khoản đã duyệt nhưng chưa xác nhận chi khỏi phép cộng.

- `refundPaidAmount` cộng `RefundAmount` mọi khoản đã chi. `refundTaxAmount` cộng `TaxAmount` của cùng tập, kể cả thuế hoàn trước giao. Hai số đều dương hoặc bằng 0 theo chứng từ.
- `refundedSalesExcludingTax` cộng `NetAmount` khi đơn có `CompletedAt <= RefundedAt` và không mang dấu hoàn trước giao (`WasUndelivered` hay `Disposition=Undelivered`). Dùng cả giao dịch giao ở kỳ trước. Đơn trước giao hoàn toàn bộ có dòng tiền đã chi, thuế hoàn nếu biết và 0 điều chỉnh doanh thu.
- Kiểm định có thể tách một dòng đã chi thành phần nhập, lỗi và còn chờ. Cộng các phần đã lưu, một lần mỗi ID. Không cộng thêm số báo giá `ReturnCaseLine` hoặc payout ca vào tổng tiền hoàn chung. Không tự phân bổ lại `DiscountAmount`, `NetAmount` hoặc `TaxAmount`. Nếu thiếu thành phần, khoản thực chi vẫn dùng được.

### D3. Giá vốn nhập lại và chi phí máy thay

- Đọc movement dương `CustomerReturn` có `OrderReturnId`. Nối movement → trả → dòng bán gốc theo cả ID đơn và dòng, kiểm tra liên kết đơn đã giao và loại khoản hoàn trước giao. Gom mỗi movement theo `CreatedAt` và cộng `QuantityChange × OrderItem.UnitCost`. Giá vốn gốc không lấy từ giá hiện tại. Ngày hoàn, ngày kiểm định và `IsRestocked` không thay ngày hay lượng thực nhập.
- Chỉ lượng đã vào tồn bán được qua movement mới làm giảm vốn. Hàng lỗi, đang sửa, còn chờ hoặc giải phóng giữ hàng sau hoàn trước giao không phát sinh giảm vốn. Cờ `IsRestocked=true` thiếu movement không tạo bút toán. Ghi giới hạn lịch sử ở tài liệu nghiệm thu. Movement đủ điều kiện nhưng thiếu giá vốn giữ số lượng khi đối chiếu và làm giá trị giảm vốn của nhóm thành `null`.
- Bảo hành tính theo `WarrantyCase.Status=Replaced` nối `MovementId` tới movement xuất `WarrantyReplacement` âm cùng dòng bán. Dùng `Movement.CreatedAt` vì đây là ngày xuất máy và dùng `ReplacementUnitCost × abs(QuantityChange)` đã lưu. Baseline ghi đúng một máy mỗi hồ sơ và unique `MovementId`. Giá vốn máy thay rỗng làm `warrantyCost` của nhóm là `null`. Nhận sửa và trả máy cũ không ghi chi phí máy thay. Không cộng movement bảo hành vào giá vốn thuần, không trừ vào lãi gộp, không tạo bán hoặc hoàn.

### D4. Tiền theo kênh và ca

- Cash thu theo `PaidAt` từ đơn Cash có `PosSessionId`, lấy `TotalAmount` một lần mỗi đơn. Trong POS, số này khớp tiền trao trừ tiền thối. Đơn Cash ngoài ca vẫn có thể thuộc `paymentReceivedAmount` gốc. Không gán các đơn đó cho két.
- Cash chi từ `cash_refund_payouts` theo `RefundId` duy nhất, mốc `ReturnCase.RefundedAt` của phiếu Cash đã chốt, khóa nhóm theo `ShiftId` của người chi. Đối chiếu payout với tổng các dòng hoàn Cash cùng phiếu và ca. Nếu khoản hoàn Cash cần chứng từ ca nhưng không có hoặc lệch số/ca, không lấy tổng dòng hoàn thay payout. Chỉ số chi ngày/kỳ của nhóm bị ảnh hưởng là `null`. Với ca xác định được, chỉ số chi và dòng tiền thuần của ca cũng là `null`. Khoản không xác định ca không được gán ca tùy ý.
- QR thu theo `ConfirmedAt` và `Amount` của giao dịch `Confirmed` có chứng từ và khớp đơn VietQR, một giao dịch mỗi đơn. Không dùng `PaidAt` làm giờ ngân hàng. QR hoàn theo `RefundedAt` từ các dòng `RefundMethod=VietQr` có phiếu đã đối soát với mã tham chiếu. Khoản có dấu đã thu/chi nhưng thiếu chứng từ cần thiết làm chỉ số QR tương ứng chưa biết. Không tính QR giữ chỗ, chờ, thất bại, hết hạn hoặc tiền tới muộn chưa đối soát. Không cộng QR hoặc Card vào két.
- `paymentReceivedAmount` tiếp tục là tổng thu gốc theo `PaidAt` của change 1. Số theo QR xác nhận và Cash ca là những phép đo riêng có thể khác về tập giao dịch hoặc ngày. Không cộng chúng thêm vào tổng thu gốc.
- `cashByShift` ở cấp response, chỉ có ca với sự kiện Cash trong kỳ và sắp theo `posSessionId` tăng dần. Mỗi dòng có `posSessionId`, `cashReceivedAmount`, `cashRefundPaidAmount`, `cashNetMovement` (thu trừ chi), dựa trên sự kiện trong kỳ. Đối chiếu ca trọn kỳ với tiền đầu ca + thu Cash − chi Cash và số dự kiến đã lưu, tách số kiểm đếm thực tế. Kỳ cắt ngang ca chỉ báo dòng tiền trong cửa sổ, không suy ra số dư đầu/cuối.

### D5. Tương thích JSON, tổng hợp và giá trị chưa biết

Điền bảy trường nullable đã công bố trong `summary` và `daily[].totals`. Bổ sung các trường kiểu decimal nullable `refundTaxAmount`, `cashReceivedAmount`, `cashRefundPaidAmount`, `vietQrReceivedAmount`, `vietQrRefundPaidAmount` và mảng `cashByShift` mô tả ở D4. Dùng response projection có kiểu trong Analytics, giữ tên camelCase và các trường hiện hành ở đúng cấp để client dùng DTO cũ vẫn đọc được; không sửa shared DTO. Endpoint, RBAC Admin/Manager, kỳ custom, bảng top, hiệu suất bán và Dashboard giữ hợp đồng hiện hành.

- Dùng số decimal VND đã lưu và cùng snapshot `RepeatableRead`. Lọc từng nguồn theo mốc sự kiện gốc trong `[fromUtc, toExclusiveUtc)` của change 1 và chỉ đổi UTC+07 lúc nhóm. Tổng hợp từng tập độc lập theo ngày hoặc ca rồi mới ghép. Việc join trực tiếp thu với chi, nhiều movement hay nhiều dòng phân bổ có thể nhân số tiền.
- Vốn thuần = vốn bán − vốn thực nhập lại. Doanh thu thuần chưa thuế = bán trước thuế − tiền hàng hoàn của đơn đã giao. Lãi gộp = doanh thu thuần − vốn thuần. Ngày chỉ nhập lại có thể có vốn thuần âm, ngày chỉ hoàn có thể có doanh thu thuần âm. Ngày trống đủ nguồn trả 0. Không làm tròn lại hay ép số âm về 0.
- Cờ thiếu dữ liệu tách khỏi tổng các giá trị biết được. Nếu nguồn chưa khả dụng cho chỉ số, hoặc có sự kiện đủ điều kiện thiếu đầu vào, trả `null` cho chỉ số ở nhóm bị ảnh hưởng. Một ngày thiếu làm tổng kỳ của chỉ số đó `null`, và công thức phụ thuộc lan `null`. Nhóm khác và số độc lập vẫn đọc được. Tách trạng thái dữ liệu trên API khỏi trạng thái thực thi test. Fixture `Confirmed` dùng được để thử Analytics dù chưa thể xác nhận thu QR đầu cuối.

### D6. Expected tính tay và bằng chứng

Fixture mở rộng R1/C1/C4 trong handoff của An. Đơn A giao và thu Cash 28/10, hai món có giá vốn 600.000đ/món, doanh thu chưa thuế 1.800.000đ và thuế 180.000đ. Hoàn Cash một món lúc `2026-10-29T16:55:00Z` vào ca Manager. Thực nhập một món lúc `2026-10-29T17:05:00Z`. Hoàn nốt món lỗi qua QR đã đối soát ngày 31/10, giữ vốn của món lỗi. Thêm máy thay 500.000đ xuất ngày 30/10 trên một đơn cũ khác và một ca trả máy sau sửa. Đơn B đã thanh toán QR 550.000đ (500.000 tiền hàng, 50.000 thuế), chưa giao và được hoàn toàn bộ QR hôm sau. Gửi lại cùng mã lần hoàn, kiểm định và xuất máy, so sánh ở cùng cửa sổ dữ liệu sau mỗi lần; metadata `asOf` có thể khác.

| Ngày UTC+07 | Bán chưa thuế | Vốn bán | Thực chi hoàn | Thuế hoàn | Tiền hàng giảm doanh thu | Vốn nhập lại | Doanh thu thuần | Vốn thuần | Lãi gộp | Bảo hành |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 28/10 | 1.800 | 1.200 | 0 | 0 | 0 | 0 | 1.800 | 1.200 | 600 | 0 |
| 29/10 | 0 | 0 | 990 | 90 | 900 | 0 | -900 | 0 | -900 | 0 |
| 30/10 | 0 | 0 | 0 | 0 | 0 | 600 | 0 | -600 | 600 | 500 |
| 31/10 | 0 | 0 | 1.540 | 140 | 900 | 0 | -900 | 0 | -900 | 0 |
| Cả kỳ | 1.800 | 1.200 | 2.530 | 230 | 1.800 | 600 | 0 | 600 | -600 | 500 |

Đơn vị bảng là nghìn đồng; API trả VND. Cash ca bán thu 1.980.000đ, ca Manager chi 990.000đ; Manager mở 2.000.000đ thì dự kiến đóng 1.010.000đ. QR thu fixture 550.000đ ngày 30/10, chi 1.540.000đ ngày 31/10 và không ảnh hưởng hai ca. Xác minh kỳ hẹp riêng ngày 29 hoặc 30 vẫn đọc được hoàn/nhập của đơn giao hôm 28. Kiểm tra thêm lô tách khi kiểm định, legacy thiếu phân bổ, giá vốn nhập lại và máy thay thiếu/0, cắt tại biên UTC+07 và `asOf`. Các số đã biết ở từng ngày cộng đúng về tổng kỳ.

Append Expected/Actual, mã ca, phạm vi dữ liệu, SHA build và kết quả vào `docs/M4_FINANCIAL_REPORT.md`. Với nguồn chưa chạy được qua luồng thật, ghi **Chưa kiểm tra**, owner và tiêu chí chạy lại ở D1. Ca báo cáo dùng fixture riêng có kết quả độc lập, không thay bằng chứng đầu cuối của owner.

## Risks / Trade-offs

- Dòng đã chi bị tách sau kiểm định, một phiếu có nhiều dòng và một khoản chi ca → tổng hợp mỗi tập theo khóa sự kiện riêng, so số trước và sau tách/gửi lặp.
- Giá vốn lịch sử hoặc chứng từ kênh thiếu → giữ số độc lập, trả `null` cho chỉ số thiếu và ghi rõ giới hạn trong bằng chứng.
- PR khác đã đổi KPI Dashboard → dùng Dashboard trên baseline hiện hành làm hồi quy. Báo cáo tài chính chỉ thay phần hậu mãi và dòng tiền mới.

## Migration Plan

Khi apply, đối chiếu `origin/dev` mới nhất và giữ cả baseline nghiệp vụ lẫn change phần bán trên nhánh triển khai. Sử dụng migration sẵn có trên PostgreSQL test. Thay đổi Analytics chỉ đọc dữ liệu và thêm các trường JSON tương thích. Rollback bằng cách hoàn nguyên change Analytics nếu cần.
