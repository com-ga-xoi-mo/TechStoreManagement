# Design

## Context

See [proposal.md](proposal.md) and the two delta specs. UI-00 resources and UI-01 Shell/navigation are implemented and archived. UI-02 code has been partially applied: a D11 LoginPage, `FakeAuthRepository`, session provider and Shell sign-out exist, but no frontend Auth ApiClient or token storage does. An earlier UI-02 audit found that DI supplied the fake in `Real` mode and mock success called `SetAuthenticated`. The current source instead registers an unavailable repository in normal mode, gates the fake behind explicit development preview, and uses `SessionStatus.Preview` for mock success. These corrections still need the remaining task-level verification; they do not constitute real authentication. Backend Identity currently contains entities/configuration/README, but `Program.cs` has no authentication middleware or auth controller/service registration; `shared/DTOs` and `shared/Requests` have no auth contract. Those README descriptions and D11's server-ready/sample-account text are plans/examples, not implemented endpoints.

`docs/MVVM_REPOSITORY_BASE_PROJECT.md` §6 and the user's explicit layout choice control the client tree: sibling `Models/`, `Views/`, `ViewModels/`, `Repositories/`, `Services/`; interface beside implementation. Reuse `Styles/TechStoreTheme.xaml`, the existing Shell and its navigation/session seams. Do not switch to the older `Features/` layout shown in `AGENTS.md`. D11 is the main visual source; D10 is secondary. The HTML's 1440×900 fixed layout and blue `#0066FF` are illustrative; responsive native XAML and D31/UI-00 tokens take precedence.

## Re-application audit

At the initial audit, `ExplicitPreview` entered Shell on startup, mock success did not take the Preview path into Shell, 401/403 had no reachable UI harness, and root navigation could leave Shell in Back history. Current source has a login-first startup gate, preview transition, root navigation service and executable frontend tests. Do not assume these source changes close every task: simulated 401/403 still need a reachable runtime check, logout/Back and both auth modes need full desktop verification, and the owner acceptance gate covers more than the three login interactions confirmed below. Preserve the useful fixes on re-apply; inspect current code before changing it.

Owner manual test on 2026-10-10 initially failed: password could not be entered, pressing Đăng nhập gave no apparent response, and no validation error appeared. The defects were the `TextBox`-targeted style applied to `PasswordBox`, an unreliable two-way password binding without a `PasswordChanged` bridge, and validation returning without visible field errors. The source now has a `PasswordBox`-targeted style, a minimal View-to-ViewModel password bridge, field-level Vietnamese errors and focus requests. After these corrections, the owner replied “đã được rồi” to a follow-up covering password entry, empty-form feedback and mock login with the documented preview credentials on 2026-10-10. This is evidence for those three interactions only; it does not close the full 4.2 acceptance gate.

### Re-apply repair sequence and evidence

1. Inspect the current source and preserve any correction already present. In `Styles/TechStoreTheme.xaml`, `TechStorePasswordBoxStyle` must target `PasswordBox`; `LoginPage.xaml` must apply that style to the password input. Do not reintroduce a `TextBox` style on `PasswordBox` or a two-way `PasswordBox.Password` binding that does not work on Uno desktop.
2. In `LoginPage.xaml.cs`, keep code-behind limited to the XAML boundary: forward `PasswordChanged` into `LoginViewModel.Password`, synchronize ViewModel-initiated clearing back to the control, and honor focus requests. Verify typing, masking/reveal and clearing on a running Uno desktop window. Never log, prefill or persist the password.
3. In `LoginViewModel.cs` and `LoginPage.xaml`, make empty/whitespace submission set visible Vietnamese errors beside the affected fields, focus the first invalid field and skip the repository call. A valid submission must reach the command with the typed password, show loading and resolve to success or a visible error. Keep single-flight and cancellation behavior covered by executable tests; a visible click alone does not prove these paths.
4. In `Services/Session/SessionConfig.cs` and `App.xaml.cs`, keep `TECHSTORE_AUTH_MODE=preview` as an explicit Debug development setting. Default normal mode uses `UnavailableAuthRepository`; preview mode alone registers `FakeAuthRepository`. Both modes start on an empty LoginPage. Mock success calls `SetPreview` and enters Shell with a visible preview label; it never calls `SetAuthenticated` or silently falls back from an API failure.
5. Keep the root navigation service and Shell sign-out wired through DI. On sign-out or simulated expiry, clear both root and nested history so Back cannot reopen Shell. Add/reach a development-only 401/403 trigger and verify the visible difference between expiry and forbidden feedback before checking those tasks.
6. Run `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` and `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj`. For the manual preview path, run `TECHSTORE_AUTH_MODE=preview dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --launch-profile 'TechStore.Client (Desktop)'`, then test the three repaired interactions and the remaining task scenarios. The target is `net10.0-desktop`, not `net10.0-desktopop`. Uno RemoteControl/HotDesign/X11 development-environment messages alone do not prove a login failure if the window opens and the interaction works. Record actual behavior and separate unrelated solution/backend test failures.
7. Update `UI_02_HANDOFF.md` with commands, results, owner feedback and unresolved checks. Mark a task `[x]` only when its entire criterion is verified; keep 4.2 open until the owner confirms the complete normal and narrow-window checklist. The two delta specs remain the implementation contract for re-apply. Sync them into the main specs only after UI-02 is fully implemented and accepted, then archive the change.

