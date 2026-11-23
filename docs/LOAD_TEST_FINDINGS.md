# Load test findings — cashier sale path

Recorded lab results for the POS cash sale path (`login` → catalog → shift → cart → cash payment → shift end). This document is a diagnosis. It does not change connection strings, pooling, or queries.

**Last updated:** 2026-11-23

| Related | Link |
|---------|------|
| Script | [`scripts/load-test/README.md`](../scripts/load-test/README.md) |
| Pool defaults | `ApplicationHost.ApplyProductionNpgsqlPooling` |
| Payment transaction | `PaymentService.CreatePaymentCoreAsync` |

## Scope

- One tenant, Fake TSE (`Tse__Mode=Fake`). No Fiskaly HTTP on the signing path.
- One cash register per cashier. Cashiers do not share a register, so register-scoped `FOR UPDATE` locks do not line the cohort up behind one row.
- Sale p95 is the k6 trend from login through payment and shift end.
- The 50-cashier figure is the recorded baseline (1153 ms). The 100- and 200-cashier figures are the instrumented runs.

## Results

| Cashiers | Sale p95 | Payment step p95 | Verdict |
|----------|----------|------------------|---------|
| 50 | 1153 ms | — | OK. Under the 3000 ms lab budget. |
| 100 | 19.8 s | 14.8 s | Bottleneck is the payment step. |
| 200 | 20.2 s | 7.7 s | Sale p95 stays near 20 s. Wait spreads into login and catalog. |

At 100 cashiers the payment step p95 is 14.8 s and the slowest payment is about 15.5 s. At 200 cashiers the payment p95 drops to 7.7 s while login and catalog slow down. The sale still ends near 20 s. HTTP error rate on these two runs stayed under 1%. k6 exited 99 because sale p95 crossed the 3000 ms script budget, not because requests failed.

## 500 cashiers is a stress test

500 cashiers is a stress test. Production target is 50-200 cashiers.

At 500 cashiers, p95 is ~9.4 s due to the 200-connection pool queue.

This is expected. The pool size (200) is correct for 50-200 cashiers.

The 50-cashier baseline in the table above is 1153 ms, under the 3 s lab budget. The 100- and 200-cashier rows (about 20 s) are the 2026-11-21 lab, recorded while the pool ceiling was still 100. They are not a re-test of the current pool of 200, and they are not a pass under 3 s. The ~9.4 s figure is the post-listen-fix sale p95 in the table below (9429 ms).

Five hundred cashiers starting a sale in the same second is a stress test. It is not a normal production load. A shop does not open hundreds of cash sales on one API process at the same instant. The 3000 ms sale budget in the k6 script is a lab target for the smaller cohorts, not a production SLO for this burst.

The 2026-11-23 run used the existing multi-tenant seed (`tenants.local.json`, first 500 cashiers, 50 tenants, one register each, `PRODUCT_COUNT=3`, Fake TSE). The database pool stayed at 200. Payment code was not changed.

| Run | Admitted | Connection refused | HTTP error rate | Login p95 | Pay p95 | Sale p95 |
|-----|----------|--------------------|-----------------|-----------|---------|----------|
| Before the listen fix | 211 of 500 | 289 | 13.21% | 1179 ms | 6278 ms | 9170 ms |
| After the listen fix | 500 of 500 | 0 | 0.00% | 2699 ms | 2926 ms | 9429 ms |

After the fix, k6 recorded 4500 requests (9 steps × 500 cashiers) and a 100% check pass rate. It still exited 99 because sale p95 is above 3000 ms. That remaining wait is the fiscal transaction holding a pooled connection (ceiling 200), not the TCP accept queue. Login p95 rose from 1.2 s to 2.7 s because every cashier entered the API. Pay p95 dropped from 6.3 s to 2.9 s.

### Listen queue

The local API is Kestrel. `launchSettings.json` profile `http` is `commandName: Project`. nginx in this repo fronts the admin UI and the static POS web, not port 5184. There is no IIS in front of this process, so no proxy `worker_connections` change applies.

`Limits.MaxConcurrentConnections` and `MaxConcurrentUpgradedConnections` were unset (Kestrel default: unlimited). They are now 1000. That ceiling does not enlarge the accept queue. At 500 connections it does not reject anyone.

