# Authoritative simulation — acceptance test strategy

This document maps **operator-facing “authoritative simulation”** behavior to automated tests. Goal: stable, **non-flaky** checks by favoring **pure unit tests** (no real network, no wall-clock coupling beyond explicit zero-delay options).

## Layers

| Layer | Role | Flakiness risk |
|-------|------|----------------|
| **A — Engine / evaluator** | `FinanzOnlineDeveloperSimulationEngine`, `FinanzOnlineReadinessEvaluator`, `FinanzOnlineTransportPathKindResolver` | Low: in-memory, deterministic |
| **B — Service / reconciliation** | `PaymentService.RetryFinanzOnlineSubmitAsync`, reconciliation enrichment | Low–medium: in-memory DB in existing tests |
| **C — HTTP / E2E** | Full admin API + auth + worker | High for CI; not required for minimum safe coverage |

**Policy:** Prefer A; reuse B where already present; defer C unless a dedicated E2E job exists.

## Scenario matrix (1–12)

| # | Scenario | Primary owner | Test anchor |
|---|----------|---------------|-------------|
| 1 | Immediate simulated success | A | Config `ImmediateSuccess` → immediate protocol-style success (`SIM_IMMEDIATE_OK`, `completed`) |
| 2 | Retry then success | A | Config `RetryThenSuccess` + `RetryCountBeforeSuccess` |
| 3 | Permanent failure | A | Config `PermanentFailure` → `RKDB_COMMAND_INVALID` |
| 4 | Awaiting protocol then success | A | Config `AwaitingProtocolThenSuccess` + `ProtocolPendingQueriesBeforeSuccess` on transmission query |
| 5 | Dead letter (simulated path) | A | Config `DeadLetter` → transient `HTTP_503` (outbox worker promotes to DeadLetter after max attempts — covered by outbox unit/integration elsewhere) |
| 6 | Simulation active readiness warning | A | `FinanzOnlineReadinessEvaluatorTests`: `FO_READINESS_SIMULATION_ACTIVE`, `FO_READINESS_SIMULATION_SCENARIO_ACTIVE` |
| 7 | Mixed transport error | A | `FO_READINESS_MIXED_TRANSPORT_LAYERS` |
| 8 | Real-test disabled conflict | A | `FO_READINESS_CONFLICT_ENABLE_REAL_TEST_WITH_REG_SIMULATION` |
| 9 | Operator sees simulated badge | Frontend | `isSimulatedFinanzOnlineTransportPath`, transport tag colors; i18n contract for `transportSurfaceBadge.simulated` |
| 10 | Authoritative outbox evidence | Frontend | i18n keys for outbox column/tooltips + `buildFinanzOnlineOutboxHandoffHref` |
| 11 | Reconciliation derived warning | Frontend | i18n contract for `queuePage.derivedLegacyTruthAlert` |
| 12 | Manual retry still works | B | `FinanzOnlineReconciliationTests` + `foReconciliationRowTriage` retry contract |

## Non-flaky practices

- Set `ArtificialLatencyMs = 0`, `FixedDelayMs = 0` on simulation options in tests.
- Use **distinct correlation / transmission ids** per attempt when testing counters.
- Avoid asserting on timestamps; prefer stable codes (`HTTP_503`, `SIM_IMMEDIATE_OK`).
- Frontend: test **pure functions** and **locale JSON shape**, not full Next.js pages, unless the repo already runs Playwright for these routes.

## Manual / exploratory (not automated here)

- Visual confirmation of Ant Design `Alert` ordering on queue and outbox pages.
- Worker-driven outbox transition to `DeadLetter` after `MaxAttempts` under simulated transient failures (integration with hosted worker).

## Phase A — outbox worker integration (implemented)

`FinanzOnlineOutboxHostedService` exposes **internal** one-shot hooks (`ProcessOneForIntegrationTestsAsync`, `ReconcileOneForIntegrationTestsAsync`) for `KasseAPI_Final.Tests` only. Tests live in `FinanzOnlineOutboxWorkerIntegrationTests.cs`, use **PostgreSQL** (`PostgreSqlReplayFixture` + `SkippableFact`) because EF InMemory does not support `ExecuteUpdateAsync` used for claim semantics.

Run (requires Docker or `REGKASSE_TEST_POSTGRES`; otherwise tests skip):

```bash
dotnet test backend/KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~FinanzOnlineOutboxWorkerIntegrationTests"
```
