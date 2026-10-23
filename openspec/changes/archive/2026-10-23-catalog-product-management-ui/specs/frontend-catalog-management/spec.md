# Spec Delta

## Purpose

Cho phép người dùng desktop có vai trò Admin hoặc Manager tạo, sửa, ngừng/mở bán và xóa sản phẩm qua Catalog API thật, với kiểm tra đầu vào tại client, lỗi server hiển thị đúng chỗ và không bao giờ tự gửi lại yêu cầu ghi.

## ADDED Requirements

### Requirement: Write actions follow the session role
Client SHALL chỉ hiện các nút Thêm sản phẩm, Sửa, Ngừng kinh doanh, Mở bán và Xóa khi phiên đang hoạt động có vai trò `Admin` hoặc `Manager`. Server vẫn là nơi quyết định quyền: khi server trả 401 hoặc 403 cho một thao tác ghi, client SHALL báo lỗi tương ứng và không coi thao tác là thành công.

#### Scenario: Sales staff browses products
- **WHEN** người dùng đăng nhập với vai trò `SalesStaff` mở danh sách và chi tiết sản phẩm
- **THEN** không có nút Thêm sản phẩm, Sửa, Ngừng kinh doanh, Mở bán hay Xóa; việc xem vẫn hoạt động như trước

#### Scenario: Manager sees write actions
- **WHEN** người dùng vai trò `Manager` mở chi tiết một sản phẩm
- **THEN** các nút Sửa, Ngừng kinh doanh (hoặc Mở bán) và Xóa hiện ra

#### Scenario: Server refuses the role
- **WHEN** server trả 403 cho một yêu cầu lưu
- **THEN** màn hình báo "Bạn không có quyền thực hiện thao tác này.", dữ liệu đang soạn được giữ và không điều hướng đi đâu

#### Scenario: Session changes while the page is open
- **WHEN** phiên đăng xuất hoặc hết hạn khi đang ở danh sách, chi tiết hoặc màn soạn
- **THEN** các nút ghi bị ẩn ngay và không gửi thêm yêu cầu ghi nào

### Requirement: Create a product from the editor page
Client SHALL mở màn soạn trống từ nút Thêm sản phẩm, gửi `POST /api/products` một lần khi người dùng bấm Lưu với dữ liệu hợp lệ, và khi thành công SHALL mở trang chi tiết của sản phẩm vừa tạo với ngữ cảnh truy vấn danh sách trước đó.

#### Scenario: Successful create
- **WHEN** Admin nhập tên, SKU, thương hiệu, danh mục, giá bán, giá vốn hợp lệ rồi bấm Lưu
- **THEN** client gửi đúng một `POST /api/products`, sau đó hiển thị chi tiết sản phẩm mới do server trả về

#### Scenario: Cancel an untouched draft
- **WHEN** người dùng bấm Hủy trên màn soạn chưa sửa gì
- **THEN** client quay lại danh sách với ngữ cảnh truy vấn cũ mà không hỏi xác nhận và không gửi yêu cầu ghi

### Requirement: Edit an existing product
Client SHALL nạp chi tiết hiện tại bằng `GET /api/products/{id}` vào màn soạn, hiển thị SKU sản phẩm và SKU biến thể đã lưu ở dạng chỉ đọc, và khi Lưu SHALL gửi `PUT /api/products/{id}` chứa toàn bộ trường sửa được cùng mọi biến thể đang hiển thị. Thành công SHALL quay về trang chi tiết với dữ liệu server trả về.

#### Scenario: Change price and specs
- **WHEN** Admin đổi giá bán và một giá trị thông số rồi bấm Lưu
- **THEN** client gửi một `PUT`, trang chi tiết hiển thị giá và thông số mới theo response

#### Scenario: Product disappeared before editing
- **WHEN** mở màn sửa nhưng `GET /api/products/{id}` trả 404
- **THEN** màn hình báo không tìm thấy sản phẩm, có nút quay lại danh sách và không hiện form

#### Scenario: Inactive product
- **WHEN** Admin sửa một sản phẩm đang ngừng kinh doanh
- **THEN** việc lưu không đổi trạng thái kinh doanh; nhãn trạng thái vẫn là Ngừng hoạt động

### Requirement: Dynamic specification editor
Màn soạn SHALL cho thêm, sửa và bỏ từng dòng thông số cho sản phẩm và từng biến thể; người dùng nhập tên hiển thị, không gõ key. Tên khớp một định nghĩa có sẵn SHALL dùng key, kiểu và đơn vị của định nghĩa và khóa kiểu; tên mới SHALL được tự sinh key snake_case và gửi trong `specLabels` khi Lưu. Client SHALL chặn lưu khi key trùng trong một nhóm hoặc giá trị không hợp lệ với kiểu.

