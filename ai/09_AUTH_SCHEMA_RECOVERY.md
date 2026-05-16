# Auth Schema Recovery (auth_sessions / refresh_tokens)

## Symptom

- POS/Admin login fails with:
  - `PostgresException 42P01: relation "auth_sessions" does not exist`

## Multi-tenant note

- Login JWT includes `tenant_id` when membership/bootstrap resolves a tenant (`LoginTenantResolver`, `UserTenantMembership`).
- Auth recovery does not replace tenant middleware; after login, requests still need correct host or dev `X-Tenant-Id`.

## Why it happens

- Runtime backend issues refresh tokens on login and writes to:
  - `auth_sessions`
  - `refresh_tokens`
- But the active database does not have the migration applied, or app points to a different database than expected.

## Quick recovery steps (local/dev)

1. Confirm migration exists in code:

```powershell
dotnet ef migrations list --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj
```

Expected: `20260325120000_AddAuthSessionsAndRefreshTokens` appears.

2. Apply migrations to runtime DB:

```powershell
dotnet ef database update --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj
```

3. Verify migration history in PostgreSQL:

```sql
select migrationid
from "__EFMigrationsHistory"
order by migrationid desc;
```

Expected: includes `20260325120000_AddAuthSessionsAndRefreshTokens`.

4. Verify required tables:

```sql
select to_regclass('public.auth_sessions') as auth_sessions,
       to_regclass('public.refresh_tokens') as refresh_tokens;
```

Expected: both are not null.

5. Verify backend readiness endpoint:

```powershell
curl http://localhost:5183/health/auth-schema
```

Expected:
- `200` when both tables exist
- `503` when schema is missing

## Connection-string mismatch checklist

- Check active runtime `ConnectionStrings:DefaultConnection`.
- Check environment variable overrides (if used in your shell/launch profile).
- Ensure `dotnet ef database update` targets the same startup project and environment as runtime.

## Fail-fast behavior

- Backend startup now fails fast if:
  - pending migrations exist, or
  - critical auth tables are missing.
- This prevents late runtime failure during login.
