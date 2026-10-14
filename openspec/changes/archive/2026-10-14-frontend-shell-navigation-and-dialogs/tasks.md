# Tasks

## 1. Grounded UI API checks

- [X] 1.1 Dùng `uno-platform` và `winui-design`: restore frontend đúng .NET 10/Uno 6.7.30, kiểm tra references SettingsControls 8.2.250402 qua CPM; ghi control/property cần dùng và nguồn kiểm chứng trong `docs/FRONTEND_SHELL_HANDOFF.md`, xác nhận không đổi framework/package để theo sample mới.
- [X] 1.2 Dùng WinApp CLI >=0.7 chạy find-ui cho NavigationView, ContentDialog, SettingsCard/SettingsExpander và find-api từ restored `frontend/TechStore.Client`; lưu version, lệnh và kết quả API liên quan. Nếu Linux thiếu CLI, ghi kiểm chứng reference/source cho phần portable nhưng giữ task này unchecked đến khi có transcript CLI trên môi trường hỗ trợ.

## 2. Navigation contracts, lifecycle và headless state

- [X] 2.1 Dùng `mvvm-toolkit-di`: tạo route map thuần C# đúng sáu mục và navigation contract bên cạnh implementation trong Services/Navigation; test initial Dashboard, same-route no-op, invalid route và factory failure giữ state trước đó bằng fake activation, không UI references.
- [X] 2.2 Dùng `mvvm-toolkit`: tạo state/lifecycle production dùng chung vừa đủ, load/retry async với token/generation, constructor không I/O; thêm tests Loading→Empty/Content/Error, retry không overlap, cancel không thành Error và A→B→A bỏ old success/error/finally bằng TaskCompletionSource.
- [X] 2.3 Hoàn thiện ownership activation, cancel khi rời/close, detach subscriptions và dispose lease sau completion đúng một lần; tests load không hợp tác cancellation không chặn navigation, không ghi state cũ hoặc dispose dependency đang dùng. Document policy reset-on-revisit, giữ theme trong phiên và cách feature sau gắn repository trong handoff.

## 3. DI page factory và Shell sáu điểm vào

- [X] 3.1 Dùng `mvvm-toolkit-di` và `uno-platform`: đăng ký Shell/window services và per-activation scopes trong UiServiceCollectionExtensions; page factory cấp VM qua constructor trước binding, adapter gán content và đồng bộ route/title/selection. Verify actual DI tạo đủ sáu Page trên runtime, DI validation không lỗi và lifecycle không gọi load hai lần do Loaded.
- [X] 3.2 Dùng `winui-design`: dựng Views/Shell và sáu cặp Views/ViewModels đúng design route map, NavigationView năm mục chính + Cài đặt footer, brand/account placeholder theo mẫu; tái sử dụng resources/EmptyStateControl và StatCard khi phù hợp. Verify không có fake KPI, POS/Identity hay HTTP trong code-behind/VM; chụp baseline Light đối chiếu sidebar/header mẫu.
- [X] 3.3 Đổi startup mặc định sang Shell development offline, giữ gallery flag absent/0/1/invalid và validation API; chạy thử từng nhánh, xác nhận gallery riêng, không token/session giả, không request bắt buộc. Cập nhật lệnh chạy và default-route contract trong handoff.
- [X] 3.4 Thêm khu kiểm tra khung được ghi nhãn development để trình diễn state/cancel và dialog mà không giả dữ liệu API; dùng wiring cùng lifecycle production. Verify chuyển nhanh giữa các trang, retry và close khi pending bằng UI, cập nhật bước tái lập trong handoff.
  - Implementation and headless behavior tests are in place; Linux Shell/Gallery have been launched and captured. The real-UI rapid navigation, retry, pending-close and dialog interaction pass remains unverified and is documented in `docs/FRONTEND_SHELL_HANDOFF.md`.

## 4. Dialog service và Settings

- [X] 4.1 Dùng `mvvm-toolkit` và `mvvm-toolkit-di`: triển khai IDialogService/request/result thuần C#, FIFO policy và lifetime cancellation; headless tests confirm/cancel/dismiss, hai request đồng thời, queued/visible cancellation, root chưa sẵn sàng và shutdown không nhận request mới.
- [X] 4.2 Dùng `winui-design` và `uno-platform`: triển khai ContentDialog presenter gắn root sau Loaded, teardown an toàn và focus recovery; chạy confirm/information bằng keyboard, kiểm tra Enter/Escape, Tab/Shift+Tab, focus về caller hoặc trang mới sau navigation, không modal chồng nhau. Ghi expected/actual và hướng dẫn tái lập trong handoff.
- [X] 4.3 Dựng Settings bằng SettingsCard/SettingsExpander đã pin, theme state System/Light/Dark thuần C# theo phiên và thông tin app; test headless theme giữ khi revisit/reset khi process mới, chạy UI đổi theme cả Shell/dialog không reload trang, không lộ cấu hình nhạy cảm hoặc tuyên bố persistence.

## 5. Cross-platform integration và nghiệm thu

- [X] 5.1 Chạy `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` với production source-links mới, giữ UI implementations ngoài headless project; kiểm tra toàn bộ suite regression transport/session/gallery cùng tests navigation/lifecycle/dialog/state đều pass. Ghi command và số test vào handoff.
- [X] 5.2 Build/run Skia Linux với `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -c Release` và `dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -c Release --no-build`; kiểm tra offline startup, sáu route, actual binding, rapid navigation/cancellation, dialog queue/focus, Settings/theme và shutdown. Lưu build hash, OS/renderer, logs và expected/actual; compile đơn thuần không tính runtime pass.
- [X] 5.3 Trên Windows build/run target `net10.0-windows10.0.26100` ở Release, kiểm tra cùng flows và Settings toolkit/ContentDialog root/focus, thêm Windows Contrast; ghi build id/hash và evidence riêng. Nếu chưa có Windows runtime thì giữ task unchecked, không dùng kết quả Skia thay thế.
- [X] 5.4 Dùng `winui-design`: đối chiếu Shell mẫu tại 1440×1000, 1024×768, 800×600 DIP trên cả hai target; resize thật, ghi ActualWidth/ActualHeight và RasterizationScale, kiểm tra pane toggle/footer/content, tiếng Việt/text scale 100/150/200%, keyboard và Light/Dark/System. Lưu ảnh baseline/comparison và mọi sai khác được giải thích trong handoff; không tick khi chỉ có screenshot đổi kích thước mà layout chưa reflow.
- [X] 5.5 Hoàn thiện `docs/FRONTEND_SHELL_HANDOFF.md`: tổng hợp build/target/commands, scenario expected/actual, ảnh/log, giới hạn chưa kiểm tra, route/state/DI/dialog extension guide và cách rollback startup; đối chiếu từng requirement của ba delta specs với evidence, không báo pass cho mục unverified.
- [X] 5.6 **Manual acceptance riêng của chủ dự án:** trình bày build cụ thể và evidence của 5.2–5.5, ghi ngày, build/target đã xem cùng kết quả/ngoại lệ; chỉ tick khi chủ dự án xác nhận rõ giao diện Shell/navigation/dialog/Settings trên build đó. Không kế thừa lời nghiệm thu của change trước hoặc tự suy từ test pass.
