# FE-Admin Users — Compliance Hardening

Compliance hardening pass for the Users module: audit anti-tamper, correlation ID, IP/UserAgent policy, retention, and incident playbook.

---

## 1. Audit anti-tamper (append-only)

**Code:**
- `AppDbContext` enforces **append-only** for `AuditLog`: any `UPDATE` to an existing audit row throws in `SaveChanges`/`SaveChangesAsync`.
- **DELETE** of audit rows is only allowed via the dedicated retention method `DeleteAuditLogsOlderThanAsync` (used by the cleanup endpoint), not ad-hoc.

**Policy/procedural:**
- Restrict who can call the audit cleanup endpoint (e.g. Administrator only; already enforced).
- Document that cleanup must follow retention policy (see below); do not purge fiscal-relevant audit before legal retention ends.

---

## 2. CorrelationId propagation

**Code:**
- **Middleware:** `CorrelationIdMiddleware` runs early. It reads `X-Correlation-Id` from the request or generates a new GUID, sets `HttpContext.Items["CorrelationId"]`, and adds `X-Correlation-Id` to the response header.
- **Audit:** All audit log writes (payment, entity change, system, user activity, user lifecycle) receive the request correlation ID from context when not explicitly provided, so one request can be traced across multiple audit entries.

**Policy/procedural:**
- Frontend/admin clients should send `X-Correlation-Id` on critical flows (e.g. user create, deactivate, password reset) for end-to-end tracing; optional but recommended.
- Log aggregation (e.g. ELK) should index `CorrelationId` for cross-service correlation if you add more services later.

---

## 3. IP / UserAgent capture (privacy minimization)

**Code:**
- **IP:** Client IP is taken from `X-Forwarded-For` (first value) or `Connection.RemoteIpAddress`, stored in audit for security/incident response.
- **User-Agent:** Stored with **privacy minimization**: truncated to 200 characters to reduce fingerprinting while keeping enough for security (browser/client family). Full UA is not stored.

**Policy/procedural:**
- **Retention:** Decide how long to keep IP/UserAgent in audit (e.g. 90 days for non-fiscal operational audit; longer or 7 years only where required for fiscal/legal). Implement via cleanup job and retention window (see Retention below).
- **Disclosure:** Document in privacy notice that admin actions are logged with IP and truncated User-Agent for security and compliance.

---

## 4. Retention: fiscal vs non-fiscal PII separation

**Fiscal records (RKSV/BAO, 7 years):**
- Receipts, invoices, TSE signatures, daily closings, FinanzOnline submissions.
- Stored in: `Receipts`, `ReceiptItems`, `ReceiptTaxLines`, `Invoices`, `TseSignature`, `DailyClosing`, `FinanzOnlineSubmission`, etc.
- **Do not** shorten retention below legal requirement (e.g. 7 years in AT).

**User-lifecycle audit (operational + compliance):**
- `AuditLog` entries for user create, update, deactivate, reactivate, password reset, role change.
- **Recommendation:** Retain user-lifecycle audit for at least **7 years** for RKSV/BAO traceability (who did what, when, and why). Align with your legal advice.
- **Separation:** Fiscal tables hold no PII beyond what is legally required for the receipt/invoice. Admin/user PII (who performed the action, IP, truncated UA) lives in `AuditLog`; apply access controls and retention to `AuditLog` separately from fiscal data.
- **Cleanup:** The audit cleanup endpoint (`DELETE /api/auditlog/cleanup`) should only be used with a cutoff date that respects the longer of: (a) fiscal retention (e.g. 7 years) and (b) your internal policy for user-lifecycle audit. Prefer deleting only clearly non-fiscal, non–user-lifecycle audit if you ever split tables; otherwise keep one retention window for the whole `AuditLog` (e.g. 7 years).

**Non-fiscal PII (e.g. login logs, non–user-lifecycle audit):**
- If you later introduce separate “operational” or “security” logs (e.g. login only), shorter retention (e.g. 90 days) may be acceptable if allowed by policy and law; keep separation from fiscal and user-lifecycle audit clear.

---

## 5. Incident playbook: suspicious admin actions

**Code:**
- **Endpoint:** `GET /api/auditlog/suspicious-admin-actions?since=...&limit=100`
- Returns recent high-risk user-lifecycle actions: **USER_CREATE**, **USER_DEACTIVATE**, **USER_REACTIVATE**, **USER_PASSWORD_RESET**, **USER_ROLE_CHANGE**.
- Authorized with `UsersView` (Administrator, SuperAdmin, BranchManager, Auditor) so Auditors can run it for incident review.

**Policy/procedural (incident playbook):**
1. **On suspicion of compromised admin or misuse:** Run the suspicious-admin-actions report for the relevant time window (`since` = start of incident window). Review actor, target user, action, reason, timestamp, IP, CorrelationId.
2. **Correlation:** Use `GET /api/auditlog/correlation/{correlationId}` to get all audit entries for a given request if you have one correlation ID from the report or logs.
3. **Escalation:** Per internal security/HR process: revoke access, force password reset, deactivate account, and document in incident log.
4. **Retention:** Keep export of the report (or equivalent audit query) for the incident file according to your incident retention policy.

---

## 6. Go-live checklist (short)

- [ ] **CorrelationId:** Confirm middleware is registered and response header `X-Correlation-Id` is present on API responses.
- [ ] **Audit:** Trigger one user-lifecycle action (e.g. deactivate with reason); confirm audit row has CorrelationId, IP, truncated UserAgent, and no UPDATE to existing audit rows is possible.
- [ ] **Suspicious report:** Call `GET /api/auditlog/suspicious-admin-actions?since=<last 24h>` with a UsersView role; confirm only high-risk user-lifecycle actions are returned.
- [ ] **Retention:** Document and implement retention window(s) for `AuditLog` (e.g. 7 years for user-lifecycle; do not purge fiscal-relevant data early).
- [ ] **Cleanup:** Restrict audit cleanup to Administrator; ensure cutoff date respects retention policy.
- [ ] **Incident playbook:** Add “Run suspicious-admin-actions report” and “CorrelationId lookup” to your incident response runbook and assign owner.