## Goals / Non-Goals

**Goals:** Keep auth state independent of XAML; make preview sessions impossible to confuse with verified identity; complete the login UI and Shell handoff through an explicit mock repository. Record the missing real API contract as a later change dependency.

**Non-Goals:** Implementing an HTTP auth client, JWT issuance/storage/refresh, backend authentication, revoke endpoints, account lock policy, role/capability mapping, registration, forgot-password, Settings, or destination business pages. A local mock never establishes real identity or permission.

## Decisions

### 1. D11 page and MVVM boundary

Add `Views/Identity/LoginPage.xaml` and `ViewModels/Identity/LoginViewModel.cs`. The View uses native Uno controls, two-region composition and scroll/reflow at a narrow usable width; reuse D31 styles rather than copying D11 HTML/CSS or embedding its image. Keep branding copy factual and remove the demo state switcher, prefilled `admin.techstore`/password, fabricated version and green server-ready dot. Bind form and status to `[ObservableProperty]`/`[RelayCommand]`. Since `PasswordBox` handling is a XAML boundary, allow minimal code-behind solely to pass password changes, focus requests, and page lifecycle into the ViewModel; no repository/HTTP/business decision in code-behind. Clear the password on failed auth, navigation away, and sign-out, including the control buffer. Prefer a password-specific state abstraction if feasible over an indefinitely retained bindable string.

Give `PasswordBox` a compatible style targeting `PasswordBox` (or use its native style with matching theme setters); never assign a `TextBox` style to it. Verify typed characters reach the command input, stay masked unless reveal is requested, and are cleared from both control and ViewModel when required. Bind or bridge `ObservableValidator` field errors to visible Vietnamese text beside the corresponding input, with the first invalid input focused. A submit with empty fields must visibly respond without calling the repository. Keep the primary button reachable and distinguish an intentionally unavailable normal mode with an explanatory message rather than an unexplained disabled control.

**Alternative:** one Page per visual state or HTML/WebView would duplicate the form and break the native MVVM boundary.

### 2. Typed auth result and source separation

Add a small UI-independent `Models/Identity` result/session model. `LoginViewModel` consumes `IAuthRepository` and maps typed mock outcomes to form messages; it never sees HTTP status, JSON, `ContentDialog`, or `HttpClient`. Put `IAuthRepository` and `FakeAuthRepository` together in `Repositories/Identity`. The mock supplies deterministic invalid-credentials, locked/inactive, network-unavailable and unknown-error outcomes for UI testing. It is not an API adapter and does not assert the server will use those exact statuses. Do not create shared DTOs, HTTP repository, ApiClient or assumed route in UI-02. Cancellation propagates through the mock boundary, and the command guards double submit. A later Auth API change can add HTTP implementation beside the interface after a confirmed contract.

**Alternative:** constructing `HttpClient` in ViewModel or fabricating a backend response schema from D11 would couple UI to a nonexistent API.

### 3. Explicit preview mode and startup gate

Register the fake only in an explicit development preview mode through `App.xaml.cs` or a small `ServiceCollectionExtensions`; normal mode without configured auth shows login with a clear unavailable state and does not grant Shell access. No silent fallback from a future real/network error to mock. The fake requires no displayed sample credentials or hidden state-switcher. Fake success produces `SessionStatus.Preview`, not `Authenticated`; the Shell remains clearly marked as demo. Root content starts at `LoginPage` without restoring a preview session. After accepted mock login it replaces root content with the existing `ShellPage`, and sign-out replaces it with login and clears nested navigation history. Prevent root Back into a prior Shell.

