# FinanzOnline pilot — evidence-based blocker review

**Use after** filling `docs/finanzonline-pilot-evidence-pack-template.md` (SUCCESS, FAILURE, RETRY).  
**Do not** invent blockers without **observed** log/DB/API lines in the “Observed evidence” column.

**PASS rules reference:** `docs/finanzonline-bmf-test-validation-runbook.md` §5–§7.  
**Procedure reference:** `docs/finanzonline-pilot-test-execution-plan.md`.

---

## 1. How to use

1. Attach or paste the three completed YAML records (or redacted exports).  
2. For each gap between **expected** and **actual**, add one row below.  
3. If **no** gap → write `No blockers — observed evidence matches expected` under §3.  
4. Severity guide: **S1** production/pilot stop, **S2** fix before customer, **S3** workaround OK, **S4** doc/training only.

---

## 2. Blocker table (copy rows as needed)

| # | Blocker (short title) | Observed evidence | Expected evidence | Likely root cause | Affected modules | Fix type (code/config/ops/training) | Severity | Next action | Owner |
|---|------------------------|-------------------|-------------------|-------------------|------------------|--------------------------------------|----------|-------------|-------|
| 1 | | | | | | | | | |

**Column hints**

- **Observed:** exact log substring, SQL column values, HTTP status + JSON path.  
- **Expected:** cite execution plan step + runbook § (e.g. “S3 + §5 DB row `ProtocolSuccess`”).  
- **Affected modules:** e.g. `PaymentService`, `FinanzOnlineOutboxHostedService`, `FinanzOnlineReconciliationController`, `frontend-admin` outbox page, config `FinanzOnline:*`.

---

## 3. Focused review (mandatory checks)

Answer each with **PASS / FAIL** and point to evidence row # or YAML field.

### 3.1 Success case (S1–S4)

| Check | PASS if |
|-------|---------|
| Payment 201 + receipt + invoice in DB | Evidence SUCCESS `actual_db_evidence` |
| Outbox row exists for `RegistrierkassenSubmission` / `Invoice` | SUCCESS template |
| Terminal status matches runbook §5 | e.g. `ProtocolSuccess` for real TEST happy path |
| Logs show enqueue → (real TEST) submission start/finished → outbox processed | SUCCESS `actual_logs` |

If **FAIL** → blocker row(s) in §2.

### 3.2 Failure case (F1–F3)

| Check | PASS if |
|-------|---------|
| Payment still committed (201 + rows present) | FAILURE `actual_db_evidence` |
| Failure observable (outbox error and/or payment FO fields and/or `finanz_online_errors`) | FAILURE template |
| Matches intended variant (F-A / F-B / F-C) | `failure_variant` + logs |

### 3.3 Retry case (R1–R4)

| Check | PASS if |
|-------|---------|
| `POST …/retry/{paymentId}` returns 200 with expected body | RETRY `actual_admin_api_evidence` |
| No illegal duplicate submit when already `Submitted` | RETRY logs + DB |
| Outbox `attempt_count` / status evolves or documented no-op | RETRY `actual_db_evidence` |

### 3.4 Outbox vs payment state consistency (O1–O4)

| Check | PASS if |
|-------|---------|
| O1 Outbox terminal matches pilot policy | SUCCESS actual |
| O2 Payment `Pending` + outbox `ProtocolSuccess` documented as OK | Notes cite runbook §5 secondary |
| O3 **No** payment `Failed` with outbox `ProtocolSuccess` | Both templates |
| O4 **No** outbox `DeadLetter` with payment `Submitted` | Both templates |

### 3.5 Admin operator clarity (A1–A3)

| Check | PASS if |
|-------|---------|
| A1 Operator found outbox row via UI or `GET …/finanzonline-outbox` | Note in SUCCESS or separate |
| A2 Team distinguishes `GET /api/FinanzOnline/status` from outbox terminal | Training sign-off or runbook §7 ack |
| A3 UI/docs align with `FINANZONLINE_ADMIN_SOURCE_OF_TRUTH.md` | Link or screenshot |

---

## 4. Sign-off

| Role | Name | Date | Signature / ticket |
|------|------|------|----------------------|
| Executor | | | |
| Reviewer (DEV) | | | |
| Reviewer (OP) | | | |

**Summary sentence:**  
`Blockers: { count | none }. Pilot-unblocking work: { list | n/a }.`
