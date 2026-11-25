# Proposal

## Why

Sprint 3 acceptance needs financial report results tied to the integrated release that reviewers can reproduce. An needs Bình’s verified results, demo inputs, remaining limitations, and video segment outline for the team submission.

## What Changes

- Rerun financial report coverage and related M3 Dashboard and seed regressions on a pushed integrated release SHA. Record the run commands, environment, target, and test opt-ins for backend, PostgreSQL/HTTP, frontend headless, Skia, and Windows native.
- Complete the English release handoff in `docs/M4_FINANCIAL_REPORT.md`. Include each case’s ID, inputs, Expected/Actual, build SHA, result and rerun outcome. Provide separate totals for automated runners and the acceptance case matrix.
- Provide report demo data and run instructions, a script for the report video segment, and an owner, rerun condition and acceptance impact for each outstanding dependency.

## Capabilities

### New Capabilities

### Modified Capabilities

## Impact

- The change covers Bình’s evidence document and report tests. `skip_specs: true` applies to this verification and handoff work.
- An assembles the team README, video, and QA report. Team-wide evidence collection requires an explicit assignment recorded in the design.