#### Scenario: Pick an existing spec by its name
- **WHEN** người dùng gõ `ram` ở ô tên và chọn gợi ý `RAM`
- **THEN** dòng dùng key `ram_gb`, kiểu Số bị khóa, ô giá trị hiện đơn vị `GB`; nhập `16` thì request chứa `"ram_gb": 16` và không có `specLabels`

#### Scenario: Create a new spec from its name
- **WHEN** người dùng gõ tên `Tần số quét (Hz)`, chọn kiểu Số, nhập `144` rồi Lưu
- **THEN** request chứa specs `{"tan_so_quet_hz": 144}` và `specLabels` `{"tan_so_quet_hz": {"label": "Tần số quét", "unit": "Hz", "valueKind": "number"}}`

#### Scenario: Typed values are sent with their JSON type
- **WHEN** các dòng có giá trị Số `16`, Đúng/Sai `true` và Chuỗi `Apple M3`
- **THEN** request chứa đúng kiểu JSON `16`, `true`, `"Apple M3"`

#### Scenario: Existing specs are loaded with their names
- **WHEN** mở sửa một sản phẩm có specs `{"ram_gb": 8, "cpu": "M3"}`
- **THEN** editor có hai dòng `RAM` (Số, `8`, GB) và `Vi xử lý (CPU)` (Chuỗi, `M3`); một key chưa có định nghĩa hiện key của nó, giữ nguyên key và cho đặt tên

#### Scenario: Name without letters or digits
- **WHEN** người dùng nhập tên `!!!`
- **THEN** dòng đó báo lỗi tên và không gửi request

#### Scenario: Large and decimal numbers keep precision
- **WHEN** người dùng nhập kiểu Số giá trị `6.1` hoặc `12345678901234567890`
- **THEN** giá trị gửi đi bằng đúng số đã nhập, không bị làm tròn qua số thực nhị phân

### Requirement: Brand suggestions in the editor
Màn soạn SHALL gợi ý các thương hiệu đang có (từ `GET /api/products/brands`, kèm số sản phẩm) khi người dùng gõ vào ô Thương hiệu, chọn gợi ý thì điền đúng cách viết đó. Khi tên gõ trùng một thương hiệu có sẵn chỉ khác hoa thường, client SHALL báo rằng sản phẩm sẽ dùng cách viết có sẵn. Không tải được danh sách thì ô vẫn nhập tự do.

#### Scenario: Pick a suggested brand
- **WHEN** người dùng gõ `app` và chọn gợi ý `Apple (3)`
- **THEN** ô Thương hiệu thành `Apple` và gợi ý biến mất

#### Scenario: Same brand in another letter case
- **WHEN** người dùng gõ `apple` trong khi đã có `Apple`
- **THEN** ô hiện ghi chú sản phẩm sẽ dùng cách viết `Apple`, không có gợi ý nào khác

### Requirement: Variant rows in the editor
Màn soạn SHALL cho thêm dòng biến thể mới (tên, SKU, giá, barcode, thông số riêng) và bỏ dòng mới khi chưa lưu. Biến thể mới bắt đầu ở trạng thái chưa bán; ô Đang bán SHALL chỉ hiện trong màn Sửa. Biến thể đã lưu SHALL không có thao tác xóa; người dùng ngừng bán chúng bằng công tắc Đang bán. Client SHALL chặn lưu khi hai biến thể mới trùng SKU (không phân biệt hoa thường) hoặc trùng SKU sản phẩm.

#### Scenario: Retire a saved variant
- **WHEN** Admin tắt công tắc Đang bán của một biến thể đã lưu rồi bấm Lưu
- **THEN** request gửi biến thể đó kèm `id` và `isActive: false`, không có yêu cầu xóa nào

#### Scenario: New product variants start not selling
- **WHEN** người dùng thêm sản phẩm mới có hai biến thể rồi bấm Lưu
- **THEN** các dòng biến thể không có ô Đang bán, request tạo không gửi `isActive` cho biến thể nào và cả hai được lưu ở trạng thái chưa bán

#### Scenario: Selling tick in Sửa
- **WHEN** Admin mở màn Sửa của một sản phẩm và bấm + Thêm biến thể
- **THEN** mọi dòng biến thể có ô Đang bán, dòng mới chưa tick và chữ cạnh ô là "Chưa bán"

#### Scenario: Remove an unsaved variant row
- **WHEN** người dùng thêm một dòng biến thể rồi bấm Bỏ trước khi lưu
- **THEN** dòng biến mất và không xuất hiện trong request

