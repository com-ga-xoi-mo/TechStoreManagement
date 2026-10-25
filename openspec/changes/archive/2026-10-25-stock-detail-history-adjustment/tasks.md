# Tasks

## 1. Shared contracts and backend API

- [x] 1.1 Add `AdjustStockRequest`, `InventoryMovementDto`, and `InventoryStockDetailDto`; verify the shared project builds.
- [x] 1.2 Add repository detail loading with product/category, variant, serial count, and latest ten movements; verify the backend build succeeds.
- [x] 1.3 Add detail and adjustment service methods with row locking, quantity invariants, audit movement creation, and HTTP mappings; verify backend inventory service tests pass.

## 2. Client transport and ViewModels

- [x] 2.1 Extend `IInventoryApiClient`/`InventoryApiClient` with detail and adjustment calls and normalized 400/401/403/404/409/network failures; verify frontend unit tests pass.
- [x] 2.2 Add detail and adjustment ViewModels with live projection, validation, busy/error state, cancellation, and success events; verify projection and failure tests pass.
- [x] 2.3 Emit typed detail/adjustment row actions from `InventoryStockViewModel`; verify inventory action-hook tests pass.

## 3. C# Markup dialogs and workspace integration

- [x] 3.1 Implement the stock detail/history dialog from the supplied HTML design with metrics, movement table, Serial/IMEI hook, and adjustment action; verify desktop and Windows builds report zero warnings/errors.
- [x] 3.2 Implement the adjustment dialog with increase/decrease controls, live preview, validation feedback, and busy-safe confirmation; verify desktop and Windows builds report zero warnings/errors.
- [x] 3.3 Wire visibility/edit actions through `CategoryManagementPage`, refresh the authoritative inventory list after success, and show the existing toast; verify the end-to-end action path by targeted client tests and build output.

## 4. Verification

- [x] 4.1 Run `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj --no-restore` and verify all frontend tests pass.
- [x] 4.2 Run backend inventory tests and verify adjustment, conflict, movement mapping, and controller compilation; document any unrelated host-environment test failure.
- [x] 4.3 Confirm no `.xaml` file was added and run `git diff --check`.
