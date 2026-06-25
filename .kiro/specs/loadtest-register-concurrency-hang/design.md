# Load-Test Register Concurrency Hang Bugfix Design

## Overview

Under concurrent load the `POST /auth/register` endpoint hangs server-side and fails to
respond within the load-test client's 10-second timeout. Single requests work fine; the
failure only appears under concurrency. The visible Kestrel error
(`BadHttpRequestException: Unexpected end of request content`) is a downstream symptom of the
client aborting timed-out connections mid-body, not the root cause.

The fix approach is diagnosis-first. We will write a reproduction (exploration) test that
drives many concurrent register requests and asserts each completes successfully within the
timeout. This test is expected to FAIL on the current code, confirming the hang. We will then
measure the running server (thread-pool queue depth, CPU, Mongo latency) to confirm which of
the hypothesized root causes is real, apply a minimal targeted fix, and re-verify with the
exploration test and a full NBomber re-run.

The register path touches only MongoDB (not Neo4j):
`AuthController.Register` -> `AuthService.RegisterAsync` ->
`AuthRepository.UserExistsByUserNameOrEmail` (Mongo find) ->
`IPasswordHasher.HashPassword` (ASP.NET Identity PBKDF2, CPU-bound) ->
`AuthRepository.RegisterUserAsync` (Mongo insert) -> `TokenService.GenerateToken`.

## Glossary

- **Bug_Condition (C)**: Concurrent `POST /auth/register` requests arriving at the
  configured load-test rate that fail to complete within the 10s client timeout.
- **Property (P)**: Each well-formed concurrent register request returns `201 Created` with a
  valid JWT token within the client timeout, and no "Unexpected end of request content" is
  logged.
- **Preservation**: Single-request register/login behavior, duplicate-user rejection, and all
  non-register endpoints must remain unchanged.
- **RegisterAsync**: `AuthService.RegisterAsync` in
  `Features/Auth/Domain/Services/Implementations/AuthService.cs` — orchestrates the register
  flow.
- **HashPassword**: `IPasswordHasher<AuthUserEntity>.HashPassword`, ASP.NET Core Identity's
  PBKDF2 implementation — a synchronous, CPU-bound call invoked inside the async request path.
