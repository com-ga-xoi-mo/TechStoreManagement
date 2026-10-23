# Spec Delta

## Purpose

Cho người dùng desktop có vai trò Admin hoặc Manager chọn nhiều ảnh từ máy, chọn ảnh bìa, sắp xếp và gắn ảnh cho từng biến thể ngay trong màn soạn sản phẩm, lưu cùng nút Lưu, và xem ảnh theo biến thể trên trang chi tiết.

## ADDED Requirements

### Requirement: Image area in the editor
Màn soạn SHALL có khu "Ảnh sản phẩm" thay cho ô URL ảnh, gồm: ảnh lớn của ảnh đang chọn, hàng ảnh nhỏ theo đúng thứ tự, khung ảnh lớn có thể bấm để chọn thêm ảnh, bộ đếm dạng `3/20`, và dòng gợi ý loại và dung lượng cho phép. Bấm một ảnh nhỏ SHALL chọn ảnh đó và hiện nó ở ảnh lớn. Khi chưa có ảnh, ảnh lớn SHALL là ảnh giữ chỗ.

#### Scenario: Select a thumbnail
- **WHEN** sản phẩm có ba ảnh và người dùng bấm ảnh nhỏ thứ hai
- **THEN** ảnh lớn đổi sang ảnh thứ hai và ảnh nhỏ đó được đánh dấu đang chọn

#### Scenario: No image yet
- **WHEN** người dùng mở màn thêm sản phẩm mới
- **THEN** khu ảnh hiện ảnh giữ chỗ, bộ đếm `0/20` và khung ảnh lớn cho phép bấm để chọn ảnh

### Requirement: Add images from the computer
Bấm vào khung ảnh lớn SHALL mở hộp chọn file cho phép chọn nhiều file JPG, JPEG, PNG, WebP một lần. File lớn hơn 5 MB, file có đuôi khác hoặc file vượt quá tổng 20 ảnh SHALL không được thêm, và màn soạn SHALL báo tên từng file bị bỏ qua cùng lý do. Ảnh vừa thêm SHALL có nhãn "Mới" cho đến khi được lưu. Không có nút "+ Thêm" riêng. Khi đã có 20 ảnh, thao tác chọn thêm bằng khung ảnh SHALL bị vô hiệu. Chọn ảnh chỉ thêm vào bản nháp; file chỉ được tải lên khi bấm Lưu sản phẩm.

#### Scenario: Pick three files
- **WHEN** người dùng chọn `a.jpg`, `b.png` và `c.webp` dưới 5 MB
- **THEN** ba ảnh nhỏ có nhãn "Mới" được thêm vào cuối hàng và bộ đếm tăng thêm 3

#### Scenario: Oversized and wrong type
- **WHEN** người dùng chọn `big.jpg` (6 MB) và `photo.heic`
- **THEN** không ảnh nào được thêm và khu ảnh báo `big.jpg lớn hơn 5 MB` và `photo.heic: chỉ nhận JPG, PNG, WebP`

#### Scenario: Over the limit
- **WHEN** sản phẩm đã có 18 ảnh và người dùng chọn 4 file hợp lệ
- **THEN** chỉ 2 file đầu được thêm và khu ảnh báo chỉ thêm được 2 ảnh nữa

#### Scenario: Picker cancelled
- **WHEN** người dùng mở hộp chọn file rồi đóng mà không chọn
- **THEN** danh sách ảnh và thông báo lỗi không đổi

### Requirement: Image frame hover hint
Khi rê chuột vào khung ảnh lớn và vẫn còn chỗ thêm ảnh, màn soạn SHALL hiện lớp phủ nhẹ có biểu tượng mũi tên xuống khay và chữ "Thêm ảnh"; khi rời chuột, lớp phủ SHALL ẩn.

#### Scenario: Hover image frame
- **WHEN** người dùng rê chuột vào khung ảnh lớn khi có ít hơn 20 ảnh
- **THEN** khung hiện biểu tượng mũi tên xuống khay và chữ "Thêm ảnh"; rời chuột thì lớp phủ ẩn, và bấm khung vẫn mở hộp chọn file

