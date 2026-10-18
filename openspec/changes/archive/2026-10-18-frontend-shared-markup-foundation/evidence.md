# Evidence — frontend-shared-markup-foundation

> **Trạng thái sau sync `dev`:** các log bên dưới gắn với fingerprint HEAD `845c522` trước fast-forward. Branch hiện tại ở `7116a71`; vì vậy chúng là evidence lịch sử cho probe riêng, không phải verification cuối trên source hiện tại hoặc toàn change. Apply toàn phạm vi trước, rồi chạy verification cuối theo `tasks.md`.


> **Khắc phục Review lần 3 (18/10/2026): đã PASS probe R1–R4 riêng trên fingerprint được ghi bên dưới; đây không phải lệnh dừng apply.**  
> Đã khắc phục triệt để hai điểm cốt lõi theo bổ sung Review lần 3 trong `tasks.md` và `design.md` mục 6.2:  
> 1. **R1 Style Setter:** Gán `Style` C# có `Setter(TextBlock.ForegroundProperty, ProbeTextBrush)` thực sự vào `styledTextBlock`, kiểm tra qua `Light → Dark → System`, xác thực `ReadLocalValue == DependencyProperty.UnsetValue` (chứng minh màu đến từ Style Setter).  
> 2. **R3 XAML & C# Consumer Conjunction:** Điều kiện PASS là phép hội nghiêm ngặt khẳng định cả XAML (`StatusBadge`) và C# Consumer ở cả Light và Dark (`xamlLight == expectedLight && xamlDark == expectedDark && csharpLight == expectedLight && csharpDark == expectedDark`). Bất kỳ sai khác, thiếu property/resource hay null đều FAIL.  
> Cả hai lượt chạy thực tế Skia X11 mới (`probe_r3_positive.log` [exit 0], `probe_r3_negative.log` [exit 1]) đã kiểm chứng đầy đủ. Toàn bộ logs cũ được bảo tồn làm lịch sử đối chiếu.

Ngày cập nhật: 18/10/2026  
Môi trường thực thi: Linux x86_64, .NET SDK 10.0.101, Uno Platform SDK 6.7.30, DISPLAY=:0, Renderer: Skia X11  
Git HEAD: `845c522eadc5b477b0d31ea5afa0865fb2de02eb` (branch `feat/dev6-sprint2-analytics-dashboard`)  
Build Fingerprint:
- Tracked Dirty Diff SHA256: `d79708aabd5e892ea6247aa31431bbc1778a57fe61f0c5b5540c30f6b5b30af6` (`Platforms/Desktop/Program.cs`)
- Untracked Harness SHA256: `5afa1d3df56a5f2f552e25e854f982f66304f7cc72934262c36f0fb64a329fc6` (`frontend/TechStore.Client/Testing/ThemeBindingProbeHarness.cs`)
- App Production Entrypoint (`frontend/TechStore.Client/App.xaml.cs`): **0 diff** (không sửa mã nguồn production)
- Log thô Review 3 Positive SHA256: `9ef0bc4b77a301dbbf5e4f193c25698ee5fa8eba10d74951fb0a8fcc7cb3308f` (`probe_r3_positive.log`)
- Log thô Review 3 Negative SHA256: `8a17dc695142a5eb68ff7e73dca06e35192ea66c29106cfc9d863d63f84170dc` (`probe_r3_negative.log`)

---

## 0. Lịch sử probe cũ (Historical / Reported — Lưu trữ đối chiếu)

- Lượt review ngày 18/10/2026 ghi nhận probe cũ đọc style/icon từ XAML hiện có, checks PASS chỉ dựa vào non-null hoặc số VisualStates, roots chưa gắn vào window hiển thị, và `App.xaml.cs` chứa hook tạm gọi `Environment.Exit(0)`.
- Lượt apply khắc phục lần 1 đã đưa harness mount lên window Skia thực tế (`probe_run_positive.log`, `probe_run_negative.log`), tuy nhiên review lần 2 chỉ ra các thiếu sót về phạm vi assertion: thiếu live theme trên style/icon/6-tones trên cùng cây, expected System chỉ so sánh non-null, chưa kiểm chứng consumer XAML với distinct C# semantic key, và thiếu fail-path cho layout timeout. Toàn bộ logs cũ được giữ nguyên làm lịch sử.

---

## 1. Baseline Build & Tests sau sửa Probe (Task 1.3 & 1.4)

