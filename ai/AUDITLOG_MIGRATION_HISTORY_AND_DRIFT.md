# audit_logs Table: Migration History and Drift Fix

## 1. Where audit_logs appears in existing migrations

| Migration | Role | Notes |
|-----------|------|--------|
| **20260308175048_AddAuditLogsTable** | Originally: raw SQL `CREATE TABLE IF NOT EXISTS audit_logs` with quoted PascalCase columns. **Now: no-op** so DDL lives in one place only. |
| **20260308200033_EnsureAuditLogsTable** | Originally: same `CREATE TABLE IF NOT EXISTS audit_logs`. **Now: no-op.** |
| **20260308204523_AlignAuditLogsTableWithEntity** | **Single source of DDL:** `DROP TABLE IF EXISTS audit_logs CASCADE` then `CREATE TABLE audit_logs (...)` with all columns and indexes. This is the only migration that creates/aligns the table. |

Older migrations (e.g. 20260307204756, 20260304143812) only reference `AuditLog` in the **Designer** (model snapshot) with `ToTable("AuditLogs")` and no explicit column names. **No migration Up() ever used EF CreateTable to create the AuditLogs/audit_logs table** before AddAuditLogsTable.

## 2. Historical origin of the audit_logs table

- **First DDL that could create the table:** AddAuditLogsTable (20260308175048), then EnsureAuditLogsTable (20260308200033). Both used `CREATE TABLE IF NOT EXISTS`, so if the table already existed (e.g. wrong schema from manual creation or another source), they did nothing and **drift persisted**.
- **Definitive fix:** AlignAuditLogsTableWithEntity (20260308204523) drops and recreates the table so schema always matches the entity/AppDbContext.

## 3. Drift: current entity vs current DB schema

- **Entity + AppDbContext:** Table name `audit_logs`; BaseEntity columns `id`, `created_at`, `updated_at`, `created_by`, `updated_by`, `is_active`; AuditLog columns quoted PascalCase (`"Action"`, `"Amount"`, `"UserId"`, etc.).
- **DB after correct migration:** Must have exactly those column names (PostgreSQL stores quoted identifiers as given). If the table was ever created with unquoted or default naming, columns would be lowercase and **42703: column "Amount" does not exist** (and similar) would occur.
- **Drift:** Any existing `audit_logs` (or `AuditLogs`) table created without quoted PascalCase columns does not match the entity → INSERT/SELECT fail → users feature 500.

## 4. Fix applied (no backward compatibility)

1. **AddAuditLogsTable** and **EnsureAuditLogsTable** Up/Down are now **no-ops**. They remain in the migration chain for history but no longer run any DDL. This removes the “IF NOT EXISTS” path that could leave a wrong schema in place.
2. **AlignAuditLogsTableWithEntity** is the **only** migration that creates the `audit_logs` table. Its Up() does:
   - `DROP TABLE IF EXISTS audit_logs CASCADE`
   - `CREATE TABLE audit_logs (...)` with all columns and indexes matching AppDbContext
3. **Result:** After `dotnet ef database update`, the table always matches the entity; no drift, no workaround.

## 5. Commands and verification

```bash
cd backend
dotnet ef database update
```

Then:

- **PUT /api/UserManagement/{id}** and **GET /api/AuditLog/user/{id}** should return 200 (no 500 from audit_logs).
- Optionally check columns:  
  `SELECT column_name FROM information_schema.columns WHERE table_name = 'audit_logs' ORDER BY ordinal_position;`  
  Expect: `id`, `created_at`, `created_by`, `updated_at`, `updated_by`, `is_active`, then quoted PascalCase columns.

## 6. Files changed

- `backend/Migrations/20260308175048_AddAuditLogsTable.cs` – Up/Down made no-op.
- `backend/Migrations/20260308200033_EnsureAuditLogsTable.cs` – Up/Down made no-op.
- `ai/AUDITLOG_MIGRATION_HISTORY_AND_DRIFT.md` – this document.

No change to entity, AppDbContext, or AlignAuditLogsTableWithEntity.
