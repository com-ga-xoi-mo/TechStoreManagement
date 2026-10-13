# UI-08 Category Tree Handoff

**Status:** Implementation, automated checks, runtime route smoke, and owner visual/manual acceptance passed.

## Scope delivered

- The Shell route `Danh mục & Kho` opens the native `InventoryPage`.
- The page has three keyboard-accessible tabs. `Danh mục` loads a complete preview category tree lazily; `Tồn kho` and `Serial / IMEI` show explicit placeholders and do not request their data.
- The category view shows name, parent/root context, description, product count, read-only active status, name search, status filter, loading, empty, error/retry, and parent disclosure states.
- No category create/edit/delete or status mutation controls are included. Preview product counts are synthetic and labelled as preview data.
- Product create/edit continue using the existing flat category options.

## Verification on 2026-10-10

- `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj --no-restore`: **158 passed, 0 failed**.
- `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore`: **passed, 0 warnings, 0 errors**.
- Started the Uno desktop app in explicit preview mode; the process remained running until stopped. Startup emitted the existing Uno RemoteControl/X11 window-manager diagnostics (`RemoteControlClient` endpoint unavailable and `_NET_WM_STATE` missing). These did not terminate startup. The Linux environment cannot build the Windows WinAppSDK target.
- ViewModel tests cover lazy category loading, preview data, errors/retry, cancellation, filtering, ancestor context, expand/collapse, category counts, and placeholder tab behavior. The XAML desktop target compiles and the preview app launches.

## Owner acceptance — pending

On 2026-10-10, the owner manually tested the Inventory route and acceptance checklist on the running Uno preview and confirmed: **“ổn cả”**. No unmet behavior was reported. No screenshot or exact window measurement was required.

Launch the preview app:

```bash
TECHSTORE_AUTH_MODE=preview dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
```

Sign in with the existing preview account (`admin` / `Admin@123`), then select **Danh mục & Kho**. To test tree states, close the app and relaunch with one scenario:

```bash
TECHSTORE_AUTH_MODE=preview TECHSTORE_CATEGORY_TREE_PREVIEW=Empty dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
TECHSTORE_AUTH_MODE=preview TECHSTORE_CATEGORY_TREE_PREVIEW=Error dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
TECHSTORE_AUTH_MODE=preview TECHSTORE_CATEGORY_TREE_PREVIEW=Delayed dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
```

The owner confirmed the three tabs and selected state, category tree and filters, disclosure controls, count/description/status display, loading/empty/error/retry, placeholders, keyboard interactions, and a narrower still-usable window. All were reported as okay.

## Dependencies for later integration

There is no category-tree API contract or category query endpoint in the backend at this time. Before a real integration, define the response shape, authorization, product-count unit and parent aggregation semantics, visibility rules, and hierarchy-preserving paging. The mock data is not authoritative inventory or permission data.
