# BMF Prüftool DEP export fixtures

Deterministic test files for `scripts/verify-rksv-dep-export.ps1`.

| File | Purpose |
|------|---------|
| `dep-export.json` | BMF `Belege-Gruppe` export (3 chained RKSV §9 receipts) |
| `crypto-material.json` | BMF `cryptographicMaterialContainer` (AES key + signing cert) |
| `qr-code-rep.json` | BMF QR wire strings for `CheckSingleReceipt` (§9 machine code + JWS) |

**Not production secrets** — fixed dev-only key material for CI/local Prüftool runs.

## Regenerate

```powershell
.\scripts\generate-dep-export-fixtures.ps1
```

Or:

```bash
cd backend && dotnet test --filter "RksvDepPrueftoolFixtureTests"
```

## Verify (JDK 17+ required)

DEP export:

```powershell
.\scripts\verify-rksv-dep-export.ps1 -UseFixtures
```

Receipt QR (CheckSingleReceipt):

```powershell
.\scripts\verify-rksv-receipt-qr.ps1 -UseFixtures
```

Manual paths:

```powershell
.\scripts\verify-rksv-dep-export.ps1 `
  -DepExportPath "backend/Tests/fixtures/prueftool/dep-export.json" `
  -CryptoMaterialPath "backend/Tests/fixtures/prueftool/crypto-material.json"
```
