# Proposal

## Why

UI-01 opens directly into a preview Shell, so the desktop client has no login screen or sign-out path. UI-02 adds the D11 login experience and a replaceable client auth boundary that can be reviewed with a mock repository while the backend auth contract is absent.

## What Changes

- Add a native Uno login page based on D11's two-region layout, using D31 theme resources. Provide username/password entry, password visibility, validation, submitting and error states, keyboard/focus behavior, and a usable narrow-window layout. D10 is a secondary reference; D11's demo state-switcher, example credentials, and invented server status do not appear in the app.
- Add ViewModel → `IAuthRepository` → mock implementation through DI for this change. The mock supplies deterministic success, wrong-credentials, locked/inactive, network and session-expiry cases so the UI can be tested; it does not authenticate against the backend or enforce RBAC. HTTP `AuthRepository`/ApiClient belong to a later integration change after the server contract exists.
- Gate startup on the login screen in the configured UI/demo mode, enter the existing Shell as an explicitly labelled preview after mock success, and provide sign-out that clears local preview session and returns to login. “Remember login” stays unavailable and never stores a password or token in this change.
- **BREAKING (UI-01 startup):** the normal application startup no longer opens the preview Shell directly. Preview access stays an explicit development/demo mode and remains visibly unverified.

## Re-application acceptance

The existing UI-02 implementation is not yet accepted as UI/preview complete. The password-entry, submit-feedback and auth-mode fixes are present in source; the owner confirmed the first three login interactions after correction on 2026-10-10. Re-apply this change to preserve those fixes and finish the unverified criteria, especially full auth-mode, sign-out/Back, simulated 401/403 and narrow-window behavior. The ordered repair and verification path is recorded in [design.md](design.md#re-apply-repair-sequence-and-evidence). Checked tasks must reflect executed evidence rather than the presence of files or partial confirmation. Owner acceptance remains a separate final gate for the complete UI-02 checklist. Real Auth API/JWT integration remains a separate dependency.

## Capabilities

### New Capabilities

- `client-auth-session`: Desktop sign-in form, mock-driven preview session lifecycle, sign-out, and authentication feedback without real token persistence.

### Modified Capabilities

- `client-navigation-shell`: Gate Shell entry and reset navigation when a preview session ends; keep the footer explicitly marked as preview.

## Impact

Planning covers `frontend/TechStore.Client/Models/Identity`, `Views/Identity`, `ViewModels/Identity`, mock `Repositories/Identity`, `Services/Session`, existing Shell/navigation and `App.xaml.cs` DI/bootstrap. UI-00 and archived UI-01 are dependencies and are reused. The current backend has Identity entities/configurations but no implemented auth controller, service, request/response contract, refresh/revoke endpoints, or capability response. Real Auth API, JWT/refresh token storage and server RBAC are explicitly outside UI-02 and need a later change; no business screen, Settings screen or password-reset flow is added.
