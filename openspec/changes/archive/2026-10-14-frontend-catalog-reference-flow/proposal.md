# Proposal

## Why

Dev 6 đã có transport, design system và Shell nhưng Sản phẩm vẫn là trang khung. Cần một feature đọc Catalog chạy xuyên suốt qua API thật để kiểm chứng MVVM + Repository và làm mẫu bàn giao cho Dev 2–5 trong Sprint 1.

## What Changes

- Thay trang khung Sản phẩm bằng grid theo `UI/stitch_techstore_retail_admin_dashboard/qu_n_l_s_n_ph_m_l_i_th_tinh_g_n_techstore_grid_view/`, tái sử dụng Shell, tokens, font/icon và shared controls.
- Thêm `IProductRepository`/`ProductRepository`, Model và mapping từ DTO hiện có qua `IApiClient`; truy vấn `GET /api/products` và `GET /api/products/{id}` thật.
- Hỗ trợ search bằng nút/Enter, brand, category, trạng thái hoạt động, khoảng giá, bộ lọc spec nâng cao, sort và paging theo contract server. Dùng thêm `GET /api/categories/tree` hiện có để lấy lựa chọn category, không suy ra danh mục từ trang sản phẩm đang tải.
- Mở chi tiết chỉ đọc bằng productId trong nhánh Sản phẩm, có Back; giữ sáu điểm vào Shell. Bổ sung cancellation, chống kết quả đến muộn, lỗi/empty/retry và snapshot có nhãn khi refresh lỗi.
- Ghi rõ khác biệt mẫu/API: không có stock/status tồn kho, swatch màu trên summary, count theo category hay brand facets; không gọi N detail requests để trang trí card, không fallback fixture khi API lỗi.
- Có tests headless transport/mapping/state, integration qua API + PostgreSQL thật, chạy app và manual visual acceptance trên **Linux/Uno Skia**. Theo yêu cầu chủ dự án, không thêm task build/test Windows hoặc gate WinApp CLI trên Windows cho change này.

## Capabilities

### New Capabilities

- `frontend-catalog-browsing`: Danh sách và chi tiết Catalog chỉ đọc, truy vấn thật, mapping trung thực, trạng thái tải/lỗi/cancellation và nghiệm thu Linux.

### Modified Capabilities

- `frontend-shell-navigation`: Sản phẩm chuyển từ placeholder sang feature đọc; hỗ trợ detail theo productId, Back giữ truy vấn nhưng tải lại dữ liệu, đồng bộ selection/title và bảo toàn lifecycle. Các trang khung khác vẫn reset-on-revisit.

## Impact

- Frontend: `Models/Catalog`, `Repositories/Catalog`, `ViewModels/Catalog`, `Views/Catalog`; composition root; mở rộng có giới hạn navigation/activation tracking hiện có.
- Tests: mở rộng `tests/Frontend.UnitTests` bằng production source-links; thêm suite integration frontend chạy opt-in với API đang chạy và dữ liệu seed đã biết. Tài liệu handoff Catalog được viết ở giai đoạn apply.
- Giữ .NET 10, Uno SDK 6.7.30 và CPM; ưu tiên controls WinUI và Community Toolkit đã có, không mặc định thêm package.
- Không sửa backend/shared contracts hoặc `product-catalog` spec; không CRUD, login/refresh, Inventory, offline cache, generic CRUD repository, `ProductApiClient` chỉ chuyển tiếp hay đổi UI sang Material. `docs/MVVM_REPOSITORY_BASE_PROJECT.md` ưu tiên hơn hướng dẫn frontend Feature-First cũ.
- Chỉ tạo planning artifacts trong proposal này; chưa triển khai code và chưa tuyên bố đã chạy các kiểm thử của change.
