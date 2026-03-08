# FE-Admin Users — Compliance Hardening Deliverable

Summary of the compliance hardening pass: code changes, policy/procedural items, and go-live checklist.

---

## 1. What changed in code

| Area | Change |
|------|--------|
| **Audit anti-tamper** | `AppDbContext`: override `SaveChanges` and `SaveChangesAsync` to reject any **UPDATE** of existing `AuditLog` rows. Throws `InvalidOperationException` if modified audit entries are detected. DELETE remains allowed only via `DeleteAuditLogsOlderThanAsync` (retention cleanup). |
| **CorrelationId** | New `CorrelationIdMiddleware`: reads `X-Correlation-Id` from request or generates a GUID; sets `HttpContext.Items["CorrelationId"]` and adds `X-Correlation-Id` to response. Registered in `Program.cs` after CORS, before Authentication. |
| **CorrelationId → audit** | `AuditLogService`: all log methods (entity change, system, user activity, user lifecycle) use request correlation ID from context when not explicitly passed. New helper `GetRequestCorrelationId()`; `LogUserLifecycleAsync` uses `correlationId ?? GetRequestCorrelationId() ?? Guid.NewGuid()`. |
| **IP/UserAgent privacy** | `AuditLogService`: new `GetUserAgentMinimized()` truncates User-Agent to **200 characters**. All audit writes use this instead of full User-Agent. IP capture unchanged (X-Forwarded-For or RemoteIpAddress). |
| **Suspicious admin report** | New `IAuditLogService.GetSuspiciousAdminActionsAsync(since, limit)` and `GET /api/auditlog/suspicious-admin-actions?since=...&limit=100`. Returns USER_CREATE, USER_DEACTIVATE, USER_REACTIVATE, USER_PASSWORD_RESET, USER_ROLE_CHANGE. Authorized with `UsersView`. |

**Files touched:**
- `backend/Data/AppDbContext.cs` — SaveChanges overrides + `EnforceAuditLogAppendOnly()`
- `backend/Middleware/CorrelationIdMiddleware.cs` — new
- `backend/Program.cs` — register `CorrelationIdMiddleware`
- `backend/Services/AuditLogService.cs` — CorrelationId from context, UserAgent minimization, `GetSuspiciousAdminActionsAsync`
- `backend/Controllers/AuditLogController.cs` — `GET suspicious-admin-actions` endpoint
- `docs/architecture/USERS_MODULE_COMPLIANCE_HARDENING.md` — retention, IP/UA policy, incident playbook, go-live notes

---

## 2. What remains policy/procedural (non-code)

- **Retention:** Define and document retention for `AuditLog` (recommend ≥7 years for user-lifecycle for RKSV/BAO). Ensure audit cleanup job uses a cutoff that respects this.
- **Fiscal vs non-fiscal separation:** Keep treating fiscal tables (receipts, TSE, daily closing) as 7-year retention; treat `AuditLog` as one retention policy unless you split “fiscal audit” vs “operational audit” later.
- **IP/UserAgent retention:** Decide how long to keep IP/UA in audit (e.g. same as full audit, or shorter for non–user-lifecycle entries if you split). Document in privacy notice.
- **Incident playbook:** Add steps to your runbook: (1) Run `GET /api/auditlog/suspicious-admin-actions?since=...`, (2) Use `GET /api/auditlog/correlation/{id}` to trace a request, (3) Escalate per internal process, (4) Retain report for incident file.
- **Frontend:** Optionally send `X-Correlation-Id` from FE-Admin on critical user actions for end-to-end correlation.

---

## 3. Go-live checklist

- [ ] CorrelationId middleware: response header `X-Correlation-Id` present on API responses.
- [ ] Audit: one user-lifecycle action (e.g. deactivate with reason) creates an audit row with CorrelationId, IP, truncated UserAgent; no UPDATE to existing audit rows possible.
- [ ] Suspicious report: `GET /api/auditlog/suspicious-admin-actions?since=<last 24h>` returns only high-risk user-lifecycle actions; accessible with UsersView (e.g. Auditor).
- [ ] Retention: retention window(s) for AuditLog documented and cleanup job (if any) uses a compliant cutoff.
- [ ] Cleanup: audit cleanup endpoint restricted (Administrator); used only per retention policy.
- [ ] Incident playbook: “Suspicious admin actions report” and “CorrelationId lookup” added to runbook with owner.

Detailed text for retention, IP/UA, and incident playbook: see `docs/architecture/USERS_MODULE_COMPLIANCE_HARDENING.md`.
