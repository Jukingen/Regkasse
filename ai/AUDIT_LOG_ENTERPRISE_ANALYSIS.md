# Audit Log System – Current Architecture & Enterprise Upgrade Analysis

**Scope:** Backend audit logging only. No changes to POS, TSE, receipt numbering, daily closing, FinanzOnline, or money rounding.

---

## 1. Current Audit Architecture

### 1.1 Overview

- **Single table:** All audit events (user lifecycle, payments, entity changes, system operations) are stored in one `audit_logs` table.
- **Write path:** Controllers/services call `IAuditLogService` methods; `AuditLogService` builds an `AuditLog` entity and `Add()`s it; no update of existing rows in normal flow.
- **Read path:** `AuditLogController` exposes filtered/paginated lists and single-item get; service returns entities, controller maps to `AuditLogEntryDto` (with optional actor display name resolution for user audit).
- **Cleanup:** Optional retention policy via `DeleteAuditLogsOlderThanAsync(DateTime)` — **hard delete** of rows older than cutoff (not append-only immutable retention in the strict sense).
- **Event creation:** Fire-and-forget style via `TryLogUserLifecycleAsync` (failure is logged but does not fail the main operation). Other methods throw on failure.

### 1.2 Components

| Component | Role |
|-----------|------|
| **AuditLog** (entity) | Single model for all event types; lives in `Models/AuditLog.cs`. |
| **audit_logs** (table) | PostgreSQL table; created/aligned via EF migrations (e.g. `AlignAuditLogsTableWithEntity`). |
| **IAuditLogService / AuditLogService** | Writes and reads audit data; uses `AppDbContext.AuditLogs`, `IHttpContextAccessor` for IP/UserAgent/Endpoint. |
| **UserManagementController** | Main producer of user-lifecycle events via `TryLogUserLifecycleAsync`. |
| **AdminUsersController** | Alternative producer (if used) for user CRUD; also calls `LogUserLifecycleAsync`. |
| **AuditLogController** | HTTP API for listing/filtering/pagination and single-item get; uses `AuditLogEntryDto` and `IActorDisplayNameResolver` for user lists. |
| **UserAuditDiffHelper** | Whitelist and builder for safe OldValues/NewValues (user lifecycle diff only). |
| **ActorDisplayNameResolver** | Resolves actor UserId → display name for API response (user audit only). |

---

## 2. Data Model

### 2.1 Database Table: `audit_logs`

- **Inherits BaseEntity:** `id` (PK), `created_at`, `updated_at`, `created_by`, `updated_by`, `is_active`.
- **Core fields:**  
  `SessionId`, `UserId`, `UserRole`, `Action`, `EntityType`, `EntityId`, `EntityName`,  
  `OldValues`, `NewValues`, `RequestData`, `ResponseData`,  
  `Status`, `Timestamp`, `Description`, `Notes`,  
  `IpAddress`, `UserAgent`, `Endpoint`, `HttpMethod`, `HttpStatusCode`, `ProcessingTimeMs`,  
  `ErrorDetails`, `CorrelationId`, `TransactionId`,  
  `Amount`, `PaymentMethod`, `TseSignature`
- **Indexes (from migration):** `Action`, `EntityId`, `EntityType`, `Timestamp`, `UserId`.
- **Constraints:** No unique constraint on (Timestamp, Action, EntityName, UserId); no hash/checksum for integrity.

### 2.2 OldValues / NewValues Usage

- **Purpose:** Store previous and new state for “change” events (e.g. user update, role change) as JSON strings (max 4000 chars each).
- **Where set:**  
  - **LogUserLifecycleAsync:** When caller passes `oldValues`/`newValues` (e.g. from `UserAuditDiffHelper.CreateSafeSnapshot` for USER_UPDATE; role-only for USER_ROLE_CHANGE).  
  - **LogEntityChangeAsync:** Generic old/new object serialization.  
  - **LogPaymentOperationAsync:** Only `NewValues` (response data); `OldValues` null.
- **Content:** User lifecycle uses a **whitelist** (FirstName, LastName, Email, UserName, Role, IsActive, IsDemo); Notes, TaxNumber, EmployeeNumber, passwords are excluded.
- **Format:** JSON; no versioned schema. Frontend parses for “Änderungen ansehen” diff UI.

---

## 3. Event Generation Flow

### 3.1 Entry Points