#### Scenario: Duplicate new SKU
- **WHEN** hai dòng biến thể mới cùng SKU `ip16-256` và `IP16-256`
- **THEN** dòng thứ hai hiện lỗi trùng SKU và không gửi request

### Requirement: Product barcode follows variants
Khi màn soạn có ít nhất một biến thể (đã lưu hoặc mới), ô barcode sản phẩm SHALL bị khóa, được xóa trắng và có chú thích rằng sản phẩm có biến thể dùng barcode của từng biến thể. Khi không còn biến thể nào, ô barcode SHALL được mở lại.

#### Scenario: First variant added
- **WHEN** sản phẩm không có biến thể đang có barcode `8934567890123` và người dùng thêm một dòng biến thể
- **THEN** ô barcode bị khóa, giá trị bị xóa, và request gửi `barcode` rỗng

### Requirement: Client validation mirrors the API limits
Trước khi gửi, client SHALL kiểm tra: tên (bắt buộc, ≤ 200), SKU sản phẩm khi tạo (bắt buộc, ≤ 64), thương hiệu (bắt buộc, ≤ 100), danh mục (bắt buộc), giá bán và giá vốn (bắt buộc, số thập phân ≥ 0), barcode (≤ 64), mô tả (≤ 2000), URL ảnh (≤ 2048), và tên, SKU, giá của từng biến thể. Lỗi SHALL hiện ngay dưới ô tương ứng và không có request nào được gửi.

#### Scenario: Several errors at once
- **WHEN** người dùng để trống tên, nhập giá vốn `-1` và để trống giá một biến thể mới rồi bấm Lưu
- **THEN** ba ô đó cùng hiện lỗi và không có request nào được gửi

#### Scenario: Vietnamese number format
- **WHEN** người dùng nhập giá `35.990.000`
- **THEN** giá được hiểu là 35990000 và gửi đúng giá trị đó

### Requirement: Server errors land where they belong
Với 400 có lỗi theo trường, client SHALL hiện từng lỗi tại ô tương ứng, kể cả `specs`, `variants[i].sku`, `variants[i].price`, `variants[i].specs`; lỗi không khớp ô nào SHALL vào banner chung. Với 409, 404, 401, 403, timeout và lỗi mạng, client SHALL hiện một thông điệp ở banner và giữ nguyên dữ liệu đang soạn.

#### Scenario: Duplicate SKU from the server
- **WHEN** server trả 409 "One or more SKUs are already in use: IP16-256."
- **THEN** banner hiện nội dung đó, form giữ nguyên và người dùng sửa được rồi lưu lại

#### Scenario: Field error on a variant
- **WHEN** server trả 400 với lỗi `variants[1].sku`
- **THEN** lỗi hiện ở ô SKU của dòng biến thể thứ hai đang hiển thị

#### Scenario: Product in use
- **WHEN** server trả 409 khi đổi theo dõi IMEI hoặc thêm biến thể đầu tiên cho sản phẩm đã có giao dịch
- **THEN** banner hiện thông điệp của server và không có thay đổi nào được coi là đã lưu

### Requirement: Write requests are never repeated
Client SHALL gửi mỗi thao tác ghi đúng một lần cho mỗi lần người dùng xác nhận. Trong lúc đang gửi, nút Lưu, Ngừng/Mở bán và Xóa SHALL bị vô hiệu; client MUST NOT tự gửi lại sau timeout, lỗi mạng hay lỗi server.

#### Scenario: Double click on Save
- **WHEN** người dùng bấm Lưu hai lần liên tiếp
- **THEN** chỉ có một request được gửi

#### Scenario: Timeout after save
- **WHEN** request lưu bị timeout
- **THEN** banner báo hãy kiểm tra lại sản phẩm trước khi thử lại, và client không tự gửi lại

### Requirement: Leave the editor with unsaved changes
Khi người dùng bấm Hủy hoặc Quay lại trong màn soạn đã có thay đổi chưa lưu, client SHALL hỏi xác nhận bỏ thay đổi. Rời bằng thanh điều hướng Shell không bị chặn.

#### Scenario: Discard confirmed
- **WHEN** người dùng đã sửa tên, bấm Hủy và chọn bỏ thay đổi
- **THEN** client quay về trang trước (chi tiết khi đang sửa, danh sách khi đang tạo) mà không gửi request

#### Scenario: Discard declined
- **WHEN** người dùng bấm Hủy rồi chọn tiếp tục chỉnh sửa
- **THEN** màn soạn giữ nguyên mọi dữ liệu đang nhập

