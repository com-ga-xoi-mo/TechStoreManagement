# Tasks

## 1. Chuẩn bị và đối chiếu bằng chứng sẵn có

- [x] 1.1 Trước khi viết test, fetch và merge `origin/dev` mới nhất vào `feat/m3-manager-dashboard-access`, xác nhận ba archive của Bình và kiểm tra receiving trên build đã ghép. Build lại theo D1, xác nhận SHA source bằng Git và kết quả build qua output. Commit và push nhánh lên `origin` (không push hay merge vào `dev`), rồi mở `docs/M3_TEST_SUMMARY.md` với SHA đã push, SHA `origin/dev` đã chứa, ghi chú "chưa merge vào `dev`", môi trường, lệnh, target và vị trí log theo D1.
- [x] 1.2 Đối chiếu bằng chứng Dashboard Manager với `m3-manager-dashboard-access` và `m3-demo-data-reconciliation`, rồi tick và gắn cả hai nguồn vào checkbox trong mục Bình (hiện ở dòng 84). Đối chiếu tiêu chí số lượng, ảnh, giá vốn, tài khoản, seed lại giữ mật khẩu/dữ liệu và ngày fixture với bằng chứng `m3-demo-data-reconciliation`. Tick và gắn link vào hai mục Dữ liệu demo của Bình (165–166). Kiểm tra diff plan để xác nhận chỉ sửa đúng ba mục này và link mở tới bằng chứng.

## 2. Regression Admin M2

- [x] 2.1 Chạy các ca sẵn có cho sản phẩm theo D2, gồm vòng đời CRUD qua `CatalogManagementApiIntegrationTests`, đọc/lọc/paging qua `CatalogApiIntegrationTests` và `ProductSearchPostgresTests`. Bù ca HTTP/PostgreSQL còn thiếu cho tên rỗng/quá dài, giá 0 hợp lệ, giá âm và input sai theo contract. Xác nhận HTTP status, trường lỗi và read-back. Ghi từng mã ca, tự động/thao tác tay, Expected/Actual, SHA và bằng chứng vào phần regression.
- [x] 2.2 Chạy các ca danh mục theo D2. Xác nhận vòng đời create/đọc cây/update/xóa danh mục rỗng và tên rỗng/quá dài hoặc slug sai. Kiểm tra cây sau request lỗi. Dùng test hiện có hoặc bù ca HTTP trong lớp mới của Bình nếu thiếu. Chạy ca tìm trong cây bằng `CategoryWorkspaceTests` và ghi riêng kết quả headless.
- [x] 2.3 Chạy các ca khách hàng theo D2 bằng Admin, gồm tạo, đọc, sửa, deactivate/activate, tìm tên/điện thoại, lọc tier/active và phân trang. Kiểm tra tên rỗng/quá giới hạn và input phone/tier sai. Dùng các test hiện có, bù các assertion thiếu qua HTTP/PostgreSQL trong lớp mới nếu cần. Ghi Expected/Actual và kiểm tra read-back sau lỗi. Sau khi cả 2.1–2.3 đủ bằng chứng đạt, gắn link regression và tick riêng checkbox Admin M2 của Bình (154).

## 3. Đối chiếu sau commit và lỗi checkout

- [x] 3.1 Dùng lại bằng chứng live flow và chạy có lọc `DashboardLiveFlowPostgresHttpTests` với PostgreSQL thật. Đối chiếu checkout 201 và Sold 409 theo D3. Ghi tồn, giữ chỗ, serial, ledger, tiền ca và Dashboard trước/sau, SHA và log. Đánh dấu 409 là xung đột tuần tự.
- [x] 3.2 Đối chiếu lỗi HTTP 400 tiền nhận thiếu trên PostgreSQL test, bổ sung ca trong lớp mới của Bình nếu chưa có bằng chứng tương đương. Kiểm tra trường `cashTendered`, đơn/dòng, tồn, serial, lịch sử và tiền ca qua context đọc mới. Ghi kết quả 400 riêng với 409. Khi có bằng chứng đạt cho cả thành công và lỗi, tick và liên kết đúng checkbox commit/rollback của Bình (155).

## 4. Tổng hợp và bàn giao Sprint 5

- [x] 4.1 Ghi kết quả backend unit, PostgreSQL/HTTP và frontend headless trên build bàn giao, gồm lượt opt-in và lượt có lọc đã chạy. Đối chiếu bộ đếm runner với log/TRX và ghi SHA, lệnh cùng phạm vi của mỗi suite. Ghi riêng kết quả từ các build lịch sử.
- [x] 4.2 Gom bằng chứng RBAC/Identity của An, POS/ca của Đạt, pricing/khách của Vy, UI/HTTP methods của T. Vinh và Dashboard/seed/live flow của Bình theo D4. Với mỗi mã ca, kiểm tra đủ owner, đầu vào, Expected/Actual, SHA, lệnh hoặc link, trạng thái và dữ liệu lỗi/chạy lại theo D5. Nếu bằng chứng thiếu trường, ghi rõ phần chưa xác nhận.
- [x] 4.3 Kiểm tra bằng chứng concurrency của Q. Vinh và Đạt trên PostgreSQL, gồm giao dịch độc lập, điều kiện chồng thời gian và kết quả cuối. Ghi mục chưa xác nhận cùng receiving, luồng nhập → bán → Dashboard, giảm cấp đơn qua POS, chọn khách trên POS desktop và UI Windows native trong bảng còn mở. Xác nhận mỗi mục có đúng owner và điều kiện chạy lại cho An.
- [x] 4.4 Chốt báo cáo `docs/M3_TEST_SUMMARY.md` và kiểm tra các liên kết nội bộ đến [Dashboard Manager](../../../docs/M3_TEST_SUMMARY.md#dashboard-manager), [Dữ liệu demo và đối soát KPI](../../../docs/M3_TEST_SUMMARY.md#dữ-liệu-demo-và-đối-soát-kpi) và [Đối soát giao dịch POS live](../../../docs/M3_TEST_SUMMARY.md#đối-soát-giao-dịch-pos-live). Kiểm đếm lại Pass/Fail/Skipped/Chưa kiểm tra với mẫu số theo D5, đối chiếu lỗi đã sửa và trạng thái rerun. Khi owner đã bàn giao hoặc đến hạn đã ghi đủ các mục Chưa kiểm tra, tick riêng dòng tổng hợp của Bình (171) và gắn link báo cáo. Kiểm tra diff cuối chỉ gồm các file được phép và các mục thuộc Bình trong plan. Kiểm tra báo cáo tự chứa theo D5: mỗi lượt chạy có lệnh, dòng tổng runner và trích đoạn đã lọc credential ngay trong tài liệu, không chỉ trỏ tới log local. Commit, push nhánh và cập nhật SHA cuối đã push trong báo cáo. Chốt bàn giao với 9 mục Chưa kiểm tra, mỗi mục có owner và điều kiện chạy lại trong báo cáo.

## Workflow follow-up

- Archive theo workflow sau khi chốt bàn giao. Nếu đến hạn còn ca chờ owner, giữ trạng thái Chưa kiểm tra và điều kiện chạy lại trong báo cáo. Bổ sung bằng chứng sau khi có build mới.