| Source | Method | When |
|--------|--------|------|
| **UserManagementController** | `TryLogUserLifecycleAsync` → `LogUserLifecycleAsync` | After successful user create, update, role change, deactivate, reactivate, password change, force reset, legacy delete. |
| **UserManagementController** | `LogUserActivityAsync` | Change-own-password (CHANGE_OWN_PASSWORD). |
| **UserManagementController** | `LogSystemOperationAsync` | ROLE_PERMISSIONS_UPDATE, ROLE_DELETE. |
| **AdminUsersController** | `LogUserLifecycleAsync` (direct) | User create, update, role change, deactivate, reactivate, password reset (if this controller is used). |
| **AuditLogService** (internal) | `LogPaymentOperationAsync`, `LogEntityChangeAsync`, `LogSystemOperationAsync`, `LogUserActivityAsync` | When called from other parts of the app (payment flows, etc.); current codebase shows user-lifecycle and role operations as main producers. |

### 3.2 Current Audit Event Types (in use)

**User lifecycle (AuditLogActions):**

- `USER_CREATE`
- `USER_UPDATE` (with optional OldValues/NewValues)
- `USER_ROLE_CHANGE` (with optional OldValues/NewValues for role only)
- `USER_DEACTIVATE`
- `USER_REACTIVATE`
- `USER_PASSWORD_RESET` / `FORCE_RESET_PASSWORD`
- `CHANGE_OWN_PASSWORD`
- (Legacy) soft-delete logged as `USER_DEACTIVATE`

**Role/system:**

- `ROLE_PERMISSIONS_UPDATE`
- `ROLE_DELETE`

**Defined but not necessarily used from current controllers:**  
Payment, Invoice, Cart, Customer, Receipt, System, TSE, etc. (available for future or other services).

### 3.3 Flow Summary

1. Controller performs business operation (e.g. update user).
2. On success, controller calls `TryLogUserLifecycleAsync` (or direct `LogUserLifecycleAsync` in AdminUsersController) with action, actor, target, description, and optionally old/new snapshots.
3. `AuditLogService.LogUserLifecycleAsync` builds one `AuditLog` row (new Guid, Timestamp = UtcNow, IP/UserAgent/Endpoint from HttpContext, CorrelationId from middleware or new Guid), serializes old/new to JSON, `Add()` + `SaveChangesAsync()`.
4. No retry or queue; failure is either thrown or only logged (depending on caller).

---

## 4. Append-Only vs Mutability vs Deletion

- **Append-only in normal path:** Yes. Only `_context.AuditLogs.Add(auditLog)` is used for creating events; there is **no UPDATE** on existing audit rows.
- **Overwritten:** No. No code updates an existing `AuditLog` row.
- **Deleted:** Yes. `DeleteAuditLogsOlderThanAsync` **permanently deletes** rows with `Timestamp < cutoffDate` via `RemoveRange` + `SaveChangesAsync`. Used by the cleanup endpoint (e.g. `DELETE api/AuditLog/cleanup` with body `CutoffDate`).
- **Implication:** Logs are immutable until retention cleanup; cleanup is hard delete, so no “tombstone” or archive table in the current design.

---

## 5. Metadata Captured Today

| Metadata | Source | Stored In | Notes |
|----------|--------|-----------|--------|
| **Actor** | Caller | `UserId`, `UserRole` | Actor = user who performed the action. |
| **Target** | Caller | `EntityName` (e.g. target user id), `EntityId`, `EntityType` | For user lifecycle, target user id in `EntityName`. |
| **IP** | HttpContext | `IpAddress` | `GetClientIpAddress`: X-Forwarded-For (first) or Connection.RemoteIpAddress. |
| **Description** | Caller | `Description` | Human-readable summary (e.g. "User updated: …"). |
| **Reason** | Caller | `Notes` | E.g. deactivation reason. |
| **RequestData** | Caller / service | `RequestData` | JSON; in lifecycle = `{ targetUserId, reason }`. |
| **ResponseData** | Caller / service | `ResponseData` | Used in payment/entity flows. |
| **OldValues / NewValues** | Caller | `OldValues`, `NewValues` | JSON; user lifecycle uses whitelisted fields only. |
| **Endpoint / HttpMethod / HttpStatusCode** | HttpContext | `Endpoint`, `HttpMethod`, `HttpStatusCode` | Request path, method, response status. |
| **UserAgent** | HttpContext | `UserAgent` | Truncated to 200 chars. |
| **CorrelationId** | Middleware or new Guid | `CorrelationId` | Request correlation or new Guid. |
| **Timestamp** | Server | `Timestamp` | UtcNow at write. |
| **SessionId** | New Guid per log | `SessionId` | Per-event, not HTTP session. |

