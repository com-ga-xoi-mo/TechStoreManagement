# Spec Delta

## ADDED Requirements

### Requirement: Late responses stay in their session
A business response that arrives after the client session generation changed (logout, expiry or another account signing in) SHALL be discarded by the transport and reported to the caller as a session-changed failure; its body SHALL NOT reach any ViewModel. Login and refresh requests are exempt.

#### Scenario: Logout while a request is in flight
- **WHEN** a stock list request is sent, the user logs out and the response arrives afterwards
- **THEN** the caller receives the session-changed failure and no stock rows are returned

#### Scenario: Another account signs in before the response
- **WHEN** Admin's request is in flight, Admin logs out and `cashier1` signs in before the response arrives
- **THEN** Admin's data is not shown in `cashier1`'s session

#### Scenario: Same session
- **WHEN** the session generation is unchanged when the response arrives (including a silent token refresh)
- **THEN** the response is returned as before
