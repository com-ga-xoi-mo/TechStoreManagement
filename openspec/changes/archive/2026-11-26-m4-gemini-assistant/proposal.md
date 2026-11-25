# Proposal

## Why

M4 Sprint 1 (T. Vinh): "Làm UI chat và tools chỉ đọc: tìm sản phẩm theo ngân sách/specs, tồn khả dụng và báo cáo qua service/API hiện có. Server kiểm tra quyền ở từng tool, loại trường cấm trước khi gửi model." Hiện chưa có code AI nào. Nhân viên phải tự lọc sản phẩm, tồn và dashboard; Sales cần tư vấn nhanh từ tồn thật mà không thấy giá vốn hay báo cáo tổng.

## What Changes

- API mới `POST /api/assistant/chat` cho cả bốn role. Server giữ toàn bộ vòng gọi model ↔ tool trong một request; client chỉ gửi các lượt chữ của người dùng và trợ lý.
- Gọi model qua chuẩn **OpenAI-compatible** `/chat/completions` (`stream: false`), cấu hình bằng `Assistant__BaseUrl`, `Assistant__ApiKey`, `Assistant__Model`. Khi test dùng 9router (`ag/gemini-3.8-flash`); bản nộp có thể dùng endpoint OpenAI-compatible của Gemini mà không sửa code.
- Ba tool chỉ đọc, gọi service hiện có: `search_products` (`IProductService.SearchAsync` rồi `GetByIdAsync(includeCost: false)`, tối đa 10 chi tiết), `get_stock` (`IInventoryService`), `get_sales_report` (`IAnalyticsService`, chỉ Admin/Manager). Không SQL, không ghi dữ liệu.
- Server lọc tool theo role trước khi gửi model và kiểm tra lại quyền + tham số khi model gọi tool. Kết quả tool chỉ gồm các trường trong danh sách cho phép; không bao giờ có giá vốn, mô tả tự do, dữ liệu khách hoặc đơn.
- Giới hạn: tối đa 5 lần gọi tool mỗi câu hỏi, timeout tổng 60 giây, hủy theo request. Model lỗi/hết giờ/chưa cấu hình trả ProblemDetails rõ ràng.
- Client: mục Shell "Trợ lý AI" cho cả bốn role, trang chat C# Markup có trạng thái đang trả lời, nút hủy và lỗi có thể thử lại.

## Capabilities

### New Capabilities

- `ai-assistant`: API chat, tool chỉ đọc, quyền từng tool, giới hạn và xử lý lỗi.
- `frontend-ai-assistant`: trang chat và mục Shell.

## Impact

- Backend: module mới `backend/Assistant/` (controller, service, tools, options); port `IChatModelClient` + client HTTP OpenAI-compatible trong `backend/Common/Ai/`; đăng ký DI trong `Program.cs` (file chung của An, cần An review).
- Shared: `AssistantChatRequest`, `AssistantReplyDto`.
- Client: `ShellDestination.Assistant`, `RoleAccess`, `AssistantPage`, `AssistantViewModel`, `IAssistantRepository`; thêm overload POST timeout riêng vào `IApiClient`/`ApiClient`, không đổi timeout mặc định.
- Không có migration hay bảng mới; lịch sử chat chỉ sống trong ViewModel của phiên hiện tại.
- Docs: `docs/API_PERMISSIONS.md`, `AGENTS.md`, `backend/appsettings.example.json` (không có key).
- Báo cáo tài chính M4 của Bình (doanh thu thuần, giá vốn, lợi nhuận, bán chạy, hiệu suất thu ngân) đã vào `dev` (PR #35) nên thêm tool `get_financial_report` ngay trong change này.
- Ngoài phạm vi: streaming; lưu lịch sử chat.
