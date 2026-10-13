# UI-10 Stock Detail Handoff

Date: 2026-10-11

## Delivered

- Added a read-only native `ContentDialog` for the exact product/optional-variant balance selected from UI-09.
- Shows physical, reserved, and available quantities separately; recent movements are displayed only when supplied by the repository, with at most five rows and preview labeling.
- Added loading, not-found, empty-history, unavailable/error, retry, refresh, last-refreshed, and cancellation behavior.
- Added a row detail action and a Serial/IMEI navigation handoff that selects the existing UI-12 placeholder and retains product/variant context. The placeholder continues to state that records/filtering are not implemented.
- No adjustment form/action, stock mutation, fabricated serial records, HTTP endpoint, backend, or database changes were added.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj --no-restore` | Passed, 204 tests |
| `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore` | Passed, 0 warnings and 0 errors |

The desktop client was launched with `TECHSTORE_AUTH_MODE=preview`. The window process started; Uno emitted its existing remote dev-server/X11 window-manager diagnostics. This launch-only check did not automate dialog interaction, keyboard navigation, or resizing.

## Dependencies and limitations

- **Movement API contract is absent.** A real integration still needs endpoint/DTO semantics for product and optional variant identity, newest-first ordering, signed quantity changes, physical quantity after each movement, timestamps, notes, retention/paging, permissions, and refresh metadata. The current fixture is synthetic and marked Preview.
- **UI-11 is not implemented.** `IInventoryChangeNotifier` is only a frontend seam; UI-11 must publish after a confirmed successful server mutation. This change does not locally adjust quantities.
- **UI-12 is not implemented.** Navigation passes and retains product/variant identity, but the destination remains a placeholder and does not load Serial/IMEI records or apply a filter.
- Owner visual acceptance is complete. The owner confirmed normal and narrower usable-window behavior, keyboard operation, refresh/retry, movement readability, and Serial/IMEI navigation.

## Owner manual acceptance

- Status: Accepted by owner.
- Date: 2026-10-11
- Result / unmet behavior: Owner reported all checks were okay; no unmet behavior reported.
