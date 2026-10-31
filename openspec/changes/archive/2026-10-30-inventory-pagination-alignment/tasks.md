# Tasks

## 1. Align inventory pagination controls

- [x] 1.1 Add an observable page label to Category, Inventory, and Serial/IMEI presentation state; verify it is refreshed when page or page count changes.
- [x] 1.2 Replace icon/number controls with labeled previous and next buttons around the page label; verify existing commands and disabled states remain bound.

## 2. Verify the desktop client

- [x] 2.1 Build `frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` and confirm it finishes without errors.
- [x] 2.2 Review the diff and launch the client; verify the change is limited to inventory pagination and the UI starts.

## Workflow follow-up

- Archive the change after desktop verification is complete.