### Requirement: Stop and resume selling
Trên trang chi tiết, client SHALL cho Ngừng kinh doanh một sản phẩm đang bán và Mở bán một sản phẩm chưa bán hoặc đã ngừng, mỗi lần sau một hộp xác nhận, bằng `PATCH /api/products/{id}/status`. Thành công SHALL làm mới nhãn trạng thái theo response; trạng thái kinh doanh của biến thể không đổi.

#### Scenario: Stop selling
- **WHEN** Admin bấm Ngừng kinh doanh và xác nhận
- **THEN** client gửi `{"isActive": false}`, nhãn đổi thành Ngừng hoạt động và nút đổi thành Mở bán

#### Scenario: Confirmation declined
- **WHEN** người dùng bấm Ngừng kinh doanh rồi chọn Hủy trong hộp xác nhận
- **THEN** không có request nào được gửi

### Requirement: Selling state is visible on the detail page
Trang chi tiết SHALL hiện nhãn trạng thái cho từng biến thể. Khi sản phẩm chưa bán, trang SHALL nhắc rằng sản phẩm chưa chọn được khi lập đơn và cần Mở bán khi sẵn sàng; khi sản phẩm đang bán và có biến thể nhưng không biến thể nào đang bán, trang SHALL cảnh báo cần bật biến thể trong màn Sửa. Màn thêm sản phẩm SHALL ghi rằng sản phẩm và biến thể mới được lưu ở trạng thái chưa bán, và muốn bán thì bấm Mở bán ở trang chi tiết rồi bật Đang bán cho biến thể trong màn Sửa.

#### Scenario: New product after saving
- **WHEN** Admin tạo sản phẩm có một biến thể và được đưa tới trang chi tiết
- **THEN** sản phẩm và biến thể đều có nhãn Ngừng hoạt động, trang nhắc Mở bán khi sẵn sàng, nút hiện Mở bán

#### Scenario: Product opened but no variant selling
- **WHEN** Admin Mở bán một sản phẩm có biến thể mà chưa biến thể nào đang bán
- **THEN** trang cảnh báo chưa có biến thể nào đang bán và hướng dẫn bật trong màn Sửa

### Requirement: Delete only unused products
Trên trang chi tiết, client SHALL cho Xóa sau một hộp xác nhận nói rõ chỉ xóa được sản phẩm chưa phát sinh giao dịch, gửi `DELETE /api/products/{id}`. 204 SHALL đưa người dùng về danh sách; 409 SHALL hiện thông điệp của server kèm gợi ý dùng Ngừng kinh doanh; 404 SHALL báo sản phẩm không còn tồn tại.

#### Scenario: Delete a new product
- **WHEN** Admin xóa một sản phẩm chưa có giao dịch và server trả 204
- **THEN** client quay về danh sách với ngữ cảnh truy vấn cũ và danh sách được tải lại

#### Scenario: Product has history
- **WHEN** server trả 409 cho yêu cầu xóa
- **THEN** trang chi tiết vẫn hiện sản phẩm, banner hiện thông điệp của server và gợi ý Ngừng kinh doanh

### Requirement: Price margin hint
Màn soạn SHALL hiện nhãn lãi gộp dự kiến từ giá bán và giá vốn dự kiến (`costPrice`) đang nhập, dạng `Lãi gộp dự kiến 4.790.000 ₫ (13,3%)` với phần trăm là lãi chia giá bán, tính bằng số thập phân. Nhãn chỉ là ước tính để định giá, không phải lãi thực tế của đơn bán. Khi thiếu một trong hai giá, giá bán bằng 0 hoặc giá không hợp lệ, nhãn SHALL ẩn; khi giá vốn dự kiến lớn hơn giá bán, nhãn SHALL ghi lỗ gộp dự kiến với màu cảnh báo.

#### Scenario: Positive margin
- **WHEN** giá bán `35990000` và giá vốn dự kiến `31200000`
- **THEN** nhãn hiện `Lãi gộp dự kiến 4.790.000 ₫ (13,3%)`

#### Scenario: Selling below cost
- **WHEN** giá bán `1000000` và giá vốn dự kiến `1200000`
- **THEN** nhãn hiện `Lỗ gộp dự kiến 200.000 ₫` với màu cảnh báo và việc lưu vẫn được phép

### Requirement: Image URL preview
Màn soạn SHALL cho nhập URL ảnh và hiện xem trước khi URL là địa chỉ http/https hợp lệ (hoặc đường dẫn tương đối theo API); URL rỗng, sai hoặc ảnh tải lỗi SHALL hiện ảnh giữ chỗ mà không làm lỗi màn soạn.

#### Scenario: Broken image URL
- **WHEN** người dùng nhập một URL ảnh không tải được
- **THEN** khung xem trước hiện ảnh giữ chỗ và vẫn lưu được
