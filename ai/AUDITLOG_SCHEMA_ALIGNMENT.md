# AuditLog Schema Alignment – Root Cause and Fix

## 1. Root cause

- **Symptom:** `PUT /api/UserManagement/{id}` and `GET /api/AuditLog/user/{id}` return 500.
- **Exception:** `Npgsql.PostgresException: 42703: column "Amount" of relation "audit_logs" does not exist`
- **Cause:** The physical PostgreSQL table `audit_logs` was created with a schema that does not match the AuditLog entity mapping (e.g. created earlier with default/lowercase column names, or by an older migration). EF Core inserts use quoted PascalCase column names (`"Amount"`, `"Action"`, etc.). If the table has lowercase columns (`amount`, `action`) or is missing columns, INSERT/SELECT fail.
- **Why it persisted:** `CREATE TABLE IF NOT EXISTS` in migrations and startup only runs when the table is missing. If `audit_logs` already existed with a different schema, that DDL never ran again, so the schema was never updated.

## 2. Schema mismatch (entity vs actual DB)

**Entity/EF mapping expects (AppDbContext + AuditLog model):**

| Column (quoted in DB) | Type / notes |
|------------------------|--------------|
| id | uuid (PK), from BaseEntity |
| created_at | timestamptz |
| created_by | varchar(450) |
| updated_at | timestamptz |
| updated_by | varchar(450) |
| is_active | boolean |
| "Action" | varchar(50) |
| "Amount" | numeric(18,2) |
| "CorrelationId" | varchar(100) |
| "Description" | varchar(500) |
| "Endpoint" | varchar(100) |
| "EntityId" | uuid |
| "EntityName" | varchar(100) |
| "EntityType" | varchar(100) |
| "ErrorDetails" | varchar(500) |
| "HttpMethod" | varchar(10) |
| "HttpStatusCode" | integer |
| "IpAddress" | varchar(45) |
| "NewValues" | varchar(4000) |
| "Notes" | varchar(500) |
| "OldValues" | varchar(4000) |
| "PaymentMethod" | varchar(50) |
| "ProcessingTimeMs" | double precision |
| "RequestData" | varchar(4000) |
| "ResponseData" | varchar(4000) |
| "SessionId" | varchar(100) |
| "Status" | integer (enum) |
| "Timestamp" | timestamptz |
| "TransactionId" | varchar(100) |
| "TseSignature" | varchar(500) |
| "UserAgent" | varchar(500) |
| "UserId" | varchar(450) |
| "UserRole" | varchar(50) |

**Actual DB (before fix):** Columns were either missing or named differently (e.g. lowercase `amount` instead of `"Amount"`), so EF’s INSERT/SELECT failed.

## 3. Decision

- **Chosen approach:** Align the database to the entity by dropping and recreating `audit_logs` in a single migration with the exact DDL that matches the entity (quoted PascalCase columns). No backward compatibility required; entity and code stay the single source of truth.

## 4. Affected files

- `backend/Migrations/20260308204523_AlignAuditLogsTableWithEntity.cs` – new migration (DROP + CREATE audit_logs).
- `backend/Migrations/20260308204523_AlignAuditLogsTableWithEntity.Designer.cs` – generated Designer (unchanged logic).
- `backend/Program.cs` – removed duplicate `ExecuteSqlRawAsync` CREATE TABLE block; schema is now only applied via migrations.

- `backend/Controllers/UserManagementController.cs` – error isolation: all `LogUserLifecycleAsync` calls go through `TryLogUserLifecycleAsync`, which catches audit failures and logs them without failing the primary operation (user update/create/deactivate/etc.).

No changes to:

- `backend/Models/AuditLog.cs`
- `backend/Data/AppDbContext.cs` (AuditLog configuration already correct)
- `backend/Services/AuditLogService.cs`
- `backend/Controllers/AuditLogController.cs`

## 5. Migration name

- **Migration:** `AlignAuditLogsTableWithEntity`
- **File:** `20260308204523_AlignAuditLogsTableWithEntity.cs`

## 6. Commands to run

From repo root or backend folder:

```bash
cd backend
dotnet ef database update
```

Or start the app; if `Migrate()` runs on startup, the new migration will be applied then.

## 7. Verification steps

1. Apply migrations: `dotnet ef database update` (from `backend`).
2. Put a user: `PUT /api/UserManagement/{id}` with a valid body; expect 200, no 500.
3. Get user audit logs: `GET /api/AuditLog/user/{userId}`; expect 200 and a list (possibly empty).
4. Optionally inspect table:  
   `SELECT column_name FROM information_schema.columns WHERE table_name = 'audit_logs' ORDER BY ordinal_position;`  
   Confirm quoted PascalCase columns (`"Amount"`, `"Action"`, etc.) exist.

## 8. Error isolation (user update vs audit log)

- **Policy:** Primary operation (user update, create, deactivate, reactivate, password reset, delete) must not fail because of audit log write failure. Audit is best-effort for traceability.
- **Implementation:** In `UserManagementController`, a private helper `TryLogUserLifecycleAsync` wraps `_auditLogService.LogUserLifecycleAsync`. On exception it logs (Error level, with Action, TargetUserId, ActorUserId) and does not rethrow. All user lifecycle audit call sites in this controller use the helper.
- **Result:** If `audit_logs` insert fails (e.g. schema still wrong before migration, or transient DB error), the API still returns 200 for the user operation; the failure is visible in logs for ops follow-up.

## 9. Success criteria (met after migration + isolation)

- PUT /api/UserManagement/{id} no longer returns 500 (schema fixed; and if audit still fails, response remains 200).
- GET /api/AuditLog/user/{id} no longer returns 500.
- Inserts into `audit_logs` succeed once migration is applied.
- Audit log write failure does not cause user update/create/deactivate/etc. to return 500; failure is logged only.
- AuditLog entity and `audit_logs` table schema are aligned after migration.
