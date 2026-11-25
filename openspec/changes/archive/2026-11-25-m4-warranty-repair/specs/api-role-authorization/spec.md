# Spec Delta

## MODIFIED Requirements

### Requirement: Warranty case authorization
The `/api/warranties` endpoints SHALL allow: `GET lookup`, `GET` (list) and `GET {caseId}` for A M S W; `POST {caseId}/receive` and `POST {caseId}/return` for A M S; `POST {caseId}/inspect` and `POST {caseId}/repaired` for A W; `POST {caseId}/replace` and `POST {caseId}/reject-replacement` for A M. Other roles SHALL receive 403 and a missing or invalid token 401. The actor SHALL come from the token.

#### Scenario: Sales and Warehouse cannot authorize warranty operations
- **WHEN** SalesStaff calls inspect, repaired, replace or reject-replacement, or WarehouseStaff calls receive, return, replace or reject-replacement
- **THEN** the API returns 403 and nothing is changed

#### Scenario: Warehouse cannot receive a case
- **WHEN** WarehouseStaff posts `/api/warranties/{caseId}/receive`
- **THEN** the API returns 403 and no case is created

#### Scenario: Sales cannot approve a replacement
- **WHEN** SalesStaff posts `/api/warranties/{caseId}/replace`
- **THEN** the API returns 403 and the case stays `PendingReplacement`

#### Scenario: Manager cannot inspect
- **WHEN** Manager posts `/api/warranties/{caseId}/inspect`
- **THEN** the API returns 403

#### Scenario: Sales receives and hands back
- **WHEN** SalesStaff receives an eligible device and later returns the repaired device
- **THEN** both calls succeed

#### Scenario: Anonymous
- **WHEN** a request without a token calls `/api/warranties/lookup`
- **THEN** the API returns 401

## ADDED Requirements

### Requirement: Warranty replacement cost visibility
`replacementUnitCost` in warranty case responses SHALL be returned to Admin, Manager and WarehouseStaff, matching the device `unitCost` rule of `GET /api/inventory/serials`. SalesStaff SHALL receive `null`.

#### Scenario: Sales reads cases without cost
- **WHEN** SalesStaff reads a `Replaced` case
- **THEN** the response is 200 with `replacementUnitCost = null`

#### Scenario: Warehouse reads replacement cost
- **WHEN** WarehouseStaff reads a `Replaced` case whose replacement device has a unit cost
- **THEN** the response is 200 with that `replacementUnitCost`
