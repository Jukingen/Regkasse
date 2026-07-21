# Audit & activity logging guardrails

## Layout

There is no `Services/Audit/` folder. Canonical surfaces:

| Surface | Location |
|---------|----------|
| Security / compliance audit stream | `IAuditLogService` / `AuditLogService` → `audit_logs` |
| Column truncate + JSON redaction | `AuditLogPersistenceSanitizer` |
| User field whitelist / forbidden keys | `UserAuditDiffHelper` |
| FA activity feed (bell / email / webhook) | `Services/Activity/*` → `activity_events` |
| License sales (non-fiscal) | `BillingAuditService` → `billing_audit_log` |

Do not confuse `AuditEventType` (immutable compliance log) with `ActivityEventType` (operator notifications).

## Mandatory fields (AGENTS mapping)

| Concept | `AuditLog` property | DB column |
|---------|---------------------|-----------|
| `actor_user_id` | `UserId` | `user_id` |
| `actor_role` | `UserRole` | `user_role` |
| `tenant_id` | `TenantId` | `tenant_id` |
| `action_type` | `ActionType` (+ legacy `Action` string) | `action_type` / `action` |
| `timestamp_utc` | `Timestamp` (`DateTime.UtcNow`) | `timestamp` |

Activity feed uses `ActorUserId`, `TenantId`, `Type`, `CreatedAtUtc` (snake_case columns).

## Sensitive data

Never persist passwords, tokens, security stamps, voucher codes, or tax numbers in audit JSON.

- Prefer `UserAuditDiffHelper.CreateSafeSnapshot` for user diffs.
- `AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn` redacts known sensitive property names, then truncates to 4000 chars (defense-in-depth; not a substitute for safe callers).
- POS critical audits never include voucher code values (`PosCriticalActionAuditService`).

All `IAuditLogService` write paths stamp `TenantId` via ambient tenant or `LegacyDefaultTenantIds.Primary` fallback. With null ambient tenant, EF global filters hide those rows on read (fail-closed) — Super Admin / ops queries must use an ambient tenant or `IgnoreQueryFilters` where policy allows.

## Auth events

| Event | Action string | When |
|-------|---------------|------|
| `LoginSuccess` | `USER_LOGIN` | After tokens issued |
| `LoginFailed` | `USER_LOGIN_FAILED` | Invalid credentials / inactive |
| `UserLogout` | `USER_LOGOUT` | Logout / logout-all |

## Ops notes

- Append-only: updates to existing `audit_logs` rows are rejected in `AppDbContext`.
- Retention only via `DeleteAuditLogsOlderThanAsync` (legal hold aware).
- Typed `ActionType` preferred for lifecycle / restore / license; free-form payment actions may leave `ActionType` null.
