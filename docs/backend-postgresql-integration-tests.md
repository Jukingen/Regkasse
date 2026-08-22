# Backend PostgreSQL integration tests (CI)

## What runs in CI

Workflow: `.github/workflows/backend-postgres-integration-tests.yml` (on PR / push to `main` when `backend/**` changes).

- Spins up a **GitHub Actions `services.postgres`** container (`postgres:16-alpine`).
- Sets **`REGKASSE_TEST_POSTGRES`** to a localhost connection string (ephemeral DB user/password in the workflow only).
- Runs:

```bash
cd backend
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "Category=PostgreSql"
```

`PostgreSqlReplayFixture` sees `REGKASSE_TEST_POSTGRES`, runs migrations, and tests execute (they should **not** all skip).

## Local

- **Docker:** with `REGKASSE_TEST_POSTGRES` **unset**, the fixture uses **Testcontainers** (`postgres:16-alpine`) — start Docker, then run the same `dotnet test` command.
- **Existing Postgres:** set `REGKASSE_TEST_POSTGRES` to a connection string; the fixture runs `Database.MigrateAsync()` on that database.

### Local Postgres without Docker (PowerShell)

Point the fixture at a **dedicated scratch database** — never at `kasse_db`. The database does not need to exist; `MigrateAsync()` creates it.

```powershell
$env:REGKASSE_TEST_POSTGRES = "Host=localhost;Port=5432;Database=regkasse_pg_integration;Username=postgres;Password=<password>"
cd backend
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "Category=PostgreSql"
```

The name `regkasse_pg_integration` makes `PostgreSqlReplayFixture` drop and recreate the schema on every run (see `ShouldResetIntegrationDatabase`). For any other database name, set `REGKASSE_TEST_POSTGRES_RESET=1` to force the same one-shot reset.

### Verifying the fixture is actually running

Skipped tests are the failure mode to watch for: when the fixture cannot reach a database it sets a skip reason and **every** test in this category is skipped, so the workflow reports green while covering nothing. Check the skip count, not just the exit code:

```powershell
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "Category=PostgreSql" --logger "console;verbosity=detailed"
```

A skip reason of the form `REGKASSE_TEST_POSTGRES migrate failed: PostgreSQL integration schema is missing …` means the migration chain does not reproduce the expected schema on an empty database — fix the migrations rather than the assertion, because a fresh production or disaster-recovery database would have the same gap.

See `backend/KasseAPI_Final.Tests/PostgreSqlReplayFixture.cs` for details.