- **Thread-pool starvation**: A condition where CPU-bound work occupies all .NET thread-pool
  threads, leaving no threads to run async I/O continuations (such as Kestrel's body reads),
  causing requests to hang until the pool slowly grows.
- **Server selection timeout**: MongoDB driver's default ~30s window to select a reachable
  server before throwing; blocked selection can exceed the 10s client timeout.

## Bug Details

### Bug Condition

The bug manifests when register requests arrive concurrently at the load-test rate. Under
this concurrency the request pipeline cannot complete each register within the 10s client
timeout. The leading suspect is CPU-bound PBKDF2 password hashing (`HashPassword`) executed
synchronously on thread-pool threads inside every register request; under a burst of
concurrent registers this can starve the thread pool, stalling Kestrel's async body-read
continuations so requests hang. Alternative suspects are MongoDB server-selection/connectivity
blocking in the docker-compose `heterogeneous` setup, or pipeline concurrency/connection
limits.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type RegisterLoad   // set of concurrent register requests + arrival rate
  OUTPUT: boolean

  RETURN input.endpoint = "POST /auth/register"
         AND input.concurrentArrivalRate >= mediumLoadRate
         AND NOT allRequestsCompletedWithin(input, 10_seconds)
END FUNCTION
```

### Examples

- A single `POST /auth/register` from Scalar with a fresh username/email returns `201` with a
  token in well under a second. (Bug does NOT trigger — not concurrent.)
- NBomber `medium`/`all`: `01_register` records `Ok.Request.Count = 0` and `operation
  timeout` at 100% (counts 1200 / 41). (Bug triggers.)
- Backend logs during the run repeatedly show `BadHttpRequestException: Unexpected end of
  request content` from `Http1ContentLengthMessageBody.ReadAsyncInternal` →
  `SystemTextJsonInputFormatter.ReadRequestBodyAsync`. (Symptom of the hang + client abort.)
- A burst of N concurrent register requests (N large enough to saturate the thread pool)
  causes response latency to climb past 10s and requests to be cancelled. (Bug triggers.)

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- A single `POST /auth/register` request continues to return `201 Created` with a valid JWT
  token.
- Duplicate username/email continues to be rejected via
  `UserNameOrEmailAlreadyRegisteredException`.
- `POST /auth/login` continues to verify the password hash and return a valid token.
- All non-register endpoints (profile, categories, prayers, friendships, retrospective)
  behave exactly as before.
- Issued JWT tokens remain valid and verifiable (same claims, issuer, audience, signing key).

**Scope:**
All inputs that do NOT involve concurrent register load should be completely unaffected by
this fix. This includes:
- Single/low-volume register requests.
- Login requests.
- Any read or write traffic to other features.

## Hypothesized Root Cause

Based on the evidence (single requests OK, concurrent register hangs, fully async register
path with no sync-over-async elsewhere, correct singleton DB clients), the most likely causes
in priority order are:

1. **Thread-pool starvation from PBKDF2 password hashing (leading hypothesis)**:
   `IPasswordHasher.HashPassword` is a synchronous, CPU-intensive call run on a thread-pool
   thread for every register. Under a concurrent burst, many hashing operations occupy all
   available pool threads simultaneously. .NET's thread pool grows only gradually (roughly one
   thread per ~500ms once starved), so Kestrel's async I/O continuations — including request
   body reads — are queued behind the CPU-bound work and do not run in time. Requests stall
   past the 10s client timeout, the client aborts mid-body, and Kestrel logs "Unexpected end
   of request content."
   - Consistent with: works single-threaded, fails only under concurrency; the symptom is a
     body-read continuation that never completes in time.

2. **MongoDB server-selection / connectivity blocking**: In the docker-compose
   `heterogeneous` setup, if the Mongo connection string / network causes slow server
   selection, `UserExistsByUserNameOrEmail` (find) or `RegisterUserAsync` (insert) can block
   until the ~30s server-selection timeout, far exceeding the 10s client timeout.
   - Would also explain hangs; must be ruled in/out by measuring Mongo operation latency.

3. **Request-pipeline concurrency / connection limits**: Kestrel or upstream limits
   (max concurrent connections, container CPU quota) throttling concurrent requests.

4. **Environment mismatch (open question)**: It is not confirmed whether the working Scalar
   test hit the same containerized instance and the same MongoDB as the load test. If Scalar
   hit a local dev instance with a fast local Mongo while the load test hit the container with
   a misconfigured Mongo endpoint, that would point at hypothesis 2. This must be confirmed
   during diagnosis.

The exploration test plus runtime measurement will confirm or refute hypothesis 1 first. If
refuted, re-hypothesize toward 2/3.

## Correctness Properties

Property 1: Bug Condition - Register Succeeds Under Concurrency

_For any_ input where the bug condition holds (isBugCondition returns true — concurrent
register requests at the load-test rate), the fixed register pipeline SHALL return
`201 Created` with a valid JWT token for every well-formed request, completing within the
client's 10-second timeout and producing no "Unexpected end of request content" log entries.

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Preservation - Single-Request and Non-Register Behavior

_For any_ input where the bug condition does NOT hold (isBugCondition returns false —
single/low-volume register, login, duplicate-user rejection, and all non-register endpoints),
the fixed code SHALL produce the same result as the original code, preserving existing
status codes, tokens, exceptions, and responses.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

## Fix Implementation

### Changes Required

The specific change depends on which hypothesis the diagnosis confirms. The candidate fixes
below are documented now but MUST NOT be implemented until the exploration test + measurement
confirm the root cause.

**If hypothesis 1 (thread-pool starvation from PBKDF2) is confirmed:**

**File**: `Infrastructure/Extensions/ServiceCollection/IdentityServiceExtensions.cs` and/or
`Features/Auth/Domain/Services/Implementations/AuthService.cs`

1. **Reduce per-request CPU cost of hashing**: Lower the PBKDF2 iteration count to a sane
   value via `PasswordHasherOptions` (Identity defaults to 100,000 iterations), or evaluate a
   cheaper-but-still-secure configuration appropriate for the threat model.
2. **Prevent pool starvation**: Optionally raise the minimum thread-pool thread count at
   startup (`ThreadPool.SetMinThreads`) so bursts do not stall on gradual pool growth, and/or
   offload hashing so it cannot block request continuations.
3. **Keep hashing off the request critical path where possible** without weakening security.

**If hypothesis 2 (MongoDB server selection) is confirmed:**

**File**: `Infrastructure/Extensions/ServiceCollection/MongoDbServicesExtensions.cs` and
docker-compose / connection configuration

1. Correct the Mongo connection string / network so server selection is fast.
2. Tune `MongoClientSettings` timeouts (server selection, connect) to fail fast rather than
   block past the client timeout.

**If hypothesis 3 (pipeline limits) is confirmed:**

1. Adjust Kestrel/container concurrency limits and resource quotas as appropriate.

The final design will be narrowed to a single confirmed fix before any production code
changes, keeping the change minimal and preserving all behavior in Property 2.

## Testing Strategy

### Validation Approach

Two phases: first surface counterexamples that demonstrate the hang on the UNFIXED code, then
verify the fix resolves the hang and preserves existing behavior. Because this is a
concurrency/performance defect, the exploration "property" is scoped to a concrete,
reproducible concurrent-load case rather than randomized inputs.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples proving register hangs/times out under concurrency BEFORE
implementing the fix, and confirm or refute the thread-pool-starvation root cause. If
refuted, re-hypothesize toward Mongo server selection or pipeline limits.

**Test Plan**: Write an integration-style test that fires a burst of N concurrent
`POST /auth/register` requests (each with a unique username/email) against the running API and
asserts every response is `201` with a token within the 10s timeout. Run on the UNFIXED code
to observe timeouts/hangs. Alongside, capture runtime diagnostics during the burst: thread-pool
queue length and available worker threads, process CPU, and MongoDB operation latency, to
attribute the hang to a specific cause.

**Test Cases**:
1. **Concurrent register burst**: N concurrent fresh registers; assert all `201` + token
   within timeout (will fail on unfixed code).
2. **Sustained concurrent rate**: Drive ~20 req/s registers for a short window; assert no
   `operation timeout` (will fail on unfixed code).
3. **Thread-pool observation**: Record `ThreadPool.ThreadCount` / pending work-item count
   during the burst to confirm starvation (diagnostic).
4. **Mongo latency observation**: Time `UserExistsByUserNameOrEmail` and `RegisterUserAsync`
   under load to rule MongoDB in/out (diagnostic).

**Expected Counterexamples**:
- Register requests exceed 10s / are cancelled under concurrency.
- Likely correlated with thread-pool exhaustion during PBKDF2 hashing; alternatively with
  elevated Mongo operation latency.

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed pipeline
produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  results := RegisterAll_fixed(input)
  ASSERT every result is 201 AND has_valid_token AND completed_within(10s)
         AND no "Unexpected end of request content" logged
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed pipeline
produces the same result as the original pipeline.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT Register_original(input) = Register_fixed(input)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation because it
generates many register/login inputs across the domain and verifies behavior is unchanged for
all non-concurrent cases.

**Test Plan**: Observe behavior on UNFIXED code first (single register returns 201 + valid
token; duplicate user rejected; login returns valid token), then write property-based tests
asserting those invariants hold across generated inputs.

**Test Cases**:
1. **Single register preservation**: For generated fresh username/email/password, register
   returns `201` with a verifiable token.
2. **Duplicate-user preservation**: Registering an existing username/email is rejected.
3. **Login preservation**: After register, login with the same credentials returns a valid
   token; wrong password fails.
4. **Token validity preservation**: Issued tokens carry the same claims/issuer/audience and
   verify against the signing key.

### Unit Tests

- `AuthService.RegisterAsync` returns a token for a new user; throws on duplicate.
- `AuthService.LoginAsync` returns a token for valid credentials; throws on invalid.
- Token generation produces a token with expected claims.

### Property-Based Tests

- Generate random valid register inputs and verify `201` + valid token (single-request).
- Generate random credentials and verify login behavior is preserved.
- Generate mixed non-register inputs and verify responses are unchanged.

### Integration Tests

- Concurrent register burst against the running API completes successfully after the fix.
- Full NBomber `medium`/`all` re-run: `01_register` reports Ok > 0 (no `operation timeout` at
  100%), downstream steps execute, and the backend logs show no new "Unexpected end of
  request content" entries.
