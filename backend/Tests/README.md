# BMF Prüftool test assets

This folder holds **local-only** BMF verification JARs and **committed** JSON fixtures for DEP export checks.

## Committed to git

| Path | Purpose |
|------|---------|
| `fixtures/prueftool/dep-export.json` | Deterministic DEP export (3 RKSV §9 receipts) |
| `fixtures/prueftool/crypto-material.json` | Dev-only AES key + signing cert for Prüftool |
| `fixtures/prueftool/README.md` | Fixture usage |

Regenerate fixtures:

```powershell
.\scripts\generate-dep-export-fixtures.ps1
```

## Not committed (`.gitignore`)

| Path | Purpose |
|------|---------|
| `lib/*.jar` | BMF Prüftool dependency JARs (~19 MB) |
| `regkassen-verification-depformat-1.1.1.jar` | DEP format checker (main entry) |
| `regkassen-verification-receipts-1.1.1.jar` | Single-receipt checker (optional) |
| `_jar_*/`, `_bmf_example/` | Local JAR extract / reference scratch |
| `verification_output/` (repo root) | Prüftool run output |

Obtain JARs from the official BMF RKSV test suite distribution (same bundle that ships `regkassen-verification-depformat-1.1.1.jar` and `lib/`). Place:

- `regkassen-verification-depformat-1.1.1.jar` → `backend/Tests/`
- all dependency JARs → `backend/Tests/lib/`

## Verify (JDK 17+ required)

Java 8 cannot decrypt AES-256 turnover counters; use JDK 17+.

```powershell
.\scripts\verify-rksv-dep-export.ps1 -UseFixtures
```

The script auto-detects Microsoft OpenJDK 17 when installed; override with `$env:PRUEFTOOL_JAVA`.