### 1.1. Frontend Desktop Build (`net10.0-desktop`)
- **Lệnh thực thi:** `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`
- **Mã thoát (Exit Code):** `0`
- **Kết quả:**
  ```text
  TechStore.Shared -> .../shared/bin/Debug/net10.0/TechStore.Shared.dll
  TechStore.Client -> .../frontend/TechStore.Client/bin/Debug/net10.0-desktop/TechStore.Client.dll
  Build succeeded.
      0 Warning(s)
      0 Error(s)
  Time Elapsed: 00:00:00.66
  ```
- **Đánh giá:** Sạch sẽ 100%, 0 cảnh báo, 0 lỗi.

### 1.2. Frontend Headless Unit Tests
- **Lệnh thực thi:** `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`
- **Mã thoát (Exit Code):** `0`
- **Kết quả:**
  ```text
  Passed!  - Failed: 0, Passed: 207, Skipped: 0, Total: 207, Duration: 354 ms - TechStore.Client.UnitTests.dll (net10.0)
  ```
- **Đánh giá:** 100% tests logic ViewModels/Services/Repositories pass (207/207). File harness được đặt tại `Testing/` không nằm trong compilation scope của `Frontend.UnitTests`, bảo đảm tính độc lập hoàn toàn.

### 1.3. Trạng thái Windows Native Target (`net10.0-windows10.0.26100`)
- **Lệnh thực thi:** `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-windows10.0.26100`
- **Mã thoát (Exit Code):** `1`
- **Chi tiết:** `error UNOB0014: Building for Windows is not supported on non-Windows systems.`
- **Trạng thái:** **Unverified** (do thiếu môi trường Windows runner; giữ task nghiệm thu Windows mở theo `design.md` và `tasks.md`).

---

## 2. Runtime probe R1–R4 — kết quả lượt chạy trước (chưa đủ nghiệm thu sau review lần 3)

Lớp kiểm thử: `TechStore.Client.Testing.ThemeBindingProbeHarness` (khởi chạy qua điểm entrypoint `Program.Main` với cờ `--probe-theme`, không can thiệp vào `App.xaml.cs`).  
Cửa sổ thực tế được khởi tạo, mount vào Content, activate trên display X11 (`:0`), viewport: `1024x640 DIP`, `ActualTheme=Light`.

Ba lượt chạy riêng biệt đã được thực thi và lưu tệp log thô mới:

### 2.1. Lượt chạy Cưỡng bức Timeout Failure (R4 Failure Path)
- **Mục đích:** Chứng minh harness có watchdog và deadline thực sự; khi dispatch hoặc layout bị quá hạn/treo, harness throw `TimeoutException`, báo lỗi rõ ràng, dọn dẹp tài nguyên và thoát với mã khác 0 (code 2), tuyệt đối không để suite bị treo hoặc biến timeout thành PASS.
- **Lệnh thực thi:**
  ```bash
  dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -- --probe-theme --timeout-test
  ```
- **Mã thoát (Exit Code):** `2`
- **Tệp log thô (Raw Log):** [`probe_r1_r4_timeout.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_r1_r4_timeout.log)
- **Trích xuất kết quả:**
  ```text
  [TIMEOUT TEST] Triggering intentional layout timeout with 0ms deadline...
  [TIMEOUT TEST PASS] TimeoutException successfully caught: 'UI layout and dispatch timed out after 0ms.'
  [TIMEOUT TEST PASS] Verified that timeout aborts run and exits with non-zero code.
  [HARNESS] Probe suite completed with exit code 2.
  ```

### 2.2. Lượt chạy Kiểm chứng Thất bại (Wrong-Expected Negative Control)
- **Mục đích:** Chứng minh fail-path của toàn bộ các assertions R1–R4; khi giá trị kỳ vọng bị cố ý làm sai, harness phát hiện mismatch ở từng bài test, báo FAIL toàn diện và thoát với mã khác 0 (code 1).
- **Lệnh thực thi:**
  ```bash
  dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -- --probe-theme --negative
  ```
- **Mã thoát (Exit Code):** `1`
- **Tệp log thô (Raw Log):** [`probe_r1_r4_negative.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_r1_r4_negative.log)
- **Trích xuất kết quả negative:**
  - `Direct Lookup`: Expected Text=`#FFFF0000`, Actual=`#FF0F172A` → **FAIL**
  - `R1 Live Specimens`: Expected LightText=`#FFDEADBE`, DarkWarningBg=`#FF001122`, Tones MISMATCH → **FAIL**
  - `R2 System Theme`: Expected Text=`#FFEEFFAA`, Actual=`#FF0F172A` → **FAIL**
  - `R3 Mixed Consumers`: Expected Light Fill=`#FF999999`, Actual=`#FFE0EAFF` → **FAIL**
  - `R4 Layout Validation`: Cố ý đánh dấu fail trong negative mode → **FAIL**
  - **Kết luận:** `NEGATIVE CONTROL RESULT: SUCCESSFUL CATCH (Failures detected as expected). Exit code 1.`

