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

See `backend/KasseAPI_Final.Tests/PostgreSqlReplayFixture.cs` for details.
