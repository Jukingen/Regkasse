# audit_logs Schema Diff (Entity vs DB)

## Root cause of 500

- **INSERT/SELECT** use column names from EF mapping (AppDbContext): PascalCase for AuditLog-specific columns (`"Action"`, `"Amount"`, …), snake_case for base (`id`, `created_at`, …).
- **PostgreSQL**: Unquoted identifiers are lowercased. If the table was created by an older migration or raw SQL **without** quoted names, actual columns are lowercase (`action`, `amount`, …). Then EF sends `"Amount"` → 42703 column "Amount" does not exist.

## 1. Columns: Entity + EF mapping (what INSERT/SELECT use)

| Property (C#) | EF ColumnName (AppDbContext) | Type |
|---------------|-----------------------------|------|
| Id | id | uuid |
| CreatedAt | created_at | timestamptz |
| UpdatedAt | updated_at | timestamptz |
| CreatedBy | created_by | varchar(450) |
| UpdatedBy | updated_by | varchar(450) |
| IsActive | is_active | boolean |
| SessionId | SessionId | varchar(100) |
| UserId | UserId | varchar(450) |
| UserRole | UserRole | varchar(50) |
| Action | Action | varchar(50) |
| EntityType | EntityType | varchar(100) |
| EntityId | EntityId | uuid |
| EntityName | EntityName | varchar(100) |
| OldValues | OldValues | varchar(4000) |
| NewValues | NewValues | varchar(4000) |
| RequestData | RequestData | varchar(4000) |
| ResponseData | ResponseData | varchar(4000) |
| Status | Status | int |
| Timestamp | Timestamp | timestamptz |
| Description | Description | varchar(500) |
| Notes | Notes | varchar(500) |
| IpAddress | IpAddress | varchar(45) |
| UserAgent | UserAgent | varchar(500) |
| Endpoint | Endpoint | varchar(100) |
| HttpMethod | HttpMethod | varchar(10) |
| HttpStatusCode | HttpStatusCode | int |
| ProcessingTimeMs | ProcessingTimeMs | float8 |
| ErrorDetails | ErrorDetails | varchar(500) |
| CorrelationId | CorrelationId | varchar(100) |
| TransactionId | TransactionId | varchar(100) |
| Amount | Amount | decimal(18,2) |
| PaymentMethod | PaymentMethod | varchar(50) |
| TseSignature | TseSignature | varchar(500) |

EF generates SQL with **quoted** PascalCase for these (e.g. `"Action"`, `"Amount"`), so the **table must have the same quoted PascalCase column names**.

## 2. Original table (FixRelationshipMappings – 20250814051845)

Created with `CreateTable(name: "audit_logs", columns: ...)`:

| Column in migration | Type (then) | In current entity? |
|--------------------|-------------|---------------------|
| id | uuid | ✓ Id |
| Action | varchar(50) | ✓ |
| EntityType | varchar(100) | ✓ |
| EntityId | **varchar(100)** | ✓ but now **uuid** in entity |
| UserId | varchar(450) | ✓ |
| UserName | varchar(100) | ✗ (entity has EntityName, UserRole; no UserName) |
| OldValues | varchar(4000) | ✓ |
| NewValues | varchar(4000) | ✓ |
| IpAddress | varchar(45) | ✓ |
| UserAgent | varchar(500) | ✓ |
| Timestamp | timestamptz | ✓ |
| created_at | timestamptz | ✓ |
| updated_at | timestamptz | ✓ |
| created_by | varchar(450) | ✓ |
| updated_by | varchar(450) | ✓ |
| is_active | boolean | ✓ |

**Missing in old table:** SessionId, UserRole, Status, Description, Notes, Endpoint, HttpMethod, HttpStatusCode, RequestData, ResponseData, ErrorDetails, CorrelationId, TransactionId, Amount, PaymentMethod, TseSignature, EntityName, ProcessingTimeMs.

**Case:** Depending on Npgsql migration SQL generation, columns may have been created **unquoted** → stored as **lowercase** in PostgreSQL (e.g. `action`, `amount` missing). That causes 42703 when EF uses `"Action"`, `"Amount"`.

## 3. Align migration (20260308204523_AlignAuditLogsTableWithEntity)

- **Up:** `DROP TABLE IF EXISTS audit_logs CASCADE` then `CREATE TABLE audit_logs (...)` with:
  - Base: `id`, `created_at`, `created_by`, `updated_at`, `updated_by`, `is_active` (lowercase, no quotes in script so PG stores lowercase).
  - All AuditLog-specific columns **quoted PascalCase** (`"Action"`, `"Amount"`, `"SessionId"`, …), so PG stores them case-sensitive and they match EF.

This is the **single source of DDL**; AddAuditLogsTable and EnsureAuditLogsTable are no-ops.

## 4. Diff summary

| Source | Column names used in SQL | Match with EF? |
|--------|--------------------------|----------------|
| Entity + AppDbContext | id, created_at, … (snake_case); "Action", "Amount", … (quoted PascalCase) | — |
| FixRelationshipMappings table | Likely lowercase + missing columns | ❌ No |
| AlignAuditLogsTableWithEntity table | Same as entity mapping | ✓ Yes |

## 5. Fix applied (permanent)

- **Entity/mapping:** All AuditLog columns in AppDbContext use **snake_case** (`action`, `amount`, `session_id`, …). No quoted PascalCase; PostgreSQL unquoted identifiers match.
- **Migration:** `AuditLogsSnakeCaseSchema` (20260308210523) does `DROP TABLE IF EXISTS audit_logs CASCADE` then `CREATE TABLE audit_logs (...)` with snake_case columns and indexes. Works whether the previous table had PascalCase (Align) or old/incomplete schema.
- **No try/catch or turning off audit:** Fix is schema alignment only.

## 6. Verification after migration

- PUT /api/UserManagement/{id} (user update) → 200, audit row inserted.
- GET /api/AuditLog/user/{userId}?page=1&pageSize=10 → 200, list or empty list, no 500.
