# Bugfix Requirements Document

## Introduction

The Ebenezer API serves single requests correctly (a `POST /auth/register` issued from
Scalar returns a token as expected). However, when the NBomber load test
(`EbenezerBackend.LoadTests`, scenario `all`, load level `medium` = 20 req/s injected per
scenario across 5 scenarios for 60s) runs against the API, effectively every request fails.

The first step of every scenario is `01_register`. The NBomber HTML report for the
`medium_all` run shows `01_register` with `Ok.Request.Count = 0` and a failure reason of
`operation timeout` at 100%. Because each scenario returns early when register fails, all
downstream steps report `0/0`. The backend logs repeatedly show:

```
Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException: Unexpected end of request content
  at ...Http1ContentLengthMessageBody.ReadAsyncInternal(...)
  at ...SystemTextJsonInputFormatter.ReadRequestBodyAsync(...)
```

The Kestrel "Unexpected end of request content" message is a symptom, not the cause: the
register request hangs server-side, the shared load-test `HttpClient` (`Timeout = 10s`)
cancels the timed-out in-flight request and aborts the TCP connection mid-body, and Kestrel —
still awaiting the declared `Content-Length` body — reports the truncated body.

This bug makes the API unusable under concurrent load and blocks meaningful load testing of
every other endpoint, since all scenarios depend on register succeeding first.

## Bug Analysis

### Current Behavior (Defect)

When register requests arrive concurrently at the configured load-test rate, the server
fails to complete them within the client timeout.

1.1 WHEN multiple `POST /auth/register` requests arrive concurrently at the configured
load-test rate (20 req/s per scenario across 5 scenarios for 60s) THEN the server fails to
return a `201 Created` response with a token before the client's 10-second timeout elapses,
causing `01_register` to record `operation timeout` for ~100% of requests.

1.2 WHEN a register request times out client-side and the shared `HttpClient` aborts the
in-flight connection mid-body THEN Kestrel logs
`BadHttpRequestException: Unexpected end of request content` because it is still awaiting the
declared `Content-Length` request body.

1.3 WHEN `01_register` fails at the start of each load-test scenario THEN every dependent
downstream step records `0/0` (no successful requests), so the entire load run fails.

### Expected Behavior (Correct)

2.1 WHEN multiple `POST /auth/register` requests arrive concurrently at the configured
load-test rate THEN the system SHALL return a `201 Created` response containing a valid JWT
token for each well-formed request, completing within the client's 10-second timeout without
`operation timeout` failures.

2.2 WHEN register requests are processed under concurrent load THEN the system SHALL NOT
produce `BadHttpRequestException: Unexpected end of request content` log entries caused by
client-side timeouts aborting in-flight requests.

2.3 WHEN the NBomber `medium`/`all` run completes after the fix THEN `01_register` SHALL
report a healthy success rate (Ok requests > 0, no `operation timeout` at 100%), allowing
downstream steps to execute.

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a single `POST /auth/register` request is issued (e.g. via Scalar) THEN the system
SHALL CONTINUE TO return a `201 Created` response with a valid JWT token.

3.2 WHEN a register request uses a username or email that already exists THEN the system
SHALL CONTINUE TO reject it with the existing `UserNameOrEmailAlreadyRegisteredException`
behavior.

3.3 WHEN a `POST /auth/login` request is issued with valid credentials THEN the system SHALL
CONTINUE TO verify the password hash and return a valid JWT token.

3.4 WHEN any non-register endpoint is called (profile, categories, prayers, friendships,
retrospective) THEN the system SHALL CONTINUE TO behave exactly as before the fix.

## Deriving the Bug Condition

### Bug Condition Function

```pascal
FUNCTION isBugCondition(X)
  INPUT: X of type RegisterLoad   // a set of concurrent register requests at a given arrival rate
  OUTPUT: boolean

  // The bug manifests when register requests arrive concurrently at the load-test rate
  // and the server cannot complete them within the client timeout.
  RETURN X.endpoint = "POST /auth/register"
         AND X.concurrentArrivalRate >= mediumLoadRate   // 20 req/s per scenario, 5 scenarios
         AND NOT allRequestsCompletedWithin(X, 10_seconds)
END FUNCTION
```

### Property Specification (Fix Checking)

```pascal
// Property: Fix Checking - Register succeeds under concurrency
FOR ALL X WHERE isBugCondition(X) DO
  results <- RegisterAll'(X)            // F' = fixed register pipeline
  ASSERT for_every_request_in(results):
           status = 201
           AND has_valid_token(response)
           AND completed_within(10_seconds)
           AND no "Unexpected end of request content" logged
END FOR
```

### Preservation Goal (Preservation Checking)

```pascal
// Property: Preservation Checking - Non-concurrent / non-register behavior unchanged
FOR ALL X WHERE NOT isBugCondition(X) DO
  ASSERT F(X) = F'(X)                   // fixed code behaves identically to original
END FOR
```

Where:
- **F**: the original (unfixed) register pipeline.
- **F'**: the fixed register pipeline.
