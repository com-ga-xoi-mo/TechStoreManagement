# Spec Delta

## MODIFIED Requirements

### Requirement: Current bearer token
The client SHALL use the current access token for each request, send exactly one Bearer header when a session exists and none otherwise, and never send it to a foreign origin. Successful login SHALL retain the token pair and current profile in memory. RememberMe=true SHALL persist the existing session record for restart; RememberMe=false SHALL clear any prior persisted session and retain the new session only in memory. Logout or terminal authentication failure SHALL clear both in-memory and persisted credentials.

#### Scenario: Session replacement or clear
- **WHEN** token A is replaced by token B or the session is cleared before the next request
- **THEN** the request uses only token B or has no Authorization header

#### Scenario: Application restart
- **WHEN** the client restarts with a remembered session
- **THEN** it validates or refreshes that session before using protected data and expired/revoked credentials do not produce a successful login state

#### Scenario: Non-remembered restart
- **WHEN** login succeeded with RememberMe=false and the process restarts
- **THEN** the prior user's session is not restored

### Requirement: Authentication failure respects session identity
A 401 from a request using the current session SHALL expire only that matching session and clear its credentials; a 401 from an older session SHALL NOT affect a newer session. A 403 SHALL preserve the session and report access denied. The client SHALL NOT automatically log in, refresh in reaction to a 401, replay a failed business request or automatically replay a refresh with an uncertain network outcome. Pre-request refresh is governed by the bounded refresh requirement.

#### Scenario: Current session unauthorized
- **WHEN** a request using the current session receives 401
- **THEN** the caller receives unauthorized and only the matching session is expired and notified

#### Scenario: Late unauthorized response
- **WHEN** a token-A request receives 401 after the session has been replaced by token B
- **THEN** the error is returned and session B remains active

#### Scenario: Forbidden or anonymous unauthorized
- **WHEN** a request receives 403 or an anonymous request receives 401
- **THEN** the correct error is returned without changing a newer session or performing login/refresh

## ADDED Requirements

### Requirement: Refresh is bounded and respects the current session
Before sending a business request with an expired or soon-expiring access token, the client SHALL perform at most one shared refresh operation for that session generation. Concurrent callers SHALL await that operation and use its replacement pair only if the originating session is still current. No business write SHALL be sent before refresh succeeds or replayed afterward. Refresh transport SHALL use only the configured origin, avoid redirects and bypass recursive bearer-refresh handling. A definite refresh rejection SHALL clear only the matching session; an uncertain network failure SHALL be shown as a session-recovery error requiring login without automatically reusing the old refresh token. Cancellation by one waiting caller SHALL NOT cancel refresh needed by other callers. Token renewal SHALL preserve the RememberMe choice.

#### Scenario: Several simultaneous requests need refresh
- **WHEN** multiple requests share a soon-expiring session
- **THEN** one refresh is sent, the pair is replaced once and each uncancelled business request is sent at most once

#### Scenario: Session replaced while refresh is in flight
- **WHEN** user A's refresh completes after logout or user B logs in
- **THEN** A's result cannot restore A's session, overwrite B's tokens or save A's credentials to disk

#### Scenario: Refresh fails before a write
- **WHEN** refresh is rejected or has an uncertain network outcome before a product/order/account write
- **THEN** the write is not sent and the client reports the authentication or recovery error
