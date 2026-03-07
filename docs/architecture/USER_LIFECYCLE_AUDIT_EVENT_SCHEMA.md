# Immutable Audit Event Schema — User Lifecycle

Design for immutable audit events covering user lifecycle (create, update, deactivate, reactivate, role change, password reset). Aligns with RKSV/BAO traceability and supports FE activity timeline and reporting.

---

## 1. Event schema (immutable)

Single event record — **append-only**; no update or delete in normal operation.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| **eventId** | Guid (UUID) | ✅ | Unique event identifier; primary key. |
| **occurredAt** | DateTime (UTC) | ✅ | When the event occurred. |
| **actorUserId** | string (max 450) | ✅ | User who performed the action (e.g. admin who deactivated). |
| **targetUserId** | string (max 450) | ✅ | User who was affected (e.g. deactivated user). |
| **tenantId** | string (max 100) | ❌ | Tenant in multi-tenant setups; null for single-tenant. |
| **branchId** | string (max 100) | ❌ | Branch/location context; null if not used. |
| **actionType** | string (max 50) | ✅ | Event type: USER_CREATE, USER_UPDATE, USER_DEACTIVATE, USER_REACTIVATE, USER_ROLE_CHANGE, USER_PASSWORD_RESET, etc. |
| **beforeState** | JSON (text/jsonb) | ❌ | Snapshot of target user (or relevant subset) before the action. Sensitive fields (password hash) omitted. |
| **afterState** | JSON (text/jsonb) | ❌ | Snapshot after the action. Same rules as beforeState. |
| **reason** | string (max 500) | ❌ | Human-readable reason (e.g. deactivation reason); required for certain actionTypes by policy. |
| **ipAddress** | string (max 45) | ❌ | Client IP (IPv4/IPv6). |
| **userAgent** | string (max 500) | ❌ | Client User-Agent. |
| **correlationId** | string (max 100) | ❌ | Links related events (e.g. same request or workflow). |
| **source** | string (max 20) | ✅ | Origin: `web-admin` \| `api` \| `system`. |
| **status** | string (max 20) | ✅ | Outcome: Success, Failed, etc. (enum). |

**Optional extensions (same record):**

- **actorRole** — Role of the actor at event time (max 50).
- **description** — Short human-readable summary (max 500).

**Example JSON (API / storage):**

```json
{
  "eventId": "550e8400-e29b-41d4-a716-446655440000",
  "occurredAt": "2025-03-15T14:32:00Z",
  "actorUserId": "admin-guid-1",
  "targetUserId": "user-guid-2",
  "tenantId": null,
  "branchId": "branch-1",
  "actionType": "USER_DEACTIVATE",
  "beforeState": { "isActive": true, "userName": "jane", "role": "Cashier" },
  "afterState": { "isActive": false, "userName": "jane", "role": "Cashier" },
  "reason": "Ausscheiden zum 31.03.2025",
  "ipAddress": "192.168.1.10",
  "userAgent": "Mozilla/5.0...",
  "correlationId": "req-abc-123",
  "source": "web-admin",
  "status": "Success"
}
```

**Mapping to current `AuditLog` (if reusing one table):**

| New field | Current AuditLog |
|-----------|------------------|
| eventId | Id |
| occurredAt | Timestamp |
| actorUserId | UserId |
| targetUserId | EntityName (when EntityType=User) |
| tenantId | — (add column or leave null) |
| branchId | — (add column or leave null) |
| actionType | Action |
| beforeState | OldValues (JSON string) |
| afterState | NewValues (JSON string) |
| reason | Notes |
| ipAddress | IpAddress |
| userAgent | UserAgent |
| correlationId | CorrelationId |
| source | — (add column; e.g. web-admin/api/system) |
| status | Status |

---

## 2. Indexing strategy

Goal: fast queries by **target user** (FE activity timeline), **actor**, **time range**, and **action type**.

| Index | Columns | Purpose |
|-------|---------|--------|
| **PK** | eventId | Primary key; unique lookup. |
| **IX_user_lifecycle_target_occurred** | (targetUserId, occurredAt DESC) | FE “activity for user X” and time-ordered list. |
| **IX_user_lifecycle_actor_occurred** | (actorUserId, occurredAt DESC) | “What did this admin do?”. |
| **IX_user_lifecycle_occurred** | (occurredAt DESC) | Time-bounded exports and retention scans. |
| **IX_user_lifecycle_action_occurred** | (actionType, occurredAt DESC) | Filter by action (e.g. all deactivations). |
| **IX_user_lifecycle_correlation** | (correlationId) | Trace all events in one request. |
| **IX_user_lifecycle_tenant_branch** (optional) | (tenantId, branchId, occurredAt DESC) | Multi-tenant / branch reporting. |

**PostgreSQL example:**

```sql
CREATE INDEX ix_user_lifecycle_target_occurred ON user_lifecycle_audit_events (target_user_id, occurred_at DESC);
CREATE INDEX ix_user_lifecycle_actor_occurred ON user_lifecycle_audit_events (actor_user_id, occurred_at DESC);
CREATE INDEX ix_user_lifecycle_occurred ON user_lifecycle_audit_events (occurred_at DESC);
CREATE INDEX ix_user_lifecycle_action_occurred ON user_lifecycle_audit_events (action_type, occurred_at DESC);
CREATE INDEX ix_user_lifecycle_correlation ON user_lifecycle_audit_events (correlation_id) WHERE correlation_id IS NOT NULL;
```

**Note:** If using the existing `audit_logs` table with EntityType = 'User', add indexes on `(entity_name, timestamp DESC)` and `(user_id, timestamp DESC)` (entity_name = targetUserId, user_id = actorUserId). Add a `source` column and index if filtering by source.