For the unconfigured normal mode, use an explicit unavailable auth source or disabled submit state that cannot return a successful session. Preview must be enabled through a documented development-only setting, but enabling it must not auto-create a session. Route accepted mock login to Shell explicitly for `Preview`; reserve `SetAuthenticated` for a future verified response from the real API. Reset root and nested navigation history after replacing content, then verify Back at runtime rather than relying on a pre-navigation `BackStack.Clear()` call.

**Alternative:** keep direct Shell startup or mark fake success as authenticated. Both would present preview access as real identity.

### 4. Session, capabilities and token lifetime

Evolve `ICurrentSession` into a change-notifying session abstraction while preserving UI-01 route filtering semantics. Store no password or token. “Ghi nhớ đăng nhập” remains disabled or explained as unavailable in UI-02; no preview session is restored after restart. Unknown capabilities outside preview expose no protected routes. Shell footer updates on state changes; a visible sign-out command appears only for an active preview session, while Settings stays unavailable. Logout clears local state and never claims remote revocation. A later integration change must choose protected token storage and a refresh/revoke contract before enabling persistent sessions.

**Alternative:** plaintext settings, cached role/name, or a locally invented role-to-route map cannot establish an authenticated session.

### 5. Protected request errors

UI-02 only defines a UI-independent expiry/error coordinator and drives it with mock 401/403 outcomes. An unrecoverable simulated 401 clears preview session once, returns to login and surfaces an expiry reason; simulated 403 is a permission error and does not sign out. There is no bearer handler, JWT parsing, refresh request or live protected API call in this change. A later integration change must decide the real request/refresh policy from backend contracts. Backend remains the only authority for authentication and RBAC.

Register the coordinator in DI and provide a reachable development-only trigger or equivalent runtime harness for both simulated outcomes. Keep state transitions independent of XAML so a real test runner can exercise them; the UI adapter displays 401 expiry and 403 permission feedback. Do not claim a live HTTP 401/403 occurred.

**Alternative:** each ViewModel handling 401 independently would produce inconsistent logout and duplicate refresh attempts.

## Risks / Trade-offs

- Missing auth endpoints, status/error codes, token and capability shape → Keep HTTP/JWT work out of UI-02; document it as a separate future Auth API change dependency and never claim API completion from mock tests.
- No verified protected token store for Linux/Windows → Leave persistent remember unavailable; do not place refresh tokens in plaintext files or app settings.
- D11 fixed desktop art may clip at narrow widths → Reflow/scroll native content; owner manual test after agent build/runtime checks at normal and narrower usable windows.
- Password input, visibility and focus can behave differently on Uno Skia/Windows → Use a compatible `PasswordBox` style and verify real typing, command input, show/hide, clearing, Tab/Enter and visible field errors in the running app; record unavailable platforms as unverified.
- UI-01 snapshot/footer is currently immutable → Update the session seam and Shell subscription together; detach listeners on unload and reset navigation on sign-out.
- Fake credential flows can be mistaken for real security → Explicit preview configuration and footer label; never use fake in production mode or as an API-failure fallback.

## Migration Plan

1. Add typed auth/session contracts and deterministic headless checks; wire an explicit preview fake without changing backend.
2. Build D11 login page, root auth gate, Shell sign-out/session updates, and UI-only feedback states. Preserve access to UI-00 preview via its development path.
3. Put focused assertions in an executable test project/runner; execute them and record the command/result. Build and run Uno desktop after the final code change. First reproduce and resolve the owner's failed password-entry, submit and validation-feedback checks; then check both auth modes, keyboard/focus, narrow layout, mock error states, preview Shell, logout and Back. If a platform is unavailable, report it as unverified.
4. Ask the owner to manual-test the corrected UI in normal and narrower usable windows. Record dated confirmation and failures separately from agent checks. Only then describe UI-02 as “UI/preview complete; real API integration pending.” Record the required login/refresh/revoke/error/capability contract and protected token storage for a separate change, without leaving HTTP/JWT tasks in this UI change.

Rollback is a DI/startup configuration change back to the explicit UI-01 preview Shell for development; it must never silently convert a failed real login into preview access. No database migration is part of this change.
