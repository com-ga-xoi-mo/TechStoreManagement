# Identity

Controller → Service → Repository → AppDbContext; writes commit through IUnitOfWork.
PasswordHasher<User> retains existing salted PBKDF2 hashes. No password reset occurs during upgrade.

## Upgrade and rollback

`dotnet ef database update --project backend/TechStore.Api.csproj`

AddIdentityAccessManagement adds nonnegative users.token_version (bigint, default 0),
plus unique B-tree expression indexes on lower(btrim(username/email)). Existing raw-column
and refresh-token indexes remain. The migration preflight stops on normalized duplicates
and reports conflicting user IDs; a populated database must have an active sole-role Admin.
Resolve conflicts explicitly before retrying; no accounts, credentials or history are merged/deleted.
Zero/multiple-role employees remain untouched and need an Admin to assign one role before login.
An empty database is supported and seeded afterward.

All JWTs issued before upgrade lack token_version and require a fresh login. Before rolling
back to an older backend that ignores revocation, stop traffic, rotate the signing key through
local secrets/environment, revoke refresh tokens and clear client sessions. Prefer retaining
additive columns/indexes; only run a down migration against a backup-tested compatible version.

## PostgreSQL verification

Set TECHSTORE_TEST_CONNECTION to a local PostgreSQL connection with CREATE DATABASE rights.
`dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter FullyQualifiedName~Identity`
The fixture creates and drops a generated test database, never the application database.
Do not count skipped PostgreSQL tests as passing.

## Token lifecycle and account API

POST api/auth/login and api/auth/refresh return LoginResponseDto. Refresh tokens rotate atomically,
are single-use and expire according to Jwt settings. Reusing a consumed token returns 401 without
revoking the winning replacement. Every protected JWT is checked against active user, sole role
and token_version in PostgreSQL. Missing/malformed identity is 401; insufficient action rights 403.
Logout revokes **all devices/sessions for the caller**. Lock/unlock, effective role changes and
password reset revoke all earlier JWT and refresh tokens. Status/role no-ops preserve sessions.

Admin endpoints: GET api/users?q=&role=&isActive=&page=&pageSize=, GET api/users/{id},
POST api/users (username/fullName/email/password/role), PUT api/users/{id} (fullName/email),
PATCH api/users/{id}/status (isActive), PUT api/users/{id}/role (role),
POST api/users/{id}/reset-password (password; 204). Create returns 201 + Location;
read/update 200; invalid fields 400, duplicate/last-Admin conflict 409, unknown user 404.
Username is immutable; account deletion is not supported. Passwords are 8..128 characters,
never trimmed; role must be one of the four named roles. Safe metadata never includes hashes,
tokens or token_version. See ../../docs/API_PERMISSIONS.md for endpoint/data scope.

Account writes lock the Admin role row, then actor/target users in ascending ID order,
then refresh rows. Actor privileges/version are checked again after waiting. Last active
sole-role Admin cannot be locked or demoted. Login/refresh/logout lock user before tokens;
refresh never locks token before user. Services own transactions; repositories only stage changes.
Rollback clears the tracker even after cancellation. Tests use two contexts/connections and a
barrier immediately before the first lock; sequential calls are not counted as concurrency tests.

## Demo identities

New databases get admin/Admin@123, manager1/Manager@123, cashier1/Cashier@123,
warehouse1/Warehouse@123. Seed reruns preserve edited profiles, credentials, roles, activity
and token_version; they never reactivate or re-add a former role. Existing legacy identities
are matched by stable ID or natural username; ambiguous records fail explicitly. Business
ownership, timestamps and analytics fixtures remain unchanged. Manager dashboard and Sales
customer access are enabled in Sprint 2, not by this identity change.

## Desktop sessions

The client shares one pre-request refresh per current token pair and never replays business
writes. Account Generation survives token renewal; a late 401 checks the sent access token
as well, so it cannot expire the replacement. Login/logout/account replacement changes Generation.
RememberMe controls FileSessionStorage persistence; remembered startup validates /api/auth/me
(or refreshes first) before opening protected data. Network-uncertain refresh requires login.
Local logout always clears memory; disk deletion failures are surfaced, and offline logout
cannot claim server revocation. A failed persistence write cannot publish a remembered login.
The existing local JSON storage has not been replaced with an OS credential vault; if deleting
the file fails, its contents remain on disk until filesystem access is restored.

## Sprint 1 verification

See [the 2026-11-12 verification report](../../docs/verification/identity-access-management-2026-11-12/README.md)
for commands, PostgreSQL/HTTP/runtime evidence and the remaining UI acceptance steps.
Backend/frontend/live API suites pass without skips; full UI account acceptance remains open,
pending the user-operated create/reset password UI steps required by the UI tool handoff policy. Windows native was not run on macOS.
