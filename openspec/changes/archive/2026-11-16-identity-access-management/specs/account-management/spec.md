# Spec Delta

## Purpose

Cho phép Admin quản lý vòng đời tài khoản nhân viên qua giao diện và API, giữ lịch sử người thao tác và luôn duy trì ít nhất một Admin đang hoạt động.

## ADDED Requirements

### Requirement: Search and inspect accounts
Admin SHALL list accounts through `GET /api/users` with combined optional `q` (case-insensitive username, full name or email substring), `role`, `isActive`, `page` and `pageSize`; default page is 1 and pageSize 20, normalized to page >= 1 and pageSize 1..100. Results SHALL use the existing paged envelope, order by username then ID and include ID, username, fullName, email, role, isActive, createdAt and updatedAt; role SHALL be null for a legacy account with zero or multiple roles so Admin can repair it without guessing a role. `GET /api/users/{id}` SHALL return the same metadata or 404. Invalid role/filter values SHALL return 400. Passwords, hashes, tokens and internal token versions SHALL NOT appear in these responses.

#### Scenario: Combined account filters
- **WHEN** Admin searches q=ngan, role=SalesStaff and isActive=true
- **THEN** only active SalesStaff matching the text appear and pagination totals use the same filters

#### Scenario: Unknown account
- **WHEN** Admin fetches an unknown ID
- **THEN** the server returns 404

### Requirement: Create and edit account profiles
Admin SHALL create an account through `POST /api/users` with username, fullName, email, password and one role, returning 201, Location and safe account metadata. New accounts SHALL be active. `PUT /api/users/{id}` SHALL update only fullName and email, return 200 metadata, and keep username, password, role, activity and history unchanged. Username SHALL be trimmed, 3..64 ASCII letters/digits/dot/underscore/hyphen; fullName SHALL be trimmed and 1..200 characters; email SHALL be valid, trimmed and at most 254 characters; new passwords SHALL be 8..128 characters and SHALL NOT be trimmed. Roles SHALL be one of the four named roles, with numeric/undefined values rejected. Username and email SHALL be unique ignoring surrounding whitespace and case. Validation SHALL return 400 errors keyed by field, duplicates including concurrent writes SHALL return 409, and unknown update IDs SHALL return 404. Historical identities SHALL NOT be deleted.

#### Scenario: Create an employee
- **WHEN** Admin supplies a valid WarehouseStaff account
- **THEN** the response is 201 with the new safe metadata and the employee can log in

#### Scenario: Invalid input
- **WHEN** username is blank, email invalid, password too short and role unknown
- **THEN** one 400 response reports every invalid field and creates nothing

#### Scenario: Concurrent duplicate account
- **WHEN** two requests create usernames differing only in case or surrounding whitespace
- **THEN** exactly one is created and the other receives 409 without partial role assignment

### Requirement: Control account access and credentials
Admin SHALL set activity via `PATCH /api/users/{id}/status` with required boolean isActive, set exactly one role via `PUT /api/users/{id}/role` with a named role, and reset password via `POST /api/users/{id}/reset-password` with a new valid password. Status and role changes SHALL return 200 safe metadata; password reset SHALL return 204 without disclosing a generated or stored credential. Unknown IDs SHALL return 404 and invalid inputs 400. Effective lock/unlock or role changes and each password reset SHALL invalidate all prior access and refresh tokens for that account atomically with the change. Repeating the current status/role SHALL succeed without revoking sessions or changing updatedAt.

#### Scenario: Role change revokes old privileges
- **WHEN** Admin changes an active Manager to SalesStaff and the write commits
- **THEN** every request authenticated afterward with the former tokens returns 401, and a new login has only SalesStaff rights

#### Scenario: Unlock does not restore revoked tokens
- **WHEN** Admin locks and then unlocks an account
- **THEN** the old tokens remain invalid and a fresh login is required

#### Scenario: Password reset
- **WHEN** Admin resets another user's password
- **THEN** the prior password and all prior tokens fail and the newly supplied password can be used to log in

### Requirement: At least one active Admin is preserved
The server SHALL reject with 409 any lock or role change that would leave no active Admin whose sole assigned role is Admin. This invariant SHALL hold for self-modification and for simultaneous administrative requests; a rejected change SHALL leave profile, role, tokens and account status unchanged.

#### Scenario: Last Admin cannot lock or demote themselves
- **WHEN** the only active Admin requests isActive=false or a non-Admin role for themselves
- **THEN** the response is 409 and the Admin remains active with valid unchanged credentials and tokens

#### Scenario: Two Admins changed concurrently
- **WHEN** two independent requests attempt to remove the active Admin status of the last two Admins concurrently
- **THEN** at most one succeeds, the other is rejected and at least one active Admin remains

### Requirement: Account management is usable from desktop Settings
An authenticated Admin SHALL open an account management screen from Settings without adding a new top-level Shell destination. The screen SHALL provide search, role/status filters, pagination, create/edit, lock/unlock, role assignment and password reset through the REST API. Non-Admins SHALL have no entry or usable direct account-management route and the API SHALL independently reject them. Destructive access changes SHALL ask for confirmation. The screen SHALL show loading/empty/error/content, field errors, 409 conflicts and retry without discarding entered non-secret values on errors. Password fields SHALL be masked, cleared after success or close and never persisted. Vietnamese labels, keyboard access, responsive layout and live Light/Dark/System themes SHALL work.

#### Scenario: Admin manages an account end to end
- **WHEN** Admin opens Settings account management, creates an employee, edits the profile, assigns a role, locks/unlocks and resets the password
- **THEN** each accepted action is reflected by API data and the screen shows the resulting state

#### Scenario: A forbidden direct route
- **WHEN** SalesStaff or WarehouseStaff attempts the account route directly
- **THEN** no account data loads and a safe access-denied result is shown

#### Scenario: Failed form submission
- **WHEN** the API returns field errors or a duplicate/last-Admin conflict
- **THEN** the form reports the relevant error, keeps non-secret inputs and allows correction without claiming success