### Requirement: Cover image in the editor
Màn soạn SHALL dùng cùng quy tắc ảnh bìa với server: ảnh dùng chung đứng đầu, nếu không có ảnh chung thì ảnh đầu tiên. Ảnh đang là bìa SHALL có nhãn "Bìa". Nút "Đặt làm ảnh bìa" SHALL chỉ hiện khi ảnh đang chọn là ảnh dùng chung và chưa là bìa, và SHALL đưa ảnh đó lên đầu hàng.

#### Scenario: Make cover
- **WHEN** người dùng chọn ảnh dùng chung thứ ba rồi bấm "Đặt làm ảnh bìa"
- **THEN** ảnh đó lên đầu hàng và có nhãn "Bìa"

#### Scenario: Variant image cannot be cover
- **WHEN** ảnh đang chọn được gắn cho biến thể "Đen"
- **THEN** nút "Đặt làm ảnh bìa" không hiện

### Requirement: Reorder and remove images
Nút `<` và `>` SHALL đổi chỗ ảnh đang chọn với ảnh liền trước hoặc liền sau và SHALL bị vô hiệu ở đầu hoặc cuối hàng. Nút "Xóa ảnh" SHALL bỏ ảnh đang chọn khỏi hàng và chọn ảnh kế tiếp (hoặc ảnh trước nếu ảnh bị xóa là ảnh cuối). Xóa không cần hộp xác nhận vì chưa có gì được gửi cho đến khi bấm Lưu.

#### Scenario: Move right
- **WHEN** hàng ảnh là A, B, C, người dùng chọn A rồi bấm `>`
- **THEN** hàng ảnh là B, A, C và A vẫn đang được chọn

#### Scenario: Remove the last image
- **WHEN** hàng ảnh là A, B, C, người dùng chọn C rồi bấm "Xóa ảnh"
- **THEN** hàng ảnh là A, B và B đang được chọn

### Requirement: Assign an image to a variant
Ô "Dùng cho" SHALL liệt kê "Chung cho mọi biến thể" và mọi dòng biến thể đang hiện trong màn soạn theo tên, kể cả dòng chưa lưu, và SHALL đổi việc gắn của ảnh đang chọn. Mỗi ảnh nhỏ SHALL hiện tên biến thể nó được gắn. Đổi tên dòng biến thể SHALL đổi tên trong ô và trên ảnh nhỏ; bỏ một dòng chưa lưu SHALL đưa các ảnh gắn với nó về "Chung".

#### Scenario: Tag an image
- **WHEN** người dùng chọn một ảnh rồi chọn "Đen" ở ô "Dùng cho"
- **THEN** ảnh nhỏ đó hiện nhãn "Đen"

#### Scenario: Unsaved variant row removed
- **WHEN** một ảnh gắn với dòng biến thể chưa lưu và người dùng bấm "Bỏ" dòng đó
- **THEN** ảnh đó trở về "Chung cho mọi biến thể"

#### Scenario: Variant row shows its image count
- **WHEN** hai ảnh được gắn cho dòng "Đen"
- **THEN** dòng biến thể "Đen" hiện "2 ảnh"

### Requirement: Image changes are saved with the product
Thay đổi ảnh SHALL chỉ được gửi khi người dùng bấm Lưu và dữ liệu sản phẩm hợp lệ, theo thứ tự: lưu sản phẩm, xóa các ảnh đã bỏ, tải lên từng ảnh mới, rồi lưu thứ tự và việc gắn biến thể. Khi mọi bước thành công, client SHALL mở trang chi tiết. Hủy SHALL không gửi gì; thêm, xóa, đổi thứ tự hay đổi gắn biến thể SHALL được tính là thay đổi chưa lưu khi bấm Hủy hoặc Quay lại.

#### Scenario: Create with images
- **WHEN** người dùng tạo sản phẩm có hai ảnh mới rồi bấm Lưu
- **THEN** client gửi đúng một yêu cầu tạo sản phẩm, hai yêu cầu tải ảnh, một yêu cầu lưu thứ tự, rồi mở trang chi tiết có hai ảnh

#### Scenario: Only images changed
- **WHEN** người dùng chỉ đổi thứ tự hai ảnh của sản phẩm đã có rồi bấm Hủy
- **THEN** client hỏi xác nhận bỏ thay đổi