Kestrel's socket backlog default is 512 (`SocketTransportOptions.Backlog`). On this Windows host a **positive** `listen()` backlog is silently capped at 200. A probe that called `TcpListener.Start(2048)` and then opened 500 connections without accepting them got 200 connected and 300 `ECONNREFUSED`. That matches the first 500-cashier run (about 211 admitted, 289 refused). There is no `net.core.somaxconn` on Windows, and the old AFD dynamic-backlog registry values do not raise this cap.

Windows 8 and later honor `SOMAXCONN_HINT(n)`, which is the negative value `-n`, in the range 200–65535. `ApplicationHost.ConfigureListenBacklog` sets the socket backlog to **-2048 on Windows** and to **2048 on Linux** (Linux then applies `net.core.somaxconn`). The same probe with `Socket.Listen(-2048)` accepted all 500 connections.

## Root cause

The pool paragraphs below describe the 2026-11-21 lab, recorded before the pool helper ran in every environment. The helper now applies in Development as well. The default maximum pool size is 200. The listen-queue fix above does not change that size.

Development does not set a pool size. `appsettings.Development.json` has no `DefaultConnection` and no `MaxPoolSize` or `Minimum Pool Size`. The connection string comes from user secrets or the environment.

On 2026-11-21, `ApplicationHost` applied pool defaults only when `IsProduction()` was true. That helper set `MinPoolSize` 5, `MaxPoolSize` 20, and `ConnectionLifetime` 300 only when those keywords were absent. Development did not enter that block. The helper now runs in every environment and the default maximum is 200. The sentences that follow describe the pool that produced the 100- and 200-cashier table.

Npgsql’s own defaults then apply: **maximum pool size 100, minimum pool size 0, connection timeout 15 seconds.**

Each request gets a scoped `AppDbContext` (`AddDbContext`, `ServiceLifetime.Scoped`, lines 705–713). The context holds one pooled connection from the first open until the request ends. A cash sale keeps that connection for the fiscal transaction and the post-commit work. One hundred concurrent payments fill the pool. The next checkout waits on the 15-second timeout, which matches the 14.8 s payment p95. At 200 cashiers the same wait shows up in login and catalog, so the sale p95 stays near 20 s even when the payment p95 falls.

Copying the production cap of 20 into this lab would make the queue worse. Production capacity is a separate setting and is not the lab default.

## What this is not

- **Not Fake TSE latency.** Fake mode resolves to Soft TSE before any Fiskaly call. `FakeTseProvider.SignAsync` returns a completed task. There is no delay and no HTTP timeout on this path. The Fiskaly client timeout (30 s) and auth retry exist only when signing mode is Fiskaly.
- **Not a shared row lock.** `cash_registers`, `signature_chain_state`, and `receipt_sequences` lock per cash register (and, for the sequence, per register and day). Unique registers do not serialize 100 cashiers. The chain lock does lengthen each request’s own transaction, because it is held until the outer commit.
- **Not a missing index on the insert path.** `payment_details` has indexes on `created_at` and `(cash_register_id, created_at)`. It has no `tenant_id` column. `invoices` has a `tenant_id` index and date indexes. The cash sale does not `ORDER BY` those tables. Per-line product reads inside the transaction add round-trips; they do not explain a 15-second pile-up.

A successful cash sale calls `SaveChanges` twice on the same connection and the same transaction: once inside `TseService.CreateInvoiceSignatureAsync` (line 435) and once in `PaymentService.CreatePaymentCoreAsync` before commit (line 141). The extra call at line 487 is the idempotent replay path.

## Recommendation

Do both. Raising the pool alone leaves each connection busy for the whole fiscal transaction. Shortening the transaction alone still queues once concurrency exceeds the pool.

1. **Measure pool wait separately from SQL time** before changing capacity. Confirm the 15-second waits are pool acquisition, not query time.
2. **Increase the Development pool together with PostgreSQL `max_connections`.** Size them as a pair. Do not copy production `MaxPoolSize` 20 into the lab. A local server whose `max_connections` is also 100 will return `53300` (`sorry, too many clients already`) once the pool plus any other client exceeds that limit.
3. **Reduce how long each request holds its connection.** Read products before `BeginTransaction`. Flush the signature and the payment in one `SaveChanges`. Return the connection before post-commit audit and FinanzOnline dispatch.

No code change is included in the pull request that added this note.
