# frontend-catalog-browsing Specification

## Purpose

Cho phép người dùng tìm kiếm, lọc và xem chi tiết Catalog chỉ đọc từ API thật, với dữ liệu trung thực, trạng thái tải có thể phục hồi và giao diện desktop phù hợp mẫu TechStore.

## Requirements

### Requirement: Live read-only catalog
Client SHALL lấy danh sách từ `GET /api/products`, chi tiết từ `GET /api/products/{id}` và lựa chọn danh mục từ `GET /api/categories/tree`. Việc duyệt Catalog (danh sách, lọc, chi tiết) MUST NOT gửi mutation, truy cập database trực tiếp hoặc tự thay dữ liệu thật bằng fixture khi có lỗi. Thao tác ghi chỉ xảy ra khi người dùng chủ động dùng các hành động quản lý sản phẩm thuộc capability `frontend-catalog-management`.

#### Scenario: Enter products
- **WHEN** người dùng mở Sản phẩm từ Shell và API khả dụng
- **THEN** danh sách hiển thị kết quả thật cùng metadata phân trang; chi tiết chưa được gọi cho từng card

#### Scenario: API unavailable
- **WHEN** API không thể kết nối khi mở Catalog
- **THEN** trang báo lỗi có retry, không hiển thị dữ liệu demo hoặc empty như một lần tải thành công

#### Scenario: Browsing sends no writes
- **WHEN** người dùng tìm kiếm, lọc, sắp xếp, phân trang, mở chi tiết và quay lại mà không bấm hành động quản lý nào
- **THEN** client chỉ gửi các yêu cầu `GET`

### Requirement: Submitted catalog criteria
Client SHALL cho lọc trên giao diện theo từ khóa q, categoryId, isActive và các spec key:value. Từ khóa là ô tìm chung: API khớp tên, SKU, hãng và barcode, nên giao diện không có ô thương hiệu riêng. Giao diện không có ô khoảng giá; repository vẫn gửi được brand, minPrice và maxPrice theo API khi được truyền vào. Bộ lọc thông số SHALL được thu gọn mặc định, mở bằng một nút, và gồm các dòng điều kiện: chọn tên rồi chọn giá trị từ `GET /api/products/spec-keys` theo danh mục đang chọn; mỗi dòng gửi đúng `filterValue` do API trả về. Bộ lọc tự mở khi truy vấn đang áp dụng có thông số hoặc khi điều kiện chưa hợp lệ. Search/filter chỉ áp dụng khi submit bằng nút hoặc Enter. Category chỉ lọc trực tiếp và các điều kiện kết hợp AND theo API.

#### Scenario: Unicode and reserved characters
- **WHEN** submit từ khóa có dấu, khoảng trắng, `&`, `+`, `%`, `#` hoặc spec value chứa dấu hai chấm
- **THEN** mỗi query value được encode riêng; API nhận đúng giá trị, không sinh thêm tham số ngoài ý muốn

#### Scenario: Draft versus applied criteria
- **WHEN** sửa bộ lọc nhưng chưa submit
- **THEN** kết quả vẫn ghi nhận truy vấn đã áp dụng; submit hợp lệ bắt đầu trang 1, bỏ q rỗng và isActive ở lựa chọn Tất cả

#### Scenario: Search by brand
- **WHEN** người dùng nhập `logitech` vào ô tìm kiếm và submit
- **THEN** client gửi `q=logitech` và không gửi `brand`; danh sách gồm các sản phẩm có hãng Logitech

#### Scenario: Pick spec conditions from stored values
- **WHEN** người dùng mở lọc thông số, chọn `ram_gb` = `16` và `bluetooth` = `5.2` (giá trị chữ) rồi submit
- **THEN** client gửi `spec=ram_gb:16` và `spec=bluetooth:"5.2"` đúng như `filterValue` của API

#### Scenario: Suggestions follow the category
- **WHEN** người dùng đổi danh mục trong bộ lọc
- **THEN** danh sách tên thông số được tải lại cho danh mục đó; nếu tải lỗi thì hiện thông báo kèm nút Tải lại và tìm kiếm khác vẫn dùng được

