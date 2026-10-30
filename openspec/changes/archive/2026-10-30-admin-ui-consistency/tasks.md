# Tasks

## 1. Restore and record the approved UI scope

- [x] 1.1 Restore the previously requested visual adjustments on Products, Customers, Dashboard, Category and Inventory, and Settings; review the diff to confirm Orders changes stay in the heading/filter toolbar and leave its table and modal untouched.
- [x] 1.2 Add the cross-page presentation requirements, design choices, and implementation scope to this OpenSpec change; confirm the files match the implemented changes.

## 2. Verify the desktop client

- [x] 2.1 Build `frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore` and confirm the build finishes with no errors.
- [x] 2.2 Launch the rebuilt desktop client and confirm it starts with the restored UI assets and the Orders list remains outside the dialog/table scope.

## Workflow follow-up

- Archive this change after desktop verification is complete.
