# Design

## Context

`feat/m3-manager-dashboard-access` has merged `origin/dev` and includes the three archived changes: `m3-manager-dashboard-access`, `m3-demo-data-reconciliation`, and `m3-dashboard-live-flow`. The current `origin/dev` ref in the workspace does not yet include Q. Vinh's receiving API. Re-verify after fetching during apply and record the actual executed build in the report.

[The M3 plan](../../../../docs/MILESTONE_3_PLAN.md) currently lists Manager Dashboard on line 84 under Binh, Admin regression and commit/rollback on lines 154–155, demo data on lines 165–166, and consolidation on line 171. Identify items by section heading, Binh's name, and checkbox text if line numbers shift after merges.

## Goals / Non-Goals

**Goals**

- Separate results executed by Binh in Sprint 5 from results cited from prior builds. Every conclusion carries clear evidence and scope so An can proceed with submission assembly.
- Independently execute regression and post-transaction verification while owners finalize receiving or desktop integrations.

**Non-Goals**

- Changing application behavior, specs, production configuration, or another owner's tests/helpers. Keep other owners' wording and checkboxes in the plan untouched, including M3 completion criteria.
- Altering business logic or relaxing test assertions when defects surface. Binh records the reproduction case, observed result, and responsible owner.

## Decisions

### D1. Baseline and build

During apply, inspect branch state and local changes, fetch and merge latest `origin/dev`, and rebuild. Verify the three archives remain on the branch and check whether receiving has landed in the build. For runs executed by Binh, record date, merged source SHA, .NET SDK, OS, build configuration/target, commands, output paths, and SHA-256 hashes of executed artifacts if uncommitted test additions are present. Owner-supplied results retain the SHA and environment from their source. Where a source lacks an original SHA or log, document the missing fields explicitly and only cite evidence within its verified scope.

Binh pushes only to his own branch; merging into `dev` is performed by the maintainer via PR. Therefore the handed-off build is the SHA of `feat/m3-manager-dashboard-access` **pushed to `origin`**, never a local-only SHA. The report explicitly states which `origin/dev` SHA is included and that it has not yet merged into `dev`. On any additional commit (including report or test edits), push again and update the SHA in the report before handoff. After the PR merges, if `dev` introduces further changes beyond the recorded SHA, rerun the main suites on `dev` as a separate run rather than overwriting earlier results.

Run the existing backend unit, backend integration, and frontend headless projects to gather results on the handoff build. Enable opt-in PostgreSQL suites via `TECHSTORE_TEST_CONNECTION`, `TECHSTORE_SEED_TEST_POSTGRES`, and `TECHSTORE_ANALYTICS_TEST_POSTGRES` with their required fixture flags. Point `TECHSTORE_API_BASE_URL` for `Frontend.IntegrationTests` to Kestrel using a dedicated test DB when running live Catalog checks. Record each run separately whenever filters or environments differ. Native UI belongs to T. Vinh's handoff; a successful build alone does not verify UI interaction. Store credential-scrubbed logs/TRX in build/test output folders and extract handoff evidence directly into Binh's report. Since build/test directories are git-ignored, local logs stay on the runner machine; self-containment requirements for the report are specified in D5.

### D2. Reusing evidence and selecting regression contracts

