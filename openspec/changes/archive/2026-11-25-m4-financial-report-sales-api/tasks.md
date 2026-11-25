# Tasks

## 1. Establish the implementation baseline

- [x] 1.1 Confirm the `feat/m4-financial-reports` branch, run `git fetch origin dev`, and merge with `git merge origin/dev` before implementing. Verify with `git merge-base --is-ancestor origin/dev HEAD` and record the SHAs for `origin/dev` and the post-merge HEAD in `docs/M4_FINANCIAL_REPORT.md`.

## 2. API range, contract, and authorization

- [x] 2.1 Add the Analytics action and query validation from D1, and the DTO in `shared/DTOs/FinancialReportDto.cs` from D5. Add the report method to `IAnalyticsService` and `IAnalyticsRepository` and update Bình’s test doubles listed in D6. Test HTTP requests for 1-day and 31-day ranges, duplicate, empty or missing parameters, unsupported presets, invalid dates, unrepresentable UTC boundaries, reversed ranges, 32-day ranges, and future end dates. Verify that HTTP 400 `ValidationProblemDetails` identifies the affected field and that the API skips aggregation.
- [x] 2.2 Add the route and A/M access as one new row in `docs/API_PERMISSIONS.md` and in the independent `ApiAuthorizationCoverageTests` baseline. Run the authorization coverage tests and make HTTP requests against PostgreSQL with real JWTs for all four roles. Verify 200 and identical scope for Admin/Manager, 403 for SalesStaff/WarehouseStaff, and 401 for a missing or invalid JWT. Record test case IDs and expected and actual results in the verification document.

## 3. Sales, receipts, and performance

- [x] 3.1 Read sales by `CompletedAt` and receipts by `PaidAt` from separate Analytics datasets. Group each by UTC+07 date in the same snapshot. Test PostgreSQL events just before and exactly at the start, just before and exactly at the end, the `asOf` cutoff for the current day, payments and deliveries on separate days, `Processing` orders, and both refund statuses retaining original sales.
- [x] 3.2 Map persisted sales amounts, tax, costs, gross profit, and the allocation mismatch count. Compare PostgreSQL results against the D7 expected values and a separate known-zero fixture. Verify that daily values match period totals, empty days have zero-value rows, valid zero allocations and mismatched zero allocations are distinguished, missing costs propagate `null`, and all seven change 2 fields appear as JSON `null`.
- [x] 3.3 Add two top-10 rankings by `ProductId` and cashier performance by `CashierUserId`. Test PostgreSQL with multiple lines and variants, cashiers with identical names, ID tie-breaks, both ranking orders, the row limit, and empty lists. Verify that each order and its receipts count once, product names come from the finalized snapshot, and missing costs affect only the relevant groups.

## 4. Verification and handoff

- [x] 4.1 Build the code under test, run HTTP tests against a dedicated PostgreSQL test database, and rerun the M3 Analytics and Dashboard tests. Compare the output with the DTO and sample JSON in `design.md` and independently calculated expected values. Record test case IDs, inputs and date ranges, expected and actual results, Pass/Fail/Skipped statuses, build SHA, worktree state at build time, and data limitations in `docs/M4_FINANCIAL_REPORT.md` as the sole verification document.
- [x] 4.2 Review `git diff` to verify that implementation changes are limited to Analytics, `shared/DTOs/FinancialReportDto.cs`, Bình’s Analytics report tests and test doubles, `ApiAuthorizationCoverageTests`, the new API permissions row, and the verification document. Check for temporary scripts, logs, and Markdown files. If committing, verify the message contains no AI `Co-Authored-By` trailer.
