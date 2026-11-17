# Design

## Decisions

### D1. Bằng chứng do AI chạy, người nghiệm thu cuối
AI chạy HTTP bằng `curl` trên API + PostgreSQL thật (database test riêng), tests tự động, và app Skia trên Linux. Bước cần thao tác/nhìn mà môi trường không hỗ trợ (Windows native, chụp màn hình Wayland, HighContrast) ghi "Chưa kiểm tra" kèm lý do; không tính là đạt.

### D2. 500
Không cố tạo lỗi server trên dữ liệu thật; xác nhận middleware trả ProblemDetails 500 bằng test có sẵn hoặc ghi "Chưa kiểm tra".
