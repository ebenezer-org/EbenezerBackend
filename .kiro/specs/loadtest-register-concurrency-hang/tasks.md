# Implementation Plan

## Overview

This plan follows the bug condition methodology for the concurrent register hang: write a
reproduction (exploration) test that FAILS on the unfixed code, write preservation tests that
PASS on the unfixed code, diagnose and confirm the root cause, apply a minimal fix, and verify
both the exploration/preservation tests and a full NBomber re-run. Write the exploration test
BEFORE any production change and run it against the UNFIXED code to confirm the bug.

## Task Dependency Graph

```json
{
  "waves": [
    { "wave": 1, "tasks": ["1", "2"] },
    { "wave": 2, "tasks": ["3"] },
    { "wave": 3, "tasks": ["4"] },
    { "wave": 4, "tasks": ["5"] },
    { "wave": 5, "tasks": ["6"] }
  ]
}
```

Wave 1 (tasks 1 and 2) run against the UNFIXED code and have no dependencies. Task 3
(diagnosis) depends on task 1's failure. Task 4 (fix + verify) depends on tasks 2 and 3. Task
5 (load-test re-run) depends on task 4. Task 6 (checkpoint) depends on task 5.

## Tasks

- [x] 1. Write bug condition exploration test (concurrent register hang)
  - **Property 1: Bug Condition** - Register Succeeds Under Concurrency
  - **CRITICAL**: This test MUST FAIL on the unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate register hangs/times out under concurrency
  - **Scoped PBT Approach**: This is a concurrency/performance defect, so scope the "for all"
    property to a concrete, reproducible concurrent-load case: fire N concurrent
    `POST /auth/register` requests (each with a unique username/email, e.g. password
    "Teste@123") against the running API
  - Assert every response is `201 Created` with a non-empty JWT token, completing within the
    10-second client timeout (matches Bug Condition `isBugCondition` and Expected Behavior in design)
  - Optionally add a sustained-rate variant (~20 req/s for a short window) to mirror the
    NBomber `medium` profile
  - Run the test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (timeouts / non-201 responses - this proves the bug exists)
  - Document counterexamples found (e.g. "X of N register requests exceeded 10s / were cancelled")
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Single-Request and Non-Register Behavior
  - **IMPORTANT**: Follow observation-first methodology
  - Observe on UNFIXED code: a single `POST /auth/register` with a fresh username/email
    returns `201` + a verifiable token; a duplicate username/email is rejected; `POST /auth/login`
    with valid credentials returns a valid token
  - Write property-based tests capturing those observed invariants across generated inputs
    (fresh register -> 201 + valid token; duplicate -> rejected; login -> valid token; token
    carries expected claims/issuer/audience and verifies against the signing key) - from the
    Preservation Requirements in design
  - Property-based testing generates many test cases for stronger preservation guarantees
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms the baseline behavior to preserve)
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3. Confirm and measure the root cause (diagnosis before fixing)
  - Run the exploration test from task 1 against the running API while capturing runtime
    diagnostics during the concurrent burst
  - Record `ThreadPool` available worker threads and pending work-item count to confirm or
    refute thread-pool starvation from PBKDF2 hashing (Hypothesis 1 in design)
  - Time `UserExistsByUserNameOrEmail` and `RegisterUserAsync` under load to rule MongoDB
    server-selection/connectivity in or out (Hypothesis 2 in design)
  - Confirm whether the working Scalar test and the load test hit the same containerized
    instance and the same MongoDB (open question in design)
  - **OUTCOME**: A single confirmed root cause that narrows the fix in task 4. If Hypothesis 1
    is refuted, re-hypothesize toward 2/3 and update design.md before proceeding
  - _Requirements: 1.1, 1.2_

- [x] 4. Fix for register hang under concurrent load

  - [x] 4.1 Implement the confirmed fix
    - Apply the minimal change for the root cause confirmed in task 3 (see Fix Implementation
      in design):
      - Hypothesis 1: tune PBKDF2 cost via `PasswordHasherOptions` and/or raise
        `ThreadPool.SetMinThreads` / offload hashing so it cannot starve request continuations
      - Hypothesis 2: correct Mongo connection/network and tune `MongoClientSettings` timeouts
        to fail fast
      - Hypothesis 3: adjust Kestrel/container concurrency limits
    - Keep the change minimal and preserve all behavior covered by Property 2
    - _Bug_Condition: isBugCondition(input) from design (concurrent register over load-test rate)_
    - _Expected_Behavior: expectedBehavior(result) from design (201 + valid token within timeout, no truncated-body logs)_
    - _Preservation: Preservation Requirements from design_
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 3.4_

  - [x] 4.2 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Register Succeeds Under Concurrency
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior; when it passes, the bug is fixed
    - **EXPECTED OUTCOME**: Test PASSES (all concurrent registers return 201 + token within timeout)
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 4.3 Verify preservation tests still pass
    - **Property 2: Preservation** - Single-Request and Non-Register Behavior
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - **EXPECTED OUTCOME**: Tests PASS (no regressions in single register, duplicate handling, login, tokens)
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 5. Re-run the load test to verify the fix end-to-end
  - Run the NBomber suite (`heterogeneous`, scenario `all`, load level `medium`) against the
    containerized API
  - Confirm the generated `nbomber_report_*.html` shows `01_register` with Ok requests > 0 and
    no `operation timeout` at 100%, and that downstream steps now execute
  - Confirm the backend logs show no new `BadHttpRequestException: Unexpected end of request content` entries
  - _Requirements: 2.1, 2.2, 2.3_

- [~] 6. Checkpoint - Ensure all tests pass
  - Ensure all unit, property-based, and integration tests pass, and the load-test report is
    healthy. Ask the user if questions arise.

## Notes

- Tasks 1 and 2 MUST be completed against the UNFIXED code: task 1 is expected to FAIL
  (confirming the bug) and task 2 is expected to PASS (capturing baseline behavior to preserve).
- No production code changes until task 3 confirms the root cause; the design lists candidate
  fixes per hypothesis and the final fix is narrowed to the confirmed cause.
- The "Unexpected end of request content" Kestrel error is a symptom of client-side timeout
  aborts, not the root cause; do not chase it directly.
