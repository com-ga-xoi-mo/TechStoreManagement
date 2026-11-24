# Đối chiếu hoàn tiền M4

Các số dưới đây là kết quả kỳ vọng cho fixture tích hợp của An, Đạt, Vy và Bình. Đơn vị tiền là đồng, mốc thời gian lưu UTC và ngày báo cáo dùng Asia/Ho_Chi_Minh.

## Dữ liệu Bình đọc

- Theo dòng đã chi, nối `OrderReturn.CaseLineId → ReturnCaseLine.Id` để lấy mã lần hoàn `CaseId`. `OrderReturn.OrderItemId` trỏ đến dòng bán gốc. Khoản M3 có thể thiếu mã lần hoàn và chi tiết phân bổ.
- Dùng `OrderReturn.NetAmount` cho tiền hàng sau cả hai loại giảm giá, `DiscountAmount` cho tổng giảm giá dòng và phần giảm cấp đơn, `TaxAmount` cho thuế. `RefundAmount = NetAmount + TaxAmount`. `RefundMethod` và `RefundedAt` xác định kênh và ngày chi. Khi một lô được tách để kiểm định, cộng các dòng đã chi theo `CaseLineId`.
- Dùng `InventoryMovement` loại `CustomerReturn`, nối `OrderReturnId` đến dòng trả, lấy `QuantityChange` và `CreatedAt` của movement. Giá vốn thực nhập là `QuantityChange × OrderItem.UnitCost` của dòng bán gốc. Tổng hợp khoản chi và movement riêng trước khi ghép theo lần hoàn để giữ đủ cả khoản chưa có movement.
- Khi `UnitCost` gốc là null, giữ số lượng thực nhập và đánh dấu thiếu giá vốn cho số lượng đó. Giá vốn và lãi gộp liên quan chưa xác định. Fixture này chỉ bàn giao giá vốn cho báo cáo nội bộ.

### R1 — Hoàn sau khi giao, nhập lại vào ngày kế tiếp

Đơn R1 giao hai đơn vị lúc `2026-10-28T02:00:00Z`, giá bán 1.000.000 mỗi đơn vị, giá vốn 600.000 mỗi đơn vị, giảm cấp đơn 200.000, thuế 180.000. Sale ghi tiền hàng sau giảm 1.800.000, thực thu 1.980.000 và 18 điểm cho khách đã đăng ký.

| Mốc | Dữ liệu kỳ vọng | Ảnh hưởng báo cáo |
| --- | --- | --- |
| R1-A hoàn Cash một đơn vị, `RefundedAt = 2026-10-29T16:55:00Z` | Lượng 1, tiền hàng 900.000, giảm 100.000, thuế 90.000, chi 990.000. Ca ghi người duyệt chi, số điểm còn lại là 9. | Ngày 29/10 giờ Việt Nam, doanh thu giảm 900.000, thuế giảm 90.000. Lũy kế còn doanh thu 900.000, vốn 1.200.000, lãi −300.000. |
| Kho nhập lại R1-A, `CreatedAt = 2026-10-29T17:05:00Z` | Một movement CustomerReturn, lượng +1, trỏ tới dòng trả đã đánh dấu IsRestocked. | Ngày 30/10 giờ Việt Nam, vốn giảm 600.000. Lũy kế vốn còn 600.000 và lãi 300.000. |
| R1-B hoàn đơn vị còn lại ngày 31/10 | Tiền hàng 900.000, giảm 100.000, thuế 90.000, chi 990.000. Lượng còn lại 0, điểm còn lại 0. | Hai lần chi cộng đúng 1.980.000, gồm 1.800.000 tiền hàng và 180.000 thuế. |

Hai lần ghi nhận đầu cách nhau mười phút và nằm trong hai ngày báo cáo. Gửi lại cùng mã R1-A hoặc cùng mã kiểm định phải giữ nguyên mỗi tổng tương ứng.

### R2 — Một lô có nhiều kết quả kiểm định

