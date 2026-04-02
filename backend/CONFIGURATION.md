# Backend configuration (secrets and environment)

Technical documentation (English). Do not commit real secrets; `appsettings.json` and `appsettings.*.json` under `backend/` are gitignored by design.

## Required secrets (local, staging, production)

| Setting | JSON path | Environment variable (override) | Notes |
|--------|------------|-----------------------------------|--------|
| PostgreSQL | `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Npgsql connection string; never log unmasked. |
| JWT signing key | `JwtSettings:SecretKey` | `JwtSettings__SecretKey` | Minimum 32 characters; rotate if ever exposed. |
| JWT issuer / audience | `JwtSettings:Issuer`, `JwtSettings:Audience` | `JwtSettings__Issuer`, `JwtSettings__Audience` | Non-secret values may live in example config. |

FinanzOnline **user/password** for SOAP are expected from **company settings in the database** (or optional `FinanzOnline:Session` binding in non-tracked config). Do not put production FinanzOnline credentials in tracked files.

Optional **production cutover** token: `FinanzOnline:CutoverGuard:ProdApprovalToken` → `FinanzOnline__CutoverGuard__ProdApprovalToken` (set only when your runbook requires it).

## Local development (recommended)

1. Copy templates (once):

   ```bash
   cp appsettings.example.json appsettings.json
   cp appsettings.Development.example.json appsettings.Development.json
   ```

2. Set **secrets** with the user-secrets store (values are **not** written to disk in the project folder). Tracked `appsettings*.json` templates intentionally omit `ConnectionStrings` and `JwtSettings:SecretKey`; those must come from user secrets or environment variables.

   ```bash
   cd backend
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=YOUR_PASSWORD"
   dotnet user-secrets set "JwtSettings:SecretKey" "YOUR_RANDOM_KEY_AT_LEAST_32_CHARS"
   ```

The project defines `<UserSecretsId>` in `KasseAPI_Final.csproj` so `dotnet user-secrets` targets this API automatically.

Alternative: set the same keys as **User** or **Machine** environment variables (names use `__` for `:`).

### Backup & DR (optional — RealPgDump selectable in Development)

Repo defaults keep `Backup:ExecutionAdapterKind` = **Fake** and staging/archive roots empty so hypothetical PgDump health stays **Unhealthy** until you configure local paths. **Do not** commit machine-specific directories to tracked `appsettings*.json`; use **user secrets** or environment variables.

| Setting (JSON) | Environment variable | Notes |
|----------------|----------------------|--------|
| `Backup:ArtifactStagingRoot` | `Backup__ArtifactStagingRoot` | Absolute directory; required for PgDump hypothetical health to clear the missing-staging blocker. |
| `Backup:ExternalArchiveRoot` | `Backup__ExternalArchiveRoot` | Absolute directory; in Development avoids degraded readiness when evaluating PgDump; non-Development it is mandatory for PgDump. |

**Quick setup (Windows, from `backend/`):** run `.\scripts\setup-backup-dr-dev-secrets.ps1` (creates `C:\data\regkasse-backup-staging` and `C:\data\regkasse-backup-archive` and writes the two secrets). Restart the API. Leave `Backup:ExecutionAdapterKind` as **Fake** until you switch mode in the admin Backup & DR UI.

Equivalent manual commands:

```bash
cd backend
dotnet user-secrets set "Backup:ArtifactStagingRoot" "C:\data\regkasse-backup-staging"
dotnet user-secrets set "Backup:ExternalArchiveRoot" "C:\data\regkasse-backup-archive"
```

`ConnectionStrings:DefaultConnection` is already required for API startup; `Backup:VerifyLogicalDumpFileOnDisk` defaults to `true`. Set `Backup:PgDumpExecutablePath` only if `pg_dump` is not on `PATH`. See `docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md`.

## `dotnet ef` and design-time DbContext

The design-time factory loads, in order: optional `appsettings.json`, optional `appsettings.Development.json`, user secrets, then environment variables. CI agents without local JSON files should set `ConnectionStrings__DefaultConnection`.

## Manual rotation after exposure

If a password or JWT key was ever stored in a tracked file, shared screenshot, or chat, **rotate it manually** (this repo does not rotate secrets for you):

- PostgreSQL user password (or least-privilege DB user).
- `JwtSettings:SecretKey` (invalidates existing JWTs).
- Any FinanzOnline password stored in plaintext outside the intended secure store.

## Logging

Connection strings printed at startup use `ConnectionStringMasking`; raw passwords must not appear in logs. Do not enable verbose logging of HTTP bodies or SOAP envelopes that might contain session tokens in production without redaction.