---

## 3. Retention strategy

- **Regulatory:** RKSV/BAO and company policy often require 7+ years for fiscal and audit data. User lifecycle events that support “who changed what and when” fall under that.
- **Default recommendation:** Retain user lifecycle audit events for **at least 7 years** from `occurredAt` (or align with receipt/audit retention).
- **No destructive delete in normal operation:** Do not run DELETE on this table as part of routine cleanup; only consider archival to cold storage (e.g. export to blob + delete) after retention period, with formal process and approval.
- **If reusing generic AuditLog:** Apply retention/cleanup only to non–user-lifecycle or non–fiscal event types; **exclude** EntityType = 'User' (and optionally other critical types) from any automated delete.
- **Archival (optional):** After N years, export events to immutable storage (e.g. S3/Glacier with WORM), then optionally remove from hot DB; keep index of eventId and occurredAt for lookup.

**Summary:**

| Rule | Action |
|------|--------|
| Retention minimum | 7 years (or as per legal/audit policy). |
| Routine job | No DELETE on user lifecycle events. |
| Cleanup/archival | Only with approved process; prefer export-then-archive. |
| Generic AuditLog | Exclude User lifecycle from aggressive cleanup. |

---

## 4. Query API shape for FE activity timeline

**Use case:** FE shows “Activity” tab for a user: list of events where **targetUserId** = selected user, ordered by time descending.

**Recommended endpoint:**

```
GET /api/admin/users/{userId}/activity
GET /api/AuditLog/user/{userId}   (existing; ensure filter is by target, not only actor)
```

**Query parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| page | int (default 1) | Page number. |
| pageSize | int (default 20, max 100) | Page size. |
| startDate | DateTime (optional) | Filter occurredAt >= startDate (UTC). |
| endDate | DateTime (optional) | Filter occurredAt <= endDate (UTC). |
| actionType | string (optional) | Filter by action (e.g. USER_DEACTIVATE). |
| source | string (optional) | Filter by source (web-admin, api, system). |

**Response shape (JSON):**

```json
{
  "auditLogs": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "timestamp": "2025-03-15T14:32:00Z",
      "action": "USER_DEACTIVATE",
      "description": "User deactivated: jane. Reason: Ausscheiden zum 31.03.2025",
      "status": "Success",
      "actorUserId": "admin-guid-1",
      "targetUserId": "user-guid-2",
      "reason": "Ausscheiden zum 31.03.2025",
      "source": "web-admin"
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20
}
```

**Field mapping from canonical schema:**

- `id` ← eventId  
- `timestamp` ← occurredAt  
- `action` ← actionType  
- `description` ← description or derived from actionType + reason  
- `status` ← status  
- `actorUserId`, `targetUserId`, `reason`, `source` ← same  

**Important:** The “activity for user” API must return events where **targetUserId** = requested user (actions performed *on* that user). If the current implementation filters by **actor** (UserId), add a separate filter or overload: e.g. `GetUserAuditLogsAsync(userId, byTarget: true)` so that when `byTarget` is true, filter by EntityType = User and EntityName = userId.

---

## 5. Anti-tamper recommendations

- **Append-only store:** Only INSERT; no UPDATE or DELETE in application code for this table. Enforce via DB triggers or application discipline and code review.
- **Permissions:** Restrict table write access to the service account that runs the audit logging code; no ad-hoc UPDATE/DELETE by support or devs in production.
- **Schema:** Use a dedicated table (e.g. `user_lifecycle_audit_events`) or a strict subset of the generic audit table (e.g. EntityType = 'User') so that retention and access rules apply clearly.
- **Integrity (optional but strong):**  
  - Store a **hash** of the event payload (e.g. SHA-256 of canonical JSON) in a column `payloadHash`; later, recompute and compare to detect tampering.  
  - Or use a **signed payload** (e.g. HMAC or TSE-style signature) so that any change invalidates the signature.
- **Audit the auditors:** Any administrative or batch process that touches this table (e.g. archival script) should log its own actions to a separate, restricted log.
- **Access control:** Read access only for roles that need it (e.g. Auditor, Administrator); expose via fixed API only, no direct DB access for normal users.
- **Time and source:** Rely on server-side `occurredAt` (UTC); do not trust client-supplied timestamps for integrity. Populate `source` (web-admin / api / system) and optionally IP/UserAgent from the trusted server context.

**Summary:**

| Measure | Implementation |
|---------|----------------|
| Append-only | No UPDATE/DELETE in app; optional DB trigger to block updates/deletes. |
| Least privilege | Only audit service can write; read via API with policy. |
| Optional integrity | payloadHash or signed payload column; verify on read or in batch job. |
| Audit of changes | Any DBA/script that modifies table must be logged elsewhere. |
| Server-side time | occurredAt set by server (UTC). |

---

## 6. Implementation checklist (reference)

- [ ] Add or align table columns: eventId, occurredAt, actorUserId, targetUserId, tenantId, branchId, actionType, beforeState, afterState, reason, ipAddress, userAgent, correlationId, source, status.
- [ ] Add indexes: targetUserId+occurredAt, actorUserId+occurredAt, occurredAt, actionType+occurredAt, correlationId.
- [ ] Ensure “activity for user” API filters by targetUserId (and optionally actorUserId); document in API spec.
- [ ] Set retention policy: no delete of user lifecycle events within retention period (e.g. 7 years); exclude from generic audit cleanup if shared table.
- [ ] Enforce append-only in code and (optionally) DB; consider payloadHash or signature for anti-tamper.
- [ ] Document source values (web-admin, api, system) and set them in all write paths.

---

*This schema supports RKSV/BAO traceability, FE activity timeline, and safe retention; final retention and archival steps should be confirmed with legal/compliance.*