#### Scenario: Cancel after picking
- **WHEN** người dùng thêm ba ảnh rồi bấm Hủy và chọn bỏ thay đổi
- **THEN** không có ảnh nào được gửi lên server

### Requirement: Unsaved variants are matched by SKU
Ảnh gắn với dòng biến thể chưa lưu SHALL được gửi kèm id của biến thể mà server vừa tạo cho dòng đó, tìm bằng cách so SKU của dòng với SKU trong phản hồi lưu sản phẩm, không phân biệt hoa thường. Client MUST NOT ghép theo vị trí. Nếu một dòng không tìm thấy SKU trong phản hồi, client SHALL không gửi yêu cầu ảnh nào và xử lý như lỗi lưu ảnh sau khi sản phẩm đã lưu.

#### Scenario: Different order in the response
- **WHEN** người dùng nhập dòng "Đen" SKU `ip16-bk` trước dòng "Trắng" SKU `IP16-WH`, gắn một ảnh cho "Đen", và phản hồi trả biến thể "Trắng" trước
- **THEN** ảnh được tải lên với id của biến thể có SKU `IP16-BK`

#### Scenario: SKU missing from the response
- **WHEN** phản hồi lưu sản phẩm không có biến thể nào trùng SKU của một dòng chưa lưu
- **THEN** không có ảnh nào được tải lên và banner báo đã lưu sản phẩm nhưng chưa lưu được ảnh

### Requirement: Image save failure after the product was saved
Khi sản phẩm đã lưu nhưng một bước ảnh thất bại, client SHALL ở lại màn soạn ở chế độ Sửa của sản phẩm đó, hiện thông báo "Đã lưu sản phẩm nhưng chưa lưu được ảnh" kèm lý do, giữ các ảnh chưa lưu với nhãn "Mới", và MUST NOT tự gửi lại. Bấm Lưu lần nữa SHALL chỉ gửi các bước ảnh còn thiếu và MUST NOT tạo thêm sản phẩm.

#### Scenario: Network error on the second upload
- **WHEN** tạo sản phẩm thành công, ảnh thứ nhất tải lên được và ảnh thứ hai lỗi mạng
- **THEN** màn soạn chuyển sang chế độ Sửa, ảnh thứ nhất không còn nhãn "Mới", ảnh thứ hai vẫn "Mới", và banner hiện thông báo lỗi

#### Scenario: Retry
- **WHEN** sau lỗi đó người dùng bấm Lưu lại
- **THEN** client gửi yêu cầu sửa sản phẩm, chỉ tải lại ảnh thứ hai, lưu thứ tự, rồi mở trang chi tiết

### Requirement: Images on the detail page
Trang chi tiết SHALL hiện ảnh lớn và, khi có từ hai ảnh, hàng ảnh nhỏ bấm được để đổi ảnh lớn. Ô "Xem ảnh của" SHALL có "Tất cả" và từng biến thể có ảnh riêng; chọn một biến thể SHALL chỉ hiện ảnh dùng chung và ảnh của biến thể đó, với ảnh lớn là ảnh đầu tiên của biến thể. Trang chi tiết SHALL không có thao tác sửa ảnh.

#### Scenario: Filter by variant
- **WHEN** sản phẩm có một ảnh chung, hai ảnh "Đen", một ảnh "Trắng" và người dùng chọn "Đen"
- **THEN** hàng ảnh nhỏ có ba ảnh và ảnh lớn là ảnh "Đen" đầu tiên

#### Scenario: Single image
- **WHEN** sản phẩm có đúng một ảnh
- **THEN** trang hiện ảnh lớn và không hiện hàng ảnh nhỏ

### Requirement: Broken images show a placeholder
Ảnh không tải được ở màn soạn hay trang chi tiết SHALL hiện ảnh giữ chỗ mà không làm lỗi trang hay chặn việc lưu.

#### Scenario: Deleted file on storage
- **WHEN** link của một ảnh đã lưu trả 404
- **THEN** ảnh nhỏ và ảnh lớn của nó hiện ảnh giữ chỗ và các ảnh khác vẫn hiện bình thường