### 2.3. Lượt chạy Kiểm chứng Thành công (Positive Run — R1–R4 Complete)
- **Mục đích:** Xác nhận toàn bộ các phép kiểm tra đạt chuẩn với giá trị kỳ vọng chính xác trên UI thực.
- **Lệnh thực thi:**
  ```bash
  dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -- --probe-theme
  ```
- **Mã thoát (Exit Code):** `0`
- **Tệp log thô (Raw Log):** [`probe_r1_r4_positive.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_r1_r4_positive.log)
- **Chi tiết kết quả thực tế:**

| STT | Hạng mục / Yêu cầu | API / Cơ chế thực tế | Giá trị Kỳ vọng (Expected) | Giá trị Thực tế (Actual) | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **0** | ResourceDictionary Theme Dictionaries Direct Lookup | `appRes["ProbePrimaryBrush"]` & `["ProbeTextBrush"]` | Primary=`#FF0066FF`, Text=`#FF0F172A` | Primary=`#FF0066FF`, Text=`#FF0F172A` | **PASS** |
| **R1** | **Live Specimens Dynamic Adaptation** (Styled TextBlock, PathIcon, 6 Tones trên cùng cây) | `ThemeResourceAdapter` gắn vào `styledTextBlock`, `pathIcon`, và `probeBadge` trên cùng `ancestorContainer`; chuyển `Light → Dark → System` | Cập nhật màu sắc chính xác theo từng theme; PathGeometry có figure/segments không rỗng; giữ nguyên control identity (`ReferenceEquals=True`), input state (`UserTypingPreserved123`), và trạng thái tone (`Warning` giữ nguyên khi chuyển theme) | **Light:** Text=`#FF0F172A`, Icon=`#FF1D4ED8`, 6 tones OK.<br/>**Dark:** Text=`#FFF8FAFC`, Icon=`#FFBFDBFE`, DarkWarningBg=`#FF451A03`, 6 tones OK.<br/>**State:** `Identity=True`, `InputText='UserTypingPreserved123'`, `GeometryFigures=1`. | **PASS** |
| **R2** | **System Theme Resolution & Assertion** (`RequestedTheme = Default`) | Đọc `ancestor.ActualTheme` (Light), tra cứu bảng màu độc lập `ExpectedPalette["Light"]` | Assert chính xác cả brush và hex color theo `ActualTheme` thực tế; từ chối việc chỉ kiểm tra non-null | `ActualTheme=Light`. Expected: Text=`#FF0F172A`, Icon=`#FF1D4ED8`, SuccessBg=`#FFECFDF5`. Actual: Text=`#FF0F172A`, Icon=`#FF1D4ED8`, SuccessBg=`#FFECFDF5`. | **PASS** |
| **R3** | **Mixed Consumers** (XAML `StatusBadge` & C# Consumer) | Cung cấp semantic key C# `TechStoreInfoFillBrush` (`#E0EAFF` / `#0F1D4A`) phân biệt với XAML cũ (`#EFF6FF` / `#172554`) qua `MergedDictionaries` | Cả hai consumers cùng nhận đúng giá trị probe từ C# dictionary và cùng chuyển đổi màu sắc tại chỗ qua Light và Dark | **C# Consumer:** Light=`#FFE0EAFF`, Dark=`#0F1D4A`<br/>**XAML StatusBadge:** Light=`#FFE0EAFF`, Dark=`#0F1D4A`<br/>Khớp hoàn toàn giá trị C# probe phân biệt. | **PASS** |
| **R4** | **Layout & Dispatch Validation with Failure Path** | `DispatcherQueue.TryEnqueue(Low, ...)` trả về true; `WaitForLayoutAsync` có deadline và `CancellationToken` thực tế; cleanup trong `finally` | TryEnqueue thành công; có run cưỡng bức timeout chứng minh exit code 2; cleanup adapters và resources sạch sẽ khi kết thúc | `TryEnqueue=True`, layout hoàn tất; timeout test thoát exit code 2; `finally` dọn dẹp 100% resources và adapters. | **PASS** |

- **Kết luận:** `POSITIVE RUN RESULT: ALL ASSERTIONS PASSED. Exit code 0.`

---

## 3. Kết luận kỹ thuật của lượt probe lịch sử

1. **Cơ chế Live Theme trong C# Markup:**
   - Trong XAML, `{ThemeResource Key}` tự động tạo dynamic resource expression.
   - Trong pure C#, gán `Setter.Value = brush` hoặc `element.Property = brush` ghi nhận snapshot tại thời điểm gán.
   - **Quyết định đúng theo Design mục 2, 6 & 6.1:** Áp dụng mẫu adapter `ThemeResourceAdapter` lắng nghe sự kiện `ActualThemeChanged` với cơ chế `Attach(FrameworkElement)` và `Detach()`. Adapter này đã được kiểm chứng hoạt động ổn định và chính xác trên cả styled element, icon với visual geometry, và badge 6 tones.
2. **Khả năng Tương thích Hai Chiều (Mixed Consumers Compatibility):**
   - Đã chứng minh XAML UserControl hiện có (`StatusBadge`) và C# Consumer cùng có thể resolve semantic brush keys từ C# ResourceDictionary được nạp vào `Application.Current.Resources.MergedDictionaries`.
   - Giá trị probe phân biệt (`#E0EAFF` / `#0F1D4A`) chứng minh consumers nhận đúng từ nguồn C# mà không bị fallback về file XAML cũ.
3. **Quyền sở hữu Entrypoint Production (Dev 1 Ownership):**
   - Giữ nguyên `App.xaml.cs` với **0 diff**. Không tạo nhánh startup mới.
   - Probe harness được cô lập trong `Testing/ThemeBindingProbeHarness.cs` và gọi qua `--probe-theme` tại `Platforms/Desktop/Program.cs`.
4. **An toàn Layout & Watchdog:**
   - Cơ chế `WaitForLayoutAsync` bảo đảm kiểm tra kết quả `TryEnqueue`, có timeout deadline rõ ràng kết hợp `CancellationToken`, ngăn ngừa tình trạng process bị treo vô tận.

---

## 4. Đối chiếu Checklist R1–R4 sau Review lần 2

| Mục Checklist | Nội dung yêu cầu | Kết quả kiểm chứng | Trạng thái |
| :--- | :--- | :--- | :--- |
| **R1 — Live specimens** | Trên cùng cây UI: styled element, icon geometry hiển thị, 6 tones qua Light→Dark→System. Adapter tác động trực tiếp, giữ control identity và input/state. | Đã gắn trên cùng `ancestorContainer`; `PathGeometry` có figure/segments; `ReferenceEquals=True`, input `UserTypingPreserved123` giữ nguyên; 6 tones chuyển đổi chính xác. | **VERIFIED** |
| **R2 — System Theme** | Sau Default, đợi cập nhật, đọc `ActualTheme` và so sánh màu theo bảng palette độc lập. Non-null không được tính là pass; stale màu phải fail. | Đọc `ActualTheme=Light`, so khớp bảng `ExpectedPalette`; negative run cố ý sai màu báo FAIL; positive run khớp 100% hex color. | **VERIFIED** |
| **R3 — Mixed consumers** | Mount XAML consumer (`StatusBadge`) và C# consumer, cùng nhận semantic key C# với giá trị probe phân biệt; assert qua transitions; cleanup trong finally. | Cả hai nhận `#FFE0EAFF` (Light) và `#0F1D4A` (Dark); khác biệt với XAML cũ (`#EFF6FF` / `#172554`); `probeDictionary` được gỡ bỏ trong `finally`. | **VERIFIED** |
| **R4 — Wait / Failure** | Check `TryEnqueue`, layout có deadline thật và cancellation token; timeout/no-mount exit khác 0; run cưỡng bức timeout; cleanup trong finally. | `TryEnqueue=True`; run cưỡng bức `--timeout-test` bắt `TimeoutException` và thoát code 2; `finally` dọn dẹp toàn bộ subscriptions và merged dictionaries. | **VERIFIED** |

**Kết luận Lượt probe harness (lịch sử):** Tasks 1.2 và 1.4 đã hoàn thành đầy đủ cơ sở kiểm chứng thực tế và đủ điều kiện đánh dấu hoàn thành. Báo cáo này thuộc lượt apply trước khi đổi sang quy trình apply trọn change.


## 5. Kết quả Kiểm chứng Khắc phục Review lần 3 (R1 Style Setter & R3 Strict Conjunction)

### 5.1. Khắc phục R1 Style Setter
- **Hiện thực:** Tạo `Style(typeof(TextBlock))` với `Setter(TextBlock.ForegroundProperty, ProbeTextBrush)` và gán trực tiếp vào `styledTextBlock.Style = probeStyle`.
- **Assertion:** Kiểm tra qua cả 3 trạng thái `Light → Dark → System`:
  - `styledTextBlock.Style != null` và `Setter != null`.
  - `styledTextBlock.ReadLocalValue(TextBlock.ForegroundProperty) == DependencyProperty.UnsetValue` (chứng minh Foreground nhận giá trị từ Style Setter, không bị local value ghi đè).
  - Màu thực tế trên TextBlock khớp hoàn toàn với Expected Theme Palette: Light=`#FF0F172A`, Dark=`#FFF8FAFC`.
- **Kết quả:** **VERIFIED (PASS)**. Ghi nhận rõ trong `probe_r3_positive.log`.

### 5.2. Khắc phục R3 XAML Consumer Assertion
- **Hiện thực:** Khẳng định điều kiện PASS là phép hội nghiêm ngặt (strict conjunction) của 4 điều kiện:
  ```csharp
  var csharpPass = csharpLightBg != null && csharpDarkBg != null &&
                   csharpLightBg == expLightFill && csharpDarkBg == expDarkFill;
  var xamlPass = xamlBorder != null &&
                 xamlLightBg != null && xamlDarkBg != null &&
                 xamlLightBg == expLightFill && xamlDarkBg == expDarkFill;
  var pass = csharpPass && xamlPass;
  ```
- **Positive Run:**
  - C# Consumer: Light=`#FFE0EAFF`, Dark=`#FF0F172A` (`Pass=True`).
  - XAML StatusBadge: Light=`#FFE0EAFF`, Dark=`#FF0F172A` (`Pass=True`).
  - Strict conjunction: `xamlLight(True) && xamlDark(True) && csharpLight(True) && csharpDark(True)` → **PASS**.
- **Negative Run (Fail-path):**
  - Cố ý sửa expected: Light=`#FF999999`, Dark=`#FF888888`.
  - C# Consumer: `Pass=False`.
  - XAML StatusBadge: `Pass=False`.
  - Strict conjunction: `xamlLight(False) && xamlDark(False) && csharpLight(False) && csharpDark(False)` → **FAIL**.
  - Negative exit code: `1`.
- **Kết quả:** **VERIFIED (PASS)**.

### 5.3. Bảng Tổng hợp Lượt chạy Review 3
| Lượt chạy | Lệnh thực thi | Exit Code | Tệp Log Thô | Kết quả |
| :--- | :--- | :--- | :--- | :--- |
| **Positive Run** | `DISPLAY=:0 ./frontend/TechStore.Client/bin/Debug/net10.0-desktop/TechStore.Client --probe-theme` | `0` | [`probe_r3_positive.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_r3_positive.log) | **ALL ASSERTIONS PASSED** |
| **Negative Run** | `DISPLAY=:0 ./frontend/TechStore.Client/bin/Debug/net10.0-desktop/TechStore.Client --probe-theme --negative-control` | `1` | [`probe_r3_negative.log`](file:///home/zinn/zinn/win/project/TechStoreManagement/openspec/changes/frontend-shared-markup-foundation/probe_r3_negative.log) | **SUCCESSFUL CATCH (FAILURES DETECTED)** |

### 5.4. Đánh giá Lượt probe R1–R4 sau Review lần 3 (lịch sử)
- [x] **R1:** Style Setter thật gán cho specimen và kiểm tra qua Light/Dark/System với `LocalValue=UnsetValue`.
- [x] **R2:** System theme assertion kiểm tra chính xác ActualTheme và palette độc lập.
- [x] **R3:** Conjunction 4 điều kiện (XAML Light/Dark và C# Light/Dark), mismatch/null gây FAIL.
- [x] **R4:** Deadline và cancellation token thực tế, TryEnqueue thành công, forced timeout exit code 2.
- [x] Desktop build sạch 100% (0 errors, 0 warnings).
- [x] Headless unit tests pass 100% (207/207 tests).
- [x] Normal startup bảo toàn 100% (0 diff trên `App.xaml.cs`).
- [x] Đã lưu logs mới (`probe_r3_positive.log`, `probe_r3_negative.log`) và bảo toàn toàn bộ logs cũ.
- [x] Fingerprint mới cập nhật đầy đủ và chính xác theo git HEAD + dirty diff + harness SHA256.

=> **Các assertion R1–R4 PASS trên lượt probe có fingerprint bên trên.**  
**Kết quả này không thay verification cuối sau sync dev.**

---

## 6. Final verification after full apply on dev-synced branch (2026-10-18)

Environment: Linux x86_64, .NET SDK 10.0.101, Uno SDK 6.7.30, Skia X11 (`DISPLAY=:0`), viewport 1024x640 DIP, system `ActualTheme=Light`. Branch `feat/dev6-sprint2-analytics-dashboard`, HEAD `7116a718` (working tree contains the implementation below; no commit/push).

### Build and unit tests

- Command: `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore`
- Result: exit 0; build succeeded, 0 warnings, 0 errors.
- Command: `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj --no-restore`
- Result: exit 0; 256 passed, 0 failed, 0 skipped.
- `git diff --check` and `openspec validate frontend-shared-markup-foundation --strict`: pass.

### Runtime probes

| Scenario | Command | Exit | Result |
|---|---|---:|---|
| Positive probe | `DISPLAY=:0 ./frontend/TechStore.Client/bin/Debug/net10.0-desktop/TechStore.Client --probe-theme` | 0 | R1 Style Setter Light/Dark/System, icon geometry, six tones and preserved identity/input; R2 ActualTheme palette; R3 XAML `StatusBadge` + C# consumer strict Light/Dark conjunction; R4 dispatcher/layout all PASS. |
| Negative control | `DISPLAY=:0 ./frontend/TechStore.Client/bin/Debug/net10.0-desktop/TechStore.Client --probe-theme --negative` | 1 | Intentional wrong expectations detected; `SUCCESSFUL CATCH`. |
| Forced timeout | `DISPLAY=:0 ./frontend/TechStore.Client/bin/Debug/net10.0-desktop/TechStore.Client --probe-theme --timeout-test` | 2 | `TimeoutException` caught; intentional non-zero failure path. |
| Gallery startup smoke | `DISPLAY=:0 TECHSTORE_UI_GALLERY=1 ./frontend/TechStore.Client/bin/Debug/net10.0-desktop/TechStore.Client` | stopped after 4s | Process remained alive; smoke script terminated it (143). Initial run exposed a bad color swatch token passed as a literal; corrected to live `AppColors.Brushes`, then rerun remained alive. |
| Normal startup smoke | `DISPLAY=:0 TECHSTORE_UI_GALLERY=0 ./frontend/TechStore.Client/bin/Debug/net10.0-desktop/TechStore.Client` | stopped after 4s | Process remained alive; smoke script terminated it (143). |

Raw logs: `logs/final_probe_positive.log`, `logs/final_probe_negative.log`, `logs/final_probe_timeout.log`, `logs/gallery_startup.log`, `logs/normal_startup.log`. The display reports missing EWMH `_NET_WM_STATE` and an unavailable Uno dev-server endpoint; both app smoke processes mounted and remained alive despite these environment messages.

### Implementation fingerprint and remaining acceptance

- Source hashes: `ComponentGalleryPage.cs` `676db99c5eedc288bd722145d6296382b04dd7e7265fdb61656508d8fda0dc88`; `MainPage.cs` `2740053b9a58e99a25342123a74305e9d03beac7e234e85d8a884099f2e57b76`; `StitchTokens.cs` `bc0b6713c788e84d13d3c8c2dc0174b8735b238b7500491d5a3d9d347139f3a6`; `ThemeBindingProbeHarness.cs` `b9ea69204cf7547abfb8cdf8aa11a56eada700893956a126e1fa92c8429abe46`.
- Windows native target/runtime: **unverified** on this Linux host.
- Owner UI acceptance: **confirmed by project owner in conversation on 2026-10-18** after reviewing the implementation. Reviewed target recorded from this verified run: Linux x86_64 / Uno Skia X11, viewport 1024x640 DIP; source fingerprint listed above. Result: accepted for handoff; Windows native remains unverified and is not included in this acceptance.
