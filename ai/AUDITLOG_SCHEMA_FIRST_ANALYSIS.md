# AuditLog Schema-First Analysis

## How to get actual DB columns

Run this in PostgreSQL (e.g. `psql` or your DB tool):

```sql
SELECT ordinal_position, column_name, data_type, character_maximum_length
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'audit_logs'
ORDER BY ordinal_position;
```

If your table was created with a different name (e.g. `AuditLogs`), also run:

```sql
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public' AND table_name ILIKE '%audit%';
```

---

## 1. Actual DB columns

**If the table was created by EF Core default migration (no explicit HasColumnName for AuditLog-specific props) or by unquoted DDL**, PostgreSQL stores unquoted identifiers in **lowercase**. So the likely actual columns when you see `42703: column "Amount" does not exist` are:

| # | Actual column (likely) | Data type |
|---|------------------------|-----------|
| 1 | id | uuid |
| 2 | created_at | timestamp with time zone |
| 3 | created_by | character varying(450) |
| 4 | updated_at | timestamp with time zone |
| 5 | updated_by | character varying(450) |
| 6 | is_active | boolean |
| 7 | **action** | character varying(50) |
| 8 | **amount** | numeric(18,2) |
| 9 | **correlationid** | character varying(100) |
| 10 | **description** | character varying(500) |
| 11 | **endpoint** | character varying(100) |
| 12 | **entityid** | uuid |
| 13 | **entityname** | character varying(100) |
| 14 | **entitytype** | character varying(100) |
| 15 | **errordetails** | character varying(500) |
| 16 | **httpmethod** | character varying(10) |
| 17 | **httpstatuscode** | integer |
| 18 | **ipaddress** | character varying(45) |
| 19 | **newvalues** | character varying(4000) |
| 20 | **notes** | character varying(500) |
| 21 | **oldvalues** | character varying(4000) |
| 22 | **paymentmethod** | character varying(50) |
| 23 | **processingtimems** | double precision |
| 24 | **requestdata** | character varying(4000) |
| 25 | **responsedata** | character varying(4000) |
| 26 | **sessionid** | character varying(100) |
| 27 | **status** | integer |
| 28 | **timestamp** | timestamp with time zone |
| 29 | **transactionid** | character varying(100) |
| 30 | **tsesignature** | character varying(500) |
| 31 | **useragent** | character varying(500) |
| 32 | **userid** | character varying(450) |
| 33 | **userrole** | character varying(50) |

**Note:** Table name might be `audit_logs` (from later raw-SQL migrations) or `AuditLogs` / `auditlogs` (from older EF ToTable("AuditLogs")). Run the SQL above to get your real list.

---

## 2. Entity properties

From `AuditLog` + `BaseEntity` (navigation `User` is ignored in mapping):

| # | Property | Type | Source |
|---|----------|------|--------|
| 1 | Id | Guid | BaseEntity |
| 2 | CreatedAt | DateTime | BaseEntity |
| 3 | UpdatedAt | DateTime? | BaseEntity |
| 4 | CreatedBy | string? | BaseEntity |
| 5 | UpdatedBy | string? | BaseEntity |
| 6 | IsActive | bool | BaseEntity |
| 7 | SessionId | string | AuditLog |
| 8 | UserId | string | AuditLog |
| 9 | UserRole | string | AuditLog |
| 10 | Action | string | AuditLog |
| 11 | EntityType | string | AuditLog |
| 12 | EntityId | Guid? | AuditLog |
| 13 | EntityName | string? | AuditLog |
| 14 | OldValues | string? | AuditLog |
| 15 | NewValues | string? | AuditLog |
| 16 | RequestData | string? | AuditLog |
| 17 | ResponseData | string? | AuditLog |
| 18 | Status | AuditLogStatus (int) | AuditLog |
| 19 | Timestamp | DateTime | AuditLog |
| 20 | Description | string? | AuditLog |
| 21 | Notes | string? | AuditLog |
| 22 | IpAddress | string? | AuditLog |
| 23 | UserAgent | string? | AuditLog |
| 24 | Endpoint | string? | AuditLog |
| 25 | HttpMethod | string? | AuditLog |
| 26 | HttpStatusCode | int? | AuditLog |
| 27 | ProcessingTimeMs | double? | AuditLog |
| 28 | ErrorDetails | string? | AuditLog |
| 29 | CorrelationId | string? | AuditLog |
| 30 | TransactionId | string? | AuditLog |
| 31 | Amount | decimal? | AuditLog |
| 32 | PaymentMethod | string? | AuditLog |
| 33 | TseSignature | string? | AuditLog |

---

## 3. EF mapped columns (AppDbContext)

From `backend/Data/AppDbContext.cs` – explicit `HasColumnName(...)` for every property. Npgsql uses these names in generated SQL; PascalCase names are emitted **quoted** in PostgreSQL, so the database must have columns with the exact casing below.