**Not stored:** Request body (except as part of RequestData when explicitly passed), auth token, password or secrets (whitelist excludes them).

---

## 6. How Audit Logs Are Returned to Frontend

- **DTO:** List endpoints return `AuditLogsResponse` with `List<AuditLogEntryDto>`. Single-item returns `AuditLogResponse` with `AuditLog` (entity). DTO adds `ActorUserId`, `ActorDisplayName`, `ActorRole` (display name resolved for user-audit endpoint only).
- **Pagination:** `page`, `pageSize` (default 50, max 100); `TotalCount`, `TotalPages` returned.
- **Filtering:** Query params: `startDate`, `endDate`, `userId`, `userRole`, `action`, `entityType`, `entityId`, `status`. User-scoped endpoint: `GET api/AuditLog/user/{userId}` (filters by `EntityName == userId`).
- **Ordering:** Descending by `Timestamp` (newest first).
- **Endpoints (summary):**  
  `GET api/AuditLog`, `GET api/AuditLog/{id}`, `GET api/AuditLog/user/{userId}`, `GET api/AuditLog/payment/{paymentId}`, `GET api/AuditLog/correlation/{id}`, `GET api/AuditLog/transaction/{id}`, `GET api/AuditLog/suspicious-admin-actions`, `GET api/AuditLog/statistics`, `GET api/AuditLog/export`, `DELETE api/AuditLog/cleanup`.
- **Permissions:** `UserView`, `AuditView`, `AuditExport`, `AuditCleanup` (depending on endpoint).

---

## 7. Security Issues

1. **Cleanup is destructive:** Hard delete of old logs can conflict with legal retention (e.g. 7 years). No archive or append-only retention policy; only a cutoff date.
2. **X-Forwarded-For trust:** First proxy IP is trusted without validation; spoofed header can misattribute actor IP.
3. **Sensitive data in RequestData/Description:** Callers can pass arbitrary strings; no schema or redaction. User lifecycle path is controlled (whitelist for OldValues/NewValues), but RequestData/Description are free-form.
4. **No integrity check:** Rows are not signed/hashed; tampering (e.g. at DB level) cannot be detected.
5. **Audit of audit access:** Who read which audit log is not recorded.
6. **Error isolation:** `TryLogUserLifecycleAsync` catches and logs failures; main operation still succeeds. Good for availability but failed audit writes can go unnoticed unless logs are monitored.

---

## 8. Scalability Issues

1. **Single table:** All event types in one table; high volume (e.g. payment logs) will grow one index set; no partitioning by time or type.
2. **Synchronous write:** Each event triggers immediate `SaveChangesAsync()`; no batching or async queue. Under load, audit writes add latency and compete with business transactions.
3. **No retention tiers:** Only one policy (delete older than cutoff); no cold/archive tier or separate retention by event type.
4. **Large payloads:** OldValues/NewValues/RequestData/ResponseData up to 4000 chars each; big events increase row size and I/O.
5. **Read scaling:** All reads from primary; no read replicas or dedicated audit store.

---

## 9. Missing Enterprise Features

- **Immutable store:** No WORM or append-only guarantee; cleanup deletes rows; no hash chain or signing.
- **Event schema versioning:** No version field; consumers must guess JSON shape.
- **Structured event taxonomy:** Action strings are free-form constants; no formal event type registry or versioned event schema (e.g. event type + version).
- **Async / out-of-process write:** No message queue or background writer; all writes in-request.
- **Partitioning / sharding:** No time or tenant partitioning.
- **Access audit:** No “who read which audit record” log.
- **Retention policy per category:** Single global cutoff; no different retention for user vs payment vs system.
- **Export/streaming:** Export is pull-based (HTTP); no push/stream to SIEM or data lake.
- **Idempotency:** No idempotency key; duplicate requests can create duplicate events.

---

## 10. Safe Upgrade Plan

**Principles:** Minimal change, no regressions, no touch to TSE/receipt/daily closing/FinanzOnline/money rounding. Prefer additive changes and optional features.

