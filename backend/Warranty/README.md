# Bảo hành và sửa chữa

Module Warranty quản lý hồ sơ của thiết bị bán theo Serial/IMEI. Mỗi lần tiếp nhận và đổi trạng thái ghi một sự kiện có người thao tác lấy từ token. Máy có một hồ sơ mở tại một thời điểm.

| Thao tác | Trạng thái hồ sơ | Thao tác kho |
|---|---|---|
| Tiếp nhận | Tạo `Received` | `ReceiveForRepair`, máy `Sold → UnderRepair` |
| Kiểm định | `Received → Repairing / PendingReplacement / Rejected` | Giữ `UnderRepair` |
| Sửa xong | `Repairing → Repaired` | Giữ `UnderRepair` |
| Từ chối đổi | `PendingReplacement → Repairing` | Giữ `UnderRepair` |
| Duyệt đổi | `PendingReplacement → Replaced` | `Replace`, xuất máy cùng sản phẩm và biến thể, chuyển hạn còn lại cho máy thay |
| Trả khách | `Repaired / Rejected → ReturnedToCustomer` | `ReturnToCustomer`, máy `UnderRepair → Sold` |

Tiếp nhận, đổi và trả máy khóa đơn trước, khóa hồ sơ sau, rồi gọi Inventory trong cùng transaction. Kho khóa thiết bị và tồn cần dùng. Kiểm định, báo sửa xong và từ chối đổi chỉ khóa hồ sơ. Khi một bước thất bại, transaction rollback cả hồ sơ, lịch sử và thao tác kho.

Khi tiếp nhận, khách lẻ dùng mã đơn để chứng minh lần bán. Đơn có khách có thể dùng mã đơn hoặc mã khách. Máy phải còn hạn và dòng bán không có yêu cầu trả đang chờ. Bảo hành không tạo đơn hàng, khoản hoàn, điểm khách hoặc doanh thu. Chi phí sửa, hoàn tiền khi hết máy thay, bảo hành của hãng và in phiếu nằm ngoài phạm vi.

## Chi phí máy thay

Dùng ngày đóng hồ sơ theo múi giờ Việt Nam. Một máy thay chưa có giá vốn vẫn được tính vào số lượng máy thay và xuất hiện trong cột thiếu giá vốn. Tổng tiền chỉ cộng những máy đã có giá.

```sql
SELECT (closed_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS replacement_date,
       count(*) AS replacement_count,
       sum(replacement_unit_cost) AS known_replacement_cost,
       count(*) FILTER (WHERE replacement_unit_cost IS NULL) AS missing_cost_count
FROM warranty_cases
WHERE status = 'Replaced'
GROUP BY 1
ORDER BY 1;
```

Đã gửi Bình câu SQL và JSON mẫu có giá vốn null qua email ngày 25/11/2026.
