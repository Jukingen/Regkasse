# Prompt C — Admin Users API & Authorization (Summary)

**Base URL:** `/api/admin/users`  
**Authorization:** Policy `AdminUsers` (role **Administrator** only). All checks are backend-only; FE cannot be trusted.

---

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/admin/users` | List users (query: `role`, `isActive`) |
| GET | `/api/admin/users/{id}` | Get user by id (returns safe DTO with `etag`) |
| POST | `/api/admin/users` | Create user (audit: USER_CREATE) |
| PATCH | `/api/admin/users/{id}` | Partial update (audit: USER_UPDATE, USER_ROLE_CHANGE if role changed). **If-Match** header = `etag` (ConcurrencyStamp) for optimistic concurrency; 412 if mismatch. |
| POST | `/api/admin/users/{id}/deactivate` | Deactivate (body: `{ "reason": "..." }` required). Audit: USER_DEACTIVATE. Session invalidation. |
| POST | `/api/admin/users/{id}/reactivate` | Reactivate (body: `{ "reason": "..." }` optional). Audit: USER_REACTIVATE. |
| POST | `/api/admin/users/{id}/force-password-reset` | Force new password (body: `{ "newPassword": "..." }`). Audit: USER_PASSWORD_RESET. Session invalidation. 204 No Content. |
| GET | `/api/admin/users/{id}/activity` | Paginated audit activity for user (query: `page`, `pageSize`). |

- **No hard-delete endpoint** — only soft deactivate.

---

## Consistent Error Model (for FE forms)

All error responses use **ApiError** (JSON):

- **type:** `ValidationError` | `NotFound` | `Forbidden` | `Conflict` | `BusinessRule`
- **title:** Short message
- **status:** HTTP status (400, 404, 403, 409, 412)
- **detail:** Optional longer message
- **errors:** Optional `Dictionary<string, string[]>` for field-level validation (e.g. `{ "reason": ["Deactivation reason is required for audit compliance."] }`)

Example (400 validation):

```json
{
  "type": "ValidationError",
  "title": "Reason required",
  "status": 400,
  "errors": {
    "reason": ["Deactivation reason is required for audit compliance."]
  }
}
```

Example (412 concurrency):

```json
{
  "type": "Conflict",
  "title": "The resource was modified by another request. Please refresh and try again.",
  "status": 412
}
```

---

## Safe DTO (AdminUserDto)

- **id, userName, email, firstName, lastName, employeeNumber, role, taxNumber, notes**
- **isActive, createdAt, lastLoginAt, updatedAt, deactivatedAt**
- **etag** — use as `If-Match` on PATCH for optimistic concurrency

No password hash or other secrets.

---

## Session invalidation

- **IUserSessionInvalidation.InvalidateSessionsForUserAsync(userId)** is called on:
  - Deactivate
  - Force password reset
  - PATCH when role changes
- Current implementation: **StubUserSessionInvalidation** (logs only). When RefreshToken table exists, replace with real implementation that revokes tokens.

---

## Test cases (unit)

- **ApiError:** Validation (400, type ValidationError), NotFound (404), ConcurrencyConflict (412)
- **GetById:** 404 when user not found; 200 with safe DTO (no secrets, etag present)
- **Deactivate:** 400 when reason empty; 404 when user not found
- **Patch:** 412 when If-Match does not match ConcurrencyStamp
- **Reactivate:** 400 when user already active
- **ForcePasswordReset:** 400 when newPassword too short
- **GetActivity:** 404 when user not found

Run: `dotnet test --filter "FullyQualifiedName~AdminUsersControllerTests"`