#### Scenario: Invalid filter input
- **WHEN** một dòng đã chọn tên nhưng chưa chọn giá trị, hoặc hai dòng cùng tên
- **THEN** bộ lọc thông số mở ra và hiển thị lỗi, không gửi truy vấn; dòng chưa chọn tên được bỏ qua; lỗi validation từ API vẫn được xử lý nếu server từ chối

### Requirement: Category filter availability
Client SHALL hiển thị tên và phân cấp danh mục từ cây API, không bịa danh mục hoặc counts. Lỗi tải cây SHALL có thông báo và retry riêng, không biến kết quả sản phẩm thành rỗng hay chặn tìm kiếm không dùng danh mục.

#### Scenario: Parent category selected
- **WHEN** chọn một danh mục cha và áp dụng
- **THEN** gửi đúng categoryId của nó, không tự thêm sản phẩm thuộc danh mục con; nhãn giải thích phạm vi lọc trực tiếp

#### Scenario: Category lookup failure
- **WHEN** danh sách tải thành công nhưng cây danh mục lỗi
- **THEN** kết quả vẫn dùng được, bộ chọn danh mục báo chưa tải được và cho retry, không lấy category facets từ một trang sản phẩm

### Requirement: Server ordering and pagination
Client SHALL dùng sort name/price/createdAt với asc/desc, mặc định createdAt:desc; paging mặc định page=1/pageSize=20. Items và tổng số SHALL dùng envelope server, không sort/filter riêng trang hiện tại hoặc tự dựng tổng toàn bộ từ số card.

#### Scenario: Change sort or page size
- **WHEN** chọn sort hợp lệ hoặc page size 20/50/100
- **THEN** tải trang 1 với bộ lọc đang áp dụng; Previous/Next dùng metadata đã tải thành công và không vượt biên

#### Scenario: Empty and out-of-range page
- **WHEN** API trả items rỗng với totalCount=0 hoặc page lớn hơn totalPages
- **THEN** phân biệt không có kết quả với trang không còn tồn tại; trường hợp sau cho quay về trang 1, không tự tạo items hoặc lặp request vô hạn

### Requirement: Faithful catalog data
Client SHALL giữ id, tên, SKU, brand, category, priceFrom/priceTo, variantCount, isSerialTracked và isActive theo API. Stock, swatch màu và category counts không có dữ liệu MUST NOT bị suy diễn. Specs/effectiveSpecs SHALL giữ kiểu string/number/boolean và giá trị server; tiền SHALL không mất độ chính xác do chuyển sang số thực nhị phân.

#### Scenario: Product card without inventory data
- **WHEN** summary không có tồn kho hoặc màu biến thể
- **THEN** card không hiển thị Còn/Hết/Sắp hết hoặc màu giả; hiển thị variantCount thật và trạng thái hoạt động tách biệt tồn kho

#### Scenario: Price and missing image
- **WHEN** priceFrom bằng priceTo hoặc khác nhau, imageUrl rỗng/không hợp lệ/tải lỗi
- **THEN** hiển thị giá đơn hoặc khoảng giá VND đúng và ảnh placeholder nội bộ; lỗi ảnh không làm lỗi toàn danh sách

#### Scenario: Detail variants
- **WHEN** mở chi tiết có biến thể với effectiveSpecs đã merge ở server
- **THEN** hiển thị đúng biến thể, giá và specs/effectiveSpecs, không tự suy màu từ tên hoặc thêm biến thể không có trong response

### Requirement: Contract failures stay visible
Client SHALL báo lỗi response không hợp lệ khi payload sai kiểu, thiếu trường bắt buộc, metadata vô lý hoặc id/giá không hợp lệ; field mới ngoài contract SHALL được bỏ qua. Lỗi contract MUST NOT bị chuyển thành danh sách rỗng hoặc default DTO thành công.

#### Scenario: Missing envelope fields
- **WHEN** API trả `{}`, items=null hoặc thiếu page/totalCount trong response 200
- **THEN** hiển thị lỗi contract có retry, không hiện 0 sản phẩm như dữ liệu hợp lệ

#### Scenario: Legitimate defaults and additive fields
- **WHEN** response có đủ trường, giá bằng 0, isActive=false, variantCount=0, specs={} và field bổ sung
- **THEN** các giá trị hợp lệ vẫn được map đúng, không bị xem là field thiếu