Link sources directly into checkboxes that already have evidence. Manager Dashboard links [Manager Dashboard handoff](../../../../docs/M3_TEST_SUMMARY.md#dashboard-manager) and [four-period API reconciliation](../../../../docs/M3_TEST_SUMMARY.md#actual-qua-http-ở-mốc-cố-định). Demo data and seed idempotency link [fixture table and PostgreSQL tests](../../../../docs/M3_TEST_SUMMARY.md#dữ-liệu-demo-và-đối-soát-kpi), covering Local image HTTP checks, counts, unit costs, four accounts, password/data preservation, and fixture dates. Record SHA and scope limits per source.

For Admin regression, map every required test case against existing tests and assertions, then rerun matching tests on the handoff build. The following sources determine what can be reused and what still needs verification:

| Scope | Tests, contracts, and items to verify |
| --- | --- |
| Products | `CatalogManagementApiIntegrationTests` covers create, update, status toggle, and delete lifecycles; `CatalogApiIntegrationTests` and `ProductSearchPostgresTests` cover read, search, filter, and paging. Use `product-catalog` to verify empty/over-200 names, invalid sort/specs, negative price 400, valid price 0 with complete Catalog payload, and read-back returning 0. Verify failed writes neither create nor mutate entities. |
| Categories | Use `CategoryPostgresTests` for updates, deletes, and tree integrity. Verify create, tree read, update, and delete of empty categories on isolated data if existing evidence has gaps. Check blank/over-200 names and unchanged tree after failed requests. `GET /api/categories/tree` returns the full tree. Use `CategoryWorkspaceTests.Search_DeepMatch_OpensAncestorsAndRestoresExpansion` for client tree search and label it as headless evidence. |
| Customers | Start from `CustomerServiceTests`, `CustomerRepositoryPostgresTests`, `CustomerHistoryPostgresHttpTests`, and Vy's evidence. Verify create, read, update, deactivate/activate, search by name/phone, tier/status filters, and paging as Admin. Treat deactivate as the contract's soft-disable operation. Check empty/over-limit request names and invalid phone or tier inputs. Prefer filling missing management/validation HTTP assertions over PostgreSQL with real Admin auth. |

Add a single dedicated test class owned by Binh, expected to be `M3AcceptancePostgresHttpTests.cs` in `tests/Backend.UnitTests/`, where required cases lack coverage. Reuse `PostgresFact` and `IdentityPostgresApi`. Write via real API calls using distinguishable test data and disposable fixture DBs. Confirm read-backs via GET and use a fresh context when checking fields omitted from DTOs. Label each case clearly as automated or manual, specifying TestServer/PostgreSQL, HTTP against Kestrel, or UI. Check off the regression line once all required cases have valid evidence.

### D3. Post-transaction success, 409, and 400 cases

Start from [m3-dashboard-live-flow evidence](../../../../docs/M3_TEST_SUMMARY.md#kiểm-chứng-http-trên-postgresql) and rerun `DashboardLiveFlowPostgresHttpTests` on the handoff build. Use the 201 case to verify stock, reservations, `Sold` serial, `Sale` movement, warranty, shift totals, and Dashboard. Use the `Sold` IMEI case with a fresh key, `Open` shift, and sufficient cash to verify 409 and unchanged snapshot.

The insufficient-cash case currently in `PosCheckoutServiceTests` uses fakes. Supplement HTTP/PostgreSQL evidence in Binh's class from D2 if the build lacks an equivalent 400 HTTP test. With an `InStock` IMEI, an `Open` shift, and otherwise valid payload, send `cashTendered` below order total, verify 400 keyed to `cashTendered`, confirm the checkout key is unstored, and verify stock/reservations, serial/warranty, ledger, order/lines, and shift totals/status are unchanged via a fresh context. Record 400 as input validation rejection and 409 `Sold` as a sequential conflict check. Check off the commit/rollback line once both success and failure paths within this scope have verified evidence.

### D4. Group handoff sources

| Group | Owner / source |
| --- | --- |
| Backend unit, PostgreSQL/HTTP, frontend headless | Binh records fresh runs by project/command and environment. Cite historical owner results per build noted in source documents. |
| RBAC and Identity | An. [Identity acceptance](../../../../docs/IDENTITY_ACCESS_MANAGEMENT_ACCEPTANCE.md) and [Identity integration handoff](../../../../docs/M3_IDENTITY_INTEGRATION_HANDOFF.md). Explicitly attribute token, role, actor, and permission matrix evidence to their source. |
| POS Cash, shifts, pricing, and customers | Dat/Vy. Archived [m3-pos-cash-shift](../2026-11-17-m3-pos-cash-shift/tasks.md), [m3-order-pricing](../2026-11-17-m3-order-pricing/tasks.md), and [m3-sales-customers](../2026-11-18-m3-sales-customers/tasks.md). Distinguish customer/draft order APIs from POS desktop customer selection, and VAT/line-discount pricing from order-level discounts through POS. |
| UI, HTTP methods, and recovery | T. Vinh. [M3 UI/API verification](../../../../docs/M3_UI_API_VERIFICATION.md). Preserve platform, scope, UI verification steps, and observed HTTP status codes. Windows native awaits T. Vinh's acceptance run. |
| Dashboard, seed, live flow | Binh. The three baseline archives plus D2–D3 evidence, including UTC+07, demo data, HTTP cases, and Skia desktop. |
| Concurrency and receiving | Q. Vinh, Dat. Review tests for competing IMEI/accessory stock, duplicate intake, concurrent status transitions on the same order, and checkout racing shift close per the plan. Current POS tests gate two HTTP requests. Only mark concurrency Pass when that run's own evidence proves two independent PostgreSQL transactions genuinely overlapped (e.g., synchronized at transaction/lock acquisition or lock-wait logs). Sequential 409s or standalone client-side gates report only the behavior actually proven. |

### D5. Report template and counting rules

`docs/M3_TEST_SUMMARY.md` contains a case catalog with stable ID, group, owner, automated/manual type, inputs, Expected, Actual, SHA, command or evidence link, status, and defect/rerun section. Unrun cases state the missing step, owner, and rerun condition under Actual. The defect table records case ID, defect found, owner, fix evidence or SHA if resolved, and rerun result or pending status. Do not double-count a fixed bug and its rerun as two separate cases in the final denominator.

The report must be self-contained: a reader without Binh's machine must be able to audit the results. For every run, record directly inside `docs/M3_TEST_SUMMARY.md` the full command, runner summary line (Passed/Failed/Skipped/Total), names of any failed or skipped cases, and credential-scrubbed log or read-back excerpts (removing JWTs, passwords, connection strings) per acceptance case. Local log paths are secondary references only, never the sole evidence. Cross-reference other owners' evidence via links to repository docs or archives.

Record two kinds of denominators. For each suite run, the total is the case count reported by the runner in that invocation (Pass, Fail, Skipped). For the acceptance matrix, the total is the number of unique case IDs within the matrix scope, using exactly four statuses: Pass / Fail / Skipped / Not tested ("Chưa kiểm tra"). Each ID carries one status for handoff. Record historical runs and overlapping filtered runs separately, including PostgreSQL/HTTP tests inside the backend unit project. Do not sum suite counts with matrix case counts, and do not sum suites run on different SHAs. Cases skipped by the runner are `Skipped`. Items lacking a build, environment, concurrency proof, or executed evidence are marked Not tested ("Chưa kiểm tra").

The pending-items list for An outlines remaining steps. Q. Vinh delivers merged receiving with migrations to run intake -> sell the exact received IMEI -> Dashboard, then reruns the 409 case on that build. Dat wires Vy's order-level discount formula and customer picker into POS desktop to reconcile amounts, shift totals, Dashboard, and customer linkage. T. Vinh verifies UI on Windows native with build, OS, actions, and screenshots/logs. If owner evidence is still missing by the submission deadline, leave those rows as **Not tested** ("Chưa kiểm tra"), record owner and rerun criteria, finalize the report, check off Binh's consolidation checkbox, and archive following the live-flow precedent. Owner acceptance checkboxes and overall completion criteria remain for each owner to confirm.

## Risks / Trade-offs

- [Different handoff build SHAs] -> Keep the source SHA per result, record reruns separately on the merged build, and verify old tests genuinely cover the consolidated cases.
- [Test data or environment interference] -> Use isolated test DBs/schemas, opt-in fixtures, and report skipped or unconfigured cases accurately.
- [Handoff deadline arrives before some integrations] -> Freeze at the D5 cut-off, retaining evidence links and supplementary run conditions in the same report.

## Migration Plan

Apply the merged build's migrations when initializing isolated test databases. No new migrations. Clean up temporary run resources per existing fixtures.
