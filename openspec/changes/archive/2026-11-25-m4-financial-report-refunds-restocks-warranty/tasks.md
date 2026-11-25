# Tasks

## 1. Xác nhận nguồn và baseline lúc apply

- [x] 1.1 Đối chiếu `origin/dev` mới nhất, xác nhận nhánh triển khai có cả change phần bán và migration của các nguồn hậu mãi. Cập nhật kết quả bàn giao theo D1 và ghi SHA nguồn/build vào phần mới của `docs/M4_FINANCIAL_REPORT.md`.

## 2. Hoàn, thực nhập và bảo hành trong Analytics

- [x] 2.1 Tổng hợp các dòng thực chi theo `RefundedAt`, giữ tiền hàng/thuế/chi tách riêng và phân biệt hoàn trước giao. Đối chiếu HTTP/PostgreSQL các ca hoàn một phần rồi hết, `Processing` hoàn toàn bộ, phiếu chờ/thất bại, legacy thiếu phân bổ và một khoản chi bị tách dòng sau kiểm định. Ghi Expected/Actual vào tài liệu bằng chứng.
- [x] 2.2 Tính `restockedCost` từ movement dương `CustomerReturn` liên kết dòng trả. Kiểm chứng thời điểm khác ngày hoàn ở hai phía biên 17:00Z, lượng thực nhập một phần, hàng lỗi/chờ sửa, cờ có mà thiếu movement, giá vốn 0/thiếu và lượt kiểm định lặp. Ghi kết quả vào tài liệu bằng chứng.
- [x] 2.3 Tính riêng `warrantyCost` từ snapshot giá vốn máy thay và thời điểm movement xuất. Đọc API báo cáo sau khi gọi luồng bảo hành thật, kiểm chứng xuất lặp vẫn một khoản, nhận sửa/trả máy cũ trả 0 cho chi phí máy thay, vốn thiếu trả `null` và lãi gộp độc lập. Ghi kết quả hoặc điều kiện chạy lại với Q. Vinh.

## 3. Tiền theo kênh, ca và hợp đồng API

- [x] 3.1 Gom thu Cash theo ca bán, chi Cash theo payout/ca thực chi và tách QR xác nhận theo chứng từ. Kiểm tra hai ca khác nhau, ca trọn kỳ và ca cắt kỳ, số đầu ca + thu − chi đối chiếu số dự kiến, payout lặp, khoản thiếu chứng từ và QR hoàn thủ công/QR chờ. Kiểm chứng thu QR bằng fixture đã xác nhận. Nếu chưa có luồng thu/đối soát đầu cuối, ghi riêng **Chưa kiểm tra**, owner Đạt và tiêu chí chạy lại ở D1.
- [x] 3.2 Ghép các tập đã gom trong cùng snapshot, map các trường cũ và mới theo D5. Kiểm tra qua JSON cũ và JSON mở rộng, số 0 khác `null`, tổng ngày = tổng kỳ với nguồn đầy đủ, số thuần âm, `asOf` loại trừ và ngày chỉ có hậu mãi. Lưu số đối chiếu qua HTTP/PostgreSQL vào tài liệu bằng chứng.

## 4. Chốt nghiệm thu trong phạm vi Bình

- [x] 4.1 Chạy bộ tích hợp với các nguồn sẵn sàng và fixture Expected D6, đọc lại báo cáo sau thao tác lặp và đối chiếu từng số kể cả tổng kỳ. Chạy hồi quy quyền Admin/Manager, 401/403, kỳ 400, Dashboard hiện hành, top và hiệu suất phần bán. Ghi SHA build, Expected/Actual, trạng thái và giới hạn vào duy nhất `docs/M4_FINANCIAL_REPORT.md`. Mọi ca chưa chạy được qua nguồn thật ghi **Chưa kiểm tra**, owner và điều kiện chạy lại.
- [x] 4.2 Rà diff để xác nhận phạm vi implementation chỉ gồm Analytics, test báo cáo của Bình và `docs/M4_FINANCIAL_REPORT.md`. Xác nhận không thêm script, log hay Markdown tạm. Nếu commit, kiểm tra message không có trailer `Co-Authored-By` của AI.
