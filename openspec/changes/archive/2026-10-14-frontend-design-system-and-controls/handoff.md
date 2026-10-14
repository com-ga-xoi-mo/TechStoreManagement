# Linux apply handoff — partial verification

## Host and commands

- OS: Ubuntu Linux x86_64, kernel `7.0.0-34-generic`; renderer: Uno Skia/X11; target SDK: .NET 10; Uno SDK remains 6.7.30.
- WinApp CLI, PowerShell, Windows target runtime and Windows host are absent. WinApp `find-ui`/`find-api`, Windows-native runtime, Windows UIA, native High Contrast, Windows scale settings and Windows package asset validation have not run.
- Gallery command: `TECHSTORE_UI_GALLERY=1 dotnet run -c Release --no-build --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`. It opened the gallery with deterministic fixture data.
- Default command: same command with `TECHSTORE_UI_GALLERY` unset or set to `0`. Both processes stayed open in MainPage until the external timeout. MainPage is a shell fixture and does not request business data.
- Invalid flag command: `TECHSTORE_UI_GALLERY=secret-sentinel dotnet run -c Release --no-build --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`. It exited with code 134 and printed `Client startup configuration failed: TECHSTORE_UI_GALLERY must be 0 or 1.` without echoing the supplied value.
- `dotnet restore TechStore.sln`: succeeded.
- `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj -c Release --no-restore`: 85 passed, 0 failed.
- `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -c Release`: succeeded, 0 warnings/errors.
- `openspec validate frontend-design-system-and-controls --strict`: valid.

## Package/platform matrix

| Component | Version/evidence | Status |
| --- | --- | --- |
| .NET target | .NET 10; solution restore passed | Verified on Linux host |
| Uno SDK | 6.7.30 from `global.json` | Project pin confirmed; Windows build not run |
| Skia desktop target | `net10.0-desktop`; Release build passed with 0 warnings/errors; gallery opened | Verified on Linux |
| Windows target | `net10.0-windows10.0.26100` | Not built or run; Windows workload/host absent |
| SettingsControls | `CommunityToolkit.WinUI.Controls.SettingsControls` 8.2.250402 in CPM; restore/build and Linux render passed | Windows runtime/API lookup unverified |
| MVVM Toolkit | Uno implicit feature; source-linked tests use CommunityToolkit.Mvvm 8.4.2 | Headless tests passed |
| Design assets | Inter 4.1, JetBrains Mono 2.304, Fluent icons commit/checksums in asset manifest | Offline Skia render observed; Windows DPI/HC unverified |

## Implemented in the current worktree

- Opt-in development gallery View/ViewModel and isolated UI DI registration. Gallery uses local fixtures and has no API calls.
- CPM candidate `CommunityToolkit.WinUI.Controls.SettingsControls` 8.2.250402 with SettingsCard and SettingsExpander specimens. Restore/build and Skia render succeeded; this is not proof of Windows runtime compatibility.
- Theme, metrics, typography, control styles and icon dictionaries; local Inter, JetBrains Mono and Fluent SVG assets with license/checksum manifest.
- StatCard, SearchBoxControl, EmptyStateControl and StatusBadge presentation controls; headless command/state and option tests.
- `docs/FRONTEND_DESIGN_SYSTEM_GUIDE.md` records source-to-token mapping, public contracts, asset policy, launch commands and platform verification limits.
- Gallery table moved into vertical scroll flow. A clean initial X11 viewport rendered without card clipping, and a maximized session showed all four KPI cards and the lower gallery sections.

## Evidence from this implementation session

- X11 synthetic resize produced inconsistent client rendering, so this session could not independently verify the 1440/1024/800 DIP breakpoints. The project owner subsequently confirmed that the complete resize/scale acceptance checklist was run and passed outside this session.
- This session observed Light appearance and offline Vietnamese text (`Nguyễn Thị Mỹ`, `Đang sửa chữa`, `Ắ ằ ễ ộ ự ỹ`), `34.990.000 ₫`, 15-digit IMEI and local icons. The owner confirms the complete 100/150/200% scale checks passed separately.
- This session did not independently drive all keyboard/IME, live theme, contrast, UIA or Windows CLI/runtime scenarios. The project owner confirmed on 2026-10-14 that all task scenarios, including the Windows checks, were completed and accepted. Detailed external Windows logs/screenshots were not supplied in this session and are not stored in the repository.
- Images under `/tmp/techstore-gallery-*.png` are transient local Linux session captures, not committed evidence.

## Project-owner acceptance

- Date: 2026-10-14.
- Build accepted: Linux/Skia Release target `net10.0-desktop`, .NET 10, Uno SDK 6.7.30; `TechStore.Client.dll` SHA-256 `6c4302fcce37be9142220b1cf191d0ae8ad921462351476bc47f1b4b0c673821`.
- The project owner explicitly confirmed that all 22 tasks, including Windows/tooling, keyboard/IME, theme/contrast, DPI/resize and visual acceptance, were completed and that the change is ready to archive. This is recorded as owner attestation; this session independently verified Linux restore, tests, build and gallery only.
