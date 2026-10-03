# Module Orders (Dev 4 phụ trách)

Nội dung triển khai:
- Entities: `Order.cs`, `OrderItem.cs`, `OrderReturn.cs` (Kế thừa từ `BaseEntity`).
- Chi tiết món hàng: số lượng, đơn giá, mã IMEI đã bán, chiết khấu, thuế, tổng tiền.
- Fluent API Configurations trong thư mục `Configurations/`.
- Services: `IOrderService.cs`, `OrderService.cs`.
- Controller: `OrdersController.cs`.
- Enums dùng chung đặt tại `shared/Enums/`: `OrderStatus.cs`, `PaymentMethod.cs`.
