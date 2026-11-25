# Tasks

## 1. Pin the release and prepare the evidence matrix

- [ ] 1.1 Identify the pushed Sprint 3 integrated branch or tag. Record the full remote SHA and confirm the clean checkout matches it. Verify the reporting components and scoped test suites are present. Record the Release build command, SDK and initial API/client build identities in `docs/M4_FINANCIAL_REPORT.md`.
- [x] 1.2 Establish acceptance case IDs, expected results and run IDs in the English Sprint 3 section of the evidence document. Verify that the matrix includes the financial report, M3 Dashboard and seed regression, separate Skia and Windows rows, and the applicable dependency scenarios from D5.

## 2. Rerun automated coverage

- [x] 2.1 Run the scoped backend and authorization tests in Release. Record the exact commands and filters, runner totals, corresponding case results and any fixes or reruns. Verify that API validation and authorization results include the documented HTTP statuses.
- [x] 2.2 Run the report and M3 Dashboard HTTP suites on generated PostgreSQL test databases. Record the version and sanitized `TECHSTORE_TEST_CONNECTION` opt-in, and verify daily/period totals, preserved M3 Dashboard behavior, Cash/QR separation, after-sales facts, replay and rollback. Confirm PostgreSQL overlap evidence for concurrency rows counted as Pass.
- [x] 2.3 Rerun PostgreSQL Analytics and clean-seed regression using their separate disposable targets and opt-ins from D2. Record the actual configuration and results. Verify the October 2026 report and Dashboard fixtures, and compare seed behavior on a clean setup and rerun.
- [x] 2.4 Run frontend headless report and Dashboard coverage and the client-to-API test in Release. Record independent totals, the exact filters, the API opt-in and snapshot identity. Verify the authorized client figures, role scope, range changes, VND formatting, loading, retry and stale responses; record each result against the relevant row.

## 3. Verify desktop targets

- [ ] 3.1 Build and launch the Skia Release target against the isolated release API. Run the D2 desktop cases and record build and runtime results, OS, target, SHA and Expected/Actual. Verify the day and period figures against the captured API response and record any runtime blocker with its rerun condition.
- [ ] 3.2 Build and launch the Windows native Release target and run the same platform-specific cases, including High Contrast where supported. Verify separate build and runtime statuses, log the actual startup error if applicable, and record Windows results and reruns independently of Skia.

## 4. Prepare the report handoff for An

- [ ] 4.1 Document the report demo seed, role and dataset references, range, expected figures and setup and run steps in `docs/M4_FINANCIAL_REPORT.md`. Verify those steps and values against the release demo environment, and label observed Cash, seeded QR, manual, sandbox or webhook modes accurately.
- [ ] 4.2 Write Bình’s video segment script in the same document, including narration, screen actions, timing, roles, case IDs and remaining limitations. Verify the script can be followed using the documented release and demo data, and identify any unavailable footage with its owner and rerun condition.
- [x] 4.3 Reconcile all automated runner counts and acceptance matrix counts independently. Verify each row has its inputs, Expected/Actual, build SHA, status, and rerun result or reason. Check that open dependencies list the owner, rerun trigger and acceptance impact, and that reported rates show their denominators.
- [x] 4.4 Review the final scope and evidence links for An’s package. Confirm the release handoff is in English, the resulting diff stays within Bình’s document and tests, normal runner output or the evidence document contains the recorded proof, and any commit message omits AI `Co-Authored-By` trailers.
