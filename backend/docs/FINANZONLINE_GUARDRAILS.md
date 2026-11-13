# FinanzOnline guardrails

## Layout

There is no `Services/FinanzOnline/` folder. Canonical surfaces:

| Surface | Location |
|---------|----------|
| Invoice / closing façade | `Services/FinanzOnlineService.cs` |
| Outbox + worker + SOAP transports | `Services/FinanzOnlineIntegration/` |
| Payment retry (legacy stamp) | `Services/FinanzOnlineRetryHostedService.cs` |

## `FinanzOnline:Mode`

Root config key (see `FinanzOnlineModeOptions`):

| Value | Outbox mode | Notes |
|-------|-------------|--------|
| `Simulation` | TEST | Transports still honour nested `UseSimulation` / scenario flags |
| `Test` (default) | TEST | Real TEST SOAP when `UseSimulation=false` + enable flags |
| `Production` / `Prod` | PROD | Requires `FinanzOnline:CutoverGuard` (`AllowProdMode` + approval token when required) |

`GetConfigAsync().Environment` is derived from `Mode` (not a stale DB default).

Nested `Session` / `Registrierkassen` / `TransmissionQuery` `UseSimulation` flags remain the transport simulation switches — do not conflate them with `Mode`.

## Idempotency

Outbox `IdempotencyKey` = SHA256 of `aggregateType|aggregateId|messageType|businessKey|payloadHash|mode`.

- Local + DB lookup before insert
- Unique index on `IdempotencyKey`
- Concurrent race: `DbUpdateException` → detach → re-query → return existing row

## Closing stamps

`SubmitDaily/Monthly/YearlyClosingAsync` are **local stamps only** (`Status = Simulated`). They do **not** enqueue SOAP. Real BMF delivery for fiscal receipts goes through invoice / RKSV special-receipt outbox paths.

## Retry

`FinanzOnlineOutbox:MaxAttempts` default **5**, exponential backoff + jitter (`FinanzOnlineOutboxOptions`).

## Ops checklist

1. Dev: `Mode=Simulation` + nested `UseSimulation=true`
2. TEST SOAP: `Mode=Test`, nested simulation off, enable real TEST flags as documented
3. PROD: `Mode=Production` + cutover guard + real endpoints — never enable by Mode alone without cutover