Đơn R2 có bốn đơn vị cùng giá bán và giá vốn như R1, giảm cấp đơn 400.000, thuế 360.000. Ngày 29/10 trả ba trong bốn đơn vị. Lần chi R2-A có lượng 3, tiền hàng 2.700.000, giảm 300.000, thuế 270.000, thực chi 2.970.000. Ngày 30/10, một đơn vị bán lại được, một đơn vị lỗi và một đơn vị chờ xử lý. Sau khi tách dòng, giữ nguyên các tổng của R2-A trên ngày 29/10. Chỉ có một movement CustomerReturn +1 và giảm vốn 600.000 ngày 30/10.

Chạy lại R2 dưới một mã đơn mới, đặt `OrderItem.UnitCost = null` ngay từ lúc bán. Kỳ vọng toàn bộ lượng, tiền, thuế, ca chi và thời điểm như R2. Báo cáo ghi một đơn vị nhập lại thiếu giá vốn. Phải có thể phân biệt trường hợp này với một đơn vị có giá vốn gốc bằng 0.

## Đạt và Vy — Ca chi và điểm

Dùng mã lần hoàn bền vững cho mọi dòng của mỗi lượt. Ca gốc bán hàng có thể đã đóng. Cash cần khóa ca đang mở của chính người chi trước khóa đơn. Cùng giao dịch chốt, ghi mã lần hoàn, mã ca, người chi, số tiền và chuyển cho Vy mã lần hoàn, mã đơn, khách cùng số tiền hàng còn giữ chưa thuế. Đóng ca dùng số đầu ca cộng bán Cash và trừ các khoản hoàn Cash đã chốt trong ca đó. Khi ngân hàng đã chi, chứng từ đối soát gồm mã tham chiếu, số tiền thực chi và thời điểm ngân hàng đã chi. Server lưu `bankApprovedAt` khi duyệt chuyển khoản. Chứng từ hợp lệ có `bankApprovedAt ≤ paidAt ≤ thời điểm đối soát`. Nếu thời gian làm tròn trên chứng từ rơi trước mốc duyệt, cần tra giờ chi tiết. Với phiếu chờ từ phiên bản cũ thiếu mốc duyệt, quản lý đối chiếu chứng từ duyệt gốc rồi nhập `verifiedLegacyApprovalAt`. Hệ thống lưu mốc đã xác minh cùng kết quả chi. Báo cáo dùng thời điểm thực chi. Khi ngân hàng từ chối, gửi số tiền thực chi bằng 0.

| Fixture | Điều kiện và thao tác | Kết quả kỳ vọng |
| --- | --- | --- |
| C1 | Manager M có ca riêng mở với số đầu 2.000.000, doanh số Cash trong ca bằng 0. Người bán đơn R1 là S. M duyệt R1-A. | 990.000 gắn ca của M, người chi M. Số dự kiến đóng 1.010.000. Ca S giữ nguyên. Gửi lại R1-A không đổi số ca hoặc điểm. |
| C2 | M chưa mở ca hoặc ca đã đóng. S có ca mở. M duyệt R1-A bằng Cash. | Conflict, yêu cầu chưa chi, không ghi khoản hoàn, biến động kho hay điểm. M mở ca riêng rồi thử lại cùng yêu cầu. Kết quả như C1. |
| C3 | Khách lẻ, CustomerId null, cùng giá/thuế như R1. | R1-A chi 990.000, tiền hàng còn giữ 900.000. Không sinh bản ghi điểm. |
| C4 | Đơn Processing đã thanh toán 1.980.000, chưa giao và chưa cộng điểm, đã giữ hai đơn vị. | Từ chối yêu cầu một đơn vị. Hoàn hết chi 1.980.000, tiền hàng còn giữ 0, giải phóng hai đơn vị giữ của đơn, tồn vật lý giữ nguyên. Không sinh bản ghi điểm hay movement nhập lại. |
| C5 | Đơn R1 Completed có 18 điểm đã cộng. Gửi R1-A hai lần rồi R1-B. | R1-A chuyển 900.000 tiền hàng còn giữ cho Vy, chỉ đảo 9 điểm một lần. R1-B chuyển 0, đảo 9 điểm còn lại. Lượt dùng voucher của đơn đã thanh toán giữ nguyên. |

