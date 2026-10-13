# UI-11 Stock Adjustment Handoff

Date: 2026-10-11

## Delivered

- Added a D02-style adjustment dialog from each stock row. It carries the selected product/optional-variant balance and displays physical, reserved, available, and estimated physical counts.
- Regular stock supports increase/decrease, positive whole-number quantity, and a required reason. The row is never edited optimistically; a successful repository result updates the fake preview fixture and notifies UI-10 to reload.
- Conflict and generic failure preserve the draft. Conflict offers an explicit balance reload and does not auto-submit again.
- Serial-tracked and unknown tracking states fail closed: no quantity editor or submit action is shown. The D05 explanation routes to the existing Serial/IMEI placeholder with the selected context.
- Preview mutation exists only in `FakeInventoryRepository`. Non-preview uses `UnavailableInventoryRepository`; no HTTP client, API endpoint, backend, or database behavior was added.

## Automated checks

| Command | Result |
|---|---|
| `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj --no-restore` | Passed, 217 tests |
| `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore` | Passed, 0 warnings and 0 errors |
| Windows target build | Not run; this handoff environment is Linux and does not support the Windows App SDK target. |

## Preview run and scenarios

Use a Debug desktop build and set `TECHSTORE_AUTH_MODE=preview`. Sign in with username `admin` and password `Admin@123`, choose the `Danh mục & Kho` route, then the `Tồn kho` tab. Select `Điều chỉnh` on a regular laptop/accessory row to access the form; choose a phone/tablet row to see the serial block.

The repository scenario is selected before app startup:

```bash
TECHSTORE_AUTH_MODE=preview TECHSTORE_STOCK_ADJUSTMENT_PREVIEW=Success dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
TECHSTORE_AUTH_MODE=preview TECHSTORE_STOCK_ADJUSTMENT_PREVIEW=Conflict dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
TECHSTORE_AUTH_MODE=preview TECHSTORE_STOCK_ADJUSTMENT_PREVIEW=Error dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
TECHSTORE_AUTH_MODE=preview TECHSTORE_STOCK_ADJUSTMENT_PREVIEW=Delayed dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
```

Scenarios: `Success` updates only the fake fixture and refreshes the stock list; `Conflict` keeps the input and enables “Tải lại số tồn”; `Error` keeps the draft without a success notification; `Delayed` exposes the pending state. The fake repository rejects serial-tracked adjustment. Automated unit tests cover these paths and exact product/variant identity.

The Preview desktop process was started with `TECHSTORE_AUTH_MODE=preview TECHSTORE_STOCK_ADJUSTMENT_PREVIEW=Success` and opened for owner testing. Startup emitted the existing Uno RemoteControl/dev-server and X11 window-manager warnings seen in earlier phases. A separate 15-second launch-only check confirmed the process stayed running until its timeout. The owner then reported that all UI-11 checks were okay, including the requested normal/narrow window and interaction acceptance. No unmet behavior was reported; no screenshot or pixel-scale comparison was required.

## Real integration dependencies

- A real adjustment endpoint and versioned request/response/error contract for product ID, optional variant ID, direction, positive quantity, reason, authoritative balance, and any movement result.
- A reliable `IsSerialTracked` source on the balance response. Missing or unknown classification must continue to fail closed.
- Server-side role/permission enforcement. The preview UI and fake repository make no authorization decision.
- Server-owned transaction, row locking/concurrency semantics, reserved-stock invariants, non-negative quantity validation, and append-only inventory movement ledger.
- A defined conflict/error envelope, authoritative refresh behavior, and request identity/idempotency policy before automatic or user-initiated retry semantics are integrated.
- UI-12 must implement Serial/IMEI records and filtering before the current navigation context can open a functional serial workflow.

## Owner manual acceptance

- Status: Accepted by owner.
- Date: 2026-10-11
- Result / unmet behavior: Owner reported all checks were okay; no unmet behavior reported.
