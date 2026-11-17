# Spec Delta

## ADDED Requirements

### Requirement: M3 role and HTTP verification record
The repository SHALL contain `docs/M3_UI_API_VERIFICATION.md` recording, for a named build and OS, the HTTP status checks (200/201/204/400/401/403/404/409 and 500 when testable), the four-role Shell landing/menu/route results, network failure recovery and theme/resize/keyboard results, each marked Pass, Fail or Not checked with the command or evidence used.

#### Scenario: Not checked is not a pass
- **WHEN** a check cannot run in the verification environment (for example Windows native on Linux)
- **THEN** it is recorded as Not checked with the reason and is not counted as passed