Đường mở ca riêng đã kiểm thử trên PostgreSQL là `POST /api/pos/shifts`, tra lại bằng `GET /api/pos/shifts/current`. JWT của Manager xác định chủ ca, client gửi số tiền đầu ca. Dùng hai token M và S cho C1/C2 để phân biệt ca chi với ca bán. Các bài kiểm thử mở và đóng ca qua HTTP bằng JWT của Manager.

## Bằng chứng tích hợp trên PostgreSQL

Ngày 24/11/2026, nhóm kiểm thử phân bổ, điểm, Inventory và luồng hoàn qua HTTP chạy trên PostgreSQL 16.15 với .NET SDK 10.0.401. Bộ kiểm thử này đạt 52/52 trong 3 phút 26 giây. Ca riêng `RefundWhileClosing_CashDrawerEitherIncludesOnePayoutOrRejectsIt` đạt 1/1. Cả hai lượt chạy đều có 0 ca bỏ qua.

- `ReturnCash_TwoPartialRefunds_RealLedgerPreservesOpeningBalanceAndManagerDrawer` chạy checkout và hai lần hoàn qua API, với service Cash và điểm thật. Khách có 491 điểm mở đầu, lên 509 sau checkout, về 500 rồi 491 qua hai lần hoàn. Tổng bán Cash trong ca người bán vẫn là 1.980.000. Ca Manager mở 2.000.000, chi 1.980.000, đóng ở 20.000. Phiếu nhập trước checkout ghi giá vốn thực tế 600.000 mỗi đơn vị. Sau lần hoàn đầu, truy vấn dòng bán, khoản chi và movement trả về lãi gộp −300.000. Qua ngày kế tiếp, kho nhập một đơn vị và cùng phép đối chiếu trả về 300.000.
- `ReturnLoyalty_OrdersApi_OnlyDeliveryEarnsPointsAndProcessingRefundPreservesThem` đối chiếu hai đơn cùng khách. Lộ trình Pending → Processing → Completed ghi điểm khi giao. Lộ trình Pending → Processing → Refunded trả phần giữ và bảo toàn số điểm đã ghi cho đơn trước.
- `ReturnCash_TestPortsRequireActorShiftShareTransactionAndReceiveRetainedNetExactlyOnce` dùng adapter riêng trong test để làm lỗi ở bước ghi điểm sau bước ghi chi. Kiểm tra dữ liệu sau rollback rồi thử lại cùng mã.
- `ReturnBank_OverlappingClaimsFailureReleaseDbRollbackAndReplay_RefundsOnceAndLinksReplacement` kiểm tra đối soát thủ công, hai yêu cầu tranh cùng đơn vị và phục hồi sau lỗi ghi chứng từ. `ReturnInspection_PartialDispositionsAndReplay_ReconcileAmountsAndIncrementOnlyResalableUnits` kiểm tra tách lô ba đơn vị qua HTTP.
- `ReturnCash_RacesWarrantyCallerAndRepairsOrReplacesOnlyTheAuthorizedSoldDevice` giữ khóa đơn cho đến khi yêu cầu hoàn Cash qua HTTP và caller bảo hành trên một kết nối riêng đều chờ khóa. Sau khi thả khóa, chỉ một nghiệp vụ nhận được IMEI. Cũng trên một đơn bán thật, caller của Q. Vinh nhận sửa, trả đúng máy rồi xuất máy thay có giá vốn và hạn bảo hành gốc. Đây là kiểm thử hợp đồng kho. API hồ sơ, quyết định và phân quyền của giao diện bảo hành thuộc luồng của Q. Vinh.
- `Migrate_PopulatedM3_PreservesOldPayoutsFlagsUnallocatedSalesAndBackfillsProcessingReservations` nâng cấp bản M3 có khoản chi cũ, đơn thiếu phân bổ và lượng đang giữ. Các assertions kiểm tra khoản chi cũ còn nguyên, phần còn lại tính được ở đơn đủ dữ liệu, đơn thiếu phân bổ phải đối chiếu chứng từ và lượt giữ Processing mang đúng mã dòng.
