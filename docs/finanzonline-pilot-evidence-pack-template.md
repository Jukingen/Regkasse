# FinanzOnline pilot — evidence pack templates

Fill **one record per scenario run** (copy the YAML block below the divider). Procedure: `docs/finanzonline-pilot-test-execution-plan.md`. PASS rules: `docs/finanzonline-bmf-test-validation-runbook.md` §5–§7.

**Storage:** Keep completed packs outside git (ticket / object storage) if they contain PII; optionally commit **redacted** copies under `docs/evidence-packs/YYYY-MM/` with no secrets.

---

## Template: SUCCESS (`PAYMENT_RECEIPT_FO_OUTBOX_SUCCESS`)

```yaml
scenario_name: PAYMENT_RECEIPT_FO_OUTBOX_SUCCESS
date_time_utc_start:
date_time_utc_end:
environment:   # e.g. staging-kasseapi
api_git_sha:
executor:      # name or handle

config_summary:  # redacted: only key names and true/false
  FinanzOnline.Session.UseSimulation:
  FinanzOnline.Registrierkassen.UseSimulation:
  FinanzOnline.Registrierkassen.EnableRealTestSubmission:
  FinanzOnline.TransmissionQuery.UseSimulation:
  FinanzOnline.TransmissionQuery.EnableRealTestQuery:
  FinanzOnlineOutbox.Enabled:
  FinanzOnlineOutbox.PollInterval:
  Tse.TseMode:
  Tse.Mode:

trigger_action: POST /api/pos/payment (or /api/Payment) — note idempotency key if any

expected_logs:
  - Payment created successfully; Invoice {id}
  - TSE signature generated for payment {id}  # if effectiveTseRequired
  - FinanzOnline outbox enqueued MessageType=RegistrierkassenSubmission AggregateType=Invoice CorrelationId={invoice:N}
  - FinanzOnline TEST submission start … CorrelationId=…
  - FinanzOnline TEST submission finished Success=True …  # real TEST path
  - FinanzOnline outbox processed Id=… Status=ProtocolSuccess …  # or agreed terminal

actual_logs:
  - paste:

expected_db_evidence:
  payment_details:
    id:
    finanz_online_status:   # may be Pending — not sole PASS (runbook §5)
    finanz_online_error:
  invoices:
    id:
    source_payment_id:
  receipts:
    payment_id:
  finanz_online_outbox_messages:
    id:
    aggregate_type: Invoice
    aggregate_id:
    message_type: RegistrierkassenSubmission
    correlation_id:
    status: ProtocolSuccess  # or agreed
    mode: TEST
    attempt_count:
    last_error_code:
    last_error_message:

actual_db_evidence:
  paste or SQL output:

expected_admin_api_evidence:
  GET /api/admin/finanzonline-outbox?correlationId={invoice:N}: >= 1 row, terminal status matches DB
  GET /api/admin/finanzonline-outbox/{id}: matches DB row

actual_admin_api_evidence:
  paste:

pass_fail: PASS | FAIL
notes:
root_cause_if_fail:
```

---

## Template: FAILURE (`PAYMENT_RECEIPT_FO_OUTBOX_FAILURE`)

```yaml
scenario_name: PAYMENT_RECEIPT_FO_OUTBOX_FAILURE
failure_variant: F-A | F-B | F-C   # see execution plan
date_time_utc_start:
date_time_utc_end:
environment:
api_git_sha:
executor:

config_summary:
  # for F-A document EnableRealTestSubmission: false during run

trigger_action: POST payment + worker cycle (document variant)

expected_logs:
  - Payment created successfully (commit must succeed)
  - FinanzOnline outbox enqueued … OR enqueue failure path
  - TEST submission finished Success=False OR outbox processed with non-success terminal
  - Specific: TEST_REAL_SUBMISSION_DISABLED / RKDB / SESSION per variant

actual_logs:
  - paste:

expected_db_evidence:
  payment_details:
    id:
    finanz_online_status:
    finanz_online_error:
  finanz_online_outbox_messages:
    id:
    status:  # RetryableFailure | DeadLetter | ProtocolFailure | …
    last_error_code:
    last_error_message:
    failure_category:
  finanz_online_errors:  # if populated

actual_db_evidence:
  paste:

expected_admin_api_evidence:
  GET /api/admin/finanzonline-outbox?correlationId=…: shows error fields
  optional GET /api/FinanzOnline/errors: last rows

actual_admin_api_evidence:
  paste:

pass_fail: PASS | FAIL   # PASS = failure was observable and payment not rolled back incorrectly
notes:
root_cause_if_fail:      # use if PASS=FAIL (e.g. silent failure)
```

---

## Template: RETRY (`FO_RECONCILIATION_RETRY_OR_HOSTED_JOB`)

```yaml
scenario_name: FO_RECONCILIATION_RETRY_OR_HOSTED_JOB
retry_via: ADMIN_POST | HOSTED_JOB | BOTH
date_time_utc_start:
date_time_utc_end:
environment:
api_git_sha:
executor:

config_summary:
  FinanzOnlineRetryJob.Enabled:

trigger_action: POST /api/admin/finanzonline-reconciliation/retry/{payment_id}

expected_logs:
  - FinanzOnlineRetry / audit attempt (PaymentService)
  - optional: FinanzOnline retry cycle (hosted job)
  - FinanzOnline outbox processed … if new worker attempt
  - If payment already Submitted: no second external submit (early return)

actual_logs:
  - paste:

expected_db_evidence:
  payment_id:
  invoice_id:
  correlation_id_n:
  payment_details.finanz_online_retry_count:  # increment when isRetry path updates
  finanz_online_outbox_messages:
    id:
    status:
    attempt_count:

actual_db_evidence:
  paste:

expected_admin_api_evidence:
  POST retry: HTTP 200, FinanzOnlineRetryResponse body
  GET /api/admin/finanzonline-outbox/{id}: updated if worker ran

actual_admin_api_evidence:
  paste:

pass_fail: PASS | FAIL
notes:
root_cause_if_fail:
```

---

## Combined pack index (optional)

After three runs, attach an index file:

```yaml
evidence_pack_index:
  runbook_ref: docs/finanzonline-bmf-test-validation-runbook.md
  execution_plan_ref: docs/finanzonline-pilot-test-execution-plan.md
  environment:
  completed_utc:
  scenarios:
    - { scenario: SUCCESS, pass_fail: PASS, artifact: success-2026-03-28.yaml }
    - { scenario: FAILURE, pass_fail: PASS, artifact: failure-f-a-2026-03-28.yaml }
    - { scenario: RETRY, pass_fail: PASS, artifact: retry-2026-03-28.yaml }
```
