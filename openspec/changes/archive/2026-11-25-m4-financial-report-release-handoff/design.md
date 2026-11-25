# Design

## Context

- The [M4 Sprint 3 plan](../../../docs/MILESTONE_4_PLAN.md#sprint-3-nghiệm-thu-và-nộp-bài) assigns individual evidence, demo inputs, instructions, limitations and video segments to their owners. An assembles the submission and checks the clean installation. The [quality reporting requirements](../../../docs/REQUIREMENTS.md#6-báo-cáo-chất-lượng-trong-báo-cáo-cuối-kỳ) require execution totals, pass and fail rates, and a record of defects and fixes.
- [Financial report evidence](../../../docs/M4_FINANCIAL_REPORT.md) contains Sprint 1 and 2 results from several builds, including Debug runs and working-tree changes. It identifies open QR, voucher, AI and desktop checks. Those records supply case inputs and prior results for the release rerun.

## Goals / Non-Goals

**Goals** — Provide a reproducible English release handoff for the financial report, with an auditable case matrix, run totals, demo instructions, and a video script suitable for An’s team package.

**Non-Goals** — Production behavior, schema and seed changes, owner workflow fixes, and the combined team README, QA report, video and submission package. If An explicitly assigns team evidence collection, record the assignment, affected owners, deliverables and approval in this design before starting that work.

## Decisions

### D1. Identify the pushed integrated release before testing

- Use the candidate branch or tag designated for Sprint 3 integration. Record the remote ref, full Git commit SHA observed on the remote, local `git rev-parse HEAD`, and build configuration `Release`. Use the corresponding clean checkout for the claimed release evidence. Record separate API and client source/binary identities if they ship from different repositories or builds, and tie each run to the build it actually exercised.
- Check the release contains the report API, after-sales reporting, desktop changes and the tests being reported. Worktree-only tests or a checkout that differs from the remote can provide diagnostic evidence. If the integrated ref or a required suite is unavailable, record the specific blocker and leave the affected release case **Not verified**. When the candidate changes, capture the new SHA and rerun affected cases before counting them for the new candidate.
- Record exact commands, filters, exit codes, dates, OS/build, architecture, .NET SDK, target framework, PostgreSQL version where applicable, build SHA and sanitized opt-in state. Keep credentials, tokens and connection-string values out of the handoff and recordings.

### D2. Keep runner results distinct from desktop runtime checks

| Group | Release execution | Evidence and prerequisites |
| --- | --- | --- |
| Backend | `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj -c Release` with a filter for `AnalyticsControllerTests`, `AnalyticsServiceTests`, `AnalyticsQueryTranslationTests`, `ApiAuthorizationCoverageTests` and `DataSeederTests`. | Record the exact filter and selected results for controller validation, aggregation, access coverage and seed rules. |
| PostgreSQL/HTTP | Run `TechStore.UnitTests` in Release filtered to `FinancialReportPostgresHttpTests`, `FinancialReportLiveReconciliationPostgresHttpTests` and `DashboardLiveFlowPostgresHttpTests`. Run `dotnet test tests/Backend.IntegrationTests/TechStore.Backend.IntegrationTests.csproj -c Release` filtered to `AnalyticsPostgresIntegrationTests` and `DataSeederPostgresIntegrationTests`. | `TECHSTORE_TEST_CONNECTION` supplies the local server for generated disposable databases. Repository integration uses `TECHSTORE_ANALYTICS_TEST_POSTGRES` and `TECHSTORE_ANALYTICS_ALLOW_DATABASE_RESET=YES` on a dedicated test database because the runner drops and migrates it. Seed integration uses `TECHSTORE_SEED_TEST_POSTGRES` and `TECHSTORE_SEED_TEST_ALLOW_SCHEMA=YES` with its own schema in a dedicated test database. Use separate disposable targets for reset and seed runs. Capture overlap evidence when a concurrent case is counted as Pass. |
| Frontend headless | `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj -c Release`, filtered to the financial report, Dashboard, Analytics repository and preview options tests. | Record exact filters and results for formatting, date ranges, role scope, retry, empty and unavailable data, and late responses after a session or period change. |
| Client-to-API integration | `dotnet test tests/Frontend.IntegrationTests/TechStore.Client.IntegrationTests.csproj -c Release --filter FullyQualifiedName~FinancialReportApiIntegrationTests`. | Set `TECHSTORE_API_BASE_URL` to the isolated release API with demo seed. Compare the report client and its captured HTTP response on the same snapshot. Record this extra run without adding it to desktop runtime coverage. |
| Skia desktop | Build and launch `frontend/TechStore.Client/TechStore.Client.csproj` in Release with `-f net10.0-desktop`. | With `TECHSTORE_DASHBOARD_DEMO=0` and `TECHSTORE_API_BASE_URL` set, compare an authorized report with the live API and verify restricted roles, inputs, empty/unknown/error states, retry, logout or account change during loading, keyboard and focus, and Vietnamese/VND. Check 800×600, 1024×768 and 1440×1000 DIP, text scaling and Light/Dark/System with the report open. |
| Windows native | Build and launch the same project in Release with `-f net10.0-windows10.0.26100`. | Repeat the runtime matrix on Windows and check High Contrast where supported. Record OS and the launch profile actually used. Record the build and launch as separate observations when a build succeeds and startup fails. |

### D3. Use stable case IDs and explicit denominators

- Add an English Sprint 3 release section to `docs/M4_FINANCIAL_REPORT.md` and retain earlier evidence as history. Reuse the applicable `FR-01` through `FR-12` IDs and assign stable IDs for M3 seed and Dashboard regression, Sprint 2, desktop platform and dependency cases without IDs. Give a case that must pass on both desktop targets a separate row per target. Include case ID, feature, input (role, range, fixture or request), Expected, Actual, SHA and environment or run ID, status, issue/owner and rerun result. Link each row to its observed test method, command or manual steps.
- Use **Pass**, **Fail**, **Skipped**, and **Not verified** (the English label for “Chưa kiểm tra”). Pass requires observed matching evidence from the release. Fail means an executed check disagreed with Expected. Skipped preserves an explicit runner or planned skip and the reason. Not verified records a case without usable evidence, including an unavailable runtime or dependency. Retain the first failed observation and add the fix or decision, affected SKU or scenario where relevant, rerun SHA/command and outcome. A previous run can be cited with its original identity as context.
- Automated runner totals use each invocation’s reported denominator, `Pass + Fail + Skipped`. State `executed = Pass + Fail`, list missing or failed runner invocations separately, and identify reruns with their own run IDs. Report rates with the denominator shown. Acceptance matrix totals count distinct planned case rows on the selected release, `Pass + Fail + Skipped + Not verified`. Show both `Pass / planned` and the executed-only pass and fail rates using `Pass + Fail`. Use `N/A` for a zero denominator. Keep prior attempts as traceable evidence and count each case once using its latest applicable result on that release. Automated runner test instances and acceptance rows have separate denominators.

### D4. Provide reproducible demo inputs and a recording script

- Use the clean, migrated demo seed to check M3 Dashboard and the October 2026 fixture. Document a small reproducible dataset with ranges, timezone, roles, checkout and shift references, expected figures and setup commands. The existing October 15–21 window covers sales, Cash and seeded confirmed QR receipts. On the inspected seed, `M4-202610-0001` and `M4-CABLE-PACK` cover October 25 sale, October 26 refund and October 27 restock. The seed includes a repair handback. Verify these records and actual expected values on the selected release before featuring them. Keep production seed code and historical fixtures intact.
- For a real-flow walkthrough, reuse the Sprint 2 controlled case when available. Two VND 1,000,000 items at unit cost VND 600,000, with VND 200,000 discount and 10% VAT, yield a VND 1,980,000 receipt and two VND 990,000 payouts. The first valid restock contributes VND 600,000 cost reversal and the defective unit retains its cost. Identify the payment mode in each example, including seeded confirmation, manual bank reconciliation, sandbox and webhook where observed. If using a different demo workflow, provide the actual numbers and dates for its own recording.
- Put the video segment script in the same handoff document. Include a short narration and screen actions in order. Show the release identity and Manager opening the live Financial Report tab, selecting the documented UTC+07 range, and comparing one day and the full period with the expected sale, refund, restock, gross profit and Cash/QR values. Show one restricted-role check using the allowed presentation and HTTP evidence. Conclude with the payment mode and any unverified cases relevant to the segment. Give An the approximate timing, role switches, and the case IDs to cite in the team video.

### D5. Report dependencies with their acceptance impact

| Dependency from prior evidence | Owner and rerun condition | Acceptance impact if still open |
| --- | --- | --- |
| New QR receipts, late payments and webhook | Đạt supplies the integrated confirmation flow and provider test proof. An supplies bank payout proof where applicable. Verify each payment path on the release and label the mode. | Seeded receipts and manual refund proof cover their own rows. End-to-end provider and webhook rows stay open. |
| Voucher consumption, restoration and final-slot race | Vy and Đạt supply the integrated workflow and a voucher with one redemption remaining. Rerun with evidence of transaction overlap. | Voucher rows remain unverified independently of Cash and loyalty results. |
| Warranty replacement or missing source data | Q. Vinh supplies a committed replacement fixture and cost. An supplies the linked inventory or return record. Vy confirms historical money and tax allocations as needed. | Missing source data is visible in affected amount rows. A repair handback supports its own case. |
| Skia and Windows native runtime | Bình reruns with an isolated desktop profile and the release endpoint. If native startup still fails, retain the executable/profile, build result and actual startup error before rerunning the runtime matrix on a working host. | Each unverified platform has its own open rows. Headless client coverage remains separately attributable. |
| Report-backed assistant | T. Vinh supplies the integrated tool and four-role test setup. Compare Admin/Manager tool results with the captured API report and verify SalesStaff/WarehouseStaff are denied financial values. | AI evidence stays separate from backend report and desktop acceptance. |

Summarize remaining findings and confirmed fixes for An by case ID. Each finding names its owner, concrete rerun trigger, and whether it blocks a required report case or limits particular demo footage or optional integration evidence.

## Risks / Trade-offs

- A release ref can move between runs. Record the remote observation and build identity per run and tie every conclusion to the SHA actually tested.
- Earlier UI runs used different builds, and one Windows native run exited before login. Record fresh results by target and keep unavailable runtime cases visible in the denominator.
- Opt-in PostgreSQL tests have different isolation mechanisms. Use the fixture’s expected database type and opt-in values, and keep actual secrets out of proof extracts.

## Migration Plan

Run the scoped checks after release integration, append the release handoff to `docs/M4_FINANCIAL_REPORT.md`, and give An the relevant section links and script. No data migration is required. Use only the existing evidence document and Bình’s report tests. Keep output in the normal runner or this document, with no temporary helper files, logs or standalone handoff artifacts. Any commit omits AI `Co-Authored-By` trailers.
