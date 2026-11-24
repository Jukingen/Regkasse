# Load test findings — cashier sale path

Recorded lab results for the POS cash sale path (`login` → catalog → shift → cart → cash payment → shift end). This document is a diagnosis. It does not change connection strings, pooling, or queries.

**Last updated:** 2026-11-24

| Related | Link |
|---------|------|
| Script | [`scripts/load-test/README.md`](../scripts/load-test/README.md) |
| Pool defaults | `ApplicationHost.ApplyProductionNpgsqlPooling` |
| Payment transaction | `PaymentService.CreatePaymentCoreAsync` |

## Scope

- One tenant, Fake TSE (`Tse__Mode=Fake`). No Fiskaly HTTP on the signing path.
- One cash register per cashier. Cashiers do not share a register, so register-scoped `FOR UPDATE` locks do not line the cohort up behind one row.
- Sale p95 is the k6 trend from login through payment and shift end.
- The 50-cashier figure is the recorded baseline (1153 ms). The 2026-11-21 rows used a pool ceiling of 100. The 2026-11-24 rows are the re-run with the current pool maximum of 200.

## Summary

Current numbers at pool maximum 200, Fake TSE, HTTP error rate 0% on every row. Production target is 50-200 cashiers on one API process.

| Cashiers | Sale p95 | Pay p95 | Verdict |
|---------:|---------:|--------:|---------|
| 50 | 1153 ms | — | Pass (baseline). |
| 100 | 2269 ms | 375 ms | Pass. Under the 3 s lab budget. |
| 200 | 4131 ms, 5350 ms | 271 ms, 695 ms | Over the 3 s lab budget on two runs. Login is the slowest step. Not a validated pass. |
| 500 | 9429 ms | 2926 ms | Stress test, not a production target. Wait is the 200-connection pool queue. |

The k6 script calls the API directly. The POS client (Expo SDK 58) is not in that path, so these numbers do not depend on the SDK version. Details for each row are below.

## Results

### 2026-11-21, pool ceiling 100

| Cashiers | Sale p95 | Payment step p95 | Verdict |
|----------|----------|------------------|---------|
| 50 | 1153 ms | — | OK. Under the 3000 ms lab budget. |
| 100 | 19.8 s | 14.8 s | Bottleneck is the payment step. |
| 200 | 20.2 s | 7.7 s | Sale p95 stays near 20 s. Wait spreads into login and catalog. |

At 100 cashiers the payment step p95 is 14.8 s and the slowest payment is about 15.5 s. At 200 cashiers the payment p95 drops to 7.7 s while login and catalog slow down. The sale still ends near 20 s. HTTP error rate on these two runs stayed under 1%. k6 exited 99 because sale p95 crossed the 3000 ms script budget, not because requests failed.

### 2026-11-24, pool maximum 200

Same sale path, one tenant (`dev`), Fake TSE (`Tse__Mode=Fake`, `Tse__Provider=fake`, `Tse__FallbackEnabled=true`, `Tse__SoftTseEnabled=true`). Credential file `cashiers.local.json` (200 cashiers, one register each). Default `PRODUCT_COUNT=5`. The connection string does not set `MaxPoolSize`; `Database:MaxPoolSize` is 200. PostgreSQL `max_connections` on this host was 300.

```text
k6 run -e VUS=100 -e CASHIERS_FILE=cashiers.local.json scripts/load-test/pos-critical-path.js
k6 run -e VUS=200 -e CASHIERS_FILE=cashiers.local.json scripts/load-test/pos-critical-path.js
```

| Cashiers | Sale p95 | Payment step p95 | Login p95 | HTTP error rate | 3 s budget |
|----------|----------|------------------|-----------|-----------------|------------|
| 100 | 2269 ms | 375 ms | 820 ms | 0.00% | Pass. k6 exit 0. 1100 requests, checks 100%. |
| 200 | 4131 ms | 271 ms | 1295 ms | 0.00% | Fail. k6 exit 99. 2200 requests, checks 100%. |

Payment is no longer the pile-up. At 200 cashiers the pay step p95 is 271 ms and the HTTP error rate is 0%, while the full sale p95 is 4131 ms. The 3 s lab budget holds at 100 cashiers and misses at 200. Startup auto-Tagesabschluss was still catching up business day 2026-11-23 on other tenants during this run.

#### 200 cashiers, step timings (2026-11-24, later the same morning)

Same cohort (`VUS=200`, `cashiers.local.json`, `PRODUCT_COUNT=5`, Fake TSE, pool maximum 200). k6 JSON samples, one point per HTTP call. HTTP error rate 0%. Sale p95 was 5350 ms (k6 exit 99). A run taken while startup auto-Tagesabschluss was still scanning registers was slower on every step (sale p95 10111 ms, pay p95 3533 ms) and is not the table below.

Step p95 values do not add up to the sale p95. The sale figure is the p95 of the whole iteration.

| Step | Calls | p50 | p95 |
|------|------:|----:|----:|
| Login | 200 | 2142 ms | 2171 ms |
| Current shift | 200 | 243 ms | 1594 ms |
| Shift start | 200 | 241 ms | 610 ms |
| Catalog | 200 | 375 ms | 523 ms |
| Add item (each of 5) | 1000 | 241 ms | 518 ms |
| Payment | 200 | 561 ms | 695 ms |
| Shift end | 200 | 290 ms | 634 ms |

Login is the slowest step. Payment is not. A typical cashier, using the p50 of each step, spends about 5.1 s: login 2.1 s, five adds about 1.2 s together, payment 0.6 s, and the other four calls about 1.2 s together. That is the 3.8 s gap in the earlier 4131 ms / 271 ms row as well: the missing time is the sum of the non-payment calls, and login is the largest single call.

Login p50 and p95 differ by 29 ms. Every cashier waited about 2.1 s, which is a batch finishing together, not a slow-query tail. `AuthController.Login` calls `CheckPasswordAsync` on Identity's default password hasher. This host does not set `Password.IterationCount`. Two hundred of those checks at the same instant fill the CPU. A catalog cache does not move this number. Catalog p95 is 523 ms. Lowering the hasher iteration count would weaken password storage and is not a lab-budget change.

The next tail is current-shift (p50 243 ms, p95 1594 ms). It is still smaller than login.

## 500 cashiers is a stress test

500 cashiers is a stress test. Production target is 50-200 cashiers. Do not use the 500-cashier rows as a pass or fail gate.

At 500 cashiers, p95 is ~9.4 s due to the 200-connection pool queue.

This is expected for that burst. On the 2026-11-24 re-run the pool of 200 keeps the pay step under 400 ms at 100 and 200 cashiers. The 3 s sale budget holds at 100 and misses at 200.

The 50-cashier baseline is 1153 ms. The 2026-11-24 re-run with pool 200 measured 100 cashiers at 2269 ms (under 3 s) and 200 cashiers at 4131 ms (over 3 s, HTTP errors 0%). The ~20 s rows are the 2026-11-21 pool-100 lab. The ~9.4 s figure is the post-listen-fix sale p95 in the table below (9429 ms).

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

On 2026-11-21, `ApplicationHost` applied pool defaults only when `IsProduction()` was true. That helper set `MinPoolSize` 5, `MaxPoolSize` 20, and `ConnectionLifetime` 300 only when those keywords were absent. Development did not enter that block. The helper now runs in every environment and the default maximum is 200. The sentences that follow describe the pool that produced the 2026-11-21 table (sale p95 about 20 s), not the 2026-11-24 re-run.

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