### Phase 1 – Non-breaking hardening (current scope)

- **Retention:** Document that cleanup is optional and must respect legal retention (e.g. 7 years); consider renaming or scoping cleanup to “archive” or “anonymize” instead of delete, or add a guard (e.g. max cutoff = now − 7 years). **Do not change cleanup implementation yet** if it would affect compliance; only document and optionally add safeguards.
- **Sensitive data:** Keep UserAuditDiffHelper whitelist; add code comments that RequestData/Description must not contain passwords or tokens. Optionally add a small redaction helper for future use.
- **IP:** Document X-Forwarded-For trust; optionally add config to disable forwarded header (use only RemoteIpAddress) in trusted environments.
- **Observability:** Ensure audit write failures are clearly logged (and optionally metrics) so monitoring can alert.

### Phase 2 – Additive schema (optional, backward compatible)

- **Event version:** Add optional `EventVersion` (e.g. int or string) to entity and table; default 1. No change to existing writers.
- **Idempotency key:** Add optional `IdempotencyKey` column; callers can set it for critical flows to deduplicate. No change for existing callers.
- **Category / resource type:** Add optional `ResourceCategory` (e.g. "user_lifecycle", "payment") for future partitioning or retention by category; backfill from existing Action/EntityType.

### Phase 3 – Async and scale (later, requires design)

- **Async writer:** Introduce an in-process queue (e.g. channel) or out-of-process queue (e.g. message bus); worker persists to DB. API returns 202 or still 200 with “logged” semantics. Requires deployment and failure-handling design.
- **Partitioning:** If volume demands, add time-based partitioning (e.g. by month) or separate tables per category; migration and query path changes.
- **Integrity:** Add optional hash column (e.g. SHA-256 of canonical payload) and/or sign critical events; verification endpoint or tool for compliance.

### Phase 4 – Compliance and enterprise (optional)

- **Retention policy:** Implement retention by category (e.g. user lifecycle 7 years, payment 10 years); move to archive table or cold storage instead of hard delete where required.
- **Access audit:** Log reads of audit API (who, when, which resource) in a separate audit stream or table.
- **Export/streaming:** Add webhook or push to external system for critical event types; keep existing pull export.

**Suggested order:** Execute Phase 1 (documentation + small safeguards + logging). Phase 2 only if product/compliance needs it. Phase 3–4 as separate projects with explicit scope and risk review.

---

## 11. Audit event type standardization (implemented)

- **Enum:** `AuditEventType` in `Models/AuditLog.cs`: UserCreated, UserUpdated, UserRoleChanged, UserDeactivated, UserReactivated, PasswordResetForced, ChangeOwnPassword, UserPasswordReset, RolePermissionsUpdated, RoleDeleted, LoginSuccess, UserLogout, UserDeleted, LoginFailed (14), Other (99).
- **Safe migration for existing logs:** Backing values 0–12 are unchanged; only LoginFailed uses new value 14. Existing rows with `action_type` 0–12 continue to map correctly to the same event semantics. The `Action` column still stores legacy strings (USER_CREATE, USER_UPDATE, etc.) for backward compatibility with existing queries and reports.
- **Centralized creation:** All user-lifecycle audit events are created via `AuditLogService.LogUserLifecycleAsync(AuditEventType, ...)`. Controllers call this (or TryLogUserLifecycleAsync with enum). USER_UPDATED includes structured changes (BuildStructuredChanges); USER_ROLE_CHANGED includes role diff in `changes`; only changed values are logged.

---

## References (in repo)

- Entity: `backend/Models/AuditLog.cs`
- Service: `backend/Services/AuditLogService.cs`
- Controller: `backend/Controllers/AuditLogController.cs`
- User lifecycle producer: `backend/Controllers/UserManagementController.cs` (TryLogUserLifecycleAsync, LogUserActivityAsync, LogSystemOperationAsync)
- DTO / mapping: `backend/Models/DTOs/AuditLogEntryDto.cs`, `AuditLogEntryMapper.cs`
- Safe diff whitelist: `backend/Services/UserAuditDiffHelper.cs`
- Table migration: `backend/Migrations/20260308204523_AlignAuditLogsTableWithEntity.cs`
- Do-not-touch / compliance: `.cursor/rules/07-do-not-touch.mdc`, `ai/07_DO_NOT_TOUCH.md`, `ai/05_SECURITY_COMPLIANCE.md`
