# Implementation handoff — partial

Updated: 2026-10-13. Change: `frontend-core-and-api-foundation`. Local runtime: Linux x86_64, .NET SDK 10.0.101, Uno SDK 6.7.30, target `net10.0-desktop`. Implementation and handoff are complete. The owner explicitly directed that task 5.1 be accepted for archive despite the backend test failure below; this is recorded as an owner waiver, not as a passing backend test.

## Automated checks

| Check | Result |
| --- | --- |
| `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` | Passed on 2026-10-13: 67 passed, 0 failed, 0 skipped. Headless; no backend/database/UI thread required. |
| `dotnet restore TechStore.sln` | Passed in the implementation run; projects were up to date. |
| `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` | Passed in the implementation run: 0 warnings, 0 errors. |
| Frontend Configuration, Composition, Transport, API error, cancellation, Session, Bearer and Logging tests | Passed as part of the 67/67 frontend suite. |
| `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj` | Re-run on 2026-10-13: 91 passed, 1 failed, 92 total. `DataSeederTests.DataSeeder_Should_Seed_Data_And_Be_Idempotent` fails with PostgreSQL `42P01: relation "roles" does not exist`. This is why task 5.1 remains unchecked. Build also reports an EF Core Relational version conflict between 10.0.4 and 10.0.12. |
| `docker compose ps` | Earlier attempt could not access Docker daemon socket `/var/run/docker.sock` (permission denied); database state could not be verified here. |
| `openspec validate frontend-core-and-api-foundation --strict` | Passed on 2026-10-13: change is valid. |

The backend failure is a database setup/schema issue observed in the backend test, not evidence of a frontend API integration failure. The owner chose to accept/waive this outstanding backend regression check for this archive. Transport behavior is verified with fake HTTP handlers; no claim of real backend integration is made.

## Runtime and owner acceptance

- The implementation run on Linux started the Release desktop app. `xwininfo` reported the `TechStore.Client` window as `Map State: IsViewable`, size 1024x640. The earlier run did not capture interaction, close/restart cleanup, or a valid environment override in the desktop process.
- An earlier invalid configuration run with `TECHSTORE_API_BASE_URL='https://user:password@localhost/api?secret=sentinel'` stopped before window creation (exit 134); its critical log identified the configuration key and validation reason without printing user-info/query. Headless tests cover valid base URL and timeout overrides. Do not reuse the sentinel URI or real credentials.
- The user explicitly confirmed on 2026-10-13, “Mọi thứ đều ổn,” in response to the four remaining acceptance checks. This is recorded as owner acceptance for tasks 5.3–5.6. Exact tested build/commit, OS, target, environment values, Windows command output, and per-check observations were not supplied in the conversation, so this handoff does not present them as independently collected runtime evidence. Windows was not built or launched from this Linux workspace.
- Debug launch previously emitted Uno RemoteControl/HotDesign connection and X11 `_NET_WM_STATE` presenter messages. Release was used for the successful window startup observation.

## Scope and limitations

- Existing `MainPage` was moved under `Views/`; the client now has the `Views/`, `ViewModels/`, `Models/`, `Repositories/`, and `Services/` roots aligned with the MVVM repository architecture. No placeholder repositories or generic CRUD repository were added.
- DI composition root, API options, typed HTTP transport, normalized API errors, in-memory session, bearer handler, frontend headless tests, and the foundation guide are implemented. Backend/shared source was not changed for this frontend foundation.
- Session tokens are memory-only. Backend login/refresh protocol, secure token storage, session-expired UI/logout, and cancellation of in-flight requests on account change are outside this change.
- DTO-specific semantic validation belongs in feature repositories; the transport validates HTTP and JSON/body expectations.
- Task 5.1 is the remaining failed acceptance check: backend suite needs a database schema with the `roles` relation (or corrected isolated test setup), then the complete suite must be rerun. The Windows target was not independently verified in this Linux environment; owner confirmation is recorded above, but the precise Windows build/run evidence was not provided.

## Entry points and next changes

- Startup/composition: `frontend/TechStore.Client/App.xaml.cs`, `frontend/TechStore.Client/ServiceCollectionExtensions.cs`.
- API options, client transport and failures: `frontend/TechStore.Client/Services/Api/`.
- Session and bearer pipeline: `frontend/TechStore.Client/Services/Session/` and `frontend/TechStore.Client/Services/Api/BearerTokenHandler.cs`.
- Moved page: `frontend/TechStore.Client/Views/MainPage.xaml` and `.xaml.cs`.
- Headless regression coverage: `tests/Frontend.UnitTests/`.
- Architecture/setup details: `docs/FRONTEND_FOUNDATION_GUIDE.md`; planned sequencing: `docs/FRONTEND_FOUNDATION_PLAN.md`.
- Change 2 should add feature DTO/repository/API use cases against the established `IApiClient` and shared contracts, with semantic validation at repository boundaries.
- Change 3 should add session-aware sign-in/navigation and UI feedback only after the backend auth contract is agreed; do not infer a refresh-token contract.
- Change 4 should implement feature pages and visual components following the `UI/` sample and the required MVVM/repository architecture.

## Checklist status

Tasks 1.1–5.7 are checked. Task 5.1 is accepted by owner waiver on 2026-10-13 while the recorded backend result remains 91/92; do not describe the backend suite as passing. Task 5.7 records completion of evidence reconciliation and handoff. Archive is performed at the owner's explicit direction with this limitation retained.
