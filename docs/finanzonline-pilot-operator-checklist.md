# FinanzOnline pilot — operator / developer checklist

Condensed from `docs/finanzonline-pilot-test-execution-plan.md`. Full detail stays there.

**Legend:** **FAIL** = scenario invalid — fix before continuing. **WARN** = document only; may still PASS BMF TEST if outbox OK. **STOP** = halt this run; resolve then restart from **G1**.

---

## Run order

1. **G1–G5** (global)  
2. **S1–S4** (success)  
3. **F1–F3** (failure — pick F-A / F-B / F-C first)  
4. **R1–R4** (retry)  
5. **O1–O4** (outbox vs payment — execution plan §4)  
6. **A1–A3** (admin clarity — execution plan §5)

---

| Step | Action | Expected result | Fail vs warn | Stop? | Evidence to capture |
|------|--------|-----------------|--------------|-------|---------------------|
| **G1** | Apply config (runbook §3) | Keys set; secrets not in repo | **FAIL** if worker disabled while testing real pipeline | STOP if FO disabled by mistake | Redacted env list |
| **G2** | Restart API | Process up | **FAIL** if crash loop | STOP | Restart time |
| **G3** | Read `FinanzOnline.TransportStartup` | Matches intent (real TEST vs sim) | **FAIL** if real TEST intended but `UseSimulation=true` or `EnableRealTestSubmission=false` | STOP | Log lines |
| **G4** | Optional `POST /api/FinanzOnline/test-connection` | HTTP 200 | **WARN** if fails — not rkdb proof (runbook §7) | No | JSON snippet |
| **G5** | Open register; TSE path OK for fiscal pay | Payment can get signature if required | **FAIL** if TSE blocks all fiscal pays | STOP | Register id |
| **S1** | `POST` payment | HTTP 201; DB payment+invoice+receipt | **FAIL** if not 201 or missing rows | STOP | `paymentId` |
| **S2** | Find outbox by `correlationId` = `invoice.Id` `N` | Row exists; enqueued log | **FAIL** if no row after commit | STOP | `correlation_id`, outbox `id` |
| **S3** | Wait poll + protocol | Outbox terminal per runbook §5 (e.g. `ProtocolSuccess`) | **FAIL** if `DeadLetter`/`PermanentFailure` without approval | STOP for success scenario | Log + `GET …/outbox/{id}` |
| **S4** | Check `payment_details.FinanzOnline*` | Any value | **WARN** if payment `Pending` but outbox `ProtocolSuccess` — expected decoupling | No | Column snapshot |
| **F1–F3** | Induce chosen failure | Payment still committed; error visible | **FAIL** if payment rolled back solely due to FO | STOP | Outbox error fields |
| **R1–R4** | `POST …/retry/{paymentId}` | Response 200; state evolves or no-op per rules | **FAIL** if 500 loop or duplicate illegal submit | STOP if unstable | Retry response + outbox |
| **O1–O4** | Compare outbox vs payment | No contradiction (e.g. outbox `DeadLetter` + payment `Submitted`) | **FAIL** on O3/O4 mismatch | STOP until root-caused | Both sides |
| **A1–A3** | Operator finds row without DEV | Found via admin UI or API | **WARN** if only SQL possible | No | Screenshot or URL |

---

## When to stop the whole test day

- **STOP** after any **G** or **S** step marked STOP until fixed.  
- **Do not** treat **payment `FinanzOnlineStatus=Pending`** alone as STOP after **S3** if outbox terminal is **PASS** (runbook §5 secondary note).

---

## Who does what

| Item | Operator (OP) | Developer (DEV) |
|------|---------------|-------------------|
| Config / restart | — | G1–G2, F-A/F-C |
| Log pull | Can with access | Usually |
| SQL read-only | If granted | Yes |
| Admin API | Yes | Yes |
| Evidence YAML fill | Yes | Review |