### Requirement: Latest request owns presentation
Client SHALL hủy truy vấn cũ khi truy vấn mới được áp dụng, khi rời trang hoặc đóng cửa sổ. Chỉ request hiện hành được cập nhật items, metadata, lỗi và trạng thái tải; cancellation dự kiến MUST NOT hiện thành lỗi người dùng. Các phụ thuộc đang dùng SHALL chỉ được giải phóng sau khi công việc kết thúc.

#### Scenario: Slow obsolete request
- **WHEN** submit A rồi B, B hoàn tất trước A và A bỏ qua cancellation
- **THEN** chỉ B được hiển thị; success/error/finally của A không thay đổi trạng thái B

#### Scenario: Leave while loading
- **WHEN** đổi trang hoặc đóng app trong lúc list/detail/lookup đang tải
- **THEN** hủy mọi request liên quan, không cập nhật UI đã rời và không dispose tài nguyên đang bị request cũ sử dụng

### Requirement: Recoverable catalog state
Client SHALL phân biệt initial Loading, Content, Empty, NotFound của detail và Error. Khi refresh/query mới lỗi, snapshot thành công trước đó SHALL được giữ với nhãn truy vấn cũ cùng thông báo lỗi và Retry; không tuyên bố snapshot thuộc truy vấn mới. Session thay đổi SHALL xóa dữ liệu thuộc phiên cũ.

#### Scenario: Refresh fails then retry succeeds
- **WHEN** đã có kết quả rồi query mới gặp network/timeout/500 và người dùng retry
- **THEN** giữ snapshot cũ có nhãn, retry dùng query thất bại, kết quả mới thay snapshot và xóa lỗi khi thành công

#### Scenario: Authorization response
- **WHEN** API trả 401 hoặc 403
- **THEN** báo thông điệp phù hợp, 401 không tự login/refresh, 403 không xóa phiên; dữ liệu bị từ chối truy cập không tiếp tục hiển thị như nội dung khả dụng

#### Scenario: Unknown detail
- **WHEN** GET chi tiết trả 404 với ProblemDetails hoặc body rỗng
- **THEN** hiện Không tìm thấy sản phẩm và Back, không hiện dữ liệu sản phẩm đã mở trước đó

### Requirement: Linux visual acceptance
Catalog SHALL được kiểm chứng với API thật và app desktop Linux trên build cụ thể. Giao diện SHALL theo hierarchy grid mẫu, dùng được ở 1440×1000, 1024×768 và 800×600 DIP, hỗ trợ tiếng Việt, keyboard, Light/Dark/System và text scale. Nghiệm thu chủ dự án SHALL là xác nhận riêng của change này.

#### Scenario: Responsive browsing
- **WHEN** resize thật giữa ba viewport, đổi theme/text scale 100/150/200% và dùng keyboard
- **THEN** card/toolbar reflow, filter/paging/Back vẫn truy cập được, chữ không mất dấu, focus rõ; thao tác trình bày không tự tải API lại

#### Scenario: Evidence and owner review
- **WHEN** bàn giao Catalog
- **THEN** có record Linux build/hash, API/dataset, command, viewport/scale, expected/actual, ảnh/log và kết quả tests; manual acceptance chỉ hoàn tất khi chủ dự án xác nhận build đã xem; không yêu cầu kiểm thử Windows trong change này

### Requirement: Readable specification names
Trang chi tiết và bộ lọc thông số SHALL hiện tên của định nghĩa thông số thay cho key, giá trị số kèm đơn vị của định nghĩa (`16 GB`) và giá trị Đúng/Sai là `Có`/`Không`. Key chưa có định nghĩa SHALL hiện chính key. Giá trị gửi lên API và giá trị lưu không đổi.

#### Scenario: Detail with defined specs
- **WHEN** chi tiết có specs `{"ram_gb": 16, "wireless": true}` và cả hai key có định nghĩa
- **THEN** trang hiện `RAM: 16 GB` và `Không dây: Có`

#### Scenario: Key without a definition
- **WHEN** chi tiết có spec `{"fan_rpm": 2000}` và `fan_rpm` chưa có định nghĩa
- **THEN** trang hiện `fan_rpm: 2000`

#### Scenario: Filter choices
- **WHEN** người dùng mở lọc thông số
- **THEN** ô tên hiện `RAM (7)` và ô giá trị hiện `16 GB (1)`, còn request vẫn gửi `spec=ram_gb:16`