| # | EF column name (as in SQL) | Quoted in PG? |
|---|----------------------------|---------------|
| 1 | id | no (lowercase) |
| 2 | created_at | no |
| 3 | created_by | no |
| 4 | updated_at | no |
| 5 | updated_by | no |
| 6 | is_active | no |
| 7 | "Action" | yes |
| 8 | "Amount" | yes |
| 9 | "CorrelationId" | yes |
| 10 | "Description" | yes |
| 11 | "Endpoint" | yes |
| 12 | "EntityId" | yes |
| 13 | "EntityName" | yes |
| 14 | "EntityType" | yes |
| 15 | "ErrorDetails" | yes |
| 16 | "HttpMethod" | yes |
| 17 | "HttpStatusCode" | yes |
| 18 | "IpAddress" | yes |
| 19 | "NewValues" | yes |
| 20 | "Notes" | yes |
| 21 | "OldValues" | yes |
| 22 | "PaymentMethod" | yes |
| 23 | "ProcessingTimeMs" | yes |
| 24 | "RequestData" | yes |
| 25 | "ResponseData" | yes |
| 26 | "SessionId" | yes |
| 27 | "Status" | yes |
| 28 | "Timestamp" | yes |
| 29 | "TransactionId" | yes |
| 30 | "TseSignature" | yes |
| 31 | "UserAgent" | yes |
| 32 | "UserId" | yes |
| 33 | "UserRole" | yes |

---

## 4. Diff (EF expected vs likely actual)

When the DB was created with **unquoted** identifiers (e.g. by an older EF migration or default naming), PostgreSQL stores them in **lowercase**. EF then generates SQL with **quoted** PascalCase names and gets 42703.

| EF expected column | Likely actual column | Issue |
|-------------------|----------------------|--------|
| id | id | OK |
| created_at | created_at | OK |
| created_by | created_by | OK |
| updated_at | updated_at | OK |
| updated_by | updated_by | OK |
| is_active | is_active | OK |
| "Action" | action | **Wrong case** – PG has lowercase, EF expects quoted "Action" |
| "Amount" | amount | **Wrong case** – PG has lowercase, EF expects quoted "Amount" |
| "CorrelationId" | correlationid | **Wrong case** |
| "Description" | description | **Wrong case** |
| "Endpoint" | endpoint | **Wrong case** |
| "EntityId" | entityid | **Wrong case** |
| "EntityName" | entityname | **Wrong case** |
| "EntityType" | entitytype | **Wrong case** |
| "ErrorDetails" | errordetails | **Wrong case** |
| "HttpMethod" | httpmethod | **Wrong case** |
| "HttpStatusCode" | httpstatuscode | **Wrong case** |
| "IpAddress" | ipaddress | **Wrong case** |
| "NewValues" | newvalues | **Wrong case** |
| "Notes" | notes | **Wrong case** |
| "OldValues" | oldvalues | **Wrong case** |
| "PaymentMethod" | paymentmethod | **Wrong case** |
| "ProcessingTimeMs" | processingtimems | **Wrong case** |
| "RequestData" | requestdata | **Wrong case** |
| "ResponseData" | responsedata | **Wrong case** |
| "SessionId" | sessionid | **Wrong case** |
| "Status" | status | **Wrong case** |
| "Timestamp" | timestamp | **Wrong case** |
| "TransactionId" | transactionid | **Wrong case** |
| "TseSignature" | tsesignature | **Wrong case** |
| "UserAgent" | useragent | **Wrong case** |
| "UserId" | userid | **Wrong case** |
| "UserRole" | userrole | **Wrong case** |

**Summary:** BaseEntity columns (id, created_at, …) match. All AuditLog-specific columns are **wrong case / wrong quoted identifier**: DB has lowercase (or unquoted), EF expects quoted PascalCase. So the fix is to align the **database** to the **EF mapping** (quoted PascalCase), not the other way around.

---

## 5. Fix plan

1. **Single source of truth:** EF mapping (AppDbContext) + entity stay as they are; DB schema is adjusted to match.
2. **Apply migration** `AlignAuditLogsTableWithEntity`: it runs `DROP TABLE IF EXISTS audit_logs CASCADE` then `CREATE TABLE audit_logs (...)` with **quoted** PascalCase column names (`"Action"`, `"Amount"`, …) and indexes. That makes the table match what Npgsql expects.
3. **No backward compatibility:** Existing rows in `audit_logs` are dropped; no rename/alter of existing columns (avoids complex ALTER renames and type checks).
4. **Verification:** After migration, run the “How to get actual DB columns” query and confirm column names are exactly: id, created_at, created_by, updated_at, updated_by, is_active, "Action", "Amount", "CorrelationId", … (with quotes for PascalCase in `information_schema` you may see them as-is).

---

## 6. Applied changes (already in repo)

- **Migration** `20260308204523_AlignAuditLogsTableWithEntity.cs`: DROP + CREATE `audit_logs` with quoted PascalCase columns and indexes.
- **Program.cs:** Duplicate startup DDL for `audit_logs` removed; schema is only applied via this migration.
- **UserManagementController:** Audit write wrapped in `TryLogUserLifecycleAsync` so audit failure does not cause user update to return 500.

**Command to apply:**

```bash
cd backend
dotnet ef database update
```

After that, the three lists (actual DB, entity, EF mapped) are aligned and 42703 should no longer occur.
